using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Assets.Scripts.Exercises;
using Assets.Scripts.Replay;
using Assets.Scripts.Sensors;
using Assets.Scripts.Sensors.Eeg;
using Assets.Scripts.Sensors.FSR;
using Assets.Scripts.Sensors.Zed;
using Assets.Scripts.Util;
using UnityEngine;

namespace Assets.Scripts.Network
{
    /// <summary>
    ///     Formats sensor data as the dashboard's <c>sensor_update</c> JSON and sends it
    ///     through <see cref="TelerehabWebSocketServer"/>. Called every tick by
    ///     ExerciseController (live capture) and ReplayController (replay).
    ///
    ///     Contract (must match the Flutter models in lib/models/):
    ///     - skeleton.joints: map of joint name -> [x,y,z] metres; names must match
    ///       Skeleton3D.defaultBones (pelvis, chest, neck, head, shoulderL/R, elbowL/R,
    ///       wristL/R, hipL/R, kneeL/R, ankleL/R, toeL/R). NaN joints are omitted.
    ///     - plantar (single insole, study default): {foot, insoleCount:1, zones:{toe,
    ///       medial, lateral, heel} normalised 0..1, totalLoad, heelLoad, forefootLoad,
    ///       stability}. Legacy two-insole (PressureConfig.InsoleCount==2) still emits
    ///       {left, right, ...} with the same medial/lateral pad names.
    /// </summary>
    public static class TelerehabBroadcaster
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static int _sessionNumber = 1;
        private static int _trialNumber = 1;
        private static string _participantId = "P001";
        private static string _condition = "Motor";
        private static bool _isRecording;
        private static bool _isPaused;

        /// <summary>Set by ExerciseController each frame: the stored baseline was captured under load.</summary>
        public static bool PressureBaselineUnderLoad;

        /// <summary>Set by ExerciseController each frame: FSR pad samples are actually arriving
        /// (port open but silent — e.g. handshake not completed — reads false).</summary>
        public static bool PressureStreaming;
        private static float _recordingStart;
        private static readonly List<float> _pressureHistory = new();
        private static readonly List<float> _balanceHistory = new();

        /// <summary>(flutterName, BODY_34 index, BODY_38 index)</summary>
        private static readonly (string name, int idx34, int idx38)[] JointMap =
        {
            ("pelvis",    BodyFormat.JointType_34_SpineBase,     BodyFormat.JointType_PELVIS),
            ("chest",     BodyFormat.JointType_34_SpineChest,    BodyFormat.JointType_SPINE_3),
            ("neck",      BodyFormat.JointType_34_Neck,          BodyFormat.JointType_NECK),
            ("head",      BodyFormat.JointType_34_Head,          BodyFormat.JointType_NOSE),
            ("shoulderL", BodyFormat.JointType_34_ShoulderLeft,  BodyFormat.JointType_LEFT_SHOULDER),
            ("shoulderR", BodyFormat.JointType_34_ShoulderRight, BodyFormat.JointType_RIGHT_SHOULDER),
            ("elbowL",    BodyFormat.JointType_34_ElbowLeft,     BodyFormat.JointType_LEFT_ELBOW),
            ("elbowR",    BodyFormat.JointType_34_ElbowRight,    BodyFormat.JointType_RIGHT_ELBOW),
            ("wristL",    BodyFormat.JointType_34_WristLeft,     BodyFormat.JointType_LEFT_WRIST),
            ("wristR",    BodyFormat.JointType_34_WristRight,    BodyFormat.JointType_RIGHT_WRIST),
            ("hipL",      BodyFormat.JointType_34_HipLeft,       BodyFormat.JointType_LEFT_HIP),
            ("hipR",      BodyFormat.JointType_34_HipRight,      BodyFormat.JointType_RIGHT_HIP),
            ("kneeL",     BodyFormat.JointType_34_KneeLeft,      BodyFormat.JointType_LEFT_KNEE),
            ("kneeR",     BodyFormat.JointType_34_KneeRight,     BodyFormat.JointType_RIGHT_KNEE),
            ("ankleL",    BodyFormat.JointType_34_AnkleLeft,     BodyFormat.JointType_LEFT_ANKLE),
            ("ankleR",    BodyFormat.JointType_34_AnkleRight,    BodyFormat.JointType_RIGHT_ANKLE),
            ("toeL",      BodyFormat.JointType_34_FootLeft,      BodyFormat.JointType_LEFT_BIG_TOE),
            ("toeR",      BodyFormat.JointType_34_FootRight,     BodyFormat.JointType_RIGHT_BIG_TOE),
        };

