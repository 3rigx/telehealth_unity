using System;
using System.Collections;
using Assets.Scripts.Exercises.ExerciseTypes;
using Assets.Scripts.SaveSystem;
using Assets.Scripts.Sensors;
using Assets.Scripts.Sensors.Eeg;
using Assets.Scripts.Sensors.FSR;
using Assets.Scripts.Sensors.Zed;
using Assets.Scripts.UI;
using Assets.Scripts.Util;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Exercises
{
    [DisallowMultipleComponent]
    public class ExerciseController : MonoBehaviour
    {
        private float _readInterval = 0.1f, _updateInterval = 0.05f;
        private SensorSystemController _sensorSystemController;
        private StatisticsController _statisticsController;
        private CustomZedController _zedController;
        private bool _sessionSaved;
        private bool _started;     // warmup/preview begun
        private bool _previewing;   // live sensor loop running (preview + broadcast)
        private bool _recording;    // capture actively being persisted to disk
        private bool _everRecorded; // recording was started at least once this session
        private Coroutine _sensorLoop, _readLoop;
        private EegRecorder _eeg;   // null unless SessionContext.EegEnabled
        private PressureRecorder _pressure; // null unless single-insole FSR is enabled
        private long _lastPressureSampleCount; // to detect whether FSR samples are arriving
        private Vector3[] _broadcastRestPositions;
        public bool AutoStart = false;
        public string DataFileLocation = "";
        public bool IsSandBox = true;
        public string VideoFileLocation = "";
        public bool syncCalc = false;

        private IEnumerator SensorUpdateCoroutine()
        {
            _sensorSystemController?.Read();
            while (true)
            {
                if (_sensorSystemController == null) continue;
                var newstate = _sensorSystemController.GetState();
                if (DebugMenu != null) DebugMenu.SetExerciseState(newstate);
                try
                {
                    if (ScoreDisplay != null && _exercise != null) ScoreDisplay.text = _exercise.Score().ToString();
                }
                catch (NotImplementedException)
                {
                }


                // Only persist frames once recording has been started from the
                // dashboard — preview/warm-up frames are broadcast for live view
                // but never written to the session file.
                if (_recording && _exercise != null && !_exercise.IsPaused())
                    _exercise.Update(newstate);

                _eeg?.Tick(); // drain EEG + refresh live band-power metrics
                _pressure?.Tick(); // drain full-rate FSR + refresh live foot state

                BroadcastToDashboard(newstate);

                if (syncCalc && _recording)
                {
                    _statisticsController.UpdateStatistics(newstate);
                }


                yield return new WaitForSecondsRealtime(_updateInterval);
            }
        }


        /// <summary>
        ///     Streams the current sensor frame to connected Flutter dashboards. Joint
        ///     angles are measured against the first tracked pose (or the pose at the
        ///     last set_rest_position command).
        /// </summary>
        private void BroadcastToDashboard(SensorSystemState state)
        {
            if (Network.TelerehabWebSocketServer.Instance == null || state == null) return;

            Replay.JointAngleCalculator.JointAngle[] angles = null;
            var sk = state.skeleton;
            if (sk?.JointPos != null && sk.JointPos.Length > 0 &&
                !float.IsNaN(sk.JointPos[0].x)) // pelvis NaN = no body tracked yet
            {
                _broadcastRestPositions ??= (Vector3[])sk.JointPos.Clone();
                angles = Replay.JointAngleCalculator.CalculateAllJointAngles(
                    sk.JointPos, sk.Format, _broadcastRestPositions);
            }

            // Single-insole live view: the PressureRecorder owns the FSR port (not the
            // SensorSystemController), so inject its latest reading onto the configured
            // foot before broadcasting. The other foot stays empty.
            if (_pressure != null)
            {
                if (PressureConfig.Foot == "left") state.leftFoot = _pressure.LatestState;
                else state.rightFoot = _pressure.LatestState;
            }

            // Surface a "baseline captured under load" warning to the dashboard.
            Network.TelerehabBroadcaster.PressureBaselineUnderLoad = _pressure?.BaselineUnderLoad ?? false;

            // FSR "streaming" indicator: true only while pad samples are actually arriving,
            // so the dashboard can show amber ("port open, no data") vs green ("streaming").
            long ps = _pressure?.SamplesReceived ?? 0;
            Network.TelerehabBroadcaster.PressureStreaming = _pressure != null && ps > _lastPressureSampleCount;
            _lastPressureSampleCount = ps;

            Network.TelerehabBroadcaster.BroadcastState(state, angles,
                cameraConnected: _sensorSystemController.IsReady(),
                pressureConnected: _pressure != null
                    ? _pressure.Connected
                    : _sensorSystemController.FsrConnector != null,
                eeg: _eeg != null ? _eeg.Live : null);
        }

        private IEnumerator ReadSensorCoroutine()
        {
            while (_sensorSystemController != null)
            {
                _sensorSystemController.Read();
                yield return new WaitForSecondsRealtime(_readInterval);
            }
        }

        private IEnumerator SaveRemoteAndMenuCoroutine(string datafile, string videofile)
        {
            yield return RemoteManager.SaveRemoteCoroutine(datafile, videofile);
            Debug.Log("Exercise Processed");
            SceneManager.LoadScene("Menu");
        }

        public void Close()
        {
            StopSensorLoops();
            _zedController.StopRecording();
            Network.TelerehabBroadcaster.SetRecording(false);
            if (_exercise != null)
            {
                if (SessionContext.IsConfigured)
                {
                    SaveSessionIfNeeded();
                }
                else
                {
                    _exercise.Stop();
                    SaveManager.Save(DataFileLocation, _exercise);
                    if (ExerciseProvider.isRemote)
                        StartCoroutine(RemoteManager.SaveRemoteCoroutine(DataFileLocation, VideoFileLocation));
                }
            }

            ClearExerciseProviderState();
            Debug.Log("Exercise closed");
            SceneManager.LoadScene("Menu");
        }

        /// <summary>
        ///     Clears the static exercise handoff so the next scene load does not
        ///     auto-start a stale exercise. Without this, returning to the menu and
        ///     entering the exercise scene again (without a fresh configure step) makes
        ///     ExerciseController.Start() see a leftover ExerciseProvider.exercise, auto-run
        ///     DelayedStart with no SessionContext, and pop the legacy "Save location" dialog.
        /// </summary>
        private static void ClearExerciseProviderState()
        {
            ExerciseProvider.exercise = null;
            ExerciseProvider.isRemote = false; // also nulls the remote file locations
        }

        public void Discard()
        {
            StopSensorLoops();
            _zedController.StopRecording();
            Network.TelerehabBroadcaster.SetRecording(false);
            if (SessionContext.IsConfigured)
            {
                // A discarded capture should not leave an empty/partial folder behind
                // (it would also inflate the next trial number).
                try
                {
                    if (!string.IsNullOrEmpty(SessionContext.SessionFolder) &&
                        System.IO.Directory.Exists(SessionContext.SessionFolder))
                        System.IO.Directory.Delete(SessionContext.SessionFolder, true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Failed to remove discarded session folder: " + e.Message);
                }

                SessionContext.Reset();
            }

            _sessionSaved = true; // never persist a discarded session via the safety net
            ClearExerciseProviderState();
            SceneManager.LoadScene("Menu");
        }

        /// <summary>
        ///     Writes the per-sensor session files once, if a configured session has not
        ///     been saved yet. Called from Close() and as a safety net from OnDestroy(), so
        ///     a capture is persisted even when the session is ended by stopping Play mode
        ///     or an unexpected scene unload instead of the Close button.
        /// </summary>
        private void SaveSessionIfNeeded()
        {
            if (_sessionSaved || !SessionContext.IsConfigured || _exercise == null) return;
            _sessionSaved = true;

            // Warm-up/preview only, Record was never pressed — don't leave an empty
            // session folder behind (it would also inflate the next trial number).
            if (!_everRecorded)
            {
                try
                {
                    if (!string.IsNullOrEmpty(SessionContext.SessionFolder) &&
                        System.IO.Directory.Exists(SessionContext.SessionFolder))
                        System.IO.Directory.Delete(SessionContext.SessionFolder, true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Failed to remove unused session folder: " + e.Message);
                }

                SessionContext.Reset();
                Debug.Log("[Exercise] Closed without recording — no session written.");
                return;
            }

            _eeg?.Stop();      // idempotent — finalises EEG samples if Close() wasn't the exit path
            _pressure?.Stop(); // idempotent — finalises full-rate FSR samples + markers
            _exercise.Stop();
            var md = (_exercise as Exercise)?.GetMetadata();
            var startUtc = md?.start != null ? md.start.DateTime : DateTime.UtcNow;
            var endUtc = md?.end != null ? md.end.DateTime : DateTime.UtcNow;
            SessionWriter.WriteFromContext(_exercise.GetSensorStates(), startUtc, endUtc,
                _eeg?.Samples, _pressure?.Samples, _pressure?.Markers);
            SessionContext.Reset();
        }

        private void OnDestroy()
        {
            Network.TelerehabCommandBus.OnCommand -= OnDashboardCommand;
            // Safety net: persist the capture even if Close() was never called.
            SaveSessionIfNeeded();
        }

        /// <summary>
        ///     Lets the Flutter dashboard drive the capture: stop saves + returns to the
        ///     menu (same as the Close button), start kicks off a not-yet-started exercise.
        /// </summary>
        private void OnDashboardCommand(Network.TelerehabCommand cmd, string payload)
        {
            switch (cmd)
            {
                case Network.TelerehabCommand.StartRecording:
                    // Scene load already warmed up the sensors and started the
                    // preview; this begins the actual capture.
                    if (!_started) DelayedStart();
                    BeginRecording();
                    break;
                case Network.TelerehabCommand.StopRecording:
                    Close();
                    break;
                case Network.TelerehabCommand.PauseRecording:
                    // Pause only means something once recording has actually begun;
                    // otherwise it would flip the recording indicator without capturing.
                    if (_recording) ToggleBrake();
                    break;
                case Network.TelerehabCommand.MarkEvent:
                    // Protocol block boundary (or manual mark): tell the Arduino to emit a
                    // #MARK so plantar pressure can be aligned to EEG / skeleton.
                    _pressure?.SendMarker();
                    break;
                case Network.TelerehabCommand.Rebaseline:
                    // Manual re-baseline from the dashboard (preview): re-capture unloaded.
                    _pressure?.RequestBaseline();
                    break;
                case Network.TelerehabCommand.SetRestPosition:
                    // Next broadcast frame re-captures the rest pose.
                    _broadcastRestPositions = null;
                    break;
            }
        }

        private void Start()
        {
            Network.TelerehabCommandBus.OnCommand += OnDashboardCommand;
            _sensorSystemController = FindObjectsByType<SensorSystemController>()[0];
            _zedController = FindObjectsByType<CustomZedController>()[0];
            if (AutoStart || ExerciseProvider.exercise != null) DelayedStart();
        }

        public void DelayedStart()
        {
            // The dashboard auto-starts the capture on scene load; pressing the
            // dashboard's record button (or AutoStart) must not kick off a
            // second exercise + duplicate sensor coroutines.
            if (_started) return;
            _started = true;

            if (StartButton != null) StartButton.SetActive(false);
            if (ExerciseProvider.exercise == null)
            {
                if (IsSandBox)
                {
                    ExerciseProvider.exercise = new SandboxExercise(new ExerciseMetadata(0, "Test", "Test", 0.1f, 10));
                }
                else
                {
                    string[] loadLoc;
                    do
                    {
                        loadLoc = StandaloneFileBrowser.StandaloneFileBrowser.OpenFilePanel("Open instruction file", "",
                            ".json",
                            false);
                    } while (loadLoc.Length != 1);

                    ExerciseProvider.exercise = SaveManager.LoadExercise(loadLoc[0]);
                }
            }

            _exercise = ExerciseProvider.exercise;
            if (SessionContext.IsConfigured)
            {
                // New per-sensor capture: paths are derived from the session folder,
                // no manual save dialogs.
                VideoFileLocation = SessionPaths.ZedVideoPath(SessionContext.SessionFolder);
                DataFileLocation = SessionPaths.ManifestPath(SessionContext.SessionFolder);
            }
            else
            {
                DataFileLocation = ExerciseProvider.dataFileLocation ?? "";
                VideoFileLocation = ExerciseProvider.videoFileLocation ?? "";
                if (DataFileLocation == "")
                    DataFileLocation =
                        StandaloneFileBrowser.StandaloneFileBrowser.SaveFilePanel("Save location", "", "save", "json");
                if (VideoFileLocation == "")
                    VideoFileLocation =
                        StandaloneFileBrowser.StandaloneFileBrowser.SaveFilePanel("Save location", "", "save", "svo");
            }

            _statisticsController = new StatisticsController(_exercise.GetSensorStates());
            if (_exercise is Exercise exercise1)
            {
                _readInterval = exercise1.GetMetadata().measurementInterval;
                _updateInterval = exercise1.GetMetadata().measurementInterval * 2;
            }

            SetTargetEndDisplay(_exercise.GetTargetEnd());

            StartCoroutine(WarmupAndPreviewCoroutine());
        }

        /// <summary>
        ///     Warms up the sensors and starts the live preview/broadcast loop, but does
        ///     NOT record. The clinician sees the live skeleton + pressure in the dashboard
        ///     while positioning the patient, then presses Record (start_recording) to begin
        ///     the actual capture — so the 20 s ZED warm-up window is never recorded.
        /// </summary>
        private IEnumerator WarmupAndPreviewCoroutine()
        {
            if (_exercise == null) yield break;
            if (_sensorSystemController == null) yield break;

            // Only wait for the ZED when it's actually part of this session. The ZED
            // opens asynchronously (~1-2 s) and IsReady() gates on it, so with the camera
            // DISABLED (e.g. FSR-only testing) IsReady() never turns true and the old
            // unconditional loop burned the full 20 s timeout before every preview.
            if (SessionContext.ZedEnabled)
            {
                var waited = 0f;
                const float timeout = 20f;
                while (!_sensorSystemController.IsReady() && waited < timeout)
                {
                    yield return new WaitForSecondsRealtime(0.25f);
                    waited += 0.25f;
                }

                if (_sensorSystemController.IsReady())
                    Debug.Log($"[Exercise] ZED ready after {waited:0.0}s — live preview running, " +
                              "waiting for Record from the dashboard.");
                else
                    Debug.LogWarning($"[Exercise] ZED not ready after {timeout}s; previewing anyway " +
                                     "(camera may be in use by ZED Explorer, or SDK not initialised).");
            }
            else
            {
                Debug.Log("[Exercise] ZED disabled for this session — skipping camera warm-up, previewing now.");
            }

            StartPreview();
        }

        /// <summary>Starts the live sensor loop (preview + dashboard broadcast), idempotent.</summary>
        private void StartPreview()
        {
            if (_previewing) return;
            _previewing = true;
            Network.TelerehabBroadcaster.SetRecording(false); // standby, not recording
            StartPressureSource(); // stream now so the live view + pad check work pre-record
            _sensorLoop = StartCoroutine(SensorUpdateCoroutine());
            _readLoop = StartCoroutine(ReadSensorCoroutine());
        }

        /// <summary>
        ///     Opens the single-insole pressure source at PREVIEW so the live view and the
        ///     pre-record pad check work; the serial handshake (Z baseline → S stream) runs
        ///     on its own thread. Disk capture is still gated on Record (BeginCapture()).
        /// </summary>
        private void StartPressureSource()
        {
            if (_pressure != null) return; // idempotent
            if (!SessionContext.FsrEnabled || !PressureConfig.IsSingleFoot) return;

            var conntype = PlayerPrefs.GetString("FSRConntype", "");
            IPressureSource src;
            if (conntype == "Mock")
            {
                src = new MockFootPressureSource();
            }
            else
            {
                var fport = PlayerPrefs.GetString("FSRUsbPort", "");
                if (string.IsNullOrEmpty(fport))
                {
                    Debug.LogWarning("[Exercise] No FSRUsbPort set; continuing without pressure.");
                    return;
                }
                var overrideBaud = PlayerPrefs.HasKey("PressureBaudRate");
                Debug.Log($"[Exercise] Pressure baud {PressureConfig.BaudRate} " +
                          $"({(overrideBaud ? "PlayerPrefs override" : "config default 115200")}).");
                src = new SerialFootPressureSource(fport, PressureConfig.BaudRate, overrideBaud);
            }

            _pressure = new PressureRecorder();
            if (_pressure.Start(src))
                Debug.Log($"[Exercise] Pressure streaming for preview ({conntype}, foot={PressureConfig.Foot}).");
            else { Debug.LogWarning("[Exercise] Pressure connect failed; continuing without pressure."); _pressure = null; }
        }

        /// <summary>Stops the preview/recording loops via their handles (StopCoroutine by
        /// method name doesn't work — it builds a fresh, non-matching enumerator).</summary>
        private void StopSensorLoops()
        {
            if (_sensorLoop != null) StopCoroutine(_sensorLoop);
            if (_readLoop != null) StopCoroutine(_readLoop);
            _sensorLoop = _readLoop = null;
            _previewing = false;
            _recording = false;
            // Stop acquisition but KEEP the buffered samples — SaveSessionIfNeeded
            // (called right after) writes them to eeg.csv / fsr.csv.
            _eeg?.Stop();
            _pressure?.Stop();
        }

        /// <summary>
        ///     Begins the real capture (dashboard Record button → start_recording).
        ///     If the live preview is already up (the normal case — the clinician watched
        ///     the preview, then pressed Record) recording starts immediately. Only when
        ///     Record is pressed before warm-up finished do we wait for the camera.
        /// </summary>
        public void BeginRecording()
        {
            if (_recording || _exercise == null) return;
            if (_previewing)
                StartRecordingNow();
            else
                StartCoroutine(BeginRecordingCoroutine());
        }

        private IEnumerator BeginRecordingCoroutine()
        {
            if (_recording) yield break;

            // Only wait on the ZED when it's enabled — otherwise IsReady() is never true
            // and this stalls the full 20 s (see WarmupAndPreviewCoroutine).
            if (SessionContext.ZedEnabled)
            {
                var waited = 0f;
                const float timeout = 20f;
                while (!_sensorSystemController.IsReady() && waited < timeout)
                {
                    yield return new WaitForSecondsRealtime(0.25f);
                    waited += 0.25f;
                }
            }

            StartPreview(); // ensure the live loop is up if Record was pressed very early
            StartRecordingNow();
        }

        private void StartRecordingNow()
        {
            if (_recording || _exercise == null) return;

            // ZED .svo recording is best-effort: a camera that isn't ready must not
            // block FSR/skeleton capture (those still stream into the session file).
            try
            {
                _zedController.SetVideoRecordingLocation(VideoFileLocation);
                _zedController.StartRecording();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Exercise] ZED video recording could not start: " + e.Message);
            }

            // EEG (Unicorn) runs as a parallel high-rate stream when enabled. A failure
            // to connect must not block the motion/FSR capture.
            if (SessionContext.EegEnabled)
            {
                var port = PlayerPrefs.GetString("EEGComPort", "COM5");
                _eeg = new EegRecorder();
                if (_eeg.Start(port)) Debug.Log($"[Exercise] EEG acquisition started on {port}.");
                else { Debug.LogWarning($"[Exercise] EEG connect failed on {port}; continuing without EEG."); _eeg = null; }
            }

            // Single-insole plantar pressure already streams from preview (StartPreview);
            // Record just flips it to persist the full ~100 Hz stream to fsr.csv. If preview
            // never opened it (e.g. Record pressed before warm-up), open it now.
            if (_pressure == null) StartPressureSource();
            _pressure?.BeginCapture();
            if (_pressure != null) Debug.Log("[Exercise] Pressure capture armed (writing to fsr.csv).");

            _exercise.Start();
            _recording = true;
            _everRecorded = true;
            Network.TelerehabBroadcaster.SetRecording(true);
            Debug.Log("[Exercise] Recording started.");
        }

        public void StartBrake()
        {
            if (_exercise == null) return;
            _exercise.Pause();
            Network.TelerehabBroadcaster.SetRecording(true, paused: true);
            if (BreakDisplay != null) BreakDisplay.text = "On break since" + DateTime.Now.ToString("HH:mm");
            if (BreakToggleDisplay != null) BreakToggleDisplay.text = "Resume";

            // The preview loop keeps running (so the dashboard stays live); frame
            // persistence is gated on !IsPaused() in SensorUpdateCoroutine.
        }

        private void SetTargetEndDisplay(DateTime? endTime)
        {
            if (TargetEndDisplay != null && endTime.HasValue)
                TargetEndDisplay.text = "Exercise Until: " + endTime.Value.ToString("HH:mm");
        }

        public void StopBrake()
        {
            if (_exercise == null) return;
            _exercise.Resume();
            Network.TelerehabBroadcaster.SetRecording(true);
            if (BreakDisplay != null) BreakDisplay.text = "";
            if (BreakToggleDisplay != null) BreakToggleDisplay.text = "Pause";
            SetTargetEndDisplay(_exercise.GetTargetEnd());

            // Preview loop never stopped; resuming just re-enables persistence.
        }

        public void ToggleBrake()
        {
            if (_exercise == null) return;
            if (_exercise.IsPaused())
            {
                StopBrake();
                if (BreakToggleDisplay != null) BreakToggleDisplay.text = "Pause";
            }
            else
            {
                StartBrake();
                if (BreakToggleDisplay != null) BreakToggleDisplay.text = "Resume";
            }
        }
#nullable enable
        public DebugMenuController? DebugMenu;
        public GameObject? StartButton;
        public TextMeshProUGUI? ScoreDisplay;
        public TextMeshProUGUI? BreakDisplay;
        public TextMeshProUGUI? BreakToggleDisplay;
        public TextMeshProUGUI? TargetEndDisplay;
        private IExercise? _exercise;
#nullable disable
    }
}