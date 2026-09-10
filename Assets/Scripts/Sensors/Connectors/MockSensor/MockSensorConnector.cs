using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Sensors.Connectors.MockSensor
{
    /// <summary>
    ///     Mock sensor connector for testing purposes.
    /// </summary>
    internal class MockSensorConnector : IConnector<int>
    {
        public MockSensorConnector(List<SensorState<int>> sensors)
        {
            Sensors = sensors;
        }

        public List<SensorState<int>> Sensors { get; }

        public void Connect()
        {
        }

        public void Disconnect()
        {
        }

        public void Read()
        {
            // Gait-like heel→toe roll with the two feet in anti-phase, plus noise.
            // (The previous random walk started at 0 with a negatively-biased step,
            // so it stayed pinned at 0 and looked like a dead sensor.)
            var t = UnityEngine.Time.realtimeSinceStartup * (2.0 * Math.PI) / 1.2; // ~1.2 s stride
            for (var i = 0; i < Sensors.Count; i++)
            {
                var foot = i < Sensors.Count / 2 ? 0.0 : Math.PI;   // left/right alternate
                var zone = i % 2 == 0 ? 0.0 : 0.35 * Math.PI;       // heel leads, toe follows
                var wave = Math.Sin(t + foot - zone);
                var value = (wave > 0 ? wave : 0) * 215.0 + Random.Range(0f, 25f);
                Sensors[i].Update((int)Math.Min(255.0, value));
            }
        }
    }
}