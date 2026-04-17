//https://gist.github.com/danielbierwirth/0636650b005834204cb19ef5ae6ccedb

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Sensors.Connectors.NetworkSensor
{
    /// <summary>
    ///     TCP client for use with the TCP sensor connector.
    /// </summary>
    internal class TCPConnector : IConnector<int>
    {
        private readonly byte[] buffer = new byte[1024];
        private readonly string host = "";
        private readonly int port = 45845;
        private readonly TcpClient socket = new();


        public TCPConnector(List<SensorState<int>> sensors, string host, int port)
        {
            Sensors = sensors;
            this.host = host;
            this.port = port;
        }

        public List<SensorState<int>> Sensors { get; }

        public void Read()
        {
            using (var stream = socket.GetStream())
            {
                int length;
                while ((length = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    var incomingData = new byte[length];
                    Array.Copy(buffer, 0, incomingData, 0, length);
                    // Convert byte array to string message.
                    var serverMessage = Encoding.ASCII.GetString(incomingData);
                    foreach (var msg in serverMessage.Split("\n"))
                    {
                        var rawBytes = msg.Split(',');
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
        }


        public void Connect()
        {
            try
            {
                socket.Connect(host, port);
            }
            catch (SocketException e)
            {
                Debug.Log("Socket error: " + e);
            }
        }

        public void Disconnect()
        {
            socket.Close();
        }
    }
}