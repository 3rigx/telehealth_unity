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
            _fsrController.Read();
        }

        public SensorSystemState GetState()
        {
            var combinedFsrState = _fsrController.GetState();

            SensorSystemState next = new(
                (int)(DateTime.UtcNow - _lastStateTimestamp).TotalMilliseconds,
                combinedFsrState.Item1,
                combinedFsrState.Item2,
                _zedController.GetState()
            );

            _lastStateTimestamp = next.timestamp;

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
                    return new USBSensorConnector(sensors, usbPort, 115200, Parity.None, 8, StopBits.One, 4);
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
            _zedController = FindObjectsByType<CustomZedController>()[0];
            try
            {
                FsrConnector = FsrConnect();
            }
            catch (IllegalSettingsException ex)
            {
                Debug.LogError(ex.message);
                errorController.SetText(ex.message);
                errorController.Show();
                Invoke(nameof(BackToMenu), 5);
                return;
            }

            Debug.Log("Connecting to FSR");
            FsrConnector.Connect();

            _fsrController = new CombinedFSRController(FsrConnector);
        }

        public bool IsReady()
        {
            return _zedController.IsReady();
        }
    }
}