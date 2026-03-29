using System.Drawing;
using MCscrolls;

namespace MCscrolls.Tests;

public class MonitorSetTests
{
    private static MonitorIdentifier MakeMon(string name, int x, int y, int w, int h) =>
        new(name, new Rectangle(x, y, w, h), 96);

    [Fact]
    public void GetNextMonitor_CyclesForward()
    {
        var set = new MonitorSet("Test");
        var m1 = MakeMon("D1", 0, 0, 1920, 1080);
        var m2 = MakeMon("D2", 1920, 0, 1920, 1080);
        var m3 = MakeMon("D3", 3840, 0, 1920, 1080);
        set.Monitors.AddRange(new[] { m1, m2, m3 });

        Assert.Equal(m2, set.GetNextMonitor(m1, 1));
        Assert.Equal(m3, set.GetNextMonitor(m2, 1));
    }

    [Fact]
    public void GetNextMonitor_CyclesBackward()
    {
        var set = new MonitorSet("Test");
        var m1 = MakeMon("D1", 0, 0, 1920, 1080);
        var m2 = MakeMon("D2", 1920, 0, 1920, 1080);
        set.Monitors.AddRange(new[] { m1, m2 });

        Assert.Equal(m1, set.GetNextMonitor(m2, -1));
    }

    [Fact]
    public void GetNextMonitor_WrapsForward()
    {
        var set = new MonitorSet("Test");
        var m1 = MakeMon("D1", 0, 0, 1920, 1080);
        var m2 = MakeMon("D2", 1920, 0, 1920, 1080);
        set.Monitors.AddRange(new[] { m1, m2 });

        Assert.Equal(m1, set.GetNextMonitor(m2, 1));
    }

    [Fact]
    public void GetNextMonitor_WrapsBackward()
    {
        var set = new MonitorSet("Test");
        var m1 = MakeMon("D1", 0, 0, 1920, 1080);
        var m2 = MakeMon("D2", 1920, 0, 1920, 1080);
        set.Monitors.AddRange(new[] { m1, m2 });

        Assert.Equal(m2, set.GetNextMonitor(m1, -1));
    }

    [Fact]
    public void GetNextMonitor_SingleMonitor_ReturnsNull()
    {
        var set = new MonitorSet("Test");
        var m1 = MakeMon("D1", 0, 0, 1920, 1080);
        set.Monitors.Add(m1);

        Assert.Null(set.GetNextMonitor(m1, 1));
        Assert.Null(set.GetNextMonitor(m1, -1));
    }

    [Fact]
    public void GetNextMonitor_UnknownMonitor_ReturnsNull()
    {
        var set = new MonitorSet("Test");
        var m1 = MakeMon("D1", 0, 0, 1920, 1080);
        var unknown = MakeMon("D99", 0, 0, 1920, 1080);
        set.Monitors.Add(m1);

        Assert.Null(set.GetNextMonitor(unknown, 1));
    }

    [Fact]
    public void SavePosition_And_GetStoredPosition_RoundTrips()
    {
        var set = new MonitorSet("Test");
        var m1 = MakeMon("D1", 0, 0, 1920, 1080);
        set.Monitors.Add(m1);

        var pos = new Point(500, 300);
        set.SavePosition(m1, pos);

        Assert.Equal(pos, set.GetStoredPosition(m1));
    }

    [Fact]
    public void GetStoredPosition_NoSavedPosition_ReturnsCenter()
    {
        var set = new MonitorSet("Test");
        var m1 = MakeMon("D1", 100, 200, 1920, 1080);
        set.Monitors.Add(m1);

        var pos = set.GetStoredPosition(m1);
        Assert.Equal(100 + 960, pos.X);
        Assert.Equal(200 + 540, pos.Y);
    }

    [Fact]
    public void GetStoredPosition_OutOfBounds_ReturnsCenter()
    {
        var set = new MonitorSet("Test");
        var m1 = MakeMon("D1", 0, 0, 1920, 1080);
        set.Monitors.Add(m1);

        // Save a position, then change monitor bounds so it's out of range
        set.SavePosition(m1, new Point(5000, 5000));

        var pos = set.GetStoredPosition(m1);
        // Should return center since 5000,5000 is outside 0,0,1920,1080
        Assert.Equal(960, pos.X);
        Assert.Equal(540, pos.Y);
    }

    [Fact]
    public void MultipleMonitors_IndependentPositions()
    {
        var set = new MonitorSet("Test");
        var m1 = MakeMon("D1", 0, 0, 1920, 1080);
        var m2 = MakeMon("D2", 1920, 0, 2560, 1440);
        set.Monitors.AddRange(new[] { m1, m2 });

        set.SavePosition(m1, new Point(100, 200));
        set.SavePosition(m2, new Point(2500, 700));

        Assert.Equal(new Point(100, 200), set.GetStoredPosition(m1));
        Assert.Equal(new Point(2500, 700), set.GetStoredPosition(m2));
    }
}
