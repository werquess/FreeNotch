using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace FreeNotch;

public sealed class ShelfTab : IslandTab
{
    private const double TileWidth = 76;

    private static readonly Dictionary<string, Dictionary<string, string>> Text = new()
    {
        ["Shelf"] = new() { ["tr"] = "Raf", ["de"] = "Ablage", ["es"] = "Estante", ["fr"] = "Étagère" },
        ["Clear"] = new() { ["tr"] = "Temizle", ["de"] = "Leeren", ["es"] = "Vaciar", ["fr"] = "Vider" },
        ["Drop files here"] = new() { ["tr"] = "Dosyaları buraya bırak", ["de"] = "Dateien hier ablegen", ["es"] = "Suelta los archivos aquí", ["fr"] = "Déposez les fichiers ici" },
        ["Drag files to the top of the screen to keep them here"] = new()
        {
            ["tr"] = "Dosyaları burada tutmak için ekranın üstüne sürükle",
            ["de"] = "Zieh Dateien an den oberen Bildschirmrand, um sie hier abzulegen",
            ["es"] = "Arrastra archivos a la parte superior de la pantalla para guardarlos aquí",
            ["fr"] = "Faites glisser des fichiers en haut de l'écran pour les garder ici",
        },
        ["item"] = new() { ["tr"] = "öğe", ["de"] = "Element", ["es"] = "elemento", ["fr"] = "élément" },
        ["items"] = new() { ["tr"] = "öğe", ["de"] = "Elemente", ["es"] = "elementos", ["fr"] = "éléments" },
        ["Add to shelf"] = new() { ["tr"] = "Rafa ekle", ["de"] = "Zur Ablage hinzufügen", ["es"] = "Añadir al estante", ["fr"] = "Ajouter à l'étagère" },
        ["Remove"] = new() { ["tr"] = "Kaldır", ["de"] = "Entfernen", ["es"] = "Quitar", ["fr"] = "Retirer" },
    };

    private static string T(string key) => Strings.Pick(Text, key);

    private sealed class ShelfEntry
    {
        public ShelfEntry(string path, bool isFolder)
        {
            Path = path;
            IsFolder = isFolder;
        }

        public string Path { get; }
        public bool IsFolder { get; }
        public IStorageItem? Item { get; set; }

        public async Task<IStorageItem?> ResolveAsync()
        {
            if (Item != null) return Item;
            try
            {
                if (IsFolder) Item = await StorageFolder.GetFolderFromPathAsync(Path);
                else Item = await StorageFile.GetFileFromPathAsync(Path);
            }
            catch
            {
                Item = null;
            }
            return Item;
        }
    }

    private readonly List<ShelfEntry> entries = new();
    private readonly StackPanel tileRow;
    private readonly ScrollViewer scroller;
    private readonly StackPanel emptyHint;
    private readonly TextBlock countText;
    private readonly Button clearButton;
    private readonly Grid dropOverlay;
    private bool holding;

    public ShelfTab(TabContext context) : base(context)
    {
        Padding = new Thickness(22, 4, 22, 6);
        Background = new SolidColorBrush(Colors.Transparent);
        AllowDrop = true;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        countText = Ui.Text("", 12, FontWeights.Medium, 150);
        countText.VerticalAlignment = VerticalAlignment.Center;

        clearButton = new Button
        {
            Content = Ui.Text(T("Clear"), 12, FontWeights.Medium, 200),
            Padding = new Thickness(10, 2, 10, 3),
            CornerRadius = new CornerRadius(8),
            Background = Ui.White(20),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Visibility = Visibility.Collapsed,
        };
        clearButton.Click += (_, _) => ClearAll();

        var header = new Grid { Height = 28 };
        header.Children.Add(countText);
        header.Children.Add(clearButton);
        SetRow(header, 0);
        Children.Add(header);

        tileRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        scroller = new ScrollViewer
        {
            Content = tileRow,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
        };
        SetRow(scroller, 1);
        Children.Add(scroller);

        var hintIcon = Ui.Icon("", 22, 110);
        hintIcon.HorizontalAlignment = HorizontalAlignment.Center;
        var hintText = Ui.Text(T("Drag files to the top of the screen to keep them here"), 12, FontWeights.Normal, 130);
        hintText.TextWrapping = TextWrapping.Wrap;
        hintText.MaxLines = 2;
        hintText.TextAlignment = TextAlignment.Center;
        emptyHint = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 380,
            IsHitTestVisible = false,
        };
        emptyHint.Children.Add(hintIcon);
        emptyHint.Children.Add(hintText);
        SetRow(emptyHint, 1);
        Children.Add(emptyHint);

