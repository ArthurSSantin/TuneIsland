using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace TuneIsland.Services;

/// <summary>Monitor em pixels físicos, com escala de DPI.</summary>
public sealed record DisplayInfo(
    int Index,
    string Name,
    int Left,
    int Top,
    int Width,
    int Height,
    double DpiScale,
    bool IsPrimary);

public static class DisplayService
{
    /// <summary>Lista os monitores: primário primeiro, depois da esquerda para a direita.</summary>
    public static IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var raw = new List<(NativeMethods.RECT Rect, double Scale, bool Primary)>();

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data) =>
            {
                var info = new NativeMethods.MONITORINFO
                {
                    cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
                };

                if (NativeMethods.GetMonitorInfoW(hMonitor, ref info))
                {
                    double scale = 1.0;
                    if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0)
                    {
                        scale = dpiX / 96.0;
                    }

                    raw.Add((info.rcMonitor, scale, (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0));
                }

                return true;
            },
            IntPtr.Zero);

        var ordered = raw
            .OrderByDescending(m => m.Primary)
            .ThenBy(m => m.Rect.Left)
            .ToList();

        if (ordered.Count == 0)
        {
            return new[] { new DisplayInfo(0, "Monitor 1 (principal)", 0, 0, 1920, 1080, 1.0, true) };
        }

        return ordered
            .Select((m, i) => new DisplayInfo(
                i,
                $"Monitor {i + 1}" + (m.Primary ? " (principal)" : string.Empty),
                m.Rect.Left,
                m.Rect.Top,
                m.Rect.Right - m.Rect.Left,
                m.Rect.Bottom - m.Rect.Top,
                m.Scale,
                m.Primary))
            .ToList();
    }
}
