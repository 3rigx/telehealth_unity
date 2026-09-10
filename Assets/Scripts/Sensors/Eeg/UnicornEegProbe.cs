using System.Text;
using UnityEngine;

namespace Assets.Scripts.Sensors.Eeg
{
    /// <summary>
    ///     Standalone validation harness for the Unicorn raw-protocol connector.
    ///     Attach to any GameObject, set the COM port (default COM8 = UN-2023.03.29),
    ///     press Play, and watch the Console:
    ///       • "[EEG] Locked. Counter offset = N."  → framing + counter decode are correct.
    ///       • A periodic line of 8 µV values        → EEG byte order + scale are correct
    ///                                                  (resting EEG ≈ tens of µV; flat-line
    ///                                                  or millions = wrong scale/order).
    ///     Once this looks right we wire the connector into the capture pipeline.
    /// </summary>
    public class UnicornEegProbe : MonoBehaviour
    {
        public string comPort = "COM5"; // UN-2023.03.29 SPP port via the g.tec dongle
        [Tooltip("Seconds between status log lines.")]
        public float logEvery = 1.0f;

        private UnicornEegConnector _eeg;
        private float _next;

        private void Start()
        {
#if !UNITY_EDITOR
            // This is a one-shot dev diagnostic (see class doc) — it must never run in a
            // shipped build. It was left attached to a scene GameObject and was silently
            // auto-connecting on every app launch, holding its own exclusive handle to
            // the EEG's COM port for the app's entire lifetime — starving the real
            // capture pipeline (EegRecorder) of the same port during an actual session,
            // and making the headset look "connected" at the menu yet fail once a
            // session tried to use it.
            Debug.LogWarning("[EEG] UnicornEegProbe is a dev-only harness — disabled in this build.");
            return;
#else
            _eeg = new UnicornEegConnector();
            if (_eeg.Connect(comPort))
                _eeg.StartAcquisition();
#endif
        }

        // Unicorn Hybrid Black fixed 10-20 montage, in data-stream order (ch0..ch7).
        // The last four (Pz, PO7, Oz, PO8) are the back-of-head channels that carry alpha.
        private static readonly string[] ChannelNames =
            { "Fz", "C3", "Cz", "C4", "Pz", "PO7", "Oz", "PO8" };

        private void Update()
        {
            if (_eeg == null || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + logEvery;

            // Worn-headset validator: per-channel RMS (railed electrodes read in the
            // hundreds-of-thousands µV; clean worn EEG reads tens of µV) and alpha-band
            // (8–12 Hz) power, which should RISE on the back channels when eyes close —
            // the same effect you see in the Unicorn Recorder.
            var batch = _eeg.Drain();
            int n = batch.Count;
            var sb = new StringBuilder();
            sb.Append($"[EEG] locked={_eeg.IsLocked} off={_eeg.CounterOffset} ")
              .Append($"recv={_eeg.FramesReceived} drop={_eeg.FramesDropped} n={n}");

            if (n > 0)
            {
                int chans = UnicornEegConnector.ChannelCount;
                // De-interleave the batch into one contiguous array per channel.
                var col = new float[chans][];
                for (int c = 0; c < chans; c++) col[c] = new float[n];
                for (int i = 0; i < n; i++)
                {
                    var e = batch[i].eeg;
                    if (e == null) continue;
                    for (int c = 0; c < chans && c < e.Length; c++) col[c][i] = e[c];
                }

                var rms = new double[chans];
                var alpha = new double[chans];
                for (int c = 0; c < chans; c++)
                {
                    rms[c] = EegBandPower.Rms(col[c], n);
                    alpha[c] = EegBandPower.BandPower(col[c], n, 8, 12, UnicornEegConnector.SampleRateHz);
                }

                // Back-of-head channels (Pz, PO7, Oz, PO8 = idx 4..7) carry alpha.
                double backAlpha = (alpha[4] + alpha[5] + alpha[6] + alpha[7]) / 4.0;

                sb.Append("\n  RMS µV =[");
                for (int c = 0; c < chans; c++)
                    sb.Append($"{ChannelNames[c]}:{rms[c]:F0}").Append(c < chans - 1 ? ", " : "");
                sb.Append("]\n  alpha  =[");
                for (int c = 0; c < chans; c++)
                    sb.Append($"{ChannelNames[c]}:{alpha[c]:F0}").Append(c < chans - 1 ? ", " : "");
                sb.Append($"]\n  >> back-channel alpha (Pz,PO7,Oz,PO8) mean = {backAlpha:F0}  (watch this RISE when eyes CLOSED)");
            }
            Debug.Log(sb.ToString());
        }

        private void OnDestroy()
        {
            _eeg?.Dispose();
            _eeg = null;
        }
    }
}
