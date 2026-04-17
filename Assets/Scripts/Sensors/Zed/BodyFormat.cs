using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using UnityEngine;
using UnityEngine.Assertions;

namespace Assets.Scripts.Sensors.Zed
{
    public enum BODY_FORMAT
    {
        BODY_18 = 0,
        BODY_34 = 1,
        BODY_38 = 2,
    };

    public class BodyFormat
    {

        public static readonly BODY_FORMAT[] BODY_FORMATS = { BODY_FORMAT.BODY_18, BODY_FORMAT.BODY_34, BODY_FORMAT.BODY_38 };

        public static readonly BodyFormat BODY_18 = new(BODY_FORMAT.BODY_18);
        public static readonly BodyFormat BODY_34 = new(BODY_FORMAT.BODY_34);
        public static readonly BodyFormat BODY_38 = new(BODY_FORMAT.BODY_38);

        public static readonly BodyFormat[] BodyFormats = { BODY_18, BODY_34, BODY_38 };
#region constants
        // For Skeleton Display
        public const int
            // --------- Common
            JointType_PELVIS = 0,
            JointType_SPINE_1 = 1,
            JointType_SPINE_2 = 2,
            JointType_SPINE_3 = 3,
            JointType_NECK = 4,
            JointType_NOSE = 5,
            JointType_LEFT_EYE = 6,
            JointType_RIGHT_EYE = 7,
            JointType_LEFT_EAR = 8,
            JointType_RIGHT_EAR = 9,
            JointType_LEFT_CLAVICLE = 10,
            JointType_RIGHT_CLAVICLE = 11,
            JointType_LEFT_SHOULDER = 12,
            JointType_RIGHT_SHOULDER = 13,
            JointType_LEFT_ELBOW = 14,
            JointType_RIGHT_ELBOW = 15,
            JointType_LEFT_WRIST = 16,
            JointType_RIGHT_WRIST = 17,
            JointType_LEFT_HIP = 18,
            JointType_RIGHT_HIP = 19,
            JointType_LEFT_KNEE = 20,
            JointType_RIGHT_KNEE = 21,
            JointType_LEFT_ANKLE = 22,
            JointType_RIGHT_ANKLE = 23,
            JointType_LEFT_BIG_TOE = 24,
            JointType_RIGHT_BIG_TOE = 25,
            JointType_LEFT_SMALL_TOE = 26,
            JointType_RIGHT_SMALL_TOE = 27,
            JointType_LEFT_HEEL = 28,
            JointType_RIGHT_HEEL = 29,
            // --------- Body 38 specific
            JointType_38_LEFT_HAND_THUMB_4 = 30, // tip
            JointType_38_RIGHT_HAND_THUMB_4 = 31,
            JointType_38_LEFT_HAND_INDEX_1 = 32, // knuckle
            JointType_38_RIGHT_HAND_INDEX_1 = 33,
            JointType_38_LEFT_HAND_MIDDLE_4 = 34, // tip
            JointType_38_RIGHT_HAND_MIDDLE_4 = 35,
            JointType_38_LEFT_HAND_PINKY_1 = 36, // knuckle
            JointType_38_RIGHT_HAND_PINKY_1 = 37,
            JointType_38_COUNT = 38,
            // --------- Body 70 specific
            // Left hand
            JointType_70_LEFT_HAND_THUMB_1 = 30,
            JointType_70_LEFT_HAND_THUMB_2 = 31,
            JointType_70_LEFT_HAND_THUMB_3 = 32,
            JointType_70_LEFT_HAND_THUMB_4 = 33, // tip
            JointType_70_LEFT_HAND_INDEX_1 = 34, // knuckle
            JointType_70_LEFT_HAND_INDEX_2 = 35,
            JointType_70_LEFT_HAND_INDEX_3 = 36,
            JointType_70_LEFT_HAND_INDEX_4 = 37, // tip
            JointType_70_LEFT_HAND_MIDDLE_1 = 38,
            JointType_70_LEFT_HAND_MIDDLE_2 = 39,
            JointType_70_LEFT_HAND_MIDDLE_3 = 40,
            JointType_70_LEFT_HAND_MIDDLE_4 = 41,
            JointType_70_LEFT_HAND_RING_1 = 42,
            JointType_70_LEFT_HAND_RING_2 = 43,
            JointType_70_LEFT_HAND_RING_3 = 44,
            JointType_70_LEFT_HAND_RING_4 = 45,
            JointType_70_LEFT_HAND_PINKY_1 = 46,
            JointType_70_LEFT_HAND_PINKY_2 = 47,
            JointType_70_LEFT_HAND_PINKY_3 = 48,
            JointType_70_LEFT_HAND_PINKY_4 = 49,
            // Right hand
            JointType_70_RIGHT_HAND_THUMB_1 = 50,
            JointType_70_RIGHT_HAND_THUMB_2 = 51,
            JointType_70_RIGHT_HAND_THUMB_3 = 52,
            JointType_70_RIGHT_HAND_THUMB_4 = 53,
            JointType_70_RIGHT_HAND_INDEX_1 = 54,
            JointType_70_RIGHT_HAND_INDEX_2 = 55,
            JointType_70_RIGHT_HAND_INDEX_3 = 56,
            JointType_70_RIGHT_HAND_INDEX_4 = 57,
            JointType_70_RIGHT_HAND_MIDDLE_1 = 58,
            JointType_70_RIGHT_HAND_MIDDLE_2 = 59,
            JointType_70_RIGHT_HAND_MIDDLE_3 = 60,
            JointType_70_RIGHT_HAND_MIDDLE_4 = 61,
            JointType_70_RIGHT_HAND_RING_1 = 62,
            JointType_70_RIGHT_HAND_RING_2 = 63,
            JointType_70_RIGHT_HAND_RING_3 = 64,
            JointType_70_RIGHT_HAND_RING_4 = 65,
            JointType_70_RIGHT_HAND_PINKY_1 = 66,
            JointType_70_RIGHT_HAND_PINKY_2 = 67,
            JointType_70_RIGHT_HAND_PINKY_3 = 68,
            JointType_70_RIGHT_HAND_PINKY_4 = 69,
            JointType_70_COUNT = 70,
            // --------- Body34
            JointType_34_Head = 26,
            JointType_34_Neck = 3,
            JointType_34_ClavicleRight = 11,
            JointType_34_ShoulderRight = 12,
            JointType_34_ElbowRight = 13,
            JointType_34_WristRight = 14,
            JointType_34_ClavicleLeft = 4,
            JointType_34_ShoulderLeft = 5,
            JointType_34_ElbowLeft = 6,
            JointType_34_WristLeft = 7,
            JointType_34_HipRight = 22,
            JointType_34_KneeRight = 23,
            JointType_34_AnkleRight = 24,
            JointType_34_FootRight = 25,
            JointType_34_HeelRight = 33,
            JointType_34_HipLeft = 18,
            JointType_34_KneeLeft = 19,
            JointType_34_AnkleLeft = 20,
            JointType_34_FootLeft = 21,
            JointType_34_HeelLeft = 32,
            JointType_34_EyesRight = 30,
            JointType_34_EyesLeft = 28,
            JointType_34_EarRight = 31,
            JointType_34_EarLeft = 29,
            JointType_34_SpineBase = 0,
            JointType_34_SpineNaval = 1,
            JointType_34_SpineChest = 2,
            JointType_34_Nose = 27,
            JointType_34_COUNT = 34;


