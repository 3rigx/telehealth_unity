using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Assets.Scripts.Network
{
    /// <summary>
    /// Dispatches actions from background Task threads onto the Unity main thread.
    /// Must be attached to a GameObject in the scene alongside TelerehabWebSocketServer.
    /// </summary>
    public class TelerehabDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> _queue = new();

        private void Awake()
        {
            // Only one dispatcher needed across scenes
            if (FindObjectsByType<TelerehabDispatcher>(FindObjectsSortMode.None).Length > 1)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);
        }

        public static void Enqueue(Action action) => _queue.Enqueue(action);

        private void Update()
        {
            while (_queue.TryDequeue(out var action))
            {
                try   { action?.Invoke(); }
                catch (Exception e) { Debug.LogError(e); }
            }
        }
    }
}
