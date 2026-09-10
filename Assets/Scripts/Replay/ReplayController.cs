using System;
using System.Collections;
using System.Linq;
using Assets.Scripts.AvatarRenderer;
using Assets.Scripts.Exercises;
using Assets.Scripts.SaveSystem;
using Assets.Scripts.Sensors.Zed;
using Assets.Scripts.UI;
using Assets.Scripts.UI.Chart;
using Assets.Scripts.UI.Chart.MyChart;
using Assets.Scripts.Util;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

using CustomSkeletonHandler = Assets.Scripts.AvatarRenderer.CustomSkeletonHandler;

namespace Assets.Scripts.Replay
{
    /// <summary>
    ///     Replay controller responsible for executing an exercise replay
    /// </summary>
    [DisallowMultipleComponent]
    public class ReplayController : MonoBehaviour
    {
        private readonly float _camRotSpeed = 10f;
        private readonly bool _mirrorY = false;

        public bool AutoStart = false;


        /// <summary>
        ///     Avatar game object
        /// </summary>
        [Tooltip("3D Rigged model.")] public GameObject Avatar;

        private Vector3 _cameraOffset = Vector3.zero;
        private Quaternion _cameraRotationOffset = Quaternion.identity;
        private Vector3 _cameraUserOffset = new(0, 1, -1);
        private Quaternion _cameraUserRotationOffset = Quaternion.identity;

        public float CamSpeed = 0.5f;


        private int _cursor;

        private IExercise _exercise;
        private bool _isPlaying;
        [Tooltip("Joint Prefab.")] public GameObject Joint;
        public JointChart JointChart;
        public Slider PositionSlider;
        public TextMeshProUGUI ScoreDisplay;
        private CustomSkeletonHandler _skeletonHandler;
        private Vector3 _skeletonOffset = new(0, 1.25f, 0);
        public float SkeletonSpeed = 0.5f;
        private float _speed;

        private bool _useAvatar = true;
        public Camera ViewCamera;

        public SkeletonChartController SkeletonChartController;

        [Header("Angle Visualization")]
        public AngleVisualizer AngleVisualizer;
        public AngleChartController AngleChartController;
        public bool EnableAngleVisualization = true;
        public bool UseFirstFrameAsRestPosition = true;

        private Vector3[] _restJointPositions;
        private JointAngleCalculator.JointAngle[] _currentAngles;

        private void Refresh()
        {
            PositionSlider.value = _cursor;
            var currState = _exercise.GetSensorState();
            if (DebugMenu != null) DebugMenu.SetExerciseState(currState);
            if (SkeletonChartController != null) SkeletonChartController.addState(currState);
            try
            {
                if (ScoreDisplay != null) ScoreDisplay.text = _exercise.Score().ToString();
            }
            catch (NotImplementedException)
            {
            }

            if (currState.skeleton != null)
            {
                var sk = currState.skeleton;
                if (sk.JointPos.Length == _skeletonHandler.currentJoints.Length)
                {
                    if (JointChart != null) JointChart.AddState(sk);
                    _skeletonHandler.AdjustFeetOffset(sk.FeetOffset);

                    // Calculate and visualize joint angles
                    if (EnableAngleVisualization)
                    {
                        CalculateAndUpdateAngles(sk);
                    }

                    _skeletonHandler.SetControlWithJointPosition(
                        _skeletonOffset == Vector3.zero
                            ? sk.JointPos
                            : sk.JointPos.Select(joint => joint + _skeletonOffset).ToArray(), sk.JointRot, sk.RootRot,
                        _useAvatar, _mirrorY);
                    _skeletonHandler.SetFeetPressure(currState);
                    _cameraOffset = sk.cameraPos;
                    _cameraRotationOffset = sk.cameraRot;
                    if (_useAvatar) _skeletonHandler.Move();

                    // Mirror the replay frame to any connected Flutter dashboard.
                    Network.TelerehabBroadcaster.BroadcastState(currState, _currentAngles,
                        cameraConnected: true, pressureConnected: true);
                }
            }

            if (ScoreDisplay == null) return;

            try
            {
                ScoreDisplay.text = "Score: " + _exercise.Score();
            }
            catch (NotImplementedException)
            {
                ScoreDisplay.text = "";
            }
        }

        public void JumpToPos(int pos)
        {
            if (pos < 0 || pos >= _exercise.StateLength())
            {
                StopCoroutine(AdvanceCoroutine());
                _isPlaying = false;
                return;
            }

            //if (pos != cursor + 1 && jointChart != null) jointChart.Clear();
            _cursor = pos;
            _exercise.SetCursor(pos);
            Refresh();
        }

