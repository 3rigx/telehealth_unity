using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Sensors;
using Assets.Scripts.Sensors.Zed;
using UnityEngine;

namespace Assets.Scripts.UI.Chart.MyChart
{
    public class SkeletonChartController : MonoBehaviour
    {
        public GameObject linePrefab;
        private LineSelector selector;
        private int _cursor = 0;
        private int windowSize = 10;
        private Dictionary<string, int> jointNameToJointIndex = new ();
        private Dictionary<int, UIGraphRenderer[]> joints = new ();
        private string[] jointNames = new string[0];
        private BodyFormat _bodyFormat;

        public BodyFormat bodyFormat
        {
            get { return _bodyFormat; }
        }

        private UIGraphRenderer[] make3LineRenderers()
        {
            var x = Instantiate(linePrefab).GetComponent<UIGraphRenderer>();
            var y = Instantiate(linePrefab).GetComponent<UIGraphRenderer>();
            var z = Instantiate(linePrefab).GetComponent<UIGraphRenderer>();
            x.color = Color.red;
            y.color = Color.green;
            z.color = Color.blue;

            return new[] { x, y, z };
        }

        public void setBodyFormat(BodyFormat value)
        {
            _bodyFormat = value;
            jointNameToJointIndex = _bodyFormat.getJointNamesToIds();
            jointNames = jointNameToJointIndex.Keys.ToArray();
            selector.Clear();
            foreach (var (k, v) in jointNameToJointIndex)
            {
                UIGraphRenderer[] positionGroup = make3LineRenderers();
                selector.addGroup(k + "Position", positionGroup);

                UIGraphRenderer[] rotationGroup = make3LineRenderers();
                selector.addGroup(k + "Rotation", rotationGroup);

                UIGraphRenderer[] avgPosGroup = make3LineRenderers();
                selector.addGroup(k + "AvgPos", avgPosGroup);

                UIGraphRenderer[] avgSpeedGroup = make3LineRenderers();
                selector.addGroup(k + "AvgVel", avgSpeedGroup);

                UIGraphRenderer[] avgAccGroup = make3LineRenderers();
                selector.addGroup(k + "AvgAcc", avgAccGroup);

                UIGraphRenderer[] avgJerkGroup = make3LineRenderers();
                selector.addGroup(k + "AvgJerk", avgJerkGroup);

                UIGraphRenderer[] avgEulerGroup = make3LineRenderers();
                selector.addGroup(k + "AvgEuler", avgEulerGroup);

                UIGraphRenderer[] avgRotGroup = make3LineRenderers();
                selector.addGroup(k + "AvgRot", avgRotGroup);

                UIGraphRenderer[] avgAngularSpeedGroup = make3LineRenderers();
                selector.addGroup(k + "AvgAngularSpeed", avgAngularSpeedGroup);

                UIGraphRenderer[] avgAngularAccGroup = make3LineRenderers();
                selector.addGroup(k + "AvgAngularAcc", avgAngularAccGroup);

                UIGraphRenderer[] avgAngularJerkGroup = make3LineRenderers();
                selector.addGroup(k + "AvgAngularJerk", avgAngularJerkGroup);

                joints.Add(v,
                    positionGroup
                        .Concat(rotationGroup)
                        .Concat(avgPosGroup)
                        .Concat(avgSpeedGroup)
                        .Concat(avgAccGroup)
                        .Concat(avgJerkGroup)
                        .Concat(avgEulerGroup)
                        .Concat(avgRotGroup)
                        .Concat(avgAngularSpeedGroup)
                        .Concat(avgAngularAccGroup)
                        .Concat(avgAngularJerkGroup)
                        .ToArray());
            }
        }

        private void addDerivedState(DerivedState state)
        {
            for (int i = 0; i < state.avgPos.Length; i++)
            {
                joints[i][9].AddValue(state.avgPos[i].x);
                joints[i][10].AddValue(state.avgPos[i].y);
                joints[i][11].AddValue(state.avgPos[i].z);
            }

            for (int i = 0; i < state.avgSpeed.Length; i++)
            {
                joints[i][12].AddValue(state.avgSpeed[i].x);
                joints[i][13].AddValue(state.avgSpeed[i].y);
                joints[i][14].AddValue(state.avgSpeed[i].z);
            }

            for (int i = 0; i < state.avgAcc.Length; i++)
            {
                joints[i][15].AddValue(state.avgAcc[i].x);
                joints[i][16].AddValue(state.avgAcc[i].y);
                joints[i][17].AddValue(state.avgAcc[i].z);
            }

            for (int i = 0; i < state.avgJerk.Length; i++)
            {
                joints[i][18].AddValue(state.avgJerk[i].x);
                joints[i][19].AddValue(state.avgJerk[i].y);
                joints[i][20].AddValue(state.avgJerk[i].z);
            }

            for (int i = 0; i < state.avgEulerRot.Length; i++)
            {
                joints[i][21].AddValue(state.avgEulerRot[i].x);
                joints[i][22].AddValue(state.avgEulerRot[i].y);
                joints[i][23].AddValue(state.avgEulerRot[i].z);
            }

            for (int i = 0; i < state.avgRot.Length; i++)
            {
                joints[i][24].AddValue(state.avgRot[i].x);
                joints[i][25].AddValue(state.avgRot[i].y);
                joints[i][26].AddValue(state.avgRot[i].z);
            }

            for (int i = 0; i < state.avgEulerAngSpeed.Length; i++)
            {
                joints[i][27].AddValue(state.avgEulerAngSpeed[i].x);
                joints[i][28].AddValue(state.avgEulerAngSpeed[i].y);
                joints[i][29].AddValue(state.avgEulerAngSpeed[i].z);
            }

            for (int i = 0; i < state.avgEulerAngAcc.Length; i++)
            {
                joints[i][30].AddValue(state.avgEulerAngAcc[i].x);
                joints[i][31].AddValue(state.avgEulerAngAcc[i].y);
                joints[i][32].AddValue(state.avgEulerAngAcc[i].z);
            }

            for (int i = 0; i < state.avgEulerAngJerk.Length; i++)
            {
                joints[i][33].AddValue(state.avgEulerAngJerk[i].x);
                joints[i][34].AddValue(state.avgEulerAngJerk[i].y);
                joints[i][35].AddValue(state.avgEulerAngJerk[i].z);
            }
        }

        private void addSkeletonState(SkeletonState state)
        {
            for (int i = 0; i < state.JointPos.Length; i++)
            {
                joints[i][0].AddValue(state.JointPos[i].x);
                joints[i][1].AddValue(state.JointPos[i].y);
                joints[i][2].AddValue(state.JointPos[i].z);

                joints[i][3].AddValue(state.JointRot[i].x);
                joints[i][4].AddValue(state.JointRot[i].y);
                joints[i][5].AddValue(state.JointRot[i].z);
            }
        }

        public void addState(SensorSystemState state)
        {
            if (state.skeleton == null || state.derivedState == null) return;
            addSkeletonState(state.skeleton);
            addDerivedState(state.derivedState);
        }

        public int cursor
        {
            get { return _cursor; }
            set
            {
                _cursor = value;
                foreach (UIGraphRenderer[] joint in joints.Values)
                {
                    foreach (UIGraphRenderer line in joint)
                    {
                        line.cursor = value;
                    }
                }
            }
        }

        void Start()
        {
            setBodyFormat(new(BODY_FORMAT.BODY_38));
            selector = GetComponent<LineSelector>();
        }
    }
}
