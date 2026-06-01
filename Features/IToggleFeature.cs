namespace LenovoTray.Features;

/// <summary>
/// A user-toggleable on/off capability surfaced in the tray menu (e.g. Smart Charge,
/// Smart Standby, auto-start).  Abstracting the three toggles behind one interface lets
/// <see cref="LenovoTray.UI.TrayMenu"/> build and refresh them uniformly instead of
/// duplicating read-state / flip / write logic per feature.
/// </summary>
internal interface IToggleFeature
{
    /// <summary>Display label shown in the menu.</summary>
    string Name { get; }

    /// <summary>Reads the feature's current state from the OS. May perform I/O.</summary>
    bool IsEnabled { get; }

    /// <summary>Applies the requested state. May block (service/BIOS calls) — call off the UI thread.</summary>
    void SetEnabled(bool enabled);
}
