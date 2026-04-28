//======= Copyright (c) Stereolabs Corporation, All rights reserved. ===============
//======= Heavily Modified by Zoltán Mészáros
//======= This file uses the ZED SDK's included example code, with modifications where necessary.
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Sensors;
using Assets.Scripts.Sensors.Zed;
using Unity.VisualScripting;
using UnityEngine;
using UnityEditor;
using Random = UnityEngine.Random;



namespace Assets.Scripts.AvatarRenderer
{


    class PatientSelectorClickHandler : MonoBehaviour
    {
#nullable enable
        private static CustomZedController? zedController;
#nullable disable
        private CustomSkeletonHandler handler;


        private void Create(CustomSkeletonHandler handler)
        {
            this.handler = handler;
        }


        private void OnMouseOver()
        {
            if (Input.GetMouseButtonDown((int)MouseButton.Left) && Input.GetKey(KeyCode.LeftControl))
            {
                zedController?.SetPatientHandler(handler);

                // ── FIX: expanded console feedback on Ctrl+Left-click patient selection ──
                Debug.Log($"[Patient Selected] ✔ Patient set via Ctrl+Click.\n" +
                          $"  → GameObject  : {gameObject.name}\n" +
                          $"  → IsPatient   : {handler.IsPatient}\n" +
                          $"  → World Pos   : {transform.position}\n" +
                          $"  → Body Format : {handler.bodyFormat}\n" +
                          $"  → Keypoints   : {handler.currentKeypointsCount}\n" +
                          $"  → ZedCtrl     : {(zedController != null ? zedController.name : "NOT FOUND")}");
            }
        }


        public static void attach(GameObject target, CustomSkeletonHandler handler)
        {
            if (zedController == null) zedController = FindObjectsByType<CustomZedController>().First();
            target.AddComponent<PatientSelectorClickHandler>().Create(handler);
        }
    }


    public class CustomSkeletonHandler : ScriptableObject
    {
        private FeetColorer _feetColorer;
        private Animator animator;


        public GameObject[] Bones;




        private BodyFormat currentBodyFormat = new(BodyFormat.BODY_34);
        public int[] currentBonesList;
        public float[] currentConfidences;


        public HumanBodyBones[] currentHumanBodyBones;
        public Vector3[] currentJoints;


        public int currentKeypointsCount = -1;
        public int currentLeftAnkleIndex = -1;
        public int[] currentParentIds;
        public int currentRightAnkleIndex = -1;


        public int[] currentSpheresList;
        public string[] currentSpheresNameList;


        private GameObject humanoid;



        public GameObject JointPrefab;



        public float[] confidences34 = new float[BodyFormat.JointType_34_COUNT];
        public float[] confidences38 = new float[BodyFormat.JointType_38_COUNT];
        public float[] confidences70 = new float[BodyFormat.JointType_70_COUNT];


        public Vector3[] joints34 = new Vector3[BodyFormat.JointType_34_COUNT];
        public Vector3[] joints38 = new Vector3[BodyFormat.JointType_38_COUNT];
        public Vector3[] joints70 = new Vector3[BodyFormat.JointType_70_COUNT];


        private Dictionary<HumanBodyBones, Quaternion> m_DefaultRotations;



        [SerializeField] private float m_FeetOffset;


        public Color MaxPressureColor = Color.red;


        // Feet colorer setting
        public Color MinPressureColor = Color.green;
        public float Powf = 0.75f;



        private Dictionary<HumanBodyBones, RigBone> rigBone;
        private GameObject skeleton;
        public JointOverlay[] SphereOverlays;


        public GameObject[] Spheres;



        private Vector3 targetBodyPosition = new(0.0f, 0.0f, 0.0f);
        private bool usingAvatar = true;



        private ZEDSkeletonAnimator zedSkeletonAnimator = null;
        public Dictionary<HumanBodyBones, Quaternion> RigBoneTarget { get; set; }


        public Quaternion TargetBodyOrientation { get; set; } = Quaternion.identity;
        public Vector3 TargetBodyPositionWithHipOffset { get; set; } = new(0.0f, 0.0f, 0.0f);


