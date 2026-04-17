using System;
using System.Collections.Generic;
using Assets.Scripts.SaveSystem;
using Assets.Scripts.Sensors;

namespace Assets.Scripts.Exercises
{
    /// <summary>
    ///     Interface for an exercise that can be performed by the user
    /// </summary>
    public interface IExercise : ISaveable
    {
        /// <summary>
        ///     Initialize exercise
        /// </summary>
        void Initialize();


        /// <summary>
        ///     Start exercise
        /// </summary>
        void Start();

        /// <summary>
        ///     Stop exercise
        /// </summary>
        void Stop();


        /// <summary>
        ///     Pause exercise
        /// </summary>
        void Pause();

        /// <summary>
        ///     Resume exercise
        /// </summary>
        void Resume();

        /// <summary>
        ///     Set cursor to step
        /// </summary>
        /// <param name="step"> Step to set cursor to </param>
        void SetCursor(int step);

        /// <summary>
        ///     Advance cursor
        /// </summary>
        void Advance();

        /// <summary>
        ///     Update exercise with new sensor state
        /// </summary>
        /// <param name="sensorSystemState">Sensor state to add to exercise</param>
        void Update(SensorSystemState sensorSystemState);

        List<SensorSystemState> GetSensorStates();

        SensorSystemState GetSensorState();

        /// <summary>
        ///     Get exercise score
        /// </summary>
        /// <returns>Score (TBA)</returns>
        /// <exception cref="NotImplementedException"> When scoring is not implemented</exception>
        int Score();

        /// <summary>
        ///     Get the number of states in the exercise
        /// </summary>
        /// <returns>The number of states</returns>
        int StateLength();

        bool IsPaused();

        // Target exercise end time
        DateTime? GetTargetEnd();
    }
}