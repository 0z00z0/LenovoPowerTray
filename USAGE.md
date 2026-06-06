# Lenovo Power Tray

System tray app for ThinkPad laptops.  Exposes two Lenovo power features as
quick toggles without opening Vantage.

## Features

| Feature | Mechanism | Admin required |
|---------|-----------|----------------|
| **Smart Charge** | Lenovo Power Manager RPC via `LenPower.dll` bridge | ✓ |
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

## Installing & updating

End users install via **winget** (`winget install ezpl.LenovoPowerTray`) or by running
`LenovoPowerTray-Setup.exe` from the GitHub releases. The installer is a **per-user Inno Setup**
package — it installs to `%LocalAppData%` with **no admin prompt**, adds a Start-menu shortcut, and
offers two checkboxes: **"Run at startup"** and **"Auto update in background"**. Updates otherwise
come from `winget upgrade`.

The **"Auto update in background"** option creates a non-elevated logon task (`LenovoTray AutoUpdate`)
that runs `winget upgrade` silently after each sign-in — so the app self-updates without the user
running anything, *provided the package is reachable from a winget source* (public `winget-pkgs`
submission or a local source). It needs no elevation to set up.

The app stays `requireAdministrator`, so it elevates only at runtime. The single place the installer
elevates is when "Run at startup" is ticked, to register a `RunLevel=Highest` logon task
(`LenovoTray AutoStart`) — the same task the in-app "Launch at startup" toggle manages.

Building and releasing the installer (needs `winget install JRSoftware.InnoSetup`) is documented in
**[installer/README.md](installer/README.md)**:

```powershell
cd installer
.\build-installer.ps1 -Version 1.0.0
```

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

## Smart Charge (battery charge threshold)

ThinkPad firmware does **not** expose the battery charge threshold through the
Lenovo BIOS WMI provider (`Lenovo_BiosSetting`) — that class has no charge-threshold
key on these machines. The threshold is owned by the **Lenovo Power Manager**, which
Lenovo Vantage drives over a local-RPC (`ncalrpc`) interface.

`ChargeThresholdService` reaches it through a small native bridge, **`LenPower.dll`**
(sources in `native/`), which marshals the Power Manager's RPC calls via a
MIDL-generated client stub. The managed side P/Invokes two flat exports:

- `LenGetChargeThreshold(battery, out capable, out enabled, out start, out stop)`
- `LenSetChargeThreshold(battery, start, stop)`  (start = stop = 0 disables, i.e. charges to 100 %)

### Building the native bridge

`LenPower.dll` is **not** built by `dotnet build`; build it once with the VC++
toolset (any edition with "Desktop development with C++"):

```powershell
cd native
.\build.cmd        # runs MIDL + cl, emits LenPower.dll
```

The csproj copies `native\LenPower.dll` next to the app on build. To verify the
RPC path against your hardware, run (from an **elevated** shell):

```powershell
cd native
.\test-read.ps1    # prints the live capable/enabled/start/stop values
```

If the dashboard shows *"Unavailable"*, the bridge couldn't reach the Power Manager
(DLL missing, driver not installed, or not running elevated); *"Not supported"* means
the firmware reported the battery as not threshold-capable.

## Project structure

```
LenovoChargeThreshold/
├── App.xaml / .cs                   — Tray icon lifetime, coordinates dashboard
├── MainWindow.xaml / .cs            — Invisible 1×1 host window (keeps WinUI 3 alive)
│
├── Services/                        — All direct hardware / OS interaction
│   ├── ChargeThresholdService.cs    — Smart Charge read/write via LenPower.dll (Power Manager RPC)
│   └── StandbyService.cs            — LenovoSmartStandby service start/stop
│
├── native/                          — Native bridge (built separately via build.cmd)
│   ├── pwrmgr.idl                   — RPC interface definition (MIDL input)
│   ├── lenpower.c                   — Flat C exports wrapping the RPC client stub
│   ├── build.cmd                    — MIDL + cl build → LenPower.dll
│   └── test-read.ps1                — Elevated manual read check
│
├── installer/                       — Per-user Inno Setup installer + winget manifests
│   ├── LenovoPowerTray.iss          — Inno script (per-user, optional Run-at-startup task)
│   ├── build-installer.ps1          — publish + compile → Output\LenovoPowerTray-Setup.exe
│   └── winget/                      — winget manifests (ezpl.LenovoPowerTray)
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
  `Task.Run` (RPC and service calls can block for seconds). The dashboard refresh
  reads state on the UI thread (quick registry/service queries).
- **Native interop** — Smart Charge is the one feature that can't be done from
  managed code or WMI; it goes through `LenPower.dll` (see `native/`). The managed
  `ChargeThresholdService` fails soft (`Read()` → `null`) if the bridge or driver
  is absent, so the rest of the app works on non-Lenovo hardware.
- **DPI** — the app is declared `PerMonitorV2`-aware. `AppWindow.Resize/Move` work
  in physical pixels while XAML lays out in DIPs, so the popup is sized and placed
  using the work area **and** DPI scale of the monitor under the cursor
  (`NativeMethods.GetCursorMonitorMetrics`) — correct on mixed-DPI multi-monitor setups.