        // For Skeleton Display
        public static readonly int
            // JointType
            jointTypeHead = 26,
            jointTypeNeck = 3,
            jointTypeClavicleRight = 11,
            jointTypeShoulderRight = 12,
            jointTypeElbowRight = 13,
            jointTypeWristRight = 14,
            jointTypeClavicleLeft = 4,
            jointTypeShoulderLeft = 5,
            jointTypeElbowLeft = 6,
            jointTypeWristLeft = 7,
            jointTypeHipRight = 22,
            jointTypeKneeRight = 23,
            jointTypeAnkleRight = 24,
            jointTypeFootRight = 25,
            jointTypeHeelRight = 33,
            jointTypeHipLeft = 18,
            jointTypeKneeLeft = 19,
            jointTypeAnkleLeft = 20,
            jointTypeFootLeft = 21,
            jointTypeHeelLeft = 32,
            jointTypeEyesRight = 30,
            jointTypeEyesLeft = 28,
            jointTypeEarRight = 31,
            jointTypeEarLeft = 29,
            jointTypeSpineBase = 0,
            jointTypeSpineNaval = 1,
            jointTypeSpineChest = 2,
            jointTypeNose = 27,
            r_jointcount = 34;

        // List of bones (pair of joints).
        public static readonly int[] s_BonesList =
        {
            jointTypeSpineBase, jointTypeHipRight,
            jointTypeHipLeft, jointTypeSpineBase,
            jointTypeSpineBase, jointTypeSpineNaval,
            jointTypeSpineNaval, jointTypeSpineChest,
            jointTypeSpineChest, jointTypeNeck,
            jointTypeEarRight, jointTypeEyesRight,
            jointTypeEarLeft, jointTypeEyesLeft,
            jointTypeEyesRight, jointTypeNose,
            jointTypeEyesLeft, jointTypeNose,
            jointTypeNose, jointTypeNeck,
            // left
            jointTypeSpineChest, jointTypeClavicleLeft,
            jointTypeClavicleLeft, jointTypeShoulderLeft,
            jointTypeShoulderLeft, jointTypeElbowLeft, // LeftUpperArm
            jointTypeElbowLeft, jointTypeWristLeft, // LeftLowerArm
            jointTypeHipLeft, jointTypeKneeLeft, // LeftUpperLeg
            jointTypeKneeLeft, jointTypeAnkleLeft, // LeftLowerLeg6
            jointTypeAnkleLeft, jointTypeFootLeft,
            jointTypeAnkleLeft, jointTypeHeelLeft,
            jointTypeFootLeft, jointTypeHeelLeft,
            // right
            jointTypeSpineChest, jointTypeClavicleRight,
            jointTypeClavicleRight, jointTypeShoulderRight,
            jointTypeShoulderRight, jointTypeElbowRight, // RightUpperArm
            jointTypeElbowRight, jointTypeWristRight, // RightLowerArm
            jointTypeHipRight, jointTypeKneeRight, // RightUpperLeg
            jointTypeKneeRight, jointTypeAnkleRight, // RightLowerLeg
            jointTypeAnkleRight, jointTypeFootRight,
            jointTypeAnkleRight, jointTypeHeelRight,
            jointTypeFootRight, jointTypeHeelRight
        };

        // List of joint that will be rendered as a sphere in the Skeleton mode
        public static readonly int[] sphereList34 =
        {
            jointTypeSpineBase,
            jointTypeSpineNaval,
            jointTypeSpineChest,
            jointTypeNeck,
            jointTypeHipLeft,
            jointTypeHipRight,
            jointTypeClavicleLeft,
            jointTypeShoulderLeft,
            jointTypeElbowLeft,
            jointTypeWristLeft,
            jointTypeKneeLeft,
            jointTypeAnkleLeft,
            jointTypeFootLeft,
            jointTypeHeelLeft,
            jointTypeClavicleRight,
            jointTypeShoulderRight,
            jointTypeElbowRight,
            jointTypeWristRight,
            jointTypeKneeRight,
            jointTypeAnkleRight,
            jointTypeFootRight,
            jointTypeHeelRight,
            jointTypeEyesLeft,
            jointTypeEyesRight,
            jointTypeEarRight,
            jointTypeEarLeft,
            jointTypeNose
        };


