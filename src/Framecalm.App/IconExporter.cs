using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Framecalm.App;

/// <summary>Build-time helper: renders the logo (accent tile with an "f" whose crossbar is a long, flat line) to .ico and Store PNGs.</summary>
public static class IconExporter
{
    static readonly Color Accent = Color.FromRgb(0xFF, 0xB2, 0x24);
    static readonly Color Ink = Color.FromRgb(0x10, 0x0E, 0x33);

    public static BitmapSource Render(int size, double pad = 0, Color? bg = null)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            if (bg is { } b) dc.DrawRectangle(new SolidColorBrush(b), null, new Rect(0, 0, size, size));
            var s = size - pad * 2;
            var r = new Rect(pad, pad, s, s);
            dc.DrawRoundedRectangle(new SolidColorBrush(Accent), null, r, s * 0.22, s * 0.22);
            var pen = new Pen(new SolidColorBrush(Ink), Math.Max(1.6, s * 0.1125)) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                Point P(double x, double y) => new(pad + x / 24 * s, pad + y / 24 * s);
                // an "f" whose crossbar runs long and flat: a calm frame-time line
                c.BeginFigure(P(9.2, 19), false, false);
                c.LineTo(P(9.2, 9.6), true, true);
                c.ArcTo(P(13.2, 5.6), new Size(4.0 / 24 * s, 4.0 / 24 * s), 0, false, SweepDirection.Clockwise, true, true);
                c.LineTo(P(14.5, 5.6), true, true);
                c.BeginFigure(P(5.4, 12.2), false, false);
                c.LineTo(P(19, 12.2), true, true);
            }
            dc.DrawGeometry(null, pen, g);
        }
        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        return rtb;
    }

    static byte[] Png(BitmapSource b)
    {
        var e = new PngBitmapEncoder();
        e.Frames.Add(BitmapFrame.Create(b));
        using var ms = new MemoryStream();
        e.Save(ms);
        return ms.ToArray();
    }

    public static void Export(string icoPath)
    {
        int[] sizes = [16, 24, 32, 48, 64, 128, 256];
        var pngs = sizes.Select(s => Png(Render(s, s <= 32 ? 0 : s * 0.04))).ToList();
        using var fs = File.Create(icoPath);
        using var w = new BinaryWriter(fs);
        w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
        var offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
            w.Write(pngs[i].Length); w.Write(offset);
            offset += pngs[i].Length;
        }
        foreach (var p in pngs) w.Write(p);
        File.WriteAllBytes(Path.ChangeExtension(icoPath, ".png"), Png(Render(512, 20)));
    }

    public static void ExportStoreArt(string dir)
    {
        Directory.CreateDirectory(dir);
        void Save(string name, BitmapSource b) => File.WriteAllBytes(Path.Combine(dir, name), Png(b));
        Save("Square44x44Logo.png", Render(44, 2));
        Save("Square150x150Logo.png", Render(150, 30));
        Save("StoreLogo.png", Render(50, 2));
        Save("icon-32.png", Render(32));
        Save("icon-192.png", Render(192, 8));
        Save("icon-512.png", Render(512, 20));
        Save("apple-touch-icon.png", Render(180, 18, Ink));
        // wide tile: mark on the left of an ink canvas
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Ink), null, new Rect(0, 0, 310, 150));
            dc.DrawImage(Render(70), new Rect(120, 40, 70, 70));
        }
        var wide = new RenderTargetBitmap(310, 150, 96, 96, PixelFormats.Pbgra32);
        wide.Render(dv);
        Save("Wide310x150Logo.png", wide);
    }
}
