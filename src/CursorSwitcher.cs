using System.Drawing;
using System.Runtime.InteropServices;

namespace MCscrolls;

internal sealed class MonitorSwitchedEventArgs
{
    public IntPtr FromMonitor { get; init; }
    public IntPtr ToMonitor { get; init; }
    public Point PositionLeft { get; init; }
}

internal sealed class SetSwitchedEventArgs : EventArgs
{
    public int FromSetIndex { get; }
    public int ToSetIndex { get; }
    public SetSwitchedEventArgs(int from, int to) { FromSetIndex = from; ToSetIndex = to; }
}

internal sealed class CursorSwitcher : IDisposable
{
    private readonly MouseHook _hook;
    private readonly MonitorManager _monitors;
    private readonly KeyboardHook _keyboardHook;
    private DateTime _lastSwitchTime = DateTime.MinValue;
    private int _cooldownMs = 150;
    private bool _enabled = true;

    // Set-aware cycling state
    private List<IntPtr> _activeSetMonitors;
    private List<List<IntPtr>> _allSets;
    private int _activeSetIndex = 0;
    private int _betweenSetCooldownMs = 300;

    public event Action<MonitorSwitchedEventArgs>? MonitorSwitched;
    public event EventHandler<SetSwitchedEventArgs>? SetSwitched;

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public int CooldownMs
    {
        get => _cooldownMs;
        set => _cooldownMs = value;
    }

    public int BetweenSetCooldownMs
    {
        get => _betweenSetCooldownMs;
        set => _betweenSetCooldownMs = value;
    }

    public CursorSwitcher(MouseHook hook, MonitorManager monitors)
    {
        _hook = hook;
        _monitors = monitors;

        // Default: single set containing all monitors (V1 behavior)
        _activeSetMonitors = _monitors.GetAllMonitorHandles();
        _allSets = new List<List<IntPtr>> { new List<IntPtr>(_activeSetMonitors) };

        _hook.ScrollWithAlt += OnScrollWithAlt;

        _keyboardHook = new KeyboardHook();
        _keyboardHook.SetSwitchRequested += SwitchSet;
        _keyboardHook.Install();
    }

    public void SetMonitorSets(List<List<IntPtr>> sets)
    {
        if (sets == null || sets.Count == 0)
            return;

        _allSets = sets.Select(s => new List<IntPtr>(s)).ToList();
        _activeSetIndex = 0;
        _activeSetMonitors = new List<IntPtr>(_allSets[0]);
    }

    public void SetActiveSet(int index)
    {
        if (index < 0 || index >= _allSets.Count)
            return;

        _activeSetIndex = index;
        _activeSetMonitors = new List<IntPtr>(_allSets[index]);
    }

    private void OnScrollWithAlt(int delta)
    {
        if (!_enabled)
            return;

        if (_activeSetMonitors.Count < 2)
            return;

        var now = DateTime.UtcNow;
        if ((now - _lastSwitchTime).TotalMilliseconds < _cooldownMs)
            return;

        NativeMethods.GetCursorPos(out var cursorPt);
        var cursorPos = new Point(cursorPt.X, cursorPt.Y);

        IntPtr currentMonitor = _monitors.GetMonitorAt(cursorPos);
        _monitors.SavePosition(currentMonitor, cursorPos);

        // Cycle within the active set only
        int idx = _activeSetMonitors.IndexOf(currentMonitor);
        if (idx < 0)
            return;

        int direction = delta > 0 ? 1 : -1;
        int next = ((idx + direction) % _activeSetMonitors.Count + _activeSetMonitors.Count) % _activeSetMonitors.Count;
        IntPtr targetMonitor = _activeSetMonitors[next];

        if (targetMonitor == currentMonitor)
            return;

        Point targetPos = _monitors.GetStoredPosition(targetMonitor);
        NativeMethods.SetCursorPos(targetPos.X, targetPos.Y);

        _lastSwitchTime = now;

        MonitorSwitched?.Invoke(new MonitorSwitchedEventArgs
        {
            FromMonitor = currentMonitor,
            ToMonitor = targetMonitor,
            PositionLeft = cursorPos
        });
    }

    public void SwitchSet(int direction)
    {
        if (!_enabled || _allSets.Count < 2)
            return;

        // Release any cursor clip before switching
        NativeMethods.ClipCursorRelease(IntPtr.Zero);

        // Save current cursor position
        NativeMethods.GetCursorPos(out var cursorPt);
        var cursorPos = new Point(cursorPt.X, cursorPt.Y);
        IntPtr currentMonitor = _monitors.GetMonitorAt(cursorPos);
        _monitors.SavePosition(currentMonitor, cursorPos);

        int fromIndex = _activeSetIndex;
        int nextIndex = ((_activeSetIndex + direction) % _allSets.Count + _allSets.Count) % _allSets.Count;

        _activeSetIndex = nextIndex;
        _activeSetMonitors = new List<IntPtr>(_allSets[nextIndex]);

        // Move cursor to target set's first monitor
        if (_activeSetMonitors.Count > 0)
        {
            IntPtr targetMonitor = _activeSetMonitors[0];
            Point targetPos = _monitors.GetStoredPosition(targetMonitor);
            NativeMethods.SetCursorPos(targetPos.X, targetPos.Y);
        }

        SetSwitched?.Invoke(this, new SetSwitchedEventArgs(fromIndex, nextIndex));
    }

    public void Dispose()
    {
        _hook.ScrollWithAlt -= OnScrollWithAlt;
        _keyboardHook.SetSwitchRequested -= SwitchSet;
        _keyboardHook.Dispose();
    }
}

/// <summary>
/// Low-level keyboard hook that detects Ctrl+Alt+PageDown/PageUp for set switching.
/// Same pattern as MouseHook — WH_KEYBOARD_LL is reliable and can't be stolen.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelKeyboardProc _hookProc;

    public event Action<int>? SetSwitchRequested; // +1 = next, -1 = prev

    public KeyboardHook()
    {
        _hookProc = HookCallback;
    }

    public bool Install()
    {
        if (_hookId != IntPtr.Zero)
            return true;

        IntPtr hModule = NativeMethods.GetModuleHandle(null);
        _hookId = NativeMethods.SetKeyboardHookEx(WH_KEYBOARD_LL, _hookProc, hModule, 0);
        return _hookId != IntPtr.Zero;
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            int vkCode = Marshal.ReadInt32(lParam);

            if (vkCode == NativeMethods.VK_NEXT || vkCode == NativeMethods.VK_PRIOR)
            {
                bool ctrlHeld = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
                bool altHeld = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0;

                if (ctrlHeld && altHeld)
                {
                    int direction = vkCode == NativeMethods.VK_NEXT ? 1 : -1;
                    SetSwitchRequested?.Invoke(direction);
                    return (IntPtr)1; // suppress the key
                }
            }
        }

        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Uninstall();
    }
}
