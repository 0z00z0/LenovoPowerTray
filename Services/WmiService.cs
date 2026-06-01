using System.Management;

namespace LenovoTray.Services;

/// <summary>
/// Current Smart Charge configuration as read from the Lenovo BIOS via WMI.
/// Thresholds are <c>null</c> when the firmware does not expose the threshold keys under the
/// expected names, so callers can distinguish "unknown" from a real value.
/// </summary>
internal record SmartChargeState(bool IsEnabled, int? StartThreshold, int? StopThreshold);

/// <summary>
/// Reads and writes Lenovo battery charge settings through the Lenovo BIOS WMI provider
/// (<c>root\wmi</c>).  Requires administrator privileges.
/// </summary>
internal static class WmiService
{
    private const string WmiNamespace = @"root\wmi";

    // Different ThinkPad firmware revisions expose the charge-mode toggle under different names.
    private static readonly string[] EnableSettingNames =
        ["BatteryChargeMode", "SmartChargeMode", "BatteryThresholdEnable"];

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads all Lenovo BIOS settings in a single WMI pass and extracts the Smart Charge state.
    /// Returns <c>null</c> when the WMI class is unavailable or no charge-mode key is found.
    /// </summary>
    internal static SmartChargeState? ReadSmartChargeState()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(WmiNamespace, "SELECT * FROM Lenovo_BiosSetting");
            using var results  = searcher.Get();

            bool? enabled = null;
            int?  start   = null; // null = firmware did not report this key under the expected name
            int?  stop    = null;

            foreach (ManagementObject obj in results)
            {
                var raw   = obj["CurrentSetting"]?.ToString() ?? "";
                var parts = raw.Split(',');
                if (parts.Length < 2) continue;

                var name  = parts[0].Trim();
                var value = parts[1].Trim();

                if (IsEnableSettingName(name))
                    enabled = value is "1" or "Enable" or "Enabled" or "On";
                else if (name.Equals("BatteryChargeThresholdStart", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(value, out var s))
                    start = s;
                else if (name.Equals("BatteryChargeThresholdStop", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(value, out var e))
                    stop = e;
            }

            return enabled is null ? null : new SmartChargeState(enabled.Value, start, stop);
        }
        catch { return null; }
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enables or disables the Smart Charge threshold feature.
    /// Iterates known firmware key names and stops on the first one the BIOS accepts.
    /// </summary>
    internal static bool SetSmartChargeEnabled(bool enable)
    {
        try
        {
            string value  = enable ? "1" : "0";
            bool   written = false;

            foreach (var name in EnableSettingNames)
            {
                // Fresh instance per attempt — do not carry failed state across calls.
                if (TrySetBiosSetting($"{name},{value}"))
                {
                    written = true;
                    break;
                }
            }

            if (!written) return false;

            SaveBiosSettings();
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Writes custom start/stop charge thresholds (0–100 %).
    /// Both values must be written atomically before calling Save, otherwise the
    /// BIOS may reject an inconsistent pair (e.g. start &gt; stop).
    /// </summary>
    internal static bool SetChargeThresholds(int start, int stop)
    {
        try
        {
            // Write both thresholds; require both to succeed before saving.
            bool startOk = TrySetBiosSetting($"BatteryChargeThresholdStart,{start}");
            bool stopOk  = TrySetBiosSetting($"BatteryChargeThresholdStop,{stop}");

            if (!startOk || !stopOk) return false;

            SaveBiosSettings();
            return true;
        }
        catch { return false; }
    }

    // ── Debug (only compiled into DEBUG builds) ───────────────────────────────

    /// <summary>
    /// Returns all raw Lenovo BIOS settings sorted alphabetically.
    /// Use this to discover the exact key names on a specific firmware version.
    /// </summary>
#if DEBUG
    internal static IReadOnlyList<string> DumpAllSettings()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(WmiNamespace, "SELECT * FROM Lenovo_BiosSetting");
            using var results  = searcher.Get();
            return results.Cast<ManagementObject>()
                .Select(o => o["CurrentSetting"]?.ToString() ?? "")
                .Where(s => s.Length > 0)
                .OrderBy(s => s)
                .ToList();
        }
        catch { return []; }
    }
#endif

    // ── Private helpers ───────────────────────────────────────────────────────

    private static bool IsEnableSettingName(string name)
        => EnableSettingNames.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Invokes <c>Lenovo_SetBiosSetting</c> with the given "Name,Value" string.</summary>
    private static bool TrySetBiosSetting(string nameValuePair)
    {
        try
        {
            using var cls = new ManagementClass(
                new ManagementScope(WmiNamespace), new ManagementPath("Lenovo_SetBiosSetting"), null);
            cls.Get();
            using var instance = cls.CreateInstance()
                ?? throw new InvalidOperationException("Could not instantiate Lenovo_SetBiosSetting");
            instance.SetPropertyValue("setting", nameValuePair);
            instance.InvokeMethod("SetBiosSetting", null);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Flushes all pending BIOS setting changes. Must be called after every write session.</summary>
    private static void SaveBiosSettings()
    {
        using var cls = new ManagementClass(
            new ManagementScope(WmiNamespace), new ManagementPath("Lenovo_SaveBiosSettings"), null);
        cls.Get();
        using var instance = cls.CreateInstance()!;
        instance.SetPropertyValue("parameter", "");
        instance.InvokeMethod("SaveBiosSettings", null);
    }
}
