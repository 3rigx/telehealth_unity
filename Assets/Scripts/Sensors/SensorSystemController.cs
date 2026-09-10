//#define COMBINED_FSR

using System;
using System.Collections.Generic;
using System.IO.Ports;
using Assets.Scripts.Sensors.Connectors;
using Assets.Scripts.Sensors.Connectors.MockSensor;
using Assets.Scripts.Sensors.Connectors.USBSensor;
using Assets.Scripts.Sensors.Connectors.WebSocketSensor;
using Assets.Scripts.Sensors.FSR;
using Assets.Scripts.Sensors.Zed;
using Assets.Scripts.Util;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Sensors
{
    /// <summary>
    ///     Sensor controller.
    /// </summary>
    [DisallowMultipleComponent]
    public class SensorSystemController : MonoBehaviour, ISensorController<SensorSystemState>
    {
        public PopupController errorController;
        public IConnector<int> FsrConnector;
        private CombinedFSRController _fsrController;

        private CustomZedController _zedController;

        private DateTime _lastStateTimestamp;

        public void Read()
        {
            // No-op when FSR is disabled / failed to connect.
            _fsrController?.Read();
        }

        public SensorSystemState GetState()
        {
            var now = DateTime.UtcNow;

            // Empty (zero) FSR when the insoles aren't in use, so a ZED-only
            // session still produces valid state without the FSR.
            FSRState leftFoot = new(), rightFoot = new();
            if (_fsrController != null)
            {
                var combinedFsrState = _fsrController.GetState();
                leftFoot = combinedFsrState.Item1;
                rightFoot = combinedFsrState.Item2;
            }

            SensorSystemState next = new(
                (int)(now - _lastStateTimestamp).TotalMilliseconds,
                leftFoot,
                rightFoot,
                _zedController.GetState()
            );

            _lastStateTimestamp = now;

            var patientHandler = _zedController.GetPatientHandler();
            if (patientHandler) patientHandler.SetFeetPressure(next);

            return next;
        }

        private IConnector<int> FsrConnect()
        {
            var sensors = new List<SensorState<int>>
            {
                new("Heel", 0),
                new("Toe", 0),
                new("Heel", 0),
                new("Toe", 0)
            };

            var fsrConntype = PlayerPrefs.GetString("FSRConntype", "");

            switch (fsrConntype)
            {
                case "Mock":
                    return new MockSensorConnector(sensors);

                case "USB":
                    var usbPort = PlayerPrefs.GetString("FSRUsbPort", "");
                    if (usbPort == "")
                        throw new IllegalSettingsException("FSRUsbPort", "You need to select an FSR USB Port");

                    return new USBSensorConnector(
                        sensors,
                        usbPort,
                        2000000,
                        Parity.None,
                        8,
                        StopBits.One,
                        4
                    );

                case "WebSocket":
                    var uriString = PlayerPrefs.GetString("FSRUri", "");
                    if (uriString == "")
                        throw new IllegalSettingsException("FSRUri", "You need to input the FSR connection url");

                    var apiKey = PlayerPrefs.GetString("FSRApiKey", "");
                    if (apiKey == "")
                        throw new IllegalSettingsException("FSRApiKey", "You need to input an FSR API Key");

                    return new WebSocketSensorConnector(new Uri(uriString), apiKey, sensors);

                case "TCP":
                    throw new IllegalSettingsException("FSRConntype", "TCP server is not implemented");

                default:
                    throw new IllegalSettingsException("FSRConntype", "You need to select an FSR connection type!");
            }
        }

        private void BackToMenu()
        {
            SceneManager.LoadScene("Menu");
        }

        public void Start()
        {
            Debug.Log("Initializing Sensor System Controller...");
            var zedControllers = FindObjectsByType<CustomZedController>();
            if (zedControllers.Length == 0)
            {
                Debug.LogError("No CustomZedController found in the scene.");
                errorController.SetText("No ZED controller was found in the scene.");
                errorController.Show();
                Invoke(nameof(BackToMenu), 5);
                return;
            }

            _zedController = zedControllers[0];
            _lastStateTimestamp = DateTime.UtcNow;

            // FSR is optional. Only connect it when the session enables it, and
            // never let an FSR problem (e.g. the USB port isn't present) take down
            // the rest of the sensor system — the ZED must still run. Previously an
            // unhandled IOException from Connect() aborted Start(), leaving
            // _fsrController null and NPE'ing GetState() every frame, which killed
            // the ZED capture too.
            if (!SessionContext.FsrEnabled)
            {
                Debug.Log("[Sensors] FSR disabled for this session — skipping FSR connect.");
                _fsrController = null;
                return;
            }

            // Single-insole path: the PressureRecorder (owned by ExerciseController) opens
            // the Arduino serial port on its own background thread to capture the full
            // 100 Hz stream. Connecting the legacy combined controller here would fight for
            // the same COM port, so this controller stays out of FSR entirely and reports
            // empty feet; ExerciseController injects the live pressure into each frame.
            if (PressureConfig.IsSingleFoot)
            {
                Debug.Log("[Sensors] Single-insole pressure handled by PressureRecorder — skipping legacy FSR connect.");
                _fsrController = null;
                return;
            }

            try
            {
                FsrConnector = FsrConnect();
                Debug.Log("Connecting to FSR");
                FsrConnector.Connect();
                _fsrController = new CombinedFSRController(FsrConnector);
                Debug.Log("FSR connected.");
            }
            catch (IllegalSettingsException ex)
            {
                // Misconfiguration (e.g. USB selected but no port) — surface it.
                Debug.LogError(ex.message);
                errorController.SetText(ex.message);
                errorController.Show();
                Invoke(nameof(BackToMenu), 5);
            }
            catch (Exception ex)
            {
                // Hardware/port failure — carry on without FSR instead of aborting.
                Debug.LogWarning($"[Sensors] FSR connect failed: {ex.Message} — continuing without FSR.");
                FsrConnector = null;
                _fsrController = null;
            }
        }

        public bool IsReady()
        {
            return _zedController != null && _zedController.IsReady();
        }
    }
}