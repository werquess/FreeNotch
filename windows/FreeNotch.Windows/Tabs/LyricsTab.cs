using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FreeNotch;

public sealed class LyricsTab : IslandTab
{
    private static readonly Dictionary<string, Dictionary<string, string>> Text = new()
    {
        ["tr"] = new()
        {
            ["Lyrics"] = "Şarkı sözleri",
            ["No lyrics found for this song"] = "Bu şarkı için söz bulunamadı",
            ["Nothing is playing"] = "Şu an bir şey çalmıyor",
            ["Loading lyrics"] = "Sözler yükleniyor",
        },
        ["de"] = new()
        {
            ["Lyrics"] = "Songtexte",
            ["No lyrics found for this song"] = "Keine Songtexte für diesen Song gefunden",
            ["Nothing is playing"] = "Es wird nichts abgespielt",
            ["Loading lyrics"] = "Songtext wird geladen",
        },
        ["es"] = new()
        {
            ["Lyrics"] = "Letras",
            ["No lyrics found for this song"] = "No se encontró la letra de esta canción",
            ["Nothing is playing"] = "No se está reproduciendo nada",
            ["Loading lyrics"] = "Cargando letra",
        },
        ["fr"] = new()
        {
            ["Lyrics"] = "Paroles",
            ["No lyrics found for this song"] = "Aucune parole trouvée pour ce titre",
            ["Nothing is playing"] = "Aucune lecture en cours",
            ["Loading lyrics"] = "Chargement des paroles",
        },
    };

    private static string T(string key) => Strings.Pick(Text, key);

    private sealed record LyricLine(TimeSpan Time, string Text);

    private sealed record LyricsResult(List<LyricLine> Lines, bool Synced);

    private static readonly HttpClient Http = CreateClient();
    private static readonly Regex TimestampPattern = new(@"^\[(\d+):(\d+(?:\.\d+)?)\]", RegexOptions.Compiled);
    private static readonly Dictionary<string, LyricsResult?> Cache = new();

    private readonly ScrollViewer scroller;
    private readonly StackPanel linePanel;
    private readonly TextBlock message;
    private readonly List<TextBlock> lineBlocks = new();
    private DispatcherQueueTimer? timer;
    private CancellationTokenSource? fetchCancel;
    private string loadedKey = "";
    private LyricsResult? current;
    private int highlighted = -1;
    private bool shown;

    public LyricsTab(TabContext context) : base(context)
    {
        Padding = new Thickness(22, 4, 22, 6);

        linePanel = new StackPanel
        {
            Spacing = 8,
            Padding = new Thickness(0, 50, 0, 120),
        };

        scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Enabled,
            Content = linePanel,
            Visibility = Visibility.Collapsed,
        };

        message = new TextBlock
        {
            Foreground = Ui.White(120),
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        Children.Add(scroller);
        Children.Add(message);

        context.Media.Changed += OnMediaChanged;
        ShowMessage(T("Nothing is playing"));
    }

    public override string Id => "lyrics";
    public override string Glyph => "";
    public override string Title => T("Lyrics");
    public override double ContentHeight => 180;

    public override void OnShown()
    {
        shown = true;
        timer = Context.Queue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(250);
        timer.Tick += (_, _) => Tick();
        timer.Start();
        Refresh();
    }

