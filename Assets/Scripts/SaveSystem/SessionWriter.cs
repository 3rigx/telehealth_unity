using System;
using System.Collections.Generic;
using Assets.Scripts.Exercises;
using Assets.Scripts.SaveSystem.Writers;
using Assets.Scripts.Sensors;
using Assets.Scripts.Sensors.Eeg;
using Assets.Scripts.Sensors.FSR;
using Assets.Scripts.Util;
using UnityEngine;

namespace Assets.Scripts.SaveSystem
{
    /// <summary>
    ///     Writes one capture session to disk as separate per-sensor files plus a
    ///     <c>session.json</c> manifest, driven by <see cref="SessionContext"/>.
    ///     ZED and FSR rows are written 1:1 with the in-memory state list so the loader
    ///     can re-join by index. EEG is wired in M2; for now the manifest records whether
    ///     it was enabled but no eeg.csv is produced here.
    /// </summary>
    public static class SessionWriter
    {
        public static SessionManifest WriteFromContext(List<SensorSystemState> states, DateTime startUtc, DateTime endUtc,
            IReadOnlyList<EegSample> eegSamples = null,
            IReadOnlyList<PressureSample> pressureSamples = null,
            IReadOnlyList<PressureMarker> pressureMarkers = null)
        {
            var folder = SessionContext.SessionFolder;
            if (string.IsNullOrEmpty(folder))
            {
                Debug.LogError("SessionWriter: SessionContext.SessionFolder is not set.");
                return null;
            }

            states ??= new List<SensorSystemState>();

            var manifest = new SessionManifest
            {
                patientId = SessionContext.PatientId ?? "",
                sessionId = SessionContext.SessionId ?? "",
                mode = SessionContext.Mode.ToString(),
                exerciseClass = SessionContext.ExerciseClass.ToToken(),
                trialNumber = SessionContext.TrialNumber,
                startUtc = SessionTime.FormatUtc(startUtc),
                endUtc = SessionTime.FormatUtc(endUtc)
            };

            var rateHz = EstimateRateHz(states);

            // ---- ZED skeleton ----
            if (SessionContext.ZedEnabled)
            {
                var frames = ZedSkeletonWriter.Write(SessionPaths.ZedSkeletonPath(folder), states, out var bodyFormat);
                manifest.bodyFormat = bodyFormat;
                manifest.zed = new SessionManifest.SensorEntry
                {
                    enabled = true,
                    source = "live",
                    file = SessionPaths.ZedSkeletonFileName,
                    videoFile = SessionPaths.ZedVideoFileName,
                    sampleRateHz = rateHz,
                    sampleCount = frames
                };
            }
            else
            {
                manifest.zed = new SessionManifest.SensorEntry { enabled = false };
            }

            // ---- FSR / plantar pressure ----
            if (SessionContext.FsrEnabled && PressureConfig.IsSingleFoot)
            {
                // Single-insole path: write the full-rate (~100 Hz) stream captured by the
                // PressureRecorder, decoupled from the motion-frame list, plus any Arduino
                // markers. This is schema v2 (single foot, medial/lateral, arduino_ms).
                var rows = FootPressureCsvWriter.Write(SessionPaths.FsrPath(folder), pressureSamples, startUtc);
                var markerRows = 0;
                var markerFile = "";
                if (pressureMarkers != null && pressureMarkers.Count > 0)
                {
                    markerRows = FootPressureCsvWriter.WriteMarkers(SessionPaths.PressureMarkersPath(folder), pressureMarkers);
                    markerFile = SessionPaths.PressureMarkersFileName;
                }

                // Baseline the raw values are offset from (last one captured before Record).
                var baseline = LatestBaseline(pressureMarkers);
                manifest.fsr = new SessionManifest.SensorEntry
                {
                    enabled = true,
                    source = PlayerPrefs.GetString("FSRConntype", ""),
                    file = SessionPaths.FsrFileName,
                    sampleRateHz = EstimatePressureRateHz(pressureSamples),
                    sampleCount = rows,
                    insoleCount = 1,
                    foot = PressureConfig.Foot,
                    channelNames = PressureConfig.ChannelNames,
                    markerFile = markerFile,
                    baseline = baseline?.values,
                    baselineArduinoMs = baseline?.arduinoMs ?? 0
                };
                Debug.Log($"[Pressure] fsr.csv rows={rows}, markers={markerRows}, foot={PressureConfig.Foot}, " +
                          $"baseline={(baseline?.values != null ? string.Join(",", baseline.Value.values) : "none")}.");
            }
            else if (SessionContext.FsrEnabled)
            {
                // Legacy two-insole path (guarded behind PressureConfig.InsoleCount == 2).
                var rows = FsrCsvWriter.Write(SessionPaths.FsrPath(folder), states);
                manifest.fsr = new SessionManifest.SensorEntry
                {
                    enabled = true,
                    source = PlayerPrefs.GetString("FSRConntype", ""),
                    file = SessionPaths.FsrFileName,
                    sampleRateHz = rateHz,
                    sampleCount = rows,
                    insoleCount = 2,
                    channelNames = new[] { "toe", "medial", "lateral", "heel" }
                };
            }
            else
            {
                manifest.fsr = new SessionManifest.SensorEntry { enabled = false };
            }

            // ---- EEG (Unicorn, license-free raw SPP) ----
            if (SessionContext.EegEnabled && eegSamples != null && eegSamples.Count > 0)
            {
                var rows = EegCsvWriter.Write(SessionPaths.EegPath(folder), eegSamples);
                manifest.eeg = new SessionManifest.SensorEntry
                {
                    enabled = true,
                    source = "Unicorn",
                    file = SessionPaths.EegFileName,
                    sampleRateHz = 250f,
                    channels = 8,
                    sampleCount = rows
                };
            }
            else
            {
                manifest.eeg = new SessionManifest.SensorEntry
                {
                    enabled = SessionContext.EegEnabled,
                    source = SessionContext.EegEnabled ? "Unicorn" : "",
                    file = "",
                    sampleRateHz = SessionContext.EegEnabled ? 250f : 0f,
                    channels = SessionContext.EegEnabled ? 8 : 0,
                    sampleCount = 0
                };
            }

            manifest.Sign();

            try
            {
                System.IO.File.WriteAllText(SessionPaths.ManifestPath(folder), manifest.ToJson());
                Debug.Log($"Session written: {folder}");
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to write manifest: " + e.Message);
            }

            NotifyDashboard(manifest, folder);
            return manifest;
        }

