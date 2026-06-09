using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Devices.Power;
using Windows.Foundation;
using Windows.System.Power;
using LenovoTray.Helpers;
using LenovoTray.Services;

namespace LenovoTray.UI;

/// <summary>
/// Borderless popup that shows battery status and the current state of both Lenovo
/// power features.  Appears bottom-right above the taskbar; auto-dismisses when it
/// loses focus.  Data refreshes every 5 seconds while the window is active.
/// </summary>
public sealed partial class DashboardWindow : Window
{
    // Physical pixel dimensions — DPI scaling is handled by the OS (PerMonitorV2 manifest).
    // Base heights include the history sparkline and the collapsed settings expander header.
    private const int WindowWidth         = 280;
    private const int WindowHeight        = 440;  // base (no sliders, settings collapsed)
    private const int WindowHeightSliders = 530;  // extra ~90 px when threshold sliders shown
    private const int SettingsExpandExtra = 175;  // added when settings expander is open

    // Arc gauge geometry: 100×100 px canvas, 7-o'clock start (135°), 270° sweep.
    private const double GaugeCx         = 50;
    private const double GaugeCy         = 50;
    private const double GaugeRadius     = 38;
    private const double GaugeStartAngle = 135;
    private const double GaugeSweep      = 270;

    // Margin between window edge and work-area boundary (DIPs, scaled per monitor).
    private const int EdgeMargin = 12;

    private readonly DispatcherTimer _refreshTimer;
    private readonly App             _app;

    // When the popup was last hidden — lets the tray click that auto-dismissed it avoid re-showing.
    private DateTime _hiddenAtUtc = DateTime.MinValue;

    // Guards slider ValueChanged handlers from triggering each other recursively.
    private bool _updatingSliders = false;
    // Guards settings controls from writing back to SettingsService during initialisation.
    private bool _updatingSettings = false;

    /// <summary>Time elapsed since the window was last hidden.</summary>
    public TimeSpan SinceHidden => DateTime.UtcNow - _hiddenAtUtc;

    public DashboardWindow(App app)
    {
        _app = app;
        InitializeComponent();
        ConfigureWindowChrome();

        // Track arc never changes — build it once here instead of every refresh tick.
        GaugeTrack.Data = BuildArcGeometry(GaugeCx, GaugeCy, GaugeRadius, GaugeStartAngle, GaugeSweep);

        _refreshTimer       = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += (_, _) => Refresh();

        Activated += OnActivated;
        Closed    += (_, _) => _refreshTimer.Stop();
    }

    // ── Public surface ────────────────────────────────────────────────────────

    /// <summary>Positions the window above the system tray and shows it with fresh data.</summary>
    public void ShowNearTray()
    {
        // Sync settings controls before the window is visible to avoid a flash.
        InitSettingsPanel();

        // Load data before making the window visible to avoid a "Loading…" flash.
        Refresh();

        // Size and position using the initial (base) height; ApplyStatusBadges will resize
        // if the sliders section needs to expand after the background read completes.
        PlaceWindow(GetCurrentWindowHeight());

        AppWindow.Show();

        // Activate() fires the Activated event, which starts the refresh timer.
        Activate();
    }

    /// <summary>
    /// Resizes and repositions the window (above the tray, bottom-right corner) to the
    /// requested logical height. Callable from any thread — uses AppWindow which is thread-safe.
    /// </summary>
    private void PlaceWindow(int logicalHeight)
    {
        // AppWindow works in physical pixels, but the XAML content is in effective pixels
        // (DIPs). Size and place using the work area + DPI of the monitor under the cursor
        // (the screen whose tray was clicked) so content isn't clipped or shown off-monitor.
        var (work, s) = NativeMethods.GetCursorMonitorMetrics();
        int w      = (int)Math.Ceiling(WindowWidth  * s);
        int h      = (int)Math.Ceiling(logicalHeight * s);
        int margin = (int)Math.Ceiling(EdgeMargin    * s);

        AppWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
        AppWindow.Move(new Windows.Graphics.PointInt32(
            work.Right  - w - margin,
            work.Bottom - h - margin));
    }

    private int GetCurrentWindowHeight()
    {
        int h = ThresholdSliders.Visibility == Visibility.Visible
                    ? WindowHeightSliders
                    : WindowHeight;
        if (SettingsExpander?.IsExpanded == true) h += SettingsExpandExtra;
        return h;
    }

