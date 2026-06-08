namespace LenovoTray.Services;

/// <summary>
/// Manages the "charge to 100 % once" travel override.
/// <para>
/// When <see cref="Activate"/> is called the current Smart Charge threshold is saved
/// to settings and the threshold is disabled so the battery can charge to 100 %.
/// On the next Charging → Idle transition (detected by <c>App</c> via
/// <see cref="OnChargeComplete"/>) the saved threshold is restored automatically.
/// The user can also cancel early via <see cref="Cancel"/>.
/// </para>
/// <para>
/// State is persisted so the override survives an app restart mid-charge.
/// </para>
/// </summary>
internal static class TravelOverrideService
{
    /// <summary>True while a travel override is active.</summary>
    public static bool IsActive => SettingsService.Current.TravelOverrideActive;

    /// <summary>
    /// Saves the current thresholds and disables Smart Charge so the battery charges to 100 %.
    /// </summary>
    public static void Activate()
    {
        Task.Run(() =>
        {
            var s     = SettingsService.Current;
            var state = ChargeThresholdService.Read();

            // Remember what to restore only when Smart Charge is on with valid values.
            if (state is { Capable: true, Enabled: true, Start: > 0, Stop: > 0 })
            {
                s.TravelOverrideRevertStart = state.Start;
                s.TravelOverrideRevertStop  = state.Stop;
            }
            else
            {
                // Was already disabled — nothing to restore.
                s.TravelOverrideRevertStart = null;
                s.TravelOverrideRevertStop  = null;
            }

            s.TravelOverrideActive = true;
            SettingsService.Save();

            // Disable threshold (start=0, stop=0 → charge to 100 %).
            ChargeThresholdService.SetEnabled(false);
        });
    }

    /// <summary>Immediately cancels the override and restores the previous thresholds.</summary>
    public static void Cancel() => ApplyRevert();

    /// <summary>
    /// Called by <c>App</c> on every Charging → Idle battery-status transition.
    /// Reverts the threshold when the override is active (i.e. the charge cycle is complete).
    /// </summary>
    public static void OnChargeComplete()
    {
        if (IsActive) ApplyRevert();
    }

    private static void ApplyRevert()
    {
        var s = SettingsService.Current;
        Task.Run(() =>
        {
            try
            {
                if (s.TravelOverrideRevertStart is { } start &&
                    s.TravelOverrideRevertStop  is { } stop)
                {
                    ChargeThresholdService.SetEnabled(true);
                    ChargeThresholdService.SetThresholds(start, stop);
                }
                // If there was no threshold before the override, leave Smart Charge disabled.
            }
            catch { }
            finally
            {
                s.TravelOverrideActive      = false;
                s.TravelOverrideRevertStart = null;
                s.TravelOverrideRevertStop  = null;
                SettingsService.Save();
            }
        });
    }
}
