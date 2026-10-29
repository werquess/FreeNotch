using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Control;

namespace FreeNotch;

public sealed class MediaService
{
    private readonly DispatcherQueue queue;
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    private string trackKey = "";
    private bool? expectedPlaying;
    private DateTimeOffset expectedUntil;

    public event Action? Changed;

    public bool HasSession => session != null;
    public string Title { get; private set; } = "";
    public string Artist { get; private set; } = "";
    public string Source { get; private set; } = "";
    public string SourceId { get; private set; } = "";
    public ImageSource? Artwork { get; private set; }
    public bool IsPlaying { get; private set; }
    public TimeSpan Duration { get; private set; }
    private TimeSpan anchorPosition;
    private DateTimeOffset anchorTime;

    public MediaService(DispatcherQueue queue)
    {
        this.queue = queue;
    }

    public TimeSpan Position
    {
        get
        {
            var p = IsPlaying ? anchorPosition + (DateTimeOffset.Now - anchorTime) : anchorPosition;
            if (p < TimeSpan.Zero) return TimeSpan.Zero;
            return Duration > TimeSpan.Zero && p > Duration ? Duration : p;
        }
    }

    public async Task StartAsync()
    {
        try
        {
            manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch (Exception)
        {
            return;
        }
        manager.CurrentSessionChanged += (_, _) => queue.TryEnqueue(Attach);
        manager.SessionsChanged += (_, _) => queue.TryEnqueue(Attach);
        Attach();
    }

    private void Attach()
    {
        if (manager == null) return;
        var next = PickSession(manager);
        if (!ReferenceEquals(next, session))
        {
            if (session != null)
            {
                session.MediaPropertiesChanged -= OnProperties;
                session.PlaybackInfoChanged -= OnPlayback;
                session.TimelinePropertiesChanged -= OnTimeline;
            }
            session = next;
            trackKey = "";
            if (session != null)
            {
                session.MediaPropertiesChanged += OnProperties;
                session.PlaybackInfoChanged += OnPlayback;
                session.TimelinePropertiesChanged += OnTimeline;
            }
        }
        if (session == null)
        {
            Title = Artist = Source = SourceId = "";
            Artwork = null;
            IsPlaying = false;
            Changed?.Invoke();
            return;
        }
        ReadPlayback();
        ReadTimeline();
        _ = ReadPropertiesAsync();
    }

    private static GlobalSystemMediaTransportControlsSession? PickSession(GlobalSystemMediaTransportControlsSessionManager m)
    {
        var sessions = m.GetSessions();
        foreach (var s in sessions)
        {
            if (s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) return s;
        }
        return m.GetCurrentSession() ?? sessions.FirstOrDefault();
    }

    private void OnProperties(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e) =>
        queue.TryEnqueue(() => { _ = ReadPropertiesAsync(); });

    private void OnPlayback(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e) =>
        queue.TryEnqueue(() =>
        {
            Attach();
        });

    private void OnTimeline(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs e) =>
        queue.TryEnqueue(() =>
        {
            ReadTimeline();
            Changed?.Invoke();
        });

    private void ReadPlayback()
    {
        if (session == null) return;
        var playing = session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        if (expectedPlaying is bool expected)
        {
            if (DateTimeOffset.Now > expectedUntil || expected == playing) expectedPlaying = null;
            else playing = expected;
        }
        if (playing != IsPlaying)
        {
            anchorPosition = Position;
            anchorTime = DateTimeOffset.Now;
            IsPlaying = playing;
        }
        Source = SourceName(session.SourceAppUserModelId);
        SourceId = session.SourceAppUserModelId ?? "";
    }

    private void ReadTimeline()
    {
        if (session == null) return;
        var t = session.GetTimelineProperties();
        Duration = t.EndTime - t.StartTime;
        var age = DateTimeOffset.Now - t.LastUpdatedTime;
        anchorPosition = t.Position + (IsPlaying && age > TimeSpan.Zero && age < TimeSpan.FromHours(6) ? age : TimeSpan.Zero);
        anchorTime = DateTimeOffset.Now;
    }

    private async Task ReadPropertiesAsync()
    {
        var current = session;
        if (current == null) return;
        GlobalSystemMediaTransportControlsSessionMediaProperties props;
        try
        {
            props = await current.TryGetMediaPropertiesAsync();
        }
        catch (Exception)
        {
            return;
        }
        if (!ReferenceEquals(current, session)) return;
        Title = props.Title ?? "";
        Artist = string.IsNullOrEmpty(props.Artist) ? props.AlbumArtist ?? "" : props.Artist;
        var key = $"{Title}|{Artist}";
        var changedTrack = key != trackKey;
        trackKey = key;
        Changed?.Invoke();
        if (!changedTrack) return;
        if (props.Thumbnail == null)
        {
            Artwork = null;
            Changed?.Invoke();
            return;
        }
        try
        {
            using var stream = await props.Thumbnail.OpenReadAsync();
            var image = new BitmapImage();
            await image.SetSourceAsync(stream);
            if (trackKey != key) return;
            Artwork = image;
        }
        catch (Exception)
        {
            Artwork = null;
        }
        Changed?.Invoke();
    }

    public async void PlayPause()
    {
        if (session == null) return;
        anchorPosition = Position;
        anchorTime = DateTimeOffset.Now;
        IsPlaying = !IsPlaying;
        expectedPlaying = IsPlaying;
        expectedUntil = DateTimeOffset.Now.AddSeconds(2);
        Changed?.Invoke();
        try { await session.TryTogglePlayPauseAsync(); } catch (Exception) { }
    }

    public async void Next()
    {
        if (session == null) return;
        try { await session.TrySkipNextAsync(); } catch (Exception) { }
    }

    public async void Previous()
    {
        if (session == null) return;
        try { await session.TrySkipPreviousAsync(); } catch (Exception) { }
    }

    public async void Seek(TimeSpan position)
    {
        if (session == null) return;
        anchorPosition = position;
        anchorTime = DateTimeOffset.Now;
        Changed?.Invoke();
        try { await session.TryChangePlaybackPositionAsync(position.Ticks); } catch (Exception) { }
    }

    private static string SourceName(string id)
    {
        var lower = id.ToLowerInvariant();
        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("chrome")) return "Chrome";
        if (lower.Contains("brave")) return "Brave";
        if (lower.Contains("msedge")) return "Edge";
        if (lower.Contains("firefox")) return "Firefox";
        if (lower.Contains("opera")) return "Opera";
        if (lower.Contains("zunemusic") || lower.Contains("media player")) return "Media Player";
        if (lower.Contains("applemusic")) return "Apple Music";
        var name = Path.GetFileNameWithoutExtension(id.Split('!')[0]);
        return string.IsNullOrEmpty(name) ? id : name;
    }
}
