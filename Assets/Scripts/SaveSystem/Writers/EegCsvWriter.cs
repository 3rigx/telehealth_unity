using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Assets.Scripts.Sensors.Eeg;

namespace Assets.Scripts.SaveSystem.Writers
{
    /// <summary>
    ///     Writes the EEG stream to eeg.csv. Unlike ZED/FSR (1:1 per motion frame), EEG is
    ///     a parallel high-rate series (250 Hz), so it has its own row count and timeline.
    ///     t_ms is cumulative since acquisition start (from the device sample clock).
    /// </summary>
    public static class EegCsvWriter
    {
        public const string Header = "counter,t_ms,ch1,ch2,ch3,ch4,ch5,ch6,ch7,ch8";

        public static int Write(string path, IReadOnlyList<EegSample> samples)
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');

            int n = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                if (s.eeg == null) continue;
                sb.Append(s.counter).Append(',')
                  .Append(((int)(s.tSec * 1000)).ToString(CultureInfo.InvariantCulture));
                for (int c = 0; c < s.eeg.Length; c++)
                    sb.Append(',').Append(s.eeg[c].ToString("F2", CultureInfo.InvariantCulture));
                sb.Append('\n');
                n++;
            }

            File.WriteAllText(path, sb.ToString());
            return n;
        }
    }
}
