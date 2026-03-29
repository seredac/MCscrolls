namespace MCscrolls;

/// <summary>
/// Hotkey configuration data model with validation logic and modifier key utilities.
/// </summary>
internal sealed class HotkeyConfig
{
    // Virtual key codes for modifier keys
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;    // Alt
    public const int VK_SHIFT = 0x10;
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;

    // Human-readable names for serialization
    private static readonly Dictionary<string, int> NameToVK = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = VK_CONTROL,
        ["Alt"] = VK_MENU,
        ["Shift"] = VK_SHIFT,
        ["Win"] = VK_LWIN,
    };

    private static readonly Dictionary<int, string> VKToName = new()
    {
        [VK_CONTROL] = "Ctrl",
        [VK_MENU] = "Alt",
        [VK_SHIFT] = "Shift",
        [VK_LWIN] = "Win",
    };

    public List<int> WithinSetModifiers { get; set; } = new() { VK_MENU }; // Default: Alt
    public List<int> BetweenSetModifiers { get; set; } = new() { VK_CONTROL, VK_MENU }; // Default: Ctrl+Alt

    /// <summary>
    /// Convert string array from settings (["Ctrl", "Alt"]) to VK code list.
    /// </summary>
    public static List<int> FromStringArray(List<string> names)
    {
        var result = new List<int>();
        foreach (var name in names)
        {
            if (NameToVK.TryGetValue(name, out int vk))
                result.Add(vk);
        }
        return result;
    }

    /// <summary>
    /// Convert VK code list to string array for settings serialization.
    /// </summary>
    public static List<string> ToStringArray(List<int> vkCodes)
    {
        var result = new List<string>();
        foreach (var vk in vkCodes)
        {
            if (VKToName.TryGetValue(vk, out string? name))
                result.Add(name);
        }
        return result;
    }

    /// <summary>
    /// Get display string like "Ctrl + Alt".
    /// </summary>
    public static string GetDisplayString(List<int> vkCodes)
    {
        return string.Join(" + ", ToStringArray(vkCodes));
    }

    /// <summary>
    /// Check if all specified modifier keys are currently held down.
    /// Uses GetAsyncKeyState from NativeMethods.
    /// </summary>
    public static bool AreModifiersHeld(List<int> requiredVKeys)
    {
        foreach (var vk in requiredVKeys)
        {
            if ((NativeMethods.GetAsyncKeyState(vk) & 0x8000) == 0)
                return false;
        }
        return true;
    }

    private static readonly int[] AllModifiers = { VK_CONTROL, VK_MENU, VK_SHIFT, VK_LWIN, VK_RWIN };

    // Subset for capture UI (excludes VK_RWIN since VK_LWIN covers Win key)
    internal static readonly int[] CaptureModifiers = { VK_CONTROL, VK_MENU, VK_SHIFT, VK_LWIN };

    /// <summary>
    /// Check if EXACTLY these modifiers are held (and no other standard modifiers).
    /// This distinguishes "Alt only" from "Ctrl+Alt".
    /// </summary>
    public static bool AreExactModifiersHeld(List<int> requiredVKeys)
    {
        var required = new HashSet<int>(requiredVKeys);

        foreach (var vk in AllModifiers)
        {
            bool isHeld = (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
            bool isRequired = required.Contains(vk);

            if (isHeld != isRequired)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Validate the hotkey configuration.
    /// Returns null if valid, or an error message string if invalid.
    /// </summary>
    public string? Validate()
    {
        if (WithinSetModifiers.Count == 0)
            return "Within-set hotkey must have at least one modifier key.";

        if (BetweenSetModifiers.Count == 0)
            return "Between-set hotkey must have at least one modifier key.";

        // Check they're not identical
        var within = new HashSet<int>(WithinSetModifiers);
        var between = new HashSet<int>(BetweenSetModifiers);
        if (within.SetEquals(between))
            return "Within-set and between-set hotkeys must use different modifier combinations.";

        // Warn about Win key (conflicts with system shortcuts)
        if (WithinSetModifiers.Contains(VK_LWIN) || WithinSetModifiers.Contains(VK_RWIN) ||
            BetweenSetModifiers.Contains(VK_LWIN) || BetweenSetModifiers.Contains(VK_RWIN))
            return "Warning: Windows key modifier may conflict with system shortcuts.";

        return null;
    }
}

/// <summary>
/// WinForms UserControl for capturing modifier key combinations via real-time polling.
/// Layout: [display label] [Record button]
/// </summary>
internal sealed class HotkeyCaptureControl : UserControl
{
    private readonly Label _displayLabel;
    private readonly Button _captureButton;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private bool _capturing;
    private List<int> _capturedKeys = new();

    /// <summary>
    /// Fired when a new hotkey combination is captured.
    /// </summary>
    public event Action<List<int>>? HotkeyCaptured;

    /// <summary>
    /// The currently displayed hotkey VK codes.
    /// </summary>
    public List<int> CurrentHotkey { get; private set; } = new() { HotkeyConfig.VK_MENU };

    public HotkeyCaptureControl()
    {
        _displayLabel = new Label
        {
            Text = HotkeyConfig.GetDisplayString(CurrentHotkey),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            Padding = new Padding(4, 0, 4, 0),
        };

        _captureButton = new Button
        {
            Text = "Record",
            AutoSize = true,
            Dock = DockStyle.Right,
            MinimumSize = new Size(70, 0),
        };
        _captureButton.Click += OnCaptureClick;

        _pollTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _pollTimer.Tick += OnPollTick;

        Height = 28;
        Controls.Add(_displayLabel);
        Controls.Add(_captureButton);
    }

    /// <summary>
    /// Set the displayed hotkey without triggering capture.
    /// </summary>
    public void SetHotkey(List<int> vkCodes)
    {
        CurrentHotkey = new List<int>(vkCodes);
        _displayLabel.Text = HotkeyConfig.GetDisplayString(CurrentHotkey);
    }

    private void OnCaptureClick(object? sender, EventArgs e)
    {
        if (_capturing)
        {
            StopCapture();
            return;
        }
        StartCapture();
    }

    private void StartCapture()
    {
        _capturing = true;
        _capturedKeys.Clear();
        _captureButton.Text = "Cancel";
        _displayLabel.Text = "Press modifier keys...";
        _pollTimer.Start();
    }

    private void StopCapture()
    {
        _pollTimer.Stop();
        _capturing = false;
        _captureButton.Text = "Record";
        _displayLabel.Text = HotkeyConfig.GetDisplayString(CurrentHotkey);
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        var heldNow = new List<int>();
        foreach (var vk in HotkeyConfig.CaptureModifiers)
        {
            if ((NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0)
                heldNow.Add(vk);
        }

        if (heldNow.Count > 0)
        {
            // Track the broadest set held, not just the current snapshot
            foreach (var vk in heldNow)
            {
                if (!_capturedKeys.Contains(vk))
                    _capturedKeys.Add(vk);
            }
            _displayLabel.Text = HotkeyConfig.GetDisplayString(heldNow);
        }
        else if (_capturedKeys.Count > 0)
        {
            FinalizeCapture();
        }
    }

    private void FinalizeCapture()
    {
        _pollTimer.Stop();
        _capturing = false;
        _captureButton.Text = "Record";

        CurrentHotkey = new List<int>(_capturedKeys);
        _capturedKeys.Clear();
        _displayLabel.Text = HotkeyConfig.GetDisplayString(CurrentHotkey);
        HotkeyCaptured?.Invoke(new List<int>(CurrentHotkey));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pollTimer.Stop();
            _pollTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
