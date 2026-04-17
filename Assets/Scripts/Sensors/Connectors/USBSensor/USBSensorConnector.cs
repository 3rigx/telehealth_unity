#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using System.IO.Ports;
using UnityEngine;

namespace Assets.Scripts.Sensors.Connectors.USBSensor
{
    /// <summary>
    ///     USB sensor connector.
    /// </summary>
    public class USBSensorConnector : IConnector<int>
    {
        private readonly int baudRate;
        private readonly int dataBits;
        private readonly Parity parity;
        private readonly string portName;
        private readonly StopBits stopBits;
        private SerialPort serialPort;

        public USBSensorConnector(List<SensorState<int>> sensors, string portName, int baudRate, Parity parity,
            int dataBits, StopBits stopBits, int sensorCount)
        {
            this.portName = portName;
            this.baudRate = baudRate;
            this.parity = parity;
            this.dataBits = dataBits;
            this.stopBits = stopBits;
            Sensors = sensors;
        }

        public List<SensorState<int>> Sensors { get; }

        /// <summary>
        ///     Connect to the USB port
        /// </summary>
        public void Connect()
        {
            serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits);
            serialPort.Open();
        }

        /// <summary>
        ///     Disconnect USB connection.
        /// </summary>
        public void Disconnect()
        {
            serialPort.Close();
        }


        /// <summary>
        ///     Read USB connection
        /// </summary>
        /// <returns>Line read from USB</returns>
        public void Read()
        {
            serialPort.WriteLine("r");
            var data = serialPort.ReadLine();
            var rawBytes = data.Split(',');
            if (rawBytes.Length != Sensors.Count)
            {
                Debug.LogError("Message size mismatch");
                return;
            }

            for (var i = 0; i < rawBytes.Length; i++)
                if (rawBytes[i].Length > 1)
                    Debug.LogError("Unexpectedly long sensor read received!");
                else if (rawBytes.Length == 0)
                    Debug.LogError("Null byte received");
                else
                    Sensors[i].Update(Convert.ToInt32(rawBytes[0]));
        }
    }
}
#endif