using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Assets.Scripts.Exercises;
using Assets.Scripts.Sensors;
using UnityEngine;
using UnityEngine.Serialization;

#pragma warning disable S1104

namespace Assets.Scripts.SaveSystem
{
    /// <summary>
    ///     Save state for an exercise.
    ///     Used to ensure that non-serializable objects are saved as well
    /// </summary>
    [Serializable]
    public class SaveState
    {
#nullable enable
        [FormerlySerializedAs("m_Hash")] [SerializeField] private byte[]? hash;
#nullable disable
        [FormerlySerializedAs("Metadata")] [SerializeField] public ExerciseMetadata metadata;
        [FormerlySerializedAs("SensorStates")] [SerializeField] public List<SensorSystemState> sensorStates;

        public SaveState()
        {
        }

        public SaveState(string json)
        {
            FromJson(json);
        }

        private static byte[] GetHash(string str)
        {
            UTF8Encoding encoding = new();
            var sha = new SHA256CryptoServiceProvider();
            return sha.ComputeHash(encoding.GetBytes(str));
        }

        private static byte[] GetSaltedHash(string str)
        {
            return GetHash("salt1" + str + "salt2");
        }

        public string GetSaveHash()
        {
            return metadata.exerciseType + metadata.prescribedBy + sensorStates.Count;
        }


        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public void FromJson(string json)
        {
            JsonUtility.FromJsonOverwrite(json, this);
        }


        public void Sign()
        {
            hash = GetSaltedHash(GetSaveHash());
        }

        public bool Verify()
        {
            // Backward compatibility: if hash is null, allow load (file was created before hash verification)
            if (hash == null)
            {
                Debug.LogWarning("Save file loaded without hash verification (legacy file format)");
                return true;
            }
            
            // Compare byte arrays by value, not reference
            return hash.SequenceEqual(GetSaltedHash(GetSaveHash()));
        }
    }


    /// <summary>
    ///     Interface for objects that can be saved
    /// </summary>
    public interface ISaveable
    {
        void Save(SaveState st);
        void Load(SaveState st);
    }
}