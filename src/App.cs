using System.Drawing;
using Microsoft.Win32;

namespace MCscrolls;

internal sealed class App : ApplicationContext
{
    private readonly Settings _settings;
    private readonly MonitorManager _monitors;
    private readonly MouseHook _hook;
    private readonly CursorSwitcher _switcher;
    private readonly GhostCursorManager _ghostManager;
    private readonly BoxOutManager _boxOut;
    private readonly TrayIcon _tray;

    public App()
    {
        _settings = Settings.Load();
        _monitors = new MonitorManager();
        _monitors.RefreshMonitors();

        _hook = new MouseHook();
        _switcher = new CursorSwitcher(_hook, _monitors)
        {
            Enabled = _settings.Enabled,
            CooldownMs = _settings.CooldownMs,
            BetweenSetCooldownMs = _settings.BetweenSetCooldownMs
        };

        // Configure monitor sets from settings
        ConfigureMonitorSets();

        // Configure ghost cursor manager with V2 styling
        _ghostManager = new GhostCursorManager(_monitors)
        {
            Enabled = _settings.GhostCursorsEnabled,
            Style = BuildGhostStyle()
        };
        ApplyPerSetColors();
        _switcher.MonitorSwitched += _ghostManager.OnMonitorSwitched;

        // Configure box-out manager
        _boxOut = new BoxOutManager(_monitors);
        ConfigureBoxOut();
        _switcher.MonitorSwitched += _boxOut.OnMonitorSwitched;

        // Wire set-switching events
        _switcher.SetSwitched += OnSetSwitched;

        _tray = new TrayIcon(_switcher, _monitors, _settings, _ghostManager);
        _tray.SettingsApplied += OnSettingsApplied;
        UpdateTrayForMonitorCount();

        if (!_hook.Install())
        {
            _tray.ShowBalloon("MCscrolls", "Failed to install mouse hook. The application will exit.", ToolTipIcon.Error);
            Task.Delay(3000).ContinueWith(_ => Application.Exit(), TaskScheduler.FromCurrentSynchronizationContext());
            return;
        }

        _ghostManager.Initialize();

        // Apply initial box-out clip if cursor is on a boxed-out monitor
        ApplyInitialBoxOutClip();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    private void ConfigureMonitorSets()
    {
        var sets = BuildMonitorSets();
        if (sets.Count > 0)
        {
            _switcher.SetMonitorSets(sets);

            // Activate the saved set
            int activeIndex = _settings.Sets.FindIndex(s => s.Name == _settings.ActiveSetName);
            if (activeIndex >= 0)
                _switcher.SetActiveSet(activeIndex);
        }
    }

    private List<List<IntPtr>> BuildMonitorSets()
    {
        if (_settings.Sets.Count == 0)
            return new List<List<IntPtr>>();

        var monitorsByDevice = new Dictionary<string, IntPtr>();
        foreach (var mon in _monitors.GetMonitors())
            monitorsByDevice[mon.DeviceName] = mon.Handle;

        var sets = new List<List<IntPtr>>();
        foreach (var setConfig in _settings.Sets)
        {
            var handles = new List<IntPtr>();
            foreach (var entry in setConfig.Monitors.OrderBy(m => m.Order))
            {
                if (monitorsByDevice.TryGetValue(entry.DeviceName, out var handle))
                    handles.Add(handle);
            }
            if (handles.Count > 0)
                sets.Add(handles);
        }
        return sets;
    }

    private void ConfigureBoxOut()
    {
        var monitorsByDevice = new Dictionary<string, IntPtr>();
        foreach (var mon in _monitors.GetMonitors())
            monitorsByDevice[mon.DeviceName] = mon.Handle;

        foreach (var setConfig in _settings.Sets)
        {
            foreach (var entry in setConfig.Monitors)
            {
                if (entry.BoxedOut && monitorsByDevice.TryGetValue(entry.DeviceName, out var handle))
                    _boxOut.SetBoxedOut(handle, true);
            }
        }
    }

    private void ApplyInitialBoxOutClip()
    {
        NativeMethods.GetCursorPos(out var pt);
        var currentMonitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (_boxOut.IsBoxedOut(currentMonitor))
            _boxOut.ApplyClip(currentMonitor);
    }

    private GhostStyle BuildGhostStyle()
    {
        var style = GhostStyle.Default;
        var gs = _settings.GhostStyle;

        // Convert 0-100 opacity to 0-255 byte (100 = fully opaque)
        style.Opacity = gs.Opacity <= 0 ? (byte)255 : (byte)Math.Clamp(gs.Opacity * 255 / 100, 0, 255);
        style.Size = gs.Size > 0 ? gs.Size : 16;

        try
        {
            style.FillColor = ColorTranslator.FromHtml(gs.Color);
        }
        catch
        {
            style.FillColor = Color.White;
        }

        return style;
    }

    private void ApplyPerSetColors()
    {
        foreach (var (setName, colorHex) in _settings.GhostStyle.PerSetColors)
        {
            try
            {
                _ghostManager.SetSetColor(setName, ColorTranslator.FromHtml(colorHex));
            }
            catch
            {
                // Skip invalid colors
            }
        }
    }

    private void OnSettingsApplied()
    {
        _switcher.CooldownMs = _settings.CooldownMs;
        _switcher.BetweenSetCooldownMs = _settings.BetweenSetCooldownMs;
        ConfigureMonitorSets();
        ConfigureBoxOut();
        _ghostManager.Style = BuildGhostStyle();
        _ghostManager.RecreateGhosts();
        _boxOut.HandleDisplayChange();
    }

    private void OnSetSwitched(object? sender, SetSwitchedEventArgs e)
    {
        // Release any box-out clip before switching, then reapply for new set
        _boxOut.ReleaseClip();
        _ghostManager.RecreateGhosts();
        ApplyInitialBoxOutClip();
    }

    private void RefreshAfterHardwareChange()
    {
        _monitors.RefreshMonitors();
        ConfigureMonitorSets();
        _boxOut.HandleDisplayChange();
        _ghostManager.RecreateGhosts();
        _tray.UpdateMonitorOrder(_monitors);
        UpdateTrayForMonitorCount();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        RefreshAfterHardwareChange();
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
            RefreshAfterHardwareChange();
    }

    private void UpdateTrayForMonitorCount()
    {
        if (_monitors.MonitorCount < 2)
            _tray.UpdateTooltip("MCscrolls — 1 monitor detected, waiting for more.");
        else
            _tray.UpdateTooltip("MCscrolls — Monitor Continuity Scrolls");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _switcher.MonitorSwitched -= _ghostManager.OnMonitorSwitched;
            _switcher.MonitorSwitched -= _boxOut.OnMonitorSwitched;
            _switcher.SetSwitched -= OnSetSwitched;
            _tray.SettingsApplied -= OnSettingsApplied;
            _hook.Dispose();
            _switcher.Dispose();
            _boxOut.Dispose();
            _ghostManager.Dispose();
            _tray.Dispose();
        }
        base.Dispose(disposing);
    }
}
