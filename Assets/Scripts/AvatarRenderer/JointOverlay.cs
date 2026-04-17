using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.AvatarRenderer
{
    public class JointOverlay : MonoBehaviour, IPointerClickHandler
    {
        public Canvas canvas;
        private bool isVisible;
        public TextMeshProUGUI jointName, roll, pitch, yaw;
        public GameObject sphere;
        public Camera viewCamera;

        public void OnPointerClick(PointerEventData eventData)
        {
            ToggleVisibility();
        }


        public void SetName(string newname)
        {
            jointName.text = newname;
        }

        public void SetRotationDisplay(Quaternion rotation)
        {
            roll.text = rotation.eulerAngles.x.ToString("F2");
            pitch.text = rotation.eulerAngles.y.ToString("F2");
            yaw.text = rotation.eulerAngles.z.ToString("F2");
        }

        public void SetVisibility(bool visibility)
        {
            canvas.enabled = visibility;
            isVisible = visibility;
        }

        public void ToggleVisibility()
        {
            SetVisibility(!isVisible);
        }

        public void SetColor(Color32 color)
        {
            sphere.GetComponent<Renderer>().material.color = color;
        }

        public void Start()
        {
            viewCamera = FindObjectsByType<CameraProvider>()[0].viewCamera;
            canvas.worldCamera = viewCamera;
            SetVisibility(false);
        }

        public GameObject GetSphere()
        {
            return sphere;
        }

        public void LateUpdate()
        {
            transform.LookAt(transform.position + viewCamera.transform.rotation * Vector3.forward,
                               viewCamera.transform.rotation * Vector3.up);
        }
    }
}