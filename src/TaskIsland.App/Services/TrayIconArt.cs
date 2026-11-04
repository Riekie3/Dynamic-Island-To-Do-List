using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TaskIsland.App.Services;

/// <summary>
/// Draws the tray icon in memory: the island notch with two eyes, white on a dark taskbar and near-black on a light one,
/// dimmed while paused. Phase 7's IconGen replaces this with the shipped .ico files; until then nothing is read from
/// disk. Built once per theme/pause change, never animated.
/// </summary>
public static class TrayIconArt
{
    private static readonly int[] Sizes = [16, 20, 24, 32, 40, 48];

    public static System.Drawing.Icon Create(bool lightTaskbar, bool paused, int size)
    {
        using var ico = new MemoryStream(BuildIco(lightTaskbar, paused));
        return new System.Drawing.Icon(ico, size, size);
    }

    private static byte[] BuildIco(bool lightTaskbar, bool paused)
    {
        var images = Sizes.Select(s => (Size: s, Bgra: Render(s, lightTaskbar, paused))).ToList();
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((short)0);
        w.Write((short)1);
        w.Write((short)images.Count);
        int offset = 6 + 16 * images.Count;
        var blobs = images.Select(i => DibEntry(i.Size, i.Bgra)).ToList();
        for (int i = 0; i < images.Count; i++)
        {
            int s = images[i].Size;
            w.Write((byte)(s >= 256 ? 0 : s));
            w.Write((byte)(s >= 256 ? 0 : s));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((short)1);
            w.Write((short)32);
            w.Write(blobs[i].Length);
            w.Write(offset);
            offset += blobs[i].Length;
        }
        foreach (var b in blobs) w.Write(b);
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>A 32-bit DIB icon image: header, bottom-up BGRA rows (straight alpha), empty AND mask.</summary>
    private static byte[] DibEntry(int s, byte[] bgra)
    {
        int maskStride = (s + 31) / 32 * 4;
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(40);
        w.Write(s);
        w.Write(s * 2);
        w.Write((short)1);
        w.Write((short)32);
        w.Write(0);
        w.Write(s * s * 4 + maskStride * s);
        w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        for (int y = s - 1; y >= 0; y--) w.Write(bgra, y * s * 4, s * 4);
        w.Write(new byte[maskStride * s]);
        w.Flush();
        return ms.ToArray();
    }

    private static byte[] Render(int s, bool lightTaskbar, bool paused)
    {
        var glyph = lightTaskbar ? Color.FromRgb(0x1C, 0x1C, 0x1E) : Colors.White;
        var eye = lightTaskbar ? Colors.White : Color.FromRgb(0x1C, 0x1C, 0x1E);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.PushOpacity(paused ? 0.45 : 1);
            double w = s * 0.94, h = s * 0.56;
            var body = new Rect((s - w) / 2, (s - h) / 2, w, h);
            dc.DrawRoundedRectangle(new SolidColorBrush(glyph), null, body, h * 0.42, h * 0.42);
            double er = Math.Max(1.1, s * 0.085);
            double ey = body.Top + h * 0.5;
            var eyeBrush = new SolidColorBrush(eye);
            dc.DrawEllipse(eyeBrush, null, new Point(s / 2.0 - w * 0.2, ey), er, er * 1.25);
            dc.DrawEllipse(eyeBrush, null, new Point(s / 2.0 + w * 0.2, ey), er, er * 1.25);
            dc.Pop();
        }
        var bmp = new RenderTargetBitmap(s, s, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(dv);
        var px = new byte[s * s * 4];
        bmp.CopyPixels(px, s * 4, 0);
        // Icons use straight alpha; WPF renders premultiplied.
        for (int i = 0; i < px.Length; i += 4)
        {
            byte a = px[i + 3];
            if (a is 0 or 255) continue;
            px[i] = (byte)Math.Min(255, px[i] * 255 / a);
            px[i + 1] = (byte)Math.Min(255, px[i + 1] * 255 / a);
            px[i + 2] = (byte)Math.Min(255, px[i + 2] * 255 / a);
        }
        return px;
    }
}
