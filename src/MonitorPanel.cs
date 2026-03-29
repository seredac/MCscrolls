using System.Drawing;
using System.Drawing.Drawing2D;

namespace MCscrolls;

internal sealed class PanelMonitorInfo
{
    public string DeviceName { get; set; } = "";
    public Rectangle Bounds { get; set; }
    public string SetName { get; set; } = "All";
    public int OrderInSet { get; set; }
    public bool IsBoxedOut { get; set; }
}

internal sealed class MonitorPanel : Form
{
    private readonly List<PanelMonitorInfo> _monitors;
    private readonly List<string> _sets = new() { "All" };

    // Canvas state
    private readonly Panel _canvas;
    private List<RectangleF> _drawnRects = new();
    private int _dragIndex = -1;
    private Point _dragOffset;
    private bool _isDragging;
    private int _dropTargetIndex = -1;

    // Set management
    private readonly ListBox _setListBox;
    private readonly ListBox _monitorsInSetListBox;

    // Cooldown sliders
    private readonly TrackBar _withinSetCooldown;
    private readonly TrackBar _betweenSetCooldown;
    private readonly Label _withinLabel;
    private readonly Label _betweenLabel;

    // Context menu for monitor right-click
    private readonly ContextMenuStrip _monitorContextMenu;
    private int _contextMonitorIndex = -1;

    // Hotkey capture controls
    private readonly HotkeyCaptureControl _withinSetHotkey;
    private readonly HotkeyCaptureControl _betweenSetHotkey;

    // Ghost styling controls
    private readonly TrackBar _ghostOpacity;
    private readonly TrackBar _ghostSize;
    private readonly Panel _ghostColorSwatch;
    private Color _ghostColor = Color.White;

    // Public results
    public List<PanelMonitorInfo> ResultMonitors => _monitors;
    public List<string> ResultSets => _sets;
    public int WithinSetCooldownMs => _withinSetCooldown.Value;
    public int BetweenSetCooldownMs => _betweenSetCooldown.Value;
    public List<int> WithinSetModifiers => _withinSetHotkey.CurrentHotkey;
    public List<int> BetweenSetModifiers => _betweenSetHotkey.CurrentHotkey;
    public int GhostOpacity => _ghostOpacity.Value;
    public int GhostSize => _ghostSize.Value;
    public Color GhostColor => _ghostColor;
    public bool Applied { get; private set; }

    // Color palette for sets
    private static readonly Color[] SetColors = new[]
    {
        Color.FromArgb(70, 130, 230),   // Blue
        Color.FromArgb(230, 100, 70),   // Red-orange
        Color.FromArgb(70, 200, 120),   // Green
        Color.FromArgb(200, 170, 50),   // Gold
        Color.FromArgb(170, 70, 200),   // Purple
        Color.FromArgb(70, 200, 200),   // Teal
        Color.FromArgb(230, 130, 70),   // Orange
        Color.FromArgb(200, 70, 150),   // Pink
    };

    public MonitorPanel(List<PanelMonitorInfo>? existingConfig = null, int withinCooldown = 150, int betweenCooldown = 300,
        List<string>? withinModifiers = null, List<string>? betweenModifiers = null,
        int ghostOpacity = 100, int ghostSize = 26, string ghostColor = "#FFFFFF")
    {
        _monitors = new List<PanelMonitorInfo>();
        if (existingConfig != null && existingConfig.Count > 0)
        {
            foreach (var m in existingConfig)
            {
                _monitors.Add(new PanelMonitorInfo
                {
                    DeviceName = m.DeviceName,
                    Bounds = m.Bounds,
                    SetName = m.SetName,
                    OrderInSet = m.OrderInSet,
                    IsBoxedOut = m.IsBoxedOut
                });
            }
        }
        else
        {
            var screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                _monitors.Add(new PanelMonitorInfo
                {
                    DeviceName = screens[i].DeviceName,
                    Bounds = screens[i].Bounds,
                    SetName = "All",
                    OrderInSet = i,
                    IsBoxedOut = false
                });
            }
        }

        foreach (var m in _monitors)
        {
            if (!_sets.Contains(m.SetName))
                _sets.Add(m.SetName);
        }

