using System;

namespace TuneIsland.Core;

public static class TimeFormatter
{
    /// <summary>Formata como m:ss, ou h:mm:ss a partir de 1 hora. Valores negativos viram 0:00.</summary>
    public static string Format(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes}:{time.Seconds:D2}";
    }
}
