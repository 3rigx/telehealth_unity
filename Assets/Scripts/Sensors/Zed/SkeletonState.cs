using System;
using UnityEngine;

#pragma warning disable S1104

namespace Assets.Scripts.Sensors.Zed
{
    [Serializable]
    public class SkeletonState
    {
        
        [SerializeField] public Vector3 cameraPos;
        [SerializeField] public Quaternion cameraRot;
        [SerializeField] public float FeetOffset;
        [SerializeField] public Vector3[] JointPos;
        [SerializeField] public Quaternion[] JointRot;
        [SerializeField] public Quaternion RootRot;
        [SerializeField] public BODY_FORMAT m_Format;

        [NonSerialized] public BodyFormat Format;


        /// <summary>
        ///     Default constructor, mainly used by serializers
        /// </summary>
        /// <remarks>DO NOT USE</remarks>
        public SkeletonState()
        {
            RootRot = Quaternion.identity;
            JointPos = Array.Empty<Vector3>();
            JointRot = Array.Empty<Quaternion>();
            FeetOffset = 0.0f;
            cameraPos = Vector3.zero;
            cameraRot = Quaternion.identity;
            m_Format = BODY_FORMAT.BODY_34;
            Format = BodyFormat.BODY_34;
        }

        public SkeletonState(BODY_FORMAT bodyFormat, Vector3[] jointPos, Quaternion[] jointRot, Quaternion rootRot, float feetOffset,
            Transform cameraTransform)
        {
            JointPos = jointPos;
            JointRot = jointRot;
            RootRot = rootRot;
            FeetOffset = feetOffset;
            cameraPos = cameraTransform.position;
            cameraRot = cameraTransform.rotation;
            m_Format = bodyFormat;
            Format = new(bodyFormat);
        }
    }
}