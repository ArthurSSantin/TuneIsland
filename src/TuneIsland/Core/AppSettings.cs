using System;

namespace TuneIsland.Core;

public enum IslandStyle
{
    Attached,
    Floating
}

public sealed class AppSettings
{
    public const int MinWidth = 180;
    public const int MaxWidth = 560;
    public const int MinAutoExpandSeconds = 1;
    public const int MaxAutoExpandSeconds = 10;

    public IslandStyle Style { get; set; } = IslandStyle.Attached;

    /// <summary>Índice do monitor (0 = primário, depois da esquerda para a direita).</summary>
    public int MonitorIndex { get; set; }

    public int Width { get; set; } = 265;

    public int AutoExpandSeconds { get; set; } = 5;

    public bool StartWithWindows { get; set; }

    public bool HideWhenIdle { get; set; } = false;

    public bool HideInFullscreen { get; set; } = true;

    /// <summary>Restringe os valores a faixas seguras. Chamado após carregar ou salvar.</summary>
    public void Clamp()
    {
        Width = Math.Clamp(Width, MinWidth, MaxWidth);
        AutoExpandSeconds = Math.Clamp(AutoExpandSeconds, MinAutoExpandSeconds, MaxAutoExpandSeconds);
        MonitorIndex = Math.Max(0, MonitorIndex);
    }
}