        public static readonly string[] sphereNameList34 =
                {
            "Spine Base",
            "Spine Naval",
            "Spine Chest",
            "Neck",
            "Hip Left",
            "Hip Right",
            "Clavicle Left",
            "Shoulder Left",
            "Elbow Left",
            "Wrist Left",
            "Knee Left",
            "Ankle Left",
            "Foot Left",
            "Heel Left",
            "Clavicle Right",
            "Shoulder Right",
            "Elbow Right",
            "Wrist Right",
            "Knee Right",
            "Ankle Right",
            "Foot Right",
            "Heel Right",
            "Eyes Left",
            "Eyes Right",
            "Ear Right",
            "Ear Left",
            "Nose"
        };

        public static readonly int[] sphereList38 = sphereList34;
        public static string[] sphereNameList38 = sphereNameList34;

        public static readonly int[] sphereList70 = sphereList34;
        public static string[] sphereNameList70 = sphereNameList34;


        // List of bones (pair of joints) for BODY_38. Used for Skeleton mode.
        public static readonly int[] bonesList38 =
        {
            // Torso
            JointType_PELVIS, JointType_SPINE_1,
            JointType_SPINE_1, JointType_SPINE_2,
            JointType_SPINE_2, JointType_SPINE_3,
            JointType_SPINE_3, JointType_NECK,
            JointType_PELVIS, JointType_LEFT_HIP,
            JointType_PELVIS, JointType_RIGHT_HIP,
            JointType_NECK, JointType_NOSE,
            JointType_NECK, JointType_LEFT_CLAVICLE,
            JointType_LEFT_CLAVICLE, JointType_LEFT_SHOULDER,
            JointType_NECK, JointType_RIGHT_CLAVICLE,
            JointType_RIGHT_CLAVICLE, JointType_RIGHT_SHOULDER,
            JointType_NOSE, JointType_LEFT_EYE,
            JointType_LEFT_EYE, JointType_LEFT_EAR,
            JointType_NOSE, JointType_RIGHT_EYE,
            JointType_RIGHT_EYE, JointType_RIGHT_EAR,
            // Left arm
            JointType_LEFT_SHOULDER, JointType_LEFT_ELBOW,
            JointType_LEFT_ELBOW, JointType_LEFT_WRIST,
            JointType_LEFT_WRIST, JointType_38_LEFT_HAND_THUMB_4, // -
            JointType_LEFT_WRIST, JointType_38_LEFT_HAND_INDEX_1,
            JointType_LEFT_WRIST, JointType_38_LEFT_HAND_MIDDLE_4,
            JointType_LEFT_WRIST, JointType_38_LEFT_HAND_PINKY_1, // -
            // right arm
            JointType_RIGHT_SHOULDER, JointType_RIGHT_ELBOW,
            JointType_RIGHT_ELBOW, JointType_RIGHT_WRIST,
            JointType_RIGHT_WRIST, JointType_38_RIGHT_HAND_THUMB_4, // -
            JointType_RIGHT_WRIST, JointType_38_RIGHT_HAND_INDEX_1,
            JointType_RIGHT_WRIST, JointType_38_RIGHT_HAND_MIDDLE_4,
            JointType_RIGHT_WRIST, JointType_38_RIGHT_HAND_PINKY_1, // -
            // legs
            JointType_LEFT_HIP, JointType_LEFT_KNEE,
            JointType_LEFT_KNEE, JointType_LEFT_ANKLE,
            JointType_LEFT_ANKLE, JointType_LEFT_HEEL,
            JointType_LEFT_ANKLE, JointType_LEFT_BIG_TOE,
            JointType_LEFT_ANKLE, JointType_LEFT_SMALL_TOE,
            JointType_RIGHT_HIP, JointType_RIGHT_KNEE,
            JointType_RIGHT_KNEE, JointType_RIGHT_ANKLE,
            JointType_RIGHT_ANKLE, JointType_RIGHT_HEEL,
            JointType_RIGHT_ANKLE, JointType_RIGHT_BIG_TOE,
            JointType_RIGHT_ANKLE, JointType_RIGHT_SMALL_TOE
        };

