using System.Drawing;

namespace MCscrolls;

internal sealed class MonitorInfo
{
    public IntPtr Handle { get; init; }
    public Rectangle Bounds { get; init; }
    public Rectangle WorkArea { get; init; }
    public uint DpiX { get; init; }
    public uint DpiY { get; init; }
    public string DeviceName { get; init; } = "";
    public int OrderIndex { get; set; }
}

internal sealed class MonitorManager
{
    private readonly List<MonitorInfo> _monitors = new();
    private readonly Dictionary<IntPtr, Point> _storedPositions = new();
    private readonly object _lock = new();

    public int MonitorCount
    {
        get { lock (_lock) return _monitors.Count; }
    }

    public void RefreshMonitors()
    {
        lock (_lock)
        {
            var newMonitors = new List<MonitorInfo>();

            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data) =>
            {
                var info = new NativeMethods.MONITORINFOEX();
                info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFOEX>();

                if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
                {
                    NativeMethods.GetDpiForMonitor(hMonitor, 0, out uint dpiX, out uint dpiY);

                    newMonitors.Add(new MonitorInfo
                    {
                        Handle = hMonitor,
                        Bounds = new Rectangle(
                            info.rcMonitor.Left, info.rcMonitor.Top,
                            info.rcMonitor.Right - info.rcMonitor.Left,
                            info.rcMonitor.Bottom - info.rcMonitor.Top),
                        WorkArea = new Rectangle(
                            info.rcWork.Left, info.rcWork.Top,
                            info.rcWork.Right - info.rcWork.Left,
                            info.rcWork.Bottom - info.rcWork.Top),
                        DpiX = dpiX,
                        DpiY = dpiY,
                        DeviceName = info.szDevice
                    });
                }
                return true;
            }, IntPtr.Zero);

            // Sort left-to-right, then top-to-bottom for ties
            newMonitors.Sort((a, b) =>
            {
                int cmp = a.Bounds.Left.CompareTo(b.Bounds.Left);
                return cmp != 0 ? cmp : a.Bounds.Top.CompareTo(b.Bounds.Top);
            });

            for (int i = 0; i < newMonitors.Count; i++)
                newMonitors[i].OrderIndex = i;

            // Prune stored positions for removed monitors
            var validHandles = new HashSet<IntPtr>(newMonitors.Select(m => m.Handle));
            foreach (var key in _storedPositions.Keys.Where(k => !validHandles.Contains(k)).ToList())
                _storedPositions.Remove(key);

            // Validate remaining positions are within bounds
            foreach (var mon in newMonitors)
            {
                if (_storedPositions.TryGetValue(mon.Handle, out var pos))
                {
                    if (!mon.Bounds.Contains(pos))
                        _storedPositions[mon.Handle] = GetCenter(mon.Bounds);
                }
            }

            _monitors.Clear();
            _monitors.AddRange(newMonitors);
        }
    }

    public IntPtr GetMonitorAt(Point cursorPos)
    {
        var pt = new NativeMethods.POINT { X = cursorPos.X, Y = cursorPos.Y };
        return NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
    }

    public void SavePosition(IntPtr hMonitor, Point pos)
    {
        lock (_lock)
            _storedPositions[hMonitor] = pos;
    }

    public Point GetStoredPosition(IntPtr hMonitor)
    {
        lock (_lock)
        {
            if (_storedPositions.TryGetValue(hMonitor, out var pos))
                return pos;

            var mon = _monitors.FirstOrDefault(m => m.Handle == hMonitor);
            if (mon != null)
                return GetCenter(mon.Bounds);

            return Point.Empty;
        }
    }

    public IntPtr GetAdjacentMonitor(IntPtr currentMonitor, int direction)
    {
        lock (_lock)
        {
            if (_monitors.Count < 2)
                return currentMonitor;

            int idx = _monitors.FindIndex(m => m.Handle == currentMonitor);
            if (idx < 0)
                return currentMonitor;

            int next = ((idx + direction) % _monitors.Count + _monitors.Count) % _monitors.Count;
            return _monitors[next].Handle;
        }
    }

    public string GetOrderSummary()
    {
        lock (_lock)
        {
            if (_monitors.Count == 0)
                return "No monitors detected";

            return string.Join(" → ", _monitors.Select((m, i) =>
                $"#{i + 1}: {m.DeviceName} ({m.Bounds.Width}x{m.Bounds.Height} @{m.DpiX}dpi)"));
        }
    }

    public void MoveCursorToPrimary()
    {
        lock (_lock)
        {
            var primary = _monitors.FirstOrDefault();
            if (primary != null)
            {
                var center = GetCenter(primary.Bounds);
                NativeMethods.SetCursorPos(center.X, center.Y);
            }
        }
    }

    public List<IntPtr> GetAllMonitorHandles()
    {
        lock (_lock)
            return _monitors.Select(m => m.Handle).ToList();
    }

    private static Point GetCenter(Rectangle bounds) =>
        new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
}
