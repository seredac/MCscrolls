# MCscrolls
### Monitor Continuity Scrolls

Switch your cursor between monitors with Alt+Scroll. Each monitor remembers exactly where you left the cursor.

## The Problem

On multi-monitor setups (3+ screens), moving the cursor between displays means dragging across thousands of pixels. Existing tools jump to fixed positions. MCscrolls remembers where you were.

## How It Works

- **Alt + Scroll Up** — jump to the next monitor
- **Alt + Scroll Down** — jump to the previous monitor
- Your cursor lands exactly where you left it on each screen

## Install

1. Download `MCscrolls.exe` from [Releases](https://github.com/sidserd/MCscrolls/releases).
2. Run it. That's it.

No installer. No dependencies. Runs in your system tray.

## What It Stores

- A small settings file at `%APPDATA%\MCscrolls\settings.json` (your toggle preferences — enabled, ghost cursors, cooldown)
- Optionally, a registry entry at `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` if you enable "Start with Windows"
- **No network activity. No telemetry. No logging.**

## Build From Source

Requires .NET 8 SDK.

```
git clone https://github.com/sidserd/MCscrolls.git
cd MCscrolls
dotnet publish src/MCscrolls.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## License

MIT
