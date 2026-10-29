using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FreeNotch;

public readonly record struct NotchShape(float Width, float Height, float Bottom, float Ear, float Gap = 0)
{
    public bool Floating => Gap > 0;
    public float Lift => Floating ? 0 : Bottom;
}

public sealed class IslandView : Grid
{
    private const float Ball = 40;
    private const float Drip = 15;

    private readonly Compositor compositor;
    private readonly ContainerVisual root;
    private readonly ShapeVisual arcVisual;
    private readonly LayerVisual bodyLayer;
    private readonly ShapeVisual bodyVisual;
    private readonly ShapeVisual glassVisual;
    private readonly DropShadow shadow;
    private readonly CompositionEllipseGeometry ellipse;
    private readonly CompositionRoundedRectangleGeometry geometry;
    private readonly CompositionRoundedRectangleGeometry inner;
    private readonly CompositionRectangleGeometry leftEar;
    private readonly CompositionRectangleGeometry rightEar;
    private readonly CompositionSpriteShape arcSprite;
    private readonly CompositionSpriteShape sprite;
    private readonly CompositionSpriteShape sheenSprite;
    private readonly CompositionSpriteShape rimSprite;
    private readonly CompositionSpriteShape innerSprite;
    private readonly CompositionSpriteShape glassSprite;
    private readonly CompositionSpriteShape leftSprite;
    private readonly CompositionSpriteShape rightSprite;
    private readonly CompositionPropertySet props;
    private readonly Grid shapeHost = new();
    private readonly Grid layerHost = new();
    private readonly List<FrameworkElement> layers = new();
    private FrameworkElement? activeLayer;
    private IslandTheme theme = IslandTheme.Smoke;
    private float shade = 0.5f;
    private bool arcShown;

