using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Assets.Scripts.Sensors.FSR;

namespace Assets.Scripts.SaveSystem.Writers
{
    /// <summary>
    ///     Writes the single-insole plantar-pressure stream at its full acquisition rate
    ///     (~100 Hz) to fsr.csv (schema v2). Unlike the legacy two-foot writer this is a
    ///     parallel high-rate series — like eeg.csv — with its own row count and timeline,
    ///     NOT joined 1:1 with the ZED motion frames.
    ///
    ///     Columns use the canonical pad names (medial / lateral, never midInner/midOuter)
    ///     and preserve the Arduino <c>millis()</c> clock in its own column so the pressure
    ///     stream can be aligned to the EEG / skeleton streams via the markers.
    /// </summary>
    public static class FootPressureCsvWriter
    {
        // arduino_ms = Arduino millis() (device clock); t_ms = ms since first sample;
        // timestamp_utc = wall-clock reconstructed from session start + t_ms.
        public const string Header = "timestamp_utc,arduino_ms,t_ms,toe,medial,lateral,heel";

        public static int Write(string path, IReadOnlyList<PressureSample> samples, DateTime startUtc)
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            if (samples == null || samples.Count == 0)
            {
                File.WriteAllText(path, sb.ToString());
                return 0;
            }

            long first = samples[0].arduinoMs;
            int n = 0;
            foreach (var s in samples)
            {
                long tMs = s.arduinoMs - first;
                var ts = SessionTime.FormatUtc(startUtc.AddMilliseconds(tMs));
                sb.Append(ts).Append(',')
                  .Append(s.arduinoMs.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(tMs.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(s.toe).Append(',')
                  .Append(s.medial).Append(',')
                  .Append(s.lateral).Append(',')
                  .Append(s.heel)
                  .Append('\n');
                n++;
            }

            File.WriteAllText(path, sb.ToString());
            return n;
        }

        /// <summary>
        ///     Writes the Arduino sync/annotation markers to their own file, kept
        ///     structurally apart from the protocol markers.csv because they live in a
        ///     DIFFERENT clock domain (Arduino millis, not PC wall time) — the
        ///     <c>arduino_ms</c> column name makes that explicit so the two are never
        ///     subtracted from each other.
        /// </summary>
        public const string MarkerHeader = "arduino_ms,kind,label";

        public static int WriteMarkers(string path, IReadOnlyList<PressureMarker> markers)
        {
            var sb = new StringBuilder();
            sb.Append(MarkerHeader).Append('\n');
            int n = 0;
            if (markers != null)
            {
                foreach (var m in markers)
                {
                    sb.Append(m.arduinoMs.ToString(CultureInfo.InvariantCulture)).Append(',')
                      .Append(Escape(m.kind)).Append(',')
                      .Append(Escape(m.label))
                      .Append('\n');
                    n++;
                }
            }

            File.WriteAllText(path, sb.ToString());
            return n;
        }

        // Keep a label with a comma or quote from breaking the CSV column layout.
        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOf(',') < 0 && s.IndexOf('"') < 0 && s.IndexOf('\n') < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
