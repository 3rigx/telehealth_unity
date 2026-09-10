using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts.Exercises;
using Assets.Scripts.Exercises.ExerciseTypes;
using Assets.Scripts.SaveSystem.Writers;
using Assets.Scripts.Sensors;
using Assets.Scripts.Sensors.FSR;
using Assets.Scripts.Sensors.Zed;
using UnityEngine;

namespace Assets.Scripts.SaveSystem
{
    /// <summary>
    ///     Loads a capture session for replay. Reads <c>session.json</c>, loads each
    ///     per-sensor file, and re-joins ZED + FSR into the in-memory
    ///     <see cref="SensorSystemState"/> list the existing replay renderer expects.
    ///     EEG remains a parallel high-rate series (surfaced in M5), not fused here.
    ///
    ///     Falls back to the legacy fused-JSON format when the chosen file is an old save.
    /// </summary>
    public static class SessionLoader
    {
        /// <summary>
        ///     Accepts either a session folder, a <c>session.json</c> path, or a legacy
        ///     fused save .json. Returns an Exercise ready for the replay controller.
        /// </summary>
        public static Exercise LoadForReplay(string pathOrFolder)
        {
            var folder = ResolveSessionFolder(pathOrFolder, out var isLegacy);

            if (isLegacy)
            {
                Debug.Log("SessionLoader: legacy fused save detected, using SaveManager.");
                return SaveManager.LoadExercise(pathOrFolder);
            }

            if (folder == null)
            {
                Debug.LogError("SessionLoader: no session.json found at " + pathOrFolder);
                return null;
            }

            SessionManifest manifest;
            try
            {
                manifest = SessionManifest.FromJson(File.ReadAllText(SessionPaths.ManifestPath(folder)));
            }
            catch (Exception e)
            {
                Debug.LogError("SessionLoader: failed to read manifest: " + e.Message);
                return null;
            }

            if (!manifest.Verify())
                Debug.LogWarning("SessionLoader: manifest hash mismatch (loading anyway).");

            var skeletonSeries = ZedSkeletonWriter.Read(SessionPaths.ZedSkeletonPath(folder));
            var fsrRows = FsrCsvWriter.Read(SessionPaths.FsrPath(folder));

            var states = ReJoin(skeletonSeries.frames, fsrRows);

            var md = BuildMetadata(manifest, states, skeletonSeries.bodyFormat);
            return new SandboxExercise(md, states);
        }

        private static List<SensorSystemState> ReJoin(List<ZedSkeletonWriter.Frame> frames, List<FsrCsvWriter.Row> fsrRows)
        {
            frames ??= new List<ZedSkeletonWriter.Frame>();
            fsrRows ??= new List<FsrCsvWriter.Row>();

            var count = Mathf.Max(frames.Count, fsrRows.Count);
            var states = new List<SensorSystemState>(count);

            for (var i = 0; i < count; i++)
            {
                var sk = i < frames.Count && frames[i].skeleton != null ? frames[i].skeleton : new SkeletonState();
                // NonSerialized Format is null/default after JSON load; rebuild from the serialized enum.
                sk.Format = new BodyFormat(sk.m_Format);

                var left = i < fsrRows.Count ? fsrRows[i].left : new FSRState();
                var right = i < fsrRows.Count ? fsrRows[i].right : new FSRState();
                var tMs = i < frames.Count ? frames[i].tMs : (i < fsrRows.Count ? fsrRows[i].tMs : 0);

                var st = new SensorSystemState(tMs, left, right, sk);
                if (i < frames.Count && !string.IsNullOrEmpty(frames[i].timestampUtc))
                    st.timestamp = SessionTime.ParseUtc(frames[i].timestampUtc);
                else if (i < fsrRows.Count && !string.IsNullOrEmpty(fsrRows[i].timestampUtc))
                    st.timestamp = SessionTime.ParseUtc(fsrRows[i].timestampUtc);

                states.Add(st);
            }

            return states;
        }

        private static ExerciseMetadata BuildMetadata(SessionManifest manifest, List<SensorSystemState> states, int bodyFormat)
        {
            var md = new ExerciseMetadata
            {
                bodyFormat = (BODY_FORMAT)bodyFormat,
                measurementInterval = EstimateInterval(states),
                isProcessed = false
            };
            md.patientData["patientId"] = manifest.patientId ?? "";
            md.patientData["class"] = manifest.exerciseClass ?? "";
            md.patientData["sessionId"] = manifest.sessionId ?? "";
            md.patientData["mode"] = manifest.mode ?? "";
            return md;
        }

        private static float EstimateInterval(List<SensorSystemState> states)
        {
            if (states == null || states.Count < 2) return 0.1f;
            var first = states[0].timestamp?.DateTime ?? DateTime.MinValue;
            var last = states[states.Count - 1].timestamp?.DateTime ?? DateTime.MinValue;
            var seconds = (last - first).TotalSeconds;
            if (seconds <= 0) return 0.1f;
            return (float)(seconds / (states.Count - 1));
        }

        /// <summary>
        ///     Resolves the chosen path to a session folder. Sets <paramref name="isLegacy"/>
        ///     when the path is an old fused save (no session.json).
        /// </summary>
        private static string ResolveSessionFolder(string path, out bool isLegacy)
        {
            isLegacy = false;
            if (string.IsNullOrEmpty(path)) return null;

            if (Directory.Exists(path))
            {
                if (File.Exists(Path.Combine(path, SessionPaths.ManifestFileName))) return path;
                isLegacy = true; // a directory with no manifest is not ours
                return null;
            }

            var name = Path.GetFileName(path);
            if (string.Equals(name, SessionPaths.ManifestFileName, StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(path);

            // Some other .json -> treat as legacy fused save.
            isLegacy = true;
            return null;
        }
    }
}
