using Microsoft.Win32;

namespace MCscrolls;

internal sealed class App : ApplicationContext
{
    private readonly Settings _settings;
    private readonly MonitorManager _monitors;
    private readonly MouseHook _hook;
    private readonly CursorSwitcher _switcher;
    private readonly GhostCursorManager _ghostManager;
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
            CooldownMs = _settings.CooldownMs
        };

        _ghostManager = new GhostCursorManager(_monitors)
        {
            Enabled = _settings.GhostCursorsEnabled
        };
        _switcher.MonitorSwitched += _ghostManager.OnMonitorSwitched;

        _tray = new TrayIcon(_switcher, _monitors, _settings, _ghostManager);
        UpdateTrayForMonitorCount();

        if (!_hook.Install())
        {
            _tray.ShowBalloon("MCscrolls", "Failed to install mouse hook. The application will exit.", ToolTipIcon.Error);
            Task.Delay(3000).ContinueWith(_ => Application.Exit(), TaskScheduler.FromCurrentSynchronizationContext());
            return;
        }

        _ghostManager.Initialize();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _monitors.RefreshMonitors();
        _ghostManager.RecreateGhosts();
        _tray.UpdateMonitorOrder(_monitors);
        UpdateTrayForMonitorCount();
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _monitors.RefreshMonitors();
            _ghostManager.RecreateGhosts();
            _tray.UpdateMonitorOrder(_monitors);
            UpdateTrayForMonitorCount();
        }
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
            _hook.Dispose();
            _switcher.Dispose();
            _ghostManager.Dispose();
            _tray.Dispose();
        }
        base.Dispose(disposing);
    }
}
