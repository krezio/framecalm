using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Framecalm.App.Controls;

public sealed class VisIf : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => Truthy(v, p) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();

    internal static bool Truthy(object? v, object? p) => v switch
    {
        null => false,
        bool b => b,
        int i => i > 0,
        double d => d > 0,
        string s when p is string ps => ps.Split('|').Contains(s),
        string s => s.Length > 0,
        Enum e when p is string ps => ps.Split('|').Contains(e.ToString()),
        System.Collections.ICollection col => col.Count > 0,
        _ => true,
    };
}

public sealed class VisIfNot : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => VisIf.Truthy(v, p) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class VisIfAny : IMultiValueConverter
{
    public object Convert(object[] v, Type t, object? p, CultureInfo c) => v.Any(x => VisIf.Truthy(x, null)) ? Visibility.Visible : Visibility.Collapsed;
    public object[] ConvertBack(object v, Type[] t, object p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class Upper : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v?.ToString()?.ToUpperInvariant() ?? "";
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

public static class Motion
{
    /// <summary>The in-app "Reduce motion" switch. Windows' own animation setting is often off on gaming PCs to save
    /// a few milliseconds, so it doesn't decide this; the switch in Settings does.</summary>
    public static bool Reduced => ForceReduced;
    public static bool ForceReduced { get; set; }
    public static Duration D(double ms) => new(TimeSpan.FromMilliseconds(Reduced ? 0 : ms));
    public static readonly IEasingFunction Out = new CubicEase { EasingMode = EasingMode.EaseOut };
    public static readonly IEasingFunction InOut = new CubicEase { EasingMode = EasingMode.EaseInOut };
}

static class Brushes2
{
    public static SolidColorBrush B(uint argb) { var b = new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb)); b.Freeze(); return b; }
    public static Pen P(Brush b, double w, bool dashed = false) { var p = new Pen(b, w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; if (dashed) p.DashStyle = new DashStyle([3, 4], 0); p.Freeze(); return p; }
    public static readonly Brush Accent = B(0xFFFFB224), Text = B(0xFFF1F0FF), Muted = B(0xFFABA8DA), Faint = B(0xFF7C78B5), Line = B(0xFF272366), Line2 = B(0xFF3B368A), Dim = B(0xFF2B2773), Good = B(0xFF7CF5C0);
    public static readonly Typeface Tag = new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    public static FormattedText T(string s, double size, Brush b, Visual v) =>
        new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Tag, size, b, VisualTreeHelper.GetDpi(v).PixelsPerDip);
}

/// <summary>Frame-time trace: "before" in grey, "after" in the accent, target as a dashed line. Draws in from left to right.</summary>
public sealed class FrameChart : FrameworkElement
{
    public static readonly DependencyProperty BeforeProperty = Reg<object?>(nameof(Before), null);
    public static readonly DependencyProperty AfterProperty = Reg<object?>(nameof(After), null);
    public static readonly DependencyProperty TargetMsProperty = Reg(nameof(TargetMs), 0.0);
    public static readonly DependencyProperty ProgressProperty = Reg(nameof(Progress), 1.0);
    public static readonly DependencyProperty ShowAxisProperty = Reg(nameof(ShowAxis), true);
    public static readonly DependencyProperty TargetLabelProperty = Reg<string?>(nameof(TargetLabel), null);

    static DependencyProperty Reg<T>(string n, T def) => DependencyProperty.Register(n, typeof(T), typeof(FrameChart),
        new FrameworkPropertyMetadata(def, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => { if (n is nameof(Before) or nameof(After)) ((FrameChart)d).DrawIn(); }));

    public object? Before { get => GetValue(BeforeProperty); set => SetValue(BeforeProperty, value); }
    public object? After { get => GetValue(AfterProperty); set => SetValue(AfterProperty, value); }
    double[]? B => Before as double[];
    double[]? A => After as double[];
    public double TargetMs { get => (double)GetValue(TargetMsProperty); set => SetValue(TargetMsProperty, value); }
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public bool ShowAxis { get => (bool)GetValue(ShowAxisProperty); set => SetValue(ShowAxisProperty, value); }
    public string? TargetLabel { get => (string?)GetValue(TargetLabelProperty); set => SetValue(TargetLabelProperty, value); }

