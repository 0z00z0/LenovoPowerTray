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

    /// <summary>The flyout to assign to <c>TaskbarIcon.ContextFlyout</c>.</summary>
    public MenuFlyout Flyout { get; }

    public TrayMenu(IReadOnlyList<IToggleFeature> features, Action onExit)
    {
        Flyout = new MenuFlyout();

        foreach (var feature in features)
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text    = feature.Name,
                Command = new RelayCommand(() => Toggle(feature)),
            };
            _toggles.Add((item, feature));
            Flyout.Items.Add(item);
        }

        Flyout.Items.Add(new MenuFlyoutSeparator());
        Flyout.Items.Add(new MenuFlyoutItem { Text = "Exit", Command = new RelayCommand(onExit) });

        RefreshState();
    }

    /// <summary>Re-reads live state into the toggle check marks. Call right before the menu opens.</summary>
    public void RefreshState()
    {
        foreach (var (item, feature) in _toggles)
            item.IsChecked = SafeIsEnabled(feature);
    }

    // Read current state and flip it off the UI thread — BIOS/service writes can block for seconds.
    private static void Toggle(IToggleFeature feature)
        => Task.Run(() =>
        {
            try { feature.SetEnabled(!SafeIsEnabled(feature)); }
            catch { /* feature implementations already fail soft; guard the thread regardless */ }
        });

    private static bool SafeIsEnabled(IToggleFeature feature)
    {
        try { return feature.IsEnabled; }
        catch { return false; }
    }
}
