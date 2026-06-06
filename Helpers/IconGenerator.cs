using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LenovoTray.Helpers;

/// <summary>
/// Generates the red Lenovo "L" tray icon at runtime.
/// Writing to a file on disk lets H.NotifyIcon reload the icon if it is recreated,
/// and avoids the GDI handle leak that <c>Bitmap.GetHicon()</c> introduces.
/// </summary>
internal static class IconGenerator
{
    // Geometry as fractions of the icon size, so every frame is rendered natively and
    // stays crisp at small sizes. The "L" is drawn as two bars rather than a font glyph
    // (font hinting/antialiasing turns mushy at 16 px).
    private const float CornerRadiusFraction = 0.18f; // rounded-square corner radius
    private const float MarginFraction        = 0.04f; // gap from icon edge to red square
    private const float BarThicknessFraction  = 0.17f; // stroke width of the "L"
    private const float LetterHeightFraction  = 0.58f; // vertical extent of the "L"
    private const float LetterWidthFraction   = 0.44f; // foot width of the "L"

    // Sizes baked into the .ico — covers 100/125/150/200% tray DPI without upscaling.
    private static readonly int[] IconSizes = [32, 24, 20, 16];

    // Lenovo brand red: #E2001A
    private static readonly Color LenovoRed = Color.FromArgb(0xE2, 0x00, 0x1A);

    /// <summary>
    /// Generates a multi-size ICO file and returns its path.
    /// The file is created once; subsequent calls return the cached path immediately.
    /// </summary>
    // Version stamp baked into the filename so an in-place app update regenerates the icon
    // automatically rather than serving the stale cached file from a previous version.
    private const string IconVersion = "v2";

    internal static string GenerateAndSaveTrayIcon(string outputDirectory)
    {
        var icoPath = Path.Combine(outputDirectory, $"LenovoRed-{IconVersion}.ico");
        if (File.Exists(icoPath)) return icoPath;

        SaveAsIco(icoPath);
        return icoPath;
    }

    // ── Private rendering ─────────────────────────────────────────────────────

    private static Bitmap RenderIconBitmap(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.PixelOffsetMode   = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        // Rounded red square background (radius/margin scale with the icon).
        int margin = Math.Max(1, (int)Math.Round(size * MarginFraction));
        var rect   = new Rectangle(margin, margin, size - margin * 2 - 1, size - margin * 2 - 1);
        int radius = Math.Max(2, (int)Math.Round(size * CornerRadiusFraction));
        using (var bg   = new SolidBrush(LenovoRed))
        using (var path = BuildRoundedRectPath(rect, radius))
            g.FillPath(bg, path);

        // White "L": a vertical stem plus a bottom foot, centred as one bounding box.
        float thickness = size * BarThicknessFraction;
        float height    = size * LetterHeightFraction;
        float width     = size * LetterWidthFraction;
        float x0        = (size - width)  / 2f;
        float y0        = (size - height) / 2f;

        using var white = new SolidBrush(Color.White);
        g.FillRectangle(white, x0, y0, thickness, height);                       // vertical stem
        g.FillRectangle(white, x0, y0 + height - thickness, width, thickness);   // bottom foot

        return bmp;
    }

    private static GraphicsPath BuildRoundedRectPath(Rectangle bounds, int radius)
    {
        int d    = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X,         bounds.Y,          d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y,          d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d,   0, 90);
        path.AddArc(bounds.X,         bounds.Bottom - d, d, d,  90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Writes a valid ICO file with one PNG-compressed frame per size in <see cref="IconSizes"/>,
    /// each rendered natively. PNG-in-ICO is supported by Windows Vista and later.
    /// </summary>
    private static void SaveAsIco(string filePath)
    {
        // Render each size natively (no downscaling) so small frames stay sharp.
        var frames = Array.ConvertAll(IconSizes, s =>
        {
            using var bmp = RenderIconBitmap(s);
            using var ms  = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        });

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // ICO file header (6 bytes)
        bw.Write((short)0);                // reserved — must be 0
        bw.Write((short)1);                // type: 1 = icon
        bw.Write((short)IconSizes.Length); // number of images

        // Directory entries (16 bytes each); image data starts after header + directory.
        int dataOffset = 6 + IconSizes.Length * 16;
        for (int i = 0; i < IconSizes.Length; i++)
        {
            bw.Write((byte)IconSizes[i]);  // width  (0 means 256)
            bw.Write((byte)IconSizes[i]);  // height (0 means 256)
            bw.Write((byte)0);             // colour count (0 = true colour)
            bw.Write((byte)0);             // reserved
            bw.Write((short)1);            // colour planes
            bw.Write((short)32);           // bits per pixel
            bw.Write(frames[i].Length);    // data size in bytes
            bw.Write(dataOffset);          // data offset from start of file
            dataOffset += frames[i].Length;
        }

        // Image data
        foreach (var frame in frames)
            bw.Write(frame);
    }
}