        /// <summary>JointAngleCalculator angle name -> Flutter joint name (for activeJoint).</summary>
        private static readonly Dictionary<string, string> AngleNameToJoint = new()
        {
            ["Left Elbow"] = "elbowL",
            ["Right Elbow"] = "elbowR",
            ["Left Knee"] = "kneeL",
            ["Right Knee"] = "kneeR",
            ["Left Shoulder"] = "shoulderL",
            ["Right Shoulder"] = "shoulderR",
            ["Left Hip"] = "hipL",
            ["Right Hip"] = "hipR",
        };

        public static void SetSessionInfo(string participantId, int session, int trial, string condition)
        {
            _participantId = participantId;
            _sessionNumber = session;
            _trialNumber = trial;
            _condition = condition;
        }

        public static void SetRecording(bool recording, bool paused = false)
        {
            if (recording && !_isRecording)
                _recordingStart = Time.realtimeSinceStartup;
            _isRecording = recording;
            _isPaused = paused;
        }

        /// <summary>
        ///     Milliseconds since recording started, on the same clock the sensor
        ///     writers timestamp against, or -1 when not recording. Read on the Unity
        ///     main thread only (Time.realtimeSinceStartup). Used to answer the
        ///     dashboard's clock-sync ping so it can align its markers to this clock.
        /// </summary>
        public static int RecordingClockMs =>
            _isRecording
                ? Mathf.Max(0, (int)((Time.realtimeSinceStartup - _recordingStart) * 1000f))
                : -1;

        /// <summary>
        ///     Broadcast one sensor frame (skeleton + FSR + computed angles) to all
        ///     connected Flutter clients. <paramref name="angles"/> may be null when no
        ///     body is tracked.
        /// </summary>
        public static void BroadcastState(
            SensorSystemState state,
            JointAngleCalculator.JointAngle[] angles,
            bool cameraConnected = true,
            bool pressureConnected = false,
            EegLiveData eeg = null)
        {
            if (TelerehabWebSocketServer.Instance == null || state == null) return;

            // A dashboard-configured session carries the authoritative identity.
            if (SessionContext.IsConfigured)
            {
                _participantId = SessionContext.PatientId ?? _participantId;
                _trialNumber = SessionContext.TrialNumber;
                _condition = SessionContext.ExerciseClass.ToDisplay();
            }

            int recSeconds = _isRecording
                ? (int)(Time.realtimeSinceStartup - _recordingStart)
                : 0;

            var skeletonJson = BuildSkeletonJson(state.skeleton, angles);

            var sb = new StringBuilder(2048);
            sb.Append("{\"type\":\"sensor_update\",\"session\":{")
              .Append("\"participantId\":\"").Append(TelerehabWebSocketServer.EscapeJson(_participantId)).Append("\",")
              .Append("\"sessionNumber\":").Append(_sessionNumber).Append(',')
              .Append("\"trialNumber\":").Append(_trialNumber).Append(',')
              .Append("\"condition\":\"").Append(TelerehabWebSocketServer.EscapeJson(_condition)).Append("\",")
              .Append("\"recordingSeconds\":").Append(recSeconds).Append(',')
              .Append("\"isRecording\":").Append(_isRecording ? "true" : "false").Append(',')
              .Append("\"isPaused\":").Append(_isPaused ? "true" : "false")
              .Append("},\"jointAngles\":").Append(BuildAngleJson(angles))
              .Append(",\"plantar\":").Append(BuildPlantarJson(state.leftFoot, state.rightFoot));

            if (skeletonJson != null)
                sb.Append(",\"skeleton\":").Append(skeletonJson);

            bool eegConnected = eeg != null && eeg.connected;
            sb.Append(",\"sensors\":{")
              .Append("\"camera\":").Append(cameraConnected ? "true" : "false").Append(',')
              .Append("\"pressureInsole\":").Append(pressureConnected ? "true" : "false").Append(',')
              .Append("\"pressureStreaming\":").Append(PressureStreaming ? "true" : "false").Append(',')
              .Append("\"eeg\":").Append(eegConnected ? "true" : "false")
              .Append("},\"pressureHistory\":").Append(BuildFloatArrayJson(_pressureHistory));

            if (eegConnected)
                sb.Append(",\"eeg\":").Append(BuildEegJson(eeg));

            sb.Append('}');

            TelerehabWebSocketServer.Instance.Broadcast(sb.ToString());
        }