        Text = "MCscrolls Settings";
        Size = new Size(800, 750);
        MinimumSize = new Size(800, 700);
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        FormBorderStyle = FormBorderStyle.Sizable;

        _canvas = new Panel
        {
            Dock = DockStyle.Top,
            Height = 280,
            BackColor = Color.FromArgb(30, 30, 30)
        };
        _canvas.Paint += Canvas_Paint;
        _canvas.MouseDown += Canvas_MouseDown;
        _canvas.MouseMove += Canvas_MouseMove;
        _canvas.MouseUp += Canvas_MouseUp;
        _canvas.Resize += (_, _) => _canvas.Invalidate();

        _monitorContextMenu = new ContextMenuStrip();

        var bottomPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        // --- Row 1: Sets, Monitors in Set, Ghost Cursors ---
        var setGroupBox = new GroupBox
        {
            Text = "Sets",
            Location = new Point(10, 5),
            Size = new Size(180, 170)
        };

        _setListBox = new ListBox
        {
            Location = new Point(10, 22),
            Size = new Size(90, 115)
        };
        RefreshSetList();

        var addSetBtn = new Button { Text = "Add", Location = new Point(100, 22), Size = new Size(70, 28) };
        addSetBtn.Click += AddSet_Click;
        var renameSetBtn = new Button { Text = "Rename", Location = new Point(100, 56), Size = new Size(70, 28) };
        renameSetBtn.Click += RenameSet_Click;
        var deleteSetBtn = new Button { Text = "Delete", Location = new Point(100, 90), Size = new Size(70, 28) };
        deleteSetBtn.Click += DeleteSet_Click;

        setGroupBox.Controls.AddRange(new Control[] { _setListBox, addSetBtn, renameSetBtn, deleteSetBtn });

        var monitorsGroupBox = new GroupBox
        {
            Text = "Monitors in Set",
            Location = new Point(200, 5),
            Size = new Size(250, 170)
        };

        _monitorsInSetListBox = new ListBox
        {
            Location = new Point(10, 22),
            Size = new Size(148, 115)
        };

        var addMonBtn = new Button { Text = "Add \u25B6", Location = new Point(166, 22), Size = new Size(72, 28) };
        addMonBtn.Click += AddMonitorToSet_Click;
        var removeMonBtn = new Button { Text = "\u25C0 Remove", Location = new Point(166, 56), Size = new Size(72, 28) };
        removeMonBtn.Click += RemoveMonitorFromSet_Click;
        var boxOutBtn = new Button { Text = "Box Out", Location = new Point(166, 90), Size = new Size(72, 28) };
        boxOutBtn.Click += ToggleBoxOut_Click;

        monitorsGroupBox.Controls.AddRange(new Control[] { _monitorsInSetListBox, addMonBtn, removeMonBtn, boxOutBtn });

        // Wire set selection after both list boxes are created
        _setListBox.SelectedIndexChanged += (_, _) =>
        {
            RefreshMonitorsInSetList();
            _canvas.Invalidate();
        };
        if (_setListBox.Items.Count > 0)
        {
            _setListBox.SelectedIndex = 0;
        }

        // --- Row 2: Cooldowns, Hotkeys ---
        var cooldownGroupBox = new GroupBox
        {
            Text = "Cooldowns",
            Location = new Point(10, 183),
            Size = new Size(300, 155)
        };

        var withinLabelTitle = new Label
        {
            Text = "Within-set cooldown:",
            Location = new Point(10, 25),
            AutoSize = true
        };

        _withinSetCooldown = new TrackBar
        {
            Location = new Point(10, 45),
            Size = new Size(200, 45),
            Minimum = 50,
            Maximum = 500,
            Value = Math.Clamp(withinCooldown, 50, 500),
            TickFrequency = 50,
            SmallChange = 10,
            LargeChange = 50
        };

        _withinLabel = new Label
        {
            Text = $"{_withinSetCooldown.Value} ms",
            Location = new Point(215, 50),
            AutoSize = true
        };
        _withinSetCooldown.ValueChanged += (_, _) => _withinLabel.Text = $"{_withinSetCooldown.Value} ms";

