using System;

namespace Assets.Scripts.Sensors.Eeg
{
    /// <summary>
    ///     Lightweight EEG spectral helpers. Band power is the sum of Goertzel power at
    ///     each integer-Hz bin in the band — cheap enough to run on 8 channels every tick
    ///     and avoids a full FFT. The signal is DC-removed first (electrode offsets are
    ///     huge and would otherwise dominate the low bands).
    /// </summary>
    public static class EegBandPower
    {
        /// <summary>Goertzel power of a single frequency bin over n samples.</summary>
        private static double Goertzel(float[] x, int n, double freqHz, double fs)
        {
            double w = 2.0 * Math.PI * freqHz / fs;
            double coeff = 2.0 * Math.Cos(w);
            double s1 = 0, s2 = 0;
            for (int i = 0; i < n; i++)
            {
                double s0 = x[i] + coeff * s1 - s2;
                s2 = s1;
                s1 = s0;
            }
            double power = s1 * s1 + s2 * s2 - coeff * s1 * s2;
            return power / (n * 0.5); // rough per-bin normalisation
        }

        /// <summary>Sum of Goertzel power across integer-Hz bins in [loHz, hiHz] (DC-removed).</summary>
        public static double BandPower(float[] x, int n, int loHz, int hiHz, double fs)
        {
            if (x == null || n <= 0) return 0;
            double mean = 0;
            for (int i = 0; i < n; i++) mean += x[i];
            mean /= n;

            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = (float)(x[i] - mean);

            double sum = 0;
            int hi = (int)Math.Min(hiHz, fs / 2 - 1);
            for (int f = loHz; f <= hi; f++) sum += Goertzel(d, n, f, fs);
            return sum;
        }

        /// <summary>RMS amplitude after DC removal.</summary>
        public static double Rms(float[] x, int n)
        {
            if (x == null || n <= 0) return 0;
            double mean = 0;
            for (int i = 0; i < n; i++) mean += x[i];
            mean /= n;
            double ss = 0;
            for (int i = 0; i < n; i++) { double e = x[i] - mean; ss += e * e; }
            return Math.Sqrt(ss / n);
        }
    }
}
