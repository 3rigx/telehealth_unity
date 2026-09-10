using UnityEngine;
using Assets.Scripts.Sensors.Zed;

namespace Assets.Scripts.Replay
{
    /// <summary>
    /// Utility class for calculating joint angles from skeleton data
    /// </summary>
    public static class JointAngleCalculator
    {
        /// <summary>
        /// Represents a joint angle measurement
        /// </summary>
        public struct JointAngle
        {
            public string name;
            public float currentAngle;
            public float restAngle;
            public float deviationFromRest;
            public Vector3 position;

            public float previousAngle;
            public float angularVelocity;
            public float minAngleSeen;
            public float maxAngleSeen;
            public float timeInWarning;
            public float timeInCritical;
            
            public JointAngle(string name, float currentAngle, float restAngle, Vector3 position)
            {
                this.name = name;
                this.currentAngle = currentAngle;
                this.restAngle = restAngle;
                this.deviationFromRest = currentAngle - restAngle;
                this.position = position;
                this.previousAngle = currentAngle;
                this.angularVelocity = 0f;
                this.minAngleSeen = currentAngle;
                this.maxAngleSeen = currentAngle;
                this.timeInWarning = 0f;
                this.timeInCritical = 0f;
            }
        }

        /// <summary>
        /// Calculate angle between three points in degrees
        /// </summary>
        private static float CalculateAngle(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ba = (a - b).normalized;
            Vector3 bc = (c - b).normalized;
            
            float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ba, bc), -1f, 1f)) * Mathf.Rad2Deg;
            return angle;
        }

        /// <summary>
        /// Get joint position by HumanBodyBones type
        /// </summary>
        private static Vector3 GetJointPosition(Vector3[] jointPositions, HumanBodyBones bone, BodyFormat bodyFormat)
        {
            int jointIndex = GetJointIndex(bone, bodyFormat);
            if (jointIndex >= 0 && jointIndex < jointPositions.Length)
            {
                return jointPositions[jointIndex];
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Get joint index for HumanBodyBones based on body format
        /// </summary>
        private static int GetJointIndex(HumanBodyBones bone, BodyFormat bodyFormat)
        {
            // Map HumanBodyBones to joint indices based on the format
            return bodyFormat.BodyFormatValue switch
            {
                BODY_FORMAT.BODY_34 => GetJointIndex34(bone),
                BODY_FORMAT.BODY_38 => GetJointIndex38(bone),
                _ => -1
            };
        }

        /// <summary>
        /// Get joint index for BODY_34 format
        /// </summary>
        private static int GetJointIndex34(HumanBodyBones bone)
        {
            return bone switch
            {
                HumanBodyBones.Hips => BodyFormat.JointType_34_SpineBase,
                HumanBodyBones.Spine => BodyFormat.JointType_34_SpineNaval,
                HumanBodyBones.UpperChest => BodyFormat.JointType_34_SpineChest,
                HumanBodyBones.Neck => BodyFormat.JointType_34_Neck,
                HumanBodyBones.LeftShoulder => BodyFormat.JointType_34_ShoulderLeft,
                HumanBodyBones.LeftUpperArm => BodyFormat.JointType_34_ShoulderLeft,
                HumanBodyBones.LeftLowerArm => BodyFormat.JointType_34_ElbowLeft,
                HumanBodyBones.LeftHand => BodyFormat.JointType_34_WristLeft,
                HumanBodyBones.RightShoulder => BodyFormat.JointType_34_ShoulderRight,
                HumanBodyBones.RightUpperArm => BodyFormat.JointType_34_ShoulderRight,
                HumanBodyBones.RightLowerArm => BodyFormat.JointType_34_ElbowRight,
                HumanBodyBones.RightHand => BodyFormat.JointType_34_WristRight,
                HumanBodyBones.LeftUpperLeg => BodyFormat.JointType_34_HipLeft,
                HumanBodyBones.LeftLowerLeg => BodyFormat.JointType_34_KneeLeft,
                HumanBodyBones.LeftFoot => BodyFormat.JointType_34_FootLeft,
                HumanBodyBones.LeftToes => BodyFormat.JointType_34_FootLeft,
                HumanBodyBones.RightUpperLeg => BodyFormat.JointType_34_HipRight,
                HumanBodyBones.RightLowerLeg => BodyFormat.JointType_34_KneeRight,
                HumanBodyBones.RightFoot => BodyFormat.JointType_34_FootRight,
                HumanBodyBones.RightToes => BodyFormat.JointType_34_FootRight,
                HumanBodyBones.Head => BodyFormat.JointType_34_Head,
                _ => -1
            };
        }

        /// <summary>
        /// Get joint index for BODY_38 format
        /// </summary>
        private static int GetJointIndex38(HumanBodyBones bone)
        {
            return bone switch
            {
                HumanBodyBones.Hips => BodyFormat.JointType_PELVIS,
                HumanBodyBones.Spine => BodyFormat.JointType_SPINE_1,
                HumanBodyBones.Chest => BodyFormat.JointType_SPINE_2,
                HumanBodyBones.UpperChest => BodyFormat.JointType_SPINE_3,
                HumanBodyBones.Neck => BodyFormat.JointType_NECK,
                HumanBodyBones.LeftShoulder => BodyFormat.JointType_LEFT_SHOULDER,
                HumanBodyBones.LeftUpperArm => BodyFormat.JointType_LEFT_SHOULDER,
                HumanBodyBones.LeftLowerArm => BodyFormat.JointType_LEFT_ELBOW,
                HumanBodyBones.LeftHand => BodyFormat.JointType_LEFT_WRIST,
                HumanBodyBones.RightShoulder => BodyFormat.JointType_RIGHT_SHOULDER,
                HumanBodyBones.RightUpperArm => BodyFormat.JointType_RIGHT_SHOULDER,
                HumanBodyBones.RightLowerArm => BodyFormat.JointType_RIGHT_ELBOW,
                HumanBodyBones.RightHand => BodyFormat.JointType_RIGHT_WRIST,
                HumanBodyBones.LeftUpperLeg => BodyFormat.JointType_LEFT_HIP,
                HumanBodyBones.LeftLowerLeg => BodyFormat.JointType_LEFT_KNEE,
                HumanBodyBones.LeftFoot => BodyFormat.JointType_LEFT_ANKLE,
                HumanBodyBones.LeftToes => BodyFormat.JointType_LEFT_ANKLE,
                HumanBodyBones.RightUpperLeg => BodyFormat.JointType_RIGHT_HIP,
                HumanBodyBones.RightLowerLeg => BodyFormat.JointType_RIGHT_KNEE,
                HumanBodyBones.RightFoot => BodyFormat.JointType_RIGHT_ANKLE,
                HumanBodyBones.RightToes => BodyFormat.JointType_RIGHT_ANKLE,
                _ => -1
            };
        }

        /// <summary>
        /// Calculate left elbow angle (angle between upper arm, elbow, and forearm)
        /// </summary>
        public static JointAngle CalculateLeftElbowAngle(Vector3[] jointPositions, BodyFormat bodyFormat, Vector3[] restPositions = null)
        {
            Vector3 shoulder = GetJointPosition(jointPositions, HumanBodyBones.LeftShoulder, bodyFormat);
            Vector3 elbow = GetJointPosition(jointPositions, HumanBodyBones.LeftLowerArm, bodyFormat);
            Vector3 wrist = GetJointPosition(jointPositions, HumanBodyBones.LeftHand, bodyFormat);

            float currentAngle = CalculateAngle(shoulder, elbow, wrist);
            float restAngle = 0f;

            if (restPositions != null)
            {
                Vector3 restShoulder = GetJointPosition(restPositions, HumanBodyBones.LeftShoulder, bodyFormat);
                Vector3 restElbow = GetJointPosition(restPositions, HumanBodyBones.LeftLowerArm, bodyFormat);
                Vector3 restWrist = GetJointPosition(restPositions, HumanBodyBones.LeftHand, bodyFormat);
                restAngle = CalculateAngle(restShoulder, restElbow, restWrist);
            }
            else
            {
                // Default rest angle for elbow (slightly bent)
                restAngle = 15f;
            }

            return new JointAngle("Left Elbow", currentAngle, restAngle, elbow);
        }

        /// <summary>
        /// Calculate right elbow angle
        /// </summary>
        public static JointAngle CalculateRightElbowAngle(Vector3[] jointPositions, BodyFormat bodyFormat, Vector3[] restPositions = null)
        {
            Vector3 shoulder = GetJointPosition(jointPositions, HumanBodyBones.RightShoulder, bodyFormat);
            Vector3 elbow = GetJointPosition(jointPositions, HumanBodyBones.RightLowerArm, bodyFormat);
            Vector3 wrist = GetJointPosition(jointPositions, HumanBodyBones.RightHand, bodyFormat);

            float currentAngle = CalculateAngle(shoulder, elbow, wrist);
            float restAngle = 0f;

            if (restPositions != null)
            {
                Vector3 restShoulder = GetJointPosition(restPositions, HumanBodyBones.RightShoulder, bodyFormat);
                Vector3 restElbow = GetJointPosition(restPositions, HumanBodyBones.RightLowerArm, bodyFormat);
                Vector3 restWrist = GetJointPosition(restPositions, HumanBodyBones.RightHand, bodyFormat);
                restAngle = CalculateAngle(restShoulder, restElbow, restWrist);
            }
            else
            {
                restAngle = 15f;
            }

            return new JointAngle("Right Elbow", currentAngle, restAngle, elbow);
        }

        /// <summary>
        /// Calculate left knee angle (angle between thigh, knee, and shin)
        /// </summary>
        public static JointAngle CalculateLeftKneeAngle(Vector3[] jointPositions, BodyFormat bodyFormat, Vector3[] restPositions = null)
        {
            Vector3 hip = GetJointPosition(jointPositions, HumanBodyBones.LeftUpperLeg, bodyFormat);
            Vector3 knee = GetJointPosition(jointPositions, HumanBodyBones.LeftLowerLeg, bodyFormat);
            Vector3 ankle = GetJointPosition(jointPositions, HumanBodyBones.LeftFoot, bodyFormat);

            float currentAngle = CalculateAngle(hip, knee, ankle);
            float restAngle = 0f;

            if (restPositions != null)
            {
                Vector3 restHip = GetJointPosition(restPositions, HumanBodyBones.LeftUpperLeg, bodyFormat);
                Vector3 restKnee = GetJointPosition(restPositions, HumanBodyBones.LeftLowerLeg, bodyFormat);
                Vector3 restAnkle = GetJointPosition(restPositions, HumanBodyBones.LeftFoot, bodyFormat);
                restAngle = CalculateAngle(restHip, restKnee, restAnkle);
            }
            else
            {
                // Default rest angle for knee (straight leg)
                restAngle = 180f;
            }

            return new JointAngle("Left Knee", currentAngle, restAngle, knee);
        }

        /// <summary>
        /// Calculate right knee angle
        /// </summary>
        public static JointAngle CalculateRightKneeAngle(Vector3[] jointPositions, BodyFormat bodyFormat, Vector3[] restPositions = null)
        {
            Vector3 hip = GetJointPosition(jointPositions, HumanBodyBones.RightUpperLeg, bodyFormat);
            Vector3 knee = GetJointPosition(jointPositions, HumanBodyBones.RightLowerLeg, bodyFormat);
            Vector3 ankle = GetJointPosition(jointPositions, HumanBodyBones.RightFoot, bodyFormat);

            float currentAngle = CalculateAngle(hip, knee, ankle);
            float restAngle = 0f;

            if (restPositions != null)
            {
                Vector3 restHip = GetJointPosition(restPositions, HumanBodyBones.RightUpperLeg, bodyFormat);
                Vector3 restKnee = GetJointPosition(restPositions, HumanBodyBones.RightLowerLeg, bodyFormat);
                Vector3 restAnkle = GetJointPosition(restPositions, HumanBodyBones.RightFoot, bodyFormat);
                restAngle = CalculateAngle(restHip, restKnee, restAnkle);
            }
            else
            {
                restAngle = 180f;
            }

            return new JointAngle("Right Knee", currentAngle, restAngle, knee);
        }

        /// <summary>
        /// Calculate left shoulder angle (angle between spine, shoulder, and upper arm)
        /// </summary>
        public static JointAngle CalculateLeftShoulderAngle(Vector3[] jointPositions, BodyFormat bodyFormat, Vector3[] restPositions = null)
        {
            Vector3 spine = GetJointPosition(jointPositions, HumanBodyBones.Spine, bodyFormat);
            Vector3 shoulder = GetJointPosition(jointPositions, HumanBodyBones.LeftShoulder, bodyFormat);
            Vector3 elbow = GetJointPosition(jointPositions, HumanBodyBones.LeftLowerArm, bodyFormat);

            float currentAngle = CalculateAngle(spine, shoulder, elbow);
            float restAngle = 0f;

            if (restPositions != null)
            {
                Vector3 restSpine = GetJointPosition(restPositions, HumanBodyBones.Spine, bodyFormat);
                Vector3 restShoulder = GetJointPosition(restPositions, HumanBodyBones.LeftShoulder, bodyFormat);
                Vector3 restElbow = GetJointPosition(restPositions, HumanBodyBones.LeftLowerArm, bodyFormat);
                restAngle = CalculateAngle(restSpine, restShoulder, restElbow);
            }
            else
            {
                restAngle = 20f; // Arms slightly forward at rest
            }

            return new JointAngle("Left Shoulder", currentAngle, restAngle, shoulder);
        }

        /// <summary>
        /// Calculate right shoulder angle
        /// </summary>
        public static JointAngle CalculateRightShoulderAngle(Vector3[] jointPositions, BodyFormat bodyFormat, Vector3[] restPositions = null)
        {
            Vector3 spine = GetJointPosition(jointPositions, HumanBodyBones.Spine, bodyFormat);
            Vector3 shoulder = GetJointPosition(jointPositions, HumanBodyBones.RightShoulder, bodyFormat);
            Vector3 elbow = GetJointPosition(jointPositions, HumanBodyBones.RightLowerArm, bodyFormat);

            float currentAngle = CalculateAngle(spine, shoulder, elbow);
            float restAngle = 0f;

            if (restPositions != null)
            {
                Vector3 restSpine = GetJointPosition(restPositions, HumanBodyBones.Spine, bodyFormat);
                Vector3 restShoulder = GetJointPosition(restPositions, HumanBodyBones.RightShoulder, bodyFormat);
                Vector3 restElbow = GetJointPosition(restPositions, HumanBodyBones.RightLowerArm, bodyFormat);
                restAngle = CalculateAngle(restSpine, restShoulder, restElbow);
            }
            else
            {
                restAngle = 20f;
            }

            return new JointAngle("Right Shoulder", currentAngle, restAngle, shoulder);
        }

        /// <summary>
        /// Calculate left hip angle (angle between upper body, hip, and thigh)
        /// </summary>
        public static JointAngle CalculateLeftHipAngle(Vector3[] jointPositions, BodyFormat bodyFormat, Vector3[] restPositions = null)
        {
            Vector3 spine = GetJointPosition(jointPositions, HumanBodyBones.Spine, bodyFormat);
            Vector3 hip = GetJointPosition(jointPositions, HumanBodyBones.LeftUpperLeg, bodyFormat);
            Vector3 knee = GetJointPosition(jointPositions, HumanBodyBones.LeftLowerLeg, bodyFormat);

            float currentAngle = CalculateAngle(spine, hip, knee);
            float restAngle = 0f;

            if (restPositions != null)
            {
                Vector3 restSpine = GetJointPosition(restPositions, HumanBodyBones.Spine, bodyFormat);
                Vector3 restHip = GetJointPosition(restPositions, HumanBodyBones.LeftUpperLeg, bodyFormat);
                Vector3 restKnee = GetJointPosition(restPositions, HumanBodyBones.LeftLowerLeg, bodyFormat);
                restAngle = CalculateAngle(restSpine, restHip, restKnee);
            }
            else
            {
                restAngle = 180f; // Straight standing position
            }

            return new JointAngle("Left Hip", currentAngle, restAngle, hip);
        }

        /// <summary>
        /// Calculate right hip angle
        /// </summary>
        public static JointAngle CalculateRightHipAngle(Vector3[] jointPositions, BodyFormat bodyFormat, Vector3[] restPositions = null)
        {
            Vector3 spine = GetJointPosition(jointPositions, HumanBodyBones.Spine, bodyFormat);
            Vector3 hip = GetJointPosition(jointPositions, HumanBodyBones.RightUpperLeg, bodyFormat);
            Vector3 knee = GetJointPosition(jointPositions, HumanBodyBones.RightLowerLeg, bodyFormat);

            float currentAngle = CalculateAngle(spine, hip, knee);
            float restAngle = 0f;

            if (restPositions != null)
            {
                Vector3 restSpine = GetJointPosition(restPositions, HumanBodyBones.Spine, bodyFormat);
                Vector3 restHip = GetJointPosition(restPositions, HumanBodyBones.RightUpperLeg, bodyFormat);
                Vector3 restKnee = GetJointPosition(restPositions, HumanBodyBones.RightLowerLeg, bodyFormat);
                restAngle = CalculateAngle(restSpine, restHip, restKnee);
            }
            else
            {
                restAngle = 180f;
            }

            return new JointAngle("Right Hip", currentAngle, restAngle, hip);
        }

        /// <summary>
        /// Calculate all major joint angles
        /// </summary>
        public static JointAngle[] CalculateAllJointAngles(Vector3[] jointPositions, BodyFormat bodyFormat, Vector3[] restPositions = null)
        {
            return new JointAngle[]
            {
                CalculateLeftElbowAngle(jointPositions, bodyFormat, restPositions),
                CalculateRightElbowAngle(jointPositions, bodyFormat, restPositions),
                CalculateLeftKneeAngle(jointPositions, bodyFormat, restPositions),
                CalculateRightKneeAngle(jointPositions, bodyFormat, restPositions),
                CalculateLeftShoulderAngle(jointPositions, bodyFormat, restPositions),
                CalculateRightShoulderAngle(jointPositions, bodyFormat, restPositions),
                CalculateLeftHipAngle(jointPositions, bodyFormat, restPositions),
                CalculateRightHipAngle(jointPositions, bodyFormat, restPositions)
            };
        }
    }
}