        public BodyFormat bodyFormat
        {
            get => currentBodyFormat;
            set
            {
                currentBodyFormat = value;
                UpdateCurrentValues(currentBodyFormat);
            }
        }


        public bool IsPatient { get; private set; }


        public float FeetOffset
        {
            get => m_FeetOffset;
            set => m_FeetOffset = value;
        }


        public void SetPatient()
        {
            IsPatient = true;
            SetColor(new Color(0.0f / 255.0f, 0.0f / 255.0f, 0.0f / 255.0f));
        }


        public void clearPatient()
        {
            IsPatient = false;
            SetColor(colors[Random.Range(0, colors.Length)]);


            Debug.Log("[Patient Cleared] Patient deselected — skeleton returned to random colour.");
        }


        /// <summary>
        ///     Get Animator;
        /// </summary>
        /// <returns>Humanoid</returns>
        public Animator GetAnimator()
        {
            return animator;
        }



        /// <summary>
        ///     Create the avatar control
        /// </summary>
        /// <param name="avatarObject">The avatar prefab</param>
        /// <param name="jointObject">The joint prefab</param>
        /// <param name="bodyFormat">Body format to use</param>
        public void Create(GameObject avatarObject, GameObject jointObject,
            BODY_FORMAT bodyFormat = BODY_FORMAT.BODY_34)
        {
            this.bodyFormat = new(bodyFormat);


            JointPrefab = jointObject;
            humanoid = Instantiate(avatarObject, Vector3.zero, Quaternion.identity);
            PatientSelectorClickHandler.attach(humanoid, this);
            _feetColorer = humanoid.GetComponentInChildren<FeetColorer>();
            var invisiblelayer = LayerMask.NameToLayer("tagInvisibleToZED");
            //humanoid.layer = invisiblelayer;


            //zedSkeletonAnimator = humanoid.GetComponent<ZEDSkeletonAnimator>();
            //zedSkeletonAnimator.Skhandler = this;


            foreach (Transform child in humanoid.transform) child.gameObject.layer = invisiblelayer;


            // Init list of bones that will be updated by the data retrieved from the ZED SDK
            rigBone = new Dictionary<HumanBodyBones, RigBone>();
            RigBoneTarget = new Dictionary<HumanBodyBones, Quaternion>();


            m_DefaultRotations = new Dictionary<HumanBodyBones, Quaternion>();


            foreach (var bone in currentHumanBodyBones)
            {
                if (bone != HumanBodyBones.LastBone)
                {
                    rigBone[bone] = new RigBone(humanoid, bone);


                    if (avatarObject.GetComponent<Animator>())
                    {
                        animator = humanoid.GetComponent<Animator>();
                        m_DefaultRotations[bone] =
                            humanoid.GetComponent<Animator>().GetBoneTransform(bone).localRotation;
                    }
                }


                RigBoneTarget[bone] = Quaternion.identity;
            }
        }


        public void Destroy()
        {
            Destroy(humanoid);
            Destroy(skeleton);
            rigBone.Clear();
            RigBoneTarget.Clear();
            m_DefaultRotations.Clear();
            Array.Clear(Bones, 0, Bones.Length);
            Array.Clear(Spheres, 0, Spheres.Length);
        }


        /// <summary>
        ///     Function that handles the humanoid position, rotation and bones movement
        /// </summary>
        /// <param name="position_center">Position center.</param>
        /// <summary>
        ///     Function that handles the humanoid position, rotation and bones movement.
        ///     Fills the rigBoneTarget map with rotations from the SDK. They can then be applied to the corresponding bones.
        /// </summary>
        /// <param name="rootPosition">Position to apply to the root of the 3D avatar.</param>
        /// <param name="rootRotation">Global rotation of the detected body.</param>
        /// <param name="jointsRotation">Array of rotations ordered following humanBones34.</param>
        /// <param name="mirror">Should do the rotations/translations for mirror mode(true) or not(false).</param>
        private void SetHumanPoseControl(Vector3 rootPosition, Quaternion rootRotation, Quaternion[] jointsRotation,
            bool mirror)
        {
            foreach (var rb in currentHumanBodyBones)
                // Store any joint local rotation (if the bone exists)
                if (rb != HumanBodyBones.LastBone && rigBone[rb].transform)
                    RigBoneTarget[rb] = mirror
                        ? jointsRotation[Array.IndexOf(currentHumanBodyBones, MirrorBone(rb))].mirror_x()
                        : jointsRotation[Array.IndexOf(currentHumanBodyBones, rb)];


            if (mirror)
            {
                rootPosition = rootPosition.mirror_x();
                rootRotation = rootRotation.mirror_x();
            }


            // Store global transform (to be applied to the Hips joint).
            TargetBodyOrientation = rootRotation;
            targetBodyPosition = rootPosition;
        }


