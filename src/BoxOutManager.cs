using System.Runtime.InteropServices;

namespace MCscrolls;

internal sealed class BoxOutManager : IDisposable
{
    private readonly MonitorManager _monitors;
    private readonly HashSet<IntPtr> _boxedOutMonitors = new();
    private bool _isClipped;

    public BoxOutManager(MonitorManager monitors)
    {
        _monitors = monitors;
    }

    /// <summary>
    /// Mark a monitor as boxed out (cursor constrained to its bounds).
    /// </summary>
    public void SetBoxedOut(IntPtr monitorHandle, bool boxed)
    {
        if (boxed)
            _boxedOutMonitors.Add(monitorHandle);
        else
            _boxedOutMonitors.Remove(monitorHandle);
    }

    public bool IsBoxedOut(IntPtr monitorHandle)
    {
        return _boxedOutMonitors.Contains(monitorHandle);
    }

    /// <summary>
    /// Apply ClipCursor constraint to the specified monitor's bounds.
    /// Call when cursor lands on a boxed-out monitor.
    /// </summary>
    public void ApplyClip(IntPtr monitorHandle)
    {
        var info = new NativeMethods.MONITORINFOEX();
        info.cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>();
        if (NativeMethods.GetMonitorInfo(monitorHandle, ref info))
        {
            var rect = info.rcMonitor;
            NativeMethods.ClipCursor(ref rect);
            _isClipped = true;
        }
    }

    /// <summary>
    /// Release any active cursor clip constraint.
    /// Call before moving cursor to another monitor via Alt+Scroll.
    /// </summary>
    public void ReleaseClip()
    {
        if (_isClipped)
        {
            NativeMethods.ClipCursorRelease(IntPtr.Zero);
            _isClipped = false;
        }
    }

    /// <summary>
    /// Handle monitor switch: release clip from source, apply clip to target if boxed.
    /// </summary>
    public void OnMonitorSwitched(MonitorSwitchedEventArgs e)
    {
        ReleaseClip();

        if (IsBoxedOut(e.ToMonitor))
        {
            ApplyClip(e.ToMonitor);
        }
    }

    /// <summary>
    /// Handle display settings change (monitor disconnect/reconnect).
    /// Releases clip and reapplies based on current cursor position.
    /// </summary>
    public void HandleDisplayChange()
    {
        ReleaseClip();

        var currentMonitors = new HashSet<IntPtr>(_monitors.GetAllMonitorHandles());
        _boxedOutMonitors.RemoveWhere(h => !currentMonitors.Contains(h));

        NativeMethods.GetCursorPos(out var cursorPt);
        var currentMonitor = NativeMethods.MonitorFromPoint(cursorPt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (IsBoxedOut(currentMonitor))
        {
            ApplyClip(currentMonitor);
        }
    }

    public void Dispose()
    {
        ReleaseClip();
        _boxedOutMonitors.Clear();
    }
}
