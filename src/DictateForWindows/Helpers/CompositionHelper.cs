using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace DictateForWindows.Helpers;

/// <summary>
/// Factory for Composition API animations used by the Orb UI.
/// </summary>
internal static class CompositionHelper
{
    /// <summary>
    /// Gets the Compositor and Visual for a UIElement.
    /// </summary>
    public static (Compositor compositor, Visual visual) GetCompositionPair(UIElement element)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        return (visual.Compositor, visual);
    }

    /// <summary>
    /// Signature cubic bezier: quick start, smooth overshoot landing.
    /// </summary>
    public static CubicBezierEasingFunction CreateOrbEasing(Compositor c)
        => c.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.9f), new Vector2(0.3f, 1.0f));

    /// <summary>
    /// Signature ease-in for exits (implosion, dissolve).
    /// </summary>
    public static CubicBezierEasingFunction CreateExitEasing(Compositor c)
        => c.CreateCubicBezierEasingFunction(new Vector2(0.7f, 0f), new Vector2(1f, 0.5f));

    /// <summary>
    /// Smooth ease-out for dissolve/cancel.
    /// </summary>
    public static CubicBezierEasingFunction CreateDissolveEasing(Compositor c)
        => c.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0f), new Vector2(0.2f, 1f));

    /// <summary>
    /// Play appear animation: scale 0→1, opacity 0→1.
    /// </summary>
    public static void PlayAppear(UIElement element, TimeSpan? duration = null)
    {
        var (c, visual) = GetCompositionPair(element);
        var dur = duration ?? TimeSpan.FromMilliseconds(220);
        var easing = CreateOrbEasing(c);

        // Scale
        var scaleAnim = c.CreateVector3KeyFrameAnimation();
        scaleAnim.InsertKeyFrame(0f, new Vector3(0f, 0f, 1f));
        scaleAnim.InsertKeyFrame(1f, new Vector3(1f, 1f, 1f), easing);
        scaleAnim.Duration = dur;

        // Opacity
        var opacityAnim = c.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0f, 0f);
        opacityAnim.InsertKeyFrame(1f, 1f, easing);
        opacityAnim.Duration = dur;

        // Set center point for scale
        visual.CenterPoint = new Vector3(visual.Size.X / 2f, visual.Size.Y / 2f, 0f);

        visual.StartAnimation("Scale", scaleAnim);
        visual.StartAnimation("Opacity", opacityAnim);
    }

    /// <summary>
    /// Play implosion animation: scale 1→0.2, opacity 1→0, then shockwave on a second element.
    /// </summary>
    public static void PlayImplosion(UIElement orbElement, UIElement? shockwaveElement = null, Action? onComplete = null)
    {
        var (c, visual) = GetCompositionPair(orbElement);
        var easing = CreateExitEasing(c);

        // Implosion: scale 1→0.2, opacity 1→0
        var scaleAnim = c.CreateVector3KeyFrameAnimation();
        scaleAnim.InsertKeyFrame(0f, new Vector3(1f, 1f, 1f));
        scaleAnim.InsertKeyFrame(1f, new Vector3(0.2f, 0.2f, 1f), easing);
        scaleAnim.Duration = TimeSpan.FromMilliseconds(180);

        var opacityAnim = c.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0f, 1f);
        opacityAnim.InsertKeyFrame(1f, 0f, easing);
        opacityAnim.Duration = TimeSpan.FromMilliseconds(180);

        visual.CenterPoint = new Vector3(visual.Size.X / 2f, visual.Size.Y / 2f, 0f);

        // Use a scoped batch to detect completion
        var batch = c.CreateScopedBatch(CompositionBatchTypes.Animation);
        visual.StartAnimation("Scale", scaleAnim);
        visual.StartAnimation("Opacity", opacityAnim);

        // Shockwave ring
        if (shockwaveElement != null)
        {
            var (_, swVisual) = GetCompositionPair(shockwaveElement);
            swVisual.CenterPoint = new Vector3(swVisual.Size.X / 2f, swVisual.Size.Y / 2f, 0f);

            var swScale = c.CreateVector3KeyFrameAnimation();
            swScale.InsertKeyFrame(0f, new Vector3(0.2f, 0.2f, 1f));
            swScale.InsertKeyFrame(1f, new Vector3(2.5f, 2.5f, 1f), easing);
            swScale.Duration = TimeSpan.FromMilliseconds(350);
            swScale.DelayTime = TimeSpan.FromMilliseconds(100);

            var swOpacity = c.CreateScalarKeyFrameAnimation();
            swOpacity.InsertKeyFrame(0f, 0.5f);
            swOpacity.InsertKeyFrame(1f, 0f, easing);
            swOpacity.Duration = TimeSpan.FromMilliseconds(350);
            swOpacity.DelayTime = TimeSpan.FromMilliseconds(100);

            swVisual.StartAnimation("Scale", swScale);
            swVisual.StartAnimation("Opacity", swOpacity);
        }

        batch.End();
        batch.Completed += (_, _) => onComplete?.Invoke();
    }

    /// <summary>
    /// Play dissolve/cancel animation: scale 1→0.85, opacity 1→0.
    /// </summary>
    public static void PlayDissolve(UIElement element, Action? onComplete = null)
    {
        var (c, visual) = GetCompositionPair(element);
        var easing = CreateDissolveEasing(c);

        var scaleAnim = c.CreateVector3KeyFrameAnimation();
        scaleAnim.InsertKeyFrame(0f, new Vector3(1f, 1f, 1f));
        scaleAnim.InsertKeyFrame(1f, new Vector3(0.85f, 0.85f, 1f), easing);
        scaleAnim.Duration = TimeSpan.FromMilliseconds(220);

        var opacityAnim = c.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0f, 1f);
        opacityAnim.InsertKeyFrame(1f, 0f, easing);
        opacityAnim.Duration = TimeSpan.FromMilliseconds(220);

        visual.CenterPoint = new Vector3(visual.Size.X / 2f, visual.Size.Y / 2f, 0f);

        var batch = c.CreateScopedBatch(CompositionBatchTypes.Animation);
        visual.StartAnimation("Scale", scaleAnim);
        visual.StartAnimation("Opacity", opacityAnim);
        batch.End();
        batch.Completed += (_, _) => onComplete?.Invoke();
    }

    /// <summary>
    /// Play staggered appear animation for a list of elements (used for prompt arc).
    /// </summary>
    public static void PlayStaggeredAppear(IList<UIElement> elements, int delayMs = 40)
    {
        if (elements.Count == 0) return;

        var (c, _) = GetCompositionPair(elements[0]);
        var easing = CreateOrbEasing(c);

        for (int i = 0; i < elements.Count; i++)
        {
            var (_, visual) = GetCompositionPair(elements[i]);
            visual.CenterPoint = new Vector3(visual.Size.X / 2f, visual.Size.Y / 2f, 0f);

            var scaleAnim = c.CreateVector3KeyFrameAnimation();
            scaleAnim.InsertKeyFrame(0f, new Vector3(0.7f, 0.7f, 1f));
            scaleAnim.InsertKeyFrame(1f, new Vector3(1f, 1f, 1f), easing);
            scaleAnim.Duration = TimeSpan.FromMilliseconds(180);
            scaleAnim.DelayTime = TimeSpan.FromMilliseconds(i * delayMs);

            var opacityAnim = c.CreateScalarKeyFrameAnimation();
            opacityAnim.InsertKeyFrame(0f, 0f);
            opacityAnim.InsertKeyFrame(1f, 1f, easing);
            opacityAnim.Duration = TimeSpan.FromMilliseconds(180);
            opacityAnim.DelayTime = TimeSpan.FromMilliseconds(i * delayMs);

            visual.StartAnimation("Scale", scaleAnim);
            visual.StartAnimation("Opacity", opacityAnim);
        }
    }

    /// <summary>
    /// Play staggered disappear for prompt arc elements.
    /// </summary>
    public static void PlayStaggeredDisappear(IList<UIElement> elements, int delayMs = 30, Action? onComplete = null)
    {
        if (elements.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        var (c, _) = GetCompositionPair(elements[0]);
        var easing = CreateDissolveEasing(c);

        CompositionScopedBatch? batch = null;
        if (onComplete != null)
        {
            batch = c.CreateScopedBatch(CompositionBatchTypes.Animation);
        }

        for (int i = 0; i < elements.Count; i++)
        {
            var (_, visual) = GetCompositionPair(elements[i]);
            visual.CenterPoint = new Vector3(visual.Size.X / 2f, visual.Size.Y / 2f, 0f);

            var scaleAnim = c.CreateVector3KeyFrameAnimation();
            scaleAnim.InsertKeyFrame(0f, new Vector3(1f, 1f, 1f));
            scaleAnim.InsertKeyFrame(1f, new Vector3(0.7f, 0.7f, 1f), easing);
            scaleAnim.Duration = TimeSpan.FromMilliseconds(150);
            scaleAnim.DelayTime = TimeSpan.FromMilliseconds(i * delayMs);

            var opacityAnim = c.CreateScalarKeyFrameAnimation();
            opacityAnim.InsertKeyFrame(0f, 1f);
            opacityAnim.InsertKeyFrame(1f, 0f, easing);
            opacityAnim.Duration = TimeSpan.FromMilliseconds(150);
            opacityAnim.DelayTime = TimeSpan.FromMilliseconds(i * delayMs);

            visual.StartAnimation("Scale", scaleAnim);
            visual.StartAnimation("Opacity", opacityAnim);
        }

        if (batch != null)
        {
            batch.End();
            batch.Completed += (_, _) => onComplete?.Invoke();
        }
    }

    /// <summary>
    /// Create a repeating pulse animation (scale 1→1.4, opacity 0.6→0).
    /// Call on the pulse ring element during recording.
    /// </summary>
    public static void StartPulseLoop(UIElement element)
    {
        var (c, visual) = GetCompositionPair(element);
        visual.CenterPoint = new Vector3(visual.Size.X / 2f, visual.Size.Y / 2f, 0f);

        var scaleAnim = c.CreateVector3KeyFrameAnimation();
        scaleAnim.InsertKeyFrame(0f, new Vector3(1f, 1f, 1f));
        scaleAnim.InsertKeyFrame(1f, new Vector3(1.4f, 1.4f, 1f));
        scaleAnim.Duration = TimeSpan.FromMilliseconds(1200);
        scaleAnim.IterationBehavior = AnimationIterationBehavior.Forever;

        var opacityAnim = c.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0f, 0.6f);
        opacityAnim.InsertKeyFrame(1f, 0f);
        opacityAnim.Duration = TimeSpan.FromMilliseconds(1200);
        opacityAnim.IterationBehavior = AnimationIterationBehavior.Forever;

        visual.StartAnimation("Scale", scaleAnim);
        visual.StartAnimation("Opacity", opacityAnim);
    }

    /// <summary>
    /// Stop all animations on an element.
    /// </summary>
    public static void StopAnimations(UIElement element)
    {
        var (_, visual) = GetCompositionPair(element);
        visual.StopAnimation("Scale");
        visual.StopAnimation("Opacity");
    }

    /// <summary>
    /// Reset element to fully visible, normal scale.
    /// </summary>
    public static void ResetVisual(UIElement element)
    {
        var (_, visual) = GetCompositionPair(element);
        visual.Scale = new Vector3(1f, 1f, 1f);
        visual.Opacity = 1f;
    }

    /// <summary>
    /// Slide element in from below (used for context cards).
    /// </summary>
    public static void PlaySlideInFromBottom(UIElement element, float distance = 50f)
    {
        var (c, visual) = GetCompositionPair(element);
        var easing = CreateOrbEasing(c);

        var offsetAnim = c.CreateVector3KeyFrameAnimation();
        offsetAnim.InsertKeyFrame(0f, new Vector3(0f, distance, 0f));
        offsetAnim.InsertKeyFrame(1f, new Vector3(0f, 0f, 0f), easing);
        offsetAnim.Duration = TimeSpan.FromMilliseconds(250);

        var opacityAnim = c.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0f, 0f);
        opacityAnim.InsertKeyFrame(1f, 1f, easing);
        opacityAnim.Duration = TimeSpan.FromMilliseconds(250);

        visual.StartAnimation("Offset", offsetAnim);
        visual.StartAnimation("Opacity", opacityAnim);
    }

    /// <summary>
    /// Slide element in from the right (used for target apps).
    /// </summary>
    public static void PlaySlideInFromRight(UIElement element, float distance = 80f)
    {
        var (c, visual) = GetCompositionPair(element);
        var easing = CreateOrbEasing(c);

        var offsetAnim = c.CreateVector3KeyFrameAnimation();
        offsetAnim.InsertKeyFrame(0f, new Vector3(distance, 0f, 0f));
        offsetAnim.InsertKeyFrame(1f, new Vector3(0f, 0f, 0f), easing);
        offsetAnim.Duration = TimeSpan.FromMilliseconds(250);

        var opacityAnim = c.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0f, 0f);
        opacityAnim.InsertKeyFrame(1f, 1f, easing);
        opacityAnim.Duration = TimeSpan.FromMilliseconds(250);

        visual.StartAnimation("Offset", offsetAnim);
        visual.StartAnimation("Opacity", opacityAnim);
    }

    /// <summary>
    /// Subtle pulsing opacity animation for model arc in idle state.
    /// </summary>
    public static void StartSubtlePulse(UIElement element)
    {
        var (c, visual) = GetCompositionPair(element);

        var opacityAnim = c.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0f, 0.2f);
        opacityAnim.InsertKeyFrame(0.5f, 0.4f);
        opacityAnim.InsertKeyFrame(1f, 0.2f);
        opacityAnim.Duration = TimeSpan.FromMilliseconds(2000);
        opacityAnim.IterationBehavior = AnimationIterationBehavior.Forever;

        visual.StartAnimation("Opacity", opacityAnim);
    }

    /// <summary>
    /// Activate animation: scale 0.8→1, opacity 0.3→1 (used when selecting a model lens).
    /// </summary>
    public static void PlayActivate(UIElement element)
    {
        var (c, visual) = GetCompositionPair(element);
        var easing = CreateOrbEasing(c);

        visual.CenterPoint = new Vector3(visual.Size.X / 2f, visual.Size.Y / 2f, 0f);

        var scaleAnim = c.CreateVector3KeyFrameAnimation();
        scaleAnim.InsertKeyFrame(0f, new Vector3(0.8f, 0.8f, 1f));
        scaleAnim.InsertKeyFrame(1f, new Vector3(1f, 1f, 1f), easing);
        scaleAnim.Duration = TimeSpan.FromMilliseconds(200);

        var opacityAnim = c.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0f, 0.3f);
        opacityAnim.InsertKeyFrame(1f, 1f, easing);
        opacityAnim.Duration = TimeSpan.FromMilliseconds(200);

        visual.StartAnimation("Scale", scaleAnim);
        visual.StartAnimation("Opacity", opacityAnim);
    }
}
