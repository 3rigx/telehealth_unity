using System.Collections.Generic;
using System.Linq.Expressions;
using Assets.Scripts.Exercises;
using Assets.Scripts.Exercises.ExerciseTypes;
using UnityEngine;

namespace Assets.Scripts.SaveSystem
{
    /// <summary>
    ///     Save Manager on a data level.
    /// </summary>
    public static class SaveManager
    {
        /// <summary>
        ///     Save the data to a file in the persistent data path.
        /// </summary>
        /// <param name="loc">Location to write to</param>
        /// <param name="data"> Saveable data to write</param>
        public static void Save(string loc, IEnumerable<ISaveable> data)
        {
            SaveState st = new();
            foreach (var item in data) item.Save(st);
            st.Sign();


            FileManager.WriteFile(loc, JsonUtility.ToJson(st));
        }

        public static void Save(string loc, IExercise exercise)
        {
            SaveManager.Save(loc, new List<ISaveable> { exercise });
        }

        /// <summary>
        ///     Load data
        /// </summary>
        /// <param name="loc">Location to write to</param>
        /// <param name="data">Variables to load data into</param>
        public static void Load(string loc, IEnumerable<ISaveable> data)
        {
            SaveState st = new(FileManager.LoadFromFile(loc));
            if (!st.Verify()) Debug.LogError("Invalid save file hash");
            foreach (var item in data) item.Load(st);

            Debug.Log("Loaded");
        }

        public static Exercise LoadExercise(string loc)
        {
            SaveState st = new(FileManager.LoadFromFile(loc));
            if (!st.Verify()) Debug.LogError("Invalid save file hash");
            return st.metadata.exerciseType switch
            {
                _ => new SandboxExercise(st.metadata, st.sensorStates)
            };
        }
    }
}