using System;
using Microsoft.Win32;
using TuneIsland.Core;

namespace TuneIsland.Services;

/// <summary>Detecta se há um app em tela cheia (jogo, vídeo) em foco.</summary>
public static class FullscreenDetector
{
    public static bool IsFullscreenAppActive()
    {
        try
        {
            if (NativeMethods.SHQueryUserNotificationState(out int state) != 0)
            {
                return false;
            }

            return state is NativeMethods.QUNS_BUSY
                or NativeMethods.QUNS_RUNNING_D3D_FULL_SCREEN
                or NativeMethods.QUNS_PRESENTATION_MODE;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Falha ao verificar tela cheia.");
            return false;
        }
    }
}

/// <summary>Controla a entrada HKCU\...\Run para iniciar com o Windows.</summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TuneIsland";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey, true);

        if (enabled)
        {
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Caminho do executável indisponível.");
            key.SetValue(ValueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }
}

/// <summary>Ajusta o volume do sistema simulando as teclas de volume (passo do Windows).</summary>
public static class SystemVolume
{
    public static void Step(bool up)
    {
        var key = up ? NativeMethods.VK_VOLUME_UP : NativeMethods.VK_VOLUME_DOWN;
        NativeMethods.keybd_event(key, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(key, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}