        private static string BuildEegJson(EegLiveData e)
        {
            var sb = new StringBuilder(192);
            sb.Append("{\"theta\":").Append(e.theta.ToString("F2", Inv))
              .Append(",\"alpha\":").Append(e.alpha.ToString("F2", Inv))
              .Append(",\"beta\":").Append(e.beta.ToString("F2", Inv))
              .Append(",\"quality\":\"").Append(TelerehabWebSocketServer.EscapeJson(e.quality ?? "")).Append('"')
              .Append(",\"artifact\":\"").Append(TelerehabWebSocketServer.EscapeJson(e.artifact ?? "")).Append('"')
              .Append(",\"channels\":");
            if (e.channelRms == null || e.channelRms.Length == 0)
            {
                sb.Append("[]");
            }
            else
            {
                sb.Append('[');
                for (int i = 0; i < e.channelRms.Length; i++)
                    sb.Append(e.channelRms[i].ToString("F1", Inv)).Append(i < e.channelRms.Length - 1 ? "," : "");
                sb.Append(']');
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildAngleJson(JointAngleCalculator.JointAngle[] angles)
        {
            if (angles == null || angles.Length == 0) return "[]";
            var sb = new StringBuilder("[");
            for (int i = 0; i < angles.Length; i++)
            {
                var a = angles[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":\"").Append(a.name).Append("\",")
                  .Append("\"angle\":").Append(Safe(a.currentAngle).ToString("F2", Inv)).Append(',')
                  .Append("\"deviation\":").Append(Safe(a.deviationFromRest).ToString("F2", Inv)).Append(',')
                  .Append("\"repCount\":0,\"targetReps\":10,\"trackingConfidence\":1.0,\"trunkLean\":0.0}");
            }
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>Returns null when there is no usable skeleton (so Flutter gets skeleton: absent).</summary>
        private static string BuildSkeletonJson(SkeletonState sk, JointAngleCalculator.JointAngle[] angles)
        {
            if (sk?.JointPos == null || sk.JointPos.Length == 0) return null;

            var format = sk.Format != null ? sk.Format.BodyFormatValue : sk.m_Format;
            if (format != BODY_FORMAT.BODY_34 && format != BODY_FORMAT.BODY_38) return null;

            // Present the skeleton as the ZED sees it. Yaw-only camera-relative
            // frame (world up is preserved so a tilted camera doesn't lean the
            // figure), then Z flipped: the Flutter renderer treats +Z as "toward
            // the viewer" while Unity's camera-forward is +Z away from it — raw
            // coords show the subject from behind with knee flexion reading
            // backwards.
            var camFwd = sk.cameraRot * Vector3.forward;
            camFwd.y = 0f;
            var invYaw = camFwd.sqrMagnitude > 1e-6f
                ? Quaternion.Inverse(Quaternion.LookRotation(camFwd.normalized, Vector3.up))
                : Quaternion.identity;

            var sb = new StringBuilder("{\"joints\":{");
            bool any = false;
            foreach (var (name, idx34, idx38) in JointMap)
            {
                int idx = format == BODY_FORMAT.BODY_34 ? idx34 : idx38;
                if (idx < 0 || idx >= sk.JointPos.Length) continue;
                var raw = sk.JointPos[idx];
                // ZED marks undetected joints as NaN — they must not reach the JSON.
                if (float.IsNaN(raw.x) || float.IsNaN(raw.y) || float.IsNaN(raw.z)) continue;

                var p = invYaw * (raw - sk.cameraPos);

                if (any) sb.Append(',');
                any = true;
                sb.Append('"').Append(name).Append("\":[")
                  .Append(p.x.ToString("F3", Inv)).Append(',')
                  .Append(p.y.ToString("F3", Inv)).Append(',')
                  .Append((-p.z).ToString("F3", Inv)).Append(']');
            }
            if (!any) return null;
            sb.Append('}');

            // Until the exercise definition names a target joint, highlight the one
            // deviating most from rest.
            if (angles != null)
            {
                int best = -1;
                float bestDev = 0f;
                for (int i = 0; i < angles.Length; i++)
                {
                    float dev = Mathf.Abs(Safe(angles[i].deviationFromRest));
                    if (dev > bestDev && AngleNameToJoint.ContainsKey(angles[i].name))
                    {
                        bestDev = dev;
                        best = i;
                    }
                }
                if (best >= 0)
                {
                    sb.Append(",\"activeJoint\":\"").Append(AngleNameToJoint[angles[best].name]).Append("\",")
                      .Append("\"activeAngle\":").Append(Safe(angles[best].currentAngle).ToString("F2", Inv));
                }
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildPlantarJson(FSRState left, FSRState right)
        {
            left ??= new FSRState();
            right ??= new FSRState();

            if (PressureConfig.IsSingleFoot)
                return BuildSingleFootPlantarJson(PressureConfig.Foot == "left" ? left : right);

            float lToe = Norm(left.Toe), lMidI = Norm(left.Middle_Inner),
                  lMidO = Norm(left.Middle_Outer), lHeel = Norm(left.Heel);
            float rToe = Norm(right.Toe), rMidI = Norm(right.Middle_Inner),
                  rMidO = Norm(right.Middle_Outer), rHeel = Norm(right.Heel);

            float sumL = lToe + lMidI + lMidO + lHeel;
            float sumR = rToe + rMidI + rMidO + rHeel;
            float total = (sumL + sumR) / 8f * 100f;
            float heelLoad = (lHeel + rHeel) / 2f * 100f;
            float forefootLoad = (lToe + lMidI + lMidO + rToe + rMidI + rMidO) / 6f * 100f;
            float asymmetry = sumL + sumR > 1e-4f ? Mathf.Abs(sumL - sumR) / (sumL + sumR) * 100f : 0f;

            _pressureHistory.Add(total);
            if (_pressureHistory.Count > 80) _pressureHistory.RemoveAt(0);

            // Stability = SD of the lateral load balance over the recent window
            // (0 = rock steady, grows with mediolateral sway).
            float balance = sumL + sumR > 1e-4f ? (sumR - sumL) / (sumL + sumR) : 0f;
            _balanceHistory.Add(balance);
            if (_balanceHistory.Count > 80) _balanceHistory.RemoveAt(0);
            float stability = StdDev(_balanceHistory);

            var sb = new StringBuilder(256);
            sb.Append("{\"left\":").Append(ZonesJson(lToe, lMidI, lMidO, lHeel))
              .Append(",\"right\":").Append(ZonesJson(rToe, rMidI, rMidO, rHeel))
              .Append(",\"totalLoad\":").Append(total.ToString("F1", Inv))
              .Append(",\"heelLoad\":").Append(heelLoad.ToString("F1", Inv))
              .Append(",\"forefootLoad\":").Append(forefootLoad.ToString("F1", Inv))
              .Append(",\"asymmetry\":").Append(asymmetry.ToString("F1", Inv))
              .Append(",\"stability\":").Append(stability.ToString("F3", Inv))
              .Append('}');
            return sb.ToString();
        }

        /// <summary>
        ///     Single-insole plantar frame: one foot, four named pads (medial/lateral,
        ///     never midInner/midOuter), no L/R asymmetry. Stability is the SD of the
        ///     mediolateral centre-of-pressure index over the recent window.
        /// </summary>
        private static string BuildSingleFootPlantarJson(FSRState f)
        {
            f ??= new FSRState();
            float toe = Norm(f.Toe), medial = Norm(f.Middle_Inner),
                  lateral = Norm(f.Middle_Outer), heel = Norm(f.Heel);

            float sum = toe + medial + lateral + heel;
            float total = sum / 4f * 100f;
            float heelLoad = heel * 100f;
            float forefootLoad = (toe + medial + lateral) / 3f * 100f;

            _pressureHistory.Add(total);
            if (_pressureHistory.Count > 80) _pressureHistory.RemoveAt(0);

            // Mediolateral COP index: +1 fully lateral … −1 fully medial. Its SD over the
            // window is the single-foot balance measure (replaces the old L−R sway).
            float ml = sum > 1e-4f ? (lateral - medial) / sum : 0f;
            _balanceHistory.Add(ml);
            if (_balanceHistory.Count > 80) _balanceHistory.RemoveAt(0);
            float stability = StdDev(_balanceHistory);

            var sb = new StringBuilder(256);
            sb.Append("{\"foot\":\"").Append(PressureConfig.Foot).Append("\",\"insoleCount\":1,\"zones\":")
              .Append(ZonesJson(toe, medial, lateral, heel))
              .Append(",\"totalLoad\":").Append(total.ToString("F1", Inv))
              .Append(",\"heelLoad\":").Append(heelLoad.ToString("F1", Inv))
              .Append(",\"forefootLoad\":").Append(forefootLoad.ToString("F1", Inv))
              .Append(",\"stability\":").Append(stability.ToString("F3", Inv))
              .Append(",\"baselineUnderLoad\":").Append(PressureBaselineUnderLoad ? "true" : "false")
              .Append('}');
            return sb.ToString();
        }

        private static string ZonesJson(float toe, float medial, float lateral, float heel)
        {
            return "{\"toe\":" + toe.ToString("F3", Inv) +
                   ",\"medial\":" + medial.ToString("F3", Inv) +
                   ",\"lateral\":" + lateral.ToString("F3", Inv) +
                   ",\"heel\":" + heel.ToString("F3", Inv) + "}";
        }

        // Normalise a raw ADC reading to 0..1 using the configured full-scale (10-bit →
        // 1023). Was hardcoded /255 (8-bit), which clamped every loaded pad to the top of
        // the range — flat-topped data on the exact capture path we record research on.
        private static float Norm(int raw) => Mathf.Clamp01(raw / (float)PressureConfig.AdcMax);

        private static float Safe(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;

        private static float StdDev(List<float> values)
        {
            if (values.Count < 2) return 0f;
            float mean = 0f;
            foreach (var v in values) mean += v;
            mean /= values.Count;
            float sq = 0f;
            foreach (var v in values) sq += (v - mean) * (v - mean);
            return Mathf.Sqrt(sq / values.Count);
        }

        private static string BuildFloatArrayJson(IList<float> values)
        {
            if (values == null || values.Count == 0) return "[]";
            var sb = new StringBuilder("[");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(values[i].ToString("F2", Inv));
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
