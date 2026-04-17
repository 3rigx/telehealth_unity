using System;
using System.Runtime.Serialization;
using UnityEngine;

namespace Assets.Scripts.Sensors
{
    [Serializable]
    public class SensorState<T> where T : new()
    {
        [SerializeField] private string name;
#nullable enable
        [SerializeField] private T? val;
#nullable disable

        public SensorState(string name, int avgSiz = 1)
        {
            if (!typeof(T).IsSerializable && !typeof(ISerializable).IsAssignableFrom(typeof(T)))
                Debug.LogError("SensorState error: a serializable type is required");
            this.name = name;
        }

        public SensorState(string name, T val) : this(name)
        {
            this.val = val;
        }

        public void Update(T value)
        {
            val = value;
        }

        public T GetValue()
        {
            return val;
        }

        public string GetName()
        {
            return name;
        }


        public override string ToString()
        {
            return $"Sensor: {name} Value: {val}";
        }
    }
}