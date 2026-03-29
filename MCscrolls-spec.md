# MCscrolls — Build Specification
### Monitor Continuity Scrolls

## Project Overview

Build a Windows system tray utility called **MCscrolls** (Monitor Continuity Scrolls) that lets a user cycle their mouse cursor between monitors using **Alt + Scroll Wheel**, with the cursor restoring to its **exact last-known position** on each monitor.

This solves a real problem for multi-monitor users (3+ displays): instead of dragging the cursor across massive screen real estate, the user scrolls between monitors instantly, and each monitor remembers where the cursor was left.

**Language:** C# (.NET 8+, WinForms or WPF for system tray)
**Target:** Windows 10/11, x64
**License:** MIT
**Repo name:** MCscrolls

---

## Core Behavior

1. On launch, enumerate all connected monitors and store their bounds (position, resolution, DPI scale factor).
2. Initialize a **position dictionary**: one `Point` per monitor, defaulting to each monitor's center.
3. Install a **low-level mouse hook** (`WH_MOUSE_LL`) that intercepts `WM_MOUSEWHEEL` events.
4. When a scroll event fires **while Alt is held**:
   - **Suppress** the scroll event (do not pass it to the active application).
   - Save the current cursor position to the **current monitor's slot** in the dictionary.
   - Determine the **next monitor** (scroll up) or **previous monitor** (scroll down) based on a consistent left-to-right ordering of monitors by their X coordinate.
   - Call `SetCursorPos` to move the cursor to the **stored position** for the target monitor.
   - Update **ghost cursors**: hide the ghost on the target monitor, show/reposition ghosts on all other monitors to their stored coordinates.
5. When a scroll event fires **without Alt held**, pass it through normally with zero interference.
6. Monitor enumeration should refresh on `WM_DISPLAYCHANGE` (monitors added, removed, resolution changed, sleep/wake).
7. On any normal mouse movement (no Alt held), update the current monitor's stored position in the dictionary **and** reposition that monitor's ghost cursor to track the live cursor position. This way, if another monitor's user scrolls to this one, the ghost was always showing the real last-known spot.

---

## Ghost Cursors

Each inactive monitor displays a **ghost cursor sprite** — a semi-transparent cursor icon rendered at the stored coordinates for that monitor. This gives the user a persistent visual of where they'll land when they scroll to that screen.

### Implementation

- Each ghost cursor is a **small, transparent, always-on-top, click-through WPF/WinForms window** (one per inactive monitor).
- Window properties:
  - `FormBorderStyle = None` (no title bar, no border)
  - `TopMost = true` (always on top)
  - `TransparencyKey` or `WS_EX_TRANSPARENT` + `WS_EX_LAYERED` to make the window **click-through** — mouse events pass straight through to whatever is beneath it. This is critical. The ghost must never intercept clicks or interfere with normal use.
  - `ShowInTaskbar = false` (no taskbar entry)
- The ghost sprite should be a **dimmed/semi-transparent version of the default Windows arrow cursor**, approximately 50% opacity. Use a pre-rendered PNG asset or draw it programmatically.
- Ghost window size only needs to be large enough to contain the cursor sprite (~32x32 or ~48x48 pixels depending on DPI).

### Behavior

- On app launch: ghost cursors appear on all monitors except the one the real cursor is currently on, positioned at each monitor's default center.
- On Alt+Scroll switch: the ghost on the **departing** monitor appears at the position the cursor just left. The ghost on the **arriving** monitor disappears (the real cursor is now there).
- On normal mouse movement: the ghost for the **current** monitor is hidden (the real cursor is visible). Ghosts on other monitors remain at their stored positions.
- On monitor disconnect: destroy the ghost window for that monitor.
- On monitor reconnect: create a new ghost window for the new monitor at center.

### Tray Menu Addition

- **Ghost Cursors** — checkbox toggle to show/hide ghost cursors globally. Default: enabled. When disabled, the position memory and scrolling behavior still work — only the visual overlay is hidden.

### Performance

- Ghost windows should **not** repaint on a timer. They only need to update position when:
  - The user performs an Alt+Scroll switch.
  - The stored position changes due to normal cursor movement on another monitor (which only matters when the user switches back — so update lazily on switch, not continuously).
- Exception: if tracking the live cursor to update the current monitor's ghost position in real-time (for accurate preview on other monitors), use the mouse hook's `WM_MOUSEMOVE` passthrough to update the stored coordinate, but only reposition the ghost window on a **throttled interval** (~100ms) to avoid performance overhead.

