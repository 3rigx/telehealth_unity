using System;
using System.Collections.Generic;
using Assets.Scripts.Sensors;
using Assets.Scripts.Statistics;


namespace Assets.Scripts.Exercises
{
    public class StatisticsController
    {
        private LinkedList<SensorSystemState> _consideredSates;
        public List<DerivedState> _derivedStates;
        public List<SensorSystemState> States;
        
        
        public static Exercise CalcExercise(Exercise exercise)
        {
            int stateCount = exercise.GetSensorStates().Count;
            var states = exercise.GetSensorStates();
            exercise.derivedMetadata = new DerivedMetadata(exercise.GetMetadata());
            StatisticsController controller = new(states);
            for (int i = 0; i < stateCount; i++)
            {
                if (states[i].skeleton == null) continue;
                controller.UpdateStatistics(states[i]);
                states[i].derivedState = new DerivedState(controller._consideredSates);
            }
            
            exercise.GetMetadata().isProcessed = true;
            return exercise;

        }
        
        public StatisticsController(List<SensorSystemState> states)
        {
            States = states;
            _consideredSates = new LinkedList<SensorSystemState>();
            _derivedStates = new List<DerivedState>();
        }

        public void UpdateStatistics(SensorSystemState st)
        {
            if(st.skeleton == null) return;
            _consideredSates.AddLast(st);
            if (_consideredSates.Count > 10) _consideredSates.RemoveFirst();
        }
    }
}