# MCscrolls V2 — Architecture & Implementation Plan

## Context

Read the entire existing MCscrolls codebase first. Understand the current architecture: `Program.cs`, `App.cs`, `MouseHook.cs`, `MonitorManager.cs`, `CursorSwitcher.cs`, `GhostCursor.cs`, `TrayIcon.cs`, `NativeMethods.cs`, `Settings.cs`. Understand how the mouse hook intercepts Alt+Scroll, how monitors are enumerated and ordered, how cursor positions are stored and restored, and how ghost cursors render on inactive monitors. Every V2 feature builds on top of this existing foundation — do not rewrite what already works.

---

## Feature 1: Monitor Sets

### Concept

Monitors are grouped into named sets. The user defines which physical monitors belong to which set. Only one set is active at a time. Alt+Scroll cycles the cursor between monitors **within the active set only**. Ctrl+Alt+Scroll switches the active set, jumping the cursor to the last-known position in the target set.

### Data Model

```
MonitorSet {
    string Name                          // e.g. "Workstation", "Dashboard"
    List<MonitorIdentifier> Monitors     // ordered list of monitors in this set
    Dictionary<MonitorIdentifier, Point> StoredPositions  // per-monitor cursor memory
    int LastActiveMonitorIndex           // which monitor in this set was last used
}

MonitorIdentifier {
    string DeviceName                    // from Screen.DeviceName or EnumDisplayMonitors
    Rectangle Bounds                     // physical pixel bounds
    int DpiScale                         // for coordinate accuracy
}
```

### Behavior

- On startup, if no sets are configured, all monitors belong to a single default set called "All" — this preserves V1 behavior exactly.
- Alt+Scroll cycles within the active set only. Monitors not in the active set are invisible to the scroll cycle.
- Ctrl+Alt+Scroll moves to the next set. The cursor jumps to the last-known position on the last-active monitor in that set. The previous set's cursor position is frozen.
- Each set maintains its own independent position dictionary. Switching sets does not overwrite positions in the set you're leaving.
- Ghost cursors only render on inactive monitors **within the active set**. Monitors in inactive sets show no ghosts.
- If a set has only one monitor, Alt+Scroll does nothing within that set. Ctrl+Alt+Scroll still works to leave.

### Hook Changes

- `MouseHook.cs` must now detect **two** modifier combinations:
  - Alt+Scroll → fire `CycleWithinSet` event
  - Ctrl+Alt+Scroll → fire `CycleBetweenSets` event
- Check `GetAsyncKeyState(VK_CONTROL)` in addition to existing `GetAsyncKeyState(VK_MENU)`
- Suppress scroll event for both combinations

### Integration Points

- `CursorSwitcher.cs` needs a reference to the active `MonitorSet` and must scope all cycling logic to that set's monitor list
- `MonitorManager.cs` needs to expose which set a given monitor belongs to
- `GhostCursor.cs` needs to filter which ghosts to show based on active set membership
- `Settings.cs` must serialize/deserialize set configurations

---

## Feature 2: Monitor Visualization Panel

### Concept

A settings window that visually represents all connected monitors as rectangles, scaled proportionally by resolution and oriented correctly (landscape vs portrait). The user can drag monitors to reorder the scroll sequence within a set, assign monitors to sets, and toggle box-out mode per monitor.

### Implementation

- New file: `MonitorPanel.cs` — a WinForms Form
- Canvas area draws rectangles representing each monitor, scaled to fit the window while maintaining aspect ratios
- Each monitor rectangle displays:
  - Monitor number/name
  - Resolution text (e.g. "2560x1440")
  - Set membership (color-coded by set)
  - Box-out indicator (lock icon or border style)
  - Portrait monitors render as tall narrow rectangles
- Drag and drop to reorder scroll sequence within a set
- Right-click context menu on each monitor:
  - Assign to Set → submenu listing available sets + "New Set..."
  - Toggle Box-Out
  - Identify (flashes a number on the physical monitor like Windows Display Settings)
- Bottom of panel:
  - Set management: add, rename, delete sets
  - Hotkey configuration section
  - Apply / Cancel buttons

### Monitor Identification

