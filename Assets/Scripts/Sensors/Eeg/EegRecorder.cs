using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Sensors.Eeg
{
    /// <summary>
    ///     Owns a <see cref="UnicornEegConnector"/> for one capture session. Each capture
    ///     tick (main thread) it drains the background acquisition buffer into the full
    ///     session list (written to eeg.csv at the end) plus a rolling 1-second window
    ///     from which it computes the live band-power / quality metrics for the dashboard.
    /// </summary>
    public class EegRecorder
    {
        private UnicornEegConnector _conn;
        private readonly List<EegSample> _all = new List<EegSample>();
        private readonly List<float[]> _window = new List<float[]>();
        private const int WindowSize = UnicornEegConnector.SampleRateHz; // 1 s

        public readonly EegLiveData Live = new EegLiveData
        {
            channelRms = new float[UnicornEegConnector.ChannelCount]
        };

        /// <summary>Full set of samples captured this session (for eeg.csv).</summary>
        public IReadOnlyList<EegSample> Samples => _all;
        public bool Running { get; private set; }

        public bool Start(string portName)
        {
            _conn = new UnicornEegConnector();
            if (!_conn.Connect(portName)) { _conn = null; return false; }
            _conn.StartAcquisition();
            Running = true;
            return true;
        }

        /// <summary>Drain new samples, update the rolling window, refresh live metrics.</summary>
        public void Tick()
        {
            if (_conn == null) return;
            var batch = _conn.Drain();
            for (int i = 0; i < batch.Count; i++)
            {
                _all.Add(batch[i]);
                _window.Add(batch[i].eeg);
            }
            if (_window.Count > WindowSize) _window.RemoveRange(0, _window.Count - WindowSize);

            Live.connected = _conn.IsConnected && _conn.IsLocked;
            if (_window.Count >= WindowSize / 2) ComputeMetrics();
        }

        private void ComputeMetrics()
        {
            int ch = UnicornEegConnector.ChannelCount;
            int n = _window.Count;
            double fs = UnicornEegConnector.SampleRateHz;
            double theta = 0, alpha = 0, beta = 0;
            int railed = 0;
            var col = new float[n];

            for (int c = 0; c < ch; c++)
            {
                for (int i = 0; i < n; i++)
                {
                    var row = _window[i];
                    col[i] = (row != null && c < row.Length) ? row[c] : 0f;
                }

                Live.channelRms[c] = (float)EegBandPower.Rms(col, n);

                // Mean near the ±0.75 V rail = floating / no electrode contact.
                double mean = 0;
                for (int i = 0; i < n; i++) mean += col[i];
                mean /= n;
                if (System.Math.Abs(mean) > 700000.0) railed++;

                theta += EegBandPower.BandPower(col, n, 4, 7, fs);
                alpha += EegBandPower.BandPower(col, n, 8, 12, fs);
                beta  += EegBandPower.BandPower(col, n, 13, 30, fs);
            }

            Live.theta = (float)(theta / ch);
            Live.alpha = (float)(alpha / ch);
            Live.beta  = (float)(beta / ch);
            Live.quality = railed >= ch / 2 ? "Poor" : (railed > 0 ? "Fair" : "Good");

            // High relative beta is a crude EMG / movement-artifact flag.
            float tot = Live.theta + Live.alpha + Live.beta + 1e-3f;
            Live.artifact = (Live.beta / tot) > 0.6f ? "High" : "Low";
        }

        public void Stop()
        {
            if (_conn != null)
            {
                // Capture the sub-second tail still sitting in the bg buffer.
                var tail = _conn.Drain();
                for (int i = 0; i < tail.Count; i++) _all.Add(tail[i]);
                _conn.Dispose();
                _conn = null;
            }
            Running = false;
            Live.connected = false;
        }
    }
}
