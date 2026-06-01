using H.NotifyIcon;
using Microsoft.UI.Xaml;
using LenovoTray.Features;
using LenovoTray.Helpers;
using LenovoTray.UI;

namespace LenovoTray;

/// <summary>
/// Application entry point.  Owns the tray icon lifetime and coordinates the
/// dashboard popup and context menu.
/// </summary>
public partial class App : Application
{
    // Invisible WinUI 3 host — the framework exits when every window is closed.
    private Window?          _hostWindow;
    private TaskbarIcon?     _trayIcon;
    private DashboardWindow? _dashboard;
    private TrayMenu?        _menu;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _hostWindow = new MainWindow();
        InitTrayIcon();
    }

    // ── Tray icon ─────────────────────────────────────────────────────────────

    private void InitTrayIcon()
    {
        _trayIcon = (TaskbarIcon)Resources["TrayIcon"];

        // Generate the red "L" .ico once, then load it from disk.
        var exeDir   = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        var iconPath = IconGenerator.GenerateAndSaveTrayIcon(exeDir);
        _trayIcon.Icon = new System.Drawing.Icon(iconPath);

        // Left-click → dashboard. Right-click → native popup menu (refreshed first).
        IToggleFeature[] features =
        [
            new SmartChargeFeature(),
            new SmartStandbyFeature(),
            new AutoStartFeature(),
        ];
        _menu = new TrayMenu(features, Shutdown);
        _trayIcon.ContextFlyout     = _menu.Flyout;
        _trayIcon.LeftClickCommand  = new RelayCommand(ToggleDashboard);
        _trayIcon.RightClickCommand = new RelayCommand(() => _menu!.RefreshState());

        _trayIcon.ForceCreate();
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    // A tray click that lands while the popup is open first deactivates the popup
    // (auto-hiding it); guard against immediately re-showing it from the same click.
    private const int ReopenGuardMs = 300;

    private void ToggleDashboard()
    {
        // Lazily create the window once and reuse it; subscribe Closed only at creation
        // so handlers don't accumulate on every click.
        if (_dashboard is null)
        {
            _dashboard = new DashboardWindow();
            _dashboard.Closed += (_, _) => _dashboard = null;
        }

        if (_dashboard.AppWindow.IsVisible)
            _dashboard.HideWindow();
        else if (_dashboard.SinceHidden.TotalMilliseconds > ReopenGuardMs)
            _dashboard.ShowNearTray();
        // else: this click is the same gesture that just auto-hid the popup — leave it hidden.
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Shutdown()
    {
        _trayIcon?.Dispose();
        Application.Current.Exit();
    }
}
