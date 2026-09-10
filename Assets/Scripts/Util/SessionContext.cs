using Assets.Scripts.Exercises;

namespace Assets.Scripts.Util
{
    /// <summary>
    ///     Carries the configuration chosen on the Session Setup screen across the
    ///     scene load into the live capture scene (Exercise / Prediction).
    ///     Set by <c>SessionSetupController</c>, consumed by <c>ExerciseController</c>
    ///     and written into the manifest by <c>SessionWriter</c>.
    ///
    ///     When <see cref="IsConfigured"/> is false the capture scene falls back to its
    ///     legacy behaviour (manual save dialogs), so Free Mode / remote keep working.
    /// </summary>
    public static class SessionContext
    {
        public static bool IsConfigured;

        public static string PatientId;
        public static ExerciseClass ExerciseClass = ExerciseClass.Motion;
        public static SessionMode Mode = SessionMode.Exercise;

        public static bool ZedEnabled = true;
        public static bool FsrEnabled = true;
        public static bool EegEnabled; // wired in M2 (EEG connector); recorded in manifest now

        /// <summary>Absolute path of the session folder, resolved at session start.</summary>
        public static string SessionFolder;
        public static string SessionId;
        public static int TrialNumber;

        public static void Reset()
        {
            IsConfigured = false;
            PatientId = null;
            SessionFolder = null;
            SessionId = null;
            TrialNumber = 0;
        }
    }
}