        public void SetJointOverlays(Quaternion[] jointsRotation)
        {
            for (var i = 0; i < currentSpheresList.Length; i++)
                SphereOverlays[i].SetRotationDisplay(jointsRotation[currentSpheresList[i]]);
        }


        /// <summary>
        ///     Returns the symmetric/mirror bone of <paramref name="humanBodyBone" /> in the human rig of the animator.
        /// </summary>
        /// <returns></returns>
        private HumanBodyBones MirrorBone(HumanBodyBones humanBodyBone)
        {
            switch (humanBodyBone)
            {
                case HumanBodyBones.Hips: return HumanBodyBones.Hips;
                case HumanBodyBones.LeftUpperLeg: return HumanBodyBones.RightUpperLeg;
                case HumanBodyBones.RightUpperLeg: return HumanBodyBones.LeftUpperLeg;
                case HumanBodyBones.LeftLowerLeg: return HumanBodyBones.RightLowerLeg;
                case HumanBodyBones.RightLowerLeg: return HumanBodyBones.LeftLowerLeg;
                case HumanBodyBones.LeftFoot: return HumanBodyBones.RightFoot;
                case HumanBodyBones.RightFoot: return HumanBodyBones.LeftFoot;
                case HumanBodyBones.Spine: return HumanBodyBones.Spine;
                case HumanBodyBones.Chest: return HumanBodyBones.Chest;
                case HumanBodyBones.UpperChest: return HumanBodyBones.UpperChest;
                case HumanBodyBones.Neck: return HumanBodyBones.Neck;
                case HumanBodyBones.Head: return HumanBodyBones.Head;
                case HumanBodyBones.LeftShoulder: return HumanBodyBones.RightShoulder;
                case HumanBodyBones.RightShoulder: return HumanBodyBones.LeftShoulder;
                case HumanBodyBones.LeftUpperArm: return HumanBodyBones.RightUpperArm;
                case HumanBodyBones.RightUpperArm: return HumanBodyBones.LeftUpperArm;
                case HumanBodyBones.LeftLowerArm: return HumanBodyBones.RightLowerArm;
                case HumanBodyBones.RightLowerArm: return HumanBodyBones.LeftLowerArm;
                case HumanBodyBones.LeftHand: return HumanBodyBones.RightHand;
                case HumanBodyBones.RightHand: return HumanBodyBones.LeftHand;
                case HumanBodyBones.LeftToes: return HumanBodyBones.RightToes;
                case HumanBodyBones.RightToes: return HumanBodyBones.LeftToes;
                case HumanBodyBones.LeftEye: return HumanBodyBones.RightEye;
                case HumanBodyBones.RightEye: return HumanBodyBones.LeftEye;
                case HumanBodyBones.Jaw: return HumanBodyBones.Jaw;
                case HumanBodyBones.LeftThumbProximal: return HumanBodyBones.RightThumbProximal;
                case HumanBodyBones.LeftThumbIntermediate: return HumanBodyBones.RightThumbIntermediate;
                case HumanBodyBones.LeftThumbDistal: return HumanBodyBones.RightThumbDistal;
                case HumanBodyBones.LeftIndexProximal: return HumanBodyBones.RightIndexProximal;
                case HumanBodyBones.LeftIndexIntermediate: return HumanBodyBones.RightIndexIntermediate;
                case HumanBodyBones.LeftIndexDistal: return HumanBodyBones.RightIndexDistal;
                case HumanBodyBones.LeftMiddleProximal: return HumanBodyBones.RightMiddleProximal;
                case HumanBodyBones.LeftMiddleIntermediate: return HumanBodyBones.RightMiddleIntermediate;
                case HumanBodyBones.LeftMiddleDistal: return HumanBodyBones.RightMiddleDistal;
                case HumanBodyBones.LeftRingProximal: return HumanBodyBones.RightRingProximal;
                case HumanBodyBones.LeftRingIntermediate: return HumanBodyBones.RightRingIntermediate;
                case HumanBodyBones.LeftRingDistal: return HumanBodyBones.RightRingDistal;
                case HumanBodyBones.LeftLittleProximal: return HumanBodyBones.RightLittleProximal;
                case HumanBodyBones.LeftLittleIntermediate: return HumanBodyBones.RightLittleIntermediate;
                case HumanBodyBones.LeftLittleDistal: return HumanBodyBones.RightLittleDistal;
                case HumanBodyBones.RightThumbProximal: return HumanBodyBones.LeftThumbProximal;
                case HumanBodyBones.RightThumbIntermediate: return HumanBodyBones.LeftThumbIntermediate;
                case HumanBodyBones.RightThumbDistal: return HumanBodyBones.LeftThumbDistal;
                case HumanBodyBones.RightIndexProximal: return HumanBodyBones.LeftIndexProximal;
                case HumanBodyBones.RightIndexIntermediate: return HumanBodyBones.LeftIndexIntermediate;
                case HumanBodyBones.RightIndexDistal: return HumanBodyBones.LeftIndexDistal;
                case HumanBodyBones.RightMiddleProximal: return HumanBodyBones.LeftMiddleProximal;
                case HumanBodyBones.RightMiddleIntermediate: return HumanBodyBones.LeftMiddleIntermediate;
                case HumanBodyBones.RightMiddleDistal: return HumanBodyBones.LeftMiddleDistal;
                case HumanBodyBones.RightRingProximal: return HumanBodyBones.LeftRingProximal;
                case HumanBodyBones.RightRingIntermediate: return HumanBodyBones.LeftRingIntermediate;
                case HumanBodyBones.RightRingDistal: return HumanBodyBones.LeftRingDistal;
                case HumanBodyBones.RightLittleProximal: return HumanBodyBones.LeftLittleProximal;
                case HumanBodyBones.RightLittleIntermediate: return HumanBodyBones.LeftLittleIntermediate;
                case HumanBodyBones.RightLittleDistal: return HumanBodyBones.LeftLittleDistal;
                case HumanBodyBones.LastBone:
                default: return HumanBodyBones.LastBone;
            }
        }


