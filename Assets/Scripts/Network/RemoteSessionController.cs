using System;
using Assets.Scripts.Exercises;
using Assets.Scripts.Exercises.ExerciseTypes;
using Assets.Scripts.SaveSystem;
using Assets.Scripts.Util;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Network
{
    /// <summary>
    ///     Applies <c>configure_session</c> / <c>start_session</c> commands sent by the
    ///     Flutter dashboard. Mirrors what <c>TeleRehabMenu.StartSession</c> does for the
    ///     in-game menu: stores the sensor connection settings in PlayerPrefs, populates
    ///     <see cref="SessionContext"/>, creates the session folder and loads the capture
    ///     scene. Runs on the main thread (commands are dispatched via TelerehabDispatcher).
    /// </summary>
    public static class RemoteSessionController
    {
        public const string ExerciseSceneName = "CompleteExercise";
        public const string PredictionSceneName = "Prediction";

        private static bool _hasConfig;
        private static string _patientId = "";
        private static ExerciseClass _class = ExerciseClass.Motion;
        private static SessionMode _mode = SessionMode.Exercise;
        private static bool _zed = true, _fsr = true, _eeg;

        public static void Configure(TelerehabCommandMessage msg)
        {
            _patientId = msg.patientId ?? "";
            _class = ExerciseClassExtensions.FromToken(msg.exerciseClass);
            _mode = msg.mode == "Prediction" ? SessionMode.Prediction : SessionMode.Exercise;
            _zed = msg.zed;
            _fsr = msg.fsr;
            _eeg = msg.eeg;
            _hasConfig = true;

            // Sensor connection settings use the same PlayerPrefs keys as the
            // in-game settings screen, so existing connectors pick them up as-is.
            if (!string.IsNullOrEmpty(msg.fsrConnType)) PlayerPrefs.SetString("FSRConntype", msg.fsrConnType);
            if (!string.IsNullOrEmpty(msg.fsrUsbPort)) PlayerPrefs.SetString("FSRUsbPort", msg.fsrUsbPort);
            if (!string.IsNullOrEmpty(msg.fsrUri)) PlayerPrefs.SetString("FSRUri", msg.fsrUri);
            if (!string.IsNullOrEmpty(msg.fsrApiKey)) PlayerPrefs.SetString("FSRApiKey", msg.fsrApiKey);
            if (!string.IsNullOrEmpty(msg.eegComPort)) PlayerPrefs.SetString("EEGComPort", msg.eegComPort);
            PlayerPrefs.Save();

            Debug.Log($"[RemoteSession] Configured: {_patientId} / {_class.ToToken()} / {_mode}" +
                      $" zed={_zed} fsr={_fsr} eeg={_eeg} fsrConn={msg.fsrConnType}:{msg.fsrUsbPort}" +
                      $" eegComPort={PlayerPrefs.GetString("EEGComPort", "COM5")}");
        }

        /// <summary>Starts the configured capture. Returns null on success, else an error.</summary>
        public static string StartSession()
        {
            if (!_hasConfig || string.IsNullOrWhiteSpace(_patientId))
                return "No session configured — send configure_session first.";

            var patientId = SessionPaths.SanitizePatientId(_patientId);
            var scene = _mode == SessionMode.Prediction ? PredictionSceneName : ExerciseSceneName;
            if (!Application.CanStreamedLevelBeLoaded(scene))
                return $"Scene '{scene}' is not in Build Settings.";

            var startUtc = DateTime.UtcNow;
            SessionContext.PatientId = patientId;
            SessionContext.ExerciseClass = _class;
            SessionContext.Mode = _mode;
            SessionContext.ZedEnabled = _zed;
            SessionContext.FsrEnabled = _fsr;
            SessionContext.EegEnabled = _eeg;
            SessionContext.TrialNumber = SessionPaths.NextTrialNumber(patientId, _class.ToToken());
            SessionContext.SessionFolder =
                SessionPaths.CreateSessionFolder(patientId, _class.ToToken(), startUtc, out var sessionId);
            SessionContext.SessionId = sessionId;
            SessionContext.IsConfigured = true;

            var md = new ExerciseMetadata(0, "", _class.ToDisplay(), 0.1f, 0);
            md.patientData["patientId"] = patientId;
            md.patientData["class"] = _class.ToToken();
            md.patientData["mode"] = _mode.ToString();

            ExerciseProvider.isRemote = false;
            ExerciseProvider.exercise = new SandboxExercise(md);

            Debug.Log($"[RemoteSession] Start {_mode}/{_class.ToToken()} for {patientId} -> {SessionContext.SessionFolder}");
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
            return null;
        }
    }
}
