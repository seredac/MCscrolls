using System.Text.Json;
using MCscrolls;

namespace MCscrolls.Tests;

public class SettingsTests
{
    [Fact]
    public void NewSettings_HasV1Defaults()
    {
        var s = new Settings();

        Assert.True(s.Enabled);
        Assert.False(s.StartWithWindows);
        Assert.Equal(150, s.CooldownMs);
        Assert.True(s.GhostCursorsEnabled);
    }

    [Fact]
    public void NewSettings_HasV2Defaults()
    {
        var s = new Settings();

        Assert.Equal(300, s.BetweenSetCooldownMs);
        Assert.Equal("All", s.ActiveSetName);
        Assert.Empty(s.Sets);
        Assert.NotNull(s.Hotkeys);
        Assert.NotNull(s.GhostStyle);
    }

    [Fact]
    public void HotkeySettings_Defaults()
    {
        var h = new HotkeySettings();

        Assert.Equal(new[] { "Alt" }, h.WithinSetModifier);
        Assert.Equal(new[] { "Ctrl", "Shift" }, h.BetweenSetModifier);
    }

    [Fact]
    public void GhostStyleSettings_Defaults()
    {
        var g = new GhostStyleSettings();

        Assert.Equal(100, g.Opacity);
        Assert.Equal("#FFFFFF", g.Color);
        Assert.Equal(26, g.Size);
        Assert.Empty(g.PerSetColors);
    }

    [Fact]
    public void V1Json_DeserializesWithV2Defaults()
    {
        string v1Json = """
        {
            "Enabled": true,
            "StartWithWindows": false,
            "CooldownMs": 200,
            "GhostCursorsEnabled": true
        }
        """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var s = JsonSerializer.Deserialize<Settings>(v1Json, options)!;

        // V1 values preserved
        Assert.Equal(200, s.CooldownMs);
        Assert.True(s.Enabled);

        // V2 defaults applied
        Assert.Equal(300, s.BetweenSetCooldownMs);
        Assert.Equal("All", s.ActiveSetName);
        Assert.Empty(s.Sets);
        Assert.NotNull(s.Hotkeys);
        Assert.NotNull(s.GhostStyle);
    }

    [Fact]
    public void V2Json_FullRoundTrip()
    {
        var original = new Settings
        {
            Enabled = false,
            CooldownMs = 250,
            BetweenSetCooldownMs = 400,
            ActiveSetName = "Work",
            GhostStyle = new GhostStyleSettings
            {
                Opacity = 80,
                Color = "#FF0000",
                Size = 32
            }
        };
        original.Sets.Add(new MonitorSetConfig
        {
            Name = "Work",
            Monitors = new List<MonitorEntry>
            {
                new() { DeviceName = @"\\.\DISPLAY1", Order = 0, BoxedOut = false },
                new() { DeviceName = @"\\.\DISPLAY2", Order = 1, BoxedOut = true }
            }
        });

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
        string json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<Settings>(json, options)!;

        Assert.False(restored.Enabled);
        Assert.Equal(250, restored.CooldownMs);
        Assert.Equal(400, restored.BetweenSetCooldownMs);
        Assert.Equal("Work", restored.ActiveSetName);
        Assert.Equal(80, restored.GhostStyle.Opacity);
        Assert.Equal("#FF0000", restored.GhostStyle.Color);
        Assert.Single(restored.Sets);
        Assert.Equal("Work", restored.Sets[0].Name);
        Assert.Equal(2, restored.Sets[0].Monitors.Count);
        Assert.True(restored.Sets[0].Monitors[1].BoxedOut);
    }

    [Fact]
    public void MonitorEntry_Defaults()
    {
        var entry = new MonitorEntry();

        Assert.Equal("", entry.DeviceName);
        Assert.Equal(0, entry.Order);
        Assert.False(entry.BoxedOut);
    }

    [Fact]
    public void Sets_CanHoldMultipleSets()
    {
        var s = new Settings();
        s.Sets.Add(new MonitorSetConfig { Name = "A" });
        s.Sets.Add(new MonitorSetConfig { Name = "B" });
        s.Sets.Add(new MonitorSetConfig { Name = "C" });

        Assert.Equal(3, s.Sets.Count);
        Assert.Equal("B", s.Sets[1].Name);
    }

    [Fact]
    public void PerSetColors_RoundTrip()
    {
        var gs = new GhostStyleSettings();
        gs.PerSetColors["Work"] = "#FF0000";
        gs.PerSetColors["Play"] = "#00FF00";

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        string json = JsonSerializer.Serialize(gs, options);
        var restored = JsonSerializer.Deserialize<GhostStyleSettings>(json, options)!;

        Assert.Equal("#FF0000", restored.PerSetColors["Work"]);
        Assert.Equal("#00FF00", restored.PerSetColors["Play"]);
    }
}