        public void JumpToPos(float pos)
        {
            JumpToPos((int)pos);
        }

        /// <summary>
        /// Calculate joint angles and update visualizer
        /// </summary>
        private void CalculateAndUpdateAngles(SkeletonState sk)
        {
            // Initialize rest positions if not set yet
            if (_restJointPositions == null && UseFirstFrameAsRestPosition)
            {
                _restJointPositions = (Vector3[])sk.JointPos.Clone();
            }

            // Calculate current angles
            Vector3[] currentPositions = _skeletonOffset == Vector3.zero 
                ? sk.JointPos 
                : sk.JointPos.Select(joint => joint + _skeletonOffset).ToArray();

            _currentAngles = JointAngleCalculator.CalculateAllJointAngles(
                currentPositions, 
                sk.Format, 
                _restJointPositions);

            // Update visualizer
            if (AngleVisualizer != null)
                AngleVisualizer.UpdateAngleDisplays(_currentAngles);

            // Feed into chart
            AngleChartController?.AddAngleState(_currentAngles);
        }

        /// <summary>
        /// Toggle angle visualization on/off
        /// </summary>
        public void ToggleAngleVisualization()
        {
            EnableAngleVisualization = !EnableAngleVisualization;
            if (AngleVisualizer != null)
            {
                if (!EnableAngleVisualization)
                {
                    AngleVisualizer.ClearAllDisplays();
                }
            }
        }

        /// <summary>
        /// Set current joint positions as rest position
        /// </summary>
        public void SetCurrentAsRestPosition()
        {
            var currState = _exercise.GetSensorState();
            if (currState.skeleton != null)
            {
                _restJointPositions = (Vector3[])currState.skeleton.JointPos.Clone();
                Debug.Log("Rest position updated to current frame");
            }
        }

        /// <summary>
        /// Reset rest position to default values
        /// </summary>
        public void ResetRestPosition()
        {
            _restJointPositions = null;
            Debug.Log("Rest position reset to default");
        }

        /// <summary>
        /// Configure which angles to display
        /// </summary>
        public void ConfigureAngleDisplay(bool leftElbow = true, bool rightElbow = true, 
            bool leftKnee = true, bool rightKnee = true, bool leftShoulder = true, 
            bool rightShoulder = true, bool leftHip = true, bool rightHip = true)
        {
            if (AngleVisualizer != null)
            {
                AngleVisualizer.showLeftElbow = leftElbow;
                AngleVisualizer.showRightElbow = rightElbow;
                AngleVisualizer.showLeftKnee = leftKnee;
                AngleVisualizer.showRightKnee = rightKnee;
                AngleVisualizer.showLeftShoulder = leftShoulder;
                AngleVisualizer.showRightShoulder = rightShoulder;
                AngleVisualizer.showLeftHip = leftHip;
                AngleVisualizer.showRightHip = rightHip;
                
                Debug.Log("Angle display configuration updated");
            }
        }

        /// <summary>
        /// Get current angle values for external access
        /// </summary>
        public JointAngleCalculator.JointAngle[] GetCurrentAngles()
        {
            return _currentAngles;
        }

        public IEnumerator AdvanceCoroutine()
        {
            while (_cursor < _exercise.StateLength())
            {
                Advance();
                yield return new WaitForSecondsRealtime(_speed);
            }
        }

        public void DelayedStart()
        {
            if (StartButton != null) StartButton.SetActive(false);
            if (ExerciseProvider.isRemote)
            {
                _exercise = ExerciseProvider.exercise;
            }
            else
            {
                var save = StandaloneFileBrowser.StandaloneFileBrowser.OpenFilePanel(
                    "Open session.json (or legacy save)", "", "json", false);
                if (save.Length == 0) return;

                _exercise = SessionLoader.LoadForReplay(save[0]);
            }

            if (_exercise == null)
            {
                Debug.LogError("Failed to instantiate exercise!");
                return;
            }

            Debug.Log(_exercise.GetType().Name);


            if (_exercise is Exercise exercise1)
                _speed = exercise1.GetMetadata().measurementInterval;
            else
                _speed = 1.0f;

            _skeletonHandler = ScriptableObject.CreateInstance<CustomSkeletonHandler>();

            _skeletonHandler.Create(Avatar, Joint);
            _skeletonHandler.InitSkeleton(1);
            _skeletonHandler.SetPatient();
            _cursor = 0;

            PositionSlider.maxValue = _exercise.StateLength();
            Refresh();
        }

