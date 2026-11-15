using System.Windows;
using System.Windows.Media;

namespace Framecalm.App.Controls;

/// <summary>One shared ~30 fps clock for the background light effects. Stops when nothing is listening,
/// and stays still when the in-app "reduce motion" switch is on.</summary>
public static class Ambient
{
    static event Action<double>? Tick;
    static bool _hooked;
    static TimeSpan _last;
    static double _t;

    public static bool Moving => !Motion.ForceReduced;
    public static double Time => _t;

    public static void Add(Action<double> a)
    {
        Tick += a;
        if (_hooked) return;
        CompositionTarget.Rendering += OnRender;
        _hooked = true;
    }

    public static void Remove(Action<double> a)
    {
        Tick -= a;
        if (Tick != null || !_hooked) return;
        CompositionTarget.Rendering -= OnRender;
        _hooked = false;
    }

    static void OnRender(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        var dt = (now - _last).TotalSeconds;
        if (dt < 1 / 32.0) return;
        _last = now;
        if (!Moving) return;
        dt = Math.Min(dt, 0.1);
        _t += dt;
        Tick?.Invoke(dt);
    }
}

/// <summary>Base for an effect layer: ticks only while loaded and visible, never takes clicks.</summary>
public abstract class AmbientLayer : FrameworkElement
{
    protected AmbientLayer()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => Ambient.Add(OnTick);
        Unloaded += (_, _) => Ambient.Remove(OnTick);
        SizeChanged += (_, _) => Seed();
    }

    void OnTick(double dt)
    {
        if (!IsVisible || ActualWidth < 2) return;
        Step(dt);
        InvalidateVisual();
    }

    protected abstract void Step(double dt);
    protected virtual void Seed() { }

    protected static RadialGradientBrush Glow(Color c, double core = 0.9)
    {
        var b = new RadialGradientBrush();
        b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * core), c.R, c.G, c.B), 0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(70, c.R, c.G, c.B), 0.3));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
        b.Freeze();
        return b;
    }
}

/// <summary>Star dust: soft glowing points at three depths, drifting up and twinkling.</summary>
public sealed class StarField : AmbientLayer
{
    public static readonly DependencyProperty DensityProperty = DependencyProperty.Register(nameof(Density), typeof(double), typeof(StarField),
        new FrameworkPropertyMetadata(1.0, (d, _) => ((StarField)d).Seed()));
    /// <summary>Stars per 10,000 square pixels.</summary>
    public double Density { get => (double)GetValue(DensityProperty); set => SetValue(DensityProperty, value); }

    static readonly Brush[] Brushes =
    [
        Glow(Color.FromRgb(0xF1, 0xF0, 0xFF)), Glow(Color.FromRgb(0x8F, 0x8B, 0xFF)),
        Glow(Color.FromRgb(0xFF, 0xB2, 0x24)), Glow(Color.FromRgb(0x7C, 0xF5, 0xC0)),
    ];

    struct Star { public double X, Y, Z, R, P, S; public int C; }
    Star[] _stars = [];
    readonly Random _rnd = new(7);

    protected override void Seed()
    {
        var n = (int)Math.Min(160, ActualWidth * ActualHeight / 10000 * Density);
        _stars = new Star[Math.Max(0, n)];
        for (int i = 0; i < _stars.Length; i++)
        {
            var roll = _rnd.NextDouble();
            _stars[i] = new Star
            {
                X = _rnd.NextDouble() * ActualWidth, Y = _rnd.NextDouble() * ActualHeight, Z = _rnd.NextDouble() * 0.9 + 0.1,
                R = _rnd.NextDouble() * 1.5 + 0.5, P = _rnd.NextDouble() * Math.PI * 2, S = _rnd.NextDouble() * 1.2 + 0.4,
                C = roll < 0.5 ? 0 : roll < 0.82 ? 1 : roll < 0.92 ? 2 : 3,
            };
        }
        InvalidateVisual();
    }

    protected override void Step(double dt)
    {
        for (int i = 0; i < _stars.Length; i++)
        {
            ref var s = ref _stars[i];
            s.Y -= s.Z * 9 * dt;
            s.P += s.S * dt;
            if (s.Y < -8) { s.Y = ActualHeight + 8; s.X = _rnd.NextDouble() * ActualWidth; }
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        foreach (var s in _stars)
        {
            var tw = 0.5 + Math.Sin(s.P) * 0.45;
            dc.PushOpacity(Math.Clamp(tw, 0.05, 1));
            dc.DrawEllipse(Brushes[s.C], null, new Point(s.X, s.Y), s.R * 5, s.R * 5);
            dc.Pop();
        }
    }
}

/// <summary>Stage lights: soft beams fanning down from the top edge, slowly sweeping.</summary>
public sealed class LightBeams : AmbientLayer
{
    public static readonly DependencyProperty StrengthProperty = DependencyProperty.Register(nameof(Strength), typeof(double), typeof(LightBeams), new FrameworkPropertyMetadata(1.0));
    public double Strength { get => (double)GetValue(StrengthProperty); set => SetValue(StrengthProperty, value); }

    sealed record Beam(double X, double From, double To, double Speed, double Phase, Brush Fill, double Width);

    static Brush Fade(Color c, byte a)
    {
        var b = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(a, c.R, c.G, c.B), 0.5));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
        b.Freeze();
        return b;
    }

    static readonly Beam[] Beams =
    [
        new(0.32, -32, -8, 0.33, 0.0, Fade(Color.FromRgb(0xBE, 0xBA, 0xFF), 90), 0.36),
        new(0.62, 30, 6, 0.27, 1.7, Fade(Color.FromRgb(0xBE, 0xBA, 0xFF), 80), 0.32),
        new(0.48, -12, 20, 0.21, 3.1, Fade(Color.FromRgb(0xFF, 0xB2, 0x24), 60), 0.28),
        new(0.55, 14, -24, 0.24, 4.4, Fade(Color.FromRgb(0x7C, 0xF5, 0xC0), 45), 0.26),
    ];

    static readonly Brush Mask = MakeMask();
    static Brush MakeMask()
    {
        var m = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        m.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 0, 0), 0));
        m.GradientStops.Add(new GradientStop(Color.FromArgb(110, 0, 0, 0), 0.45));
        m.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.9));
        m.Freeze();
        return m;
    }

    protected override void Step(double dt) { }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight, t = Ambient.Time;
        if (w < 2) return;
        dc.PushOpacityMask(Mask);
        dc.PushOpacity(Math.Clamp(Strength, 0, 1));
        foreach (var b in Beams)
        {
            var k = 0.5 + 0.5 * Math.Sin(t * b.Speed + b.Phase);
            var angle = b.From + (b.To - b.From) * k;
            var len = h * 1.3;
            var half = w * b.Width / 2;
            var top = new Point(w * b.X, -10);
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(top.X - 6, top.Y), true, true);
                c.LineTo(new Point(top.X + 6, top.Y), false, false);
                c.LineTo(new Point(top.X + half, top.Y + len), false, false);
                c.LineTo(new Point(top.X - half, top.Y + len), false, false);
            }
            g.Transform = new RotateTransform(angle, top.X, top.Y);
            g.Freeze();
            dc.DrawGeometry(b.Fill, null, g);
        }
        dc.Pop();
        dc.Pop();
    }
}
