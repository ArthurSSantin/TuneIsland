using System;

namespace TuneIsland.Core;

/// <summary>Estado imutável da sessão de mídia que está tocando.</summary>
public sealed record MediaSnapshot(
    string AppId,
    string Title,
    string Artist,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    DateTimeOffset CapturedAtUtc,
    byte[]? Thumbnail,
    string TrackKey)
{
    /// <summary>Estima a posição atual sem novas leituras do SMTC, limitada a [0, Duration].</summary>
    public TimeSpan EstimatePosition(DateTimeOffset nowUtc)
    {
        var elapsed = IsPlaying ? nowUtc - CapturedAtUtc : TimeSpan.Zero;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var estimate = Position + elapsed;
        if (estimate < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        if (Duration > TimeSpan.Zero && estimate > Duration)
        {
            return Duration;
        }

        return estimate;
    }
}
