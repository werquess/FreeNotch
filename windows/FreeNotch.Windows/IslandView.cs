using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FreeNotch;

public sealed class IslandView : Grid
{
    private readonly Compositor compositor;
    private readonly ContainerVisual root;
    private readonly ShapeVisual arcVisual;
    private readonly ShapeVisual bodyVisual;
    private readonly ShapeVisual glowVisual;
    private readonly CompositionEllipseGeometry ellipse;
    private readonly CompositionRoundedRectangleGeometry geometry;
    private readonly CompositionSpriteShape sprite;
    private readonly CompositionPropertySet props;
    private readonly Grid shapeHost = new();
    private readonly Grid layerHost = new();
    private readonly List<FrameworkElement> layers = new();
    private FrameworkElement? activeLayer;

    public IslandView()
    {
        Background = new SolidColorBrush(Colors.Transparent);
        Children.Add(shapeHost);
        Children.Add(layerHost);
        layerHost.VerticalAlignment = VerticalAlignment.Top;

        compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        props = compositor.CreatePropertySet();
        props.InsertScalar("Top", 0f);
        props.InsertScalar("Radius", 999f);
        props.InsertScalar("ArcY", 0f);

        root = compositor.CreateContainerVisual();

        ellipse = compositor.CreateEllipseGeometry();
        ellipse.Radius = Vector2.Zero;
        var arcSprite = compositor.CreateSpriteShape(ellipse);
        arcSprite.FillBrush = GlassFill(true);
        arcSprite.StrokeBrush = GlassRim();
        arcSprite.StrokeThickness = 1;
        arcVisual = compositor.CreateShapeVisual();
        arcVisual.Shapes.Add(arcSprite);

        geometry = compositor.CreateRoundedRectangleGeometry();
        geometry.Size = Vector2.Zero;
        sprite = compositor.CreateSpriteShape(geometry);
        bodyVisual = compositor.CreateShapeVisual();
        bodyVisual.Shapes.Add(sprite);

        var glowSprite = compositor.CreateSpriteShape(geometry);
        glowSprite.FillBrush = GlassFill(false);
        glowSprite.StrokeBrush = GlassRim();
        glowSprite.StrokeThickness = 1;
        glowVisual = compositor.CreateShapeVisual();
        glowVisual.Shapes.Add(glowSprite);

        root.Children.InsertAtTop(bodyVisual);
        root.Children.InsertAtTop(glowVisual);
        root.Children.InsertAtTop(arcVisual);
        arcVisual.Opacity = 0;
        bodyVisual.Opacity = 0;
        glowVisual.Opacity = 0;

        var center = compositor.CreateExpressionAnimation("Vector2(host.Size.X * 0.5, props.ArcY)");
        center.SetReferenceParameter("host", arcVisual);
        center.SetReferenceParameter("props", props);
        ellipse.StartAnimation("Center", center);

        var offset = compositor.CreateExpressionAnimation("Vector2((host.Size.X - geo.Size.X) * 0.5, props.Top)");
        offset.SetReferenceParameter("host", bodyVisual);
        offset.SetReferenceParameter("geo", geometry);
        offset.SetReferenceParameter("props", props);
        geometry.StartAnimation("Offset", offset);

        var corner = compositor.CreateExpressionAnimation(
            "Vector2(Min(props.Radius, Min(geo.Size.X, geo.Size.Y) * 0.5), Min(props.Radius, Min(geo.Size.X, geo.Size.Y) * 0.5))");
        corner.SetReferenceParameter("geo", geometry);
        corner.SetReferenceParameter("props", props);
        geometry.StartAnimation("CornerRadius", corner);

        ElementCompositionPreview.SetElementChildVisual(shapeHost, root);
        shapeHost.SizeChanged += (_, e) =>
        {
            var size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
            root.Size = size;
            arcVisual.Size = size;
            bodyVisual.Size = size;
            glowVisual.Size = size;
        };
        ApplyTheme(IslandTheme.Smoke);
    }

