using LenovoTray.Helpers;
using LenovoTray.Services;

namespace LenovoTray.Features;

/// <summary>Smart Charge battery threshold, backed by the Lenovo BIOS WMI provider.</summary>
internal sealed class SmartChargeFeature : IToggleFeature
{
    public string Name => "Smart Charge";
    public bool IsEnabled => WmiService.ReadSmartChargeState()?.IsEnabled ?? false;
    public void SetEnabled(bool enabled) => WmiService.SetSmartChargeEnabled(enabled);
}

/// <summary>Smart Standby scheduling, backed by the <c>LenovoSmartStandby</c> Windows service.</summary>
internal sealed class SmartStandbyFeature : IToggleFeature
{
    public string Name => "Smart Standby";
    public bool IsEnabled => StandbyService.IsRunning();
    public void SetEnabled(bool enabled) => StandbyService.SetEnabled(enabled);
}

/// <summary>Launch-at-logon, backed by a Task Scheduler entry (UAC-free elevated start).</summary>
internal sealed class AutoStartFeature : IToggleFeature
{
    public string Name => "Launch at startup";
    public bool IsEnabled => TaskSchedulerHelper.IsAutoStartEnabled();
    public void SetEnabled(bool enabled) => TaskSchedulerHelper.SetAutoStart(enabled);
}