        /// <summary>
        ///     Tell a connected Flutter dashboard that the session landed on disk so it
        ///     can refresh its library / offer to open the replay.
        /// </summary>
        private static void NotifyDashboard(SessionManifest manifest, string folder)
        {
            try
            {
                var server = Network.TelerehabWebSocketServer.Instance;
                if (server == null) return;
                string E(string s) => Network.TelerehabWebSocketServer.EscapeJson(s);
                server.Broadcast(
                    "{\"type\":\"session_saved\"" +
                    $",\"patientId\":\"{E(manifest.patientId)}\"" +
                    $",\"sessionId\":\"{E(manifest.sessionId)}\"" +
                    $",\"folder\":\"{E(folder)}\"}}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("session_saved broadcast failed: " + e.Message);
            }
        }

        /// <summary>Most recent baseline (with 4 values) in the marker stream — the one in
        /// effect at Record. Re-baselines only happen in preview, so the last wins.</summary>
        private static PressureMarker? LatestBaseline(IReadOnlyList<PressureMarker> markers)
        {
            if (markers == null) return null;
            for (int i = markers.Count - 1; i >= 0; i--)
                if (markers[i].kind == "BASELINE" && markers[i].values != null && markers[i].values.Length == 4)
                    return markers[i];
            return null;
        }

        /// <summary>
        ///     Estimate the pressure stream rate from the Arduino millis() span, so the
        ///     manifest records the true acquisition rate (~100 Hz), not the motion rate.
        /// </summary>
        private static float EstimatePressureRateHz(IReadOnlyList<PressureSample> samples)
        {
            if (samples == null || samples.Count < 2) return PressureConfig.SampleRateHz;
            var spanMs = samples[samples.Count - 1].arduinoMs - samples[0].arduinoMs;
            if (spanMs <= 0) return PressureConfig.SampleRateHz;
            return (float)((samples.Count - 1) * 1000.0 / spanMs);
        }

        /// <summary>Estimate sample rate from the captured timestamps; falls back to 10 Hz.</summary>
        private static float EstimateRateHz(List<SensorSystemState> states)
        {
            if (states == null || states.Count < 2) return 10f;
            var first = states[0].timestamp?.DateTime ?? DateTime.MinValue;
            var last = states[states.Count - 1].timestamp?.DateTime ?? DateTime.MinValue;
            var seconds = (last - first).TotalSeconds;
            if (seconds <= 0) return 10f;
            return (float)((states.Count - 1) / seconds);
        }
    }
}
