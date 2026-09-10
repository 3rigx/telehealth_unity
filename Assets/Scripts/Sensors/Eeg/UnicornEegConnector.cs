using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.Sensors.Eeg
{
    /// <summary>One decoded EEG scan (250 Hz) — 8 channels in microvolts.</summary>
    public struct EegSample
    {
        public uint counter;   // device sample index (monotonic)
        public double tSec;     // host arrival time (seconds since acquisition start)
        public float[] eeg;     // 8 channels, microvolts
    }

    /// <summary>
    ///     License-free Unicorn Hybrid Black acquisition over its classic-Bluetooth SPP
    ///     serial port (no g.tec DLL / license). Opens the COM port, sends the start
    ///     command, and decodes the continuous 45-byte frame stream on a background
    ///     thread. Frame boundaries are found by COUNTER CONTINUITY (the per-sample
    ///     counter must increment by 1), and the counter's byte offset is AUTO-DETECTED,
    ///     so the parser self-calibrates and is robust to byte misalignment / dropouts.
    ///
    ///     NOTE: the values in the PROTOCOL CONSTANTS block below are the canonical
    ///     Unicorn raw-protocol values and must be verified against UnicornSuite.pdf
    ///     (the "data acquisition / Bluetooth data format" section). The empirical probe
    ///     (UnicornEegProbe) will reveal immediately if any are wrong (no lock = wrong
    ///     start command or counter decode; garbage µV = wrong scale or EEG byte order).
    /// </summary>
    public class UnicornEegConnector : IDisposable
    {
        // ===================== PROTOCOL CONSTANTS — VERIFY vs UnicornSuite.pdf ==========
        public const int FrameLength = 45;     // bytes per scan
        public const int ChannelCount = 8;     // EEG channels
        public const int SampleRateHz = 250;

        // Host -> device commands.
        private static readonly byte[] StartCmd = { 0x61, 0x7C, 0x87 };
        private static readonly byte[] StopCmd  = { 0x63, 0x5C, 0xC5 };

        // Confirmed Unicorn frame (45 bytes), from raw capture 2026-06-18:
        //   [0,1]    header  0xC0 0x00
        //   [2..25]  8 EEG channels × 3 bytes — 24-bit BIG-ENDIAN two's-complement
        //   [26..31] accelerometer x/y/z (int16)
        //   [32..37] gyroscope x/y/z (int16)
        //   [38]     battery
        //   [39..42] sample counter (uint32 little-endian)
        //   [43,44]  footer  0x0D 0x0A
        // We lock onto the counter (offset auto-detected) and read EEG 37 bytes before it,
        // so the EEG position self-corrects to whatever window alignment the lock produced.
        private const int CounterToEegBytes = 37;

        // Microvolts per LSB of the 24-bit sample. Affects ABSOLUTE amplitude only (not
        // signal structure). This default is DERIVED, not verified against the Recorder —
        // treat absolute µV as UNCALIBRATED until the procedure below is run. It is
        // overridable at runtime (no recompile) via the PlayerPrefs key "EegScaleUv",
        // which the dashboard settings can write.
        //
        // CALIBRATION PROCEDURE (needs the headset + Unicorn Recorder):
        //   1. Record the SAME signal in both Unicorn Recorder and this pipeline
        //      (e.g. eyes-closed occipital alpha, or a known cal/test input).
        //   2. Measure one stable feature amplitude in µV in each — e.g. alpha-band
        //      peak-to-peak: A_recorder and A_ours.
        //   3. newScale = EegScaleUv * (A_recorder / A_ours).
        //   4. Persist it: PlayerPrefs.SetFloat("EegScaleUv", newScale); PlayerPrefs.Save();
        //      (or edit DefaultScaleUv), then re-verify the two now agree.
        public const double DefaultScaleUv = 4500000.0 / 50331642.0; // ≈ 0.0894 µV/LSB
        public static double EegScaleUv = DefaultScaleUv;
        // ===============================================================================

        private SerialPort _port;
        private Thread _thread;
        private volatile bool _running;
        private readonly object _lock = new object();
        private readonly List<EegSample> _drain = new List<EegSample>();
        private System.Diagnostics.Stopwatch _clock;

        /// <summary>Detected byte offset of the uint32 little-endian sample counter.</summary>
        public int CounterOffset { get; private set; } = -1;
        public bool IsLocked => CounterOffset >= 0;
        public long FramesReceived { get; private set; }
        public long FramesDropped { get; private set; }
        public EegSample Latest { get; private set; }
        /// <summary>Raw bytes of the most recent decoded frame (diagnostics).</summary>
        public byte[] LatestRaw { get; private set; }
        public bool IsConnected => _port != null && _port.IsOpen;

        // A Bluetooth SPP COM port can exist (device paired) without the RFCOMM link
        // actually being up yet — opening it too early fails with a transient,
        // network-flavoured error ("The network location cannot be reached") that a
        // short retry usually clears. Called only once, synchronously, during session
        // start, so a bounded blocking retry here (not a background poll) is fine.
        private const int ConnectAttempts = 3;
        private const int ConnectRetryDelayMs = 500;

        public bool Connect(string portName)
        {
            for (int attempt = 1; attempt <= ConnectAttempts; attempt++)
            {
                try
                {
                    _port = new SerialPort(portName, 115200)
                    {
                        ReadTimeout = 1000,
                        WriteTimeout = 1000,
                    };
                    _port.Open();
                    Debug.Log($"[EEG] Opened {portName}" + (attempt > 1 ? $" (attempt {attempt}/{ConnectAttempts})" : ""));
                    return true;
                }
                catch (Exception e)
                {
                    _port = null;
                    if (attempt < ConnectAttempts)
                    {
                        Debug.LogWarning($"[EEG] Could not open {portName} (attempt {attempt}/{ConnectAttempts}): " +
                                          $"{e.Message} — retrying…");
                        Thread.Sleep(ConnectRetryDelayMs);
                    }
                    else
                    {
                        Debug.LogError($"[EEG] Could not open {portName} after {ConnectAttempts} attempts: {e.Message}");
                    }
                }
            }
            return false;
        }

        public void StartAcquisition()
        {
            if (_port == null || !_port.IsOpen)
            {
                Debug.LogWarning("[EEG] StartAcquisition called with no open port.");
                return;
            }
            try { _port.DiscardInBuffer(); } catch { }
            _port.Write(StartCmd, 0, StartCmd.Length);

            // Pick up a calibrated µV scale if one has been stored. Read here on the
            // main thread (before the decode thread starts) so the worker sees the
            // final value; absent/invalid → the derived default.
            var stored = PlayerPrefs.GetFloat("EegScaleUv", 0f);
            EegScaleUv = stored > 0f ? stored : DefaultScaleUv;
            Debug.Log($"[EEG] µV scale = {EegScaleUv:F6}/LSB " +
                      (stored > 0f ? "(calibrated override)" : "(derived default — uncalibrated)"));

            _clock = System.Diagnostics.Stopwatch.StartNew();
            _running = true;
            _thread = new Thread(ReadLoop) { IsBackground = true, Name = "UnicornEeg" };
            _thread.Start();
            Debug.Log("[EEG] Acquisition started.");
        }

        public void StopAcquisition()
        {
            _running = false;
            try { if (_port != null && _port.IsOpen) _port.Write(StopCmd, 0, StopCmd.Length); }
            catch { }
            try { _thread?.Join(750); } catch { }
            _thread = null;
        }

        /// <summary>Removes and returns all samples buffered since the last drain.</summary>
        public List<EegSample> Drain()
        {
            lock (_lock)
            {
                var copy = new List<EegSample>(_drain);
                _drain.Clear();
                return copy;
            }
        }

        // ── acquisition thread ──────────────────────────────────────────────────────

        private void ReadLoop()
        {
            var buf = new byte[FrameLength * 16];
            int have = 0;
            uint lastCounter = 0;

            while (_running)
            {
                int n;
                try { n = _port.Read(buf, have, buf.Length - have); }
                catch (TimeoutException) { continue; }
                catch (Exception e)
                {
                    // The Bluetooth link can drop mid-stream (same underlying instability
                    // that can also block the initial Connect()) — this used to fail
                    // completely silently, leaving "was connected, now isn't" with zero
                    // trace in Player.log. Log it, and explicitly close the port so
                    // IsConnected deterministically flips false right here instead of
                    // depending on ambiguous OS/.NET behaviour.
                    Debug.LogWarning($"[EEG] Read loop ended unexpectedly: {e.Message}");
                    _running = false;
                    try { _port?.Close(); } catch { }
                    break;
                }
                if (n <= 0) continue;
                have += n;

                int pos = 0;
                while (have - pos >= FrameLength)
                {
                    if (CounterOffset < 0)
                    {
                        // Not locked yet — need two consecutive frames to (a) find the
                        // counter offset and (b) confirm continuity. Defer until we have
                        // at least two frames buffered from this position.
                        if (have - pos < FrameLength * 2) break;
                        int off = DetectCounterOffset(buf, pos);
                        if (off >= 0)
                        {
                            CounterOffset = off;
                            lastCounter = ReadCounter(buf, pos, off);
                            EmitFrame(buf, pos, lastCounter);
                            pos += FrameLength;
                            Debug.Log($"[EEG] Locked. Counter offset = {off}.");
                        }
                        else
                        {
                            pos += 1; // slide and retry
                        }
                        continue;
                    }

                    uint counter = ReadCounter(buf, pos, CounterOffset);
                    if (counter == lastCounter + 1 || (lastCounter != 0 && counter == 0))
                    {
                        EmitFrame(buf, pos, counter);
                        lastCounter = counter;
                        pos += FrameLength;
                    }
                    else if (counter == lastCounter)
                    {
                        // duplicate — skip without counting as a new sample
                        pos += FrameLength;
                    }
                    else
                    {
                        // Continuity broken: either dropped samples or byte slip.
                        long gap = (long)counter - lastCounter - 1;
                        if (gap > 0 && gap < SampleRateHz * 5)
                        {
                            // Plausible dropout (BT hiccup) — accept and account for it.
                            FramesDropped += gap;
                            EmitFrame(buf, pos, counter);
                            lastCounter = counter;
                            pos += FrameLength;
                        }
                        else
                        {
                            // Implausible — assume byte misalignment, re-lock.
                            CounterOffset = -1;
                            pos += 1;
                        }
                    }
                }

                if (pos > 0)
                {
                    Array.Copy(buf, pos, buf, 0, have - pos);
                    have -= pos;
                }
                if (have >= buf.Length) have = 0; // overflow safety valve
            }
        }

        /// <summary>
        ///     Finds the uint32 LE offset whose value at frame[pos] and frame[pos+45]
        ///     differ by exactly 1 — i.e. the sample counter. Returns -1 if none.
        /// </summary>
        private static int DetectCounterOffset(byte[] buf, int pos)
        {
            for (int off = 0; off + 4 <= FrameLength; off++)
            {
                uint a = ReadCounter(buf, pos, off);
                uint b = ReadCounter(buf, pos + FrameLength, off);
                if (b == a + 1 && a < uint.MaxValue - 1) return off;
            }
            return -1;
        }

        private static uint ReadCounter(byte[] buf, int pos, int off)
        {
            return (uint)(buf[pos + off]
                          | (buf[pos + off + 1] << 8)
                          | (buf[pos + off + 2] << 16)
                          | (buf[pos + off + 3] << 24));
        }

        private void EmitFrame(byte[] buf, int pos, uint counter)
        {
            var eeg = new float[ChannelCount];
            int eegOff = pos + CounterOffset - CounterToEegBytes;
            for (int ch = 0; ch < ChannelCount; ch++)
            {
                int o = eegOff + ch * 3;
                int raw = (buf[o] << 16) | (buf[o + 1] << 8) | buf[o + 2]; // 24-bit big-endian
                if ((raw & 0x800000) != 0) raw |= unchecked((int)0xFF000000); // sign-extend 24→32
                eeg[ch] = (float)(raw * EegScaleUv);
            }

            var s = new EegSample
            {
                counter = counter,
                tSec = _clock != null ? _clock.Elapsed.TotalSeconds : 0,
                eeg = eeg,
            };

            var rawFrame = new byte[FrameLength];
            Array.Copy(buf, pos, rawFrame, 0, FrameLength);

            lock (_lock)
            {
                Latest = s;
                LatestRaw = rawFrame;
                _drain.Add(s);
                FramesReceived++;
                if (_drain.Count > SampleRateHz * 60 * 30) // ~30 min cap if never drained
                    _drain.RemoveRange(0, _drain.Count - SampleRateHz * 60 * 30);
            }
        }

        public void Dispose()
        {
            StopAcquisition();
            try { _port?.Close(); } catch { }
            _port = null;
        }
    }
}