        private void Start()
        {
            Assert.IsNotNull(ViewCamera);
            Assert.IsNotNull(PositionSlider);
            Assert.IsNotNull(Avatar);

            if (AutoStart) DelayedStart();
        }

        private void OnDestroy()
        {
            StopCoroutine(AdvanceCoroutine());
            AngleVisualizer?.ClearAllDisplays();
            AngleChartController?.Clear();
        }

        public void Play()
        {
            if (_isPlaying) return;

            StartCoroutine(AdvanceCoroutine());
            _isPlaying = true;
        }

        public void Pause()
        {
            if (!_isPlaying) return;
            StopCoroutine(AdvanceCoroutine());
            _isPlaying = false;
        }

        public void PlayPause()
        {
            if (_isPlaying) Pause();
            else Play();
        }

        public void Advance()
        {
            if (_cursor < 0 || _cursor >= _exercise.StateLength())
            {
                StopCoroutine(AdvanceCoroutine());
                _isPlaying = false;
                return;
            }

            ++_cursor;
            _exercise.Advance();
            Refresh();
        }

        public void Back()
        {
            JumpToPos(--_cursor);
        }

        public void JumpBack(int amount = 10)
        {
            JumpToPos(_cursor - amount);
        }

        public void JumpForward(int amount = 10)
        {
            while (amount-- > 0) _exercise.Advance();
            //JumpToPos(cursor + amount);
            Refresh();
        }


        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _useAvatar = !_useAvatar;
                Refresh();
                if (_useAvatar) _skeletonHandler.Move();
                Debug.Log("Avatar switch!");
            }

            if (Input.GetKeyDown(KeyCode.LeftAlt))
            {
                _cameraUserRotationOffset = Quaternion.identity;
                _cameraUserOffset = new Vector3(0, 1, -1);
                _skeletonOffset = new Vector3(0, 1.25f, 0);
            }

            // Angle visualization controls
            if (Input.GetKeyDown(KeyCode.A))
            {
                ToggleAngleVisualization();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                SetCurrentAsRestPosition();
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                ResetRestPosition();
            }

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                var mouseDelta = new Vector2(Input.GetAxis("Mouse X"), -Input.GetAxis("Mouse Y"));
                var camRotDelta = Quaternion.AngleAxis(mouseDelta.x * _camRotSpeed, Vector3.up) *
                                  Quaternion.AngleAxis(mouseDelta.y * _camRotSpeed, Vector3.right);
                _cameraUserRotationOffset *= camRotDelta;
            }


            var moveVec = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) moveVec += ViewCamera.transform.forward;
            if (Input.GetKey(KeyCode.S)) moveVec -= ViewCamera.transform.forward;
            if (Input.GetKey(KeyCode.A)) moveVec -= ViewCamera.transform.right;
            if (Input.GetKey(KeyCode.D)) moveVec += ViewCamera.transform.right;
            if (Input.GetKey(KeyCode.Q)) moveVec -= ViewCamera.transform.up;
            if (Input.GetKey(KeyCode.E)) moveVec += ViewCamera.transform.up;
            if (moveVec != Vector3.zero)
            {
                _cameraUserOffset += moveVec * (CamSpeed * Time.deltaTime);
                _cameraUserOffset.y = Mathf.Max(_cameraUserOffset.y, 1);
            }

            moveVec = Vector3.zero;

            if (Input.GetKey(KeyCode.UpArrow)) moveVec += Vector3.forward;
            if (Input.GetKey(KeyCode.DownArrow)) moveVec += Vector3.back;
            if (Input.GetKey(KeyCode.LeftArrow)) moveVec += Vector3.left;
            if (Input.GetKey(KeyCode.RightArrow)) moveVec += Vector3.right;
            if (Input.GetKey(KeyCode.PageUp)) moveVec += Vector3.up;
            if (Input.GetKey(KeyCode.PageDown)) moveVec += Vector3.down;
            if (moveVec != Vector3.zero)
            {
                _skeletonOffset += moveVec * (SkeletonSpeed * Time.deltaTime);
                Refresh();
            }

            ViewCamera.transform.SetLocalPositionAndRotation(_cameraUserOffset + _cameraOffset,
                _cameraUserRotationOffset * _cameraRotationOffset);
        }

#nullable enable
        public DebugMenuController? DebugMenu;
        public GameObject? StartButton;
#nullable disable
    }
}