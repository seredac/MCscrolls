using System.Drawing;
using MCscrolls;

namespace MCscrolls.Tests;

public class PanelMonitorInfoTests
{
    [Fact]
    public void Defaults_AllSetNoBoxOut()
    {
        var info = new PanelMonitorInfo();

        Assert.Equal("", info.DeviceName);
        Assert.Equal("All", info.SetName);
        Assert.Equal(0, info.OrderInSet);
        Assert.False(info.IsBoxedOut);
    }

    [Fact]
    public void CanAssignToSet()
    {
        var info = new PanelMonitorInfo
        {
            DeviceName = @"\\.\DISPLAY1",
            Bounds = new Rectangle(0, 0, 1920, 1080),
            SetName = "Work"
        };

        Assert.Equal("Work", info.SetName);
    }

    [Fact]
    public void GroupBySet_WorksCorrectly()
    {
        var monitors = new List<PanelMonitorInfo>
        {
            new() { DeviceName = "D1", SetName = "A" },
            new() { DeviceName = "D2", SetName = "B" },
            new() { DeviceName = "D3", SetName = "A" },
            new() { DeviceName = "D4", SetName = "B" },
            new() { DeviceName = "D5", SetName = "A" },
        };

        var groups = monitors.GroupBy(m => m.SetName).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(3, groups["A"]);
        Assert.Equal(2, groups["B"]);
    }

    [Fact]
    public void ReassignSet_UpdatesGrouping()
    {
        var monitors = new List<PanelMonitorInfo>
        {
            new() { DeviceName = "D1", SetName = "All" },
            new() { DeviceName = "D2", SetName = "All" },
        };

        // Move D2 to a new set
        monitors[1].SetName = "Gaming";

        var allCount = monitors.Count(m => m.SetName == "All");
        var gamingCount = monitors.Count(m => m.SetName == "Gaming");

        Assert.Equal(1, allCount);
        Assert.Equal(1, gamingCount);
    }
}
