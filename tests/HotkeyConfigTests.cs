using MCscrolls;

namespace MCscrolls.Tests;

public class HotkeyConfigTests
{
    [Fact]
    public void Defaults_AltForWithinSet()
    {
        var config = new HotkeyConfig();
        Assert.Single(config.WithinSetModifiers);
        Assert.Equal(HotkeyConfig.VK_MENU, config.WithinSetModifiers[0]);
    }

    [Fact]
    public void Defaults_CtrlAltForBetweenSet()
    {
        var config = new HotkeyConfig();
        Assert.Equal(2, config.BetweenSetModifiers.Count);
        Assert.Contains(HotkeyConfig.VK_CONTROL, config.BetweenSetModifiers);
        Assert.Contains(HotkeyConfig.VK_MENU, config.BetweenSetModifiers);
    }

    [Fact]
    public void Validate_ValidConfig_ReturnsNull()
    {
        var config = new HotkeyConfig();
        Assert.Null(config.Validate());
    }

    [Fact]
    public void Validate_EmptyWithinSet_ReturnsError()
    {
        var config = new HotkeyConfig { WithinSetModifiers = new List<int>() };
        Assert.NotNull(config.Validate());
        Assert.Contains("Within-set", config.Validate()!);
    }

    [Fact]
    public void Validate_EmptyBetweenSet_ReturnsError()
    {
        var config = new HotkeyConfig { BetweenSetModifiers = new List<int>() };
        Assert.NotNull(config.Validate());
        Assert.Contains("Between-set", config.Validate()!);
    }

    [Fact]
    public void Validate_IdenticalModifiers_ReturnsError()
    {
        var config = new HotkeyConfig
        {
            WithinSetModifiers = new List<int> { HotkeyConfig.VK_MENU },
            BetweenSetModifiers = new List<int> { HotkeyConfig.VK_MENU }
        };
        Assert.NotNull(config.Validate());
        Assert.Contains("different", config.Validate()!);
    }

    [Fact]
    public void Validate_WinKey_ReturnsWarning()
    {
        var config = new HotkeyConfig
        {
            WithinSetModifiers = new List<int> { HotkeyConfig.VK_LWIN },
            BetweenSetModifiers = new List<int> { HotkeyConfig.VK_MENU }
        };
        Assert.NotNull(config.Validate());
        Assert.Contains("Windows", config.Validate()!);
    }

    [Fact]
    public void FromStringArray_ConvertsKnownNames()
    {
        var result = HotkeyConfig.FromStringArray(new List<string> { "Ctrl", "Alt" });
        Assert.Equal(2, result.Count);
        Assert.Contains(HotkeyConfig.VK_CONTROL, result);
        Assert.Contains(HotkeyConfig.VK_MENU, result);
    }

    [Fact]
    public void FromStringArray_IgnoresUnknownNames()
    {
        var result = HotkeyConfig.FromStringArray(new List<string> { "Alt", "FooBar" });
        Assert.Single(result);
        Assert.Equal(HotkeyConfig.VK_MENU, result[0]);
    }

    [Fact]
    public void FromStringArray_CaseInsensitive()
    {
        var result = HotkeyConfig.FromStringArray(new List<string> { "ctrl", "ALT", "Shift" });
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void ToStringArray_ConvertsVKCodes()
    {
        var result = HotkeyConfig.ToStringArray(new List<int> { HotkeyConfig.VK_CONTROL, HotkeyConfig.VK_MENU });
        Assert.Equal(new[] { "Ctrl", "Alt" }, result);
    }

    [Fact]
    public void ToStringArray_IgnoresUnknownCodes()
    {
        var result = HotkeyConfig.ToStringArray(new List<int> { HotkeyConfig.VK_MENU, 0xFF });
        Assert.Single(result);
        Assert.Equal("Alt", result[0]);
    }

    [Fact]
    public void GetDisplayString_FormatsCorrectly()
    {
        Assert.Equal("Alt", HotkeyConfig.GetDisplayString(new List<int> { HotkeyConfig.VK_MENU }));
        Assert.Equal("Ctrl + Alt", HotkeyConfig.GetDisplayString(new List<int> { HotkeyConfig.VK_CONTROL, HotkeyConfig.VK_MENU }));
    }

    [Fact]
    public void FromStringArray_And_ToStringArray_RoundTrip()
    {
        var original = new List<string> { "Ctrl", "Alt", "Shift" };
        var vkCodes = HotkeyConfig.FromStringArray(original);
        var restored = HotkeyConfig.ToStringArray(vkCodes);
        Assert.Equal(original, restored);
    }
}
