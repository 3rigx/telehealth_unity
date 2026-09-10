using UnityEngine;

namespace Assets.Scripts.Sensors.FSR
{
    /// <summary>
    ///     Single source of truth for the plantar-pressure hardware layout.
    ///
    ///     The study runs ONE insole on a fixed foot with four FSR pads. The legacy
    ///     two-insole (left+right) rig is still selectable via <see cref="InsoleCount"/>
    ///     = 2 but is off by default — the code path is guarded, never deleted, so the
    ///     old hardware can be brought back without a revert.
    ///
    ///     Everything that needs to know "how many feet", "which foot", "what are the
    ///     pads called", or "what is the ADC full-scale" reads it HERE. The number 1,
    ///     the foot name, and the 1023 full-scale are never written inline elsewhere.
    ///
    ///     Values are overridable at runtime (no recompile) via PlayerPrefs, mirroring
    ///     the EEG scale/port pattern, but the defaults are the study configuration.
    /// </summary>
    public static class PressureConfig
    {
        /// <summary>
        ///     Canonical pad names in Arduino analog-pin order (A0..A3):
        ///     A0 = toe (anterior), A1 = medial, A2 = lateral, A3 = heel (posterior).
        ///     One name per pad across firmware, wire, disk, and UI — never sensor1..4,
        ///     never midInner/midOuter.
        /// </summary>
        public static readonly string[] ChannelNames = { "toe", "medial", "lateral", "heel" };

        public const int ChannelCount = 4;

        /// <summary>1 = single insole (study default); 2 = legacy left+right rig.</summary>
        public static int InsoleCount => Mathf.Clamp(PlayerPrefs.GetInt("PressureInsoleCount", 1), 1, 2);

        /// <summary>True when the single-insole path is active (the study default).</summary>
        public static bool IsSingleFoot => InsoleCount == 1;

        /// <summary>Which foot the single insole is on ("left"/"right"). Fixed for the study.</summary>
        public static string Foot => PlayerPrefs.GetString("PressureFoot", "right").ToLowerInvariant();

        /// <summary>
        ///     ADC resolution of the Arduino. The board is 10-bit, so the full-scale
        ///     value a fully loaded pad reports is <see cref="AdcMax"/> = 1023. Everything
        ///     that normalises a raw reading to 0..1 divides by AdcMax, NOT a literal 255.
        /// </summary>
        public static int AdcBits => Mathf.Clamp(PlayerPrefs.GetInt("PressureAdcBits", 10), 8, 16);
        public static int AdcMax => (1 << AdcBits) - 1;

        /// <summary>
        ///     Serial baud rate for the single-insole Arduino. MUST match the firmware's
        ///     <c>Serial.begin(...)</c>. Settable without recompile via PlayerPrefs.
        /// </summary>
        public static int BaudRate => PlayerPrefs.GetInt("PressureBaudRate", 115200);

        /// <summary>Nominal Arduino stream rate (Hz). Documentation / expectations only.</summary>
        public const int SampleRateHz = 100;

        /// <summary>
        ///     Max per-channel ADC value still considered "unloaded" when a baseline is
        ///     captured. A baseline with any pad above this was taken under load (foot
        ///     already in the shoe) and will silently offset every later reading down —
        ///     so it raises a visible dashboard warning. Overridable via PlayerPrefs.
        /// </summary>
        public static int BaselineMaxAdc => Mathf.Clamp(PlayerPrefs.GetInt("PressureBaselineMaxAdc", 80), 0, AdcMax);
    }
}
