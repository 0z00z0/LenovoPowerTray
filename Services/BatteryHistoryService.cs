namespace LenovoTray.Services;

/// <summary>
/// Thread-safe in-memory ring buffer of recent battery-percentage samples.
/// Samples are recorded by <c>App</c> on every battery-report event;
/// <c>DashboardWindow</c> reads them to render the history sparkline.
/// Nothing is persisted to disk — the history is intentionally session-only.
/// </summary>
internal static class BatteryHistoryService
{
    // 720 samples at 5 s interval = 1 hour of history while the dashboard is open.
    // At idle (OS-driven events every ~30 s) this covers ~6 hours.
    private const int Capacity = 720;

    private static readonly (DateTime At, int Pct)[] _buf = new (DateTime, int)[Capacity];
    private static int _head;
    private static int _count;
    private static readonly Lock _lock = new();

    /// <summary>Appends a new percentage reading. Thread-safe.</summary>
    public static void Record(int pct)
    {
        lock (_lock)
        {
            _buf[_head] = (DateTime.UtcNow, pct);
            _head       = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;
        }
    }

    /// <summary>
    /// Returns all samples within the requested time <paramref name="window"/>,
    /// ordered oldest → newest. Returns an empty array when fewer than two samples exist
    /// (not enough to draw a line).
    /// </summary>
    public static (DateTime At, int Pct)[] GetWindow(TimeSpan window)
    {
        lock (_lock)
        {
            if (_count < 2) return [];
            var cutoff = DateTime.UtcNow - window;
            var result = new List<(DateTime, int)>(_count);
            int start  = (_head - _count + Capacity) % Capacity;
            for (int i = 0; i < _count; i++)
            {
                var s = _buf[(start + i) % Capacity];
                if (s.At >= cutoff) result.Add(s);
            }
            return result.Count >= 2 ? [.. result] : [];
        }
    }
}
