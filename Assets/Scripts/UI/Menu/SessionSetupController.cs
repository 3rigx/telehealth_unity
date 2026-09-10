using System;
using Assets.Scripts.Exercises;
using Assets.Scripts.Exercises.ExerciseTypes;
using Assets.Scripts.SaveSystem;
using Assets.Scripts.Util;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    /// <summary>
    ///     Session setup screen shown before an Exercise / Prediction capture.
    ///     Collects the patient ID (pick-or-create), the active sensors, and the exercise
    ///     class, populates <see cref="SessionContext"/> + creates the session folder, then
    ///     loads the capture scene.
    /// </summary>
    public class SessionSetupController : MonoBehaviour, MenuScreenController
    {
        [Header("Navigation")]
        public MenuController menuController;
        public string exerciseSceneName = "CompleteExercise";
        public string predictionSceneName = "Prediction";

        [Header("Patient")]
        public TMP_Dropdown patientDropdown;
        public TMP_InputField newPatientField;

        [Header("Sensors")]
        public Toggle zedToggle;
        public Toggle fsrToggle;
        public Toggle eegToggle;

        [Header("Class")]
        public TMP_Dropdown classDropdown; // Idle / Motion / Motion + Cognitive

        [Header("Optional UI")]
        public TMP_Text titleText;
#nullable enable
        public PopupController? errorController;
#nullable disable

        private const string NewPatientLabel = "➕ New patient";
        private SessionMode _mode = SessionMode.Exercise;

        // ---- entry points wired to the main menu buttons ----

        public void OpenForExercise()
        {
            _mode = SessionMode.Exercise;
            Open();
        }

        public void OpenForPrediction()
        {
            _mode = SessionMode.Prediction;
            Open();
        }

        private void Open()
        {
            if (menuController != null) menuController.SetScreen<SessionSetupController>();
            else gameObject.SetActive(true);
        }

        private void OnEnable()
        {
            RefreshPatients();
            EnsureClassOptions();
            if (titleText != null)
                titleText.text = _mode == SessionMode.Prediction ? "Prediction — Setup" : "Exercise — Setup";
        }

        private void RefreshPatients()
        {
            if (patientDropdown == null) return;
            patientDropdown.ClearOptions();
            patientDropdown.options.Add(new TMP_Dropdown.OptionData(NewPatientLabel));
            foreach (var p in SessionPaths.ListPatients())
                patientDropdown.options.Add(new TMP_Dropdown.OptionData(p));
            patientDropdown.value = 0;
            patientDropdown.RefreshShownValue();
        }

        private void EnsureClassOptions()
        {
            if (classDropdown == null || classDropdown.options.Count == 3) return;
            classDropdown.ClearOptions();
            // Order MUST match the ExerciseClass enum: Idle=0, Motion=1, MotionCognitive=2
            classDropdown.options.Add(new TMP_Dropdown.OptionData(ExerciseClass.Idle.ToDisplay()));
            classDropdown.options.Add(new TMP_Dropdown.OptionData(ExerciseClass.Motion.ToDisplay()));
            classDropdown.options.Add(new TMP_Dropdown.OptionData(ExerciseClass.MotionCognitive.ToDisplay()));
            classDropdown.value = 1; // default Motion
            classDropdown.RefreshShownValue();
        }

        // ---- start ----

        public void OnStart()
        {
            var patientId = ResolvePatientId();
            if (patientId == null) return;

            var cls = (ExerciseClass)(classDropdown != null ? classDropdown.value : 1);

            var scene = _mode == SessionMode.Prediction ? predictionSceneName : exerciseSceneName;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                ShowError(_mode == SessionMode.Prediction
                    ? "Prediction scene is not available yet."
                    : $"Scene '{scene}' is not in Build Settings.");
                return;
            }

            var startUtc = DateTime.UtcNow;

            SessionContext.PatientId = patientId;
            SessionContext.ExerciseClass = cls;
            SessionContext.Mode = _mode;
            SessionContext.ZedEnabled = zedToggle == null || zedToggle.isOn;
            SessionContext.FsrEnabled = fsrToggle == null || fsrToggle.isOn;
            SessionContext.EegEnabled = eegToggle != null && eegToggle.isOn;
            SessionContext.TrialNumber = SessionPaths.NextTrialNumber(patientId, cls.ToToken());
            SessionContext.SessionFolder =
                SessionPaths.CreateSessionFolder(patientId, cls.ToToken(), startUtc, out var sessionId);
            SessionContext.SessionId = sessionId;
            SessionContext.IsConfigured = true;

            // Build a fresh, empty exercise to capture into.
            var md = new ExerciseMetadata(0, "", cls.ToDisplay(), 0.1f, 0);
            md.patientData["patientId"] = patientId;
            md.patientData["class"] = cls.ToToken();
            md.patientData["mode"] = _mode.ToString();

            ExerciseProvider.isRemote = false; // also clears remote file locations
            ExerciseProvider.exercise = new SandboxExercise(md);

            Debug.Log($"Starting {_mode} session for {patientId} ({cls.ToToken()}) -> {SessionContext.SessionFolder}");
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }

        private string ResolvePatientId()
        {
            if (patientDropdown == null)
            {
                ShowError("Patient selector is not wired.");
                return null;
            }

            if (patientDropdown.value == 0) // New patient
            {
                var raw = newPatientField != null ? newPatientField.text : "";
                if (string.IsNullOrWhiteSpace(raw))
                {
                    ShowError("Enter a patient ID for the new patient.");
                    return null;
                }

                return SessionPaths.SanitizePatientId(raw);
            }

            return patientDropdown.options[patientDropdown.value].text;
        }

        private void ShowError(string msg)
        {
            Debug.LogWarning("SessionSetup: " + msg);
            if (errorController != null) errorController.Show(msg);
        }
    }
}
