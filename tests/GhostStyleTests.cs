using System.Drawing;
using MCscrolls;

namespace MCscrolls.Tests;

public class GhostStyleTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var style = GhostStyle.Default;

        Assert.Equal(255, style.Opacity);
        Assert.Equal(Color.White, style.FillColor);
        Assert.Equal(26, style.Size);
    }

    [Fact]
    public void CanSetAllProperties()
    {
        var style = new GhostStyle
        {
            Opacity = 128,
            FillColor = Color.Blue,
            Size = 48
        };

        Assert.Equal(128, style.Opacity);
        Assert.Equal(Color.Blue, style.FillColor);
        Assert.Equal(48, style.Size);
    }
}