        public void SetColor(Color color)
        {
            foreach (var bone in Bones) bone.GetComponent<Renderer>().material.color = color;


            foreach (var sphere in Spheres) sphere.GetComponent<Renderer>().material.color = color;
        }



        // Init skeleton display
        public void InitSkeleton(int personId)
        {
            Bones = new GameObject[currentBonesList.Length / 2];
            Spheres = new GameObject[currentSpheresList.Length];
            SphereOverlays = new JointOverlay[currentSpheresList.Length];
            skeleton = new GameObject();
            skeleton.name = "Skeleton_ID_" + personId;
            var width = 0.025f;


            var color = colors[personId % colors.Length];


            for (var i = 0; i < Bones.Length; i++)
            {
                var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cylinder.transform.parent = skeleton.transform;
                Bones[i] = cylinder;
            }


            for (var j = 0; j < Spheres.Length; j++)
            {
                var newOverlay = Instantiate(JointPrefab, Vector3.zero, Quaternion.identity, skeleton.transform);
                SphereOverlays[j] = newOverlay.GetComponent<JointOverlay>();


                SphereOverlays[j].SetName(currentSpheresNameList[j]);


                SphereOverlays[j].transform.localScale = new Vector3(width * 2, width * 2, width * 2);


                var sphere = SphereOverlays[j].GetSphere();
                sphere.name = currentSpheresList[j].ToString();


                Spheres[j] = sphere;
            }


            SetColor(color);


            PatientSelectorClickHandler.attach(skeleton, this);
        }


