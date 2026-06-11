using System;

namespace yomusic.Services
{
    public static class DurationHelper
    {
        public static string FormatDuration(TimeSpan? duration)
        {
            if (duration == null) return "";
            var ts = duration.Value;
            return ts.Hours > 0
                ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        }

        public static string FormatDuration(int? seconds)
        {
            if (seconds == null || seconds <= 0) return "";
            var ts = TimeSpan.FromSeconds(seconds.Value);
            return FormatDuration(ts);
        }
    }
}
