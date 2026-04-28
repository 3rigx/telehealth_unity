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
            try
            {
                // Just read the latest line from Arduino (no command needed)
                var data = serialPort.ReadLine();
                var rawBytes = data.Split(',');
                
                // Skip header lines (non-numeric data like "A0,A1,A2,A3")
                if (!char.IsDigit(rawBytes[0][0]))
                {
                    return;
                }

                if (rawBytes.Length != Sensors.Count)
                {
                    Debug.LogWarning($"Message size mismatch: expected {Sensors.Count}, got {rawBytes.Length}");
                    return;
                }

                for (var i = 0; i < rawBytes.Length; i++)
                {
                    var value = rawBytes[i].Trim();
                    if (string.IsNullOrEmpty(value))
                    {
                        Debug.LogWarning($"Empty value received at index {i}");
                        continue;
                    }
                    if (value.Length > 4) // Max value for 10-bit ADC is 1023 (4 chars)
                    {
                        Debug.LogWarning($"Unexpectedly long sensor read received: {value}");
                        continue;
                    }
                    Sensors[i].Update(Convert.ToInt32(value));
                }
            }
            catch (TimeoutException)
            {
                // Timeout is expected if no data available - just skip this read
            }
            catch (Exception ex)
            {
                Debug.LogError($"USB sensor read error: {ex.Message}");
            }
        }
    }
}
#endif