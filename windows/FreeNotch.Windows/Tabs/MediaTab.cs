using Microsoft.UI.Xaml;

namespace FreeNotch;

public sealed class MediaTab : IslandTab
{
    private readonly MediaCard card;
    private readonly LyricsTab lyrics;
    private readonly Microsoft.UI.Xaml.Controls.Button back;
    private bool shown;
    private bool showingLyrics;

    public MediaTab(TabContext context) : base(context)
    {
        card = new MediaCard(context.Media, () => SetLyrics(true));
        lyrics = new LyricsTab(context) { Visibility = Visibility.Collapsed };
        back = Ui.IconButton("music-note", 12, 28, () => SetLyrics(false));
        back.HorizontalAlignment = HorizontalAlignment.Right;
        back.VerticalAlignment = VerticalAlignment.Top;
        back.Margin = new Thickness(0, 0, 14, 0);
        back.Opacity = 0.75;
        back.Visibility = Visibility.Collapsed;
        Children.Add(card);
        Children.Add(lyrics);
        Children.Add(back);
        context.Media.Changed += () => { if (shown && !showingLyrics) card.Update(); };
    }

    public override string Id => "media";
    public override string Glyph => "music-note";
    public override string Title => showingLyrics ? Strings.L("Lyrics") : Strings.L("Media");
    public override double ContentHeight => 168;

    private void SetLyrics(bool on)
    {
        if (on == showingLyrics) return;
        showingLyrics = on;
        card.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        lyrics.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        back.Visibility = lyrics.Visibility;
        if (!shown) return;
        if (on)
        {
            card.SetActive(false);
            lyrics.OnShown();
        }
        else
        {
            lyrics.OnHidden();
            card.SetActive(true);
        }
    }

    public override void OnShown()
    {
        shown = true;
        if (showingLyrics) lyrics.OnShown();
        else card.SetActive(true);
    }

    public override void OnHidden()
    {
        shown = false;
        card.SetActive(false);
        lyrics.OnHidden();
    }
}
