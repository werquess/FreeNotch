using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FreeNotch;

public readonly record struct NotchShape(float Width, float Height, float Bottom, float Ear);

public sealed class IslandView : Grid
{
    private readonly Compositor compositor;
    private readonly ContainerVisual root;
    private readonly SpriteVisual arcBlur;
    private readonly ShapeVisual arcVisual;
    private readonly SpriteVisual bodyBlur;
    private readonly ShapeVisual bodyVisual;
    private readonly ShapeVisual glassVisual;
    private readonly CompositionEllipseGeometry ellipse;
    private readonly CompositionRoundedRectangleGeometry geometry;
    private readonly CompositionRectangleGeometry leftEar;
    private readonly CompositionRectangleGeometry rightEar;
    private readonly CompositionSpriteShape arcSprite;
    private readonly CompositionSpriteShape sprite;
    private readonly CompositionSpriteShape glassSprite;
    private readonly CompositionSpriteShape leftSprite;
    private readonly CompositionSpriteShape rightSprite;
    private readonly CompositionPropertySet props;
    private readonly Grid shapeHost = new();
    private readonly Grid layerHost = new();
    private readonly List<FrameworkElement> layers = new();
    private FrameworkElement? activeLayer;
    private readonly bool blurAvailable;
    private IslandTheme theme = IslandTheme.Smoke;

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
        props.InsertScalar("ArcY", 0f);

        root = compositor.CreateContainerVisual();

        ellipse = compositor.CreateEllipseGeometry();
        ellipse.Radius = Vector2.Zero;
        arcSprite = compositor.CreateSpriteShape(ellipse);
        arcSprite.StrokeThickness = 1;
        arcVisual = compositor.CreateShapeVisual();
        arcVisual.Shapes.Add(arcSprite);

        geometry = compositor.CreateRoundedRectangleGeometry();
        sprite = compositor.CreateSpriteShape(geometry);
        leftEar = compositor.CreateRectangleGeometry();
        rightEar = compositor.CreateRectangleGeometry();
        leftSprite = compositor.CreateSpriteShape(leftEar);
        rightSprite = compositor.CreateSpriteShape(rightEar);
        bodyVisual = compositor.CreateShapeVisual();
        bodyVisual.Shapes.Add(sprite);
        bodyVisual.Shapes.Add(leftSprite);
        bodyVisual.Shapes.Add(rightSprite);

        glassSprite = compositor.CreateSpriteShape(geometry);
        glassSprite.StrokeThickness = 1;
        glassVisual = compositor.CreateShapeVisual();
        glassVisual.Shapes.Add(glassSprite);

        arcBlur = compositor.CreateSpriteVisual();
        bodyBlur = compositor.CreateSpriteVisual();
        blurAvailable = false;

        root.Children.InsertAtTop(bodyBlur);
        root.Children.InsertAtTop(bodyVisual);
        root.Children.InsertAtTop(glassVisual);
        root.Children.InsertAtTop(arcBlur);
        root.Children.InsertAtTop(arcVisual);
        foreach (var v in AllVisuals) v.Opacity = 0;

        Bind(ellipse, "Center", "Vector2(host.Size.X * 0.5, props.ArcY)");
        Bind(geometry, "Size", "Vector2(props.W, props.H + props.R)");
        Bind(geometry, "Offset", "Vector2((host.Size.X - props.W) * 0.5, -props.R)");
        Bind(geometry, "CornerRadius", "Vector2(Min(props.R, props.W * 0.5), Min(props.R, (props.H + props.R) * 0.5))");
        Bind(leftEar, "Size", "Vector2(props.E, Min(props.E, props.H))");
        Bind(leftEar, "Offset", "Vector2((host.Size.X - props.W) * 0.5 - props.E, 0)");
        Bind(rightEar, "Size", "Vector2(props.E, Min(props.E, props.H))");
        Bind(rightEar, "Offset", "Vector2((host.Size.X + props.W) * 0.5, 0)");