        private void UpdateJointColor(Color32 leftHeelColor, Color32 leftToeColor, Color32 rightHeelColor,
            Color32 rightToeColor)
        {
            var LeftFootIndex  = Array.IndexOf(currentSpheresList, BodyFormat.JointType_34_FootLeft);
            var LeftHeelIndex  = Array.IndexOf(currentSpheresList, BodyFormat.JointType_34_HeelLeft);
            var RightFootIndex = Array.IndexOf(currentSpheresList, BodyFormat.JointType_34_FootRight);
            var RightHeelIndex = Array.IndexOf(currentSpheresList, BodyFormat.JointType_34_HeelRight);


            if (LeftHeelIndex  != -1) SphereOverlays[LeftHeelIndex].SetColor(leftHeelColor);
            if (LeftFootIndex  != -1) SphereOverlays[LeftFootIndex].SetColor(leftToeColor);


            if (RightHeelIndex != -1) SphereOverlays[RightHeelIndex].SetColor(rightHeelColor);
            if (RightFootIndex != -1) SphereOverlays[RightFootIndex].SetColor(rightToeColor);
        }


        public void SetFeetPressure(float lHeelPressure, float lToePressure, float rHeelPressure, float rToePressure)
        {
            // ── FIX: was `!= null`, which caused an immediate early return whenever
            //         _feetColorer was assigned — preventing foot colour from ever updating.
            //         Corrected to `== null` so we only bail out when the component is missing.
            if (_feetColorer == null)
            {
                Debug.LogWarning("[FeetColorer] Component is null — foot pressure colours cannot be applied. " +
                                 "Ensure FeetColorer is present as a child of the humanoid prefab.");
                return;
            }


            var leftHeelColor =
                Color32.Lerp(MinPressureColor, MaxPressureColor, Mathf.Pow(lHeelPressure / 255, Powf));
            var leftToeColor =
                Color32.Lerp(MinPressureColor, MaxPressureColor, Mathf.Pow(lToePressure / 255, Powf));
            var rightHeelColor =
                Color32.Lerp(MinPressureColor, MaxPressureColor, Mathf.Pow(rHeelPressure / 255, Powf));
            var rightToeColor =
                Color32.Lerp(MinPressureColor, MaxPressureColor, Mathf.Pow(rToePressure / 255, Powf));


            UpdateJointColor(leftHeelColor, leftToeColor, rightHeelColor, rightToeColor);
            _feetColorer.UpdateColor(leftHeelColor, leftToeColor, rightHeelColor, rightToeColor);
        }


        public void SetFeetPressure(SensorSystemState st)
        {
            SetFeetPressure(st.leftFoot.Heel, st.leftFoot.Toe, st.rightFoot.Heel, st.rightFoot.Toe);
        }


        // Update skeleton display with new SDK data
        private void UpdateSkeleton()
        {
            var width = 0.025f;


            for (var j = 0; j < Spheres.Length; j++)
                if (ZEDSupportFunctions.IsVector3NaN(currentJoints[currentSpheresList[j]]))
                {
                    SphereOverlays[j].transform.position = Vector3.zero;
                    Spheres[j].SetActive(false);
                    SphereOverlays[j].SetVisibility(false);
                }
                else
                {
                    SphereOverlays[j].transform.position = currentJoints[currentSpheresList[j]];
                    Spheres[j].SetActive(true);
                }



            for (var i = 0; i < Bones.Length; i++)
            {
                var start = Spheres[Array.IndexOf(currentSpheresList, currentBonesList[2 * i])].transform.position;
                var end   = Spheres[Array.IndexOf(currentSpheresList, currentBonesList[2 * i + 1])].transform.position;


                if (start == Vector3.zero || end == Vector3.zero)
                {
                    Bones[i].SetActive(false);
                    continue;
                }


                Bones[i].SetActive(true);
                var offset   = end - start;
                var scale    = new Vector3(width, offset.magnitude / 2.0f, width);
                var position = start + offset / 2.0f;


                Bones[i].transform.position   = position;
                Bones[i].transform.up          = offset;
                Bones[i].transform.localScale  = scale;
            }
        }


