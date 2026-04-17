using System;
using System.Collections.Generic;
using Assets.Scripts.Sensors;
using UnityEngine;

namespace Assets.Scripts.Exercises.ExerciseTypes
{
    [Serializable]
    public class SandboxExercise : Exercise
    {
        public SandboxExercise()
        {
        }

        /// <summary>
        ///     Sandbox Exercise (No scoring)
        /// </summary>
        /// <param name="metadata"> Exercise metadata</param>
        public SandboxExercise(ExerciseMetadata metadata) : base(metadata)
        {
            Debug.Log("SandboxExercise created");
        }

        public SandboxExercise(ExerciseMetadata metadata, List<SensorSystemState> sensorStates) : base(metadata,
            sensorStates)
        {
            Debug.Log("SandboxExercise created");
        }

        public override void Initialize()
        {
        }

        /// <summary>
        ///     Sandbox Exercise Has no Scoring
        /// </summary>
        /// <exception cref="NotImplementedException">As required by interface</exception>
        public override int Score()
        {
            throw new NotImplementedException();
        }

        public override void Process()
        {
        }
    }
}