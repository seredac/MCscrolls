using System.Drawing;
using System.Drawing.Drawing2D;

namespace MCscrolls;

internal sealed class GhostCursorWindow : Form
{
    private readonly int _size;
    private readonly Color _fillColor;
    private readonly byte _opacity;

    // Arrow cursor polygon points (standard Windows arrow shape, designed for 28px coordinate space)
    private static readonly PointF[] ArrowOutline = new PointF[]
    {
        new(0, 0),
        new(0, 21),
        new(4, 17),
        new(8, 24),
        new(11, 22),
        new(7, 16),
        new(12, 16),
        new(0, 0)
    };

    public GhostCursorWindow(int size = 26, Color? fillColor = null, byte opacity = 255)
    {
        _size = size;
        _fillColor = fillColor ?? Color.White;
        _opacity = opacity;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(_size, _size);
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;

        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // WS_EX_TRANSPARENT makes the window click-through
            // WS_EX_LAYERED is required for TransparencyKey to work
            // WS_EX_TOOLWINDOW hides from Alt+Tab
            cp.ExStyle |= NativeMethods.WS_EX_TRANSPARENT
                        | NativeMethods.WS_EX_LAYERED
                        | NativeMethods.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (_opacity < 255)
        {
            NativeMethods.SetLayeredWindowAttributes(
                this.Handle,
                (uint)ColorTranslator.ToWin32(Color.Magenta),
                _opacity,
                (uint)(NativeMethods.LWA_COLORKEY | NativeMethods.LWA_ALPHA)
            );
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        g.Clear(BackColor);

        // Scale the arrow to fit the window
        float scale = _size / 28f;
        var matrix = new Matrix();
        matrix.Scale(scale, scale);
        matrix.Translate(1, 1);

        using var path = new GraphicsPath();
        path.AddPolygon(ArrowOutline);
        path.Transform(matrix);

        // Fully opaque colors to prevent magenta bleed-through
        using var fillBrush = new SolidBrush(_fillColor);
        g.FillPath(fillBrush, path);

        using var outlinePen = new Pen(Color.Black, 1f);
        g.DrawPath(outlinePen, path);
    }

    public void MoveTo(Point screenPos)
    {
        Location = new Point(screenPos.X, screenPos.Y);
    }
}

internal struct GhostStyle
{
    public byte Opacity { get; set; }
    public Color FillColor { get; set; }
    public int Size { get; set; }

    public static GhostStyle Default => new()
    {
        Opacity = 255,
        FillColor = Color.White,
        Size = 26
    };
}

internal sealed class GhostCursorManager : IDisposable
{
    private readonly MonitorManager _monitors;
    private readonly Dictionary<IntPtr, GhostCursorWindow> _ghosts = new();
    private readonly Dictionary<string, Color> _setColors = new();
    private IntPtr _activeMonitor;
    private bool _enabled = true;
    private GhostStyle _style = GhostStyle.Default;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (_enabled)
                ShowAllGhosts();
            else
                HideAllGhosts();
        }
    }

    public GhostStyle Style
    {
        get => _style;
        set
        {
            _style = value;
            if (_ghosts.Count > 0)
                RecreateGhosts();
        }
    }

    public void SetSetColor(string setName, Color color) => _setColors[setName] = color;
    public void ClearSetColors() => _setColors.Clear();

    public GhostCursorManager(MonitorManager monitors)
    {
        _monitors = monitors;
    }

    public void Initialize()
    {
        NativeMethods.GetCursorPos(out var pt);
        _activeMonitor = _monitors.GetMonitorAt(new Point(pt.X, pt.Y));
        RecreateGhosts();
    }

    public void OnMonitorSwitched(MonitorSwitchedEventArgs e)
    {
        _activeMonitor = e.ToMonitor;

        // Hide ghost on the monitor we just moved to
        if (_ghosts.TryGetValue(e.ToMonitor, out var targetGhost))
            targetGhost.Visible = false;

        // Show/update ghost on the monitor we just left
        if (_ghosts.TryGetValue(e.FromMonitor, out var sourceGhost))
        {
            sourceGhost.MoveTo(e.PositionLeft);
            if (_enabled)
                sourceGhost.Visible = true;
        }
        else if (_enabled)
        {
            // Create ghost for the departed monitor if it doesn't exist
            var ghost = CreateGhostWindow();
            ghost.MoveTo(e.PositionLeft);
            _ghosts[e.FromMonitor] = ghost;
            ghost.Show();
        }
    }

    public void RecreateGhosts()
    {
        DestroyAllGhosts();

        NativeMethods.GetCursorPos(out var pt);
        _activeMonitor = _monitors.GetMonitorAt(new Point(pt.X, pt.Y));

        foreach (var handle in _monitors.GetAllMonitorHandles())
        {
            if (handle == _activeMonitor)
                continue;

            var pos = _monitors.GetStoredPosition(handle);
            var ghost = CreateGhostWindow();
            ghost.MoveTo(pos);
            _ghosts[handle] = ghost;

            if (_enabled)
                ghost.Show();
        }
    }

    private GhostCursorWindow CreateGhostWindow()
    {
        return new GhostCursorWindow(_style.Size, _style.FillColor, _style.Opacity);
    }

    private void ShowAllGhosts()
    {
        foreach (var (handle, ghost) in _ghosts)
        {
            if (handle != _activeMonitor)
                ghost.Visible = true;
        }
    }

    private void HideAllGhosts()
    {
        foreach (var ghost in _ghosts.Values)
            ghost.Visible = false;
    }

    private void DestroyAllGhosts()
    {
        foreach (var ghost in _ghosts.Values)
        {
            ghost.Visible = false;
            ghost.Close();
            ghost.Dispose();
        }
        _ghosts.Clear();
    }

    public void Dispose()
    {
        DestroyAllGhosts();
    }
}