    public IslandView()
    {
        Background = new SolidColorBrush(Colors.Transparent);
        Children.Add(shapeHost);
        Children.Add(layerHost);
        layerHost.VerticalAlignment = VerticalAlignment.Top;

        compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        props = compositor.CreatePropertySet();
        props.InsertScalar("W", 0f);
        props.InsertScalar("H", 0f);
        props.InsertScalar("R", 14f);
        props.InsertScalar("E", 0f);
        props.InsertScalar("Y", 0f);
        props.InsertScalar("T", 14f);
        props.InsertScalar("ArcY", 0f);

        root = compositor.CreateContainerVisual();

        ellipse = compositor.CreateEllipseGeometry();
        ellipse.Radius = Vector2.Zero;
        arcSprite = compositor.CreateSpriteShape(ellipse);
        arcSprite.StrokeThickness = 1;
        arcVisual = compositor.CreateShapeVisual();
        arcVisual.Shapes.Add(arcSprite);

        geometry = compositor.CreateRoundedRectangleGeometry();
        inner = compositor.CreateRoundedRectangleGeometry();
        sprite = compositor.CreateSpriteShape(geometry);
        sheenSprite = compositor.CreateSpriteShape(geometry);
        rimSprite = compositor.CreateSpriteShape(geometry);
        rimSprite.StrokeThickness = 1;
        innerSprite = compositor.CreateSpriteShape(inner);
        innerSprite.StrokeThickness = 1;
        leftEar = compositor.CreateRectangleGeometry();
        rightEar = compositor.CreateRectangleGeometry();
        leftSprite = compositor.CreateSpriteShape(leftEar);
        rightSprite = compositor.CreateSpriteShape(rightEar);
        bodyVisual = compositor.CreateShapeVisual();
        bodyVisual.Shapes.Add(sprite);
        bodyVisual.Shapes.Add(leftSprite);
        bodyVisual.Shapes.Add(rightSprite);
        bodyVisual.Shapes.Add(sheenSprite);
        bodyVisual.Shapes.Add(innerSprite);
        bodyVisual.Shapes.Add(rimSprite);

        glassSprite = compositor.CreateSpriteShape(geometry);
        glassSprite.StrokeThickness = 1;
        glassVisual = compositor.CreateShapeVisual();
        glassVisual.Shapes.Add(glassSprite);
        glassVisual.Opacity = 0;

        shadow = compositor.CreateDropShadow();
        shadow.BlurRadius = 28;
        shadow.Offset = new Vector3(0, 10, 0);
        shadow.Color = Colors.Black;
        shadow.Opacity = 0.5f;
        bodyLayer = compositor.CreateLayerVisual();
        bodyLayer.Children.InsertAtTop(bodyVisual);
        bodyLayer.Children.InsertAtTop(glassVisual);
        bodyLayer.Shadow = shadow;

        root.Children.InsertAtTop(bodyLayer);
        root.Children.InsertAtTop(arcVisual);
        arcVisual.Opacity = 0;
        bodyLayer.Opacity = 0;

        Bind(ellipse, "Center", "Vector2(host.Size.X * 0.5, props.ArcY)");
        Bind(geometry, "Size", "Vector2(props.W, props.H + props.T)");
        Bind(geometry, "Offset", "Vector2((host.Size.X - props.W) * 0.5, props.Y - props.T)");
        Bind(geometry, "CornerRadius", "Vector2(Min(props.R, props.W * 0.5), Min(props.R, (props.H + props.T) * 0.5))");
        Bind(inner, "Size", "Vector2(Max(props.W - 3, 0), Max(props.H + props.T - 3, 0))");
        Bind(inner, "Offset", "Vector2((host.Size.X - props.W) * 0.5 + 1.5, props.Y - props.T + 1.5)");
        Bind(inner, "CornerRadius", "Vector2(Max(Min(props.R, props.W * 0.5) - 1.5, 0), Max(Min(props.R, (props.H + props.T) * 0.5) - 1.5, 0))");
        Bind(leftEar, "Size", "Vector2(props.E, Min(props.E, props.H))");
        Bind(leftEar, "Offset", "Vector2((host.Size.X - props.W) * 0.5 - props.E, props.Y)");
        Bind(rightEar, "Size", "Vector2(props.E, Min(props.E, props.H))");
        Bind(rightEar, "Offset", "Vector2((host.Size.X + props.W) * 0.5, props.Y)");

        ElementCompositionPreview.SetElementChildVisual(shapeHost, root);
        shapeHost.SizeChanged += (_, e) =>
        {
            var size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
            root.Size = size;
            foreach (var v in new Visual[] { arcVisual, bodyLayer, bodyVisual, glassVisual }) v.Size = size;
        };
        ApplyTheme(IslandTheme.Smoke);
    }

    private void Bind(CompositionObject target, string property, string expression)
    {
        var animation = compositor.CreateExpressionAnimation(expression);
        animation.SetReferenceParameter("host", root);
        animation.SetReferenceParameter("props", props);
        target.StartAnimation(property, animation);
    }

    private static Color Argb(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);

    private static Color White(byte a) => Argb(a, 255, 255, 255);

    private CompositionLinearGradientBrush Gradient(Vector2 from, Vector2 to, params (float At, Color Color)[] stops)
    {
        var brush = compositor.CreateLinearGradientBrush();
        brush.StartPoint = from;
        brush.EndPoint = to;
        foreach (var (at, color) in stops) brush.ColorStops.Add(compositor.CreateColorGradientStop(at, color));
        return brush;
    }

    private CompositionLinearGradientBrush Vertical(params (float At, Color Color)[] stops) =>
        Gradient(new Vector2(0, 0), new Vector2(0, 1), stops);

    private CompositionRadialGradientBrush Ear(Vector2 center, Color color)
    {
        var brush = compositor.CreateRadialGradientBrush();
        brush.MappingMode = CompositionMappingMode.Relative;
        brush.EllipseCenter = center;
        brush.EllipseRadius = new Vector2(1, 1);
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Argb(0, color.R, color.G, color.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.93f, Argb(0, color.R, color.G, color.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(1f, color));
        return brush;
    }

    private CompositionRadialGradientBrush Sheen()
    {
        var brush = compositor.CreateRadialGradientBrush();
        brush.MappingMode = CompositionMappingMode.Relative;
        brush.EllipseCenter = new Vector2(0.5f, 0f);
        brush.EllipseRadius = new Vector2(0.8f, 0.62f);
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0f, White(30)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, White(9)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(1f, White(0)));
        return brush;
    }

