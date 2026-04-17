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
            Sensors.ForEach(s => { s.Update(Math.Max(0, Math.Min(255, s.GetValue() + Random.Range(-30, 30)))); });
        }
    }
}