    void DrawIn()
    {
        if (Motion.Reduced) { Progress = 1; return; }
        BeginAnimation(ProgressProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(1100)) { EasingFunction = Motion.Out });
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth; var h = ActualHeight;
        if (w < 10 || h < 10) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        var all = (B ?? []).Concat(A ?? []).ToArray();
        if (all.Length == 0) return;
        var sorted = all.OrderBy(x => x).ToArray();
        var top = Math.Max(sorted[(int)(sorted.Length * 0.995)] * 1.15, TargetMs * 1.6);
        top = Math.Max(top, 8);
        var left = ShowAxis ? 34.0 : 0;
        double Y(double ms) => h - 4 - Math.Min(ms, top) / top * (h - 14);

        if (ShowAxis)
        {
            foreach (var g in Nice(top))
            {
                var y = Y(g);
                dc.DrawLine(Brushes2.P(Brushes2.Line, 1), new Point(left, y), new Point(w, y));
                var t = Brushes2.T($"{g:0}", 10, Brushes2.Faint, this);
                dc.DrawText(t, new Point(0, y - t.Height / 2));
            }
            var unit = Brushes2.T("MS", 9, Brushes2.Faint, this);
            dc.DrawText(unit, new Point(0, 0));
        }

        dc.PushClip(new RectangleGeometry(new Rect(0, -20, left + (w - left) * Progress, h + 40)));
        if (B is { Length: > 1 } b) dc.DrawGeometry(null, Brushes2.P(Brushes2.Faint, 1.3), Line(b, left, w, Y));
        if (A is { Length: > 1 } a) dc.DrawGeometry(null, Brushes2.P(Brushes2.Accent, 2), Line(a, left, w, Y));
        dc.Pop();

        if (TargetMs > 0)
        {
            var y = Y(TargetMs);
            dc.DrawLine(Brushes2.P(Brushes2.Text, 1, dashed: true), new Point(left, y), new Point(w, y));
            if (TargetLabel is { } tl)
            {
                var t = Brushes2.T(tl, 10, Brushes2.Text, this);
                dc.DrawRectangle(Brushes2.B(0xFF09090B), null, new Rect(w - t.Width - 10, y - t.Height / 2 - 1, t.Width + 10, t.Height + 2));
                dc.DrawText(t, new Point(w - t.Width - 4, y - t.Height / 2));
            }
        }
    }

    static IEnumerable<double> Nice(double top)
    {
        var step = top > 60 ? 20 : top > 30 ? 10 : top > 12 ? 5 : 2;
        for (double v = step; v < top * 0.95; v += step) yield return v;
    }

    /// <summary>One point per ~2 px, keeping the worst frame in each bucket so stutters stay visible.</summary>
    static StreamGeometry Line(double[] data, double left, double w, Func<double, double> y)
    {
        var buckets = (int)Math.Max(2, (w - left) / 2);
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            for (int i = 0; i < buckets; i++)
            {
                var s = (int)((long)i * data.Length / buckets);
                var e = Math.Max(s + 1, (int)((long)(i + 1) * data.Length / buckets));
                double max = 0;
                for (int k = s; k < e && k < data.Length; k++) max = Math.Max(max, data[k]);
                var p = new Point(left + (w - left) * i / (buckets - 1), y(max));
                if (i == 0) c.BeginFigure(p, false, false); else c.LineTo(p, true, true);
            }
        }
        g.Freeze();
        return g;
    }
}