---

## Monitor Ordering

- Order monitors left-to-right by the `Left` coordinate of their bounding rectangle (`Screen.Bounds.Left`).
- If two monitors share the same X origin (vertically stacked), break ties by `Top` coordinate (top first).
- Cycling wraps around: scrolling past the last monitor goes to the first, and vice versa.
- Store the ordered list and rebuild it on display change events.

---

## DPI Awareness

- The application **must** be Per-Monitor DPI Aware (v2 if possible).
- Set this in the application manifest:
```xml
<dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
```
- Stored coordinates must be in **raw/physical pixel space**, not scaled logical coordinates, so that `SetCursorPos` lands accurately regardless of per-monitor scaling differences.
- When converting between logical and physical coordinates across monitors with different DPI, use `GetDpiForMonitor` or equivalent .NET APIs.

---

## System Tray Application

- Runs as a **system tray icon** with no main window.
- Tray icon context menu:
  - **Enabled** — checkbox toggle to enable/disable the hook without closing the app.
  - **Startup with Windows** — checkbox toggle that manages a Registry Run key or Startup folder shortcut.
  - **Monitor Order** — displays the current detected monitor order (read-only, for debugging).
  - **About** — shows app name, version, GitHub link.
  - **Exit** — unhooks and exits cleanly.
- Tray icon tooltip: "MCscrolls — Monitor Continuity Scrolls"
- Use a simple distinctive icon (a cursor with arrows, or similar). Generate a basic `.ico` file or embed a simple drawn icon.

---

## Error Handling & Edge Cases

- **Single monitor:** App installs but does nothing. Tray tooltip should say "MCscrolls — 1 monitor detected, waiting for more."
- **Monitor disconnected while running:** Rebuild monitor list on `WM_DISPLAYCHANGE`. If the stored position for a removed monitor is orphaned, discard it. If the cursor was on a removed monitor, move it to the center of the primary monitor.
- **Sleep/Wake:** Re-enumerate monitors on resume. Validate all stored positions are still within valid monitor bounds; reset any that are out of bounds to center.
- **Hook failure:** If `SetWindowsHookEx` fails, show a tray balloon notification explaining the app couldn't start and exit gracefully.
- **Alt key state:** Use `GetAsyncKeyState(VK_MENU)` to check Alt state at the moment of the scroll event. Check for both left and right Alt.
- **Scroll debouncing:** High-resolution scroll wheels (e.g., Logitech MX Master free-spin) can fire dozens of scroll events per second. Implement a **cooldown** of ~150ms between monitor switches to prevent rapid unintended cycling. Make this configurable if time allows.
- **Fullscreen applications:** The hook should still work, but note that some exclusive fullscreen apps (games, etc.) may not respect `SetCursorPos`. This is acceptable and does not need to be solved in v1.

---

## Project Structure

```
MCscrolls/
├── MCscrolls.sln
├── src/
│   ├── Program.cs              # Entry point, application setup
│   ├── App.cs                  # System tray application host
│   ├── MouseHook.cs            # Low-level mouse hook (WH_MOUSE_LL)
│   ├── MonitorManager.cs       # Monitor enumeration, ordering, position storage
│   ├── CursorSwitcher.cs       # Core logic: intercept scroll, save/restore positions
│   ├── GhostCursor.cs          # Ghost cursor overlay windows (click-through, semi-transparent)
│   ├── TrayIcon.cs             # System tray icon and context menu
│   ├── NativeMethods.cs        # P/Invoke declarations (SetCursorPos, GetCursorPos, etc.)
│   ├── Settings.cs             # Simple settings (enabled, startup, cooldown)
│   └── app.manifest            # DPI awareness manifest
├── assets/
│   ├── icon.ico                # Tray icon
│   └── ghost-cursor.png        # Semi-transparent cursor sprite for ghost overlay
├── README.md
├── LICENSE                     # MIT
└── .gitignore
```

---

## P/Invoke Signatures Needed

