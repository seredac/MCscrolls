# MCscrolls
### Monitor Continuity Scrolls

Switch your cursor between monitors with Alt+Scroll. Each monitor remembers exactly where you left the cursor.

## The Problem

On multi-monitor setups (3+ screens), moving the cursor between displays means dragging across thousands of pixels. Existing tools jump to fixed positions. MCscrolls remembers where you were.

## How It Works

- **Alt + Scroll Up** — jump to the next monitor
- **Alt + Scroll Down** — jump to the previous monitor
- Your cursor lands exactly where you left it on each screen
- **Ghost cursors** on inactive monitors show where you'll land before you scroll

### Monitor Sets (v2)

Group monitors into named sets (e.g. "Workstation", "Dashboard"). Alt+Scroll cycles within the active set. Switch sets with **Ctrl + Alt + PageDown/PageUp**.

Each set maintains its own cursor position memory — switching away and back restores exactly where you were.

### Box-Out Mode (v2)

Lock the cursor to a specific monitor. Normal mouse movement can't leave a boxed-out monitor — only Alt+Scroll can. Useful for keeping your cursor from drifting onto secondary displays.

### Settings GUI (v2)

Right-click the tray icon → **Settings...** to open a visual configuration panel:

- Proportional monitor layout matching your physical setup
- Drag to reorder the scroll sequence within a set
- Assign monitors to sets, toggle box-out per monitor
- Identify button to flash numbers on physical displays
- Ghost cursor styling — opacity, color, size, per-set colors
- Cooldown adjustment for within-set and between-set switching
- Custom hotkey configuration

### Custom Hotkeys (v2)

Change the modifier keys for within-set cycling and between-set switching via the Settings panel. Defaults:

| Action | Default Hotkey |
|--------|---------------|
| Cycle within set | Alt + Scroll |
| Switch between sets | Ctrl + Alt + PageDown/PageUp |

## Install

1. Download `MCscrolls.exe` from [Releases](https://github.com/seredac/MCscrolls/releases).
2. Run it. That's it.

No installer. No dependencies. Runs in your system tray.

## What It Stores

- A small settings file at `%APPDATA%\MCscrolls\settings.json` (your toggle preferences, monitor sets, hotkeys, ghost styling, cooldown)
- Optionally, a registry entry at `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` if you enable "Start with Windows"
- **No network activity. No telemetry. No logging.**

## Build From Source

Requires .NET 8 SDK.

```
git clone https://github.com/seredac/MCscrolls.git
cd MCscrolls
dotnet publish src/MCscrolls.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

### Run Tests

```
dotnet test tests/MCscrolls.Tests.csproj
```

## License

MIT
