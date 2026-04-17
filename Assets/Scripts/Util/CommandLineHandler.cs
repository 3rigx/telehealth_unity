using System;
using System.Collections;
using System.Linq;
using Assets.Scripts.Exercises;
using Assets.Scripts.SaveSystem;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Util
{
    internal class CommandLineHandler : MonoBehaviour
    {
#if UNITY_EDITOR
        public string commandLine;
#endif

#nullable enable
        public PopupController? ErrorPopup;
#nullable disable

        private IEnumerator UpgradeRemoteCoroutine(int id)
        {
            RemoteManager.SetHost(new Uri(PlayerPrefs.GetString("host", "http://localhost:3000/")));
            yield return RemoteManager.LoadRemoteCoroutine(id);
            ExerciseProvider.isRemote = true;
            if (RemoteManager.Exercise == null)
            {
                Debug.LogError("Destination unreachable");
                if (ErrorPopup != null) ErrorPopup.Show("Destination unreachable");
                yield break;
            }
            ExerciseProvider.exercise = RemoteManager.Exercise;
            string upgradedFilePath = Application.persistentDataPath + $"/{id}_data.json";
            SaveManager.Save(upgradedFilePath,StatisticsController.CalcExercise(RemoteManager.Exercise));
            StartCoroutine(RemoteManager.SaveRemoteCoroutine(upgradedFilePath, null));
            FileManager.DeleteFile(upgradedFilePath);
        }



        private IEnumerator StartWithRemoteCoroutine(int id)
        {
            RemoteManager.SetHost(new Uri(PlayerPrefs.GetString("host", "http://localhost:3000/")));
            yield return RemoteManager.LoadRemoteCoroutine(id);
            ExerciseProvider.isRemote = true;
            if (RemoteManager.Exercise == null)
            {
                Debug.LogError("Destination unreachable");
                if(ErrorPopup != null) ErrorPopup.Show("Destination unreachable");
                yield break;
            }
            ExerciseProvider.exercise = RemoteManager.Exercise;
            Debug.Log("Loaded exercise " + id);
            Debug.Log("Exercise in provider: " + ExerciseProvider.exercise.GetMetadata().id);
            SceneManager.LoadScene("CompleteExercise");
        }

        private void Start()
        {
#if UNITY_EDITOR
            var args = commandLine.Split(' ');
#else
            var args = Environment.GetCommandLineArgs();
#endif
            if (args.Length < 1 || args[0] == "")
            {
                Debug.Log("No args");
                return;
            }

            UriBuilder prompt = new(args[0]);
            PlayerPrefs.SetString("", prompt.Host + ":" + prompt.Port);

            Debug.Log(prompt.Path);
            

            var dict = prompt.Query.Remove(0, 1).Split('&').Select(x => x.Split('='))
                .ToDictionary(x => x[0], x => x[1]);
            if (dict.ContainsKey("key")) PlayerPrefs.SetString("ApiKey", dict["key"]);

            int exerciseID = int.Parse(prompt.Path.Remove(0, 1));

            if (dict.ContainsKey("upgrade"))
            {
                StartCoroutine(UpgradeRemoteCoroutine(exerciseID));
                return;
            }
            
            StartCoroutine(StartWithRemoteCoroutine(exerciseID));
        }
    }
}