using System.Drawing;

namespace MCscrolls;

public record MonitorIdentifier(string DeviceName, Rectangle Bounds, int DpiScale);

public class MonitorSet
{
    public string Name { get; set; }
    public List<MonitorIdentifier> Monitors { get; set; } = new();
    public Dictionary<string, Point> StoredPositions { get; set; } = new();
    public int LastActiveMonitorIndex { get; set; }

    public MonitorSet(string name)
    {
        Name = name;
    }

    public MonitorIdentifier? GetNextMonitor(MonitorIdentifier current, int direction)
    {
        if (Monitors.Count < 2) return null;
        int idx = Monitors.FindIndex(m => m.DeviceName == current.DeviceName);
        if (idx < 0) return null;
        int next = ((idx + direction) % Monitors.Count + Monitors.Count) % Monitors.Count;
        return Monitors[next];
    }

    public Point GetStoredPosition(MonitorIdentifier monitor)
    {
        if (StoredPositions.TryGetValue(monitor.DeviceName, out var pos))
        {
            // Validate position is within monitor bounds
            if (monitor.Bounds.Contains(pos))
                return pos;
        }
        // Default to center of monitor
        return new Point(
            monitor.Bounds.X + monitor.Bounds.Width / 2,
            monitor.Bounds.Y + monitor.Bounds.Height / 2
        );
    }

    public void SavePosition(MonitorIdentifier monitor, Point position)
    {
        StoredPositions[monitor.DeviceName] = position;
    }
}
