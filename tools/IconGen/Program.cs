// Renders the TaskIsland application icon (dark rounded square, blue circle, white tick) at several sizes and writes a
// multi-size .ico (PNG-compressed entries). Usage: dotnet run --project tools/IconGen -- <output.ico>
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

int[] sizes = [16, 20, 24, 32, 40, 48, 64, 256];
string output = args.Length > 0 ? args[0] : "TaskIsland.ico";

var pngs = sizes.Select(Render).ToList();
using var ms = new MemoryStream();
using var w = new BinaryWriter(ms);
w.Write((short)0);
w.Write((short)1);
w.Write((short)sizes.Length);
int offset = 6 + 16 * sizes.Length;
for (int i = 0; i < sizes.Length; i++)
{
    int s = sizes[i];
    w.Write((byte)(s >= 256 ? 0 : s));
    w.Write((byte)(s >= 256 ? 0 : s));
    w.Write((byte)0);
    w.Write((byte)0);
    w.Write((short)1);
    w.Write((short)32);
    w.Write(pngs[i].Length);
    w.Write(offset);
    offset += pngs[i].Length;
}
foreach (var p in pngs) w.Write(p);
w.Flush();
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllBytes(output, ms.ToArray());
File.WriteAllBytes(Path.ChangeExtension(output, ".png"), pngs[^1]);
Console.WriteLine($"Wrote {output} ({ms.Length} bytes, {sizes.Length} sizes)");

static byte[] Render(int s)
{
    var dv = new DrawingVisual();
    using (var dc = dv.RenderOpen())
    {
        double u = s / 256.0;
        var body = new Rect(8 * u, 8 * u, 240 * u, 240 * u);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)), null, body, 56 * u, 56 * u);
        var blue = new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF));
        dc.DrawEllipse(blue, null, new Point(128 * u, 128 * u), 80 * u, 80 * u);
        var pen = new Pen(Brushes.White, Math.Max(1.5, 20 * u)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var tick = new StreamGeometry();
        using (var g = tick.Open())
        {
            g.BeginFigure(new Point(94 * u, 130 * u), false, false);
            g.LineTo(new Point(120 * u, 156 * u), true, true);
            g.LineTo(new Point(166 * u, 102 * u), true, true);
        }
        dc.DrawGeometry(null, pen, tick);
    }
    var bmp = new RenderTargetBitmap(s, s, 96, 96, PixelFormats.Pbgra32);
    bmp.Render(dv);
    var enc = new PngBitmapEncoder();
    enc.Frames.Add(BitmapFrame.Create(bmp));
    using var o = new MemoryStream();
    enc.Save(o);
    return o.ToArray();
}
