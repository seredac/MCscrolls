using MCscrolls;

namespace MCscrolls.Tests;

public class BoxOutManagerTests
{
    // BoxOutManager requires MonitorManager which uses P/Invoke,
    // so we test the state tracking logic that doesn't need real monitors.

    [Fact]
    public void NewManager_NoMonitorsBoxedOut()
    {
        var monitors = new MonitorManager();
        using var boxOut = new BoxOutManager(monitors);

        Assert.False(boxOut.IsBoxedOut(new IntPtr(1)));
        Assert.False(boxOut.IsBoxedOut(new IntPtr(2)));
    }

    [Fact]
    public void SetBoxedOut_True_TracksState()
    {
        var monitors = new MonitorManager();
        using var boxOut = new BoxOutManager(monitors);

        var handle = new IntPtr(42);
        boxOut.SetBoxedOut(handle, true);

        Assert.True(boxOut.IsBoxedOut(handle));
    }

    [Fact]
    public void SetBoxedOut_False_RemovesState()
    {
        var monitors = new MonitorManager();
        using var boxOut = new BoxOutManager(monitors);

        var handle = new IntPtr(42);
        boxOut.SetBoxedOut(handle, true);
        Assert.True(boxOut.IsBoxedOut(handle));

        boxOut.SetBoxedOut(handle, false);
        Assert.False(boxOut.IsBoxedOut(handle));
    }

    [Fact]
    public void MultipleMonitors_IndependentTracking()
    {
        var monitors = new MonitorManager();
        using var boxOut = new BoxOutManager(monitors);

        var h1 = new IntPtr(1);
        var h2 = new IntPtr(2);
        var h3 = new IntPtr(3);

        boxOut.SetBoxedOut(h1, true);
        boxOut.SetBoxedOut(h2, false);
        boxOut.SetBoxedOut(h3, true);

        Assert.True(boxOut.IsBoxedOut(h1));
        Assert.False(boxOut.IsBoxedOut(h2));
        Assert.True(boxOut.IsBoxedOut(h3));
    }

    [Fact]
    public void SetBoxedOut_Idempotent()
    {
        var monitors = new MonitorManager();
        using var boxOut = new BoxOutManager(monitors);

        var handle = new IntPtr(42);
        boxOut.SetBoxedOut(handle, true);
        boxOut.SetBoxedOut(handle, true);
        boxOut.SetBoxedOut(handle, true);

        Assert.True(boxOut.IsBoxedOut(handle));

        boxOut.SetBoxedOut(handle, false);
        Assert.False(boxOut.IsBoxedOut(handle));
    }

    [Fact]
    public void Dispose_ClearsState()
    {
        var monitors = new MonitorManager();
        var boxOut = new BoxOutManager(monitors);
        var handle = new IntPtr(42);

        boxOut.SetBoxedOut(handle, true);
        Assert.True(boxOut.IsBoxedOut(handle));

        boxOut.Dispose();
        Assert.False(boxOut.IsBoxedOut(handle));
    }
}