    private CompositionLinearGradientBrush GlassFill(bool hanging)
    {
        var brush = compositor.CreateLinearGradientBrush();
        brush.StartPoint = new Vector2(0, 0);
        brush.EndPoint = new Vector2(0, 1);
        var start = hanging ? 0.5f : 0f;
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(28, 235, 235, 245)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(start, Color.FromArgb(46, 200, 200, 214)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(start + (1f - start) * 0.45f, Color.FromArgb(150, 22, 22, 26)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(246, 2, 2, 4)));
        return brush;
    }

    private CompositionLinearGradientBrush GlassRim()
    {
        var brush = compositor.CreateLinearGradientBrush();
        brush.StartPoint = new Vector2(0, 0);
        brush.EndPoint = new Vector2(1, 1);
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(150, 255, 255, 255)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.45f, Color.FromArgb(36, 255, 255, 255)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(90, 255, 255, 255)));
        return brush;
    }

    public void ApplyTheme(IslandTheme theme)
    {
        if (theme == IslandTheme.Oled)
        {
            sprite.FillBrush = compositor.CreateColorBrush(Color.FromArgb(255, 0, 0, 0));
            sprite.StrokeBrush = null;
            sprite.StrokeThickness = 0;
            return;
        }
        var fill = compositor.CreateLinearGradientBrush();
        fill.StartPoint = new Vector2(0, 0);
        fill.EndPoint = new Vector2(0, 1);
        fill.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(246, 6, 6, 8)));
        fill.ColorStops.Add(compositor.CreateColorGradientStop(0.55f, Color.FromArgb(236, 14, 14, 17)));
        fill.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(222, 40, 38, 40)));
        sprite.FillBrush = fill;

        var edge = compositor.CreateLinearGradientBrush();
        edge.StartPoint = new Vector2(0, 0);
        edge.EndPoint = new Vector2(0, 1);
        edge.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(34, 255, 255, 255)));
        edge.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(70, 255, 255, 255)));
        sprite.StrokeBrush = edge;
        sprite.StrokeThickness = 1;
    }

    public void AddLayer(FrameworkElement layer)
    {
        layer.HorizontalAlignment = HorizontalAlignment.Center;
        layer.VerticalAlignment = VerticalAlignment.Top;
        layer.IsHitTestVisible = false;
        layerHost.Children.Add(layer);
        layers.Add(layer);
        ElementCompositionPreview.GetElementVisual(layer).Opacity = 0;
    }

    public void ResizeLayer(FrameworkElement layer, Vector2 size)
    {
        layer.Width = size.X;
        layer.Height = size.Y;
    }

    public void Morph(Vector2 size)
    {
        var spring = compositor.CreateSpringVector2Animation();
        spring.FinalValue = size;
        spring.DampingRatio = 0.82f;
        spring.Period = TimeSpan.FromMilliseconds(58);
        geometry.StartAnimation("Size", spring);
    }

    public void SetLayerTop(float top) => layerHost.Margin = new Thickness(0, top, 0, 0);

    public void ShowLayer(FrameworkElement? layer, Vector2 size, int delayMs)
    {
        if (activeLayer != null && activeLayer != layer)
        {
            activeLayer.IsHitTestVisible = false;
            Fade(activeLayer, 0f, 0, 90);
        }
        activeLayer = layer;
        if (layer == null) return;
        layer.Width = size.X;
        layer.Height = size.Y;
        layer.IsHitTestVisible = true;
        Fade(layer, 1f, delayMs, 220);
    }

    private void Fade(UIElement element, float to, int delayMs, int durationMs)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var anim = compositor.CreateScalarKeyFrameAnimation();
        anim.InsertKeyFrame(1f, to, Ease(0.2f, 0.7f, 0.3f, 1f));
        anim.Duration = TimeSpan.FromMilliseconds(durationMs);
        anim.DelayTime = TimeSpan.FromMilliseconds(delayMs);
        anim.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Opacity", anim);
    }

    private CompositionEasingFunction Ease(float x1, float y1, float x2, float y2) =>
        compositor.CreateCubicBezierEasingFunction(new Vector2(x1, y1), new Vector2(x2, y2));

    private CompositionEasingFunction Step() => compositor.CreateStepEasingFunction(1);

    private void FadeVisual(Visual visual, TimeSpan duration, params (float At, float Value, bool Hold)[] keys)
    {
        var anim = compositor.CreateScalarKeyFrameAnimation();
        foreach (var (at, value, hold) in keys)
        {
            anim.InsertKeyFrame(at, value, hold ? Step() : compositor.CreateLinearEasingFunction());
        }
        anim.Duration = duration;
        visual.StartAnimation("Opacity", anim);
    }

    private void Run(TimeSpan duration, Action build, Action? completed)
    {
        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        build();
        batch.End();
        batch.Completed += (_, _) => DispatcherQueue.TryEnqueue(() => completed?.Invoke());
    }

    public void Reset()
    {
        ellipse.StopAnimation("Radius");
        geometry.StopAnimation("Size");
        props.StopAnimation("Top");
        props.StopAnimation("Radius");
        props.StopAnimation("ArcY");
        arcVisual.StopAnimation("Opacity");
        bodyVisual.StopAnimation("Opacity");
        glowVisual.StopAnimation("Opacity");
        ellipse.Radius = Vector2.Zero;
        geometry.Size = Vector2.Zero;
        props.InsertScalar("Top", 0f);
        props.InsertScalar("Radius", 999f);
        props.InsertScalar("ArcY", 0f);
        arcVisual.Opacity = 0;
        bodyVisual.Opacity = 0;
        glowVisual.Opacity = 0;
        foreach (var layer in layers)
        {
            layer.IsHitTestVisible = false;
            ElementCompositionPreview.GetElementVisual(layer).Opacity = 0;
        }
        activeLayer = null;
    }

    public void Peek(Vector2 arc, int swellMs)
    {
        Reset();
        ellipse.Radius = new Vector2(arc.X * 0.08f, 0);
        arcVisual.Opacity = 1;

        const float spreadMs = 380f;
        var total = spreadMs + Math.Max(swellMs, 0);
        var spread = compositor.CreateVector2KeyFrameAnimation();
        spread.InsertKeyFrame(spreadMs / total, new Vector2(arc.X * 0.5f, arc.Y), Ease(0.16f, 1f, 0.3f, 1f));
        if (swellMs > 0)
        {
            spread.InsertKeyFrame(1f, new Vector2(arc.X * 0.54f, arc.Y * 1.38f), Ease(0.4f, 0f, 0.6f, 1f));
        }
        spread.Duration = TimeSpan.FromMilliseconds(total);
        ellipse.StartAnimation("Radius", spread);
    }

    public void Retract(Vector2 arc, Action completed)
    {
        var duration = TimeSpan.FromMilliseconds(260);
        Run(duration, () =>
        {
            var shrink = compositor.CreateVector2KeyFrameAnimation();
            shrink.InsertExpressionKeyFrame(0f, "this.StartingValue");
            shrink.InsertKeyFrame(1f, new Vector2(arc.X * 0.16f, 0), Ease(0.5f, 0f, 0.75f, 0f));
            shrink.Duration = duration;
            ellipse.StartAnimation("Radius", shrink);
        }, completed);
    }

    public void Open(Vector2 arc, float landTop, Vector2 card, float radius, Action completed)
    {
        var d = TimeSpan.FromMilliseconds(960);
        geometry.StopAnimation("Size");
        arcVisual.Opacity = 1;
        Run(d, () =>
        {
            var settle = Ease(0.2f, 0.9f, 0.3f, 1f);
            var pinch = Ease(0.45f, 0f, 0.55f, 1f);
            var fall = Ease(0.55f, 0f, 0.95f, 0.55f);
            var linear = compositor.CreateLinearEasingFunction();

            var r = compositor.CreateVector2KeyFrameAnimation();
            r.InsertExpressionKeyFrame(0f, "this.StartingValue");
            r.InsertKeyFrame(0.22f, new Vector2(19, 30), settle);
            r.InsertKeyFrame(0.34f, new Vector2(13, 19), pinch);
            r.InsertKeyFrame(0.44f, new Vector2(11, 15), fall);
            r.InsertKeyFrame(1f, new Vector2(11, 15), linear);
            r.Duration = d;
            ellipse.StartAnimation("Radius", r);

            var cy = compositor.CreateScalarKeyFrameAnimation();
            cy.InsertExpressionKeyFrame(0f, "this.StartingValue");
            cy.InsertKeyFrame(0.22f, 0f, settle);
            cy.InsertKeyFrame(0.34f, 17f, pinch);
            cy.InsertKeyFrame(0.44f, 34f, fall);
            cy.InsertKeyFrame(1f, 34f, linear);
            cy.Duration = d;
            props.StartAnimation("ArcY", cy);

            var mid = landTop + card.Y * 0.5f;
            var size = compositor.CreateVector2KeyFrameAnimation();
            size.InsertKeyFrame(0f, new Vector2(22, 30));
            size.InsertKeyFrame(0.44f, new Vector2(22, 30), linear);
            size.InsertKeyFrame(0.6f, new Vector2(34, 42), fall);
            size.InsertKeyFrame(0.7f, new Vector2(card.X * 0.42f, card.Y * 0.36f), linear);
            size.InsertKeyFrame(0.86f, new Vector2(card.X * 1.025f, card.Y * 0.97f), settle);
            size.InsertKeyFrame(1f, card, settle);
            size.Duration = d;
            geometry.StartAnimation("Size", size);

            var top = compositor.CreateScalarKeyFrameAnimation();
            top.InsertKeyFrame(0f, 19f);
            top.InsertKeyFrame(0.44f, 19f, linear);
            top.InsertKeyFrame(0.6f, mid - 21f, fall);
            top.InsertKeyFrame(0.7f, landTop + card.Y * 0.32f, linear);
            top.InsertKeyFrame(0.86f, landTop + card.Y * 0.015f, settle);
            top.InsertKeyFrame(1f, landTop, settle);
            top.Duration = d;
            props.StartAnimation("Top", top);

            var round = compositor.CreateScalarKeyFrameAnimation();
            round.InsertKeyFrame(0f, 999f);
            round.InsertKeyFrame(0.6f, 999f, linear);
            round.InsertKeyFrame(0.7f, 60f, linear);
            round.InsertKeyFrame(1f, radius, settle);
            round.Duration = d;
            props.StartAnimation("Radius", round);

            FadeVisual(arcVisual, d, (0f, 1f, false), (0.44f, 1f, false), (0.441f, 0f, true), (1f, 0f, false));
            FadeVisual(glowVisual, d, (0f, 0f, false), (0.44f, 0f, false), (0.441f, 1f, true), (0.66f, 1f, false), (0.9f, 0f, false), (1f, 0f, false));
            FadeVisual(bodyVisual, d, (0f, 0f, false), (0.44f, 0f, false), (0.441f, 1f, true), (1f, 1f, false));
        }, () =>
        {
            geometry.Size = card;
            props.InsertScalar("Top", landTop);
            props.InsertScalar("Radius", radius);
            arcVisual.Opacity = 0;
            glowVisual.Opacity = 0;
            bodyVisual.Opacity = 1;
            completed();
        });
    }

    public void Close(Vector2 arc, float landTop, Action completed)
    {
        var d = TimeSpan.FromMilliseconds(640);
        Run(d, () =>
        {
            var shrink = Ease(0.5f, 0f, 0.3f, 1f);
            var rise = Ease(0.6f, 0f, 0.8f, 0.4f);
            var settle = Ease(0.2f, 0.9f, 0.3f, 1f);
            var linear = compositor.CreateLinearEasingFunction();

            var round = compositor.CreateScalarKeyFrameAnimation();
            round.InsertExpressionKeyFrame(0f, "this.StartingValue");
            round.InsertKeyFrame(0.3f, 999f, linear);
            round.InsertKeyFrame(1f, 999f, linear);
            round.Duration = d;
            props.StartAnimation("Radius", round);

            var size = compositor.CreateVector2KeyFrameAnimation();
            size.InsertExpressionKeyFrame(0f, "this.StartingValue");
            size.InsertKeyFrame(0.4f, new Vector2(40, 46), shrink);
            size.InsertKeyFrame(0.62f, new Vector2(22, 30), rise);
            size.InsertKeyFrame(1f, new Vector2(22, 30), linear);
            size.Duration = d;
            geometry.StartAnimation("Size", size);

            var top = compositor.CreateScalarKeyFrameAnimation();
            top.InsertExpressionKeyFrame(0f, "this.StartingValue");
            top.InsertKeyFrame(0.4f, landTop + 34f, shrink);
            top.InsertKeyFrame(0.62f, 8f, rise);
            top.InsertKeyFrame(1f, 8f, linear);
            top.Duration = d;
            props.StartAnimation("Top", top);

            ellipse.Radius = new Vector2(11, 15);
            props.InsertScalar("ArcY", 23f);
            var r = compositor.CreateVector2KeyFrameAnimation();
            r.InsertKeyFrame(0f, new Vector2(11, 15));
            r.InsertKeyFrame(0.62f, new Vector2(11, 15), linear);
            r.InsertKeyFrame(0.8f, new Vector2(18, 24), settle);
            r.InsertKeyFrame(1f, new Vector2(arc.X * 0.3f, 0), Ease(0.3f, 0f, 0.7f, 1f));
            r.Duration = d;
            ellipse.StartAnimation("Radius", r);

            var cy = compositor.CreateScalarKeyFrameAnimation();
            cy.InsertKeyFrame(0f, 23f);
            cy.InsertKeyFrame(0.62f, 23f, linear);
            cy.InsertKeyFrame(0.8f, 0f, rise);
            cy.InsertKeyFrame(1f, 0f, linear);
            cy.Duration = d;
            props.StartAnimation("ArcY", cy);

            FadeVisual(glowVisual, d, (0f, 0f, false), (0.25f, 0f, false), (0.55f, 1f, false), (0.62f, 1f, false), (0.621f, 0f, true), (1f, 0f, false));
            FadeVisual(bodyVisual, d, (0f, 1f, false), (0.62f, 1f, false), (0.621f, 0f, true), (1f, 0f, false));
            FadeVisual(arcVisual, d, (0f, 0f, false), (0.62f, 0f, false), (0.621f, 1f, true), (0.9f, 1f, false), (1f, 0f, false));
        }, completed);
    }

    public void NotchIn(Vector2 size, float radius)
    {
        Reset();
        props.InsertScalar("Radius", radius);
        props.InsertScalar("Top", -radius);
        geometry.Size = new Vector2(size.X * 0.35f, radius);
        bodyVisual.Opacity = 1;
        var spring = compositor.CreateSpringVector2Animation();
        spring.FinalValue = new Vector2(size.X, size.Y + radius);
        spring.DampingRatio = 0.72f;
        spring.Period = TimeSpan.FromMilliseconds(50);
        geometry.StartAnimation("Size", spring);
    }

    public void NotchOut(Vector2 size, float radius, Action completed)
    {
        var d = TimeSpan.FromMilliseconds(300);
        Run(d, () =>
        {
            var shrink = compositor.CreateVector2KeyFrameAnimation();
            shrink.InsertExpressionKeyFrame(0f, "this.StartingValue");
            shrink.InsertKeyFrame(1f, new Vector2(size.X * 0.3f, radius), Ease(0.5f, 0f, 0.75f, 0f));
            shrink.Duration = d;
            geometry.StartAnimation("Size", shrink);
        }, completed);
    }
}
