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
    private const int    IconRadius      = 5;
    private const float  LetterScale     = 0.55f; // "L" height relative to icon size
    private const int    BorderMargin    = 1;

    // Lenovo brand red: #E2001A
    private static readonly System.Drawing.Color LenovoRed =
        System.Drawing.Color.FromArgb(0xE2, 0x00, 0x1A);

    /// <summary>
    /// Generates a multi-size ICO file (32 px + 16 px) and returns its path.
    /// The file is created once; subsequent calls return the cached path immediately.
    /// </summary>
    internal static string GenerateAndSaveTrayIcon(string outputDirectory)
    {
        var icoPath = Path.Combine(outputDirectory, "LenovoRed.ico");
        if (File.Exists(icoPath)) return icoPath;

        using var bmp32 = RenderIconBitmap(32);
        SaveAsIco(bmp32, icoPath);
        return icoPath;
    }

    // ── Private rendering ─────────────────────────────────────────────────────

    private static Bitmap RenderIconBitmap(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.Clear(System.Drawing.Color.Transparent);

        // Rounded red square background
        var rect = new Rectangle(BorderMargin, BorderMargin,
                                 size - BorderMargin * 2 - 1,
                                 size - BorderMargin * 2 - 1);
        using var bg   = new SolidBrush(LenovoRed);
        using var path = BuildRoundedRectPath(rect, IconRadius);
        g.FillPath(bg, path);

        // White "L" centred inside the square
        float   fontSize = size * LetterScale;
        using var font  = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var white = new SolidBrush(System.Drawing.Color.White);
        var textSize    = g.MeasureString("L", font);
        g.DrawString("L", font, white,
            x: (size - textSize.Width)  / 2f + 1,
            y: (size - textSize.Height) / 2f - 1);

        return bmp;
    }

    private static GraphicsPath BuildRoundedRectPath(Rectangle bounds, int radius)
    {
        int d    = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X,               bounds.Y,                d, d, 180, 90);
        path.AddArc(bounds.Right - d,        bounds.Y,                d, d, 270, 90);
        path.AddArc(bounds.Right - d,        bounds.Bottom - d,       d, d,   0, 90);
        path.AddArc(bounds.X,               bounds.Bottom - d,       d, d,  90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Writes a valid ICO file containing PNG-compressed frames at 32 px and 16 px.
    /// The PNG-in-ICO format is supported by Windows Vista and later.
    /// </summary>
    private static void SaveAsIco(Bitmap source, string filePath)
    {
        int[] sizes = [32, 16];

        // Pre-render all frames to PNG bytes
        var frames = Array.ConvertAll(sizes, s =>
        {
            using var scaled = new Bitmap(source, s, s);
            using var ms     = new MemoryStream();
            scaled.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        });

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // ICO file header (6 bytes)
        bw.Write((short)0);             // reserved — must be 0
        bw.Write((short)1);             // type: 1 = icon
        bw.Write((short)sizes.Length);  // number of images

        // Directory entries (16 bytes each), data starts after header + directory
        int dataOffset = 6 + sizes.Length * 16;
        for (int i = 0; i < sizes.Length; i++)
        {
            bw.Write((byte)sizes[i]);    // width  (0 means 256)
            bw.Write((byte)sizes[i]);    // height (0 means 256)
            bw.Write((byte)0);           // colour count (0 = true colour)
            bw.Write((byte)0);           // reserved
            bw.Write((short)1);          // colour planes
            bw.Write((short)32);         // bits per pixel
            bw.Write(frames[i].Length);  // data size in bytes
            bw.Write(dataOffset);        // data offset from start of file
            dataOffset += frames[i].Length;
        }

        // Image data
        foreach (var frame in frames)
            bw.Write(frame);
    }
}
