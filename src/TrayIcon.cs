using System.Drawing;
using System.Reflection;

namespace MCscrolls;

internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _ghostCursorsItem;
    private readonly ToolStripMenuItem _monitorOrderItem;
    private readonly MonitorManager _monitors;
    private readonly Settings _settings;
    private MonitorPanel? _settingsPanel;

    public event Action? SettingsApplied;

    public TrayIcon(CursorSwitcher switcher, MonitorManager monitors, Settings settings, GhostCursorManager ghostManager)
    {
        _monitors = monitors;
        _settings = settings;

        _enabledItem = new ToolStripMenuItem("Enabled")
        {
            CheckOnClick = true,
            Checked = settings.Enabled
        };
        _enabledItem.CheckedChanged += (_, _) =>
        {
            switcher.Enabled = _enabledItem.Checked;
            settings.Enabled = _enabledItem.Checked;
            settings.Save();
        };

        _startupItem = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = settings.IsStartupEnabled()
        };
        _startupItem.CheckedChanged += (_, _) =>
        {
            settings.SetStartup(_startupItem.Checked);
        };

        _ghostCursorsItem = new ToolStripMenuItem("Ghost Cursors")
        {
            CheckOnClick = true,
            Checked = settings.GhostCursorsEnabled
        };
        _ghostCursorsItem.CheckedChanged += (_, _) =>
        {
            ghostManager.Enabled = _ghostCursorsItem.Checked;
            settings.GhostCursorsEnabled = _ghostCursorsItem.Checked;
            settings.Save();
        };

        _monitorOrderItem = new ToolStripMenuItem("Monitor Order")
        {
            Enabled = false
        };
        UpdateMonitorOrder(monitors);

        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => OpenSettingsPanel();

        var aboutItem = new ToolStripMenuItem("About");
        aboutItem.Click += (_, _) =>
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            MessageBox.Show(
                $"MCscrolls — Monitor Continuity Scrolls\n" +
                $"Version {version.Major}.{version.Minor}.{version.Build}\n\n" +
                $"Switch between monitors with Alt+Scroll.\n" +
                $"Each monitor remembers your cursor position.\n\n" +
                $"https://github.com/sidserd/MCscrolls",
                "About MCscrolls",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        };

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => Application.Exit();

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_enabledItem);
        contextMenu.Items.Add(_startupItem);
        contextMenu.Items.Add(_ghostCursorsItem);
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_monitorOrderItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(aboutItem);
        contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "MCscrolls — Monitor Continuity Scrolls",
            ContextMenuStrip = contextMenu,
            Visible = true
        };
    }

    public void UpdateMonitorOrder(MonitorManager monitors)
    {
        _monitorOrderItem.Text = monitors.GetOrderSummary();
    }

    public void UpdateTooltip(string text)
    {
        // NotifyIcon.Text has a 127 character limit
        _notifyIcon.Text = text.Length > 127 ? text[..127] : text;
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, text, icon);
    }

    private static Icon LoadIcon()
    {
        // Try to load from file next to executable
        string? exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (exeDir != null)
        {
            string iconPath = Path.Combine(exeDir, "icon.ico");
            if (File.Exists(iconPath))
                return new Icon(iconPath);
        }

        // Fallback: generate a simple icon programmatically
        return CreateDefaultIcon();
    }

    private static Icon CreateDefaultIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);

        // Draw four monitor rectangles
        using var brush = new SolidBrush(Color.FromArgb(70, 130, 230));
        using var pen = new Pen(Color.White, 1);

        g.FillRectangle(brush, 2, 2, 12, 10);
        g.DrawRectangle(pen, 2, 2, 12, 10);

        g.FillRectangle(brush, 18, 2, 12, 10);
        g.DrawRectangle(pen, 18, 2, 12, 10);

        g.FillRectangle(brush, 2, 18, 12, 10);
        g.DrawRectangle(pen, 2, 18, 12, 10);

        g.FillRectangle(brush, 18, 18, 12, 10);
        g.DrawRectangle(pen, 18, 18, 12, 10);

        // Draw arrow cursor in center
        using var arrowPen = new Pen(Color.White, 2);
        g.DrawLine(arrowPen, 16, 8, 16, 24);
        g.DrawLine(arrowPen, 12, 12, 16, 8);
        g.DrawLine(arrowPen, 20, 12, 16, 8);

        return Icon.FromHandle(bmp.GetHicon());
    }

    private void OpenSettingsPanel()
    {
        if (_settingsPanel != null && !_settingsPanel.IsDisposed)
        {
            _settingsPanel.BringToFront();
            _settingsPanel.Activate();
            return;
        }

        // Build panel config from current settings
        var monitorInfos = new List<PanelMonitorInfo>();
        var screenLookup = new Dictionary<string, Rectangle>();
        foreach (var screen in Screen.AllScreens)
            screenLookup[screen.DeviceName] = screen.Bounds;

        if (_settings.Sets.Count > 0)
        {
            foreach (var setConfig in _settings.Sets)
            {
                foreach (var entry in setConfig.Monitors)
                {
                    if (screenLookup.TryGetValue(entry.DeviceName, out var bounds))
                    {
                        monitorInfos.Add(new PanelMonitorInfo
                        {
                            DeviceName = entry.DeviceName,
                            Bounds = bounds,
                            SetName = setConfig.Name,
                            OrderInSet = entry.Order,
                            IsBoxedOut = entry.BoxedOut
                        });
                    }
                }
            }

            // Add any new monitors not yet in settings
            foreach (var screen in Screen.AllScreens)
            {
                if (!monitorInfos.Any(m => m.DeviceName == screen.DeviceName))
                {
                    monitorInfos.Add(new PanelMonitorInfo
                    {
                        DeviceName = screen.DeviceName,
                        Bounds = screen.Bounds,
                        SetName = "All",
                        OrderInSet = monitorInfos.Count,
                        IsBoxedOut = false
                    });
                }
            }
        }
        else
        {
            foreach (var screen in Screen.AllScreens)
            {
                monitorInfos.Add(new PanelMonitorInfo
                {
                    DeviceName = screen.DeviceName,
                    Bounds = screen.Bounds
                });
            }
        }

        _settingsPanel = new MonitorPanel(
            monitorInfos,
            _settings.CooldownMs,
            _settings.BetweenSetCooldownMs,
            _settings.Hotkeys.WithinSetModifier,
            _settings.Hotkeys.BetweenSetModifier,
            _settings.GhostStyle.Opacity,
            _settings.GhostStyle.Size,
            _settings.GhostStyle.Color);

        _settingsPanel.FormClosed += (_, _) =>
        {
            if (_settingsPanel.Applied)
            {
                // Save cooldowns
                _settings.CooldownMs = _settingsPanel.WithinSetCooldownMs;
                _settings.BetweenSetCooldownMs = _settingsPanel.BetweenSetCooldownMs;

                // Save monitor sets
                _settings.Sets.Clear();
                var setGroups = _settingsPanel.ResultMonitors
                    .GroupBy(m => m.SetName);
                foreach (var group in setGroups)
                {
                    var setConfig = new MonitorSetConfig { Name = group.Key };
                    foreach (var m in group.OrderBy(m => m.OrderInSet))
                    {
                        setConfig.Monitors.Add(new MonitorEntry
                        {
                            DeviceName = m.DeviceName,
                            Order = m.OrderInSet,
                            BoxedOut = m.IsBoxedOut
                        });
                    }
                    _settings.Sets.Add(setConfig);
                }

                // Save hotkeys
                _settings.Hotkeys.WithinSetModifier = HotkeyConfig.ToStringArray(_settingsPanel.WithinSetModifiers);
                _settings.Hotkeys.BetweenSetModifier = HotkeyConfig.ToStringArray(_settingsPanel.BetweenSetModifiers);

                // Save ghost cursor styling
                _settings.GhostStyle.Opacity = _settingsPanel.GhostOpacity;
                _settings.GhostStyle.Size = _settingsPanel.GhostSize;
                _settings.GhostStyle.Color = ColorTranslator.ToHtml(_settingsPanel.GhostColor);

                _settings.Save();
                SettingsApplied?.Invoke();
            }
            _settingsPanel = null;
        };

        _settingsPanel.Show();
    }

    public void Dispose()
    {
        _settingsPanel?.Close();
        _settingsPanel?.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
