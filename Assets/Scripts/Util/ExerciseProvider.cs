using Assets.Scripts.Exercises;
using UnityEngine;

namespace Assets.Scripts.Util
{
    public class ExerciseProvider
    {
        private static bool m_isRemote;
        public static bool isRemote
        {
            get => m_isRemote;
            set
            {
                m_isRemote = value;
                if (value)
                {
                    dataFileLocation = Application.persistentDataPath + "/datafile.json";
                    videoFileLocation = Application.persistentDataPath + "/videofile.mp4";
                }
                else
                {
                    dataFileLocation = null;
                    videoFileLocation = null;
                }
            }
        }
#nullable enable
        public static Exercise? exercise { get; set; }
        public static string? dataFileLocation { get; set; }
        public static string? videoFileLocation { get; set; }
#nullable disable
    }
}