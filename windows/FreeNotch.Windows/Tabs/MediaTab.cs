namespace FreeNotch;

public sealed class MediaTab : IslandTab
{
    private readonly MediaCard card;
    private bool shown;

    public MediaTab(TabContext context) : base(context)
    {
        card = new MediaCard(context.Media);
        Children.Add(card);
        context.Media.Changed += () => { if (shown) card.Update(); };
    }

    public override string Id => "media";
    public override string Glyph => "";
    public override string Title => Strings.L("Media");
    public override double ContentHeight => 150;

    public override void OnShown()
    {
        shown = true;
        card.SetActive(true);
    }

    public override void OnHidden()
    {
        shown = false;
        card.SetActive(false);
    }
}
