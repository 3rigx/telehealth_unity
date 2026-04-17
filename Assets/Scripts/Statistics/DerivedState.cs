using Assets.Scripts.Sensors.Zed;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Sensors
{
    [Serializable]
    public class DerivedState
    {
        [NonSerialized] public static List<int> watchedJoints = new List<int>() {
            BodyFormat.jointTypeHead,
            BodyFormat.jointTypeNeck,
            BodyFormat.jointTypeClavicleRight,
            BodyFormat.jointTypeShoulderRight,
            BodyFormat.jointTypeElbowRight,
            BodyFormat.jointTypeWristRight,
            BodyFormat.jointTypeClavicleLeft,
            BodyFormat.jointTypeShoulderLeft,
            BodyFormat.jointTypeElbowLeft,
            BodyFormat.jointTypeWristLeft,
            BodyFormat.jointTypeHipRight,
            BodyFormat.jointTypeKneeRight,
            BodyFormat.jointTypeAnkleRight,
            BodyFormat.jointTypeFootRight,
            BodyFormat.jointTypeHeelRight,
            BodyFormat.jointTypeHipLeft,
            BodyFormat.jointTypeKneeLeft,
            BodyFormat.jointTypeAnkleLeft,
            BodyFormat.jointTypeFootLeft,
            BodyFormat.jointTypeHeelLeft,
            BodyFormat.jointTypeEyesRight,
            BodyFormat.jointTypeEyesLeft,
            BodyFormat.jointTypeEarRight,
            BodyFormat.jointTypeEarLeft,
            BodyFormat.jointTypeSpineBase,
            BodyFormat.jointTypeSpineNaval,
            BodyFormat.jointTypeSpineChest,
            BodyFormat.jointTypeNose
        };
        
        [SerializeField] public Vector3[] avgAcc;
        [SerializeField] public Vector3[] avgEulerAngAcc;
        [SerializeField] public Vector3[] avgEulerAngJerk;

        [SerializeField] public Vector3[] avgEulerAngSpeed;
        [SerializeField] public Vector3[] avgEulerRot;
        [SerializeField] public Vector3[] avgJerk;


        [SerializeField] public Vector3[] avgPos;
        [SerializeField] public Quaternion[] avgRot;

        [SerializeField] public Vector3[] avgSpeed;

        public DerivedState(LinkedList<SensorSystemState> states)
        {

            avgPos = new Vector3[watchedJoints.Count];
            avgRot = new Quaternion[watchedJoints.Count];
            avgEulerRot = new Vector3[watchedJoints.Count];


            avgSpeed = new Vector3[watchedJoints.Count];
            avgAcc = new Vector3[watchedJoints.Count];
            avgJerk = new Vector3[watchedJoints.Count];

            avgEulerAngSpeed = new Vector3[watchedJoints.Count];
            avgEulerAngAcc = new Vector3[watchedJoints.Count];
            avgEulerAngJerk = new Vector3[watchedJoints.Count];

          

            var statesWithSkeleton = new LinkedList<SensorSystemState>(states.Where(s  => s.skeleton != null && s.skeleton.JointPos.Length == s.skeleton.Format.jointCount));

            for (var i = 0; i < watchedJoints.Count; i++)
            {
                // avgRot[i] = AverageQuaternion(statesWithSkeleton.Select(s => s.Skeleton.JointRot[i]).ToList());
            }

            var statePtr = statesWithSkeleton.First;
            var si = 0;
            while (statePtr != null)
            {
                var state = statePtr.Value;

               

                    for (var i = 0; i < watchedJoints.Count; i++)
                    {
                        avgPos[i] += state.skeleton.JointPos[watchedJoints[i]];
                        
                        avgRot[i] = Quaternion.Slerp(avgRot[i], state.skeleton.JointRot[watchedJoints[i]],
                            1 / (float)(si + 1));
                        
                        avgEulerRot[i] += state.skeleton.JointRot[watchedJoints[i]].eulerAngles;
                        
                        if(statePtr.Previous == null) continue;
                        
                        
                        if (si > 0)
                        {
                            avgSpeed[i] +=
                                (state.skeleton.JointPos[watchedJoints[i]] -
                                 statePtr.Previous.Value.skeleton.JointPos[watchedJoints[i]]) / state.time;
                            avgEulerAngSpeed[i] +=
                                (state.skeleton.JointRot[watchedJoints[i]].eulerAngles - statePtr.Previous.Value
                                    .skeleton.JointRot[watchedJoints[i]].eulerAngles) / state.time;
                        }

                        if (si > 1)
                        {
                            avgAcc[i] += (avgSpeed[i] - statePtr.Previous.Value.derivedState.avgSpeed[i]) / state.time;
                            avgEulerAngAcc[i] += (avgEulerAngSpeed[i] - statePtr.Previous.Value.derivedState.avgEulerAngSpeed[i]) / state.time;
                        }

                        if (si > 2)
                        {
                            avgJerk[i] += (avgAcc[i] - statePtr.Previous.Value.derivedState.avgAcc[i]) / state.time;
                            avgEulerAngJerk[i] += (avgEulerAngAcc[i] - statePtr.Previous.Value.derivedState.avgEulerAngAcc[i]) / state.time;
                        }
                    
                }
                ++si;
                statePtr = statePtr.Next;
            }

            var duration = (int) (states.Last.Value.timestamp.DateTime - states.First.Value.timestamp.DateTime).TotalMilliseconds;

            var skeletonStateCount = statesWithSkeleton.Count;

            for (var i = 0; i < avgPos.Length; i++)
            {
                avgPos[i] /= skeletonStateCount;
                avgEulerRot[i] /= skeletonStateCount;

                avgSpeed[i] /= skeletonStateCount - 1;
                avgEulerAngSpeed[i] /= skeletonStateCount - 1;

                avgAcc[i] /= skeletonStateCount - 2;
                avgEulerAngAcc[i] /= skeletonStateCount - 2;

                avgJerk[i] /= skeletonStateCount - 3;
                avgEulerAngJerk[i] /= skeletonStateCount - 3;

            }
        }


        public void WatchJoint(int jointID)
        {
            watchedJoints.Add(jointID);
        }

        public static Quaternion AverageQuaternion(List<Quaternion> quaternions)
        {
            var acc = Quaternion.identity;
            foreach (var q in quaternions)
            {
                acc.w += q.w;
                acc.x += q.x;
                acc.y += q.y;
                acc.z += q.z;
            }

            var Det = 1.0f / quaternions.Count;
            return new Quaternion(acc.w * Det, acc.x * Det, acc.y * Det, acc.z * Det).normalized;
        }
    }
}