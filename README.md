# Lenovo Power Tray

A lightweight Windows **system-tray app** for Lenovo ThinkPad laptops (built and tested on an
**X1 Yoga Gen 7**) that toggles two power features without opening the slow Lenovo Vantage app:

- **Smart Charge** — battery charge threshold, via the Lenovo Power Manager local-RPC interface
  (the same one Lenovo Vantage uses), through a small native bridge (`LenPower.dll`)
- **Smart Standby** — Modern Standby scheduling, via the `LenovoSmartStandby` Windows service

Left-click the tray icon for a battery dashboard (arc gauge, power source, charge/drain rate,
live feature status); right-click for quick toggles plus a launch-at-startup option.

> ### ⚠️ 100% vibe coded
> This project was written **entirely by an AI assistant ("vibe coded")** through natural-language
> prompting — no line was hand-authored by a human. Treat it accordingly: it works on the author's
> machine, but it drives Lenovo's power-management RPC interface and a Windows service and comes
> with **no warranty**. Read the code before running it elevated on your own hardware.

![WinUI 3](https://img.shields.io/badge/UI-WinUI%203-blue) ![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![License: MIT](https://img.shields.io/badge/License-MIT-green)

## Install

**winget** (recommended):

```powershell
winget install ezpl.LenovoPowerTray
winget upgrade ezpl.LenovoPowerTray   # update later
```

Or grab `LenovoPowerTray-Setup.exe` from the [latest release](https://github.com/ezpl/LenovoPowerTray/releases)
and run it. The installer is **per-user — no admin needed to install** — and offers two options:

- **Run at startup** — auto-starts the app at sign-in. Ticking it asks for elevation once, to
  register a prompt-free elevated logon task.
- **Auto update in background** — a logon task that silently runs `winget upgrade` after each
  sign-in, so the app keeps itself current. Needs no elevation, and only works once the package is
  available from a winget source (see [installer/README.md](installer/README.md)).

The app itself shows a UAC prompt when it launches, since changing the charge threshold / standby
service requires administrator rights.

## Requirements

- Windows 10 (1809+) / Windows 11 on a Lenovo ThinkPad
- .NET 10 SDK to build the app
- A C++ toolset (Visual Studio / Build Tools with **"Desktop development with C++"**) to build the
  native Smart Charge bridge — only needed once; the rest of the app builds without it
- **Administrator rights** — both features require elevation (the app manifest declares
  `requireAdministrator`)

## Build from source

```powershell
# 1. Build the native Smart Charge bridge (LenPower.dll). One-time, needs the C++ toolset.
#    build.cmd locates MSVC + MIDL automatically (incl. VS 2026 Insiders).
cd native
.\build.cmd
cd ..

# 2. Build the app (Release output is Authenticode-signed if a cert is set up — see USAGE.md).
#    The csproj copies native\LenPower.dll next to the executable.
dotnet build -c Release

# 3. Run elevated
dotnet run
# or right-click the compiled LenovoTray.exe → "Run as administrator"
```

> Smart Standby, the dashboard, and auto-start work without the native bridge. If `LenPower.dll`
> is missing, Smart Charge simply shows as **Unavailable** rather than breaking the app.

A UAC prompt appears on first launch. Enable **Launch at startup** from the right-click menu to
auto-start prompt-free on subsequent boots (via a Task Scheduler logon task).

See **[USAGE.md](USAGE.md)** for full usage, code-signing, troubleshooting, and architecture notes.

## Building the installer

The per-user installer is built with [Inno Setup](https://jrsoftware.org/isinfo.php) and distributed
via winget. See **[installer/README.md](installer/README.md)** for the full release workflow:

```powershell
winget install JRSoftware.InnoSetup     # one-time
cd installer
.\build-installer.ps1 -Version 1.0.0    # → installer\Output\LenovoPowerTray-Setup.exe
```

## External libraries

The app targets the Microsoft stack (.NET, Windows App SDK / WinUI 3, `System.*` runtime
packages). The only **non-Microsoft** dependencies are:

| Library | Author | Purpose | License |
|---------|--------|---------|---------|
| [H.NotifyIcon.WinUI](https://github.com/HavenDV/H.NotifyIcon) | HavenDV | System-tray icon + native context menu for WinUI 3 | MIT |
| [TaskScheduler](https://github.com/dahall/TaskScheduler) | David Hall | Managed wrapper over the Windows Task Scheduler API (auto-start) | MIT |

## Credits & acknowledgements

Smart Charge is the hard part: ThinkPad firmware does **not** expose the battery charge threshold
through WMI, so it has to be driven over the Lenovo Power Manager's local-RPC interface — the same
one Lenovo Vantage uses.

- **[LenPwrCtl](https://github.com/alandau/LenPwrCtl)** by **alandau** (MIT) — the Power Manager RPC
  interface in [`native/pwrmgr.idl`](native/pwrmgr.idl) (endpoint, context handles, and the
  `LpcGetChargeThreshold` / `LpcSetChargeThreshold` procedure layout) was **reverse-engineered by
  this project** and is reused here under its MIT license. This app would not be possible without it.
  Huge thanks. 🙏

The `native/` bridge (`lenpower.c`) is a thin wrapper that exposes two flat exports over that
interface for the managed app to P/Invoke; the interface definition itself is alandau's work.

**Tooling:** the installer is built with **[Inno Setup](https://jrsoftware.org/isinfo.php)** by
Jordan Russell & Martijn Laan (free, with attribution under its license), and distributed via
**[winget](https://github.com/microsoft/winget-cli)** (Microsoft, MIT).

## License

[MIT](LICENSE) © Zero Zero Software ([0z0.xyz](https://0z0.xyz)) — you are free to use, modify,
fork, and redistribute, including commercially, **provided you keep the copyright and license
notice**. See [LICENSE](LICENSE) for the full text.
