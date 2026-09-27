using System;
using System.Globalization;

internal static class TimeLimitedSaleWindow
{
    public static bool TryGetActiveEndUtc(string startTime, string endTime, DateTime utcNow, out DateTime endUtc)
    {
        endUtc = default;
        const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        if (!DateTimeOffset.TryParse(startTime, CultureInfo.InvariantCulture, styles, out var start) ||
            !DateTimeOffset.TryParse(endTime, CultureInfo.InvariantCulture, styles, out var end))
        {
            return false;
        }

        endUtc = end.UtcDateTime;
        return start.UtcDateTime <= utcNow && utcNow < endUtc;
    }
}