    public void ApplyTheme(IslandTheme next)
    {
        theme = next;
        var rim = Vertical((0f, White(150)), (0.1f, White(70)), (0.45f, White(14)), (0.85f, White(26)), (1f, White(96)));

        if (theme == IslandTheme.Oled)
        {
            var black = Argb(255, 0, 0, 0);
            sprite.FillBrush = compositor.CreateColorBrush(black);
            leftSprite.FillBrush = Ear(new Vector2(0, 1), black);
            rightSprite.FillBrush = Ear(new Vector2(1, 1), black);
            sheenSprite.FillBrush = null;
            rimSprite.StrokeBrush = null;
            innerSprite.StrokeBrush = null;
            arcSprite.FillBrush = compositor.CreateColorBrush(black);
            arcSprite.StrokeBrush = null;
            glassSprite.FillBrush = compositor.CreateColorBrush(black);
            glassSprite.StrokeBrush = null;
            shade = 0.6f;
            Shade(current.Floating);
            return;
        }

        var top = Argb(236, 10, 10, 13);
        sprite.FillBrush = Vertical((0f, top), (0.55f, Argb(226, 16, 16, 20)), (0.92f, Argb(214, 32, 31, 36)), (1f, Argb(222, 46, 45, 52)));
        leftSprite.FillBrush = Ear(new Vector2(0, 1), top);
        rightSprite.FillBrush = Ear(new Vector2(1, 1), top);
        sheenSprite.FillBrush = Sheen();
        rimSprite.StrokeBrush = rim;
        innerSprite.StrokeBrush = Gradient(new Vector2(0, 0), new Vector2(1, 1),
            (0f, White(58)), (0.32f, White(0)), (0.7f, White(0)), (1f, White(36)));
        shade = 0.5f;
        Shade(current.Floating);

        arcSprite.FillBrush = Vertical(
            (0f, Argb(18, 230, 230, 240)),
            (0.5f, Argb(40, 210, 210, 222)),
            (0.78f, Argb(140, 20, 20, 24)),
            (1f, Argb(236, 2, 2, 4)));
        arcSprite.StrokeBrush = rim;
        glassSprite.FillBrush = Vertical(
            (0f, Argb(60, 220, 220, 232)),
            (0.45f, Argb(150, 18, 18, 22)),
            (1f, Argb(236, 4, 4, 6)));
        glassSprite.StrokeBrush = rim;
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

    public void ShowLayer(FrameworkElement? layer, Vector2 size, int delayMs)
    {
        if (activeLayer != null && activeLayer != layer)
        {
            activeLayer.IsHitTestVisible = false;
            Fade(ElementCompositionPreview.GetElementVisual(activeLayer), 0f, 0, 110);
        }
        activeLayer = layer;
        if (layer == null) return;
        layer.Width = size.X;
        layer.Height = size.Y;
        layer.IsHitTestVisible = true;
        Fade(ElementCompositionPreview.GetElementVisual(layer), 1f, delayMs, 240);
    }

    private void Fade(Visual visual, float to, int delayMs, int durationMs)
    {
        var anim = compositor.CreateScalarKeyFrameAnimation();
        anim.InsertKeyFrame(1f, to, Ease(0.2f, 0.7f, 0.3f, 1f));
        anim.Duration = TimeSpan.FromMilliseconds(Math.Max(durationMs, 1));
        anim.DelayTime = TimeSpan.FromMilliseconds(delayMs);
        anim.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Opacity", anim);
    }

    private CompositionEasingFunction Ease(float x1, float y1, float x2, float y2) =>
        compositor.CreateCubicBezierEasingFunction(new Vector2(x1, y1), new Vector2(x2, y2));

    private void Run(Action build, Action? completed)
    {
        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        build();
        batch.End();
        batch.Completed += (_, _) => DispatcherQueue.TryEnqueue(() => completed?.Invoke());
    }

    private void Key(string property, TimeSpan duration, params (float At, float Value, CompositionEasingFunction? Easing)[] keys)
    {
        var anim = compositor.CreateScalarKeyFrameAnimation();
        anim.InsertExpressionKeyFrame(0f, "this.StartingValue");
        foreach (var (at, value, easing) in keys)
        {
            if (easing == null) anim.InsertKeyFrame(at, value);
            else anim.InsertKeyFrame(at, value, easing);
        }
        anim.Duration = duration;
        props.StartAnimation(property, anim);
    }

    private void Spring(string property, float to, float damping, int periodMs)
    {
        var spring = compositor.CreateSpringScalarAnimation();
        spring.FinalValue = to;
        spring.DampingRatio = damping;
        spring.Period = TimeSpan.FromMilliseconds(periodMs);
        props.StartAnimation(property, spring);
    }

    private void Set(NotchShape s)
    {
        foreach (var p in new[] { "W", "H", "R", "E", "Y", "T" }) props.StopAnimation(p);
        props.InsertScalar("W", s.Width);
        props.InsertScalar("H", s.Height);
        props.InsertScalar("R", s.Bottom);
        props.InsertScalar("E", s.Ear);
        props.InsertScalar("Y", s.Gap);
        props.InsertScalar("T", s.Lift);
        current = s;
        Shade(s.Floating);
    }

    private void Shade(bool on) => shadow.Opacity = on ? shade : 0;

    private NotchShape current;

    private void ShowBody(bool on)
    {
        bodyLayer.StopAnimation("Opacity");
        bodyLayer.Opacity = on ? 1 : 0;
        bodyShown = on;
    }

    private bool bodyShown;

    private void Glass(TimeSpan d, params (float At, float Value)[] keys)
    {
        var anim = compositor.CreateScalarKeyFrameAnimation();
        foreach (var (at, value) in keys) anim.InsertKeyFrame(at, value, Ease(0.4f, 0f, 0.6f, 1f));
        anim.Duration = d;
        glassVisual.StartAnimation("Opacity", anim);
    }

    public void Reset()
    {
        ellipse.StopAnimation("Radius");
        props.StopAnimation("ArcY");
        Set(new NotchShape(0, 0, 14, 0));
        ellipse.Radius = Vector2.Zero;
        props.InsertScalar("ArcY", 0f);
        foreach (var v in new Visual[] { arcVisual, bodyLayer, glassVisual })
        {
            v.StopAnimation("Opacity");
            v.Opacity = 0;
        }
        bodyShown = false;
        arcShown = false;
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
        ellipse.Radius = new Vector2(arc.X * 0.1f, 0);
        arcVisual.Opacity = 1;
        arcShown = true;

        const float spreadMs = 420f;
        var total = spreadMs + Math.Max(swellMs, 0);
        var spread = compositor.CreateVector2KeyFrameAnimation();
        spread.InsertKeyFrame(spreadMs / total, new Vector2(arc.X * 0.5f, arc.Y), Ease(0.16f, 1f, 0.3f, 1f));
        if (swellMs > 0) spread.InsertKeyFrame(1f, new Vector2(arc.X * 0.53f, arc.Y * 1.32f), Ease(0.45f, 0f, 0.55f, 1f));
        spread.Duration = TimeSpan.FromMilliseconds(total);
        ellipse.StartAnimation("Radius", spread);
    }

    public void Retract(Vector2 arc, Action completed)
    {
        Run(() =>
        {
            var shrink = compositor.CreateVector2KeyFrameAnimation();
            shrink.InsertExpressionKeyFrame(0f, "this.StartingValue");
            shrink.InsertKeyFrame(1f, new Vector2(arc.X * 0.18f, 0), Ease(0.4f, 0f, 0.7f, 0.2f));
            shrink.Duration = TimeSpan.FromMilliseconds(300);
            ellipse.StartAnimation("Radius", shrink);
        }, completed);
    }

    private void ArcTo(TimeSpan d, params (float At, Vector2 Radius)[] keys)
    {
        var anim = compositor.CreateVector2KeyFrameAnimation();
        anim.InsertExpressionKeyFrame(0f, "this.StartingValue");
        foreach (var (at, radius) in keys) anim.InsertKeyFrame(at, radius, Ease(0.4f, 0f, 0.6f, 1f));
        anim.Duration = d;
        ellipse.StartAnimation("Radius", anim);
    }

    // A drop gathers under the top edge (or the live activity notch), lets go,
    // falls a little and spreads into the floating card.
    public void Fall(Vector2 arc, NotchShape target, Action completed)
    {
        var d = TimeSpan.FromMilliseconds(860);
        if (!bodyShown || current.Height <= 0.5f) Set(new NotchShape(Ball * 0.8f, Drip * 1.4f, Drip, 0));
        ShowBody(true);
        Shade(true);
        glassVisual.StopAnimation("Opacity");
        glassVisual.Opacity = 1;
        var g = target.Gap;
        Run(() =>
        {
            var stretch = Ease(0.45f, 0f, 0.55f, 1f);
            var pop = Ease(0.3f, 0f, 0.2f, 1f);
            var drop = Ease(0.5f, 0f, 0.9f, 0.6f);
            var spread = Ease(0.2f, 1f, 0.3f, 1f);
            var settle = Ease(0.3f, 0.9f, 0.3f, 1f);
            Key("W", d, (0.26f, Ball * 0.6f, stretch), (0.42f, Ball, pop), (0.74f, target.Width * 1.012f, spread), (1f, target.Width, settle));
            Key("H", d, (0.26f, Ball * 1.35f, stretch), (0.42f, Ball, pop), (0.78f, target.Height * 1.03f, spread), (1f, target.Height, settle));
            Key("R", d, (0.26f, Drip, stretch), (0.42f, Ball * 0.5f, pop), (0.8f, target.Bottom, settle), (1f, target.Bottom, null));
            Key("E", d, (0.2f, 0f, stretch), (1f, 0f, null));
            Key("T", d, (0.3f, Drip, stretch), (0.42f, 0f, pop), (1f, 0f, null));
            Key("Y", d, (0.3f, 0f, null), (0.48f, g + 12, drop), (0.82f, g - 1, spread), (1f, g, settle));

            if (arcShown)
            {
                ArcTo(d, (0.28f, new Vector2(arc.X * 0.34f, arc.Y * 0.9f)), (0.6f, new Vector2(arc.X * 0.1f, 0)));
                var arcOut = compositor.CreateScalarKeyFrameAnimation();
                arcOut.InsertKeyFrame(0.35f, 1f);
                arcOut.InsertKeyFrame(0.65f, 0f, Ease(0.3f, 0f, 0.7f, 1f));
                arcOut.Duration = d;
                arcVisual.StartAnimation("Opacity", arcOut);
            }

            Glass(d, (0f, 1f), (0.42f, 1f), (1f, 0f));
        }, () =>
        {
            Set(target);
            arcVisual.Opacity = 0;
            arcShown = false;
            ellipse.Radius = Vector2.Zero;
            glassVisual.Opacity = 0;
            completed();
        });
    }

    // The card pulls back into a drop, rises into the top edge and is gone.
    public void Drain(Vector2 arc, Action completed)
    {
        var d = TimeSpan.FromMilliseconds(620);
        var y = current.Gap;
        Run(() =>
        {
            var gather = Ease(0.5f, 0f, 0.2f, 1f);
            var rise = Ease(0.55f, 0f, 0.35f, 1f);
            Key("W", d, (0.45f, Ball, gather), (0.7f, Ball * 0.65f, rise), (1f, Ball * 0.4f, null));
            Key("H", d, (0.45f, Ball, gather), (0.7f, Drip * 1.6f, rise), (1f, 0f, Ease(0.5f, 0f, 0.8f, 0.5f)));
            Key("R", d, (0.45f, Ball * 0.5f, gather), (0.7f, Drip, rise), (1f, Drip, null));
            Key("E", d, (0.3f, 0f, gather), (1f, 0f, null));
            Key("Y", d, (0.45f, y + 4, gather), (0.7f, 0f, rise), (1f, 0f, null));
            Key("T", d, (0.5f, 0f, null), (0.7f, Drip, rise), (1f, Drip, null));

            ellipse.Radius = new Vector2(arc.X * 0.12f, 0);
            arcVisual.StopAnimation("Opacity");
            arcVisual.Opacity = 1;
            ArcTo(d, (0.6f, new Vector2(arc.X * 0.12f, 0)), (0.78f, new Vector2(arc.X * 0.3f, arc.Y * 0.8f)), (1f, new Vector2(arc.X * 0.08f, 0)));

            Glass(d, (0f, 0f), (0.5f, 1f), (1f, 1f));
        }, () =>
        {
            ShowBody(false);
            arcVisual.Opacity = 0;
            ellipse.Radius = Vector2.Zero;
            glassVisual.Opacity = 0;
            Set(new NotchShape(0, 0, 14, 0));
            completed();
        });
    }

    // The card pulls back into a drop that rises and melts into the live activity notch.
    public void Absorb(NotchShape target, Action completed)
    {
        var d = TimeSpan.FromMilliseconds(720);
        var y = current.Gap;
        Run(() =>
        {
            var gather = Ease(0.5f, 0f, 0.2f, 1f);
            var rise = Ease(0.55f, 0f, 0.35f, 1f);
            var spread = Ease(0.2f, 1f, 0.3f, 1f);
            Key("W", d, (0.4f, Ball, gather), (0.62f, Ball * 0.7f, rise), (1f, target.Width, spread));
            Key("H", d, (0.4f, Ball, gather), (0.62f, Drip * 2f, rise), (1f, target.Height, spread));
            Key("R", d, (0.4f, Ball * 0.5f, gather), (0.62f, Drip, rise), (1f, target.Bottom, spread));
            Key("E", d, (0.62f, 0f, null), (1f, target.Ear, spread));
            Key("Y", d, (0.4f, y + 4, gather), (0.62f, target.Gap, rise), (1f, target.Gap, null));
            Key("T", d, (0.45f, 0f, null), (0.62f, Math.Max(target.Lift, Drip), rise), (1f, target.Lift, null));

            Glass(d, (0f, 0f), (0.4f, 1f), (1f, 0f));
        }, () =>
        {
            Set(target);
            glassVisual.Opacity = 0;
            completed();
        });
    }

    public void Morph(NotchShape target)
    {
        if (!bodyShown || current.Height <= 0.5f)
        {
            Set(new NotchShape(target.Width * 0.72f, 0, target.Bottom, 0, target.Gap));
            ShowBody(true);
        }
        current = target;
        Shade(target.Floating);
        Spring("W", target.Width, 0.78f, 56);
        Spring("H", target.Height, 0.8f, 52);
        Spring("R", target.Bottom, 0.9f, 56);
        Spring("E", target.Ear, 0.9f, 56);
        Spring("Y", target.Gap, 0.9f, 56);
        Spring("T", target.Lift, 0.9f, 56);
    }

    public void Vanish(Action completed)
    {
        var d = TimeSpan.FromMilliseconds(320);
        var width = current.Width;
        Run(() =>
        {
            var ease = Ease(0.5f, 0f, 0.75f, 0.2f);
            Key("H", d, (1f, 0f, ease));
            Key("W", d, (1f, width * 0.7f, ease));
            Key("E", d, (0.5f, 0f, ease), (1f, 0f, null));
        }, () =>
        {
            ShowBody(false);
            current = new NotchShape(0, 0, 14, 0);
            completed();
        });
    }
}
