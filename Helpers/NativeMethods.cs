using System.Runtime.InteropServices;

namespace LenovoTray.Helpers;

/// <summary>Thin wrappers around Win32 APIs used across the app.</summary>
internal static class NativeMethods
{
    // SPI_GETWORKAREA: usable desktop area on the primary display, excluding the taskbar.
    private const uint SPI_GETWORKAREA = 0x0030;

    private const uint MONITOR_DEFAULTTONEAREST = 0x0002;
    private const int  MDT_EFFECTIVE_DPI        = 0;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int  cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, out RECT output, uint winIni);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    /// <summary>
    /// Work area (taskbar-excluded desktop bounds, in physical pixels) and DPI scale factor of the
    /// monitor currently under the mouse cursor — i.e. the screen whose tray the user just clicked.
    /// This positions the popup on the correct monitor and sizes it for that monitor's scaling,
    /// even in mixed-DPI multi-monitor setups.  Falls back to the primary monitor at 100 %.
    /// </summary>
    internal static (RECT WorkArea, double Scale) GetCursorMonitorMetrics()
    {
        if (GetCursorPos(out var cursor))
        {
            var monitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);
            var info    = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

            if (GetMonitorInfo(monitor, ref info))
            {
                double scale = 1.0;
                if (GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX != 0)
                    scale = dpiX / 96.0;

                return (info.rcWork, scale);
            }
        }

        return (GetPrimaryWorkArea(), 1.0);
    }

    /// <summary>
    /// Usable desktop area on the primary monitor (total area minus the taskbar), in physical
    /// pixels.  Falls back to a sensible 1080p work area if the Win32 call fails.
    /// </summary>
    private static RECT GetPrimaryWorkArea()
    {
        if (SystemParametersInfo(SPI_GETWORKAREA, 0, out var rect, 0))
            return rect;

        // Fallback: assume a typical 1920×1040 work area (1080p minus a 40 px taskbar).
        return new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
    }
}
