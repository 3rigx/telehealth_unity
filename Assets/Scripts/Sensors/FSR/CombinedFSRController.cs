using Assets.Scripts.Sensors.Connectors;

namespace Assets.Scripts.Sensors.FSR
{
    public class CombinedFSRController : ISensorController<(FSRState, FSRState)>
    {
        private readonly IConnector<int> _connector;
        private FSRState left, right;

        public CombinedFSRController(IConnector<int> connector)
        {
            _connector = connector;
        }

        public void Read()
        {
            _connector.Read();
            left = new FSRState(_connector.Sensors[0].GetValue(), 0,0, _connector.Sensors[1].GetValue());
            right = new FSRState(_connector.Sensors[2].GetValue(), 0, 0, _connector.Sensors[3].GetValue());
        }

        public (FSRState, FSRState) GetState()
        {
            return (left, right);
        }
    }
}