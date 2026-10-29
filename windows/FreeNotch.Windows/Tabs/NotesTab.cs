using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace FreeNotch;

public sealed class NotesTab : IslandTab
{
    private const string AddGlyph = "";
    private const string LockGlyph = "";
    private const string UnlockGlyph = "";
    private const string DeleteGlyph = "";

    private static readonly Dictionary<string, Dictionary<string, string>> Text = new()
    {
        ["tr"] = new()
        {
            ["Notes"] = "Notlar",
            ["New note"] = "Yeni not",
            ["Locked note"] = "Kilitli not",
            ["Empty note"] = "Boş not",
            ["Start typing..."] = "Yazmaya başla...",
            ["Create or select a note"] = "Bir not oluştur veya seç",
            ["Lock with password"] = "Parolayla kilitle",
            ["Lock now"] = "Şimdi kilitle",
            ["Remove lock"] = "Kilidi kaldır",
            ["Delete note"] = "Notu sil",
            ["Delete this note?"] = "Bu not silinsin mi?",
            ["Delete"] = "Sil",
            ["Cancel"] = "Vazgeç",
            ["Set a password"] = "Parola belirle",
            ["Password"] = "Parola",
            ["Repeat password"] = "Parolayı tekrarla",
            ["Enter a password"] = "Bir parola gir",
            ["Passwords do not match"] = "Parolalar eşleşmiyor",
            ["Lock"] = "Kilitle",
            ["This note is locked"] = "Bu not kilitli",
            ["Unlock"] = "Kilidi aç",
            ["Wrong password"] = "Yanlış parola",
        },
        ["de"] = new()
        {
            ["Notes"] = "Notizen",
            ["New note"] = "Neue Notiz",
            ["Locked note"] = "Gesperrte Notiz",
            ["Empty note"] = "Leere Notiz",
            ["Start typing..."] = "Schreib los...",
            ["Create or select a note"] = "Notiz erstellen oder auswählen",
            ["Lock with password"] = "Mit Passwort sperren",
            ["Lock now"] = "Jetzt sperren",
            ["Remove lock"] = "Sperre entfernen",
            ["Delete note"] = "Notiz löschen",
            ["Delete this note?"] = "Diese Notiz löschen?",
            ["Delete"] = "Löschen",
            ["Cancel"] = "Abbrechen",
            ["Set a password"] = "Passwort festlegen",
            ["Password"] = "Passwort",
            ["Repeat password"] = "Passwort wiederholen",
            ["Enter a password"] = "Passwort eingeben",
            ["Passwords do not match"] = "Passwörter stimmen nicht überein",
            ["Lock"] = "Sperren",
            ["This note is locked"] = "Diese Notiz ist gesperrt",
            ["Unlock"] = "Entsperren",
            ["Wrong password"] = "Falsches Passwort",
        },
        ["es"] = new()
        {
            ["Notes"] = "Notas",
            ["New note"] = "Nueva nota",
            ["Locked note"] = "Nota bloqueada",
            ["Empty note"] = "Nota vacía",
            ["Start typing..."] = "Empieza a escribir...",
            ["Create or select a note"] = "Crea o selecciona una nota",
            ["Lock with password"] = "Bloquear con contraseña",
            ["Lock now"] = "Bloquear ahora",
            ["Remove lock"] = "Quitar bloqueo",
            ["Delete note"] = "Eliminar nota",
            ["Delete this note?"] = "¿Eliminar esta nota?",
            ["Delete"] = "Eliminar",
            ["Cancel"] = "Cancelar",
            ["Set a password"] = "Establece una contraseña",
            ["Password"] = "Contraseña",
            ["Repeat password"] = "Repite la contraseña",
            ["Enter a password"] = "Introduce una contraseña",
            ["Passwords do not match"] = "Las contraseñas no coinciden",
            ["Lock"] = "Bloquear",
            ["This note is locked"] = "Esta nota está bloqueada",
            ["Unlock"] = "Desbloquear",
            ["Wrong password"] = "Contraseña incorrecta",
        },
        ["fr"] = new()
        {
            ["Notes"] = "Notes",
            ["New note"] = "Nouvelle note",
            ["Locked note"] = "Note verrouillée",
            ["Empty note"] = "Note vide",
            ["Start typing..."] = "Commencez à écrire...",
            ["Create or select a note"] = "Créez ou sélectionnez une note",
            ["Lock with password"] = "Verrouiller par mot de passe",
            ["Lock now"] = "Verrouiller maintenant",
            ["Remove lock"] = "Retirer le verrou",
            ["Delete note"] = "Supprimer la note",
            ["Delete this note?"] = "Supprimer cette note ?",
            ["Delete"] = "Supprimer",
            ["Cancel"] = "Annuler",
            ["Set a password"] = "Définir un mot de passe",
            ["Password"] = "Mot de passe",
            ["Repeat password"] = "Répéter le mot de passe",
            ["Enter a password"] = "Saisissez un mot de passe",
            ["Passwords do not match"] = "Les mots de passe ne correspondent pas",
            ["Lock"] = "Verrouiller",
            ["This note is locked"] = "Cette note est verrouillée",
            ["Unlock"] = "Déverrouiller",
            ["Wrong password"] = "Mot de passe incorrect",
        },
    };