    public override void OnHidden()
    {
        shown = false;
        if (timer != null)
        {
            timer.Stop();
            timer = null;
        }
        fetchCancel?.Cancel();
        fetchCancel = null;
        if (current == null) loadedKey = "";
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FreeNotch-Windows/0.2 (https://github.com/werquess/FreeNotch)");
        return client;
    }

    private void OnMediaChanged()
    {
        if (!shown) return;
        Context.Queue.TryEnqueue(Refresh);
    }

    private void ShowMessage(string text)
    {
        message.Text = text;
        message.Visibility = Visibility.Visible;
        scroller.Visibility = Visibility.Collapsed;
    }

    private void Refresh()
    {
        if (!shown) return;
        var media = Context.Media;
        if (!media.HasSession || string.IsNullOrWhiteSpace(media.Title))
        {
            fetchCancel?.Cancel();
            loadedKey = "";
            current = null;
            lineBlocks.Clear();
            linePanel.Children.Clear();
            ShowMessage(T("Nothing is playing"));
            return;
        }

        var key = media.Artist.Trim().ToLowerInvariant() + "\n" + media.Title.Trim().ToLowerInvariant();
        if (key == loadedKey) return;
        loadedKey = key;
        fetchCancel?.Cancel();
        highlighted = -1;

        if (Cache.TryGetValue(key, out var cached))
        {
            Apply(cached);
            return;
        }

        current = null;
        lineBlocks.Clear();
        linePanel.Children.Clear();
        ShowMessage(T("Loading lyrics"));
        fetchCancel = new CancellationTokenSource();
        _ = LoadAsync(key, media.Artist, media.Title, media.Duration, fetchCancel.Token);
    }

    private async Task LoadAsync(string key, string artist, string title, TimeSpan duration, CancellationToken token)
    {
        LyricsResult? result = null;
        var failed = false;
        try
        {
            result = await FetchAsync(artist, title, duration, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            failed = true;
        }

        if (token.IsCancellationRequested) return;
        Context.Queue.TryEnqueue(() =>
        {
            if (!shown || key != loadedKey) return;
            if (!failed) Cache[key] = result;
            Apply(result);
        });
    }

    private static async Task<LyricsResult?> FetchAsync(string artist, string title, TimeSpan duration, CancellationToken token)
    {
        var seconds = Math.Max(0, (int)Math.Round(duration.TotalSeconds));
        var getUrl = "https://lrclib.net/api/get?artist_name=" + Uri.EscapeDataString(artist)
            + "&track_name=" + Uri.EscapeDataString(title)
            + (seconds > 0 ? "&duration=" + seconds.ToString(CultureInfo.InvariantCulture) : "");

        using (var response = await Http.GetAsync(getUrl, token))
        {
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync(token);
                using var document = JsonDocument.Parse(body);
                return ParseEntry(document.RootElement);
            }
        }

        var searchUrl = "https://lrclib.net/api/search?q=" + Uri.EscapeDataString((artist + " " + title).Trim());
        using var searchResponse = await Http.GetAsync(searchUrl, token);
        searchResponse.EnsureSuccessStatusCode();
        var searchBody = await searchResponse.Content.ReadAsStringAsync(token);
        using var searchDocument = JsonDocument.Parse(searchBody);
        if (searchDocument.RootElement.ValueKind != JsonValueKind.Array) return null;

        LyricsResult? plainFallback = null;
        foreach (var entry in searchDocument.RootElement.EnumerateArray())
        {
            var parsed = ParseEntry(entry);
            if (parsed == null) continue;
            if (parsed.Synced) return parsed;
            plainFallback ??= parsed;
        }
        return plainFallback;
    }

    private static LyricsResult? ParseEntry(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object) return null;

        var synced = ReadString(entry, "syncedLyrics");
        if (!string.IsNullOrWhiteSpace(synced))
        {
            var lines = ParseSynced(synced);
            if (lines.Count > 0) return new LyricsResult(lines, true);
        }

        var plain = ReadString(entry, "plainLyrics");
        if (!string.IsNullOrWhiteSpace(plain))
        {
            var lines = new List<LyricLine>();
            foreach (var raw in plain.Split('\n'))
            {
                var text = raw.Trim();
                if (text.Length > 0) lines.Add(new LyricLine(TimeSpan.Zero, text));
            }
            if (lines.Count > 0) return new LyricsResult(lines, false);
        }

        return null;
    }

    private static string ReadString(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static List<LyricLine> ParseSynced(string synced)
    {
        var lines = new List<LyricLine>();
        foreach (var raw in synced.Split('\n'))
        {
            var line = raw.Trim();
            var stamps = new List<TimeSpan>();
            while (true)
            {
                var match = TimestampPattern.Match(line);
                if (!match.Success) break;
                var minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var secs = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                stamps.Add(TimeSpan.FromSeconds(minutes * 60 + secs));
                line = line.Substring(match.Length).TrimStart();
            }
            if (stamps.Count == 0) continue;
            foreach (var stamp in stamps) lines.Add(new LyricLine(stamp, line));
        }
        lines.Sort((a, b) => a.Time.CompareTo(b.Time));
        return lines;
    }

    private void Apply(LyricsResult? result)
    {
        current = result;
        highlighted = -1;
        lineBlocks.Clear();
        linePanel.Children.Clear();

        if (result == null)
        {
            ShowMessage(T("No lyrics found for this song"));
            return;
        }

        for (var i = 0; i < result.Lines.Count; i++)
        {
            var index = i;
            var block = new TextBlock
            {
                Text = result.Lines[i].Text.Length == 0 ? "♪" : result.Lines[i].Text,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Ui.White(result.Synced ? (byte)90 : (byte)170),
            };
            if (result.Synced)
            {
                block.Tapped += (_, _) => SeekToLine(index);
            }
            lineBlocks.Add(block);
            linePanel.Children.Add(block);
        }

        message.Visibility = Visibility.Collapsed;
        scroller.Visibility = Visibility.Visible;
        scroller.ChangeView(null, 0, null, true);
        Tick();
    }

    private void SeekToLine(int index)
    {
        if (current == null || !current.Synced || index < 0 || index >= current.Lines.Count) return;
        Context.Media.Seek(current.Lines[index].Time);
    }

    private void Tick()
    {
        if (!shown || current == null || !current.Synced || lineBlocks.Count == 0) return;

        var position = Context.Media.Position;
        var index = -1;
        for (var i = 0; i < current.Lines.Count; i++)
        {
            if (current.Lines[i].Time <= position) index = i;
            else break;
        }

        if (index == highlighted) return;

        if (highlighted >= 0 && highlighted < lineBlocks.Count) lineBlocks[highlighted].Foreground = Ui.White(90);
        highlighted = index;
        if (index < 0) return;

        lineBlocks[index].Foreground = Ui.White(255);
        ScrollTo(lineBlocks[index]);
    }

    private void ScrollTo(TextBlock block)
    {
        if (block.ActualHeight <= 0) return;
        var offset = block.TransformToVisual(linePanel).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
        var target = offset - scroller.ViewportHeight / 3 + block.ActualHeight / 2;
        scroller.ChangeView(null, Math.Max(0, target), null, false);
    }
}
