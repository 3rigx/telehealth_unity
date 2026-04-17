using System;
using Assets.Scripts.Sensors.Zed;
using UnityEngine;

namespace Assets.Scripts.Sensors
{
    public class DerivedJointState
    {
        [NonSerialized] public int avgam = 10;
        public Vector3 AvGPosition;


        public DerivedJointState(DerivedJointState prev, SkeletonState s, int id)
        {
            AvGPosition = (s.JointPos[id] + AvGPosition * avgam) / avgam;
        }
    }
}