- Use `EnumDisplayMonitors` and match against `Screen.AllScreens` to get bounds
- To flash identification on physical monitors, create a temporary fullscreen borderless form on each monitor showing its number in large text, auto-dismiss after 2 seconds
- Same approach Windows Display Settings uses with the "Identify" button

### Layout Calculation

- Find the bounding box of all monitors combined
- Scale all monitors proportionally to fit within the panel canvas (e.g. 600x300 pixel drawing area)
- Position them relative to each other matching their actual physical pixel coordinates
- Portrait monitors will naturally appear as tall thin rectangles

### Tray Menu Integration

- Add "Settings..." menu item to existing tray context menu
- Opens the MonitorPanel window
- MonitorPanel is a singleton — clicking "Settings..." while it's open brings it to front

---

## Feature 3: Box-Out Mode

### Concept

When a monitor is "boxed out," the cursor cannot leave that monitor by normal mouse movement (dragging). The only way to exit is via Alt+Scroll. This creates an invisible boundary at the monitor's edges.

### Implementation

- Per-monitor boolean flag: `IsBoxedOut` stored in settings
- When the cursor is on a boxed-out monitor, use `ClipCursor` (Win32 API) to constrain the cursor to that monitor's bounds
- When Alt+Scroll moves the cursor away from a boxed-out monitor, call `ClipCursor(NULL)` to release the constraint before moving, then reapply if the target monitor is also boxed out
- When the cursor lands on a non-boxed monitor, `ClipCursor(NULL)` ensures free movement

### Hook Changes

- `MouseHook.cs` must track `WM_MOUSEMOVE` to detect when the cursor is approaching a monitor boundary on a boxed-out monitor
- Alternatively, apply `ClipCursor` once when landing on a boxed-out monitor and release on Alt+Scroll departure — simpler and more reliable

### P/Invoke Addition

```csharp
[DllImport("user32.dll")]
static extern bool ClipCursor(ref RECT lpRect);

[DllImport("user32.dll")]
static extern bool ClipCursor(IntPtr lpRect); // pass IntPtr.Zero to release
```

### Edge Cases

- If all monitors are boxed out, cursor is always constrained to whichever monitor it's on. Alt+Scroll is the only movement.
- If a boxed-out monitor is disconnected, release ClipCursor immediately
- Sleep/wake must reapply ClipCursor if the cursor is on a boxed-out monitor
- ClipCursor is process-global — only one clip rect at a time. This is fine because there's only one cursor.

---

## Feature 4: Seamless/Boxed Hybrid

### Concept

Within a set, some monitors can have seamless (normal drag-across) behavior between each other while others are boxed out. This is achieved entirely through the per-monitor box-out flag — seamless is just "not boxed out."

### Behavior Matrix

- **Both monitors not boxed out:** Normal Windows cursor movement between them. Alt+Scroll also works.
- **Source monitor boxed out, target not:** Can only reach target via Alt+Scroll. Cannot drag out.
- **Source monitor not boxed out, target boxed out:** Can drag into the boxed-out monitor (cursor enters normally), but once there, cursor is clipped. This might be undesirable — consider whether entering a boxed-out monitor by dragging should be blocked. Implementation decision: block entry by dragging using a secondary hook that detects cross-monitor movement toward a boxed-out monitor and prevents it.
- **Both monitors boxed out:** Alt+Scroll only between them.

### Implementation Decision

Two options for preventing drag-into on boxed monitors:

**Option A (Simpler):** Only clip the cursor when on a boxed monitor. If you drag into a boxed monitor, you're now clipped there. Slightly surprising UX but simple.

**Option B (Stricter):** Use `WM_MOUSEMOVE` in the hook to detect when the cursor crosses into a boxed-out monitor via normal movement and immediately `SetCursorPos` it back to the edge of the previous monitor. This prevents accidental entry entirely.

Recommend **Option A** for V2. Add Option B as a per-monitor toggle ("Strict Boundary") in V3 if users request it.

---

## Feature 5: Custom Hotkeys

### Concept

Allow the user to change the modifier key(s) and scroll action for both within-set cycling and between-set cycling.

### Data Model

