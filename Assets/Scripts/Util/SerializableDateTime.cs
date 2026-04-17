using System;
using System.Globalization;
using UnityEngine;

namespace Assets.Scripts.Util
{
    /// <summary>
    ///     Serializable lazy-parsing DateTime Wrapper
    /// </summary>
    /// <see cref="https://answers.unity.com/questions/1245121/saving-systemdatetime-in-a-serialized-scriptable-o.html" />
    [Serializable]
    public class SerializableDateTime
    {
        private bool loaded;
        private DateTime m_DateTime;
        [SerializeField] private long m_Ticks;

        public SerializableDateTime(DateTime dateTime)
        {
            m_Ticks = dateTime.Ticks;
            m_DateTime = dateTime;
            loaded = true;
        }

        public DateTime DateTime
        {
            get
            {
                Load();
                return m_DateTime;
            }
        }

        private void Load()
        {
            if (loaded) return;
            m_DateTime = new DateTime(m_Ticks);
            loaded = true;
        }

        public static implicit operator DateTime(SerializableDateTime serializableDateTime)
        {
            return serializableDateTime.DateTime;
        }

        public static implicit operator SerializableDateTime(DateTime dateTime)
        {
            return new SerializableDateTime(dateTime);
        }

        public override string ToString()
        {
            Load();
            return m_DateTime.ToString(CultureInfo.CurrentCulture);
        }
    }
}