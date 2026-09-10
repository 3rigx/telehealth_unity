namespace Assets.Scripts.Sensors.Zed
{
    /// <summary>
    ///     BODY_34 joint enum mapping for named access to skeleton joints.
    ///     Based on Stereolabs ZED SDK BODY_34 format:
    ///     https://www.stereolabs.com/docs/body-tracking
    /// </summary>
    public enum ZedBody34Joint
    {
        // Spine and core
        PELVIS = 0,
        SPINE_1 = 1,
        SPINE_2 = 2,
        SPINE_3 = 3,
        NECK = 4,
        
        // Head
        NOSE = 5,
        LEFT_EYE = 6,
        RIGHT_EYE = 7,
        LEFT_EAR = 8,
        RIGHT_EAR = 9,
        
        // Left upper limb
        LEFT_CLAVICLE = 10,
        LEFT_SHOULDER = 11,
        LEFT_ELBOW = 12,
        LEFT_WRIST = 13,
        
        // Right upper limb
        RIGHT_CLAVICLE = 14,
        RIGHT_SHOULDER = 15,
        RIGHT_ELBOW = 16,
        RIGHT_WRIST = 17,
        
        // Left lower limb
        LEFT_HIP = 18,
        LEFT_KNEE = 19,
        LEFT_ANKLE = 20,
        LEFT_BIG_TOE = 21,
        LEFT_SMALL_TOE = 22,
        LEFT_HEEL = 23,
        
        // Right lower limb
        RIGHT_HIP = 24,
        RIGHT_KNEE = 25,
        RIGHT_ANKLE = 26,
        RIGHT_BIG_TOE = 27,
        RIGHT_SMALL_TOE = 28,
        RIGHT_HEEL = 29,
        
        // Additional BODY_34 specific joints (hand refinements if applicable)
        COUNT = 30
    }
}
