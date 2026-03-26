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

    public TrayIcon(CursorSwitcher switcher, MonitorManager monitors, Settings settings, GhostCursorManager ghostManager)
    {
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

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