        // List of bones (pair of joints) for BODY_70. Used for Skeleton mode.
        public static readonly int[] bonesList70 =
        {
            // Torso
            JointType_PELVIS, JointType_SPINE_1,
            JointType_SPINE_1, JointType_SPINE_2,
            JointType_SPINE_2, JointType_SPINE_3,
            JointType_SPINE_3, JointType_NECK,
            JointType_PELVIS, JointType_LEFT_HIP,
            JointType_PELVIS, JointType_RIGHT_HIP,
            JointType_NECK, JointType_NOSE,
            JointType_NECK, JointType_LEFT_CLAVICLE,
            JointType_LEFT_CLAVICLE, JointType_LEFT_SHOULDER,
            JointType_NECK, JointType_RIGHT_CLAVICLE,
            JointType_RIGHT_CLAVICLE, JointType_RIGHT_SHOULDER,
            JointType_NOSE, JointType_LEFT_EYE,
            JointType_LEFT_EYE, JointType_LEFT_EAR,
            JointType_NOSE, JointType_RIGHT_EYE,
            JointType_RIGHT_EYE, JointType_RIGHT_EAR,
            // legs
            JointType_LEFT_HIP, JointType_LEFT_KNEE,
            JointType_LEFT_KNEE, JointType_LEFT_ANKLE,
            JointType_LEFT_ANKLE, JointType_LEFT_HEEL,
            JointType_LEFT_ANKLE, JointType_LEFT_BIG_TOE,
            JointType_LEFT_ANKLE, JointType_LEFT_SMALL_TOE,
            JointType_RIGHT_HIP, JointType_RIGHT_KNEE,
            JointType_RIGHT_KNEE, JointType_RIGHT_ANKLE,
            JointType_RIGHT_ANKLE, JointType_RIGHT_HEEL,
            JointType_RIGHT_ANKLE, JointType_RIGHT_BIG_TOE,
            JointType_RIGHT_ANKLE, JointType_RIGHT_SMALL_TOE,
            // Left arm
            JointType_LEFT_SHOULDER, JointType_LEFT_ELBOW,
            JointType_LEFT_ELBOW, JointType_LEFT_WRIST,
            // right arm
            JointType_RIGHT_SHOULDER, JointType_RIGHT_ELBOW,
            JointType_RIGHT_ELBOW, JointType_RIGHT_WRIST,
            // left hand
            JointType_LEFT_WRIST, JointType_70_LEFT_HAND_THUMB_1,
            JointType_70_LEFT_HAND_THUMB_1, JointType_70_LEFT_HAND_THUMB_2,
            JointType_70_LEFT_HAND_THUMB_2, JointType_70_LEFT_HAND_THUMB_3,
            JointType_70_LEFT_HAND_THUMB_3, JointType_70_LEFT_HAND_THUMB_4,
            JointType_LEFT_WRIST, JointType_70_LEFT_HAND_INDEX_1,
            JointType_70_LEFT_HAND_INDEX_1, JointType_70_LEFT_HAND_INDEX_2,
            JointType_70_LEFT_HAND_INDEX_2, JointType_70_LEFT_HAND_INDEX_3,
            JointType_70_LEFT_HAND_INDEX_3, JointType_70_LEFT_HAND_INDEX_4,
            JointType_LEFT_WRIST, JointType_70_LEFT_HAND_MIDDLE_1,
            JointType_70_LEFT_HAND_MIDDLE_1, JointType_70_LEFT_HAND_MIDDLE_2,
            JointType_70_LEFT_HAND_MIDDLE_2, JointType_70_LEFT_HAND_MIDDLE_3,
            JointType_70_LEFT_HAND_MIDDLE_3, JointType_70_LEFT_HAND_MIDDLE_4,
            JointType_LEFT_WRIST, JointType_70_LEFT_HAND_RING_1,
            JointType_70_LEFT_HAND_RING_1, JointType_70_LEFT_HAND_RING_2,
            JointType_70_LEFT_HAND_RING_2, JointType_70_LEFT_HAND_RING_3,
            JointType_70_LEFT_HAND_RING_3, JointType_70_LEFT_HAND_RING_4,
            JointType_LEFT_WRIST, JointType_70_LEFT_HAND_PINKY_1,
            JointType_70_LEFT_HAND_PINKY_1, JointType_70_LEFT_HAND_PINKY_2,
            JointType_70_LEFT_HAND_PINKY_2, JointType_70_LEFT_HAND_PINKY_3,
            JointType_70_LEFT_HAND_PINKY_3, JointType_70_LEFT_HAND_PINKY_4,
            // right hand
            JointType_RIGHT_WRIST, JointType_70_RIGHT_HAND_THUMB_1,
            JointType_70_RIGHT_HAND_THUMB_1, JointType_70_RIGHT_HAND_THUMB_2,
            JointType_70_RIGHT_HAND_THUMB_2, JointType_70_RIGHT_HAND_THUMB_3,
            JointType_70_RIGHT_HAND_THUMB_3, JointType_70_RIGHT_HAND_THUMB_4,
            JointType_RIGHT_WRIST, JointType_70_RIGHT_HAND_INDEX_1,
            JointType_70_RIGHT_HAND_INDEX_1, JointType_70_RIGHT_HAND_INDEX_2,
            JointType_70_RIGHT_HAND_INDEX_2, JointType_70_RIGHT_HAND_INDEX_3,
            JointType_70_RIGHT_HAND_INDEX_3, JointType_70_RIGHT_HAND_INDEX_4,
            JointType_RIGHT_WRIST, JointType_70_RIGHT_HAND_MIDDLE_1,
            JointType_70_RIGHT_HAND_MIDDLE_1, JointType_70_RIGHT_HAND_MIDDLE_2,
            JointType_70_RIGHT_HAND_MIDDLE_2, JointType_70_RIGHT_HAND_MIDDLE_3,
            JointType_70_RIGHT_HAND_MIDDLE_3, JointType_70_RIGHT_HAND_MIDDLE_4,
            JointType_RIGHT_WRIST, JointType_70_RIGHT_HAND_RING_1,
            JointType_70_RIGHT_HAND_RING_1, JointType_70_RIGHT_HAND_RING_2,
            JointType_70_RIGHT_HAND_RING_2, JointType_70_RIGHT_HAND_RING_3,
            JointType_70_RIGHT_HAND_RING_3, JointType_70_RIGHT_HAND_RING_4,
            JointType_RIGHT_WRIST, JointType_70_RIGHT_HAND_PINKY_1,
            JointType_70_RIGHT_HAND_PINKY_1, JointType_70_RIGHT_HAND_PINKY_2,
            JointType_70_RIGHT_HAND_PINKY_2, JointType_70_RIGHT_HAND_PINKY_3,
            JointType_70_RIGHT_HAND_PINKY_3, JointType_70_RIGHT_HAND_PINKY_4
        };

