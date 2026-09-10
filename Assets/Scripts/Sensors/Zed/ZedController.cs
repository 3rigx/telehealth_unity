//======= Copyright (c) Stereolabs Corporation, All rights reserved. ===============
//======= Heavily Modified by Zoltán Mészáros
//======= This file uses the ZED SDK's included example code, with modifications where necessary.

#if UNITY_STANDALONE
using System.Collections.Generic;
using sl;
using Assets.Scripts.AvatarRenderer;
using Assets.Scripts.UI.Chart;
using UnityEngine;
using UnityEngine.Assertions;
#if ZED_URP
using UnityEngine.Rendering.Universal;
#endif

using CustomSkeletonHandler = Assets.Scripts.AvatarRenderer.CustomSkeletonHandler;

namespace Assets.Scripts.Sensors.Zed
{
    /// <summary>
    ///     Contols the ZEDSkeletonTracking . Links the SDK to Unity
    /// </summary>
    [DisallowMultipleComponent]
    public class CustomZedController : MonoBehaviour, ISensorController<SkeletonState>
    {
        public static bool DisplayDebugSkeleton = false;

        private readonly float alpha = 0.1f;

        [Header("Avatar Control")]
        /// <summary>
        /// Avatar game object
        /// </summary>
        [Tooltip("3D Rigged model.")]
        public GameObject Avatar;

        public Dictionary<int, CustomSkeletonHandler> AvatarControlList;

        private sl.BODY_FORMAT bodyFormat = sl.BODY_FORMAT.BODY_34;

        [Tooltip("Record video?")] public bool IsRecording = true;

        public GameObject Joint;

#nullable enable
        public JointChart? jointChart;
#nullable disable
        [Space(5)]
        [Tooltip("Mirror the animation.")]
        public bool MirrorMode;

        /// <summary>
        ///     Display objects that are visible but not actively being tracked by object tracking (usually because object tracking
        ///     is disabled in ZEDManager).
        /// </summary>
        [Tooltip(
            "Display objects that are visible but not actively being tracked by object tracking (usually because object tracking is disabled in ZEDManager).")]
        public bool ShowOff = false;

        [Space(5)]
        [Header("State Filters")]
        [Tooltip(
            "Display objects that are actively being tracked by object tracking, where valid positions are known. ")]
        public bool ShowOn = true;

        /// <summary>
        ///     Display objects that were actively being tracked by object tracking, but that were lost very recently.
        /// </summary>
        [Tooltip(
            "Display objects that were actively being tracked by object tracking, but that were lost very recently.")]
        public bool ShowSearching = false;


        /// <summary>
        ///     Activate skeleton tracking when play mode is on and ZED ready
        /// </summary>
        [Header("Game Control")] public bool StartObjectDetectionAutomatically = true;

        /// <summary>
        ///     Vizualisation mode. Use a 3D model or only display the skeleton
        /// </summary>
        [Header("Vizualisation Mode")]
        /// <summary>
        /// Display 3D avatar. If set to false, only display bones and joint
        /// </summary>
        [Tooltip("Display 3D avatar. If set to false, only display bones and joint")]
        public bool UseAvatar = true;

        [Tooltip("The camera view to display ZED images")]
        public Camera ViewCamera;


        /// <summary>
        ///     The scene's ZEDManager.
        ///     If you want to visualize detections from multiple ZEDs at once you will need multiple ZED3DSkeletonVisualizer
        ///     commponents in the scene.
        /// </summary>
        [Tooltip("The scene's ZEDManager.\r\n" +
                 "If you want to visualize detections from multiple ZEDs at once you will need multiple ZED3DSkeletonVisualizer commponents in the scene. ")]
        public ZEDManager ZedManager;

        public void Read()
        {
            // Nothing to do: Read operation handled by camera
        }

        public SkeletonState GetState()
        {
            return skeletonSensorState;
        }


        /// <summary>
        ///     Start this instance.
        /// </summary>
        private void Start()
        {
            QualitySettings.vSyncCount = 1; // Activate vsync

            AvatarControlList = new Dictionary<int, CustomSkeletonHandler>();
            if (!ZedManager) ZedManager = FindObjectsByType<ZEDManager>()[0];
            Assert.IsNotNull(ZedManager, "ZEDManager not found in scene.");
#if ZED_URP
                UniversalAdditionalCameraData urpCamData =
     ZedManager.GetLeftCamera().GetComponent<UniversalAdditionalCameraData>();
                urpCamData.renderPostProcessing = true;
                urpCamData.renderShadows = false;
#endif
            ZedManager.OnZEDReady += OnZEDReady;
            ZedManager.OnBodyTracking += OnBodyTrackingFrame;

            bodyFormat = ZedManager.bodyFormat;
        }

        private void OnZEDReady()
        {
            if (StartObjectDetectionAutomatically && !ZedManager.IsObjectDetectionRunning)
            {
                // Self-occlusion during adduction (arm/hand against the torso) makes the
                // MEDIUM model drop the wrist/elbow. The ACCURATE model tracks occluded
                // limbs far better, body fitting infers hidden joints from the kinematic
                // skeleton, and a longer prediction timeout keeps the joint alive while
                // it is briefly hidden against the body instead of blanking out at 0.2 s.
                ZedManager.bodyTrackingModel = sl.BODY_TRACKING_MODEL.HUMAN_BODY_ACCURATE;
                ZedManager.enableBodyFitting = true;
                ZedManager.bodyTrackingPredictionTimeout = 1.0f;
                ZedManager.StartBodyTracking();
            }
        }

