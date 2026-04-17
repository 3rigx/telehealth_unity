using System;
using System.Collections.Generic;
using Assets.Scripts.Sensors.Connectors;
using Assets.Scripts.Sensors.FSR;

namespace Assets.Scripts.Sensors
{
    /// <summary>
    ///     FSR
    /// </summary>
    [Serializable]
    public class FootFsrController : ISensorController<FSRState>
    {
        private IConnector<int> _connector;
        private List<SensorState<int>> _sensors;
        private FSRState state;

        public FootFsrController(IConnector<int> connector)
        {
            _sensors = new List<SensorState<int>>
            {
                new("FSR Toe"),
                new("FSR MiddleInner"),
                new("FSR MiddleOuter"),
                new("FSR Heel")
            };
            _connector = connector;
            _connector.Connect();
        }

        public void Read()
        {
            _connector.Read();
            state = new FSRState(_sensors[0].GetValue(), _sensors[1].GetValue(), _sensors[2].GetValue(), _sensors[3].GetValue());
        }

        public FSRState GetState()
        {
            return state;
        }

        public override string ToString()
        {
            return $"FSR: {string.Join(",", state)}";
        }
    }
}