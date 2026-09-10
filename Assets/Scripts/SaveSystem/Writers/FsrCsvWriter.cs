using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Assets.Scripts.Sensors;
using Assets.Scripts.Sensors.FSR;

namespace Assets.Scripts.SaveSystem.Writers
{
    /// <summary>
    ///     Writes the FSR stream to its own CSV, one row per captured sensor state
    ///     (kept 1:1 with the ZED skeleton file so the loader can re-join by index).
    /// </summary>
    public static class FsrCsvWriter
    {
        public const string Header =
            "timestamp_utc,t_ms,L_toe,L_mid_inner,L_mid_outer,L_heel,R_toe,R_mid_inner,R_mid_outer,R_heel";

        public static int Write(string path, List<SensorSystemState> states)
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');

            var n = 0;
            foreach (var s in states)
            {
                var l = s.leftFoot ?? new FSRState();
                var r = s.rightFoot ?? new FSRState();
                var ts = s.timestamp != null ? SessionTime.FormatUtc(s.timestamp.DateTime) : "";

                sb.Append(ts).Append(',')
                    .Append(s.time.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(l.Toe).Append(',').Append(l.Middle_Inner).Append(',')
                    .Append(l.Middle_Outer).Append(',').Append(l.Heel).Append(',')
                    .Append(r.Toe).Append(',').Append(r.Middle_Inner).Append(',')
                    .Append(r.Middle_Outer).Append(',').Append(r.Heel)
                    .Append('\n');
                n++;
            }

            File.WriteAllText(path, sb.ToString());
            return n;
        }

        /// <summary>Row parsed back from fsr.csv.</summary>
        public struct Row
        {
            public FSRState left;
            public FSRState right;
            public int tMs;
            public string timestampUtc;
        }

        public static List<Row> Read(string path)
        {
            var rows = new List<Row>();
            if (!File.Exists(path)) return rows;

            var lines = File.ReadAllLines(path);
            for (var i = 1; i < lines.Length; i++) // skip header
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                var c = line.Split(',');
                if (c.Length < 10) continue;

                rows.Add(new Row
                {
                    timestampUtc = c[0],
                    tMs = ParseInt(c[1]),
                    // FSRState(toe, middle_inner, middle_outer, heel)
                    left = new FSRState(ParseInt(c[2]), ParseInt(c[3]), ParseInt(c[4]), ParseInt(c[5])),
                    right = new FSRState(ParseInt(c[6]), ParseInt(c[7]), ParseInt(c[8]), ParseInt(c[9]))
                });
            }

            return rows;
        }

        private static int ParseInt(string s)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }
    }
}