        private void OnDestroy()
        {
            if (!ZedManager) return;

            ZedManager.OnBodyTracking -= OnBodyTrackingFrame;
            ZedManager.OnZEDReady -= OnZEDReady;

            ZedManager.needRecordFrame = false;

            if (!IsRecording) return;

            ZedManager.needRecordFrame = false;
            ZedManager.zedCamera?.DisableRecording();
            IsRecording = false;
        }


        public void StartRecording()
        {
            if (ZedManager && videoFilePath != null)
            {
                Debug.Log(videoFilePath);

                var code = ZedManager.zedCamera.EnableRecording(videoFilePath);
                if (code == ERROR_CODE.SUCCESS)
                {
                    ZedManager.needRecordFrame = true;
                    IsRecording = true;
                }
                else
                {
                    ZedManager.needRecordFrame = false;
                    Debug.LogError("Recording failed " + code);
                }
            }
        }

        public void StopRecording()
        {
            if (!ZedManager) return;

            ZedManager.needRecordFrame = false;
            ZedManager.zedCamera.DisableRecording();
            IsRecording = false;
        }

        public void ToggleRecording()
        {
            if (IsRecording)
                StopRecording();
            else
                StartRecording();
        }

        public void SetVideoRecordingLocation(string path)
        {
            videoFilePath = path;
        }

        /// <summary>
        ///     Updates the skeleton data from ZEDCamera call and send it to Skeleton Handler script.
        /// </summary>
        public void OnBodyTrackingFrame(BodyTrackingFrame bodyframe)
        {
            var remainingKeyList = new List<int>(AvatarControlList.Keys);
            var newobjects = bodyframe.GetFilteredObjectList(ShowOn, ShowSearching, ShowOff);

            foreach (var dobj in newobjects)
            {
                var personId = dobj.id;


                //Avatar controller already exist 
                if (AvatarControlList.TryGetValue(personId, out CustomSkeletonHandler handler))
                {
                    // remove keys from list
                    remainingKeyList.Remove(personId);
                }
                else
                {
                    handler = ScriptableObject.CreateInstance<CustomSkeletonHandler>();
                    handler.Create(Avatar, Joint);
                    handler.InitSkeleton(personId);
                    AvatarControlList.Add(personId, handler);
                }
                UpdateAvatarControl(handler, dobj.rawBodyData);


                if (patientHandler == null)
                {
                    patientHandler = handler;
                    handler.SetPatient();
                }
            }

            // Remove deleted skeletons
            foreach (var index in remainingKeyList)
            {
                CustomSkeletonHandler handler = AvatarControlList[index]!;

                if (handler == patientHandler)
                {
                    Debug.Log("Lost patient");
                    patientHandler = null;
                }
                handler.Destroy();
                AvatarControlList.Remove(index);
            }
        }


        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                UseAvatar = !UseAvatar;
                Debug.Log(UseAvatar
                    ? "<b><color=green> " +
                      "Switch" +
                      " to Avatar mode</color></b>"
                    : "<b><color=green> Switch to Skeleton mode</color></b>"
                );
            }

            if (UseAvatar)
                foreach (var skelet in AvatarControlList)
                    skelet.Value.Move();

            UpdateViewCameraPosition();
        }

        /// <summary>
        ///     Function to update avatar control with data from ZED SDK.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <param name="data">Body Data From SDK</param>
        private void UpdateAvatarControl(CustomSkeletonHandler handler, BodyData data)
        {
            var worldJointsPos = new Vector3[handler.currentJoints.Length];
            var worldJointsRot = new Quaternion[handler.currentJoints.Length];

            for (var i = 0; i < handler.currentJoints.Length; i++)
            {
                worldJointsPos[i] = ZedManager.GetZedRootTransform().TransformPoint(data.keypoint[i]);
                worldJointsRot[i] = data.localOrientationPerJoint[i].normalized;
            }
            var rootRotation = ZedManager.GetZedRootTransform().rotation * data.globalRootOrientation;


            if (bodyFormat == sl.BODY_FORMAT.BODY_34 && data.keypointConfidence[(int)BODY_34_PARTS.LEFT_ANKLE] != 0 &&
                data.keypointConfidence[(int)BODY_34_PARTS.RIGHT_ANKLE] != 0) handler.AdjustFeetOffset(alpha);


            handler.SetControlWithJointPosition(worldJointsPos, worldJointsRot, rootRotation, UseAvatar, MirrorMode);

            if (handler != patientHandler) return;

            skeletonSensorState = new SkeletonState((BODY_FORMAT)bodyFormat, worldJointsPos, worldJointsRot, rootRotation,
                handler.FeetOffset, ViewCamera.transform);
            if (jointChart != null && worldJointsPos.Length == handler.currentJoints.Length)
                jointChart.AddState(skeletonSensorState);
        }

        private void UpdateViewCameraPosition()
        {
            ViewCamera.transform.SetLocalPositionAndRotation(ZedManager.transform.localPosition,
                ZedManager.transform.localRotation);
        }

        public void SetPatientHandler(CustomSkeletonHandler handler)
        {
            Debug.Log("Trying to set patient");
            if (patientHandler != null)
            {
                patientHandler.clearPatient();
            }
            patientHandler = handler;
            handler.SetPatient();
        }

        public bool IsReady()
        {
            return ZedManager.IsZEDReady && ZedManager.IsObjectDetectionRunning;
        }

        public CustomSkeletonHandler GetPatientHandler()
        {
            return patientHandler;
        }

#nullable enable
        [Tooltip("Index of the patient to track.")]
        private CustomSkeletonHandler? patientHandler;

        private string? videoFilePath;
        private SkeletonState? skeletonSensorState;
#nullable disable
    }
}
#endif