        var betweenLabelTitle = new Label
        {
            Text = "Between-set cooldown:",
            Location = new Point(10, 95),
            AutoSize = true
        };

        _betweenSetCooldown = new TrackBar
        {
            Location = new Point(10, 115),
            Size = new Size(200, 45),
            Minimum = 50,
            Maximum = 500,
            Value = Math.Clamp(betweenCooldown, 50, 500),
            TickFrequency = 50,
            SmallChange = 10,
            LargeChange = 50
        };

        _betweenLabel = new Label
        {
            Text = $"{_betweenSetCooldown.Value} ms",
            Location = new Point(215, 120),
            AutoSize = true
        };
        _betweenSetCooldown.ValueChanged += (_, _) => _betweenLabel.Text = $"{_betweenSetCooldown.Value} ms";

        cooldownGroupBox.Controls.AddRange(new Control[]
        {
            withinLabelTitle, _withinSetCooldown, _withinLabel,
            betweenLabelTitle, _betweenSetCooldown, _betweenLabel
        });

        var hotkeyGroupBox = new GroupBox
        {
            Text = "Hotkeys (Advanced)",
            Location = new Point(320, 183),
            Size = new Size(440, 155)
        };

        var withinHotkeyLabel = new Label
        {
            Text = "Cycle monitors:",
            Location = new Point(10, 30),
            AutoSize = true
        };

        _withinSetHotkey = new HotkeyCaptureControl
        {
            Location = new Point(120, 26),
            Size = new Size(170, 28)
        };
        _withinSetHotkey.SetHotkey(HotkeyConfig.FromStringArray(
            withinModifiers ?? new List<string> { "Alt" }));

        var withinHint = new Label
        {
            Text = "+ Scroll",
            Location = new Point(295, 30),
            AutoSize = true,
            ForeColor = SystemColors.GrayText
        };

        var betweenHotkeyLabel = new Label
        {
            Text = "Switch sets:",
            Location = new Point(10, 68),
            AutoSize = true
        };

        _betweenSetHotkey = new HotkeyCaptureControl
        {
            Location = new Point(120, 64),
            Size = new Size(170, 28)
        };
        _betweenSetHotkey.SetHotkey(HotkeyConfig.FromStringArray(
            betweenModifiers ?? new List<string> { "Ctrl", "Shift" }));

        var betweenHint = new Label
        {
            Text = "+ PageDn / PageUp",
            Location = new Point(295, 68),
            AutoSize = true,
            ForeColor = SystemColors.GrayText
        };

        hotkeyGroupBox.Controls.AddRange(new Control[]
        {
            withinHotkeyLabel, _withinSetHotkey, withinHint,
            betweenHotkeyLabel, _betweenSetHotkey, betweenHint
        });

        // Ghost Cursor Styling
        try { _ghostColor = ColorTranslator.FromHtml(ghostColor); } catch { _ghostColor = Color.White; }

        var ghostGroupBox = new GroupBox
        {
            Text = "Ghost Cursors",
            Location = new Point(460, 5),
            Size = new Size(300, 170)
        };

        var opacityLabel = new Label { Text = "Opacity:", Location = new Point(10, 25), AutoSize = true };
        _ghostOpacity = new TrackBar
        {
            Location = new Point(10, 45),
            Size = new Size(180, 45),
            Minimum = 10,
            Maximum = 100,
            Value = Math.Clamp(ghostOpacity, 10, 100),
            TickFrequency = 10,
            SmallChange = 5,
            LargeChange = 10
        };
        var opacityValueLabel = new Label
        {
            Text = $"{_ghostOpacity.Value}%",
            Location = new Point(195, 50),
            AutoSize = true
        };
        _ghostOpacity.ValueChanged += (_, _) => opacityValueLabel.Text = $"{_ghostOpacity.Value}%";