        // List of bones (pair of joints) for BODY_34. Used for Skeleton mode.
        public static readonly int[] bonesList34 =
        {
            // Torso
            JointType_34_SpineBase, JointType_34_HipRight,
            JointType_34_HipLeft, JointType_34_SpineBase,
            JointType_34_SpineBase, JointType_34_SpineNaval,
            JointType_34_SpineNaval, JointType_34_SpineChest,
            JointType_34_SpineChest, JointType_34_Neck,
            JointType_34_EarRight, JointType_34_EyesRight,
            JointType_34_EarLeft, JointType_34_EyesLeft,
            JointType_34_EyesRight, JointType_34_Nose,
            JointType_34_EyesLeft, JointType_34_Nose,
            JointType_34_Nose, JointType_34_Neck,
            // left
            JointType_34_SpineChest, JointType_34_ClavicleLeft,
            JointType_34_ClavicleLeft, JointType_34_ShoulderLeft,
            JointType_34_ShoulderLeft, JointType_34_ElbowLeft, // LeftUpperArm
            JointType_34_ElbowLeft, JointType_34_WristLeft, // LeftLowerArm
            JointType_34_HipLeft, JointType_34_KneeLeft, // LeftUpperLeg
            JointType_34_KneeLeft, JointType_34_AnkleLeft, // LeftLowerLeg6
            JointType_34_AnkleLeft, JointType_34_FootLeft,
            JointType_34_AnkleLeft, JointType_34_HeelLeft,
            JointType_34_FootLeft, JointType_34_HeelLeft,
            // right
            JointType_34_SpineChest, JointType_34_ClavicleRight,
            JointType_34_ClavicleRight, JointType_34_ShoulderRight,
            JointType_34_ShoulderRight, JointType_34_ElbowRight, // RightUpperArm
            JointType_34_ElbowRight, JointType_34_WristRight, // RightLowerArm
            JointType_34_HipRight, JointType_34_KneeRight, // RightUpperLeg
            JointType_34_KneeRight, JointType_34_AnkleRight, // RightLowerLeg
            JointType_34_AnkleRight, JointType_34_FootRight,
            JointType_34_AnkleRight, JointType_34_HeelRight,
            JointType_34_FootRight, JointType_34_HeelRight
        };

        // Indexes of bones' parents for BODY_38 
        public static readonly int[] parentsIdx_38 =
        {
            -1,
            0,
            1,
            2,
            3,
            4,
            4,
            4,
            4,
            4,
            3,
            3,
            10,
            11,
            12,
            13,
            14,
            15,
            0,
            0,
            18,
            19,
            20,
            21,
            22,
            23,
            22,
            23,
            22,
            23,
            16,
            17,
            16,
            17,
            16,
            17,
            16,
            17
        };

        // Indexes of bones' parents for BODY_70
        public static readonly int[] parentsIdx_70 =
        {
            -1,
            0,
            1,
            2,
            3,
            4,
            4,
            4,
            4,
            4,
            3,
            3,
            10,
            11,
            12,
            13,
            14,
            15,
            0,
            0,
            18,
            19,
            20,
            21,
            22,
            23,
            22,
            23,
            22,
            23,
            16,
            30,
            31,
            32,
            16,
            30,
            31,
            32,
            16,
            30,
            31,
            32,
            16,
            30,
            31,
            32,
            16,
            30,
            31,
            32,
            17,
            50,
            51,
            52,
            17,
            50,
            51,
            52,
            17,
            50,
            51,
            52,
            17,
            50,
            51,
            52,
            17,
            50,
            51,
            52
        };

        // Indexes of bones' parents for BODY_34 
        public static readonly int[] parentsIdx_34 =
        {
            -1,
            0,
            1,
            2,
            2,
            4,
            5,
            6,
            7,
            8,
            7,
            2,
            11,
            12,
            13,
            14,
            15,
            14,
            0,
            18,
            19,
            20,
            0,
            22,
            23,
            24,
            3,
            26,
            26,
            26,
            26,
            26
        };

        // Bones output by the ZED SDK (in this order)
        public static HumanBodyBones[] humanBones38 =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Neck,
            HumanBodyBones.LastBone, // Nose
            HumanBodyBones.LastBone, // Left Eye
            HumanBodyBones.LastBone, // Right Eye
            HumanBodyBones.LastBone, // Left Ear
            HumanBodyBones.LastBone, // Right Ear
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand, // Left Wrist
            HumanBodyBones.RightHand, // Left Wrist
            HumanBodyBones.LeftUpperLeg, // Left Hip
            HumanBodyBones.RightUpperLeg, // Right Hip
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
            HumanBodyBones.LastBone, // Left Big Toe
            HumanBodyBones.LastBone, // Right Big Toe
            HumanBodyBones.LastBone, // Left Small Toe
            HumanBodyBones.LastBone, // Right Small Toe
            HumanBodyBones.LastBone, // Left Heel
            HumanBodyBones.LastBone, // Right Heel
            // Hands
            HumanBodyBones.LastBone, // Left Hand Thumb Tip
            HumanBodyBones.LastBone, // Right Hand Thumb Tip
            HumanBodyBones.LastBone, // Left Hand Index Knuckle
            HumanBodyBones.LastBone, // Right Hand Index Knuckle
            HumanBodyBones.LastBone, // Left Hand Middle Tip
            HumanBodyBones.LastBone, // Right Hand Middle Tip
            HumanBodyBones.LastBone, // Left Hand Pinky Knuckle
            HumanBodyBones.LastBone, // Right Hand Pinky Knuckle
            HumanBodyBones.LastBone // Last
        };

