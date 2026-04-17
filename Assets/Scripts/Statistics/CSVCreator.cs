using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Scripts.Exercises;
using Assets.Scripts.Sensors;
using Assets.Scripts.Sensors.FSR;
using Assets.Scripts.Sensors.Zed;
using UnityEngine;

namespace Assets.Scripts.Statistics
{
    public class CSVCreator
    {

        public string Vector3Header(string prefix)
        {
            return $"{prefix}X,{prefix}Y,{prefix}Z";
        }

        public string Vector3Line(Vector3 v)
        {
            Vector3 vNorm = v.normalized;
            int x = (int) (vNorm.x * 1000);
            int y = (int) (vNorm.y * 1000);
            int z = (int) (vNorm.z * 1000);
            return $"{x},{y},{z}";
        }

        public string Vector3ArrayHeader(string prefix, int length)
        {
            StringBuilder builder = new();
            for (int i = 0; i < length; i++)
            {
                builder.Append(Vector3Header($"{prefix}{i}"));
                builder.Append(",");
            }

            return builder.ToString();
        }

        public string Vector3ArrayLine(Vector3[] v)
        {
            StringBuilder builder = new();
            for (int i = 0; i < v.Length; i++)
            {
                builder.Append(Vector3Line(v[i]));
                builder.Append(",");
            }

            return builder.ToString();
        }

        public string QuaternionHeader(string prefix)
        {
            return $"{prefix}X,{prefix}Y,{prefix}Z,{prefix}W";
        }

        public string QuaternionLine(Quaternion q)
        {
            Quaternion qNorm = q.normalized;
            int x = (int) (qNorm.x * 1000);
            int y = (int) (qNorm.y * 1000);
            int z = (int) (qNorm.z * 1000);
            int w = (int) (qNorm.w * 1000);
            return $"{x},{y},{z},{w}";
        }

        public string QuaternionArrayHeader(string prefix, int length)
        {
            StringBuilder builder = new();
            for (int i = 0; i < length; i++)
            {
                builder.Append(QuaternionHeader($"{prefix}{i}"));
                builder.Append(",");
            }

            return builder.ToString();
        }

        public string QuaternionArrayLine(Quaternion[] q)
        {
            StringBuilder builder = new();
            for (int i = 0; i < q.Length; i++)
            {
                builder.Append(QuaternionLine(q[i]));
                builder.Append(",");
            }

            return builder.ToString();
        }


        public string DerivedStateHeader()
        {
            var jointCount = DerivedState.watchedJoints.Count;
            StringBuilder builder = new();
            builder.Append(Vector3ArrayHeader("avgPos", jointCount));
            builder.Append(",");
            builder.Append(Vector3ArrayHeader("avgSpeed", jointCount));
            builder.Append(",");
            builder.Append(Vector3ArrayHeader("avgAcc", jointCount));
            builder.Append(",");
            builder.Append(Vector3ArrayHeader("avgJerk", jointCount));
            builder.Append(",");
            builder.Append(QuaternionArrayHeader("avgRot", jointCount));
            builder.Append(",");
            builder.Append(Vector3ArrayHeader("avgEulerRot", jointCount));
            builder.Append(",");
            builder.Append(Vector3ArrayHeader("avgEulerAngSpeed", jointCount));
            builder.Append(",");
            builder.Append(Vector3ArrayHeader("avgEulerAngAcc", jointCount));
            builder.Append(",");
            builder.Append(Vector3ArrayHeader("avgEulerAngJerk", jointCount));
            return builder.ToString();
        }
        public string DerivedStateLine(DerivedState state)
        {
            StringBuilder builder = new();
            builder.Append(Vector3ArrayLine(state.avgPos));
            builder.Append(",");
            builder.Append(Vector3ArrayLine(state.avgSpeed));
            builder.Append(",");
            builder.Append(Vector3ArrayLine(state.avgAcc));
            builder.Append(",");
            builder.Append(Vector3ArrayLine(state.avgJerk));
            builder.Append(",");
            builder.Append(QuaternionArrayLine(state.avgRot));
            builder.Append(",");
            builder.Append(Vector3ArrayLine(state.avgEulerRot));
            builder.Append(",");
            builder.Append(Vector3ArrayLine(state.avgEulerAngSpeed));
            builder.Append(",");
            builder.Append(Vector3ArrayLine(state.avgEulerAngAcc));
            builder.Append(",");
            builder.Append(Vector3ArrayLine(state.avgEulerAngJerk));
            return builder.ToString();
        }