/// <summary>Segmented tachometer: lit segments up to the value, a bone marker at the target.</summary>
public sealed class Tach : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(Tach),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => ((Tach)d).Animate((double)e.OldValue, (double)e.NewValue)));
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(nameof(Target), typeof(double), typeof(Tach), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(nameof(Max), typeof(double), typeof(Tach), new FrameworkPropertyMetadata(200.0, FrameworkPropertyMetadataOptions.AffectsRender));
    static readonly DependencyProperty ShownProperty = DependencyProperty.Register("Shown", typeof(double), typeof(Tach), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Target { get => (double)GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public double Max { get => (double)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    void Animate(double from, double to) =>
        BeginAnimation(ShownProperty, new DoubleAnimation(to, Motion.D(900)) { EasingFunction = Motion.Out });

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight * 1.25);
        if (size < 40) return;
        var c = new Point(ActualWidth / 2, size * 0.52);
        var r = size * 0.46;
        const int n = 54;
        const double start = 150, sweep = 240;
        var shown = (double)GetValue(ShownProperty);
        var lit = Math.Clamp(shown / Max, 0, 1) * n;
        for (int i = 0; i < n; i++)
        {
            var a = (start + sweep * i / (n - 1)) * Math.PI / 180;
            var major = i % 9 == 0;
            var len = major ? r * 0.17 : r * 0.12;
            var p1 = new Point(c.X + Math.Cos(a) * r, c.Y + Math.Sin(a) * r);
            var p2 = new Point(c.X + Math.Cos(a) * (r - len), c.Y + Math.Sin(a) * (r - len));
            var brush = i < lit ? Brushes2.Accent : Brushes2.Dim;
            dc.DrawLine(Brushes2.P(brush, major ? 4 : 3.2), p1, p2);
        }
        if (Target > 0)
        {
            var a = (start + sweep * Math.Clamp(Target / Max, 0, 1)) * Math.PI / 180;
            var p1 = new Point(c.X + Math.Cos(a) * (r + 7), c.Y + Math.Sin(a) * (r + 7));
            var p2 = new Point(c.X + Math.Cos(a) * (r - r * 0.26), c.Y + Math.Sin(a) * (r - r * 0.26));
            dc.DrawLine(Brushes2.P(Brushes2.Text, 2.5), p1, p2);
            var t = Brushes2.T($"{Target:0}", 11, Brushes2.Text, this);
            dc.DrawText(t, new Point(c.X + Math.Cos(a) * (r + 18) - t.Width / 2, c.Y + Math.Sin(a) * (r + 18) - t.Height / 2));
        }
    }
}

/// <summary>A number that counts up to its value.</summary>
public sealed class CountUp : TextBlock
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(CountUp),
        new PropertyMetadata(0.0, (d, e) => ((CountUp)d).Start((double)e.OldValue, (double)e.NewValue)));
    static readonly DependencyProperty ShownProperty = DependencyProperty.Register("Shown", typeof(double), typeof(CountUp),
        new PropertyMetadata(0.0, (d, e) => ((CountUp)d).Render()));
    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(nameof(Format), typeof(string), typeof(CountUp), new PropertyMetadata("0", (d, e) => ((CountUp)d).Render()));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Format { get => (string)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    void Start(double from, double to) => BeginAnimation(ShownProperty, new DoubleAnimation(from, to, Motion.D(Math.Abs(to - from) < 0.001 ? 0 : 1000)) { EasingFunction = Motion.Out });
    void Render() => Text = ((double)GetValue(ShownProperty)).ToString(Format, CultureInfo.InvariantCulture);
}