        // Bones output by the ZED SDK (in this order)
        public static HumanBodyBones[] humanBones70 =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Neck,
            HumanBodyBones.LastBone, // Nose
            HumanBodyBones.LastBone, // Left Eye
            HumanBodyBones.LastBone, // Right Eye
            HumanBodyBones.LastBone, // Left Ear
            HumanBodyBones.LastBone, // Right Ear
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand, // Left Wrist
            HumanBodyBones.RightHand, // Left Wrist
            HumanBodyBones.LeftUpperLeg, // Left Hip
            HumanBodyBones.RightUpperLeg, // Right Hip
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
            HumanBodyBones.LastBone, // Left Big Toe
            HumanBodyBones.LastBone, // Right Big Toe
            HumanBodyBones.LastBone, // Left Small Toe
            HumanBodyBones.LastBone, // Right Small Toe
            HumanBodyBones.LastBone, // Left Heel
            HumanBodyBones.LastBone, // Right Heel
            // Left Hand
            HumanBodyBones.LeftThumbProximal,
            HumanBodyBones.LeftThumbIntermediate,
            HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LastBone, // Left Hand Thumb Tip
            HumanBodyBones.LeftIndexProximal,
            HumanBodyBones.LeftIndexIntermediate,
            HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LastBone, // Left Hand Index Tip
            HumanBodyBones.LeftMiddleProximal,
            HumanBodyBones.LeftMiddleIntermediate,
            HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LastBone, // Left Hand Middle Tip
            HumanBodyBones.LeftRingProximal,
            HumanBodyBones.LeftRingIntermediate,
            HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LastBone, // Left Hand Ring Tip
            HumanBodyBones.LeftLittleProximal,
            HumanBodyBones.LeftLittleIntermediate,
            HumanBodyBones.LeftLittleDistal,
            HumanBodyBones.LastBone, // Left Hand Pinky Tip
            // Right Hand
            HumanBodyBones.RightThumbProximal,
            HumanBodyBones.RightThumbIntermediate,
            HumanBodyBones.RightThumbDistal,
            HumanBodyBones.LastBone, // Right Hand Thumb Tip
            HumanBodyBones.RightIndexProximal,
            HumanBodyBones.RightIndexIntermediate,
            HumanBodyBones.RightIndexDistal,
            HumanBodyBones.LastBone, // Right Hand Index Tip
            HumanBodyBones.RightMiddleProximal,
            HumanBodyBones.RightMiddleIntermediate,
            HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.LastBone, // Right Hand Middle Tip
            HumanBodyBones.RightRingProximal,
            HumanBodyBones.RightRingIntermediate,
            HumanBodyBones.RightRingDistal,
            HumanBodyBones.LastBone, // Right Hand Ring Tip
            HumanBodyBones.RightLittleProximal,
            HumanBodyBones.RightLittleIntermediate,
            HumanBodyBones.RightLittleDistal,
            HumanBodyBones.LastBone, // Right Hand Pinky Tip
            HumanBodyBones.LastBone // Last
        };

        // Bones output by the ZED SDK (in this order)
        public static readonly HumanBodyBones[] humanBones34 =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Neck,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand, // Left Wrist
            HumanBodyBones.LastBone, // Left Hand
            HumanBodyBones.LastBone, // Left HandTip
            HumanBodyBones.LastBone,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand, // Right Wrist
            HumanBodyBones.LastBone, // Right Hand
            HumanBodyBones.LastBone, // Right HandTip
            HumanBodyBones.LastBone,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            HumanBodyBones.RightToes,
            HumanBodyBones.Head,
            HumanBodyBones.LastBone, // Nose
            HumanBodyBones.LastBone, // Left Eye
            HumanBodyBones.LastBone, // Left Ear
            HumanBodyBones.LastBone, // Right Eye
            HumanBodyBones.LastBone // Right Ear
            // HumanBodyBones.LastBone, // Left Heel
            // HumanBodyBones.LastBone, // Right Heel
        };

        private static Dictionary<int, string> jointCommonNames = new Dictionary<int, string>()
        {
            { JointType_PELVIS, "Pelvis" },
            { JointType_SPINE_1, "Spine 1" },
            { JointType_SPINE_2, "Spine 2" },
            { JointType_SPINE_3, "Spine 3" },
            { JointType_NECK, "Neck" },
            { JointType_NOSE, "Nose" },
            { JointType_LEFT_EYE, "Left Eye" },
            { JointType_RIGHT_EYE, "Right Eye" },
            { JointType_LEFT_EAR, "Left Ear" },
            { JointType_RIGHT_EAR, "Right Ear" },
            { JointType_LEFT_CLAVICLE, "Left Clavicle" },
            { JointType_RIGHT_CLAVICLE, "Right Clavicle" },
            { JointType_LEFT_SHOULDER, "Left Shoulder" },
            { JointType_RIGHT_SHOULDER, "Right Shoulder" },
            { JointType_LEFT_ELBOW, "Left Elbow" },
            { JointType_RIGHT_ELBOW, "Right Elbow" },
            { JointType_LEFT_WRIST, "Left Wrist" },
            { JointType_RIGHT_WRIST, "Right Wrist" },
            { JointType_LEFT_HIP, "Left Hip" },
            { JointType_RIGHT_HIP, "Right Hip" },
            { JointType_LEFT_KNEE, "Left Knee" },
            { JointType_RIGHT_KNEE, "Right Knee" },
            { JointType_LEFT_ANKLE, "Left Ankle" },
            { JointType_RIGHT_ANKLE, "Right Ankle" },
            { JointType_LEFT_BIG_TOE, "Left Big Toe" },
            { JointType_RIGHT_BIG_TOE, "Right Big Toe" },
            { JointType_LEFT_SMALL_TOE, "Left Small Toe" },
            { JointType_RIGHT_SMALL_TOE, "Right Small Toe" },
            { JointType_LEFT_HEEL, "Left Heel" },
            { JointType_RIGHT_HEEL, "Right Heel" },
        };

       
        private static Dictionary<int, string> joint34Names = new Dictionary<int, string>()
        {
            {JointType_34_Head, "Head"},
            {JointType_34_Neck, "Neck"},
            { JointType_34_ClavicleRight, "Clavicle Right"},
            { JointType_34_ShoulderRight, "Shoulder Right"},
            { JointType_34_ElbowRight, "Elbow Right"},
            { JointType_34_WristRight, "Wrist Right"},
            { JointType_34_ClavicleLeft, "Clavicle Left"},
            { JointType_34_ShoulderLeft, "Shoulder Left"},
            { JointType_34_ElbowLeft, "Elbow Left"},
            { JointType_34_WristLeft, "Wrist Left"},
            { JointType_34_HipRight, "Hip Right"},
            { JointType_34_KneeRight, "Knee Right"},
            { JointType_34_AnkleRight, "Ankle Right"},
            { JointType_34_FootRight, "Foot Right"},
            { JointType_34_HeelRight, "Heel Right"},
            { JointType_34_HipLeft, "Hip Left"},
            { JointType_34_KneeLeft, "Knee Left"},
            { JointType_34_AnkleLeft, "Ankle Left"},
            { JointType_34_FootLeft, "Foot Left"},
            { JointType_34_HeelLeft, "Heel Left"},
            { JointType_34_EyesRight, "Eyes Right"},
            { JointType_34_EyesLeft, "Eyes Left"},
            { JointType_34_EarRight, "Ear Right"},
            { JointType_34_EarLeft, "Ear Left"},
            { JointType_34_SpineBase, "Spine Base"},
            { JointType_34_SpineNaval, "Spine Naval"},
            { JointType_34_SpineChest, "Spine Chest"},
            { JointType_34_Nose, "Nose"},
        };
        private static Dictionary<int, string> joint38Names = new Dictionary<int, string>()
        {
            {JointType_38_LEFT_HAND_THUMB_4, "Left Hand Thumb Tip"},
            {JointType_38_RIGHT_HAND_THUMB_4, "Right Hand Thumb Tip"},
            {JointType_38_LEFT_HAND_INDEX_1, "Left Hand Index Knuckle"},
            {JointType_38_RIGHT_HAND_INDEX_1, "Right Hand Index Knuckle"},
            {JointType_38_LEFT_HAND_MIDDLE_4, "Left Hand Middle Tip"},
            {JointType_38_RIGHT_HAND_MIDDLE_4, "Right Hand Middle Tip"},
            {JointType_38_LEFT_HAND_PINKY_1, "Left Hand Pinky Knuckle"},
            {JointType_38_RIGHT_HAND_PINKY_1, "Right Hand Pinky Knuckle"},
        };

        private static Dictionary<int, string> joint70Names = new Dictionary<int, string>()
        {
            {JointType_70_LEFT_HAND_THUMB_1 ,  "Left Hand Thumb Knuckle"},
            {JointType_70_LEFT_HAND_THUMB_2 ,  "Left Hand Thumb Middle1"},
            {JointType_70_LEFT_HAND_THUMB_3 ,  "Left Hand Thumb Middle2"},
            {JointType_70_LEFT_HAND_THUMB_4 ,  "Left Hand Thumb Tip"},
            {JointType_70_LEFT_HAND_INDEX_1 ,  "Left Hand Index Knuckle"},
            {JointType_70_LEFT_HAND_INDEX_2 ,  "Left Hand Index Middle1"},
            {JointType_70_LEFT_HAND_INDEX_3 ,  "Left Hand Index Middle2"},
            {JointType_70_LEFT_HAND_INDEX_4 ,  "Left Hand Index Tip"},
            {JointType_70_LEFT_HAND_MIDDLE_1 ,  "Left Hand Middle Knuckle"},
            {JointType_70_LEFT_HAND_MIDDLE_2 ,  "Left Hand Middle Middle1"},
            {JointType_70_LEFT_HAND_MIDDLE_3 ,  "Left Hand Middle Middle2"},
            {JointType_70_LEFT_HAND_MIDDLE_4 ,  "Left Hand Middle Tip"},
            {JointType_70_LEFT_HAND_RING_1 ,  "Left Hand Ring Knuckle"},
            {JointType_70_LEFT_HAND_RING_2 ,  "Left Hand Ring Middle1"},
            {JointType_70_LEFT_HAND_RING_3 ,  "Left Hand Ring Middle2"},
            {JointType_70_LEFT_HAND_RING_4 ,  "Left Hand Ring Tip"},
            {JointType_70_LEFT_HAND_PINKY_1 ,  "Left Hand Pinky Knuckle"},
            {JointType_70_LEFT_HAND_PINKY_2 ,  "Left Hand Pinky Middle1"},
            {JointType_70_LEFT_HAND_PINKY_3 ,  "Left Hand Pinky Middle2"},
            {JointType_70_LEFT_HAND_PINKY_4 ,  "Left Hand Pinky Tip"},

            {JointType_70_RIGHT_HAND_THUMB_1 ,  "Right Hand Thumb Knuckle" },
            {JointType_70_RIGHT_HAND_THUMB_2 ,  "Right Hand Thumb Middle1"},
            {JointType_70_RIGHT_HAND_THUMB_3 ,  "Right Hand Thumb Middle2"},
            {JointType_70_RIGHT_HAND_THUMB_4 ,  "Right Hand Thumb Tip"},
            {JointType_70_RIGHT_HAND_INDEX_1 ,  "Right Hand Index Knuckle"},
            {JointType_70_RIGHT_HAND_INDEX_2 ,  "Right Hand Index Middle1"},
            {JointType_70_RIGHT_HAND_INDEX_3 ,  "Right Hand Index Middle2"},
            {JointType_70_RIGHT_HAND_INDEX_4 ,  "Right Hand Index Tip"},
            {JointType_70_RIGHT_HAND_MIDDLE_1 ,  "Right Hand Middle Knuckle"},
            {JointType_70_RIGHT_HAND_MIDDLE_2 ,  "Right Hand Middle Middle1"},
            {JointType_70_RIGHT_HAND_MIDDLE_3 ,  "Right Hand Middle Middle2"},
            {JointType_70_RIGHT_HAND_MIDDLE_4 ,  "Right Hand Middle Tip"},
            {JointType_70_RIGHT_HAND_RING_1 ,  "Right Hand Ring Knuckle"},
            {JointType_70_RIGHT_HAND_RING_2 ,  "Right Hand Ring Middle1"},
            {JointType_70_RIGHT_HAND_RING_3 ,  "Right Hand Ring Middle2"},
            {JointType_70_RIGHT_HAND_RING_4 ,  "Right Hand Ring Tip"},
            {JointType_70_RIGHT_HAND_PINKY_1 ,  "Right Hand Pinky Knuckle"},
            {JointType_70_RIGHT_HAND_PINKY_2 ,  "Right Hand Pinky Middle1"},
            {JointType_70_RIGHT_HAND_PINKY_3 ,  "Right Hand Pinky Middle2"},
            {JointType_70_RIGHT_HAND_PINKY_4 ,  "Right Hand Pinky Tip"},
        };
