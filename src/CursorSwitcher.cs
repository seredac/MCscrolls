using System.Drawing;

namespace MCscrolls;

internal sealed class MonitorSwitchedEventArgs
{
    public IntPtr FromMonitor { get; init; }
    public IntPtr ToMonitor { get; init; }
    public Point PositionLeft { get; init; }
}

internal sealed class CursorSwitcher : IDisposable
{
    private readonly MouseHook _hook;
    private readonly MonitorManager _monitors;
    private DateTime _lastSwitchTime = DateTime.MinValue;
    private int _cooldownMs = 150;
    private bool _enabled = true;

    public event Action<MonitorSwitchedEventArgs>? MonitorSwitched;

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

    public CursorSwitcher(MouseHook hook, MonitorManager monitors)
    {
        _hook = hook;
        _monitors = monitors;
        _hook.ScrollWithAlt += OnScrollWithAlt;
    }

    private void OnScrollWithAlt(int delta)
    {
        if (!_enabled)
            return;

        if (_monitors.MonitorCount < 2)
            return;

        var now = DateTime.UtcNow;
        if ((now - _lastSwitchTime).TotalMilliseconds < _cooldownMs)
            return;

        NativeMethods.GetCursorPos(out var cursorPt);
        var cursorPos = new Point(cursorPt.X, cursorPt.Y);

        IntPtr currentMonitor = _monitors.GetMonitorAt(cursorPos);
        _monitors.SavePosition(currentMonitor, cursorPos);

        int direction = delta > 0 ? 1 : -1;
        IntPtr targetMonitor = _monitors.GetAdjacentMonitor(currentMonitor, direction);

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

    public void Dispose()
    {
        _hook.ScrollWithAlt -= OnScrollWithAlt;
    }
}
