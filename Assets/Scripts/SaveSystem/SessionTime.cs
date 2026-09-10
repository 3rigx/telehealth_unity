using System;
using System.Globalization;

namespace Assets.Scripts.SaveSystem
{
    /// <summary>
    ///     Consistent UTC timestamp formatting/parsing for per-sensor files.
    ///     Captured timestamps originate from <c>DateTime.UtcNow</c>, but a DateTime
    ///     rebuilt from ticks has <c>Kind == Unspecified</c>, so we pin the kind to UTC
    ///     before formatting to avoid a local-time shift on round-trip.
    /// </summary>
    public static class SessionTime
    {
        public static string FormatUtc(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Unspecified)
                dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return dt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }

        public static DateTime ParseUtc(string s)
        {
            if (!string.IsNullOrEmpty(s) &&
                DateTime.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal, out var dt))
                return dt.ToUniversalTime();
            return DateTime.UtcNow;
        }
    }
}
