# Lenovo Power Tray

A lightweight Windows **system-tray app** for Lenovo ThinkPad laptops (built and tested on an
**X1 Yoga Gen 7**) that toggles two power features without opening the slow Lenovo Vantage app:

- **Smart Charge** — battery charge threshold, via the Lenovo BIOS WMI provider (`root\wmi`)
- **Smart Standby** — Modern Standby scheduling, via the `LenovoSmartStandby` Windows service

Left-click the tray icon for a battery dashboard (arc gauge, power source, charge/drain rate,
live feature status); right-click for quick toggles plus a launch-at-startup option.

> ### ⚠️ 100% vibe coded
> This project was written **entirely by an AI assistant ("vibe coded")** through natural-language
> prompting — no line was hand-authored by a human. Treat it accordingly: it works on the author's
> machine, but it pokes at BIOS settings and a Windows service and comes with **no warranty**.
> Read the code before running it elevated on your own hardware.

![WinUI 3](https://img.shields.io/badge/UI-WinUI%203-blue) ![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![License: MIT](https://img.shields.io/badge/License-MIT-green)

## Requirements

- Windows 10 (1809+) / Windows 11 on a Lenovo ThinkPad with the Lenovo BIOS WMI provider
- .NET 10 SDK to build
- **Administrator rights** — both features require elevation (the app manifest declares
  `requireAdministrator`)

## Quick start

```powershell
# Build (Release output is Authenticode-signed if a code-signing cert is set up — see USAGE.md)
dotnet build -c Release

# Run elevated
dotnet run
# or right-click the compiled LenovoTray.exe → "Run as administrator"
```

A UAC prompt appears on first launch. Enable **Launch at startup** from the right-click menu to
auto-start prompt-free on subsequent boots (via a Task Scheduler logon task).

See **[USAGE.md](USAGE.md)** for full usage, code-signing, troubleshooting, and architecture notes.

## External libraries

The app targets the Microsoft stack (.NET, Windows App SDK / WinUI 3, `System.*` runtime
packages). The only **non-Microsoft** dependencies are:

| Library | Author | Purpose | License |
|---------|--------|---------|---------|
| [H.NotifyIcon.WinUI](https://github.com/HavenDV/H.NotifyIcon) | HavenDV | System-tray icon + native context menu for WinUI 3 | MIT |
| [TaskScheduler](https://github.com/dahall/TaskScheduler) | David Hall | Managed wrapper over the Windows Task Scheduler API (auto-start) | MIT |

## License

[MIT](LICENSE) © Zero Zero Software ([0z0.xyz](https://0z0.xyz)) — you are free to use, modify,
fork, and redistribute, including commercially, **provided you keep the copyright and license
notice**. See [LICENSE](LICENSE) for the full text.