        var zone = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            RadiusX = 12,
            RadiusY = 12,
            Stroke = Ui.White(170),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Fill = Ui.White(22),
        };
        var zoneContent = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var zoneIcon = Ui.Icon("", 24);
        zoneIcon.HorizontalAlignment = HorizontalAlignment.Center;
        zoneContent.Children.Add(zoneIcon);
        zoneContent.Children.Add(Ui.Text(T("Drop files here"), 14, FontWeights.SemiBold));
        dropOverlay = new Grid { Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        dropOverlay.Children.Add(zone);
        dropOverlay.Children.Add(zoneContent);
        SetRow(dropOverlay, 0);
        SetRowSpan(dropOverlay, 2);
        Children.Add(dropOverlay);

        LoadEntries();
        Rebuild();
    }

    public override string Id => "shelf";
    public override string Glyph => "tray";
    public override string Title => T("Shelf");
    public override double ContentHeight => 150;

    public override void OnShown()
    {
        var before = entries.Count;
        entries.RemoveAll(entry => !Exists(entry));
        if (entries.Count != before)
        {
            Save();
            Rebuild();
        }
    }

    public override void OnHidden()
    {
        dropOverlay.Visibility = Visibility.Collapsed;
    }

    private static string StorePath => Path.Combine(TabContext.DataFolder, "shelf.json");

    private static bool Exists(ShelfEntry entry) => entry.IsFolder ? Directory.Exists(entry.Path) : File.Exists(entry.Path);

    private void LoadEntries()
    {
        try
        {
            if (!File.Exists(StorePath)) return;
            var paths = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(StorePath));
            if (paths == null) return;
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                if (entries.Any(existing => string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
                if (Directory.Exists(path)) entries.Add(new ShelfEntry(path, true));
                else if (File.Exists(path)) entries.Add(new ShelfEntry(path, false));
            }
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
            File.WriteAllText(StorePath, JsonSerializer.Serialize(entries.Select(entry => entry.Path).ToList()));
        }
        catch
        {
        }
    }

    public static bool CarriesFiles(DragEventArgs e) => e.DataView.Contains(StandardDataFormats.StorageItems);

    public void HideDropZone() => dropOverlay.Visibility = Visibility.Collapsed;

    public void AcceptDrag(DragEventArgs e)
    {
        if (CarriesFiles(e))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = T("Add to shelf");
            e.DragUIOverride.IsGlyphVisible = true;
            dropOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
        e.Handled = true;
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        var changed = false;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (entries.Any(existing => string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
            if (Directory.Exists(path)) entries.Add(new ShelfEntry(path, true));
            else if (File.Exists(path)) entries.Add(new ShelfEntry(path, false));
            else continue;
            changed = true;
        }
        if (!changed) return;
        Save();
        Rebuild();
    }

    public async void AcceptDrop(DragEventArgs e)
    {
        dropOverlay.Visibility = Visibility.Collapsed;
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var deferral = e.GetDeferral();
        try
        {
            var dropped = await e.DataView.GetStorageItemsAsync();
            var changed = false;
            foreach (var item in dropped)
            {
                var path = item.Path;
                if (string.IsNullOrEmpty(path)) continue;
                if (entries.Any(existing => string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
                entries.Add(new ShelfEntry(path, item.IsOfType(StorageItemTypes.Folder)) { Item = item });
                changed = true;
            }
            if (changed)
            {
                Save();
                Rebuild();
            }
        }
        catch
        {
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void ClearAll()
    {
        entries.Clear();
        Save();
        Rebuild();
    }

    private void Remove(ShelfEntry entry)
    {
        entries.Remove(entry);
        Save();
        Rebuild();
    }

    private void Rebuild()
    {
        tileRow.Children.Clear();
        foreach (var entry in entries) tileRow.Children.Add(BuildTile(entry));

        var any = entries.Count > 0;
        scroller.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        emptyHint.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        clearButton.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        countText.Text = any ? $"{entries.Count} {T(entries.Count == 1 ? "item" : "items")}" : "";
    }

    private UIElement BuildTile(ShelfEntry entry)
    {
        var name = Path.GetFileName(entry.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(name)) name = entry.Path;

        var thumbBox = new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(10),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = Ui.Icon(entry.IsFolder ? "" : "", 26, 170),
        };

        var label = Ui.Text(name, 11, FontWeights.Normal, 210);
        label.TextWrapping = TextWrapping.Wrap;
        label.MaxLines = 2;
        label.TextAlignment = TextAlignment.Center;
        label.HorizontalAlignment = HorizontalAlignment.Center;

        var stack = new StackPanel { Spacing = 4, Padding = new Thickness(4, 6, 4, 4) };
        stack.Children.Add(thumbBox);
        stack.Children.Add(label);

        var removeButton = Ui.IconButton("", 9, 20, () => Remove(entry));
        removeButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(200, 40, 40, 44));
        removeButton.HorizontalAlignment = HorizontalAlignment.Right;
        removeButton.VerticalAlignment = VerticalAlignment.Top;
        removeButton.Margin = new Thickness(0, 2, 2, 0);
        removeButton.Visibility = Visibility.Collapsed;
        ToolTipService.SetToolTip(removeButton, T("Remove"));

        var tile = new Grid
        {
            Width = TileWidth,
            Margin = new Thickness(0, 0, 8, 0),
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Colors.Transparent),
            CanDrag = true,
        };
        tile.Children.Add(stack);
        tile.Children.Add(removeButton);
        ToolTipService.SetToolTip(tile, entry.Path);

        tile.PointerEntered += (_, _) =>
        {
            tile.Background = Ui.White(16);
            removeButton.Visibility = Visibility.Visible;
        };
        tile.PointerExited += (_, _) =>
        {
            tile.Background = new SolidColorBrush(Colors.Transparent);
            removeButton.Visibility = Visibility.Collapsed;
        };
        tile.DoubleTapped += (_, _) => OpenEntry(entry);
        tile.DragStarting += async (_, args) => await OnTileDragStarting(entry, args);
        tile.DropCompleted += (_, _) => ReleaseHold();

        LoadThumbnail(entry, thumbBox);
        return tile;
    }

    private async Task OnTileDragStarting(ShelfEntry entry, DragStartingEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            SetHold(true);
            var item = await entry.ResolveAsync();
            if (item == null)
            {
                args.Cancel = true;
                ReleaseHold();
                return;
            }
            args.Data.SetStorageItems(new[] { item }, false);
            args.Data.RequestedOperation = DataPackageOperation.Copy;
        }
        catch
        {
            args.Cancel = true;
            ReleaseHold();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void SetHold(bool hold)
    {
        if (holding == hold) return;
        holding = hold;
        Context.HoldOpen?.Invoke(hold);
    }

    private void ReleaseHold() => SetHold(false);

    private static void OpenEntry(ShelfEntry entry)
    {
        try
        {
            Process.Start(new ProcessStartInfo(entry.Path) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private async void LoadThumbnail(ShelfEntry entry, Border thumbBox)
    {
        try
        {
            var item = await entry.ResolveAsync();
            if (item == null) return;

            StorageItemThumbnail? thumbnail = null;
            if (item is StorageFile file) thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 96);
            else if (item is StorageFolder folder) thumbnail = await folder.GetThumbnailAsync(ThumbnailMode.SingleItem, 96);
            if (thumbnail == null) return;

            using (thumbnail)
            {
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(thumbnail);
                Context.Queue.TryEnqueue(() =>
                {
                    thumbBox.Child = new Image
                    {
                        Source = bitmap,
                        Stretch = Stretch.Uniform,
                        Width = 48,
                        Height = 48,
                    };
                });
            }
        }
        catch
        {
        }
    }
}
