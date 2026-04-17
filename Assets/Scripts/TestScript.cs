
using Assets.Scripts.Util;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace Assets.Scripts
{
    public class TestScript: MonoBehaviour
    {
        [Serializable]
        private class DictState
        {

            [SerializeField] public SerializableDictionary<string, string> dict = new();

            public DictState()
            {
                dict.Add("key1", "value1");
                dict.Add("key2", "value2");
            }
        }

        private void Start()
        {
            Debug.Log(JsonUtility.ToJson(new DictState()));
            DictState state2 = JsonUtility.FromJson<DictState>(JsonUtility.ToJson(new DictState()));
            Debug.Log(state2.dict["key1"]);
        }
    }
}
