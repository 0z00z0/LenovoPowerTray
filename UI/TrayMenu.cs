using Microsoft.UI.Xaml.Controls;
using LenovoTray.Features;
using LenovoTray.Helpers;
using LenovoTray.Services;

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
    private readonly List<(ToggleMenuFlyoutItem Item, ThresholdPreset Preset)> _presetItems = [];

    private MenuFlyoutItem?       _updateItem;
    private MenuFlyoutItem?       _travelItem;
    private ToggleMenuFlyoutItem? _iconModeItem;

    private readonly Action _onIconModeChanged;

    /// <summary>The flyout to assign to <c>TaskbarIcon.ContextFlyout</c>.</summary>
    public MenuFlyout Flyout { get; }

    public TrayMenu(IReadOnlyList<IToggleFeature> features, Action onExit, Action onIconModeChanged)
    {
        _onIconModeChanged = onIconModeChanged;
        Flyout = new MenuFlyout();

        foreach (var feature in features)
        {
            var item = new ToggleMenuFlyoutItem { Text = feature.Name };
            // Capture current IsChecked at click time to avoid TOCTOU: the target state comes
            // from the item the user just interacted with rather than a fresh OS read.
            item.Command = new RelayCommand(() => Toggle(feature, !item.IsChecked));
            _toggles.Add((item, feature));
            Flyout.Items.Add(item);

            // Append preset submenu + travel override directly under Smart Charge.
            if (feature is SmartChargeFeature)
            {
                Flyout.Items.Add(BuildPresetsSubmenu());

                _travelItem = new MenuFlyoutItem
                {
                    Text    = TravelOverrideService.IsActive
                                ? "✕  Cancel charge override"
                                : "⚡  Charge to 100 % once",
                    Command = new RelayCommand(OnTravelOverride),
                };
                Flyout.Items.Add(_travelItem);
            }
        }

        // ── App settings ─────────────────────────────────────────────────────
        Flyout.Items.Add(new MenuFlyoutSeparator());

        _iconModeItem = new ToggleMenuFlyoutItem
        {
            Text      = "Numeric % icon",
            IsChecked = SettingsService.Current.IconMode == TrayIconMode.Numeric,
            Command   = new RelayCommand(ToggleIconMode),
        };
        Flyout.Items.Add(_iconModeItem);

        Flyout.Items.Add(new MenuFlyoutItem
        {
            Text    = "Open settings file",
            Command = new RelayCommand(OpenSettingsFile),
        });

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
            _updateItem.Text = $"⬆  Update available: v{version}";
            return;
        }

        _updateItem = new MenuFlyoutItem
        {
            Text    = $"⬆  Update available: v{version}",
            Command = new RelayCommand(() =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "https://github.com/0z00z0/LenovoPowerTray/releases/latest")
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

        // Preset check marks: highlight whichever preset is active (null = custom / none).
        string? active = SettingsService.Current.ActivePreset;
        foreach (var (item, preset) in _presetItems)
            item.IsChecked = preset.Name == active;

        // Travel override label.
        if (_travelItem is not null)
            _travelItem.Text = TravelOverrideService.IsActive
                ? "✕  Cancel charge override"
                : "⚡  Charge to 100 % once";

        // Icon mode toggle.
        if (_iconModeItem is not null)
            _iconModeItem.IsChecked = SettingsService.Current.IconMode == TrayIconMode.Numeric;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private MenuFlyoutSubItem BuildPresetsSubmenu()
    {
        var sub = new MenuFlyoutSubItem { Text = "Presets" };
        foreach (var preset in SettingsService.Current.Presets)
        {
            var p    = preset; // local copy for lambda capture
            var item = new ToggleMenuFlyoutItem
            {
                Text      = $"{p.Name}  ({p.Start}–{p.Stop} %)",
                IsChecked = p.Name == SettingsService.Current.ActivePreset,
            };
            item.Command = new RelayCommand(() => ApplyPreset(p));
            _presetItems.Add((item, p));
            sub.Items.Add(item);
        }
        return sub;
    }

    private static void ApplyPreset(ThresholdPreset preset)
        => Task.Run(() =>
        {
            try
            {
                // Enable Smart Charge then apply the preset thresholds.
                ChargeThresholdService.SetEnabled(true);
                bool ok = ChargeThresholdService.SetThresholds(preset.Start, preset.Stop);
                if (ok)
                {
                    SettingsService.Current.ActivePreset = preset.Name;
                    SettingsService.Save();
                }
            }
            catch { }
        });

    private static void OnTravelOverride()
    {
        if (TravelOverrideService.IsActive)
            TravelOverrideService.Cancel();
        else
            TravelOverrideService.Activate();
    }

    private void ToggleIconMode()
    {
        var s = SettingsService.Current;
        s.IconMode = s.IconMode == TrayIconMode.Arc ? TrayIconMode.Numeric : TrayIconMode.Arc;
        SettingsService.Save();
        _onIconModeChanged();
    }

    private static void OpenSettingsFile()
    {
        // Open Explorer with the settings file selected so the user can find, copy, or edit it.
        var filePath = SettingsService.FilePath;
        var dir      = Path.GetDirectoryName(filePath) ?? filePath;

        // If the file exists, select it; otherwise just open the folder.
        var args = File.Exists(filePath) ? $"/select,\"{filePath}\"" : $"\"{dir}\"";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = "explorer.exe",
            Arguments       = args,
            UseShellExecute = true,
        });
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
