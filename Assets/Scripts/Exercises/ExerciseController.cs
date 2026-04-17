using System;
using System.Collections;
using Assets.Scripts.Exercises.ExerciseTypes;
using Assets.Scripts.SaveSystem;
using Assets.Scripts.Sensors;
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


                _exercise?.Update(newstate);

                if (syncCalc)
                {
                    _statisticsController.UpdateStatistics(newstate);
                }


                yield return new WaitForSecondsRealtime(_updateInterval);
            }
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
            StopCoroutine(SensorUpdateCoroutine());
            StopCoroutine(ReadSensorCoroutine());
            _zedController.StopRecording();
            if (_exercise != null)
            {
                _exercise.Stop();
                SaveManager.Save(DataFileLocation, _exercise);
            }

            if (ExerciseProvider.isRemote)
                StartCoroutine(RemoteManager.SaveRemoteCoroutine(DataFileLocation, VideoFileLocation));
            Debug.Log("Exercise closed");
            SceneManager.LoadScene("Menu");
        }

        public void Discard()
        {
            StopCoroutine(SensorUpdateCoroutine());
            StopCoroutine(ReadSensorCoroutine());
            _zedController.StopRecording();
            SceneManager.LoadScene("Menu");

        }

        private void Start()
        {
            _sensorSystemController = FindObjectsByType<SensorSystemController>()[0];
            _zedController = FindObjectsByType<CustomZedController>()[0];
            if (AutoStart || ExerciseProvider.exercise != null) DelayedStart();
        }

        public void DelayedStart()
        {
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
            DataFileLocation = ExerciseProvider.dataFileLocation ?? "";
            VideoFileLocation = ExerciseProvider.videoFileLocation ?? "";
            if (DataFileLocation == "")
                DataFileLocation =
                    StandaloneFileBrowser.StandaloneFileBrowser.SaveFilePanel("Save location", "", "save", "json");
            if (VideoFileLocation == "")
                VideoFileLocation =
                    StandaloneFileBrowser.StandaloneFileBrowser.SaveFilePanel("Save location", "", "save", "svo");

            _zedController.SetVideoRecordingLocation(VideoFileLocation);
            _zedController.StartRecording();

            _statisticsController = new StatisticsController(_exercise.GetSensorStates());
            if (_exercise is Exercise exercise1)
            {
                _readInterval = exercise1.GetMetadata().measurementInterval;
                _updateInterval = exercise1.GetMetadata().measurementInterval * 2;
            }

            SetTargetEndDisplay(_exercise.GetTargetEnd());

            StartCoroutine(ExerciseControlCoroutine());
        }

        private IEnumerator ExerciseControlCoroutine()
        {
            if (_exercise == null) yield break;
            Debug.Log("Starting exercise" + _exercise.GetType() + ": ");
            if (_sensorSystemController == null) yield break;
            if (!_sensorSystemController.IsReady())
            {
                Debug.Log("SensorSystemController not ready");
                yield return new WaitForSecondsRealtime(1);
            }

            Debug.Log("SensorSystemController ready");
            _exercise.Start();
            StartCoroutine(SensorUpdateCoroutine());
            StartCoroutine(ReadSensorCoroutine());
        }

        public void StartBrake()
        {
            if (_exercise == null) return;
            _exercise.Pause();
            if (BreakDisplay != null) BreakDisplay.text = "On break since" + DateTime.Now.ToString("HH:mm");
            if (BreakToggleDisplay != null) BreakToggleDisplay.text = "Resume";

            StopCoroutine(SensorUpdateCoroutine());
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
            if (BreakDisplay != null) BreakDisplay.text = "";
            if (BreakToggleDisplay != null) BreakToggleDisplay.text = "Pause";
            SetTargetEndDisplay(_exercise.GetTargetEnd());

            StartCoroutine(SensorUpdateCoroutine());
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