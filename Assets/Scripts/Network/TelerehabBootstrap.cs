using Assets.Scripts.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Network
{
    /// <summary>
    /// Attach this to the first scene (main menu or a dedicated Bootstrap scene).
    /// It starts the WebSocket server and waits for Flutter to send commands.
    ///
    /// Flutter "Launch & Connect" flow:
    ///   1. Flutter launches TeleHealth.exe
    ///   2. Unity starts → this script runs → server starts on ws://localhost:8765
    ///   3. Flutter connects via WebSocket
    ///   4. Flutter sends {"type":"command","action":"load_scene","scene":"Replay"}
    ///   5. Unity loads the Replay scene
    ///
    /// Setup: Add this component alongside TelerehabWebSocketServer +
    ///        TelerehabDispatcher on the same GameObject in your first scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class TelerehabBootstrap : MonoBehaviour
    {
        [Header("Scene Names (must match Build Settings)")]
        public string replaySceneName   = "Replay";
        public string exerciseSceneName = "FreeMode";
        public string mainMenuSceneName = "MainMenu";

        [Header("Behaviour")]
        [Tooltip("Hide the Unity window while waiting for Flutter commands")]
        public bool startMinimized = false;

        private void Start()
        {
            // Subscribe to commands from Flutter
            TelerehabCommandBus.OnCommand += OnCommand;

            if (startMinimized)
                MinimizeWindow();

            // Broadcast a "ready" heartbeat so Flutter knows server is up
            InvokeRepeating(nameof(SendHeartbeat), 1f, 5f);
        }

        private void OnDestroy()
        {
            TelerehabCommandBus.OnCommand -= OnCommand;
            CancelInvoke(nameof(SendHeartbeat));
        }

        private void OnCommand(TelerehabCommand cmd, string payload)
        {
            switch (cmd)
            {
                case TelerehabCommand.StartExercise:
                    LoadScene(replaySceneName);
                    RestoreWindow();
                    break;
            }

            // Also handle raw scene-load JSON: {"action":"load_scene","scene":"Replay"}
            if (payload != null && payload.Contains("\"load_scene\""))
            {
                string scene = ExtractSceneName(payload);
                if (!string.IsNullOrEmpty(scene))
                {
                    LoadScene(scene);
                    RestoreWindow();
                }
            }
        }

        private void LoadScene(string sceneName)
        {
            Debug.Log($"[Bootstrap] Loading scene: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }

        private void SendHeartbeat()
        {
            if (TelerehabWebSocketServer.Instance == null) return;
            TelerehabWebSocketServer.Instance.Broadcast(
                "{\"type\":\"heartbeat\",\"status\":\"ready\"}");
        }

        private static string ExtractSceneName(string json)
        {
            // Simple extraction: find "scene":"VALUE"
            const string key = "\"scene\":\"";
            int start = json.IndexOf(key, System.StringComparison.Ordinal);
            if (start < 0) return null;
            start += key.Length;
            int end = json.IndexOf('"', start);
            return end > start ? json.Substring(start, end - start) : null;
        }

        private static void MinimizeWindow()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (hwnd != System.IntPtr.Zero)
                ShowWindow(hwnd, 2); // SW_MINIMIZE
#endif
        }

        private static void RestoreWindow()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (hwnd != System.IntPtr.Zero)
            {
                ShowWindow(hwnd, 9); // SW_RESTORE
                SetForegroundWindow(hwnd);
            }
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(System.IntPtr hWnd);
#endif
    }
}
