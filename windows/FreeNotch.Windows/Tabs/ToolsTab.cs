using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace FreeNotch;

public sealed class ToolsTab : IslandTab
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;
    private const uint EsDisplayRequired = 0x00000002;
    private const double FaceSize = 64;

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);

    private static readonly Dictionary<string, Dictionary<string, string>> Text = new()
    {
        ["tr"] = new()
        {
            ["Tools"] = "Araçlar",
            ["Mirror"] = "Ayna",
            ["Keep Awake"] = "Uyanık Tut",
            ["Color Picker"] = "Renk Seçici",
            ["On"] = "Açık",
            ["Off"] = "Kapalı",
            ["Starting..."] = "Başlatılıyor...",
            ["Live"] = "Canlı",
            ["No camera"] = "Kamera yok",
            ["Camera blocked in Privacy settings"] = "Kamera Gizlilik ayarlarında engelli",
            ["Pick a color"] = "Renk seç",
            ["Click to pick, Esc cancels"] = "Tıkla seç, Esc iptal",
            ["Pick failed"] = "Seçilemedi",
        },
        ["de"] = new()
        {
            ["Tools"] = "Werkzeuge",
            ["Mirror"] = "Spiegel",
            ["Keep Awake"] = "Wachhalten",
            ["Color Picker"] = "Farbwähler",
            ["On"] = "An",
            ["Off"] = "Aus",
            ["Starting..."] = "Wird gestartet...",
            ["Live"] = "Live",
            ["No camera"] = "Keine Kamera",
            ["Camera blocked in Privacy settings"] = "Kamera in den Datenschutzeinstellungen blockiert",
            ["Pick a color"] = "Farbe wählen",
            ["Click to pick, Esc cancels"] = "Klicken zum Wählen, Esc bricht ab",
            ["Pick failed"] = "Auswahl fehlgeschlagen",
        },
        ["es"] = new()
        {
            ["Tools"] = "Herramientas",
            ["Mirror"] = "Espejo",
            ["Keep Awake"] = "Mantener activo",
            ["Color Picker"] = "Selector de color",
            ["On"] = "Activado",
            ["Off"] = "Desactivado",
            ["Starting..."] = "Iniciando...",
            ["Live"] = "En vivo",
            ["No camera"] = "Sin cámara",
            ["Camera blocked in Privacy settings"] = "Cámara bloqueada en Privacidad",
            ["Pick a color"] = "Elegir un color",
            ["Click to pick, Esc cancels"] = "Clic para elegir, Esc cancela",
            ["Pick failed"] = "No se pudo elegir",
        },
        ["fr"] = new()
        {
            ["Tools"] = "Outils",
            ["Mirror"] = "Miroir",
            ["Keep Awake"] = "Rester éveillé",
            ["Color Picker"] = "Pipette",
            ["On"] = "Activé",
            ["Off"] = "Désactivé",
            ["Starting..."] = "Démarrage...",
            ["Live"] = "En direct",
            ["No camera"] = "Aucune caméra",
            ["Camera blocked in Privacy settings"] = "Caméra bloquée dans la confidentialité",
            ["Pick a color"] = "Choisir une couleur",
            ["Click to pick, Esc cancels"] = "Cliquez pour choisir, Échap annule",
            ["Pick failed"] = "Échec de la sélection",
        },
    };

    private static string T(string key) => Strings.Pick(Text, key);

    private static bool keepAwakeOn;

    public static bool KeepAwakeOn => keepAwakeOn;

    public static event Action? KeepAwakeChanged;

    private readonly Border mirrorFace;
    private readonly Grid mirrorHost;
    private readonly GlyphIcon mirrorIcon;
    private readonly TextBlock mirrorState;
    private readonly Border awakeFace;
    private readonly GlyphIcon awakeIcon;
    private readonly TextBlock awakeState;
    private readonly Border pickFace;
    private readonly GlyphIcon pickIcon;
    private readonly TextBlock pickState;
    private readonly ToolsColorPicker picker = new();
    private readonly DispatcherQueueTimer pickTimer;

    private MediaCapture? capture;
    private MediaPlayer? player;
    private MediaPlayerElement? previewElement;
    private int cameraToken;
    private bool cameraBusy;
    private bool picking;
    private string? pickedHex;
    private Windows.UI.Color pickedColor;

    public ToolsTab(TabContext context) : base(context)
    {
        Padding = new Thickness(22, 4, 22, 6);

        mirrorIcon = Ui.Icon("camera", 24, 230);
        mirrorHost = new Grid();
        mirrorFace = MakeFace(mirrorIcon, mirrorHost);
        mirrorState = MakeState(T("Off"));

        awakeIcon = Ui.Icon("coffee", 24, 230);
        awakeFace = MakeFace(awakeIcon, null);
        awakeState = MakeState(T("Off"));

        pickIcon = Ui.Icon("eyedropper", 24, 230);
        pickFace = MakeFace(pickIcon, null);
        pickState = MakeState(T("Pick a color"));

        var row = new Grid { VerticalAlignment = VerticalAlignment.Center, ColumnSpacing = 8 };
        for (var i = 0; i < 3; i++) row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        AddTile(row, 0, mirrorFace, T("Mirror"), mirrorState, OnMirrorTapped);
        AddTile(row, 1, awakeFace, T("Keep Awake"), awakeState, OnAwakeTapped);
        AddTile(row, 2, pickFace, T("Color Picker"), pickState, OnPickTapped);
        Children.Add(row);

        pickTimer = context.Queue.CreateTimer();
        pickTimer.Interval = TimeSpan.FromMilliseconds(30);
        pickTimer.Tick += (_, _) => OnPickTick();

        RefreshAwake();
    }

    public override string Id => "tools";
    public override string Glyph => "squares-four";
    public override string Title => T("Tools");
    public override double ContentHeight => 170;

    public override void OnShown()
    {
        RefreshAwake();
    }

    public override void OnHidden()
    {
        StopPicking(false);
        StopCamera();
    }

    private static Border MakeFace(GlyphIcon icon, Grid? host)
    {
        var inner = new Grid();
        if (host != null) inner.Children.Add(host);
        inner.Children.Add(icon);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.VerticalAlignment = VerticalAlignment.Center;
        var face = new Border
        {
            Width = FaceSize,
            Height = FaceSize,
            CornerRadius = new CornerRadius(FaceSize / 2),
            Background = Ui.White(22),
            BorderBrush = Ui.White(36),
            BorderThickness = new Thickness(1),
            Child = inner,
        };
        var visual = ElementCompositionPreview.GetElementVisual(face);
        var geometry = visual.Compositor.CreateEllipseGeometry();
        geometry.Center = new Vector2((float)FaceSize / 2, (float)FaceSize / 2);
        geometry.Radius = new Vector2((float)FaceSize / 2, (float)FaceSize / 2);
        visual.Clip = visual.Compositor.CreateGeometricClip(geometry);
        return face;
    }

    private static TextBlock MakeState(string text)
    {
        var state = Ui.Text(text, 11, FontWeights.Normal, 140);
        state.TextWrapping = TextWrapping.Wrap;
        state.MaxLines = 2;
        state.TextAlignment = TextAlignment.Center;
        return state;
    }

    private static void AddTile(Grid row, int column, Border face, string name, TextBlock state, Action action)
    {
        var title = Ui.Text(name, 13, FontWeights.SemiBold, 235);
        title.TextAlignment = TextAlignment.Center;
        var stack = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        face.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(face);
        stack.Children.Add(title);
        stack.Children.Add(state);
        var tile = new Grid
        {
            Background = Ui.White(0),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(6, 8, 6, 8),
        };
        tile.Children.Add(stack);
        tile.PointerEntered += (_, _) => tile.Background = Ui.White(14);
        tile.PointerExited += (_, _) => tile.Background = Ui.White(0);
        tile.Tapped += (_, e) =>
        {
            e.Handled = true;
            action();
        };
        Grid.SetColumn(tile, column);
        row.Children.Add(tile);
    }

    private void RefreshAwake()
    {
        awakeState.Text = keepAwakeOn ? T("On") : T("Off");
        awakeFace.Background = Ui.White((byte)(keepAwakeOn ? 80 : 22));
        awakeIcon.Foreground = Ui.White((byte)(keepAwakeOn ? 255 : 200));
    }

    private void OnAwakeTapped()
    {
        keepAwakeOn = !keepAwakeOn;
        try
        {
            SetThreadExecutionState(keepAwakeOn
                ? EsContinuous | EsSystemRequired | EsDisplayRequired
                : EsContinuous);
        }
        catch
        {
        }
        RefreshAwake();
        KeepAwakeChanged?.Invoke();
    }

    private void OnMirrorTapped()
    {
        if (cameraBusy) return;
        if (capture != null || player != null)
        {
            StopCamera();
            return;
        }
        _ = StartCameraAsync();
    }

    private async Task StartCameraAsync()
    {
        var token = ++cameraToken;
        cameraBusy = true;
        mirrorState.Text = T("Starting...");
        MediaCapture? newCapture = null;
        try
        {
            var groups = await MediaFrameSourceGroup.FindAllAsync();
            MediaFrameSourceGroup? chosen = null;
            foreach (var group in groups)
            {
                if (group.SourceInfos.Any(info => info.SourceKind == MediaFrameSourceKind.Color))
                {
                    chosen = group;
                    break;
                }
            }
            if (token != cameraToken) return;
            if (chosen == null)
            {
                mirrorState.Text = T("No camera");
                return;
            }

            newCapture = new MediaCapture();
            await newCapture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                SourceGroup = chosen,
                SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
            });
            if (token != cameraToken)
            {
                newCapture.Dispose();
                newCapture = null;
                return;
            }

            MediaFrameSource? frameSource = null;
            foreach (var source in newCapture.FrameSources.Values)
            {
                if (source.Info.SourceKind == MediaFrameSourceKind.Color)
                {
                    frameSource = source;
                    break;
                }
            }
            if (frameSource == null)
            {
                newCapture.Dispose();
                newCapture = null;
                mirrorState.Text = T("No camera");
                return;
            }

            var newPlayer = new MediaPlayer
            {
                Source = MediaSource.CreateFromMediaFrameSource(frameSource),
                RealTimePlayback = true,
                IsMuted = true,
            };
            var element = new MediaPlayerElement
            {
                AreTransportControlsEnabled = false,
                Stretch = Stretch.UniformToFill,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new ScaleTransform { ScaleX = -1, ScaleY = 1 },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            element.SetMediaPlayer(newPlayer);
            mirrorHost.Children.Add(element);
            mirrorIcon.Visibility = Visibility.Collapsed;
            capture = newCapture;
            newCapture = null;
            player = newPlayer;
            previewElement = element;
            newPlayer.Play();
            mirrorState.Text = T("Live");
        }
        catch (UnauthorizedAccessException)
        {
            mirrorState.Text = T("Camera blocked in Privacy settings");
        }
        catch
        {
            mirrorState.Text = T("No camera");
        }
        finally
        {
            if (newCapture != null)
            {
                try
                {
                    newCapture.Dispose();
                }
                catch
                {
                }
            }
            cameraBusy = false;
        }
    }

    private void StopCamera()
    {
        cameraToken++;
        var oldPlayer = player;
        var oldCapture = capture;
        var oldElement = previewElement;
        player = null;
        capture = null;
        previewElement = null;
        try
        {
            if (oldElement != null)
            {
                oldElement.SetMediaPlayer(null);
                mirrorHost.Children.Remove(oldElement);
            }
            oldPlayer?.Pause();
            oldPlayer?.Dispose();
        }
        catch
        {
        }
        try
        {
            oldCapture?.Dispose();
        }
        catch
        {
        }
        mirrorIcon.Visibility = Visibility.Visible;
        if (!cameraBusy) mirrorState.Text = T("Off");
    }

    private void OnPickTapped()
    {
        if (picking)
        {
            StopPicking(false);
            return;
        }
        picking = true;
        Context.HoldOpen?.Invoke(true);
        if (!picker.Start())
        {
            picking = false;
            Context.HoldOpen?.Invoke(false);
            pickState.Text = T("Pick failed");
            return;
        }
        pickState.Text = T("Click to pick, Esc cancels");
        pickTimer.Start();
    }

    private void OnPickTick()
    {
        if (!picking)
        {
            pickTimer.Stop();
            return;
        }
        try
        {
            if (picker.EscapePressed())
            {
                StopPicking(false);
                return;
            }
            if (picker.ReadCursorColor(out var r, out var g, out var b))
            {
                pickFace.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
            }
            if (picker.TryTakePick(out var x, out var y))
            {
                if (picker.ReadPixel(x, y, out r, out g, out b))
                {
                    CompletePick(r, g, b);
                }
                else
                {
                    StopPicking(false);
                    pickState.Text = T("Pick failed");
                }
            }
        }
        catch
        {
            StopPicking(false);
            pickState.Text = T("Pick failed");
        }
    }

    private void CompletePick(byte r, byte g, byte b)
    {
        var hex = $"#{r:X2}{g:X2}{b:X2}";
        pickedHex = hex;
        pickedColor = Windows.UI.Color.FromArgb(255, r, g, b);
        StopPicking(true);
        try
        {
            var package = new DataPackage();
            package.SetText(hex);
            Clipboard.SetContent(package);
        }
        catch
        {
        }
    }

    private void StopPicking(bool picked)
    {
        pickTimer.Stop();
        picker.Stop();
        var wasPicking = picking;
        picking = false;
        if (wasPicking) Context.HoldOpen?.Invoke(false);
        if (picked && pickedHex != null)
        {
            pickFace.Background = new SolidColorBrush(pickedColor);
            pickState.Text = pickedHex;
        }
        else
        {
            pickFace.Background = Ui.White(22);
            pickState.Text = pickedHex ?? T("Pick a color");
            if (pickedHex != null) pickFace.Background = new SolidColorBrush(pickedColor);
        }
    }
}