        public string FSRLine(FSRState state)
        {
            return $"{state.Heel},{state.Middle_Inner},{state.Middle_Outer},{state.Toe}";
        }

        public string FSRHeaderLine(string prefix = "")
        {
            return $"{prefix}Heel,{prefix}Middle_Inner,{prefix}Middle_Outer,{prefix}Toe";
        }


        public string JointHeaderLing(string prefix)
        {
            return $"{prefix}PosX,{prefix}PosY,{prefix}PosZ,{prefix}RotX,{prefix}RotY,{prefix}RotZ,{prefix}RotW";
        }

        public string SkeletonHeaderLine(int jointCount)
        {
            StringBuilder builder = new();
            for (int i = 0; i < jointCount; i++)
            {
                builder.Append(JointHeaderLing($"Joint{i}"));
                builder.Append(",");
            }
            return builder.ToString();
        }
        public string SkeletonLine(SkeletonState state)
        {
            StringBuilder builder = new();
            for (int i = 0; i < state.JointPos.Length; i++)
            {
                builder.Append($"{Vector3Line(state.JointPos[i])},{QuaternionLine(state.JointRot[i])}");
                builder.Append(",");
            }

            return builder.ToString();
        }
    
        public string StateLine(SensorSystemState state)
        {
            return $"{state.timestamp},{FSRLine(state.leftFoot)},{FSRLine((state.rightFoot))},{SkeletonLine(state.skeleton)},{DerivedStateLine(state.derivedState)}";
        }


        public string StateDataHeaderLine(int jointCount)
        {
            return $"Time,{FSRHeaderLine("left")},{FSRHeaderLine("right")},{SkeletonHeaderLine(jointCount)},{DerivedStateHeader()}";
        }

        public string ExerciseMetadataHeader() {
            return "ExerciseType, TimeOfDay";
        }

        public string PatientDataHeader(Dictionary<string,string> patientData) { 
            StringBuilder builder = new();
            foreach (var key in patientData.Keys)
            {
                builder.Append($"{key},");
            }
            return builder.ToString();
        }

        public string PatientDataLine(Dictionary<string, string> patientData)
        {
            StringBuilder builder = new();
            foreach (var value in patientData.Values)
            {
                builder.Append($"{value},");
            }
            return builder.ToString();
        }

        public string ExerciseMetadataLine(ExerciseMetadata rawdata, DerivedMetadata derivedData)
        {
            return $"{rawdata.exerciseType}, {derivedData.TimeOfDay} {PatientDataLine(rawdata.patientData)}";
        }

        public string HeaderLine(Exercise exercise)
        {
            var jointCount = new BodyFormat(exercise.GetMetadata().bodyFormat).jointCount;
            return $"{ExerciseMetadataHeader()},{PatientDataHeader(exercise.GetMetadata().patientData)},{StateDataHeaderLine(jointCount)}";
        }


        public string CSVString(Exercise exercise)
        {
            if (!exercise.GetMetadata().isProcessed)
            {
                Debug.LogError("Unprocessed Exercise Loaded into CSVCreator");
                return "unprocessed";
            };

            StringBuilder builder = new();
            builder.Append(HeaderLine(exercise));
            builder.Append("\n");
            foreach (var state in exercise.GetSensorStates())
            {
                if(state.skeleton == null || state.skeleton.JointPos.Length == 0)
                {
                    continue;
                }
                builder.Append(ExerciseMetadataLine(exercise.GetMetadata(), exercise.derivedMetadata    ));
                builder.Append(",");
                builder.Append(StateLine(state));
                builder.Append("\n");
            }

            return builder.ToString();
        }

        
    }
}