/// <summary>Where each frame's time goes: GPU busy vs the whole frame. The gap is time the GPU waited on the CPU.</summary>
public sealed class SplitBar : FrameworkElement
{
    public static readonly DependencyProperty FrameMsProperty = DependencyProperty.Register(nameof(FrameMs), typeof(double), typeof(SplitBar), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty GpuMsProperty = DependencyProperty.Register(nameof(GpuMs), typeof(double), typeof(SplitBar), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public double FrameMs { get => (double)GetValue(FrameMsProperty); set => SetValue(FrameMsProperty, value); }
    public double GpuMs { get => (double)GetValue(GpuMsProperty); set => SetValue(GpuMsProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth; var h = ActualHeight;
        if (FrameMs <= 0 || w < 10) return;
        var gw = Math.Clamp(GpuMs / FrameMs, 0, 1) * w;
        // hatched "waiting" area so the split doesn't rely on colour alone
        dc.DrawRectangle(Brushes2.Dim, null, new Rect(0, 0, w, h));
        for (double x = gw - h; x < w; x += 6)
            dc.DrawLine(Brushes2.P(Brushes2.Line2, 1.4), new Point(Math.Max(gw, x), h - Math.Max(0, gw - x)), new Point(Math.Min(w, x + h), Math.Max(0, x + h - w)));
        dc.DrawRectangle(Brushes2.Accent, null, new Rect(0, 0, gw, h));
    }
}

/// <summary>
/// Cover art. Real games use the art their launcher already cached on this PC (never copied or shipped).
/// Anything else, including all demo games, gets a generated cover: flat shapes and type, no third-party art.
/// </summary>
public static class Covers
{
    static readonly Dictionary<string, ImageSource> Cache = new();

    public static ImageSource For(string id, string name, string? artPath)
    {
        var key = id + "|" + artPath;
        if (Cache.TryGetValue(key, out var hit)) return hit;
        ImageSource src;
        if (artPath != null && File.Exists(artPath))
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.DecodePixelWidth = 400;
                bi.UriSource = new Uri(artPath);
                bi.EndInit();
                bi.Freeze();
                src = bi;
            }
            catch { src = Generate(id, name); }
        }
        else src = Generate(id, name);
        return Cache[key] = src;
    }

    sealed record Look(uint Bg, uint A, uint B, string Motif);

    static readonly Dictionary<string, Look> Looks = new()
    {
        ["minecraft-java"] = new(0xFF1D2B22, 0xFF6FA35A, 0xFF8A6A47, "blocks"),
        ["fortnite"] = new(0xFF1B1F3A, 0xFF7C5CFF, 0xFFFFC94A, "storm"),
        ["rocket-league"] = new(0xFF0F2436, 0xFF2E8BFF, 0xFFFF7A2E, "ball"),
        ["valorant"] = new(0xFF22100F, 0xFFFF4655, 0xFFECE8E1, "blade"),
        ["cs2"] = new(0xFF1E1A14, 0xFFDE9B35, 0xFF5A6B7D, "grid"),
        ["apex-legends"] = new(0xFF241111, 0xFFDA292A, 0xFFE8E1D9, "chevron"),
        ["stardew"] = new(0xFF1E2A3A, 0xFFF2B35B, 0xFF6EB5E6, "sun"),
    };

    static readonly Look[] Palette =
    [
        new(0xFF1A2230, 0xFF4F8DFF, 0xFFE8E1D9, "sun"), new(0xFF2A1A16, 0xFFFF7448, 0xFFF2D6A2, "chevron"),
        new(0xFF16241F, 0xFF46C69A, 0xFFE9E4D8, "grid"), new(0xFF241A2C, 0xFFC779FF, 0xFFFFD6A0, "storm"),
        new(0xFF1F1F1F, 0xFFE8E1D9, 0xFFFF5A3C, "blade"), new(0xFF102226, 0xFF33C3D9, 0xFFFFCF5A, "ball"),
    ];

    static ImageSource Generate(string id, string name)
    {
        var look = Looks.TryGetValue(id, out var l) ? l : Palette[(uint)StableHash(id) % Palette.Length];
        var W = 400.0; var H = 600.0;
        var dg = new DrawingGroup();
        using (var dc = dg.Open())
        {
            var bg = Brushes2.B(look.Bg); var a = Brushes2.B(look.A); var b = Brushes2.B(look.B);
            dc.DrawRectangle(bg, null, new Rect(0, 0, W, H));
            var rng = new Random(StableHash(id));
            switch (look.Motif)
            {
                case "blocks":
                    for (int x = 0; x < 10; x++)
                    {
                        var hgt = 6 + (int)(Math.Sin(x * 0.9 + 1) * 2.5 + rng.Next(2));
                        for (int y = 0; y < hgt; y++)
                            dc.DrawRectangle(y == hgt - 1 ? a : y > hgt - 4 ? b : Brushes2.B(0xFF4A4038), null, new Rect(x * 40, H - (y + 1) * 40, 39, 39));
                    }
                    dc.DrawRectangle(Brushes2.B(0xFFEEEBE4), null, new Rect(290, 90, 56, 56));
                    break;
                case "storm":
                    for (int i = 9; i > 0; i--) dc.DrawEllipse(i % 2 == 0 ? a : bg, null, new Point(W * 0.6, H * 0.42), i * 34, i * 34);
                    dc.DrawRectangle(b, null, new Rect(0, H * 0.72, W, 14));
                    break;
                case "ball":
                    dc.DrawRectangle(Brushes2.B(0xFF14304A), null, new Rect(0, H * 0.62, W, H * 0.38));
                    for (int i = 0; i < 6; i++) dc.DrawLine(Brushes2.P(Brushes2.B(0x332E8BFF), 3), new Point(i * 90 - 60, H), new Point(W / 2, H * 0.62));
                    dc.DrawEllipse(b, null, new Point(W * 0.62, H * 0.4), 92, 92);
                    dc.DrawEllipse(null, Brushes2.P(a, 10), new Point(W * 0.62, H * 0.4), 120, 120);
                    break;
                case "blade":
                    var g = new StreamGeometry();
                    using (var c = g.Open()) { c.BeginFigure(new Point(W * 0.15, H), true, true); c.LineTo(new Point(W * 0.85, H * 0.1), true, false); c.LineTo(new Point(W, H * 0.25), true, false); c.LineTo(new Point(W * 0.45, H), true, false); }
                    dc.DrawGeometry(a, null, g);
                    dc.DrawRectangle(b, null, new Rect(40, 70, 6, 160));
                    break;
                case "grid":
                    for (int x = 0; x < 8; x++) for (int y = 0; y < 12; y++)
                        if (rng.NextDouble() < 0.35) dc.DrawRectangle(rng.NextDouble() < 0.25 ? a : b, null, new Rect(x * 50 + 4, y * 50 + 4, 42, 42));
                    break;
                case "chevron":
                    for (int i = 0; i < 5; i++)
                    {
                        var ch = new StreamGeometry();
                        var y0 = 120 + i * 70;
                        using (var c = ch.Open()) { c.BeginFigure(new Point(60, y0), true, true); c.LineTo(new Point(W / 2, y0 + 70), true, false); c.LineTo(new Point(W - 60, y0), true, false); c.LineTo(new Point(W - 60, y0 + 30), true, false); c.LineTo(new Point(W / 2, y0 + 100), true, false); c.LineTo(new Point(60, y0 + 30), true, false); }
                        dc.DrawGeometry(i == 1 ? b : a, null, ch);
                    }
                    break;
                default: // sun
                    dc.DrawEllipse(a, null, new Point(W / 2, H * 0.5), 130, 130);
                    for (int i = 0; i < 7; i++) dc.DrawRectangle(bg, null, new Rect(0, H * 0.5 + i * 22, W, 6 + i * 2));
                    dc.DrawRectangle(b, null, new Rect(0, H * 0.72, W, H * 0.28));
                    break;
            }
            // big cropped initials, like a record sleeve
            var initials = new string(name.Split([' ', ':', '-'], StringSplitOptions.RemoveEmptyEntries).Where(w => char.IsLetterOrDigit(w[0])).Take(2).Select(w => char.ToUpperInvariant(w[0])).ToArray());
            var ft = new FormattedText(initials, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 200, Brushes2.B(0x26FFFFFF), 1);
            dc.DrawText(ft, new Point(-10, -40));
        }
        dg.Freeze();
        var img = new DrawingImage(dg);
        img.Freeze();
        return img;
    }

    static int StableHash(string s) { unchecked { int h = 23; foreach (var ch in s) h = h * 31 + ch; return h & 0x7FFFFFFF; } }
}
