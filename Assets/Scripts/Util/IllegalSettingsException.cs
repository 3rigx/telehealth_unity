using System;

namespace Assets.Scripts.Util
{
    public class IllegalSettingsException : Exception
    {
        public string message;
        public string setting;

        public IllegalSettingsException(string setting)
        {
            this.setting = setting;
        }

        public IllegalSettingsException(string setting, string message) : base(message)
        {
            this.setting = setting;
            this.message = message;
        }
    }
}