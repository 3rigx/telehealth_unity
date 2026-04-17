using System;
using System.Collections.Generic;
using System.Linq;
using SocketIOClient;
using SocketIOClient.Transport;
using UnityEngine;

namespace Assets.Scripts.Sensors.Connectors.WebSocketSensor
{
    /// <summary>
    ///     WebSocket sensor connector.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class WebSocketSensorConnector : IConnector<int>
    {
        private readonly SocketIOUnity socket;

        public WebSocketSensorConnector(Uri uri, string apiKey, List<SensorState<int>> sensors)
        {
            Sensors = sensors;
            socket = new SocketIOUnity(uri, new SocketIOOptions
            {
                RandomizationFactor = 0.5,
                ReconnectionDelay = 1000,
                ReconnectionDelayMax = 5000,
                ReconnectionAttempts = int.MaxValue,
                Path = "/socket.io",
                ConnectionTimeout = TimeSpan.FromSeconds(20),
                Reconnection = true,
                AutoUpgrade = true,
                EIO = EngineIO.V4,
                Transport = TransportProtocol.Polling,
                Query = new Dictionary<string, string>
                {
                    { "key", apiKey },
                    { "platform", "unity" }
                }
            });

            socket.OnUnityThread("sensor", HandleSensor);

            socket.OnConnected += (_, _) => { socket.Emit("sensoron"); };
        }

        public List<SensorState<int>> Sensors { get; }


        public async void Connect()
        {
            await socket.ConnectAsync();
            Debug.Log("sensor connected" + socket.Connected + " -> " + socket);
            ;
        }

        public async void Disconnect()
        {
            socket.Emit("sensoroff");
            await socket.DisconnectAsync();
        }

        public void Read()
        {
            // Do nothing -> cannot update on demand
        }


        private void HandleSensor(SocketIOResponse res)
        {
            var resString = res.ToString();
            var data = resString.Substring(2, resString.Length - 4).Split(',').Select(e => int.Parse(e)).ToArray();
            if (data.Length != Sensors.Count)
            {
                Debug.Log("Invalid sensor data");
                return;
            }

            var i = 0;
            foreach (var sensor in Sensors)
            {
                sensor.Update(data[i]);
                i++;
            }
        }


        ~WebSocketSensorConnector()
        {
            socket.Emit("sensoroff");
            socket.Disconnect();
        }
    }
}