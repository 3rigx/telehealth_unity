namespace Assets.Scripts.Exercises
{
    /// <summary>
    ///     The three capture classes for an Exercise session.
    ///     These are NOT used by Replay or Prediction; they exist to label captured
    ///     sessions (and become the training labels for the deferred prediction model).
    /// </summary>
    public enum ExerciseClass
    {
        Idle,
        Motion,
        MotionCognitive
    }

    /// <summary>
    ///     What kind of live session is being run.
    /// </summary>
    public enum SessionMode
    {
        Exercise,
        Prediction
    }

    public static class ExerciseClassExtensions
    {
        /// <summary>Filesystem-safe token used in the session folder name.</summary>
        public static string ToToken(this ExerciseClass c)
        {
            return c switch
            {
                ExerciseClass.Idle => "Idle",
                ExerciseClass.Motion => "Motion",
                ExerciseClass.MotionCognitive => "MotionCognitive",
                _ => "Unknown"
            };
        }

        /// <summary>Human-readable label for UI.</summary>
        public static string ToDisplay(this ExerciseClass c)
        {
            return c switch
            {
                ExerciseClass.Idle => "Idle",
                ExerciseClass.Motion => "Motion",
                ExerciseClass.MotionCognitive => "Motion + Cognitive",
                _ => "Unknown"
            };
        }

        public static ExerciseClass FromToken(string token)
        {
            return token switch
            {
                "Idle" => ExerciseClass.Idle,
                "Motion" => ExerciseClass.Motion,
                "MotionCognitive" => ExerciseClass.MotionCognitive,
                _ => ExerciseClass.Motion
            };
        }
    }
}
