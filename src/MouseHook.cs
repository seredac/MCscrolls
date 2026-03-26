using System.Runtime.InteropServices;

namespace MCscrolls;

internal sealed class MouseHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelMouseProc _hookProc; // prevent GC collection!

    public event Action<int>? ScrollWithAlt; // delta: positive = up, negative = down

    public MouseHook()
    {
        _hookProc = HookCallback;
    }

    public bool Install()
    {
        if (_hookId != IntPtr.Zero)
            return true;

        IntPtr hModule = NativeMethods.GetModuleHandle(null);
        _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _hookProc, hModule, 0);
        return _hookId != IntPtr.Zero;
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_MOUSEWHEEL)
        {
            short altState = NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU);
            bool altHeld = (altState & 0x8000) != 0;

            if (altHeld)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                int delta = (short)(hookStruct.mouseData >> 16);
                ScrollWithAlt?.Invoke(delta);
                return (IntPtr)1; // suppress the scroll event
            }
        }

        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Uninstall();
    }
}
