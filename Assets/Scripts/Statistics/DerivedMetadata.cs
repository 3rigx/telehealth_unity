using Assets.Scripts.Exercises;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Statistics
{

    /// <summary>
    /// Metadata container holding Exercise Metadata fields derived from Metadata.
    ///
    /// Examples include: Time of Day from timestamp 
    /// </summary>
    public class DerivedMetadata
    {
        /// <summary>
        /// Valid Time of Day values.
        /// </summary>
        public enum TimeOfDayType { Morning, Afternoon, Evening, Night };

        
        [SerializeField] public TimeOfDayType TimeOfDay;

        private TimeOfDayType GetTimeOfDay(TimeSpan time)
        {
           if(time.Hours >= 6 && time.Hours < 12)
            {
                return TimeOfDayType.Morning;
            }
           else if(time.Hours >= 12 && time.Hours < 18)
            {
                return TimeOfDayType.Afternoon;
            }
           else if(time.Hours >= 18 && time.Hours < 24)
            {
                return TimeOfDayType.Evening;
            }
           else
            {
                return TimeOfDayType.Night;
            }
        }

        public DerivedMetadata(ExerciseMetadata metadata)
        {
            TimeOfDay = GetTimeOfDay(metadata.start.DateTime.TimeOfDay);
        }
    }
}
