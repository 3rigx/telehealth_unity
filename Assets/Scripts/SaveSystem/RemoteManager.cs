using Assets.Scripts.Exercises;
using System;
using System.Collections;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.Scripts.SaveSystem
{
    internal class RemoteManager
    {
        private static readonly object Oplock = new();
        private static Uri _host;
        private static string _error;
        private static Exercise _mExercise;


        public string Error
        {
            get
            {
                lock (Oplock)
                {
                    return _error;
                }
            }
        }

        public static Exercise Exercise
        {
            get
            {
                lock (Oplock)
                {
                    return _mExercise;
                }
            }
        }

        public static int ProgressPercent { get; private set; }

        public static bool IsDone { get; private set; }

        public static void SetHost(Uri host)
        {
            RemoteManager._host = host;
        }

        public static IEnumerator SaveRemoteCoroutine([CanBeNull] string datafile, [CanBeNull] string videofile)
        {
            if (datafile == null && videofile == null)
            {
                Debug.LogError("No files to save");
                yield break;
            }
            lock (Oplock)
            {
                IsDone = false;
                ProgressPercent = 0;

                _mExercise = SaveManager.LoadExercise(datafile);
                var id = _mExercise.GetMetadata().id;

                var apikey = PlayerPrefs.GetString("ApiKey");
                if (apikey == null)
                {
                    Debug.LogError("Api Key not set");
                    yield break;
                }

                var builder = new UriBuilder(_host);
                builder.Query = $"key={apikey}&platform=unity";

                builder.Path = $"/api/exercise/instance/{id}/data";

                var form = new WWWForm();
                if(datafile != null) form.AddBinaryData("datafile", FileManager.LoadFromFileBinary(datafile));
                if(videofile != null) form.AddBinaryData("videofile", FileManager.LoadFromFileBinary(videofile));

                using (var req = UnityWebRequest.Post(builder.Uri, form))
                {
                    req.SendWebRequest();
                    while (req.isDone)
                    {
                        ProgressPercent = (int)(req.uploadProgress * 100);
                        yield return new WaitForSecondsRealtime(0.05f);
                    }

                    switch (req.result)
                    {
                        case UnityWebRequest.Result.ConnectionError:
                            _error = "Connection Error";
                            break;
                        case UnityWebRequest.Result.DataProcessingError:
                            _error = "Data Processing Error";
                            break;
                        case UnityWebRequest.Result.ProtocolError:
                            switch (req.responseCode)
                            {
                                case 401:
                                    _error = "Unauthorized";
                                    break;
                                case 403:
                                    _error = "Forbidden";
                                    break;
                                case 404:
                                    _error = "Exercise not found";
                                    break;
                            }

                            break;
                        case UnityWebRequest.Result.Success:
                            FileManager.DeleteFile(datafile);
                            FileManager.DeleteFile(videofile);
                            _error = "";
                            break;
                    }
                }

                IsDone = true;
            }
        }

        public static IEnumerator LoadRemoteCoroutine(int id)
        {
            lock (Oplock)
            {
                IsDone = false;
                var apikey = PlayerPrefs.GetString("ApiKey");
                if (apikey == null)
                {
                    Debug.LogError("Api Key not set");
                    yield break;
                }

                var builder = new UriBuilder(_host);
                builder.Path = $"/api/exercise/instance/{id}/data";
                builder.Query = $"key={apikey}&platform=unity";

                using (var req = new UnityWebRequest(builder.Uri, UnityWebRequest.kHttpVerbGET))
                {
                    var fileloc = Application.persistentDataPath + $"/{id}_instruction.json";
                    req.downloadHandler = new DownloadHandlerFile(fileloc);

                    yield return req.SendWebRequest();
                    switch (req.result)
                    {
                        case UnityWebRequest.Result.ConnectionError:
                            _error = "Connection Error";
                            break;
                        case UnityWebRequest.Result.DataProcessingError:
                            _error = "Data Processing Error";
                            break;
                        case UnityWebRequest.Result.ProtocolError:
                            switch (req.responseCode)
                            {
                                case 401:
                                    _error = "Unauthorized";
                                    break;
                                case 403:
                                    _error = "Forbidden";
                                    break;
                                case 404:
                                    _error = "Exercise not found";
                                    break;
                            }

                            break;
                        case UnityWebRequest.Result.Success:
                            _mExercise = SaveManager.LoadExercise(fileloc);
                            FileManager.DeleteFile(fileloc);
                            _error = "";
                            break;
                    }
                }

                IsDone = true;
            }
        }
    }
}