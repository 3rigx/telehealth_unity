using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts.Sensors;
using Assets.Scripts.Sensors.Zed;
using UnityEngine;

namespace Assets.Scripts.SaveSystem.Writers
{
    /// <summary>
    ///     Writes the ZED skeleton stream to its own JSON file, one frame per captured
    ///     sensor state (1:1 with fsr.csv). The raw .svo video is produced separately by
    ///     the ZED controller and only referenced from the manifest.
    /// </summary>
    public static class ZedSkeletonWriter
    {
        [Serializable]
        public class Frame
        {
            public string timestampUtc;
            public int tMs;
            public SkeletonState skeleton;
        }

        [Serializable]
        public class Series
        {
            public int bodyFormat = (int)BODY_FORMAT.BODY_34;
            public List<Frame> frames = new();
        }

        public static int Write(string path, List<SensorSystemState> states, out int bodyFormat)
        {
            var series = new Series();
            bodyFormat = (int)BODY_FORMAT.BODY_34;

            foreach (var s in states)
            {
                var sk = s.skeleton ?? new SkeletonState();
                series.frames.Add(new Frame
                {
                    timestampUtc = s.timestamp != null ? SessionTime.FormatUtc(s.timestamp.DateTime) : "",
                    tMs = s.time,
                    skeleton = sk
                });

                if (s.skeleton != null) bodyFormat = (int)s.skeleton.m_Format;
            }

            series.bodyFormat = bodyFormat;
            File.WriteAllText(path, JsonUtility.ToJson(series));
            return series.frames.Count;
        }

        public static Series Read(string path)
        {
            if (!File.Exists(path)) return new Series();
            var series = JsonUtility.FromJson<Series>(File.ReadAllText(path));
            return series ?? new Series();
        }
    }
}
