using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.Sensors.FSR
{
    /// <summary>One decoded plantar-pressure sample from the single insole (100 Hz).</summary>
    public struct PressureSample
    {
        public long arduinoMs;   // Arduino millis() from the CSV line — the authoritative clock
        public double tSec;      // host arrival time (seconds since acquisition start)
        public int toe;
        public int medial;
        public int lateral;
        public int heel;
    }

    /// <summary>
    ///     A synchronisation / annotation line emitted inline by the Arduino:
    ///     <c>#MARK,&lt;millis&gt;,&lt;label&gt;</c> or the numeric
    ///     <c>#BASELINE,&lt;millis&gt;,&lt;toe&gt;,&lt;medial&gt;,&lt;lateral&gt;,&lt;heel&gt;</c>.
    ///     (The firmware's text pre-amble <c>#BASELINE_BEGIN,...</c> is NOT a marker — it is
    ///     logged and ignored.) Carries the Arduino <c>millis()</c> clock, not PC wall time.
    /// </summary>
    public struct PressureMarker
    {
        public long arduinoMs;
        public string kind;   // "MARK" | "BASELINE"
        public string label;
        public int[] values;  // BASELINE only: [toe, medial, lateral, heel] unloaded ADC; null otherwise
    }

    /// <summary>
    ///     A streaming source of single-insole pressure. Implementations run their own
    ///     background acquisition (so no sample is lost to the 5 Hz broadcast tick) and
    ///     expose a drain-all buffer plus the latest sample, mirroring
    ///     <c>UnicornEegConnector</c>.
    /// </summary>
    public interface IPressureSource : IDisposable
    {
        /// <summary>Connect and begin streaming. Returns false if the source can't start.</summary>
        bool Start();

        /// <summary>Removes and returns all data samples buffered since the last drain.</summary>
        List<PressureSample> Drain();

        /// <summary>Removes and returns all markers buffered since the last drain.</summary>
        List<PressureMarker> DrainMarkers();

        PressureSample Latest { get; }
        bool IsConnected { get; }

        /// <summary>Total data samples received since Start() (for the row-count sanity check).</summary>
        long SamplesReceived { get; }

        /// <summary>
        ///     Command the source to emit a sync marker (Arduino command 'M'). The firmware
        ///     answers with a <c>#MARK</c> line stamped with its own <c>millis()</c>, which is
        ///     the only thing aligning plantar pressure to the EEG / skeleton streams.
        /// </summary>
        void SendMarker();

        /// <summary>
        ///     Re-capture the unloaded baseline (Arduino command 'Z'). Used by the
        ///     dashboard's manual re-baseline during preview after the insole is seated;
        ///     the firmware answers with a fresh <c>#BASELINE</c> and resumes streaming.
        /// </summary>
        void RequestBaseline();
    }

    /// <summary>
    ///     Shared line parser, thread-safe buffers, and the command handshake for a
    ///     newline-delimited pressure stream. Concrete sources feed raw lines in via
    ///     <see cref="Feed"/> and deliver outbound commands via <see cref="SendCommand"/>;
    ///     the handshake logic (below) is identical for real serial and the mock, so the
    ///     mock exercises exactly what the hardware does.
    ///
    ///     Handshake (firmware waits for commands; does not stream on boot):
    ///       #READY            → send 'Z' (capture unloaded baseline)
    ///       #BASELINE_BEGIN   → logged + ignored (NOT a marker, does NOT advance state)
    ///       #BASELINE,ms,...  → send 'S' (start / resume streaming); persisted as a marker
    ///       block boundary    → 'M' → firmware emits #MARK
    ///       stop              → 'X'
    ///
    ///     Wire format (per <c>PressureConfig.ChannelNames</c>, A0..A3 order):
    ///       data:    <c>millis,toe,medial,lateral,heel</c>
    /// </summary>
    public abstract class FootPressureSourceBase : IPressureSource
    {
        protected volatile bool _running;
        private readonly object _lock = new object();
        private readonly List<PressureSample> _drain = new List<PressureSample>();
        private readonly List<PressureMarker> _drainMarkers = new List<PressureMarker>();
        protected System.Diagnostics.Stopwatch _clock;

        // Captured from PressureConfig on the MAIN thread in Start() — PlayerPrefs is
        // not thread-safe, so the acquisition thread must never read it directly.
        protected int adcMax = 1023;

        /// <summary>Snapshot main-thread-only config. Call from Start() before the thread spins up.</summary>
        protected void CaptureConfig() => adcMax = PressureConfig.AdcMax;

        // Handshake state: 0 = waiting for #READY, 1 = baseline requested ('Z' sent),
        // 2 = streaming ('S' sent). Advanced atomically; the Interlocked guard makes a
        // redundant read on the read/mock or watchdog thread harmless.
        private int _hs;
        private Thread _watchThread;
        // #READY (board boot after the DTR reset) is fast; the BASELINE capture on real
        // hardware takes ~2 s (500 ms settle + 200 samples @ 5 ms across 4 channels), so its
        // watchdog window must be comfortably longer than that or the fallback 'S' fires
        // mid-capture.
        private const int ReadyTimeoutMs = 3000;
        private const int BaselineTimeoutMs = 6000;

        // Guard so 'Z' is sent at most once per capture. The firmware BLOCKS while it
        // captures (~2 s) and BUFFERS late commands, so a spurious/duplicate 'Z' (a
        // watchdog/handshake race, or a double re-baseline press) would queue an unwanted
        // second capture — possibly with the foot now loaded. Every 'Z' goes through
        // SendBaselineCommand; the in-flight flag clears when #BASELINE returns.
        private readonly object _zLock = new object();
        private volatile bool _baselineInFlight;
        private long _lastZMs = long.MinValue;

        public PressureSample Latest { get; private set; }
        public long SamplesReceived { get; private set; }
        public abstract bool IsConnected { get; }

        public abstract bool Start();
        public abstract void Dispose();

        /// <summary>Deliver one outbound command char to the firmware (serial write / mock interpret).</summary>
        protected abstract void SendCommand(char c);

        public void SendMarker() => SendCommand('M');
        public abstract void RequestBaseline();

        public List<PressureSample> Drain()
        {
            lock (_lock)
            {
                var copy = new List<PressureSample>(_drain);
                _drain.Clear();
                return copy;
            }
        }

        public List<PressureMarker> DrainMarkers()
        {
            lock (_lock)
            {
                var copy = new List<PressureMarker>(_drainMarkers);
                _drainMarkers.Clear();
                return copy;
            }
        }

        /// <summary>Parse and buffer one raw line. Safe to call from the acquisition thread.</summary>
        protected void Feed(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            line = line.Trim();

            if (line[0] == '#') { HandleControl(line); return; }

            // Data line: millis,toe,medial,lateral,heel  (5 fields; field 0 is the Arduino
            // timestamp, NOT a pad value).
            var c = line.Split(',');
            if (c.Length < 5) return;                 // header ("millis,toe,...") or partial line
            if (!long.TryParse(c[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
                return;                               // non-numeric first field → not a data row

            var s = new PressureSample
            {
                arduinoMs = ms,
                tSec = _clock != null ? _clock.Elapsed.TotalSeconds : 0,
                toe = ParseAdc(c[1]),
                medial = ParseAdc(c[2]),
                lateral = ParseAdc(c[3]),
                heel = ParseAdc(c[4]),
            };

            lock (_lock)
            {
                Latest = s;
                _drain.Add(s);
                SamplesReceived++;
                // ~30 min cap if the consumer never drains, so a runaway can't OOM.
                int cap = PressureConfig.SampleRateHz * 60 * 30;
                if (_drain.Count > cap) _drain.RemoveRange(0, _drain.Count - cap);
            }
        }

        /// <summary>
        ///     Handle a '#' control line. #MARK / numeric #BASELINE are buffered as markers;
        ///     #WARN is surfaced; #BASELINE_BEGIN and every other line (#READY, #CMDS,
        ///     #COLUMNS, #SAMPLE_HZ, unknown) are logged at info and IGNORED — never throw,
        ///     never drop the connection. The handshake then reacts.
        /// </summary>
        private void HandleControl(string line)
        {
            var c = line.Split(',');
            var tag = c[0].TrimStart('#').ToUpperInvariant();
            switch (tag)
            {
                case "MARK":
                case "BASELINE":
                    var m = ParseMarker(line);
                    if (m.HasValue) lock (_lock) { _drainMarkers.Add(m.Value); }
                    Debug.Log($"[Pressure] {line}");
                    break;
                case "BASELINE_BEGIN":
                    // Text pre-amble before the numeric baseline — informational only. It
                    // must NOT be treated as a marker and must NOT advance the handshake.
                    Debug.Log($"[Pressure] {line}");
                    break;
                case "WARN":
                    Debug.LogWarning($"[Pressure] firmware: {line}");
                    break;
                default:
                    Debug.Log($"[Pressure] {line}");
                    break;
            }
            RunHandshake(tag);
        }

        /// <summary>Advance the command handshake off an incoming control-line tag.</summary>
        private void RunHandshake(string tag)
        {
            if (tag == "READY" && Interlocked.CompareExchange(ref _hs, 1, 0) == 0)
            {
                Debug.Log("[Pressure] #READY → sending 'Z' (capture unloaded baseline).");
                SendBaselineCommand("#READY");
            }
            else if (tag == "BASELINE")
            {
                _baselineInFlight = false; // capture complete — a new 'Z' is allowed again
                // First baseline advances to streaming; a later re-baseline keeps state at 2
                // but still (re)sends 'S' so streaming resumes after the capture pause.
                Interlocked.CompareExchange(ref _hs, 2, 1);
                Debug.Log("[Pressure] #BASELINE → sending 'S' (start/resume streaming).");
                SendCommand('S');
            }
            // #BASELINE_BEGIN and everything else: no handshake advance.
        }

        /// <summary>
        ///     The single guarded gate for 'Z'. Sends it only when no capture is in flight
        ///     (or the previous one has clearly timed out), so a duplicate/spurious 'Z' can
        ///     never queue a second baseline behind the firmware's blocking capture.
        /// </summary>
        protected void SendBaselineCommand(string why)
        {
            lock (_zLock)
            {
                long now = _clock != null ? _clock.ElapsedMilliseconds : 0;
                if (_baselineInFlight && now - _lastZMs < BaselineTimeoutMs)
                {
                    Debug.LogWarning($"[Pressure] 'Z' suppressed ({why}) — a baseline capture is already in progress.");
                    return;
                }
                _baselineInFlight = true;
                _lastZMs = now;
            }
            SendCommand('Z');
        }

        // Fallback so a missed #READY / #BASELINE (reset hiccup, board already booted) never
        // leaves us silently un-streamed. Started from Start() by each source.
        protected void StartHandshakeWatchdog()
        {
            _watchThread = new Thread(HandshakeWatchdog) { IsBackground = true, Name = "FootPressureHandshake" };
            _watchThread.Start();
        }

        private void HandshakeWatchdog()
        {
            if (!WaitForState(1, ReadyTimeoutMs) && Interlocked.CompareExchange(ref _hs, 1, 0) == 0)
            {
                Debug.LogWarning($"[Pressure] No #READY within {ReadyTimeoutMs} ms — sending 'Z' anyway.");
                SendBaselineCommand("watchdog: no #READY");
            }
            if (!WaitForState(2, BaselineTimeoutMs) && Interlocked.CompareExchange(ref _hs, 2, 1) == 1)
            {
                Debug.LogWarning($"[Pressure] No #BASELINE within {BaselineTimeoutMs} ms — sending 'S' anyway.");
                SendCommand('S');
            }
        }

        private bool WaitForState(int target, int timeoutMs)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (_running && sw.ElapsedMilliseconds < timeoutMs)
            {
                if (_hs >= target) return true;
                Thread.Sleep(20);
            }
            return _hs >= target;
        }

        protected void JoinWatchdog()
        {
            try { _watchThread?.Join(300); } catch { }
            _watchThread = null;
        }

        private static PressureMarker? ParseMarker(string line)
        {
            var c = line.Split(',');
            var tag = c[0].TrimStart('#').ToUpperInvariant();

            if (tag == "BASELINE")
            {
                // Firmware-confirmed: #BASELINE,<millis>,<toe>,<medial>,<lateral>,<heel>.
                var nums = new List<int>();
                for (int i = 1; i < c.Length; i++)
                    if (int.TryParse(c[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                        nums.Add(v);

                long bms = nums.Count >= 1 ? nums[0] : 0;
                int[] vals = null;
                if (nums.Count >= 5)
                    vals = new[] { nums[1], nums[2], nums[3], nums[4] };
                else
                    Debug.LogWarning($"[Pressure] malformed #BASELINE (expected millis + 4 values): {line}");

                string blabel = vals != null
                    ? $"toe={vals[0]} medial={vals[1]} lateral={vals[2]} heel={vals[3]}"
                    : "";
                return new PressureMarker { arduinoMs = bms, kind = "BASELINE", label = blabel, values = vals };
            }

            if (tag != "MARK") return null;

            long ms = 0;
            if (c.Length >= 2)
                long.TryParse(c[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ms);

            string label = c.Length >= 3 ? string.Join(",", c, 2, c.Length - 2).Trim() : "";
            return new PressureMarker { arduinoMs = ms, kind = "MARK", label = label, values = null };
        }

        private int ParseAdc(string s)
        {
            return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? Mathf.Clamp(v, 0, adcMax)
                : 0;
        }
    }

    /// <summary>
    ///     Real single-insole source: opens the Arduino's serial port and reads every
    ///     newline-delimited line on a background thread. Unlike the legacy
    ///     <c>USBSensorConnector</c> (which drained the backlog to the latest line each
    ///     tick, throwing ~95% of samples away), this keeps EVERY line, so the full
    ///     100 Hz stream reaches disk.
    /// </summary>
    public class SerialFootPressureSource : FootPressureSourceBase
    {
        private readonly string _portName;
        private readonly int _baud;
        private readonly bool _baudIsOverride;
        private SerialPort _port;
        private Thread _thread;
        private readonly object _writeLock = new object();

        private const int ConnectAttempts = 3;
        private const int ConnectRetryDelayMs = 500;

        public SerialFootPressureSource(string portName, int baud, bool baudIsOverride = false)
        {
            _portName = portName;
            _baud = baud;
            _baudIsOverride = baudIsOverride;
        }

        public override bool IsConnected => _port != null && _port.IsOpen;

        public override bool Start()
        {
            for (int attempt = 1; attempt <= ConnectAttempts; attempt++)
            {
                try
                {
                    _port = new SerialPort(_portName, _baud)
                    {
                        ReadTimeout = 1000,
                        WriteTimeout = 1000,
                        NewLine = "\n",
                        DtrEnable = true,   // many Arduinos hold in reset until DTR is asserted
                    };
                    _port.Open();
                    try { _port.DiscardInBuffer(); } catch { }
                    Debug.Log($"[Pressure] Opened {_portName} @ {_baud} baud " +
                              $"({(_baudIsOverride ? "PlayerPrefs override" : "config default")})" +
                              (attempt > 1 ? $" (attempt {attempt}/{ConnectAttempts})" : ""));
                    CaptureConfig();
                    _clock = System.Diagnostics.Stopwatch.StartNew();
                    _running = true;
                    _thread = new Thread(ReadLoop) { IsBackground = true, Name = "FootPressure" };
                    _thread.Start();
                    StartHandshakeWatchdog();
                    return true;
                }
                catch (Exception e)
                {
                    _port = null;
                    if (attempt < ConnectAttempts)
                    {
                        Debug.LogWarning($"[Pressure] Could not open {_portName} " +
                                         $"(attempt {attempt}/{ConnectAttempts}): {e.Message} — retrying…");
                        Thread.Sleep(ConnectRetryDelayMs);
                    }
                    else
                    {
                        Debug.LogError($"[Pressure] Could not open {_portName} after {ConnectAttempts} attempts: {e.Message}");
                    }
                }
            }
            return false;
        }

        protected override void SendCommand(char c)
        {
            try
            {
                lock (_writeLock)
                {
                    if (_port != null && _port.IsOpen) _port.Write(new[] { c, '\n' }, 0, 2);
                }
            }
            catch (Exception e) { Debug.LogWarning($"[Pressure] send '{c}' failed: {e.Message}"); }
        }

        /// <summary>Manual re-baseline (dashboard, preview): ask the firmware for a fresh baseline.</summary>
        public override void RequestBaseline()
        {
            Debug.Log("[Pressure] Re-baseline requested.");
            SendBaselineCommand("manual re-baseline");
        }

        private void ReadLoop()
        {
            while (_running)
            {
                string line;
                try { line = _port.ReadLine(); }
                catch (TimeoutException) { continue; }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Pressure] Read loop ended: {e.Message}");
                    _running = false;
                    try { _port?.Close(); } catch { }
                    break;
                }
                Feed(line);
            }
        }

        public override void Dispose()
        {
            // Ask the firmware to stop streaming before dropping the link.
            try { SendCommand('X'); } catch { }
            _running = false;
            try { _thread?.Join(750); } catch { }
            JoinWatchdog();
            _thread = null;
            try { _port?.Close(); } catch { }
            _port = null;
        }
    }

    /// <summary>
    ///     Synthetic 100 Hz single-insole source — no hardware required. It is a command
    ///     LOOPBACK: it emits <c>#READY</c>, then reacts to the handshake's 'Z' by sending
    ///     <c>#BASELINE_BEGIN</c> and (after a ~500 ms unloaded pause) the numeric
    ///     <c>#BASELINE</c>, then streams data on 'S'. So the mock drives the exact same
    ///     handshake path the firmware does. A loaded baseline can be forced for testing
    ///     via PlayerPrefs "PressureMockLoadedBaseline" (any pad value above the threshold).
    /// </summary>
    public class MockFootPressureSource : FootPressureSourceBase
    {
        private Thread _thread;
        private bool _connected;
        private readonly System.Random _rng = new System.Random();

        private readonly object _mockLock = new object();
        private volatile bool _streaming;
        private volatile int _loadedBaselineVal;   // 0 = unloaded; >0 = forced loaded value
        private int _pendingBaseline = -1;          // >=0 → a baseline capture is queued (guarded by _mockLock)
        private long _baselineBeginAtMs = -1;
        // Synthetic DEVICE clock: advances exactly periodMs per data sample, like an
        // Arduino's millis() at a fixed 100 Hz — independent of host scheduling, so the
        // recorded arduino_ms deltas are a true 10 ms (not the host Sleep granularity).
        private long _ardMs;

        public override bool IsConnected => _connected;

        public override bool Start()
        {
            CaptureConfig();
            _clock = System.Diagnostics.Stopwatch.StartNew();
            _connected = true;
            _running = true;
            // Read the debug flag on the MAIN thread (PlayerPrefs); the mock thread never does.
            _loadedBaselineVal = Mathf.Max(0, PlayerPrefs.GetInt("PressureMockLoadedBaseline", 0));
            _thread = new Thread(Loop) { IsBackground = true, Name = "FootPressureMock" };
            _thread.Start();
            StartHandshakeWatchdog();
            return true;
        }

        protected override void SendCommand(char c)
        {
            switch (c)
            {
                case 'Z':   // queue a baseline capture; firmware pauses streaming meanwhile
                    lock (_mockLock) { _pendingBaseline = _loadedBaselineVal; _baselineBeginAtMs = -1; }
                    _streaming = false;
                    break;
                case 'S': _streaming = true; break;
                case 'X': _streaming = false; break;
                case 'M': Feed($"#MARK,{_ardMs},marker"); break;  // device-clock timestamp
            }
        }

        public override void RequestBaseline()
        {
            // Re-read the debug flag on the MAIN thread, then run the same guarded 'Z'.
            _loadedBaselineVal = Mathf.Max(0, PlayerPrefs.GetInt("PressureMockLoadedBaseline", 0));
            SendBaselineCommand("manual re-baseline");
        }

        // Matches real hardware: ~500 ms settle + 200 samples @ 5 ms ≈ 2 s of blocking capture.
        private const int MockBaselineCaptureMs = 2000;

        private void Loop()
        {
            const int periodMs = 1000 / PressureConfig.SampleRateHz; // 10 ms (SampleRateHz is const)
            int max = adcMax;

            Feed("#READY,mock_fsr_insole"); // → handshake sends 'Z' → we emit BEGIN/BASELINE → 'S'

            while (_running)
            {
                long realMs = _clock.ElapsedMilliseconds;

                // Baseline capture: #BASELINE_BEGIN, ~500 ms unloaded pause, numeric #BASELINE.
                bool inBaseline = false;
                lock (_mockLock)
                {
                    if (_pendingBaseline >= 0)
                    {
                        inBaseline = true;
                        if (_baselineBeginAtMs < 0)
                        {
                            Feed("#BASELINE_BEGIN,capturing - keep the insole UNLOADED");
                            _baselineBeginAtMs = realMs;
                        }
                        else if (realMs - _baselineBeginAtMs >= MockBaselineCaptureMs)
                        {
                            int v = _pendingBaseline;
                            int toe = v > 0 ? v : 12 + _rng.Next(8);
                            int medial = v > 0 ? v : 10 + _rng.Next(8);
                            int lateral = v > 0 ? v : 9 + _rng.Next(8);
                            int heel = v > 0 ? v : 14 + _rng.Next(8);
                            _ardMs += MockBaselineCaptureMs; // device clock kept running through the capture pause
                            Feed($"#BASELINE,{_ardMs},{toe},{medial},{lateral},{heel}"); // → handshake sends 'S'
                            _pendingBaseline = -1;
                            _baselineBeginAtMs = -1;
                        }
                    }
                }
                if (inBaseline) { Thread.Sleep(2); continue; }  // firmware pauses streaming during capture
                if (!_streaming) { Thread.Sleep(2); continue; } // no data until 'S'

                // Resync after startup / a long stall so a catch-up burst stays bounded.
                if (realMs - _ardMs > 200) _ardMs = realMs;

                // Emit every sample due up to real time, each exactly periodMs apart on the
                // device clock. Host scheduling only affects HOW MANY per wake, never the
                // arduino_ms spacing — so the recorded deltas are a true 10 ms.
                while (_running && _streaming && _ardMs <= realMs)
                {
                    double t = _ardMs / 1000.0 * (2.0 * Math.PI) / 1.2; // ~1.2 s stride
                    double Roll(double phase)
                    {
                        var wave = Math.Sin(t - phase);
                        return (wave > 0 ? wave : 0);
                    }

                    int toe2 = (int)Math.Min(max, Roll(0.35 * Math.PI) * max * 0.9 + Rand(max * 0.04));
                    int medial2 = (int)Math.Min(max, Roll(0.15 * Math.PI) * max * 0.7 + Rand(max * 0.04));
                    int lateral2 = (int)Math.Min(max, Roll(0.20 * Math.PI) * max * 0.6 + Rand(max * 0.04));
                    int heel2 = (int)Math.Min(max, Roll(0.0) * max * 0.95 + Rand(max * 0.04));

                    Feed($"{_ardMs},{toe2},{medial2},{lateral2},{heel2}");
                    _ardMs += periodMs;
                }
                Thread.Sleep(1);
            }
        }

        private double Rand(double amp) => _rng.NextDouble() * amp;

        public override void Dispose()
        {
            _running = false;
            try { _thread?.Join(500); } catch { }
            JoinWatchdog();
            _thread = null;
            _connected = false;
        }
    }
}