```
HotkeyConfig {
    Keys WithinSetModifier     // default: Alt (VK_MENU)
    Keys BetweenSetModifier    // default: Ctrl+Alt (VK_CONTROL + VK_MENU)
    // Scroll wheel is always the trigger — only modifiers are configurable
}
```

### Implementation

- Add hotkey configuration to the Monitor Visualization Panel under an "Advanced" section
- Hotkey capture: user clicks a "Record" button, presses desired modifier(s), app captures via `GetAsyncKeyState` polling or a temporary keyboard hook
- Validate that chosen modifiers don't conflict with common system shortcuts
- Store in `Settings.cs` as virtual key code arrays
- `MouseHook.cs` reads modifier config from settings instead of hardcoding `VK_MENU`

### Constraints

- The scroll wheel is always the trigger — this is non-negotiable, it's the core interaction
- At least one modifier must be held — bare scroll must always pass through
- Within-set and between-set modifiers must be different combinations
- Warn if the user picks a modifier that conflicts with known system shortcuts (e.g. Win+Scroll)

---

## Feature 6: Ghost Cursor Styling

### Concept

Let users customize ghost cursor appearance.

### Options

- Opacity slider: 10% to 90% (default 50%)
- Color tint: preset options (white, blue, green, red, yellow) or custom color picker
- Size: small (24px), medium (32px), large (48px)
- Per-set color: different ghost color per set so users can visually distinguish which set a ghost belongs to

### Implementation

- Add styling section to Monitor Visualization Panel
- `GhostCursor.cs` reads style config from settings
- `SetLayeredWindowAttributes` controls opacity
- Ghost cursor sprite redrawn with tint color in `OnPaint`
- Store in `Settings.cs`

---

## Feature 7: Configurable Cooldown

### Concept

Let users adjust the debounce timing between scroll-triggered monitor switches.

### Implementation

- Slider in settings: 50ms to 500ms (default 150ms)
- `CursorSwitcher.cs` reads cooldown value from settings
- Applied to both within-set and between-set cycling independently
- Lower values for users who want rapid switching, higher for free-spin wheel users

---

## Updated Settings Schema

```json
{
  "enabled": true,
  "ghostCursorsEnabled": true,
  "startWithWindows": false,
  "cooldownMs": 150,
  "betweenSetCooldownMs": 300,
  "hotkeys": {
    "withinSetModifier": ["Alt"],
    "betweenSetModifier": ["Ctrl", "Alt"]
  },
  "ghostStyle": {
    "opacity": 50,
    "color": "#FFFFFF",
    "size": 32,
    "perSetColors": {
      "Workstation": "#FFFFFF",
      "Dashboard": "#4488FF"
    }
  },
  "sets": [
    {
      "name": "Workstation",
      "monitors": [
        { "deviceName": "\\\\.\\DISPLAY1", "order": 0, "boxedOut": false },
        { "deviceName": "\\\\.\\DISPLAY2", "order": 1, "boxedOut": false },
        { "deviceName": "\\\\.\\DISPLAY3", "order": 2, "boxedOut": true }
      ]
    },
    {
      "name": "Dashboard",
      "monitors": [
        { "deviceName": "\\\\.\\DISPLAY4", "order": 0, "boxedOut": true },
        { "deviceName": "\\\\.\\DISPLAY5", "order": 1, "boxedOut": true }
      ]
    }
  ],
  "activeSetName": "Workstation"
}
```

---

## Updated Project Structure

```
MCscrolls/
├── src/
│   ├── Program.cs              # Entry point (unchanged)
│   ├── App.cs                  # Application host (add MonitorPanel wiring)
│   ├── MouseHook.cs            # Add Ctrl+Alt+Scroll detection
│   ├── MonitorManager.cs       # Add set management, box-out tracking
│   ├── MonitorSet.cs           # NEW — MonitorSet data model and logic
│   ├── CursorSwitcher.cs       # Scope cycling to active set, add set switching
│   ├── GhostCursor.cs          # Filter ghosts by active set, add styling
│   ├── BoxOutManager.cs        # NEW — ClipCursor management per monitor
│   ├── MonitorPanel.cs         # NEW — Settings GUI with monitor visualization
│   ├── HotkeyConfig.cs         # NEW — Hotkey capture and validation
│   ├── TrayIcon.cs             # Add "Settings..." menu item
│   ├── NativeMethods.cs        # Add ClipCursor, additional P/Invoke
│   ├── Settings.cs             # Expanded schema with sets, hotkeys, styles
│   └── app.manifest            # DPI awareness (unchanged)
├── assets/
│   ├── icon.ico
│   └── ghost-cursor.png
├── README.md
├── LICENSE
└── .gitignore
```

