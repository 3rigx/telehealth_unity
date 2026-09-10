using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.SaveSystem
{
    /// <summary>
    ///     Per-sensor manifest written as <c>session.json</c>. It is the index that ties
    ///     the separate sensor files of one session together (the source of truth is the
    ///     individual files; this manifest records what they are and how to re-join them).
    ///     JsonUtility-serializable: public fields only, nested [Serializable] types.
    /// </summary>
    [Serializable]
    public class SessionManifest
    {
        // v2 (2026-08): plantar pressure moved from a two-insole 10-column fsr.csv to a
        // single-insole high-rate fsr.csv (see FootPressureCsvWriter). The SensorEntry
        // now records foot / insoleCount / channelNames so a reader can tell the layout
        // of a file without guessing, and v1 recordings still load (reader branches).
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public string patientId = "";
        public string sessionId = "";
        public string mode = "Exercise";          // Exercise | Prediction
        public string exerciseClass = "Motion";   // Idle | Motion | MotionCognitive
        public string operatorName = "";
        public int trialNumber;
        public string startUtc = "";
        public string endUtc = "";
        public int bodyFormat = 1;                 // BODY_FORMAT enum value (1 == BODY_34)

        public SensorEntry zed = new();
        public SensorEntry fsr = new();
        public SensorEntry eeg = new();

        public CognitiveTask cognitiveTask = new();

        public string hash = "";

        [Serializable]
        public class SensorEntry
        {
            public bool enabled;
            public string source = "";
            public string file = "";       // primary data file (csv / json), relative to folder
            public string videoFile = "";  // ZED only, optional
            public float sampleRateHz;
            public int sampleCount;
            public int channels;           // EEG only

            // ---- FSR / plantar pressure layout (schema v2) ----
            public int insoleCount;        // 1 = single insole (v2), 2 = legacy left+right (v1-style)
            public string foot = "";       // "left" | "right" — the insole side (single-insole only)
            public string[] channelNames;  // pad names in column order, e.g. [toe,medial,lateral,heel]
            public string markerFile = ""; // pressure_markers.csv, when Arduino markers were captured

            // Unloaded baseline the raw values are offset from — REQUIRED to zero each
            // channel offline. Last baseline captured before Record (channel order matches
            // channelNames); all baselines also appear in pressure_markers.csv.
            public int[] baseline;         // [toe, medial, lateral, heel] unloaded ADC, or null
            public long baselineArduinoMs; // Arduino millis() of that baseline capture
        }

        [Serializable]
        public class CognitiveEvent
        {
            public string onsetUtc = "";
            public string offsetUtc = "";
            public string prompt = "";
            public string response = "";
            public bool correct;
        }

        [Serializable]
        public class CognitiveTask
        {
            public string type = "None"; // "None" | "SerialSubtraction" | ... (M3)
            public List<CognitiveEvent> events = new();
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        public static SessionManifest FromJson(string json)
        {
            var m = new SessionManifest();
            JsonUtility.FromJsonOverwrite(json, m);
            return m;
        }

        // ---- integrity ----

        private string CanonicalString()
        {
            return $"{schemaVersion}|{patientId}|{sessionId}|{mode}|{exerciseClass}|{trialNumber}" +
                   $"|{zed.sampleCount}|{fsr.sampleCount}|{eeg.sampleCount}";
        }

        private static byte[] SaltedHash(string str)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes("salt1" + str + "salt2"));
        }

        public void Sign()
        {
            hash = Convert.ToBase64String(SaltedHash(CanonicalString()));
        }

        public bool Verify()
        {
            if (string.IsNullOrEmpty(hash))
            {
                Debug.LogWarning("Session manifest loaded without hash (treating as valid).");
                return true;
            }

            try
            {
                return SaltedHash(CanonicalString()).SequenceEqual(Convert.FromBase64String(hash));
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