#endregion constants
        private static string GetJointCommonTypeName(int jointId)
        {
            return jointCommonNames.TryGetValue(jointId, out var value) ? value : "Unknown";
        }
        private static string GetJoint34TypeName(int jointId)
        {
            return joint34Names.TryGetValue(jointId, out var value) ? value : "Unknown";
        }

       

        public static string GetJoint38TypeName(int jointId)
        {
            return joint38Names.TryGetValue(jointId, out var value) ? value : GetJointCommonTypeName(jointId);
        }

        private static string GetJoint70TypeName(int jointId)
        {
            return joint70Names.TryGetValue(jointId, out var value) ? value : GetJointCommonTypeName(jointId);
        }

        public string GetJointTypeName(int jointId)
        {
            return _bodyFormat switch
            {
                BODY_FORMAT.BODY_34 => GetJoint34TypeName(jointId),
                BODY_FORMAT.BODY_38 => GetJoint38TypeName(jointId),
                _ => "Unknown"
            };
        }

        public string[] getJointNames()
        {
            return _bodyFormat switch
            {
                BODY_FORMAT.BODY_34 => joint34Names.Values.ToArray(),
                BODY_FORMAT.BODY_38 => jointCommonNames.Values.Concat(joint38Names.Values).ToArray(),
                _ => throw new IndexOutOfRangeException("Illegal " + nameof(_bodyFormat) + " " + _bodyFormat)
            };
        }

        public Dictionary<string, int> getJointNamesToIds()
        {
            return _bodyFormat switch
            {
                BODY_FORMAT.BODY_34 => joint34Names.ToDictionary(x => x.Value, x => x.Key),
                BODY_FORMAT.BODY_38 => jointCommonNames.Concat(joint38Names).ToDictionary(x => x.Value, x => x.Key),
                _ => throw new IndexOutOfRangeException("Illegal " + nameof(_bodyFormat) + " " + _bodyFormat)
            };
        }

        [SerializeField] private BODY_FORMAT _bodyFormat;
        public BODY_FORMAT BodyFormatValue => _bodyFormat;

        public BodyFormat(BODY_FORMAT bodyFormat)
        {
            _bodyFormat = bodyFormat;
        }

        public BodyFormat(int bodyFormatValue)
        {
            Assert.IsTrue(Array.IndexOf(BODY_FORMATS, bodyFormatValue) == 1);
            _bodyFormat = (BODY_FORMAT)bodyFormatValue;
        }

        [Obsolete("To transition to attribute jointCount")]
        public int GetJointCount()
        {
            return jointCount;
        }

        public int jointCount
        {
            get
            {
                return _bodyFormat switch
                {
                    BODY_FORMAT.BODY_18 => 18,
                    BODY_FORMAT.BODY_34 => 34,
                    BODY_FORMAT.BODY_38 => 38,
                    _ => throw new IndexOutOfRangeException("Illegal " + nameof(_bodyFormat) + " " + _bodyFormat)
                };
            }
        }
        public int[] parentsIdx
        {
            get
            {
                return _bodyFormat switch
                {
                    BODY_FORMAT.BODY_18 => parentsIdx_38,
                    BODY_FORMAT.BODY_34 => parentsIdx_34,
                    BODY_FORMAT.BODY_38 => parentsIdx_38,
                    _ => throw new IndexOutOfRangeException("Illegal " + nameof(_bodyFormat) + " " + _bodyFormat)
                };
            }
        }

        public HumanBodyBones humanBones
        {
            get
            {
                return _bodyFormat switch
                {
                    BODY_FORMAT.BODY_18 => HumanBodyBones.LastBone,
                    BODY_FORMAT.BODY_34 => HumanBodyBones.LastBone,
                    BODY_FORMAT.BODY_38 => HumanBodyBones.LastBone,
                    _ => throw new IndexOutOfRangeException("Illegal " + nameof(_bodyFormat) + " " + _bodyFormat)
                };
            }
        }

        public static implicit operator BODY_FORMAT(BodyFormat bodyFormat)
        {
            return bodyFormat._bodyFormat;
        }

    }
}
