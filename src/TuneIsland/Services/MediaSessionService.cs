using System;
using System.IO;
using System.Threading.Tasks;
using TuneIsland.Core;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace TuneIsland.Services;

/// <summary>
/// Envolve o SMTC. Reage a eventos (sem polling) e publica um <see cref="MediaSnapshot"/>
/// da sessão que está tocando. Sem sessão tocando, publica null (ilha ociosa).
/// </summary>
public sealed class MediaSessionService : IDisposable
{
    private readonly object _sync = new();
    private readonly System.Threading.SemaphoreSlim _gate = new(1, 1);

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private string? _thumbnailKey;
    private byte[]? _thumbnail;
    private bool _disposed;

    public event EventHandler<MediaSnapshot?>? SnapshotChanged;

    public async Task StartAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += (_, _) => _ = ReselectAsync();
        _manager.CurrentSessionChanged += (_, _) => _ = ReselectAsync();
        await ReselectAsync();
    }

    public async Task<bool> TogglePlayPauseAsync()
    {
        var session = CurrentSession();
        return session is not null && await session.TryTogglePlayPauseAsync();
    }

    public async Task<bool> NextAsync()
    {
        var session = CurrentSession();
        return session is not null && await session.TrySkipNextAsync();
    }

    public async Task<bool> PreviousAsync()
    {
        var session = CurrentSession();
        return session is not null && await session.TrySkipPreviousAsync();
    }

    public async Task<bool> SeekAsync(TimeSpan position)
    {
        var session = CurrentSession();
        if (session is null || position < TimeSpan.Zero)
        {
            return false;
        }

        return await session.TryChangePlaybackPositionAsync(position.Ticks);
    }

    private GlobalSystemMediaTransportControlsSession? CurrentSession()
    {
        lock (_sync)
        {
            return _session;
        }
    }

    private async Task ReselectAsync()
    {
        if (_manager is null || _disposed)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            var chosen = PickPlayingSession(_manager);

            Detach();
            lock (_sync)
            {
                _session = chosen;
            }

            Attach(chosen);
            await PublishLockedAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Falha ao escolher a sessão de mídia.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PublishAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await PublishLockedAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Falha ao ler metadados de mídia.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PublishLockedAsync()
    {
        var snapshot = await BuildSnapshotAsync(CurrentSession());
        SnapshotChanged?.Invoke(this, snapshot);
    }

    private async Task<MediaSnapshot?> BuildSnapshotAsync(GlobalSystemMediaTransportControlsSession? session)
    {
        if (session is null || !IsPlaying(session))
        {
            return null;
        }

        var properties = await session.TryGetMediaPropertiesAsync();
        var timeline = session.GetTimelineProperties();

        var title = properties.Title ?? string.Empty;
        var artist = properties.Artist ?? string.Empty;
        var appId = session.SourceAppUserModelId ?? string.Empty;
        var trackKey = $"{appId}|{title}|{artist}";

        if (trackKey != _thumbnailKey)
        {
            _thumbnail = await ReadThumbnailAsync(properties.Thumbnail);
            _thumbnailKey = trackKey;
        }

        var duration = timeline.EndTime - timeline.StartTime;
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        var position = timeline.Position;
        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }

        return new MediaSnapshot(
            appId,
            title,
            artist,
            IsPlaying: true,
            Position: position,
            Duration: duration,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            Thumbnail: _thumbnail,
            TrackKey: trackKey);
    }

    private static async Task<byte[]?> ReadThumbnailAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null)
        {
            return null;
        }

        using var winStream = await reference.OpenReadAsync();
        using var netStream = winStream.AsStreamForRead();
        using var buffer = new MemoryStream();
        await netStream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static GlobalSystemMediaTransportControlsSession? PickPlayingSession(
        GlobalSystemMediaTransportControlsSessionManager manager)
    {
        var current = manager.GetCurrentSession();
        if (current is not null && IsPlaying(current))
        {
            return current;
        }

        foreach (var session in manager.GetSessions())
        {
            if (IsPlaying(session))
            {
                return session;
            }
        }

        return null;
    }

    private static bool IsPlaying(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            return session.GetPlaybackInfo().PlaybackStatus ==
                   GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void Attach(GlobalSystemMediaTransportControlsSession? session)
    {
        if (session is null)
        {
            return;
        }

        session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
        session.PlaybackInfoChanged += OnPlaybackInfoChanged;
    }

    private void Detach()
    {
        GlobalSystemMediaTransportControlsSession? previous;
        lock (_sync)
        {
            previous = _session;
        }

        if (previous is null)
        {
            return;
        }

        previous.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        previous.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        previous.PlaybackInfoChanged -= OnPlaybackInfoChanged;
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args) => _ = PublishAsync();

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender,
        TimelinePropertiesChangedEventArgs args) => _ = PublishAsync();

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args) => _ = ReselectAsync();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Detach();
        lock (_sync)
        {
            _session = null;
        }
    }
}
