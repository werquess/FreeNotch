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
    private readonly ShapeVisual shapes;
    private readonly CompositionRoundedRectangleGeometry geometry;
    private readonly CompositionSpriteShape sprite;
    private readonly CompositionPropertySet props;
    private readonly Grid shapeHost = new();
    private readonly Grid layerHost = new();
    private readonly List<FrameworkElement> layers = new();
    private FrameworkElement? activeLayer;
    private bool growsUp;

    public IslandView()
    {
        Background = new SolidColorBrush(Colors.Transparent);
        Children.Add(shapeHost);
        Children.Add(layerHost);

        compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        geometry = compositor.CreateRoundedRectangleGeometry();
        geometry.Size = new Vector2(10, 10);
        sprite = compositor.CreateSpriteShape(geometry);
        shapes = compositor.CreateShapeVisual();
        shapes.Shapes.Add(sprite);
        shapes.Opacity = 0;

        props = compositor.CreatePropertySet();
        props.InsertScalar("Top", 0f);
        props.InsertScalar("Radius", 15f);
        props.InsertScalar("Up", 0f);

        var offset = compositor.CreateExpressionAnimation(
            "Vector2((host.Size.X - geo.Size.X) * 0.5, props.Top + props.Up * (host.Size.Y - geo.Size.Y - props.Top))");
        offset.SetReferenceParameter("host", shapes);
        offset.SetReferenceParameter("geo", geometry);
        offset.SetReferenceParameter("props", props);
        geometry.StartAnimation("Offset", offset);

        var corner = compositor.CreateExpressionAnimation(
            "Vector2(Min(props.Radius, Min(geo.Size.X, geo.Size.Y) * 0.5), Min(props.Radius, Min(geo.Size.X, geo.Size.Y) * 0.5))");
        corner.SetReferenceParameter("geo", geometry);
        corner.SetReferenceParameter("props", props);
        geometry.StartAnimation("CornerRadius", corner);

        ElementCompositionPreview.SetElementChildVisual(shapeHost, shapes);
        shapeHost.SizeChanged += (_, e) => shapes.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
        ApplyTheme(IslandTheme.Smoke);
    }

    public Vector2 CurrentSize => geometry.Size;

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
        layer.VerticalAlignment = growsUp ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        layer.IsHitTestVisible = false;
        layerHost.Children.Add(layer);
        layers.Add(layer);
        ElementCompositionPreview.GetElementVisual(layer).Opacity = 0;
    }

    public void SetGrowsUp(bool up)
    {
        growsUp = up;
        props.InsertScalar("Up", up ? 1f : 0f);
        foreach (var layer in layers) layer.VerticalAlignment = up ? VerticalAlignment.Bottom : VerticalAlignment.Top;
    }

    public void SetTop(float top)
    {
        props.InsertScalar("Top", top);
        layerHost.Margin = new Thickness(0, growsUp ? 0 : top, 0, 0);
    }

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
        anim.InsertKeyFrame(1f, to, compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.7f), new Vector2(0.3f, 1f)));
        anim.Duration = TimeSpan.FromMilliseconds(durationMs);
        anim.DelayTime = TimeSpan.FromMilliseconds(delayMs);
        anim.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Opacity", anim);
    }

    public void Drop(float landTop, Vector2 pill, Action completed)
    {
        props.InsertScalar("Radius", 999f);
        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        var fall = Math.Max(landTop, 0f);
        var duration = TimeSpan.FromMilliseconds(Math.Clamp(640 + Math.Sqrt(fall) * 18, 640, 1150));

        var accelerate = compositor.CreateCubicBezierEasingFunction(new Vector2(0.55f, 0f), new Vector2(0.95f, 0.5f));
        var settle = compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.9f), new Vector2(0.3f, 1f));
        var linear = compositor.CreateLinearEasingFunction();

        var size = compositor.CreateVector2KeyFrameAnimation();
        size.InsertKeyFrame(0f, new Vector2(8, 8));
        size.InsertKeyFrame(0.12f, new Vector2(9, 16), settle);
        size.InsertKeyFrame(0.5f, new Vector2(10, 30), accelerate);
        size.InsertKeyFrame(0.6f, new Vector2(pill.X * 0.4f, pill.Y * 0.42f), linear);
        size.InsertKeyFrame(0.78f, new Vector2(pill.X * 1.06f, pill.Y * 0.9f), settle);
        size.InsertKeyFrame(0.9f, new Vector2(pill.X * 0.985f, pill.Y * 1.04f), settle);
        size.InsertKeyFrame(1f, pill, settle);
        size.Duration = duration;

        var top = compositor.CreateScalarKeyFrameAnimation();
        top.InsertKeyFrame(0f, -8f);
        top.InsertKeyFrame(0.12f, 0f, settle);
        top.InsertKeyFrame(0.5f, fall + pill.Y - 30f, accelerate);
        top.InsertKeyFrame(0.6f, fall + pill.Y * 0.58f, linear);
        top.InsertKeyFrame(0.78f, fall + pill.Y * 0.05f, settle);
        top.InsertKeyFrame(0.9f, fall - pill.Y * 0.02f, settle);
        top.InsertKeyFrame(1f, fall, settle);
        top.Duration = duration;

        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0f, 0f);
        opacity.InsertKeyFrame(0.1f, 1f, linear);
        opacity.InsertKeyFrame(1f, 1f, linear);
        opacity.Duration = duration;

        geometry.StartAnimation("Size", size);
        props.StartAnimation("Top", top);
        shapes.StartAnimation("Opacity", opacity);
        batch.End();
        batch.Completed += (_, _) =>
        {
            geometry.Size = pill;
            props.InsertScalar("Radius", pill.Y / 2);
            DispatcherQueue.TryEnqueue(() => completed());
        };
    }

    public void Lift(Action completed)
    {
        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        var duration = TimeSpan.FromMilliseconds(420);
        var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.5f, 0f), new Vector2(0.8f, 0.3f));
        var current = geometry.Size;

        props.InsertScalar("Radius", 999f);
        var size = compositor.CreateVector2KeyFrameAnimation();
        size.InsertKeyFrame(0.35f, new Vector2(current.Y * 0.9f, current.Y * 0.9f), ease);
        size.InsertKeyFrame(1f, new Vector2(4, 4), ease);
        size.Duration = duration;

        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0.6f, 1f);
        opacity.InsertKeyFrame(1f, 0f, ease);
        opacity.Duration = duration;

        geometry.StartAnimation("Size", size);
        shapes.StartAnimation("Opacity", opacity);
        batch.End();
        batch.Completed += (_, _) => DispatcherQueue.TryEnqueue(() => completed());
    }

    public void Morph(Vector2 size, float radius)
    {
        var springSize = compositor.CreateSpringVector2Animation();
        springSize.FinalValue = size;
        springSize.DampingRatio = 0.74f;
        springSize.Period = TimeSpan.FromMilliseconds(52);
        geometry.StartAnimation("Size", springSize);

        var springRadius = compositor.CreateSpringScalarAnimation();
        springRadius.FinalValue = radius;
        springRadius.DampingRatio = 0.9f;
        springRadius.Period = TimeSpan.FromMilliseconds(52);
        props.StartAnimation("Radius", springRadius);
        shapes.Opacity = 1;
    }
}
