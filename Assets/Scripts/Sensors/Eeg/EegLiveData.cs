namespace Assets.Scripts.Sensors.Eeg
{
    /// <summary>Live EEG metrics handed to the dashboard broadcaster each tick.</summary>
    public class EegLiveData
    {
        public bool connected;
        public float theta;   // mean band power across channels, µV²
        public float alpha;
        public float beta;
        public float[] channelRms; // per-channel RMS µV (DC-removed), length = ChannelCount
        public string quality = "—";
        public string artifact = "—";
    }
}
