using System;
using UnityEngine;

namespace Assets.Scripts.Network
{
    public enum TelerehabCommand
    {
        StartRecording,
        StopRecording,
        PauseRecording,
        MarkEvent,
        Rebaseline,
        SetRestPosition,
        StartExercise,
        LoadScene,
    }

    /// <summary>
    /// Simple event bus for commands received from Flutter dashboard.
    /// Subscribe from any MonoBehaviour that needs to respond to dashboard commands.
    /// </summary>
    public static class TelerehabCommandBus
    {
        public static event Action<TelerehabCommand, string> OnCommand;

        public static void Raise(TelerehabCommand cmd, string payload = null)
        {
            OnCommand?.Invoke(cmd, payload);
        }
    }

}
