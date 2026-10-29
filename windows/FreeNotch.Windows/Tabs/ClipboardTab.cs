using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace FreeNotch;

public sealed class ClipboardEntry
{
    public string Kind { get; set; } = "text";
    public string Content { get; set; } = "";
    public string ImageFile { get; set; } = "";
    public string Hash { get; set; } = "";
    public long UtcTicks { get; set; }
}

public sealed class ClipboardTab : IslandTab
{
    private const int MaxItems = 40;
    private const int MaxTextLength = 50000;
    private const string KindText = "text";
    private const string KindImage = "image";
    private const string KindFiles = "files";

    private static readonly string[] IgnoredFormats =
    {
        "ExcludeClipboardContentFromMonitorProcessing",
        "Clipboard Viewer Ignore",
        "CanIncludeInClipboardHistory",
    };

    private static readonly Dictionary<string, Dictionary<string, string>> Text = new()
    {
        ["tr"] = new()
        {
            ["Clipboard"] = "Pano",
            ["Search clipboard"] = "Panoda ara",
            ["Clear"] = "Temizle",
            ["Copied"] = "Kopyalandı",
            ["Nothing copied yet"] = "Henüz bir şey kopyalanmadı",
            ["No matches"] = "Eşleşme yok",
            ["now"] = "şimdi",
            ["min"] = "dk",
            ["h"] = "sa",
            ["Yesterday"] = "Dün",
            ["Image"] = "Görsel",
            ["Remove"] = "Kaldır",
        },
        ["de"] = new()
        {
            ["Clipboard"] = "Zwischenablage",
            ["Search clipboard"] = "Zwischenablage durchsuchen",
            ["Clear"] = "Leeren",
            ["Copied"] = "Kopiert",
            ["Nothing copied yet"] = "Noch nichts kopiert",
            ["No matches"] = "Keine Treffer",
            ["now"] = "jetzt",
            ["min"] = "Min.",
            ["h"] = "Std.",
            ["Yesterday"] = "Gestern",
            ["Image"] = "Bild",
            ["Remove"] = "Entfernen",
        },
        ["es"] = new()
        {
            ["Clipboard"] = "Portapapeles",
            ["Search clipboard"] = "Buscar en el portapapeles",
            ["Clear"] = "Borrar",
            ["Copied"] = "Copiado",
            ["Nothing copied yet"] = "Aún no se ha copiado nada",
            ["No matches"] = "Sin resultados",
            ["now"] = "ahora",
            ["min"] = "min",
            ["h"] = "h",
            ["Yesterday"] = "Ayer",
            ["Image"] = "Imagen",
            ["Remove"] = "Quitar",
        },
        ["fr"] = new()
        {
            ["Clipboard"] = "Presse-papiers",
            ["Search clipboard"] = "Rechercher dans le presse-papiers",
            ["Clear"] = "Effacer",
            ["Copied"] = "Copié",
            ["Nothing copied yet"] = "Rien de copié pour l'instant",
            ["No matches"] = "Aucun résultat",
            ["now"] = "à l'instant",
            ["min"] = "min",
            ["h"] = "h",
            ["Yesterday"] = "Hier",
            ["Image"] = "Image",
            ["Remove"] = "Supprimer",
        },
    };

    private static string T(string key) => Strings.Pick(Text, key);

    private readonly List<ClipboardEntry> entries = new();
    private readonly TextBox searchBox;
    private readonly Button clearButton;
    private readonly StackPanel listPanel;
    private readonly ScrollViewer scroller;
    private readonly DispatcherQueueTimer copiedTimer;
    private bool shown;
    private bool holding;
    private bool capturing;
    private bool captureAgain;
    private DateTime ignoreUntilUtc = DateTime.MinValue;
    private TextBlock? copiedLabel;
    private ClipboardEntry? copiedEntry;

