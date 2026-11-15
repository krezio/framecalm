using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Framecalm.App.Controls;

/// <summary>Eased mouse-wheel scrolling for every ScrollViewer: each notch glides instead of jumping.
/// Nested scroll areas scroll themselves first; the outer one only moves when the inner one can't.</summary>
public static class SmoothScroll
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(SmoothScroll),
        new PropertyMetadata(false, (d, e) =>
        {
            if (d is not ScrollViewer sv) return;
            sv.PreviewMouseWheel -= OnWheel;
            if ((bool)e.NewValue) sv.PreviewMouseWheel += OnWheel;
        }));
    public static void SetEnabled(DependencyObject d, bool v) => d.SetValue(EnabledProperty, v);
    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);

    static readonly DependencyProperty OffsetProperty = DependencyProperty.RegisterAttached("Offset", typeof(double), typeof(SmoothScroll),
        new PropertyMetadata(0.0, (d, e) => ((ScrollViewer)d).ScrollToVerticalOffset((double)e.NewValue)));
    static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached("Target", typeof(double), typeof(SmoothScroll), new PropertyMetadata(0.0));
    static readonly DependencyProperty LastProperty = DependencyProperty.RegisterAttached("Last", typeof(long), typeof(SmoothScroll), new PropertyMetadata(0L));

    static readonly IEasingFunction Ease = new CubicEase { EasingMode = EasingMode.EaseOut };

    static void OnWheel(object sender, MouseWheelEventArgs e)
    {
        var sv = (ScrollViewer)sender;
        if (e.Handled || Motion.ForceReduced || sv.ScrollableHeight <= 0) return;

        // let a scrollable area nested inside this one take the wheel if it can still move that way
        for (var d = e.OriginalSource as DependencyObject; d != null && d != sv; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (d is ScrollViewer inner && inner.ScrollableHeight > 0)
            {
                var canMove = e.Delta > 0 ? inner.VerticalOffset > 0 : inner.VerticalOffset < inner.ScrollableHeight;
                if (canMove) return;
            }
        }

        var now = Environment.TickCount64;
        var recent = now - (long)sv.GetValue(LastProperty) < 450;
        var from = recent ? (double)sv.GetValue(TargetProperty) : sv.VerticalOffset;
        var target = Math.Clamp(from - e.Delta * 1.1, 0, sv.ScrollableHeight);
        sv.SetValue(TargetProperty, target);
        sv.SetValue(LastProperty, now);
        sv.BeginAnimation(OffsetProperty, new DoubleAnimation(sv.VerticalOffset, target, TimeSpan.FromMilliseconds(420)) { EasingFunction = Ease });
        e.Handled = true;
    }
}

/// <summary>Entrance: an element fades and rises in shortly after it appears, so a page builds up
/// in a quick stagger instead of all at once. Set the delay in milliseconds.</summary>
public static class Entrance
{
    public static readonly DependencyProperty DelayProperty = DependencyProperty.RegisterAttached("Delay", typeof(double), typeof(Entrance),
        new PropertyMetadata(-1.0, (d, e) =>
        {
            if (d is not FrameworkElement fe) return;
            fe.Loaded -= Play;
            if ((double)e.NewValue >= 0) fe.Loaded += Play;
        }));
    public static void SetDelay(DependencyObject d, double v) => d.SetValue(DelayProperty, v);
    public static double GetDelay(DependencyObject d) => (double)d.GetValue(DelayProperty);

    static readonly IEasingFunction Ease = new QuinticEase { EasingMode = EasingMode.EaseOut };

    static void Play(object sender, RoutedEventArgs _)
    {
        var fe = (FrameworkElement)sender;
        if (Motion.Reduced) return;
        var delay = TimeSpan.FromMilliseconds(GetDelay(fe));
        var t = new TranslateTransform(0, 18);
        fe.RenderTransform = t;
        fe.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(520)) { BeginTime = delay, EasingFunction = Ease, FillBehavior = FillBehavior.Stop });
        fe.Opacity = 1;
        t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(620)) { BeginTime = delay, EasingFunction = Ease });
        // keep it invisible until its turn
        if (delay > TimeSpan.Zero)
        {
            fe.Opacity = 0;
            var hold = new DoubleAnimationUsingKeyFrames { Duration = delay + TimeSpan.FromMilliseconds(520) };
            hold.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            hold.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(delay)));
            hold.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(delay + TimeSpan.FromMilliseconds(520)), Ease));
            hold.Completed += (_, _) => { fe.BeginAnimation(UIElement.OpacityProperty, null); fe.Opacity = 1; };
            fe.BeginAnimation(UIElement.OpacityProperty, hold);
        }
    }
}