- `SetWindowsHookEx` (WH_MOUSE_LL)
- `CallNextHookEx`
- `UnhookWindowsHookEx`
- `GetCursorPos`
- `SetCursorPos`
- `GetAsyncKeyState` (VK_MENU = 0x12)
- `GetMonitorInfo` / `MonitorFromPoint`
- `EnumDisplayMonitors`
- `GetDpiForMonitor` (Shcore.dll)
- `SetWindowLong` / `GetWindowLong` (for `WS_EX_TRANSPARENT`, `WS_EX_LAYERED`, `WS_EX_TOOLWINDOW`)
- `SetLayeredWindowAttributes` (for ghost cursor opacity)

The `LowLevelMouseProc` callback receives `MSLLHOOKSTRUCT` which contains the `mouseData` field — extract the scroll delta from the high-order word (`HIWORD(mouseData)`). Positive delta = scroll up = next monitor. Negative delta = scroll down = previous monitor.

---

## Build & Run

- Target .NET 8+ with `dotnet build` / `dotnet publish`.
- Publish as a **single-file self-contained executable** for easy distribution:
```
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false
```
- The published binary should be runnable with zero dependencies — no .NET runtime install required.
- Output binary name: `MCscrolls.exe`

### Security Build Notes

- The publish flags include `-p:DebugType=None -p:DebugSymbols=false` to strip PDB paths and source file paths from the binary — these can leak the build machine's username and directory structure.
- In the `.csproj`, explicitly set `<AssemblyTitle>MCscrolls</AssemblyTitle>`, `<Company>MCscrolls</Company>`, and `<Product>MCscrolls</Product>` so assembly metadata doesn't pull defaults from the build environment.
- Before publishing, run a `strings` equivalent on the compiled binary and grep for the build machine username, file paths, and machine name. Nothing identifying should appear.
- Before the first Git commit, verify `.gitconfig` uses the intended public name and email.

---

## What NOT to Build in V1

- No custom modifier key configuration (Alt is hardcoded in v1).
- No GUI settings window (tray menu only).
- No auto-update mechanism.
- No telemetry or analytics.
- No installer — distribute as a single `.exe`.

---

## Testing Checklist

After building, verify the following manually:

- [ ] Alt+ScrollUp moves cursor to next monitor (left-to-right order).
- [ ] Alt+ScrollDown moves cursor to previous monitor.
- [ ] Cursor position is accurately remembered per monitor after switching away and back.
- [ ] Normal scrolling (without Alt) is completely unaffected.
- [ ] App starts minimized to system tray with no visible window.
- [ ] Tray menu Enable/Disable toggle works.
- [ ] Cycling wraps around (last monitor → first monitor and vice versa).
- [ ] Disconnecting a monitor doesn't crash the app.
- [ ] Reconnecting a monitor picks it up automatically.
- [ ] Sleep/wake doesn't break the hook or stored positions.
- [ ] DPI scaling differences between monitors don't offset the cursor position.
- [ ] High-speed scrolling doesn't cause the cursor to fly through multiple monitors uncontrollably.
- [ ] App exits cleanly and removes the hook.
- [ ] Ghost cursors appear on all inactive monitors at their stored positions.
- [ ] Ghost cursor disappears on the monitor the real cursor moves to.
- [ ] Ghost cursor appears on the monitor the real cursor just left.
- [ ] Ghost cursors are fully click-through — clicking where a ghost is displayed interacts with the window beneath it, not the ghost.
- [ ] Ghost cursors do not appear in the taskbar or Alt+Tab.
- [ ] Ghost cursor toggle in tray menu shows/hides all ghosts without affecting scroll behavior.
- [ ] Ghost cursors reposition correctly when monitor layout changes.

---

## README.md Template

Include in the repo:

```markdown
# MCscrolls
### Monitor Continuity Scrolls

Switch your cursor between monitors with Alt+Scroll. Each monitor remembers exactly where you left the cursor.

## The Problem

On multi-monitor setups (3+ screens), moving the cursor between displays means dragging across thousands of pixels. Existing tools jump to fixed positions. MCscrolls remembers where you were.

## How It Works

- **Alt + Scroll Up** — jump to the next monitor
- **Alt + Scroll Down** — jump to the previous monitor
- Your cursor lands exactly where you left it on each screen
- **Ghost cursors** on inactive monitors show you where you'll land before you scroll

## Install

1. Download `MCscrolls.exe` from [Releases](link).
2. Run it. That's it.

No installer. No dependencies. Runs in your system tray.

## Build From Source

Requires .NET 8 SDK.

git clone https://github.com/yourname/MCscrolls.git
cd MCscrolls
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false

## License

MIT
```