        var sizeLabel = new Label { Text = "Size:", Location = new Point(10, 95), AutoSize = true };
        _ghostSize = new TrackBar
        {
            Location = new Point(10, 115),
            Size = new Size(180, 45),
            Minimum = 12,
            Maximum = 48,
            Value = Math.Clamp(ghostSize, 12, 48),
            TickFrequency = 6,
            SmallChange = 2,
            LargeChange = 6
        };
        var sizeValueLabel = new Label
        {
            Text = $"{_ghostSize.Value}px",
            Location = new Point(195, 120),
            AutoSize = true
        };
        _ghostSize.ValueChanged += (_, _) => sizeValueLabel.Text = $"{_ghostSize.Value}px";

        var colorLabel = new Label { Text = "Color:", Location = new Point(240, 25), AutoSize = true };
        _ghostColorSwatch = new Panel
        {
            Location = new Point(240, 45),
            Size = new Size(28, 28),
            BackColor = _ghostColor,
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Hand
        };
        _ghostColorSwatch.Click += (_, _) =>
        {
            using var dlg = new ColorDialog { Color = _ghostColor, FullOpen = true };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _ghostColor = dlg.Color;
                _ghostColorSwatch.BackColor = _ghostColor;
            }
        };

        ghostGroupBox.Controls.AddRange(new Control[]
        {
            opacityLabel, _ghostOpacity, opacityValueLabel,
            sizeLabel, _ghostSize, sizeValueLabel,
            colorLabel, _ghostColorSwatch
        });

        var applyBtn = new Button
        {
            Text = "Apply",
            Size = new Size(90, 32),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        applyBtn.Click += (_, _) =>
        {
            Applied = true;
            Close();
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            Size = new Size(90, 32),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        cancelBtn.Click += (_, _) =>
        {
            Applied = false;
            Close();
        };

        bottomPanel.Layout += (_, _) =>
        {
            applyBtn.Location = new Point(bottomPanel.ClientSize.Width - 200, bottomPanel.ClientSize.Height - 42);
            cancelBtn.Location = new Point(bottomPanel.ClientSize.Width - 100, bottomPanel.ClientSize.Height - 42);
        };

        bottomPanel.Controls.AddRange(new Control[]
        {
            setGroupBox, monitorsGroupBox, cooldownGroupBox, ghostGroupBox, hotkeyGroupBox, applyBtn, cancelBtn
        });

        Controls.Add(bottomPanel);
        Controls.Add(_canvas);
    }

    private Color GetSetColor(string setName)
    {
        int idx = _sets.IndexOf(setName);
        if (idx < 0) idx = 0;
        return SetColors[idx % SetColors.Length];
    }

    private void Canvas_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (_monitors.Count == 0) return;

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var m in _monitors)
        {
            if (m.Bounds.Left < minX) minX = m.Bounds.Left;
            if (m.Bounds.Top < minY) minY = m.Bounds.Top;
            if (m.Bounds.Right > maxX) maxX = m.Bounds.Right;
            if (m.Bounds.Bottom > maxY) maxY = m.Bounds.Bottom;
        }

        int totalW = maxX - minX;
        int totalH = maxY - minY;
        if (totalW <= 0 || totalH <= 0) return;

        float padX = 40, padY = 30;
        float availW = _canvas.ClientSize.Width - padX * 2;
        float availH = _canvas.ClientSize.Height - padY * 2;
        if (availW <= 0 || availH <= 0) return;

        float scale = Math.Min(availW / totalW, availH / totalH);

        float scaledW = totalW * scale;
        float scaledH = totalH * scale;
        float offsetX = padX + (availW - scaledW) / 2;
        float offsetY = padY + (availH - scaledH) / 2;

        _drawnRects.Clear();

        for (int i = 0; i < _monitors.Count; i++)
        {
            var m = _monitors[i];
            float rx = offsetX + (m.Bounds.Left - minX) * scale;
            float ry = offsetY + (m.Bounds.Top - minY) * scale;
            float rw = m.Bounds.Width * scale;
            float rh = m.Bounds.Height * scale;

            var rect = new RectangleF(rx, ry, rw, rh);
            _drawnRects.Add(rect);

            // Skip drawing if this is the actively dragged monitor (we draw it last)
            if (_isDragging && i == _dragIndex) continue;

            DrawMonitorRect(g, i, rect, m, false);
        }

        // Draw dragged monitor on top
        if (_isDragging && _dragIndex >= 0 && _dragIndex < _drawnRects.Count)
        {
            var m = _monitors[_dragIndex];
            var rect = _drawnRects[_dragIndex];
            DrawMonitorRect(g, _dragIndex, rect, m, true);
        }

        // Draw drop target indicator
        if (_isDragging && _dropTargetIndex >= 0 && _dropTargetIndex < _drawnRects.Count
            && _dropTargetIndex != _dragIndex)
        {
            var targetRect = _drawnRects[_dropTargetIndex];
            using var highlightPen = new Pen(Color.Yellow, 3) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(highlightPen, targetRect.X, targetRect.Y, targetRect.Width, targetRect.Height);
        }
    }

    private void DrawMonitorRect(Graphics g, int index, RectangleF rect, PanelMonitorInfo m, bool dragging)
    {
        Color setColor = GetSetColor(m.SetName);
        byte alpha = dragging ? (byte)160 : (byte)255;

        using var fillBrush = new SolidBrush(Color.FromArgb(alpha, 50, 55, 65));
        g.FillRectangle(fillBrush, rect);

        if (m.IsBoxedOut)
        {
            using var borderPen = new Pen(Color.FromArgb(alpha, 200, 50, 50), 2) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);
        }
        else
        {
            using var borderPen = new Pen(Color.FromArgb(alpha, setColor.R, setColor.G, setColor.B), 2);
            g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);
        }

        // Clip all text to the monitor rectangle with padding
        var clipRect = new RectangleF(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);
        var oldClip = g.Clip;
        g.SetClip(clipRect);

        // Scale fonts proportionally to the rectangle — never exceed available space
        float minDim = Math.Min(rect.Width, rect.Height);
        float numFontSize = Math.Clamp(minDim * 0.28f, 10, 36);
        float smallFontSize = Math.Clamp(minDim * 0.12f, 8, 14);

        using var numFont = new Font("Segoe UI", numFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var smallFont = new Font("Segoe UI", smallFontSize, FontStyle.Regular, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.FromArgb(alpha, 240, 240, 240));
        using var dimBrush = new SolidBrush(Color.FromArgb(alpha, 180, 180, 180));

        // Measure all three lines to center them as a block
        string numText = $"{index + 1}";
        string devName = m.DeviceName;
        if (devName.StartsWith(@"\\.\")) devName = devName[4..];
        string resText = $"{m.Bounds.Width}x{m.Bounds.Height}";

        var numSize = g.MeasureString(numText, numFont);
        var devSize = g.MeasureString(devName, smallFont);
        var resSize = g.MeasureString(resText, smallFont);

        float lineGap = 2;
        float totalTextHeight = numSize.Height + devSize.Height + resSize.Height + lineGap * 2;
        float startY = rect.Y + (rect.Height - totalTextHeight) / 2;

        // Draw monitor number (centered)
        float numX = rect.X + (rect.Width - numSize.Width) / 2;
        g.DrawString(numText, numFont, textBrush, numX, startY);

        // Draw device name (centered)
        float devX = rect.X + (rect.Width - devSize.Width) / 2;
        float devY = startY + numSize.Height + lineGap;
        g.DrawString(devName, smallFont, dimBrush, devX, devY);

        // Draw resolution (centered)
        float resX = rect.X + (rect.Width - resSize.Width) / 2;
        float resY = devY + devSize.Height + lineGap;
        g.DrawString(resText, smallFont, dimBrush, resX, resY);

        // Restore clip before drawing overlays
        g.Clip = oldClip;

        // Box-out lock icon (top-right corner, inside bounds)
        if (m.IsBoxedOut)
        {
            g.SetClip(clipRect);
            string lockText = "\U0001F512";
            using var lockFont = new Font("Segoe UI Emoji", Math.Clamp(minDim * 0.1f, 8, 14), FontStyle.Regular, GraphicsUnit.Pixel);
            var lockSize = g.MeasureString(lockText, lockFont);
            g.DrawString(lockText, lockFont, textBrush, rect.Right - lockSize.Width - 6, rect.Y + 5);
            g.Clip = oldClip;
        }

        // Set name badge (top-left corner, inside bounds)
        if (m.SetName != "All")
        {
            g.SetClip(clipRect);
            using var badgeFont = new Font("Segoe UI", Math.Clamp(smallFontSize * 0.9f, 7, 11), FontStyle.Regular, GraphicsUnit.Pixel);
            var badgeSize = g.MeasureString(m.SetName, badgeFont);
            float badgeW = Math.Min(badgeSize.Width + 4, rect.Width - 10);
            float badgeX = rect.X + 5;
            float badgeY = rect.Y + 5;
            using var badgeBg = new SolidBrush(Color.FromArgb(alpha, setColor.R, setColor.G, setColor.B));
            g.FillRectangle(badgeBg, badgeX, badgeY, badgeW, badgeSize.Height + 2);
            using var badgeTextBrush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255));
            g.DrawString(m.SetName, badgeFont, badgeTextBrush, badgeX + 2, badgeY + 1);
            g.Clip = oldClip;
        }
    }

    private int HitTestMonitor(Point pt)
    {
        // Reverse order so topmost drawn rectangles are hit first
        for (int i = _drawnRects.Count - 1; i >= 0; i--)
        {
            if (_drawnRects[i].Contains(pt))
                return i;
        }
        return -1;
    }

    private void Canvas_MouseDown(object? sender, MouseEventArgs e)
    {
        int hit = HitTestMonitor(e.Location);
        if (hit < 0) return;

        if (e.Button == MouseButtons.Right)
        {
            _contextMonitorIndex = hit;
            ShowMonitorContextMenu(e.Location);
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            _dragIndex = hit;
            _dragOffset = new Point(
                (int)(e.Location.X - _drawnRects[hit].X),
                (int)(e.Location.Y - _drawnRects[hit].Y));
            _isDragging = false; // only start after mouse moves
        }
    }

    private void Canvas_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_dragIndex < 0) return;

        if (!_isDragging)
        {
            // Start dragging after a small threshold
            if (Math.Abs(e.X - (_drawnRects[_dragIndex].X + _dragOffset.X)) > 5 ||
                Math.Abs(e.Y - (_drawnRects[_dragIndex].Y + _dragOffset.Y)) > 5)
            {
                _isDragging = true;
            }
            else return;
        }

        // Update drop target
        _dropTargetIndex = -1;
        for (int i = 0; i < _drawnRects.Count; i++)
        {
            if (i == _dragIndex) continue;
            if (_drawnRects[i].Contains(e.Location))
            {
                _dropTargetIndex = i;
                break;
            }
        }

        _canvas.Invalidate();
    }

    private void Canvas_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_isDragging && _dragIndex >= 0 && _dropTargetIndex >= 0 && _dropTargetIndex != _dragIndex)
        {
            int tempOrder = _monitors[_dragIndex].OrderInSet;
            _monitors[_dragIndex].OrderInSet = _monitors[_dropTargetIndex].OrderInSet;
            _monitors[_dropTargetIndex].OrderInSet = tempOrder;

            var temp = _monitors[_dragIndex];
            _monitors[_dragIndex] = _monitors[_dropTargetIndex];
            _monitors[_dropTargetIndex] = temp;
        }

        _dragIndex = -1;
        _isDragging = false;
        _dropTargetIndex = -1;
        _canvas.Invalidate();
    }

    private void ShowMonitorContextMenu(Point location)
    {
        _monitorContextMenu.Items.Clear();

        var m = _monitors[_contextMonitorIndex];

        var setSubmenu = new ToolStripMenuItem("Assign to Set");
        foreach (var setName in _sets)
        {
            var item = new ToolStripMenuItem(setName)
            {
                Checked = m.SetName == setName
            };
            string capturedName = setName;
            item.Click += (_, _) =>
            {
                m.SetName = capturedName;
                _canvas.Invalidate();
            };
            setSubmenu.DropDownItems.Add(item);
        }
        setSubmenu.DropDownItems.Add(new ToolStripSeparator());
        var newSetItem = new ToolStripMenuItem("New Set...");
        newSetItem.Click += (_, _) =>
        {
            string? name = PromptForText("New Set", "Enter set name:");
            if (!string.IsNullOrWhiteSpace(name) && !_sets.Contains(name))
            {
                _sets.Add(name);
                RefreshSetList();
                m.SetName = name;
                _canvas.Invalidate();
            }
        };
        setSubmenu.DropDownItems.Add(newSetItem);
        _monitorContextMenu.Items.Add(setSubmenu);

        var boxOutItem = new ToolStripMenuItem("Toggle Box-Out")
        {
            Checked = m.IsBoxedOut
        };
        boxOutItem.Click += (_, _) =>
        {
            m.IsBoxedOut = !m.IsBoxedOut;
            _canvas.Invalidate();
        };
        _monitorContextMenu.Items.Add(boxOutItem);

        var identifyItem = new ToolStripMenuItem("Identify");
        int monitorIndex = _contextMonitorIndex;
        identifyItem.Click += (_, _) => IdentifyMonitor(monitorIndex);
        _monitorContextMenu.Items.Add(identifyItem);

        _monitorContextMenu.Show(_canvas, location);
    }

    private void IdentifyMonitor(int index)
    {
        if (index < 0 || index >= _monitors.Count) return;

        var m = _monitors[index];

        Screen? targetScreen = null;
        foreach (var scr in Screen.AllScreens)
        {
            if (scr.Bounds == m.Bounds || scr.DeviceName == m.DeviceName)
            {
                targetScreen = scr;
                break;
            }
        }

        Rectangle displayBounds = targetScreen?.Bounds ?? m.Bounds;

        var idForm = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Bounds = displayBounds,
            BackColor = Color.FromArgb(20, 20, 20),
            TopMost = true,
            ShowInTaskbar = false,
            Opacity = 0.85
        };

        float fontSize = Math.Min(displayBounds.Width, displayBounds.Height) * 0.4f;
        var labelFont = new Font("Segoe UI", Math.Max(fontSize, 48), FontStyle.Bold, GraphicsUnit.Pixel);

        var label = new Label
        {
            Text = $"{index + 1}",
            Font = labelFont,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        idForm.Controls.Add(label);

        var timer = new System.Windows.Forms.Timer { Interval = 2000 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            idForm.Close();
            labelFont.Dispose();
            idForm.Dispose();
        };

        idForm.Show();
        timer.Start();
    }

    private void RefreshSetList()
    {
        _setListBox.Items.Clear();
        foreach (var s in _sets)
            _setListBox.Items.Add(s);
    }

    private void RefreshMonitorsInSetList()
    {
        _monitorsInSetListBox.Items.Clear();
        if (_setListBox.SelectedItem is not string setName) return;

        foreach (var m in _monitors.Where(m => m.SetName == setName).OrderBy(m => m.OrderInSet))
        {
            string devName = m.DeviceName;
            if (devName.StartsWith(@"\\.\")) devName = devName[4..];
            string boxed = m.IsBoxedOut ? " [Boxed]" : "";
            _monitorsInSetListBox.Items.Add($"{devName} ({m.Bounds.Width}x{m.Bounds.Height}){boxed}");
        }
    }

    private void AddMonitorToSet_Click(object? sender, EventArgs e)
    {
        if (_setListBox.SelectedItem is not string setName) return;

        var unassigned = _monitors.Where(m => m.SetName != setName).ToList();
        if (unassigned.Count == 0)
        {
            MessageBox.Show("All monitors are already in this set.", "Add Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var picker = new Form
        {
            Text = $"Add Monitor to '{setName}'",
            Size = new Size(350, 250),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false
        };

        var listBox = new ListBox { Location = new Point(12, 12), Size = new Size(310, 150) };
        foreach (var m in unassigned)
        {
            string devName = m.DeviceName;
            if (devName.StartsWith(@"\\.\")) devName = devName[4..];
            listBox.Items.Add($"{devName} ({m.Bounds.Width}x{m.Bounds.Height}) — in '{m.SetName}'");
        }

        var ok = new Button { Text = "Add", DialogResult = DialogResult.OK, Location = new Point(166, 172), Size = new Size(75, 28) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(247, 172), Size = new Size(75, 28) };
        picker.AcceptButton = ok;
        picker.CancelButton = cancel;
        picker.Controls.AddRange(new Control[] { listBox, ok, cancel });

        if (picker.ShowDialog() == DialogResult.OK && listBox.SelectedIndex >= 0)
        {
            var selected = unassigned[listBox.SelectedIndex];
            selected.SetName = setName;
            selected.OrderInSet = _monitors.Count(m => m.SetName == setName);
            RefreshMonitorsInSetList();
            _canvas.Invalidate();
        }
    }

    private void RemoveMonitorFromSet_Click(object? sender, EventArgs e)
    {
        if (_setListBox.SelectedItem is not string setName) return;
        if (_monitorsInSetListBox.SelectedIndex < 0) return;

        var monitorsInSet = _monitors.Where(m => m.SetName == setName).OrderBy(m => m.OrderInSet).ToList();
        if (_monitorsInSetListBox.SelectedIndex >= monitorsInSet.Count) return;

        var selected = monitorsInSet[_monitorsInSetListBox.SelectedIndex];
        selected.SetName = "All";
        selected.OrderInSet = _monitors.Count(m => m.SetName == "All");

        RefreshMonitorsInSetList();
        _canvas.Invalidate();
    }

    private void ToggleBoxOut_Click(object? sender, EventArgs e)
    {
        if (_setListBox.SelectedItem is not string setName) return;
        if (_monitorsInSetListBox.SelectedIndex < 0) return;

        var monitorsInSet = _monitors.Where(m => m.SetName == setName).OrderBy(m => m.OrderInSet).ToList();
        if (_monitorsInSetListBox.SelectedIndex >= monitorsInSet.Count) return;

        var selected = monitorsInSet[_monitorsInSetListBox.SelectedIndex];
        selected.IsBoxedOut = !selected.IsBoxedOut;

        RefreshMonitorsInSetList();
        _canvas.Invalidate();
    }

    private void AddSet_Click(object? sender, EventArgs e)
    {
        string? name = PromptForText("Add Set", "Enter set name:");
        if (!string.IsNullOrWhiteSpace(name) && !_sets.Contains(name))
        {
            _sets.Add(name);
            RefreshSetList();
        }
    }

    private void RenameSet_Click(object? sender, EventArgs e)
    {
        if (_setListBox.SelectedItem is not string oldName) return;
        if (oldName == "All")
        {
            MessageBox.Show("The 'All' set cannot be renamed.", "Rename Set", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? newName = PromptForText("Rename Set", $"Rename '{oldName}' to:", oldName);
        if (!string.IsNullOrWhiteSpace(newName) && newName != oldName && !_sets.Contains(newName))
        {
            int idx = _sets.IndexOf(oldName);
            _sets[idx] = newName;

            foreach (var m in _monitors)
            {
                if (m.SetName == oldName)
                    m.SetName = newName;
            }

            RefreshSetList();
            _canvas.Invalidate();
        }
    }

    private void DeleteSet_Click(object? sender, EventArgs e)
    {
        if (_setListBox.SelectedItem is not string setName) return;
        if (setName == "All")
        {
            MessageBox.Show("The 'All' set cannot be deleted.", "Delete Set", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Delete set '{setName}'? Monitors in this set will be moved to 'All'.",
            "Delete Set", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            foreach (var m in _monitors)
            {
                if (m.SetName == setName)
                    m.SetName = "All";
            }

            _sets.Remove(setName);
            RefreshSetList();
            _canvas.Invalidate();
        }
    }

    private static string? PromptForText(string title, string prompt, string defaultValue = "")
    {
        using var dialog = new Form
        {
            Text = title,
            Size = new Size(350, 150),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false
        };

        var lbl = new Label { Text = prompt, Location = new Point(12, 12), AutoSize = true };
        var txt = new TextBox { Location = new Point(12, 35), Width = 310, Text = defaultValue };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(166, 70), Size = new Size(75, 28) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(247, 70), Size = new Size(75, 28) };

        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        dialog.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });

        return dialog.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : null;
    }
}