    public ClipboardTab(TabContext context) : base(context)
    {
        RequestedTheme = ElementTheme.Dark;
        Padding = new Thickness(22, 4, 22, 6);
        RowSpacing = 6;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        searchBox = new TextBox
        {
            PlaceholderText = T("Search clipboard"),
            Height = 30,
            MinHeight = 30,
            FontSize = 12,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 5, 10, 5),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        searchBox.TextChanged += (_, _) => Rebuild();
        searchBox.GotFocus += (_, _) => SetHolding(true);
        searchBox.LostFocus += (_, _) => SetHolding(false);

        clearButton = new Button
        {
            Content = Ui.Text(T("Clear"), 12, FontWeights.SemiBold, 200),
            Height = 30,
            MinHeight = 30,
            Padding = new Thickness(12, 0, 12, 0),
            CornerRadius = new CornerRadius(8),
            Background = Ui.White(20),
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        clearButton.Click += (_, _) => ClearAll();

        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(searchBox);
        header.Children.Add(clearButton);
        Grid.SetColumn(clearButton, 1);
        Children.Add(header);

        listPanel = new StackPanel { Spacing = 2 };
        scroller = new ScrollViewer
        {
            Content = listPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Auto,
        };
        Children.Add(scroller);
        Grid.SetRow(scroller, 1);

        copiedTimer = context.Queue.CreateTimer();
        copiedTimer.Interval = TimeSpan.FromMilliseconds(1200);
        copiedTimer.IsRepeating = false;
        copiedTimer.Tick += (_, _) => RestoreCopiedLabel();

        Load();
        Rebuild();
        Clipboard.ContentChanged += OnClipboardChanged;
    }

    public override string Id => "clipboard";
    public override string Glyph => "";
    public override string Title => T("Clipboard");
    public override double ContentHeight => 220;

    private static string ImageFolder
    {
        get
        {
            var folder = Path.Combine(TabContext.DataFolder, "clipboard");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    private static string JsonPath => Path.Combine(TabContext.DataFolder, "clipboard.json");

    public override void OnShown()
    {
        shown = true;
        Rebuild();
    }

    public override void OnHidden()
    {
        shown = false;
        copiedTimer.Stop();
        copiedLabel = null;
        copiedEntry = null;
        SetHolding(false);
    }

    private void SetHolding(bool hold)
    {
        if (holding == hold) return;
        holding = hold;
        Context.HoldOpen?.Invoke(hold);
    }

    private void OnClipboardChanged(object? sender, object e)
    {
        Context.Queue.TryEnqueue(() => _ = CaptureAsync());
    }

    private async Task CaptureAsync()
    {
        if (capturing)
        {
            captureAgain = true;
            return;
        }

        capturing = true;
        try
        {
            do
            {
                captureAgain = false;
                if (DateTime.UtcNow < ignoreUntilUtc) continue;
                await CaptureOnceAsync();
            }
            while (captureAgain);
        }
        finally
        {
            capturing = false;
        }
    }

    private async Task CaptureOnceAsync()
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                var view = Clipboard.GetContent();
                foreach (var format in IgnoredFormats)
                {
                    if (view.Contains(format)) return;
                }

                var entry = await ReadEntryAsync(view);
                if (entry != null) AddEntry(entry);
                return;
            }
            catch
            {
                await Task.Delay(120);
            }
        }
    }

    private static async Task<ClipboardEntry?> ReadEntryAsync(DataPackageView view)
    {
        if (view.Contains(StandardDataFormats.StorageItems))
        {
            var items = await view.GetStorageItemsAsync();
            var paths = items.Select(item => item.Path).Where(path => !string.IsNullOrEmpty(path)).ToList();
            if (paths.Count > 0)
            {
                var joined = string.Join("\n", paths);
                return new ClipboardEntry
                {
                    Kind = KindFiles,
                    Content = joined,
                    Hash = "f:" + joined,
                    UtcTicks = DateTime.UtcNow.Ticks,
                };
            }
        }

        if (view.Contains(StandardDataFormats.Text))
        {
            var text = await view.GetTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (text.Length > MaxTextLength) text = text.Substring(0, MaxTextLength);
                return new ClipboardEntry
                {
                    Kind = KindText,
                    Content = text,
                    Hash = "t:" + text,
                    UtcTicks = DateTime.UtcNow.Ticks,
                };
            }
        }

        if (view.Contains(StandardDataFormats.Bitmap))
        {
            return await ReadImageAsync(view);
        }

