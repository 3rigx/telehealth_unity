using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.UI
{
    public class MainMenuController : MonoBehaviour, MenuScreenController
    {
#nullable enable
        public PopupController? errorController;
#nullable disable
        private bool hasZed;



        private void Start()
        {
            hasZed = Application.CanStreamedLevelBeLoaded("CompleteExercise");
        }

        // Update is called once per frame
        private void Update()
        {
        }

        public void StartExercise()
        {
            if (hasZed)
            {
                SceneManager.LoadScene("CompleteExercise", LoadSceneMode.Single);
            }
            else if (errorController != null)
            {
                errorController.SetText("Tried to start exercise without ZED libraries");
                errorController.Show();
            }
        }

        public void StartReplay()
        {
            SceneManager.LoadScene("Replay", LoadSceneMode.Single);
        }

        public void StartFreeMode()
        {
            if (hasZed)
            {
                SceneManager.LoadScene("FreeMode", LoadSceneMode.Single);
            }
            else if (errorController != null)
            {
                errorController.SetText("Tried to start exercise without ZED libraries");
                errorController.Show();
            }
        }

        public void SettingsButton()
        {
        }

        public void MainMenuButton()
        {
            SceneManager.LoadScene("Menu", LoadSceneMode.Single);
        }

        public void WebsiteButton()
        {
            Application.OpenURL(PlayerPrefs.GetString("FSRUri", "http://localhost:3000"));
        }

        public void ExitButton()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}