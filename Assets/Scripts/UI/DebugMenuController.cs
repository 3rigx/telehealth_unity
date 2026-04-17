using Assets.Scripts.Sensors;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;

namespace Assets.Scripts.UI
{
    public class DebugMenuController : MonoBehaviour
    {
        // Start is called before the first frame update

        private SensorSystemState _sensorSystemState;
        public TextMeshProUGUI IDText, TimeText, SkeletonText;
        public TextMeshProUGUI LeftToeText, LeftHeelText, RightToeText, RightHeelText;

        private void Start()
        {
            Assert.IsNotNull(LeftToeText);
            Assert.IsNotNull(LeftHeelText);
            Assert.IsNotNull(RightToeText);
            Assert.IsNotNull(RightHeelText);
            Assert.IsNotNull(IDText);
            Assert.IsNotNull(TimeText);
        }

        public void SetExerciseState(SensorSystemState sensorSystemState)
        {
            _sensorSystemState = sensorSystemState;
            LeftToeText.text = _sensorSystemState.leftFoot.Toe.ToString();
            LeftHeelText.text = _sensorSystemState.leftFoot.Heel.ToString();
            RightToeText.text = _sensorSystemState.rightFoot.Toe.ToString();
            RightHeelText.text = _sensorSystemState.rightFoot.Heel.ToString();
            IDText.text = _sensorSystemState.id.ToString();
            TimeText.text = _sensorSystemState.timestamp.DateTime.ToString("HH:mm");
            SkeletonText.text = _sensorSystemState.skeleton != null ? "Set" : "None";
        }
    }
}