        return null;
    }

    private static async Task<ClipboardEntry?> ReadImageAsync(DataPackageView view)
    {
        var reference = await view.GetBitmapAsync();
        using var source = await reference.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(source);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);
        var fileName = Guid.NewGuid().ToString("N") + ".png";
        var path = Path.Combine(ImageFolder, fileName);
        using (var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
        {
            using var output = file.AsRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
        }

        var hash = "i:" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        return new ClipboardEntry
        {
            Kind = KindImage,
            ImageFile = fileName,
            Hash = hash,
            UtcTicks = DateTime.UtcNow.Ticks,
        };
    }

    private void AddEntry(ClipboardEntry entry)
    {
        var existing = entries.FirstOrDefault(item => item.Hash == entry.Hash);
        if (existing != null)
        {
            if (entry.Kind == KindImage && entry.ImageFile != existing.ImageFile) DeleteImage(entry);
            entries.Remove(existing);
            existing.UtcTicks = entry.UtcTicks;
            entry = existing;
        }

        entries.Insert(0, entry);
        while (entries.Count > MaxItems)
        {
            var last = entries[^1];
            entries.RemoveAt(entries.Count - 1);
            DeleteImage(last);
        }

        Save();
        Rebuild();
    }

    private static void DeleteImage(ClipboardEntry entry)
    {
        if (entry.Kind != KindImage || string.IsNullOrEmpty(entry.ImageFile)) return;
        try
        {
            File.Delete(Path.Combine(ImageFolder, entry.ImageFile));
        }
        catch
        {
        }
    }

    private void ClearAll()
    {
        foreach (var entry in entries) DeleteImage(entry);
        entries.Clear();
        Save();
        Rebuild();
    }

    private void Remove(ClipboardEntry entry)
    {
        entries.Remove(entry);
        DeleteImage(entry);
        Save();
        Rebuild();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(JsonPath)) return;
            var loaded = JsonSerializer.Deserialize<List<ClipboardEntry>>(File.ReadAllText(JsonPath, Encoding.UTF8));
            if (loaded == null) return;
            foreach (var entry in loaded)
            {
                if (entry.Kind == KindImage && !File.Exists(Path.Combine(ImageFolder, entry.ImageFile))) continue;
                entries.Add(entry);
            }

            if (entries.Count > MaxItems) entries.RemoveRange(MaxItems, entries.Count - MaxItems);
        }
        catch
        {
            entries.Clear();
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(JsonPath, JsonSerializer.Serialize(entries), Encoding.UTF8);
        }
        catch
        {
        }
    }

    private static string Preview(ClipboardEntry entry)
    {
        if (entry.Kind == KindImage) return T("Image");

        if (entry.Kind == KindFiles)
        {
            var paths = entry.Content.Split('\n');
            var name = Path.GetFileName(paths[0].TrimEnd('\\', '/'));
            if (string.IsNullOrEmpty(name)) name = paths[0];
            return paths.Length > 1 ? name + "  +" + (paths.Length - 1) : name;
        }

        var flat = string.Join(" ", entry.Content.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        return flat.Length > 200 ? flat.Substring(0, 200) : flat;
    }

    private static string Relative(long utcTicks)
    {
        var then = new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime();
        var span = DateTime.Now - then;
        if (span.TotalSeconds < 60) return T("now");
        if (span.TotalMinutes < 60) return (int)span.TotalMinutes + " " + T("min");
        if (span.TotalHours < 24 && then.Date == DateTime.Today) return (int)span.TotalHours + " " + T("h");
        if (then.Date == DateTime.Today.AddDays(-1)) return T("Yesterday");
        return then.ToString("d MMM");
    }

    private void Rebuild()
    {
        copiedLabel = null;
        copiedEntry = null;
        listPanel.Children.Clear();

        var query = searchBox.Text.Trim();
        var visible = entries
            .Where(entry => query.Length == 0 || SearchText(entry).Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        clearButton.Visibility = entries.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        if (visible.Count == 0)
        {
            var empty = Ui.Text(T(entries.Count == 0 ? "Nothing copied yet" : "No matches"), 12, FontWeights.Normal, 110);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.Margin = new Thickness(0, 40, 0, 0);
            listPanel.Children.Add(empty);
            return;
        }

        foreach (var entry in visible) listPanel.Children.Add(BuildRow(entry));
    }

    private static string SearchText(ClipboardEntry entry) =>
        entry.Kind == KindImage ? T("Image") : entry.Content;

    private UIElement BuildRow(ClipboardEntry entry)
    {
        var row = new Border
        {
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(6, 4, 6, 4),
            Background = new SolidColorBrush(Colors.Transparent),
        };

        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });

        var iconBox = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(7),
            Background = Ui.White(22),
            VerticalAlignment = VerticalAlignment.Center,
        };
        iconBox.Child = BuildIcon(entry);
        grid.Children.Add(iconBox);

        var preview = Ui.Text(Preview(entry), 13, FontWeights.Normal, 230);
        preview.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(preview);
        Grid.SetColumn(preview, 1);

        var time = Ui.Text(Relative(entry.UtcTicks), 11, FontWeights.Normal, 120);
        time.HorizontalAlignment = HorizontalAlignment.Right;
        time.VerticalAlignment = VerticalAlignment.Center;

        var remove = Ui.IconButton("", 11, 24, () => Remove(entry));
        remove.HorizontalAlignment = HorizontalAlignment.Right;
        remove.VerticalAlignment = VerticalAlignment.Center;
        remove.Visibility = Visibility.Collapsed;
        remove.Tapped += (_, e) => e.Handled = true;
        ToolTipService.SetToolTip(remove, T("Remove"));

        var right = new Grid();
        right.Children.Add(time);
        right.Children.Add(remove);
        grid.Children.Add(right);
        Grid.SetColumn(right, 2);

        row.Child = grid;

        row.PointerEntered += (_, _) =>
        {
            row.Background = Ui.White(18);
            if (!ReferenceEquals(copiedLabel, time)) time.Visibility = Visibility.Collapsed;
            remove.Visibility = Visibility.Visible;
        };
        row.PointerExited += (_, _) =>
        {
            row.Background = new SolidColorBrush(Colors.Transparent);
            time.Visibility = Visibility.Visible;
            remove.Visibility = Visibility.Collapsed;
        };
        row.Tapped += async (_, _) =>
        {
            time.Visibility = Visibility.Visible;
            remove.Visibility = Visibility.Collapsed;
            if (await CopyBackAsync(entry)) ShowCopied(entry, time);
        };

        return row;
    }

    private UIElement BuildIcon(ClipboardEntry entry)
    {
        if (entry.Kind == KindImage)
        {
            var image = new Image { Stretch = Stretch.UniformToFill };
            try
            {
                image.Source = new BitmapImage(new Uri(Path.Combine(ImageFolder, entry.ImageFile))) { DecodePixelWidth = 64 };
            }
            catch
            {
            }

            return image;
        }

        var glyph = entry.Kind == KindFiles ? "" : "";
        var icon = Ui.Icon(glyph, 13, 200);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.VerticalAlignment = VerticalAlignment.Center;
        return icon;
    }

    private void ShowCopied(ClipboardEntry entry, TextBlock label)
    {
        RestoreCopiedLabel();
        copiedEntry = entry;
        copiedLabel = label;
        label.Text = T("Copied");
        label.Foreground = Ui.White(230);
        copiedTimer.Stop();
        copiedTimer.Start();
    }

    private void RestoreCopiedLabel()
    {
        copiedTimer.Stop();
        if (copiedLabel != null && copiedEntry != null)
        {
            copiedLabel.Text = Relative(copiedEntry.UtcTicks);
            copiedLabel.Foreground = Ui.White(120);
        }

        copiedLabel = null;
        copiedEntry = null;
    }

    private async Task<bool> CopyBackAsync(ClipboardEntry entry)
    {
        try
        {
            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };

            if (entry.Kind == KindText)
            {
                package.SetText(entry.Content);
            }
            else if (entry.Kind == KindImage)
            {
                var file = await StorageFile.GetFileFromPathAsync(Path.Combine(ImageFolder, entry.ImageFile));
                package.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
            }
            else
            {
                var items = new List<IStorageItem>();
                foreach (var path in entry.Content.Split('\n'))
                {
                    if (File.Exists(path)) items.Add(await StorageFile.GetFileFromPathAsync(path));
                    else if (Directory.Exists(path)) items.Add(await StorageFolder.GetFolderFromPathAsync(path));
                }

                if (items.Count == 0) return false;
                package.SetStorageItems(items);
            }

            ignoreUntilUtc = DateTime.UtcNow.AddMilliseconds(900);
            Clipboard.SetContent(package);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
