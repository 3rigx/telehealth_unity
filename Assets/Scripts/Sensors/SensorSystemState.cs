using System;
using Assets.Scripts.Sensors.FSR;
using Assets.Scripts.Sensors.Zed;
using Assets.Scripts.Util;
using UnityEngine;
using UnityEngine.Serialization;

#pragma warning disable S3010
#pragma warning disable S1104


namespace Assets.Scripts.Sensors
{
    /// <summary>
    ///     Sensor state.
    /// </summary>
    [Serializable]
    public class SensorSystemState
    {
        private static int _nextId;



#nullable enable
        [FormerlySerializedAs("Timestamp")] [SerializeField] public SerializableDateTime timestamp;
        [FormerlySerializedAs("Id")] [SerializeField] public int id;
        [FormerlySerializedAs("Time")] [SerializeField] public int time; // State time in milliseconds
        [FormerlySerializedAs("LeftFoot")] [SerializeField] public FSRState leftFoot;
        [FormerlySerializedAs("RightFoot")] [SerializeField] public FSRState rightFoot;
        [FormerlySerializedAs("Skeleton")] [SerializeField] public SkeletonState? skeleton;
        [FormerlySerializedAs("DerivedState")] [SerializeField] public DerivedState? derivedState;
#nullable disable

        public SensorSystemState(int stateTime, FSRState leftFoot, FSRState rightFoot,
            SkeletonState skeleton)
        {
            id = _nextId++;
            time = stateTime;
            timestamp = DateTime.UtcNow;
            this.leftFoot = leftFoot;
            this.rightFoot = rightFoot;
            this.skeleton = skeleton;
        }


        public override string ToString()
        {
            return
                $"ID: {id}, Timestamp: {timestamp}, LeftFoot: {leftFoot}, RightFoot: {rightFoot}, Skeleton: {skeleton}";
        }
    }
}