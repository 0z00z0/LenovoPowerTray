using Microsoft.UI.Xaml.Controls;
using LenovoTray.Features;
using LenovoTray.Helpers;

namespace LenovoTray.UI;

/// <summary>
/// Owns the tray icon's right-click context menu.
///
/// H.NotifyIcon builds a native Win32 popup menu from this <see cref="Flyout"/> on every
/// right-click and invokes each item's <c>Command</c> (the XAML <c>Click</c> and
/// <c>Opening</c> events do NOT fire for the native menu).  Items are therefore created once
/// with command bindings; <see cref="RefreshState"/> resyncs the check marks before the menu
/// is shown.  Each toggle is generated from an <see cref="IToggleFeature"/>, so adding a new
/// feature is a one-line change at the call site.
/// </summary>
internal sealed class TrayMenu
{
    private readonly List<(ToggleMenuFlyoutItem Item, IToggleFeature Feature)> _toggles = [];
    private MenuFlyoutItem? _updateItem;

    /// <summary>The flyout to assign to <c>TaskbarIcon.ContextFlyout</c>.</summary>
    public MenuFlyout Flyout { get; }

    public TrayMenu(IReadOnlyList<IToggleFeature> features, Action onExit)
    {
        Flyout = new MenuFlyout();

        foreach (var feature in features)
        {
            var item = new ToggleMenuFlyoutItem { Text = feature.Name };
            // Capture current IsChecked at click time to avoid TOCTOU: the target state comes
            // from the item the user just interacted with rather than a fresh OS read.
            item.Command = new RelayCommand(() => Toggle(feature, !item.IsChecked));
            _toggles.Add((item, feature));
            Flyout.Items.Add(item);
        }

        Flyout.Items.Add(new MenuFlyoutSeparator());
        Flyout.Items.Add(new MenuFlyoutItem { Text = "Exit", Command = new RelayCommand(onExit) });

        RefreshState();
    }

    /// <summary>
    /// Inserts (or updates) an "Update available" item at the top of the menu.
    /// Safe to call more than once — subsequent calls update the existing item.
    /// </summary>
    public void SetUpdateBadge(string version)
    {
        if (_updateItem is not null)
        {
            _updateItem.Text = $"⬆ Update available: v{version}";
            return;
        }

        _updateItem = new MenuFlyoutItem
        {
            Text    = $"⬆ Update available: v{version}",
            Command = new RelayCommand(() =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "https://github.com/ezpl/LenovoPowerTray/releases/latest")
                { UseShellExecute = true })),
        };

        // Insert before the first toggle item so it's always at the top.
        Flyout.Items.Insert(0, _updateItem);
        Flyout.Items.Insert(1, new MenuFlyoutSeparator());
    }

    /// <summary>Re-reads live state into the toggle check marks and availability. Call right before the menu opens.</summary>
    public void RefreshState()
    {
        foreach (var (item, feature) in _toggles)
        {
            item.IsEnabled = SafeCall(() => feature.IsAvailable, fallback: true);
            item.IsChecked = item.IsEnabled && SafeCall(() => feature.IsEnabled, fallback: false);
        }
    }

    // Apply target state off the UI thread — RPC/service writes can block for seconds.
    private static void Toggle(IToggleFeature feature, bool enable)
        => Task.Run(() =>
        {
            try
            {
                bool ok = feature.SetEnabled(enable);
                if (!ok)
                    System.Diagnostics.Debug.WriteLine($"[TrayMenu] Toggle '{feature.Name}' → {enable} returned false");
            }
            catch (Exception ex)
            {
                // AutoStartFeature can throw InvalidOperationException when exe path can't be resolved.
                System.Diagnostics.Debug.WriteLine($"[TrayMenu] Toggle '{feature.Name}' failed: {ex.Message}");
            }
        });

    private static T SafeCall<T>(Func<T> fn, T fallback)
    {
        try { return fn(); }
        catch { return fallback; }
    }
}
