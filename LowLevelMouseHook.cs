using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Forms = System.Windows.Forms;

namespace LazyCatCompanion;

/// <summary>Observes desktop left-clicks so an awake cat can jump to their screen position.</summary>
internal sealed class LowLevelMouseHook : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmLButtonDown = 0x0201;
    private HookProc? _callback;
    private IntPtr _hookHandle;

    public event Action<Forms.MouseEventArgs>? LeftButtonDown;

    public void Install()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        _callback = HookCallback;
        _hookHandle = SetWindowsHookEx(WhMouseLl, _callback, IntPtr.Zero, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not listen for desktop mouse clicks.");
        }
    }

    private IntPtr HookCallback(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && message == (IntPtr)WmLButtonDown)
        {
            var info = Marshal.PtrToStructure<MouseHookInfo>(data);
            LeftButtonDown?.Invoke(new Forms.MouseEventArgs(Forms.MouseButtons.Left, 1, info.Point.X, info.Point.Y, 0));
        }

        return CallNextHookEx(_hookHandle, code, message, data);
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        _callback = null;
    }

    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookInfo
    {
        public NativePoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, HookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
}

/// <summary>Tells clicks on the desktop wallpaper apart from clicks inside other windows.</summary>
internal static class DesktopProbe
{
    private const uint GaRoot = 2;

    public static bool IsDesktopAt(int x, int y)
    {
        var window = GetAncestor(WindowFromPoint(new NativePoint { X = x, Y = y }), GaRoot);
        if (window == IntPtr.Zero)
        {
            return false;
        }

        var className = new StringBuilder(64);
        _ = GetClassName(window, className, className.Capacity);
        return className.ToString() is "Progman" or "WorkerW";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
}
