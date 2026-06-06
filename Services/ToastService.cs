using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace LenovoTray.Services;

internal static class ToastService
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        try
        {
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch
        {
            // Toast registration failure must not crash the app.
        }
    }

    public static void NotifyChargeComplete(int stopPct)
    {
        try
        {
            string body = stopPct == 100
                ? "Fully charged"
                : $"Smart Charge stopped at {stopPct}%  —  charged to limit";

            var builder = new AppNotificationBuilder()
                .AddText("Battery charged")
                .AddText(body);

            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch
        {
            // Toast failure must not crash the app.
        }
    }

    public static void NotifyChargingStarted()
    {
        try
        {
            var builder = new AppNotificationBuilder()
                .AddText("Charging")
                .AddText("AC power connected");

            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch
        {
            // Toast failure must not crash the app.
        }
    }

    public static void Cleanup()
    {
        try
        {
            AppNotificationManager.Default.Unregister();
        }
        catch
        {
            // Cleanup failure must not crash the app.
        }
    }
}
