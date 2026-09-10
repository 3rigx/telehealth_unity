using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.SaveSystem
{
    /// <summary>
    ///     Owns the on-disk session layout:
    ///     <c>&lt;persistentDataPath&gt;/Sessions/&lt;PatientId&gt;/&lt;timestamp&gt;_&lt;Class&gt;/{session.json, zed_skeleton.json, zed_video.svo, fsr.csv, eeg.csv}</c>
    ///     Also enumerates existing patients/sessions for the pick-or-create UI and
    ///     sanitizes patient IDs into filesystem-safe folder names.
    /// </summary>
    public static class SessionPaths
    {
        public const string ManifestFileName = "session.json";
        public const string ZedSkeletonFileName = "zed_skeleton.json";
        public const string ZedVideoFileName = "zed_video.svo";
        public const string FsrFileName = "fsr.csv";
        public const string PressureMarkersFileName = "pressure_markers.csv";
        public const string EegFileName = "eeg.csv";

        public static string Root => Path.Combine(Application.persistentDataPath, "Sessions");

        /// <summary>
        ///     Reduce arbitrary user input to a stable, filesystem-safe slug so the same
        ///     patient never fragments across typo'd folders.
        /// </summary>
        public static string SanitizePatientId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "unknown";

            var sb = new StringBuilder(raw.Length);
            foreach (var ch in raw.Trim())
            {
                if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                    sb.Append(ch);
                else if (char.IsWhiteSpace(ch))
                    sb.Append('_');
                // everything else dropped
            }

            var slug = sb.ToString().Trim('_');
            return slug.Length == 0 ? "unknown" : slug;
        }

        public static List<string> ListPatients()
        {
            try
            {
                if (!Directory.Exists(Root)) return new List<string>();
                return Directory.GetDirectories(Root)
                    .Select(Path.GetFileName)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception e)
            {
                Debug.LogError("ListPatients failed: " + e.Message);
                return new List<string>();
            }
        }

        public static string PatientFolder(string patientId)
        {
            return Path.Combine(Root, SanitizePatientId(patientId));
        }

        public static List<string> ListSessions(string patientId)
        {
            var dir = PatientFolder(patientId);
            if (!Directory.Exists(dir)) return new List<string>();
            return Directory.GetDirectories(dir).Select(Path.GetFileName).ToList();
        }

        /// <summary>Next 1-based trial number for this patient + class.</summary>
        public static int NextTrialNumber(string patientId, string classToken)
        {
            var existing = ListSessions(patientId)
                .Count(name => name.EndsWith("_" + classToken, StringComparison.Ordinal));
            return existing + 1;
        }

        /// <summary>
        ///     Create (and return) the absolute session folder for a new capture.
        /// </summary>
        public static string CreateSessionFolder(string patientId, string classToken, DateTime startUtc, out string sessionId)
        {
            sessionId = $"{startUtc.ToLocalTime():yyyy-MM-dd_HH-mm-ss}_{classToken}";
            var folder = Path.Combine(PatientFolder(patientId), sessionId);
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static string ManifestPath(string folder) => Path.Combine(folder, ManifestFileName);
        public static string ZedSkeletonPath(string folder) => Path.Combine(folder, ZedSkeletonFileName);
        public static string ZedVideoPath(string folder) => Path.Combine(folder, ZedVideoFileName);
        public static string FsrPath(string folder) => Path.Combine(folder, FsrFileName);
        public static string PressureMarkersPath(string folder) => Path.Combine(folder, PressureMarkersFileName);
        public static string EegPath(string folder) => Path.Combine(folder, EegFileName);
    }
}
