using System;

namespace Assets.Scripts.Sensors.FSR
{
    [Serializable]
    public class FSRState
    {
        public int Heel;
        public int Middle_Inner;
        public int Middle_Outer;
        public int Toe;

        public FSRState()
        {
            Toe = 0;
            Middle_Inner = 0;
            Middle_Outer = 0;
            Heel = 0;
        }

        public FSRState(int toe, int middle_inner, int middle_outer, int heel)
        {
            Toe = toe;
            Middle_Inner = middle_inner;
            Middle_Outer = middle_outer;
            Heel = heel;
        }
    }
}