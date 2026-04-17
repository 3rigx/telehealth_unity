using System;
using System.Collections.Generic;
using Assets.Scripts.SaveSystem;
using Assets.Scripts.Sensors;
using Assets.Scripts.Sensors.Zed;
using Assets.Scripts.Statistics;
using Assets.Scripts.Util;
using UnityEngine;
using UnityEngine.Serialization;

#pragma warning disable S1104

namespace Assets.Scripts.Exercises
{
    [Serializable]
    public class ExerciseMetadata
    {
        [SerializeField] public BODY_FORMAT bodyFormat = BODY_FORMAT.BODY_34;
        [FormerlySerializedAs("Breaks")] [SerializeField] public List<Break> breaks;
        [FormerlySerializedAs("Comment")] [SerializeField] public string comment;
        [FormerlySerializedAs("ExerciseType")] [SerializeField] public string exerciseType;
        [FormerlySerializedAs("Id")] [SerializeField] public int id;
        [FormerlySerializedAs("MeasurementInterval")] [SerializeField] public float measurementInterval;
        [FormerlySerializedAs("PrescribedBy")] [SerializeField] public string prescribedBy;
        [FormerlySerializedAs("Start")] [SerializeField] public SerializableDateTime start;
        [SerializeField] public bool isProcessed = false;
        [SerializeField] public SerializableDictionary<string, string> patientData = new();


        internal ExerciseMetadata()
        {
            exerciseType = "";
            prescribedBy = "";
            comment = "";
            measurementInterval = 0;
            prescribedLength = 0;
            start = null;
            end = null;
            breakStart = null;
            breaks = new List<Break>();
        }

        internal ExerciseMetadata(string exerciseType)
        {
            this.exerciseType = exerciseType;
            prescribedBy = "";
            comment = "";
            measurementInterval = 0;
            prescribedLength = 0;
            start = DateTime.Now;
            end = null;
            breakStart = null;
            breaks = new List<Break>();
        }

        public ExerciseMetadata(int id, string prescribedBy, string comment, float measurementInterval,
            int prescribedLength,
            DateTime? prescribedStart = null, DateTime? prescribedEnd = null)
        {
            this.id = id;
            this.prescribedBy = prescribedBy;
            this.comment = comment;
            this.measurementInterval = measurementInterval;
            this.prescribedLength = prescribedLength;
            this.prescribedStart = prescribedStart;
            this.prescribedEnd = prescribedEnd;
            start = null;
            end = null;
            breakStart = null;
            breaks = new List<Break>();
        }

        [Serializable]
        public class Break
        {
            [FormerlySerializedAs("End")] [SerializeField] public SerializableDateTime end;
            [FormerlySerializedAs("Start")] [SerializeField] public SerializableDateTime start;

            public Break(DateTime start, DateTime end)
            {
                this.start = start;
                this.end = end;
            }

            public TimeSpan Length()
            {
                return end.DateTime - start.DateTime;
            }
        }
#nullable enable
        [FormerlySerializedAs("BreakStart")] [SerializeField] public SerializableDateTime? breakStart;
        [FormerlySerializedAs("End")] [SerializeField] public SerializableDateTime? end;
        [FormerlySerializedAs("PrescribedEnd")] [SerializeField] public SerializableDateTime? prescribedEnd;
        [FormerlySerializedAs("PrescribedStart")] [SerializeField] public SerializableDateTime? prescribedStart;
        [FormerlySerializedAs("PrescribedLength")] [SerializeField] public int prescribedLength;

        [FormerlySerializedAs("Score")] [SerializeField] public int score;
        [FormerlySerializedAs("TargetScore")] [SerializeField] public int targetScore;
#nullable disable
    }

    /*
     * Abstract exercise for all exercises implemented by me
     *
     * Further exercises may opt to use the IExercise Interface instead
     */
    [Serializable]
    public abstract class Exercise : IExercise
    {
#nullable enable
        [NonSerialized] protected DateTime? _targetEnd;
        [SerializeField] public DerivedMetadata? derivedMetadata;
#nullable disable

        [NonSerialized] protected int currI;
        [SerializeField] protected ExerciseMetadata metadata;
        [NonSerialized] protected float secondLength;
        [SerializeField] protected List<SensorSystemState> sensorStates;

        public Exercise()
        {
            metadata = new ExerciseMetadata(GetType().Name);
            sensorStates = new List<SensorSystemState>();
            secondLength = 1f / metadata.measurementInterval;
            _targetEnd = DateTime.Now;
        }

        public Exercise(ExerciseMetadata metadata)
        {
            this.metadata = metadata;
            this.metadata.exerciseType = GetType().Name;
            sensorStates = new List<SensorSystemState>();
            secondLength = 1f / metadata.measurementInterval;
            _targetEnd = metadata.prescribedEnd?.DateTime;
        }

        public Exercise(ExerciseMetadata metadata, List<SensorSystemState> sensorInput) : this(metadata)
        {
            sensorStates = sensorInput;
        }

        public List<SensorSystemState> GetSensorStates()
        {
            return sensorStates;
        }

        public abstract int Score();

        public abstract void Initialize();


        public void Start()
        {
            metadata.start = DateTime.Now;
        }

        public void Pause()
        {
            if (metadata.breakStart != null) throw new InvalidOperationException("Exercise is already paused");
            metadata.breakStart = DateTime.Now;
        }

        public void Resume()
        {
            if (metadata.breakStart == null) throw new InvalidOperationException("Exercise is not paused");
            var currbreak = new ExerciseMetadata.Break(metadata.breakStart, DateTime.Now);
            metadata.breaks.Add(currbreak);
            metadata.breakStart = null;
            _targetEnd += currbreak.Length();
        }

        public void Stop()
        {
            metadata.end = DateTime.Now;
            try
            {
                metadata.score = Score();
            }
            catch (NotImplementedException)
            {
                metadata.score = 0;
            }
        }

        public virtual void Update(SensorSystemState sensorSystemState)
        {
            sensorStates.Add(sensorSystemState);
            ++currI;
            try
            {
                Process();
            }
            catch (NotImplementedException)
            {
            }
        }

        public SensorSystemState GetSensorState()
        {
            return sensorStates[currI];
        }

        public virtual void Advance()
        {
            if (currI < sensorStates.Count - 1) ++currI;
        }

        public virtual void SetCursor(int step)
        {
            if (step >= sensorStates.Count) throw new ArgumentOutOfRangeException(nameof(step), "Step out of range");
            currI = step;
            for (var i = 0; i <= step; i++)
                try
                {
                    Process();
                }
                catch (NotImplementedException)
                {
                }
        }


        public void Save(SaveState st)
        {
            st.metadata = metadata;
            st.sensorStates = sensorStates;
        }

        public void Load(SaveState st)
        {
            metadata = st.metadata;
            sensorStates = st.sensorStates;
            currI = 0;
        }

        public int StateLength()
        {
            return sensorStates.Count;
        }

        public bool IsPaused()
        {
            return metadata.breakStart != null;
        }

        public DateTime? GetTargetEnd()
        {
            return _targetEnd;
        }

        public ExerciseMetadata GetMetadata()
        {
            return metadata;
        }

        public int getStateOffsetBySeconds(int seconds)
        {
            var offset = (int)(seconds / metadata.measurementInterval);
            return offset;
        }

        /// <summary>
        ///     Called when a state is first loaded to evaluate scoring
        /// </summary>
        public abstract void Process();
    }
}