    private static string T(string key) => Strings.Pick(Text, key);

    private readonly NotesStore store = new();
    private readonly DispatcherQueueTimer saveTimer;
    private readonly StackPanel listPanel = new() { Spacing = 2 };
    private readonly TextBox editor;
    private readonly Grid emptyPanel;
    private readonly StackPanel lockScreen;
    private readonly PasswordBox unlockBox;
    private readonly TextBlock unlockError;
    private readonly StackPanel setLockPanel;
    private readonly PasswordBox firstBox;
    private readonly PasswordBox secondBox;
    private readonly TextBlock setLockError;
    private readonly Button lockButton;
    private readonly Button removeLockButton;
    private readonly Button deleteButton;
    private readonly StackPanel confirmRow;
    private readonly StackPanel toolRow;

    private NoteItem? current;
    private bool settingLock;
    private bool confirmingDelete;
    private bool suppressChange;
    private bool dirty;
    private bool busy;
    private bool holding;

    public NotesTab(TabContext context) : base(context)
    {
        RequestedTheme = ElementTheme.Dark;
        Padding = new Thickness(22, 4, 22, 6);
        ColumnSpacing = 12;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        saveTimer = context.Queue.CreateTimer();
        saveTimer.Interval = TimeSpan.FromMilliseconds(600);
        saveTimer.IsRepeating = false;
        saveTimer.Tick += (_, _) => SaveNow();

        var left = new Grid { RowSpacing = 6 };
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = new Grid { Height = 28 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headerTitle = Ui.Text(T("Notes"), 13, FontWeights.SemiBold, 220);
        headerTitle.VerticalAlignment = VerticalAlignment.Center;
        headerTitle.Margin = new Thickness(4, 0, 0, 0);
        var addButton = Ui.IconButton(AddGlyph, 13, 28, AddNote);
        ToolTipService.SetToolTip(addButton, T("New note"));
        Grid.SetColumn(addButton, 1);
        header.Children.Add(headerTitle);
        header.Children.Add(addButton);
        var listScroll = new ScrollViewer
        {
            Content = listPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Grid.SetRow(listScroll, 1);
        left.Children.Add(header);
        left.Children.Add(listScroll);
        Children.Add(left);

        lockButton = Ui.IconButton(LockGlyph, 13, 28, LockClicked);
        removeLockButton = Ui.IconButton(UnlockGlyph, 13, 28, RemoveLock);
        deleteButton = Ui.IconButton(DeleteGlyph, 13, 28, () => SetConfirming(true));
        ToolTipService.SetToolTip(removeLockButton, T("Remove lock"));
        ToolTipService.SetToolTip(deleteButton, T("Delete note"));
        toolRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
        toolRow.Children.Add(lockButton);
        toolRow.Children.Add(removeLockButton);
        toolRow.Children.Add(deleteButton);

        var confirmText = Ui.Text(T("Delete this note?"), 12, FontWeights.Normal, 200);
        confirmText.VerticalAlignment = VerticalAlignment.Center;
        confirmRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, Visibility = Visibility.Collapsed };
        confirmRow.Children.Add(confirmText);
        confirmRow.Children.Add(TextButton(T("Delete"), () => DeleteCurrent()));
        confirmRow.Children.Add(TextButton(T("Cancel"), () => SetConfirming(false)));

        var toolbar = new Grid { Height = 28 };
        toolbar.Children.Add(toolRow);
        toolbar.Children.Add(confirmRow);

        editor = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Foreground = Ui.White(235),
            FontSize = 13,
            PlaceholderText = T("Start typing..."),
            Padding = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(10),
        };
        foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" })
        {
            editor.Resources[key] = new SolidColorBrush(Colors.Transparent);
        }
        editor.Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(0);
        ScrollViewer.SetVerticalScrollBarVisibility(editor, ScrollBarVisibility.Auto);
        editor.TextChanged += EditorTextChanged;
        TrackFocus(editor);

        emptyPanel = new Grid();
        var emptyText = Ui.Text(T("Create or select a note"), 12, FontWeights.Normal, 110);
        emptyText.HorizontalAlignment = HorizontalAlignment.Center;
        emptyText.VerticalAlignment = VerticalAlignment.Center;
        emptyPanel.Children.Add(emptyText);

        unlockBox = NewPasswordBox(T("Password"));
        unlockBox.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) Unlock(); };
        unlockError = NewErrorText();
        var lockIcon = Ui.Icon(LockGlyph, 22, 150);
        lockIcon.HorizontalAlignment = HorizontalAlignment.Center;
        var lockTitle = Ui.Text(T("This note is locked"), 13, FontWeights.SemiBold, 220);
        lockTitle.HorizontalAlignment = HorizontalAlignment.Center;
        lockScreen = new StackPanel
        {
            Spacing = 8,
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        lockScreen.Children.Add(lockIcon);
        lockScreen.Children.Add(lockTitle);
        lockScreen.Children.Add(unlockBox);
        lockScreen.Children.Add(unlockError);
        lockScreen.Children.Add(TextButton(T("Unlock"), Unlock, true));

        firstBox = NewPasswordBox(T("Password"));
        secondBox = NewPasswordBox(T("Repeat password"));
        secondBox.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) ApplyLock(); };
        setLockError = NewErrorText();
        var setLockTitle = Ui.Text(T("Set a password"), 13, FontWeights.SemiBold, 220);
        var setLockButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        setLockButtons.Children.Add(TextButton(T("Lock"), ApplyLock, true));
        setLockButtons.Children.Add(TextButton(T("Cancel"), CancelSetLock));
        setLockPanel = new StackPanel
        {
            Spacing = 8,
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        setLockPanel.Children.Add(setLockTitle);
        setLockPanel.Children.Add(firstBox);
        setLockPanel.Children.Add(secondBox);
        setLockPanel.Children.Add(setLockError);
        setLockPanel.Children.Add(setLockButtons);

        var surface = new Border
        {
            Background = Ui.White(14),
            CornerRadius = new CornerRadius(12),
            Child = new Grid(),
        };
        var surfaceGrid = (Grid)surface.Child;
        surfaceGrid.Children.Add(emptyPanel);
        surfaceGrid.Children.Add(editor);
        surfaceGrid.Children.Add(lockScreen);
        surfaceGrid.Children.Add(setLockPanel);
        Grid.SetRow(surface, 1);

        var right = new Grid { RowSpacing = 6 };
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.Children.Add(toolbar);
        right.Children.Add(surface);
        Grid.SetColumn(right, 1);
        Children.Add(right);

        store.Load();
        current = store.Items.FirstOrDefault();
        Refresh();
    }

    public override string Id => "notes";
    public override string Glyph => "";
    public override string Title => T("Notes");
    public override double ContentHeight => 240;

    public override void OnShown()
    {
        Refresh();
    }

    public override void OnHidden()
    {
        saveTimer.Stop();
        PruneEmpty();
        store.Save();
        dirty = false;
        foreach (var note in store.Items) note.Forget();
        settingLock = false;
        confirmingDelete = false;
        firstBox.Password = "";
        secondBox.Password = "";
        unlockBox.Password = "";
        Refresh();
        if (holding)
        {
            holding = false;
            Context.HoldOpen?.Invoke(false);
        }
    }

    private static Button TextButton(string label, Action action, bool accent = false)
    {
        var button = new Button
        {
            Content = Ui.Text(label, 12, FontWeights.Normal, 240),
            Padding = new Thickness(12, 4, 12, 4),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(0),
            Background = Ui.White((byte)(accent ? 46 : 22)),
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static TextBlock NewErrorText() => new()
    {
        Text = "",
        FontSize = 11,
        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(230, 255, 140, 130)),
        TextWrapping = TextWrapping.Wrap,
        Visibility = Visibility.Collapsed,
    };

    private PasswordBox NewPasswordBox(string placeholder)
    {
        var box = new PasswordBox { PlaceholderText = placeholder, FontSize = 13 };
        TrackFocus(box);
        return box;
    }

    private void TrackFocus(Control control)
    {
        control.GotFocus += (_, _) => UpdateHold();
        control.LostFocus += (_, _) => Context.Queue.TryEnqueue(UpdateHold);
    }

    private void UpdateHold()
    {
        var any = editor.FocusState != FocusState.Unfocused
            || unlockBox.FocusState != FocusState.Unfocused
            || firstBox.FocusState != FocusState.Unfocused
            || secondBox.FocusState != FocusState.Unfocused;
        if (any == holding) return;
        holding = any;
        Context.HoldOpen?.Invoke(any);
    }

    private static void ShowError(TextBlock block, string message)
    {
        block.Text = message;
        block.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Refresh()
    {
        BuildList();
        ShowCurrent();
    }

    private void BuildList()
    {
        listPanel.Children.Clear();
        foreach (var note in store.Items)
        {
            var selected = ReferenceEquals(note, current);
            var row = new Grid { ColumnSpacing = 6 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            string title;
            if (note.Locked) title = T("Locked note");
            else title = note.Title.Length == 0 ? T("Empty note") : note.Title;
            var titleBlock = Ui.Text(title, 12, selected ? FontWeights.SemiBold : FontWeights.Normal, (byte)(selected ? 245 : 190));
            titleBlock.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(titleBlock);
            FrameworkElement trailing = note.Locked
                ? Ui.Icon(LockGlyph, 11, 150)
                : Ui.Text(FormatDate(note.Modified), 10, FontWeights.Normal, 100);
            trailing.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(trailing, 1);
            row.Children.Add(trailing);
            var item = new Border
            {
                Child = row,
                Padding = new Thickness(10, 7, 10, 7),
                CornerRadius = new CornerRadius(8),
                Background = selected ? Ui.White(30) : new SolidColorBrush(Colors.Transparent),
            };
            var captured = note;
            item.Tapped += (_, _) => Select(captured);
            listPanel.Children.Add(item);
        }
    }

    private static string FormatDate(DateTime value) =>
        value.Date == DateTime.Today ? value.ToString("t") : value.ToString("d");

    private void ShowCurrent()
    {
        var note = current;
        var readable = note != null && note.IsReadable;
        var showEditor = note != null && !settingLock && readable;
        var showLockScreen = note != null && !settingLock && !readable;

        emptyPanel.Visibility = note == null ? Visibility.Visible : Visibility.Collapsed;
        editor.Visibility = showEditor ? Visibility.Visible : Visibility.Collapsed;
        lockScreen.Visibility = showLockScreen ? Visibility.Visible : Visibility.Collapsed;
        setLockPanel.Visibility = note != null && settingLock ? Visibility.Visible : Visibility.Collapsed;

        suppressChange = true;
        editor.Text = showEditor ? note!.Text : "";
        suppressChange = false;

        var hasNote = note != null;
        lockButton.Visibility = hasNote && readable && !settingLock ? Visibility.Visible : Visibility.Collapsed;
        removeLockButton.Visibility = hasNote && readable && note!.Locked && !settingLock ? Visibility.Visible : Visibility.Collapsed;
        deleteButton.Visibility = hasNote ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(lockButton, T(note != null && note.Locked ? "Lock now" : "Lock with password"));

        toolRow.Visibility = confirmingDelete ? Visibility.Collapsed : Visibility.Visible;
        confirmRow.Visibility = confirmingDelete && hasNote ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Select(NoteItem note)
    {
        if (ReferenceEquals(note, current)) return;
        FlushPending();
        var previous = current;
        if (previous != null && previous.Locked) previous.Forget();
        current = note;
        settingLock = false;
        confirmingDelete = false;
        ShowError(unlockError, "");
        unlockBox.Password = "";
        PruneEmpty(note);
        Refresh();
    }

    private void PruneEmpty(NoteItem? keep = null)
    {
        var empty = store.Items.Where(n => !n.Locked && n.Title.Length == 0 && !ReferenceEquals(n, keep)).ToList();
        if (empty.Count == 0) return;
        foreach (var note in empty)
        {
            store.Items.Remove(note);
            if (ReferenceEquals(note, current)) current = store.Items.FirstOrDefault();
        }
        dirty = true;
    }

    private void AddNote()
    {
        FlushPending();
        PruneEmpty();
        var previous = current;
        if (previous != null && previous.Locked) previous.Forget();
        var note = new NoteItem();
        store.Items.Insert(0, note);
        current = note;
        settingLock = false;
        confirmingDelete = false;
        Refresh();
        editor.Focus(FocusState.Programmatic);
    }

    private void EditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (suppressChange || current == null || !current.IsReadable) return;
        current.Text = editor.Text;
        current.Modified = DateTime.Now;
        MarkDirty();
        BuildList();
    }

    private void MarkDirty()
    {
        dirty = true;
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void FlushPending()
    {
        if (!dirty) return;
        saveTimer.Stop();
        SaveNow();
    }

    private void SaveNow()
    {
        saveTimer.Stop();
        store.Save();
        dirty = false;
    }

    private void SetConfirming(bool value)
    {
        confirmingDelete = value;
        ShowCurrent();
    }

    private void DeleteCurrent()
    {
        var note = current;
        confirmingDelete = false;
        if (note == null) return;
        var index = store.Items.IndexOf(note);
        note.Forget();
        store.Items.Remove(note);
        current = store.Items.Count == 0 ? null : store.Items[Math.Min(index, store.Items.Count - 1)];
        settingLock = false;
        SaveNow();
        Refresh();
    }

    private void LockClicked()
    {
        var note = current;
        if (note == null) return;
        if (note.Locked)
        {
            FlushPending();
            store.Save();
            note.Forget();
            ShowError(unlockError, "");
            unlockBox.Password = "";
            Refresh();
            return;
        }
        settingLock = true;
        firstBox.Password = "";
        secondBox.Password = "";
        ShowError(setLockError, "");
        ShowCurrent();
        firstBox.Focus(FocusState.Programmatic);
    }

    private void CancelSetLock()
    {
        settingLock = false;
        firstBox.Password = "";
        secondBox.Password = "";
        ShowCurrent();
    }

    private async void ApplyLock()
    {
        var note = current;
        if (note == null || busy) return;
        var password = firstBox.Password;
        if (password.Length == 0)
        {
            ShowError(setLockError, T("Enter a password"));
            return;
        }
        if (password != secondBox.Password)
        {
            ShowError(setLockError, T("Passwords do not match"));
            return;
        }
        busy = true;
        var salt = NotesCrypto.NewSalt();
        var key = await Task.Run(() => NotesCrypto.DeriveKey(password, salt));
        busy = false;
        note.Salt = Convert.ToBase64String(salt);
        note.Key = key;
        note.Locked = true;
        firstBox.Password = "";
        secondBox.Password = "";
        settingLock = false;
        SaveNow();
        Refresh();
    }

    private void RemoveLock()
    {
        var note = current;
        if (note == null || !note.IsReadable) return;
        if (note.Key != null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(note.Key);
        note.Key = null;
        note.Locked = false;
        note.Salt = null;
        note.Nonce = null;
        note.Tag = null;
        note.Cipher = null;
        SaveNow();
        Refresh();
    }

    private async void Unlock()
    {
        var note = current;
        if (note == null || busy || note.IsReadable) return;
        var password = unlockBox.Password;
        busy = true;
        var result = await Task.Run<(byte[]? Key, string Text)>(() =>
        {
            try
            {
                var salt = Convert.FromBase64String(note.Salt ?? "");
                var key = NotesCrypto.DeriveKey(password, salt);
                if (NotesCrypto.TryDecrypt(note, key, out var text)) return (key, text);
            }
            catch (FormatException)
            {
            }
            return (null, "");
        });
        busy = false;
        unlockBox.Password = "";
        if (result.Key == null)
        {
            ShowError(unlockError, T("Wrong password"));
            return;
        }
        ShowError(unlockError, "");
        note.Key = result.Key;
        note.Text = result.Text;
        if (ReferenceEquals(note, current)) Refresh();
        editor.Focus(FocusState.Programmatic);
    }
}