        /// <summary>
        ///     Sets the avatar control with joint position.
        ///     Called on camera's OnObjectDetection event.
        ///     Updates the target rotations so that the 3D avatar can be correctly animated.
        /// </summary>
        /// <param name="jointsPosition">The keypoints position from the ZED SDK.</param>
        /// <param name="jointsRotation">The bones local orientations from the ZED SDK.</param>
        /// <param name="rootRotation">The global root orientation from the ZED SDK.</param>
        /// <param name="useAvatar">If the 3D avatar should be displayed (and if the corresponding data should be updated).</param>
        /// <param name="_mirrorOnYAxis">Mirror the 3D avatars or not.</param>
        public void SetControlWithJointPosition(Vector3[] jointsPosition, Quaternion[] jointsRotation,
            Quaternion rootRotation, bool useAvatar, bool _mirrorOnYAxis)
        {
            if (jointsPosition.Length != currentKeypointsCount) return;
            usingAvatar = useAvatar;


            currentJoints = jointsPosition;


            humanoid.SetActive(useAvatar);
#if UNITY_STANDALONE
            skeleton.SetActive(!useAvatar || CustomZedController.DisplayDebugSkeleton);
#else
            skeleton.SetActive(!useAvatar);
#endif


            if (useAvatar)
            {
                SetHumanPoseControl(jointsPosition[0], rootRotation, jointsRotation, _mirrorOnYAxis);
#if UNITY_STANDALONE
                if (CustomZedController.DisplayDebugSkeleton) UpdateSkeleton();
#endif
            }
            else
            {
                UpdateSkeleton();
                SetJointOverlays(jointsRotation);
            }


            //zedSkeletonAnimator.PoseWasUpdatedIK();
        }


        /// <summary>
        ///     Utility function to apply the rest pose to the bones.
        /// </summary>
        private void PropagateRestPoseRotations(int parentIdx, Dictionary<HumanBodyBones, RigBone> outPose,
            Quaternion restPosRot, bool inverse)
        {
            for (var i = 0; i < currentHumanBodyBones.Length; i++)
                if (currentHumanBodyBones[i] != HumanBodyBones.LastBone && outPose[currentHumanBodyBones[i]].transform)
                {
                    var outPoseTransform = outPose[currentHumanBodyBones[i]].transform;


                    if (currentParentIds[i] == parentIdx)
                    {
                        var restPoseRotation = m_DefaultRotations[currentHumanBodyBones[i]];
                        var restPoseRotChild = new Quaternion();


                        if (currentParentIds[i] != -1)
                        {
                            var jointRotation = restPosRot * outPoseTransform.localRotation;
                            outPoseTransform.localRotation = jointRotation;


                            if (!inverse)
                                restPoseRotChild = restPosRot * restPoseRotation;
                            else
                                restPoseRotChild = Quaternion.Inverse(restPoseRotation) * restPosRot;
                        }
                        else
                        {
                            restPoseRotChild = restPosRot;
                        }


                        PropagateRestPoseRotations(i, outPose, restPoseRotChild, inverse);
                    }
                }
        }



        /// <summary>
        ///     Sets 3D avatar position, and the bones rotations. Called in Update().
        ///     This method does not use the animator, and instead directly sets the rotations of the bones transforms.
        /// </summary>
        private void MoveAvatar()
        {
            // Put in Ref Pose
            foreach (var bone in currentHumanBodyBones)
                if (bone != HumanBodyBones.LastBone)
                    if (rigBone[bone].transform)
                        rigBone[bone].transform.localRotation = m_DefaultRotations[bone];


            PropagateRestPoseRotations(0, rigBone, m_DefaultRotations[0], false);


            for (var i = 0; i < currentHumanBodyBones.Length; i++)
                if (currentHumanBodyBones[i] != HumanBodyBones.LastBone && rigBone[currentHumanBodyBones[i]].transform)
                    if (currentParentIds[i] != -1)
                    {
                        var newRotation = RigBoneTarget[currentHumanBodyBones[i]] *
                                          rigBone[currentHumanBodyBones[i]].transform.localRotation;
                        rigBone[currentHumanBodyBones[i]].transform.localRotation = newRotation;
                    }


            PropagateRestPoseRotations(0, rigBone, Quaternion.Inverse(m_DefaultRotations[0]), true);


            // Reposition root depending on hips position.
            if (rigBone[HumanBodyBones.Hips].transform)
            {
                TargetBodyPositionWithHipOffset = targetBodyPosition;
                rigBone[HumanBodyBones.Hips].transform
                    .SetPositionAndRotation(TargetBodyPositionWithHipOffset, TargetBodyOrientation);
            }
        }



