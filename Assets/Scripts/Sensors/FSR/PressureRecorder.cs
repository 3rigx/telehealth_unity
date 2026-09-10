using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Sensors.FSR
{
    /// <summary>
    ///     Owns an <see cref="IPressureSource"/> for one capture session (single-insole
    ///     path). Each capture tick (main thread) it drains the background acquisition
    ///     buffer into the full session list — written to fsr.csv at the end at the full
    ///     100 Hz rate — plus the latest sample for the 5 Hz live view. This is the FSR
    ///     analogue of <c>EegRecorder</c>: the recorded stream is DECOUPLED from the
    ///     broadcast tick, so live rendering at 5 Hz never downsamples what lands on disk.
    /// </summary>
    public class PressureRecorder
    {
        private IPressureSource _source;
        private readonly List<PressureSample> _all = new List<PressureSample>();
        private readonly List<PressureMarker> _markers = new List<PressureMarker>();

        /// <summary>Full set of samples captured this session (for fsr.csv).</summary>
        public IReadOnlyList<PressureSample> Samples => _all;

        /// <summary>All Arduino sync/annotation markers captured (for pressure_markers.csv).</summary>
        public IReadOnlyList<PressureMarker> Markers => _markers;

        /// <summary>Latest pressure as an <see cref="FSRState"/> for the live broadcast.</summary>
        public FSRState LatestState { get; private set; } = new FSRState();

        /// <summary>
        ///     Last unloaded baseline [toe, medial, lateral, heel] and its Arduino clock.
        ///     Tracked even during preview, so the last baseline BEFORE Record is the one
        ///     written into session.json (raw values are unanalysable without it).
        /// </summary>
        public int[] Baseline { get; private set; }
        public long BaselineArduinoMs { get; private set; }
        public bool HasBaseline => Baseline != null && Baseline.Length == 4;

        /// <summary>True when the stored baseline was captured under load (a pad above the threshold).</summary>
        public bool BaselineUnderLoad
        {
            get
            {
                if (!HasBaseline) return false;
                int max = PressureConfig.BaselineMaxAdc;
                foreach (var v in Baseline) if (v > max) return true;
                return false;
            }
        }

        public bool Connected { get; private set; }
        public bool Running { get; private set; }

        /// <summary>
        ///     True once <see cref="BeginCapture"/> is called (Record pressed). The source
        ///     streams from preview so the live view / pad-check work, but samples and
        ///     markers are only written to the session lists while Capturing — so fsr.csv
        ///     holds exactly the record window, not the warm-up.
        /// </summary>
        public bool Capturing { get; private set; }
        public DateTime StartedUtc { get; private set; }

        /// <summary>Total samples the source has received (for the row-count sanity check).</summary>
        public long SamplesReceived => _source?.SamplesReceived ?? _all.Count;

        public bool Start(IPressureSource source)
        {
            _source = source;
            if (_source == null || !_source.Start()) { _source = null; return false; }
            StartedUtc = DateTime.UtcNow;
            Running = true;
            return true;
        }

        /// <summary>Start persisting drained SAMPLES (Record pressed). Markers are kept from preview.</summary>
        public void BeginCapture()
        {
            // Drop the preview SAMPLE backlog so the recorded stream starts clean at
            // record-time. Markers (incl. preview baselines) are NOT dropped — they were
            // already drained into _markers by earlier ticks and must survive.
            _source?.Drain();
            Capturing = true;
            StartedUtc = DateTime.UtcNow;
        }

        /// <summary>Ask the firmware to emit a sync marker (protocol block boundary).</summary>
        public void SendMarker() => _source?.SendMarker();

        /// <summary>Ask the firmware to re-capture the unloaded baseline (manual, preview).</summary>
        public void RequestBaseline() => _source?.RequestBaseline();

        /// <summary>Drain new samples + markers and refresh the live state.</summary>
        public void Tick()
        {
            if (_source == null) return;

            // Samples: only accumulate once recording has begun (fsr.csv = record window).
            var batch = _source.Drain();
            if (Capturing && batch.Count > 0) _all.AddRange(batch);

            // Markers: accumulate ALWAYS (incl. preview baselines), and track the latest
            // baseline so the last one before Record lands in session.json while every
            // one appears in pressure_markers.csv.
            var mk = _source.DrainMarkers();
            foreach (var m in mk)
            {
                if (m.kind == "BASELINE" && m.values != null && m.values.Length == 4)
                {
                    Baseline = m.values;
                    BaselineArduinoMs = m.arduinoMs;
                }
                _markers.Add(m);
            }

            Connected = _source.IsConnected;

            var s = _source.Latest;
            // FSRState(toe, middle_inner=medial, middle_outer=lateral, heel).
            LatestState = new FSRState(s.toe, s.medial, s.lateral, s.heel);
        }

        public void Stop()
        {
            if (_source != null)
            {
                // Capture the sub-tick tail still sitting in the bg buffer.
                var tail = _source.Drain();
                if (Capturing && tail.Count > 0) _all.AddRange(tail);
                foreach (var m in _source.DrainMarkers())
                {
                    if (m.kind == "BASELINE" && m.values != null && m.values.Length == 4)
                    {
                        Baseline = m.values;
                        BaselineArduinoMs = m.arduinoMs;
                    }
                    _markers.Add(m);
                }

                _source.Dispose(); // sends 'X' to stop the firmware streaming
                _source = null;
            }
            Capturing = false;
            Running = false;
            Connected = false;
        }
    }
}
