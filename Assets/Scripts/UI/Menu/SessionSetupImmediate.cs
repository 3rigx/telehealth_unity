using System;
using System.Collections.Generic;
using Assets.Scripts.Exercises;
using Assets.Scripts.Exercises.ExerciseTypes;
using Assets.Scripts.SaveSystem;
using Assets.Scripts.Util;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.UI
{
    /// <summary>
    ///     Zero-wiring session setup overlay for Milestone 1 verification.
    ///     Draws its own UI with IMGUI (no Canvas / prefabs / TMP needed): just add this
    ///     component to ANY GameObject in the Menu scene and press Play.
    ///
    ///     It collects patient ID (pick-or-create), active sensors, and exercise class,
    ///     fills <see cref="SessionContext"/>, creates the session folder, and loads the
    ///     capture scene. The polished in-scene menu version arrives in M3.
    /// </summary>
    public class SessionSetupImmediate : MonoBehaviour
    {
        [Tooltip("Show the overlay automatically when the scene starts.")]
        public bool showOnStart = true;

        [Tooltip("Key that toggles the overlay on/off.")]
        public KeyCode toggleKey = KeyCode.F9;

        public string exerciseSceneName = "CompleteExercise";

        private bool _visible;
        private string _patientId = "";
        private bool _zed = true, _fsr = true, _eeg;
        private int _classIndex = 1; // 0 Idle, 1 Motion, 2 MotionCognitive
        private string _message = "";
        private List<string> _patients = new();
        private Vector2 _scroll;

        private static readonly string[] ClassLabels =
        {
            "Idle", "Motion", "Motion + Cognitive"
        };

        private void Start()
        {
            _visible = showOnStart;
            RefreshPatients();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _visible = !_visible;
                if (_visible) RefreshPatients();
            }
        }

        private void RefreshPatients()
        {
            _patients = SessionPaths.ListPatients();
        }

        private void OnGUI()
        {
            if (!_visible) return;

            const float w = 460f, h = 470f;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 14, rect.y + 12, rect.width - 28, rect.height - 24));

            GUILayout.Label("<b>Start Session (M1)</b>", RichLabel(16));
            GUILayout.Space(6);

            // ---- Patient ID ----
            GUILayout.Label("Patient ID");
            _patientId = GUILayout.TextField(_patientId ?? "");

            if (_patients.Count > 0)
            {
                GUILayout.Label("Existing patients (click to use):", RichLabel(11));
                _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(70));
                foreach (var p in _patients)
                    if (GUILayout.Button(p, GUILayout.Height(20)))
                        _patientId = p;
                GUILayout.EndScrollView();
            }

            GUILayout.Space(8);

            // ---- Sensors ----
            GUILayout.Label("Sensors");
            _zed = GUILayout.Toggle(_zed, " ZED (skeleton + video)");
            _fsr = GUILayout.Toggle(_fsr, " FSR (foot pressure)");
            _eeg = GUILayout.Toggle(_eeg, " EEG (Unicorn) — wired in M2");

            GUILayout.Space(8);

            // ---- Class ----
            GUILayout.Label("Exercise class");
            _classIndex = GUILayout.SelectionGrid(_classIndex, ClassLabels, 3);

            GUILayout.Space(12);

            if (GUILayout.Button("Start", GUILayout.Height(34)))
                StartSession();

            if (!string.IsNullOrEmpty(_message))
            {
                GUILayout.Space(6);
                GUILayout.Label(_message, RichLabel(11));
            }

            GUILayout.Space(4);
            GUILayout.Label($"Toggle this panel with [{toggleKey}].", RichLabel(10));

            GUILayout.EndArea();
        }

        private void StartSession()
        {
            if (string.IsNullOrWhiteSpace(_patientId))
            {
                _message = "<color=#ff6666>Enter a patient ID.</color>";
                return;
            }

            var patientId = SessionPaths.SanitizePatientId(_patientId);
            var cls = (ExerciseClass)_classIndex;

            if (!Application.CanStreamedLevelBeLoaded(exerciseSceneName))
            {
                _message = $"<color=#ff6666>Scene '{exerciseSceneName}' is not in Build Settings.</color>";
                return;
            }

            var startUtc = DateTime.UtcNow;

            SessionContext.PatientId = patientId;
            SessionContext.ExerciseClass = cls;
            SessionContext.Mode = SessionMode.Exercise;
            SessionContext.ZedEnabled = _zed;
            SessionContext.FsrEnabled = _fsr;
            SessionContext.EegEnabled = _eeg;
            SessionContext.TrialNumber = SessionPaths.NextTrialNumber(patientId, cls.ToToken());
            SessionContext.SessionFolder =
                SessionPaths.CreateSessionFolder(patientId, cls.ToToken(), startUtc, out var sessionId);
            SessionContext.SessionId = sessionId;
            SessionContext.IsConfigured = true;

            var md = new ExerciseMetadata(0, "", cls.ToDisplay(), 0.1f, 0);
            md.patientData["patientId"] = patientId;
            md.patientData["class"] = cls.ToToken();
            md.patientData["mode"] = SessionMode.Exercise.ToString();

            ExerciseProvider.isRemote = false;
            ExerciseProvider.exercise = new SandboxExercise(md);

            Debug.Log($"[SessionSetupImmediate] Starting {cls.ToToken()} for {patientId} -> {SessionContext.SessionFolder}");
            SceneManager.LoadScene(exerciseSceneName, LoadSceneMode.Single);
        }

        private static GUIStyle RichLabel(int fontSize)
        {
            return new GUIStyle(GUI.skin.label) { richText = true, fontSize = fontSize, wordWrap = true };
        }
    }
}