        /// <summary>
        ///     Update the "currentXXX" values depending on the active BODY_FORMAT
        /// </summary>
        /// <param name="pBodyFormat"></param>
        private void UpdateCurrentValues(BODY_FORMAT pBodyFormat)
        {
            switch (pBodyFormat)
            {
                case BODY_FORMAT.BODY_34:
                    currentConfidences    = confidences34;
                    currentJoints         = joints34;
                    currentHumanBodyBones = BodyFormat.humanBones34;
                    currentSpheresList    = BodyFormat.sphereList34;
                    currentSpheresNameList = BodyFormat.sphereNameList34;
                    currentBonesList      = BodyFormat.bonesList34;
                    currentParentIds      = BodyFormat.parentsIdx_34;
                    currentLeftAnkleIndex  = BodyFormat.JointType_34_AnkleLeft;
                    currentRightAnkleIndex = BodyFormat.JointType_34_AnkleRight;
                    currentKeypointsCount  = BodyFormat.JointType_34_COUNT;
                    break;


                case BODY_FORMAT.BODY_38:
                    currentConfidences    = confidences38;
                    currentJoints         = joints38;
                    currentHumanBodyBones = BodyFormat.humanBones38;
                    currentSpheresList    = BodyFormat.sphereList38;
                    currentSpheresNameList = BodyFormat.sphereNameList38;
                    currentBonesList      = BodyFormat.bonesList38;
                    currentParentIds      = BodyFormat.parentsIdx_38;
                    currentLeftAnkleIndex  = BodyFormat.JointType_LEFT_ANKLE;
                    currentRightAnkleIndex = BodyFormat.JointType_RIGHT_ANKLE;
                    currentKeypointsCount  = BodyFormat.JointType_38_COUNT;
                    break;

                default:
                    Debug.LogError("Error: Invalid BODY_MODEL! Please use either BODY_34 or BODY_38.");
#if UNITY_EDITOR
                    EditorApplication.ExitPlaymode();
#else
                    Application.Quit();
#endif
                    break;
            }
        }



        public void AdjustFeetOffset(float alpha)
        {
            if (animator.GetBoneTransform(HumanBodyBones.LeftToes) &&
                animator.GetBoneTransform(HumanBodyBones.RightToes))
            {
                var leftFootHeight  = animator.GetBoneTransform(HumanBodyBones.LeftToes).position.y;
                var rightFootHeight = animator.GetBoneTransform(HumanBodyBones.RightToes).position.y;
                FeetOffset = alpha * Mathf.Min(leftFootHeight, rightFootHeight) + (1 - alpha) * FeetOffset;
            }
        }



        /// <summary>
        ///     Update Engine function (move this avatar)
        /// </summary>
        public void Move()
        {
            MoveAvatar();
        }



        public string GetJointTypeName(int jointId)
        {
            return currentBodyFormat.GetJointTypeName(jointId);
        }


        #region const_variables


        // List of available colors for Skeletons
        private readonly Color[] colors =
        {
            new(232.0f / 255.0f, 176.0f / 255.0f, 59.0f / 255.0f),
            new(175.0f / 255.0f, 208.0f / 255.0f, 25.0f / 255.0f),
            new(102.0f / 255.0f / 255.0f, 205.0f / 255.0f, 105.0f / 255.0f),
            new(185.0f / 255.0f, 0.0f / 255.0f, 255.0f / 255.0f),
            new(99.0f / 255.0f, 107.0f / 255.0f, 252.0f / 255.0f),
            new(252.0f / 255.0f, 225.0f / 255.0f, 8.0f / 255.0f),
            new(167.0f / 255.0f, 130.0f / 255.0f, 141.0f / 255.0f),
            new(194.0f / 255.0f, 72.0f / 255.0f, 113.0f / 255.0f)
        };


        #endregion
    }


    public static class TransformExtensions
    {
        public static Vector3 mirror_x(this Vector3 input)
        {
            input.x *= -1f;
            return input;
        }


        public static Quaternion mirror_x(this Quaternion input)
        {
            input.x *= -1f;
            input.w *= -1f;
            return input;
        }
    }
}
