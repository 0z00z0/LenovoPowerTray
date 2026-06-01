# Lenovo Power Tray

System tray app for ThinkPad laptops.  Exposes two Lenovo power features as
quick toggles without opening Vantage.

## Features

| Feature | Mechanism | Admin required |
|---------|-----------|----------------|
| **Smart Charge** | Lenovo BIOS via `root\wmi` WMI | ✓ |
| **Smart Standby** | `LenovoSmartStandby` Windows service | ✓ |

## Running

The app manifest declares `requireAdministrator`.  A UAC prompt appears on the
first launch; auto-start via Task Scheduler is prompt-free on subsequent boots.

```powershell
# From an elevated terminal
dotnet run
# or right-click the compiled .exe → "Run as administrator"
```

## Tray interactions

| Input | Action |
|-------|--------|
| Left-click | Toggle the battery dashboard popup |
| Right-click | Context menu with live-state toggles |
| Click away from popup | Auto-dismisses the dashboard |

### Context menu items
- **Smart Charge** — enable/disable the charge threshold  
- **Smart Standby** — start/stop the `LenovoSmartStandby` service  
- **Launch at startup** — add/remove the Task Scheduler auto-start entry  
- **Exit**

## Dashboard popup
Appears bottom-right above the taskbar.  Closes on focus loss.  Refreshes every 5 s.

- Circular arc gauge: charge %, colour-coded green > 50 %, orange ≤ 50 %, red ≤ 20 %  
- Power source (AC / Battery) and charge/drain rate in watts  
- Status badges for Smart Charge (with threshold values when active) and Smart Standby

## Building

```powershell
dotnet build -c Release
```

Output: `bin\Release\net10.0-windows10.0.26100.0\win-x64\`

## Code signing

The app is Authenticode-signed so the UAC elevation prompt shows a verified
publisher (`Zero Zero Software`) instead of *"Unknown Publisher"*.

**One-time setup** — create and trust a self-signed code-signing certificate:

```powershell
.\sign.ps1 -Setup
```

This creates a 5-year cert in `Cert:\CurrentUser\My` and registers it as a trusted
root + trusted publisher for the current user (no admin required).

**Signing happens automatically** on every `Release` build via the `SignOutput`
MSBuild target, which calls `sign.ps1`. To sign manually:

```powershell
.\sign.ps1                                   # signs the latest Release exe
.\sign.ps1 -Path path\to\LenovoTray.exe      # sign a specific file
```

Verify a signature:

```powershell
Get-AuthenticodeSignature .\bin\Release\net10.0-windows10.0.26100.0\win-x64\LenovoTray.exe
```

**Notes**
- The certificate's private key lives only in the Windows cert store — no `.pfx`
  is written to the project folder, so nothing secret is committed.
- To use a real CA-issued certificate, import it into `Cert:\CurrentUser\My` with
  subject `CN=Zero Zero Software` (or pass `-Subject` to `sign.ps1`); signing picks it up
  by subject automatically — no other change needed.
- A self-signed cert is trusted only on machines where `-Setup` has been run.
  Other machines will still show "Unknown Publisher" unless the public cert is
  imported into their trusted stores.

## Auto-start

`HKCU\Run` entries for elevated apps trigger a UAC prompt on every boot.
`TaskSchedulerHelper` instead creates a Task Scheduler logon-trigger task with
`RunLevel = Highest` — elevated, prompt-free.

## WMI setting names

The BIOS key name for Smart Charge varies by firmware revision.  `WmiService`
tries three known variants in order and stops at the first the firmware accepts:

1. `BatteryChargeMode`
2. `SmartChargeMode`
3. `BatteryThresholdEnable`

If the dashboard shows *"Could not read WMI"* on your machine, build in `DEBUG`
mode and call `WmiService.DumpAllSettings()` to inspect available key names.

## Project structure

```
LenovoChargeThreshold/
├── App.xaml / .cs                   — Tray icon lifetime, coordinates dashboard
├── MainWindow.xaml / .cs            — Invisible 1×1 host window (keeps WinUI 3 alive)
│
├── Services/                        — All direct hardware / OS interaction
│   ├── WmiService.cs                — Smart Charge read/write via Lenovo BIOS WMI
│   └── StandbyService.cs            — LenovoSmartStandby service start/stop
│
├── Features/                        — Toggleable capabilities behind one interface
│   ├── IToggleFeature.cs            — Name / IsEnabled / SetEnabled abstraction
│   └── PowerFeatures.cs             — SmartCharge / SmartStandby / AutoStart implementations
│
├── UI/                              — Visual layer
│   ├── DashboardWindow.xaml / .cs   — Battery popup, arc gauge, status badges
│   └── TrayMenu.cs                  — Builds the right-click menu from the feature list
│
└── Helpers/                         — Infrastructure utilities
    ├── AppColors.cs                 — Shared colour constants and pre-allocated brushes
    ├── IconGenerator.cs             — Generates LenovoRed.ico at runtime
    ├── NativeMethods.cs             — Win32 PInvoke (per-monitor work area + DPI)
    ├── RelayCommand.cs              — Minimal ICommand for tray click binding
    └── TaskSchedulerHelper.cs       — Auto-start management via Task Scheduler
```

## Design notes

- **No public API surface** — all service/feature types are `internal`; the only
  `public` class is `App`, required by the WinUI framework.
- **Feature abstraction** — the three menu toggles implement `IToggleFeature`
  (`Name` / `IsEnabled` / `SetEnabled`), so `TrayMenu` builds and refreshes them
  in a single loop with no per-feature branching.
- **UI thread safety** — every toggle write runs on a background thread via
  `Task.Run` (BIOS and service calls can block for seconds). The dashboard refresh
  reads state on the UI thread (quick registry/service queries).
- **DPI** — the app is declared `PerMonitorV2`-aware. `AppWindow.Resize/Move` work
  in physical pixels while XAML lays out in DIPs, so the popup is sized and placed
  using the work area **and** DPI scale of the monitor under the cursor
  (`NativeMethods.GetCursorMonitorMetrics`) — correct on mixed-DPI multi-monitor setups.