---

## Implementation Order

Build in this exact sequence. Each step should compile and run before moving to the next.

1. **Settings schema expansion** — Update `Settings.cs` to support the new JSON schema. Ensure backward compatibility: if no sets are defined in the config file, create a default "All" set containing every monitor. Existing V1 settings files must load without error.

2. **MonitorSet data model** — Create `MonitorSet.cs`. Implement the data structures for sets, monitor membership, and per-set position dictionaries. Unit-testable logic only, no UI.

3. **Set-aware cycling** — Modify `CursorSwitcher.cs` to scope Alt+Scroll cycling to the active set's monitor list instead of the global monitor list. With only one default set, behavior is identical to V1.

4. **Between-set switching** — Add Ctrl+Alt+Scroll detection to `MouseHook.cs`. Implement set switching in `CursorSwitcher.cs`: freeze current set positions, activate target set, restore cursor to last-known position in target set.

5. **Ghost cursor set filtering** — Modify `GhostCursor.cs` to only render ghosts for inactive monitors within the active set. Monitors in other sets show no ghosts.

6. **Box-out mode** — Create `BoxOutManager.cs`. Implement `ClipCursor` application when landing on a boxed-out monitor, release on departure. Wire into `CursorSwitcher.cs` switch logic.

7. **Monitor Visualization Panel** — Create `MonitorPanel.cs`. Draw proportional monitor rectangles. Implement drag-to-reorder, right-click context menus, set assignment, box-out toggle, and monitor identification flash.

8. **Custom hotkeys** — Create `HotkeyConfig.cs`. Add hotkey capture UI to MonitorPanel. Update `MouseHook.cs` to read modifier keys from config instead of hardcoding.

9. **Ghost cursor styling** — Add styling options to MonitorPanel. Update `GhostCursor.cs` to apply opacity, color, and size from settings.

10. **Configurable cooldown** — Add cooldown slider to MonitorPanel. Update `CursorSwitcher.cs` to read cooldown values from settings.

---

## Testing Checklist

After each implementation step, verify:

### Set Management
- [ ] Default "All" set created when no config exists (V1 backward compatibility)
- [ ] Alt+Scroll cycles only within active set monitors
- [ ] Ctrl+Alt+Scroll switches to next set
- [ ] Cursor lands at last-known position in target set
- [ ] Positions in previous set are preserved after switching away and back
- [ ] Ghost cursors only appear on inactive monitors within active set
- [ ] Single-monitor sets: Alt+Scroll does nothing, Ctrl+Alt+Scroll works

### Box-Out Mode
- [ ] Cursor cannot drag out of a boxed-out monitor
- [ ] Alt+Scroll exits a boxed-out monitor normally
- [ ] ClipCursor releases before SetCursorPos on switch
- [ ] ClipCursor reapplies on landing if target is boxed out
- [ ] Monitor disconnect releases ClipCursor
- [ ] Sleep/wake reapplies ClipCursor correctly

### Monitor Panel
- [ ] All monitors rendered proportionally with correct orientation
- [ ] Drag to reorder updates scroll sequence
- [ ] Right-click assign to set works
- [ ] Right-click toggle box-out works
- [ ] Identify button flashes numbers on physical monitors
- [ ] Settings persist after Apply and app restart

### Custom Hotkeys
- [ ] User can change within-set modifier
- [ ] User can change between-set modifier
- [ ] Bare scroll always passes through regardless of config
- [ ] Conflicting modifier combinations are rejected

### Ghost Styling
- [ ] Opacity changes apply to all ghost windows
- [ ] Color tint renders correctly
- [ ] Size changes resize ghost windows
- [ ] Per-set colors work when configured

### Backward Compatibility
- [ ] V1 settings file loads without error
- [ ] V1 behavior is identical when no sets are configured
- [ ] Upgrade path: first launch after update creates default set from existing config