        ElementCompositionPreview.SetElementChildVisual(shapeHost, root);
        shapeHost.SizeChanged += (_, e) =>
        {
            var size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
            root.Size = size;
            foreach (var v in AllVisuals) v.Size = size;
        };
        ApplyTheme(IslandTheme.Smoke);
    }

    private Visual[] AllVisuals => new Visual[] { arcBlur, arcVisual, bodyBlur, bodyVisual, glassVisual };

    private void Bind(CompositionObject target, string property, string expression)
    {
        var animation = compositor.CreateExpressionAnimation(expression);
        animation.SetReferenceParameter("host", root);
        animation.SetReferenceParameter("props", props);
        target.StartAnimation(property, animation);
    }

    private static Color Argb(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);

    private CompositionLinearGradientBrush Vertical(params (float At, Color Color)[] stops)
    {
        var brush = compositor.CreateLinearGradientBrush();
        brush.StartPoint = new Vector2(0, 0);
        brush.EndPoint = new Vector2(0, 1);
        foreach (var (at, color) in stops) brush.ColorStops.Add(compositor.CreateColorGradientStop(at, color));
        return brush;
    }

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

    public void ApplyTheme(IslandTheme next)
    {
        theme = next;
        var rim = compositor.CreateLinearGradientBrush();
        rim.StartPoint = new Vector2(0, 0);
        rim.EndPoint = new Vector2(1, 1);
        rim.ColorStops.Add(compositor.CreateColorGradientStop(0f, Argb(120, 255, 255, 255)));
        rim.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Argb(26, 255, 255, 255)));
        rim.ColorStops.Add(compositor.CreateColorGradientStop(1f, Argb(70, 255, 255, 255)));

        if (theme == IslandTheme.Oled)
        {
            var black = Argb(255, 0, 0, 0);
            sprite.FillBrush = compositor.CreateColorBrush(black);
            sprite.StrokeBrush = null;
            sprite.StrokeThickness = 0;
            leftSprite.FillBrush = Ear(new Vector2(0, 1), black);
            rightSprite.FillBrush = Ear(new Vector2(1, 1), black);
            arcSprite.FillBrush = compositor.CreateColorBrush(black);
            arcSprite.StrokeBrush = null;
            glassSprite.FillBrush = compositor.CreateColorBrush(black);
            glassSprite.StrokeBrush = null;
            bodyBlur.Opacity = 0;
            arcBlur.Opacity = 0;
            return;
        }

        var top = Argb(238, 10, 10, 13);
        sprite.FillBrush = Vertical((0f, top), (0.5f, Argb(228, 16, 16, 20)), (1f, Argb(214, 34, 33, 38)));
        sprite.StrokeBrush = Vertical((0f, Argb(0, 255, 255, 255)), (0.6f, Argb(20, 255, 255, 255)), (1f, Argb(64, 255, 255, 255)));
        sprite.StrokeThickness = 1;
        leftSprite.FillBrush = Ear(new Vector2(0, 1), top);
        rightSprite.FillBrush = Ear(new Vector2(1, 1), top);

        arcSprite.FillBrush = Vertical(
            (0f, Argb(18, 230, 230, 240)),
            (0.5f, Argb(40, 210, 210, 222)),
            (0.78f, Argb(140, 20, 20, 24)),
            (1f, Argb(236, 2, 2, 4)));
        arcSprite.StrokeBrush = rim;
        glassSprite.FillBrush = Vertical(
            (0f, Argb(46, 210, 210, 222)),
            (0.45f, Argb(150, 18, 18, 22)),
            (1f, Argb(238, 2, 2, 4)));
        glassSprite.StrokeBrush = rim;
        if (bodyVisual.Opacity > 0) bodyBlur.Opacity = Blur ? 1 : 0;
    }

    private bool Blur => blurAvailable && theme == IslandTheme.Smoke;

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
        foreach (var p in new[] { "W", "H", "R", "E" }) props.StopAnimation(p);
        props.InsertScalar("W", s.Width);
        props.InsertScalar("H", s.Height);
        props.InsertScalar("R", s.Bottom);
        props.InsertScalar("E", s.Ear);
        current = s;
    }

    private NotchShape current;

    private void ShowBody(bool on)
    {
        bodyVisual.StopAnimation("Opacity");
        bodyBlur.StopAnimation("Opacity");
        bodyVisual.Opacity = on ? 1 : 0;
        bodyBlur.Opacity = on && Blur ? 1 : 0;
        bodyShown = on;
    }

    private bool bodyShown;

    public void Reset()
    {
        ellipse.StopAnimation("Radius");
        props.StopAnimation("ArcY");
        Set(new NotchShape(0, 0, 14, 0));
        ellipse.Radius = Vector2.Zero;
        props.InsertScalar("ArcY", 0f);
        foreach (var v in AllVisuals)
        {
            v.StopAnimation("Opacity");
            v.Opacity = 0;
        }
        bodyShown = false;
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
        arcBlur.Opacity = Blur ? 1 : 0;

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

    public void Pour(Vector2 arc, NotchShape target, Action completed)
    {
        var d = TimeSpan.FromMilliseconds(680);
        var start = new NotchShape(arc.X * 0.6f, arc.Y * 1.15f, arc.Y * 1.15f, 0);
        Set(start);
        ShowBody(true);
        glassVisual.StopAnimation("Opacity");
        glassVisual.Opacity = 1;
        Run(() =>
        {
            var spread = Ease(0.22f, 1f, 0.36f, 1f);
            var pour = Ease(0.45f, 0f, 0.2f, 1f);
            var settle = Ease(0.25f, 0.9f, 0.3f, 1f);
            Key("W", d, (0.55f, target.Width * 1.012f, spread), (1f, target.Width, settle));
            Key("H", d, (0.12f, start.Height * 1.25f, Ease(0.4f, 0f, 0.6f, 1f)), (0.7f, target.Height * 1.03f, pour), (1f, target.Height, settle));
            Key("R", d, (0.6f, target.Bottom, settle), (1f, target.Bottom, null));
            Key("E", d, (0.35f, 0f, null), (0.75f, target.Ear, settle), (1f, target.Ear, null));

            var arcOut = compositor.CreateScalarKeyFrameAnimation();
            arcOut.InsertKeyFrame(0f, 1f);
            arcOut.InsertKeyFrame(1f, 0f, Ease(0.3f, 0f, 0.7f, 1f));
            arcOut.Duration = TimeSpan.FromMilliseconds(180);
            arcVisual.StartAnimation("Opacity", arcOut);
            if (Blur) arcBlur.StartAnimation("Opacity", arcOut);

            var glassOut = compositor.CreateScalarKeyFrameAnimation();
            glassOut.InsertKeyFrame(0f, 1f);
            glassOut.InsertKeyFrame(0.3f, 1f);
            glassOut.InsertKeyFrame(1f, 0f, Ease(0.4f, 0f, 0.6f, 1f));
            glassOut.Duration = d;
            glassVisual.StartAnimation("Opacity", glassOut);
        }, () =>
        {
            Set(target);
            arcVisual.Opacity = 0;
            arcBlur.Opacity = 0;
            glassVisual.Opacity = 0;
            completed();
        });
    }

    public void Drain(Vector2 arc, Action completed)
    {
        var d = TimeSpan.FromMilliseconds(520);
        var small = new NotchShape(arc.X * 0.55f, arc.Y, arc.Y, 0);
        Run(() =>
        {
            var gather = Ease(0.5f, 0f, 0.2f, 1f);
            Key("W", d, (0.7f, small.Width, gather), (1f, small.Width * 0.9f, null));
            Key("H", d, (0.7f, small.Height, gather), (1f, 0f, Ease(0.5f, 0f, 0.8f, 0.5f)));
            Key("R", d, (0.6f, small.Bottom, gather), (1f, small.Bottom, null));
            Key("E", d, (0.35f, 0f, gather), (1f, 0f, null));

            var glassIn = compositor.CreateScalarKeyFrameAnimation();
            glassIn.InsertKeyFrame(0f, 0f);
            glassIn.InsertKeyFrame(0.55f, 1f, Ease(0.4f, 0f, 0.6f, 1f));
            glassIn.InsertKeyFrame(1f, 1f);
            glassIn.Duration = d;
            glassVisual.StartAnimation("Opacity", glassIn);
        }, () =>
        {
            ShowBody(false);
            glassVisual.Opacity = 0;
            current = new NotchShape(0, 0, 14, 0);
            completed();
        });
    }

    public void Morph(NotchShape target)
    {
        if (!bodyShown || current.Height <= 0.5f)
        {
            Set(new NotchShape(target.Width * 0.72f, 0, target.Bottom, 0));
            ShowBody(true);
        }
        current = target;
        Spring("W", target.Width, 0.78f, 56);
        Spring("H", target.Height, 0.8f, 52);
        Spring("R", target.Bottom, 0.9f, 56);
        Spring("E", target.Ear, 0.9f, 56);
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