    /// <summary>Hides the window without destroying it so it can be shown again cheaply.</summary>
    public void HideWindow()
    {
        _refreshTimer.Stop();
        _hiddenAtUtc = DateTime.UtcNow;
        AppWindow.Hide();
    }

    // ── Window chrome ─────────────────────────────────────────────────────────

    private void ConfigureWindowChrome()
    {
        AppWindow.IsShownInSwitchers = false;

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        presenter.IsResizable   = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
    }

    // ── Focus / activation ────────────────────────────────────────────────────

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState == WindowActivationState.Deactivated)
        {
            // Auto-dismiss when the user clicks away — popup widget behaviour.
            HideWindow();
        }
        else
        {
            _refreshTimer.Start();
        }
    }

    // ── Data refresh ──────────────────────────────────────────────────────────

    private void Refresh()
    {
        // Battery info uses WinRT APIs that must stay on the UI thread.
        RefreshBatteryInfo();

        // Badge updates read from RPC/service — do that work off-thread so a slow Lenovo
        // Power Manager response doesn't freeze the window, then marshal back to apply.
        Task.Run(() =>
        {
            var chargeState = ChargeThresholdService.Read();
            bool standbyOn  = StandbyService.IsRunning();
            DispatcherQueue.TryEnqueue(() => ApplyStatusBadges(chargeState, standbyOn));
        });
    }

    private void RefreshBatteryInfo()
    {
        try
        {
            var report = Battery.AggregateBattery.GetReport();

            // Compute percentage from raw mWh values reported by the battery driver.
            int? pct = null;
            if (report.FullChargeCapacityInMilliwattHours is > 0 and { } full &&
                report.RemainingCapacityInMilliwattHours  is { } remaining)
            {
                pct = Math.Clamp((int)Math.Round(100.0 * remaining / full), 0, 100);
            }

            BatteryPercentText.Text = pct.HasValue ? $"{pct}%" : "--";
            UpdateGaugeArc(pct ?? 0);

            // Idle and Charging both indicate AC is connected.
            bool onAC = report.Status is BatteryStatus.Charging or BatteryStatus.Idle;
            PowerSourceText.Text = onAC ? "AC Power" : "Battery";

            ChargeStatusText.Text = report.Status switch
            {
                BatteryStatus.Charging    => "Charging",
                BatteryStatus.Discharging => "Discharging",
                BatteryStatus.Idle        => "Full / Idle",
                BatteryStatus.NotPresent  => "No Battery",
                _                         => ""
            };

            // Charge rate: positive = power in, negative = drain.
            PowerRateText.Text = report.ChargeRateInMilliwatts switch
            {
                null               => "—",
                0                  => "0 W",
                int mw when mw > 0 => $"+{mw / 1000.0:F1} W",
                int mw             => $"-{Math.Abs(mw) / 1000.0:F1} W"
            };

            // Time remaining / time to full.
            TimeRemainingText.Text = ComputeTimeRemaining(report);

            // History sparkline.
            UpdateSparkline(pct ?? 0);
        }
        catch
        {
            BatteryPercentText.Text = "--";
            ChargeStatusText.Text   = "Error";
        }
    }

    // ── Time remaining ────────────────────────────────────────────────────────

    private static string ComputeTimeRemaining(BatteryReport report)
    {
        // Need a non-trivial charge rate and valid capacity values.
        if (report.ChargeRateInMilliwatts is not { } rate || Math.Abs(rate) < 100)
            return "—";
        if (report.RemainingCapacityInMilliwattHours is not { } remaining)
            return "—";

        if (rate > 0 && report.FullChargeCapacityInMilliwattHours is > 0 and { } full)
        {
            // Charging: time to reach full.
            double h = (full - remaining) / (double)rate;
            return FormatHours(h);
        }
        if (rate < 0)
        {
            // Discharging: time until empty.
            double h = remaining / (double)Math.Abs(rate);
            return FormatHours(h);
        }
        return "—";
    }

    private static string FormatHours(double h)
    {
        if (h <= 0 || double.IsInfinity(h) || double.IsNaN(h)) return "—";
        if (h > 99) return ">99h";
        var ts = TimeSpan.FromHours(h);
        return ts.TotalHours >= 1
            ? $"~{(int)ts.TotalHours}h {ts.Minutes}m"
            : $"~{ts.Minutes}m";
    }

    // ── History sparkline ─────────────────────────────────────────────────────

    private void UpdateSparkline(int currentPct)
    {
        SparklineCanvas.Children.Clear();

        var samples = BatteryHistoryService.GetWindow(TimeSpan.FromHours(1));
        if (samples.Length < 2) return;

        // Canvas size is known once the element has been measured; guard against first render.
        double w = SparklineCanvas.ActualWidth;
        double h = SparklineCanvas.ActualHeight;
        if (w < 4 || h < 4) return;

        const double pad = 4;
        double tMin   = samples[0].At.Ticks;
        double tRange = Math.Max((double)(samples[^1].At.Ticks - samples[0].At.Ticks), 1);

        var poly = new Microsoft.UI.Xaml.Shapes.Polyline
        {
            StrokeThickness = 1.5,
            StrokeLineJoin  = PenLineJoin.Round,
            Stroke          = currentPct switch
            {
                <= 20 => AppColors.GaugeLowBrush,
                <= 50 => AppColors.GaugeMedBrush,
                _     => AppColors.GaugeHighBrush,
            },
        };

        foreach (var (at, pct) in samples)
        {
            double x = pad + (at.Ticks - tMin) / tRange * (w - pad * 2);
            // Y axis: 0% at bottom, 100% at top; invert because canvas Y grows downward.
            double y = (h - pad) - pct / 100.0 * (h - pad * 2);
            poly.Points.Add(new Point(x, y));
        }

        SparklineCanvas.Children.Add(poly);
    }

    // Called on the UI thread after the background read completes.
    private void ApplyStatusBadges(ChargeThresholdState? chargeState, bool standbyOn)
    {
        // ── Smart Charge ──────────────────────────────────────────────────────
        if (chargeState is { Capable: true })
        {
            SetFeatureBadge(SmartChargeBadge, SmartChargeIndicator, chargeState.Enabled);
            SmartChargeDetailText.Text = chargeState.Enabled switch
            {
                true when chargeState.Start > 0 && chargeState.Stop > 0
                    => $"Custom: {chargeState.Start}% → {chargeState.Stop}%",
                true  => "On — reading thresholds…",
                false => "Off — charges to 100%"
            };
        }
        else
        {
            // Read failed (driver/DLL missing or RPC error) or firmware reports not capable.
            SmartChargeBadge.Background     = AppColors.BadgeInactiveBrush;
            SmartChargeIndicator.Background = AppColors.IndicatorOrangeBrush;
            SmartChargeDetailText.Text      = chargeState is null ? "Unavailable" : "Not supported";
        }

        // ── Gauge threshold tick markers ──────────────────────────────────────
        if (chargeState is { Capable: true, Enabled: true, Start: > 0, Stop: > 0 })
        {
            double startAngle = GaugeStartAngle + GaugeSweep * chargeState.Start / 100.0;
            double stopAngle  = GaugeStartAngle + GaugeSweep * chargeState.Stop  / 100.0;
            GaugeStartTick.Data       = BuildTickGeometry(GaugeCx, GaugeCy, startAngle);
            GaugeStopTick.Data        = BuildTickGeometry(GaugeCx, GaugeCy, stopAngle);
            GaugeStartTick.Visibility = Visibility.Visible;
            GaugeStopTick.Visibility  = Visibility.Visible;
        }
        else
        {
            GaugeStartTick.Visibility = Visibility.Collapsed;
            GaugeStopTick.Visibility  = Visibility.Collapsed;
        }

        // ── Threshold sliders ─────────────────────────────────────────────────
        bool showSliders = chargeState is { Capable: true, Enabled: true };
        if (showSliders && chargeState!.Start > 0 && chargeState.Stop > 0)
        {
            _updatingSliders  = true;
            StartSlider.Value = chargeState.Start;
            StopSlider.Value  = chargeState.Stop;
            StartValueText.Text = $"{chargeState.Start}%";
            StopValueText.Text  = $"{chargeState.Stop}%";
            _updatingSliders  = false;
        }
        ThresholdSliders.Visibility = showSliders ? Visibility.Visible : Visibility.Collapsed;

        // Resize window so sliders fit without clipping (only when already visible).
        if (AppWindow.IsVisible)
            PlaceWindow(GetCurrentWindowHeight());

        // ── Smart Standby ─────────────────────────────────────────────────────
        SetFeatureBadge(SmartStandbyBadge, SmartStandbyIndicator, standbyOn);
        SmartStandbyDetailText.Text = standbyOn
            ? "Active — scheduling idle sleep"
            : "Off — always Modern Standby";
    }

    /// <summary>Applies active/inactive colours to a feature badge + indicator pair.</summary>
    private static void SetFeatureBadge(Border badge, Border indicator, bool on)
    {
        badge.Background     = on ? AppColors.BadgeActiveBrush    : AppColors.BadgeInactiveBrush;
        indicator.Background = on ? AppColors.IndicatorGreenBrush : AppColors.IndicatorGreyBrush;
    }

    // ── Threshold slider handlers ─────────────────────────────────────────────

    private void OnStartSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_updatingSliders) return;
        int val = (int)e.NewValue;
        StartValueText.Text = $"{val}%";
        // Enforce at least a 5% gap between start and stop.
        if (StopSlider.Value <= val)
        {
            _updatingSliders   = true;
            StopSlider.Value   = Math.Min(val + 5, 100);
            StopValueText.Text = $"{(int)StopSlider.Value}%";
            _updatingSliders   = false;
        }
    }

    private void OnStopSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_updatingSliders) return;
        int val = (int)e.NewValue;
        StopValueText.Text = $"{val}%";
        // Enforce at least a 5% gap.
        if (StartSlider.Value >= val)
        {
            _updatingSliders    = true;
            StartSlider.Value   = Math.Max(val - 5, 5);
            StartValueText.Text = $"{(int)StartSlider.Value}%";
            _updatingSliders    = false;
        }
    }

    private void OnApplyThresholds(object sender, RoutedEventArgs e)
    {
        int start = (int)StartSlider.Value;
        int stop  = (int)StopSlider.Value;
        ApplyThresholdsButton.IsEnabled = false;
        Task.Run(() =>
        {
            bool ok = ChargeThresholdService.SetThresholds(start, stop);
            DispatcherQueue.TryEnqueue(() =>
            {
                ApplyThresholdsButton.IsEnabled = true;
                if (!ok)
                {
                    SmartChargeDetailText.Text = "Error — check driver";
                    return;
                }
                // Clear the active preset name since the threshold is now custom.
                SettingsService.Current.ActivePreset = null;
                SettingsService.Save();
                // Full Refresh re-reads the service and repaints all badges, ticks, and sliders.
                Refresh();
            });
        });
    }

    // ── Settings panel ────────────────────────────────────────────────────────

    /// <summary>Syncs all settings controls with the current persisted values.</summary>
    private void InitSettingsPanel()
    {
        var s = SettingsService.Current;
        _updatingSettings = true;
        LowBatteryToggle.IsOn         = s.LowBatteryWarningEnabled;
        LowBatteryPctSlider.Value     = s.LowBatteryWarningPct;
        LowBatteryPctValueText.Text   = $"{s.LowBatteryWarningPct}%";
        StartupDelaySlider.Value      = s.StartupDelaySeconds;
        StartupDelayValueText.Text    = $"{s.StartupDelaySeconds} s";
        NumericIconToggle.IsOn        = s.IconMode == TrayIconMode.Numeric;
        LowBatteryPctRow.Visibility   = s.LowBatteryWarningEnabled
                                       ? Visibility.Visible
                                       : Visibility.Collapsed;
        _updatingSettings = false;
    }

    private void OnSettingsExpanderSizeChanged(object sender, SizeChangedEventArgs e)
        => PlaceWindow(GetCurrentWindowHeight());

    private void OnLowBatteryToggled(object sender, RoutedEventArgs e)
    {
        if (_updatingSettings) return;
        var s = SettingsService.Current;
        s.LowBatteryWarningEnabled  = LowBatteryToggle.IsOn;
        LowBatteryPctRow.Visibility = s.LowBatteryWarningEnabled
                                       ? Visibility.Visible
                                       : Visibility.Collapsed;
        SettingsService.Save();
    }

    private void OnLowBatteryPctChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        int val = (int)e.NewValue;
        LowBatteryPctValueText.Text = $"{val}%";
        if (_updatingSettings) return;
        SettingsService.Current.LowBatteryWarningPct = val;
        SettingsService.Save();
    }

    private void OnStartupDelayChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        int val = (int)e.NewValue;
        StartupDelayValueText.Text = $"{val} s";
        if (_updatingSettings) return;
        SettingsService.Current.StartupDelaySeconds = val;
        SettingsService.Save();
    }

    private void OnNumericIconToggled(object sender, RoutedEventArgs e)
    {
        if (_updatingSettings) return;
        SettingsService.Current.IconMode = NumericIconToggle.IsOn
                                            ? TrayIconMode.Numeric
                                            : TrayIconMode.Arc;
        SettingsService.Save();
        // Immediately re-render the tray icon so the change is visible without waiting.
        _app.ForceIconRefresh();
    }

    private void OnExportSettings(object sender, RoutedEventArgs e)
    {
        // Win32 Save dialog (elevation-safe — see NativeMethods). WinRT pickers are unreliable
        // in this requireAdministrator process.
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var path = NativeMethods.ShowSaveFileDialog(hwnd, "Export Lenovo Power Tray settings",
            "LenovoPowerTray-settings.json", "json",
            "Settings JSON (*.json)|*.json|All files (*.*)|*.*");
        if (path is null) return;
        try { SettingsService.Export(path); }
        catch { /* I/O failure must not crash the popup. */ }
    }

    private void OnImportSettings(object sender, RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var path = NativeMethods.ShowOpenFileDialog(hwnd, "Import Lenovo Power Tray settings",
            "json", "Settings JSON (*.json)|*.json|All files (*.*)|*.*");
        if (path is null) return;

        if (SettingsService.Import(path))
        {
            // Reflect the imported values everywhere they're shown.
            InitSettingsPanel();
            Refresh();
            _app.ForceIconRefresh();
        }
    }

    private void OnOpenSettingsFile(object sender, RoutedEventArgs e)
    {
        var filePath = SettingsService.FilePath;
        var dir      = Path.GetDirectoryName(filePath) ?? filePath;
        var args     = File.Exists(filePath) ? $"/select,\"{filePath}\"" : $"\"{dir}\"";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = "explorer.exe",
            Arguments       = args,
            UseShellExecute = true,
        });
    }

    // ── Arc gauge ─────────────────────────────────────────────────────────────

    private void UpdateGaugeArc(int percent)
    {
        // Track geometry is constant and set in the constructor — only fill changes here.
        GaugeFill.Data = percent > 0
            ? BuildArcGeometry(GaugeCx, GaugeCy, GaugeRadius, GaugeStartAngle, GaugeSweep * percent / 100.0)
            : null;

        GaugeFill.Stroke = percent switch
        {
            <= 20 => AppColors.GaugeLowBrush,
            <= 50 => AppColors.GaugeMedBrush,
            _     => AppColors.GaugeHighBrush
        };
    }

    /// <summary>
    /// Builds a short radial tick-mark line on the gauge arc at the given clock-face angle.
    /// Used to mark the Smart Charge start and stop thresholds.
    /// </summary>
    private static Geometry BuildTickGeometry(double cx, double cy, double angleDeg)
    {
        const double innerR = GaugeRadius - 6;
        const double outerR = GaugeRadius + 6;
        double rad = (angleDeg - 90) * Math.PI / 180;
        return new LineGeometry
        {
            StartPoint = new Point(cx + innerR * Math.Cos(rad), cy + innerR * Math.Sin(rad)),
            EndPoint   = new Point(cx + outerR * Math.Cos(rad), cy + outerR * Math.Sin(rad)),
        };
    }

    /// <summary>
    /// Builds a <see cref="PathGeometry"/> for a circular arc.
    /// Angles follow clock-face convention (0° = 12 o'clock, increasing clockwise).
    /// </summary>
    private static Geometry BuildArcGeometry(
        double cx, double cy, double r, double startDeg, double sweepDeg)
    {
        // A full 360° arc is degenerate in SVG/XAML — cap slightly below.
        sweepDeg = Math.Min(sweepDeg, 359.99);

        // Rotate reference frame: clock-face 0° maps to math 270° (i.e. subtract 90°).
        double startRad = (startDeg - 90) * Math.PI / 180;
        double endRad   = (startDeg + sweepDeg - 90) * Math.PI / 180;

        var startPt = new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
        var endPt   = new Point(cx + r * Math.Cos(endRad),   cy + r * Math.Sin(endRad));

        var figure = new PathFigure { StartPoint = startPt, IsClosed = false };
        figure.Segments.Add(new ArcSegment
        {
            Point          = endPt,
            Size           = new Size(r, r),
            IsLargeArc     = sweepDeg > 180,
            SweepDirection = SweepDirection.Clockwise,
            RotationAngle  = 0
        });

        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        return geo;
    }
}
