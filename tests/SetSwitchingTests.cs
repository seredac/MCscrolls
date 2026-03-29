using System.Drawing;
using MCscrolls;

namespace MCscrolls.Tests;

/// <summary>
/// Tests for set index arithmetic and state management.
/// Can't test the full CursorSwitcher (needs hooks + real monitors),
/// but we can test MonitorSet cycling logic exhaustively.
/// </summary>
public class SetSwitchingTests
{
    private static MonitorIdentifier MakeMon(string name, int x) =>
        new(name, new Rectangle(x, 0, 1920, 1080), 96);

    [Fact]
    public void TwoSets_CycleForwardAndBack()
    {
        var setA = new MonitorSet("A");
        setA.Monitors.Add(MakeMon("D1", 0));
        setA.Monitors.Add(MakeMon("D2", 1920));

        var setB = new MonitorSet("B");
        setB.Monitors.Add(MakeMon("D3", 3840));

        var sets = new List<MonitorSet> { setA, setB };
        int activeIndex = 0;

        // Forward: A -> B
        activeIndex = WrapIndex(activeIndex + 1, sets.Count);
        Assert.Equal(1, activeIndex);
        Assert.Equal("B", sets[activeIndex].Name);

        // Forward: B -> A (wrap)
        activeIndex = WrapIndex(activeIndex + 1, sets.Count);
        Assert.Equal(0, activeIndex);
        Assert.Equal("A", sets[activeIndex].Name);

        // Back: A -> B (wrap backward)
        activeIndex = WrapIndex(activeIndex - 1, sets.Count);
        Assert.Equal(1, activeIndex);

        // Back: B -> A
        activeIndex = WrapIndex(activeIndex - 1, sets.Count);
        Assert.Equal(0, activeIndex);
    }

    [Fact]
    public void ThreeSets_FullCycleForward()
    {
        int count = 3;
        int idx = 0;

        idx = WrapIndex(idx + 1, count); Assert.Equal(1, idx);
        idx = WrapIndex(idx + 1, count); Assert.Equal(2, idx);
        idx = WrapIndex(idx + 1, count); Assert.Equal(0, idx); // wrap
    }

    [Fact]
    public void ThreeSets_FullCycleBackward()
    {
        int count = 3;
        int idx = 0;

        idx = WrapIndex(idx - 1, count); Assert.Equal(2, idx); // wrap
        idx = WrapIndex(idx - 1, count); Assert.Equal(1, idx);
        idx = WrapIndex(idx - 1, count); Assert.Equal(0, idx);
    }

    [Fact]
    public void RapidAlternation_StaysConsistent()
    {
        int count = 2;
        int idx = 0;

        for (int i = 0; i < 100; i++)
        {
            idx = WrapIndex(idx + 1, count);
            Assert.Equal(i % 2 == 0 ? 1 : 0, idx);
        }
    }

    [Fact]
    public void EachSet_MaintainsIndependentPositions()
    {
        var setA = new MonitorSet("A");
        setA.Monitors.Add(MakeMon("D1", 0));

        var setB = new MonitorSet("B");
        setB.Monitors.Add(MakeMon("D2", 1920));

        setA.SavePosition(setA.Monitors[0], new Point(100, 200));
        setB.SavePosition(setB.Monitors[0], new Point(2000, 500));

        Assert.Equal(new Point(100, 200), setA.GetStoredPosition(setA.Monitors[0]));
        Assert.Equal(new Point(2000, 500), setB.GetStoredPosition(setB.Monitors[0]));

        // Modifying one doesn't affect the other
        setA.SavePosition(setA.Monitors[0], new Point(300, 400));
        Assert.Equal(new Point(2000, 500), setB.GetStoredPosition(setB.Monitors[0]));
    }

    [Fact]
    public void SingleMonitorSet_GetNextReturnsNull()
    {
        var set = new MonitorSet("Solo");
        set.Monitors.Add(MakeMon("D1", 0));

        Assert.Null(set.GetNextMonitor(set.Monitors[0], 1));
        Assert.Null(set.GetNextMonitor(set.Monitors[0], -1));
    }

    [Fact]
    public void EmptySet_GetNextReturnsNull()
    {
        var set = new MonitorSet("Empty");
        var fakeMon = MakeMon("D1", 0);

        Assert.Null(set.GetNextMonitor(fakeMon, 1));
    }

    // Same wrap-around math used in CursorSwitcher.SwitchSet
    private static int WrapIndex(int index, int count) =>
        ((index % count) + count) % count;
}
