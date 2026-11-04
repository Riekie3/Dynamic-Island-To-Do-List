using System.Runtime.InteropServices;

namespace TaskIsland.Windows.Native;

internal static class Shell32
{
    /// <summary>Returns QUNS_* (1–7). Reads a system state only; it touches no other process.</summary>
    [DllImport("shell32.dll")]
    public static extern int SHQueryUserNotificationState(out int state);

    public const uint ABM_NEW = 0x0, ABM_REMOVE = 0x1;
    public const int ABN_FULLSCREENAPP = 0x2;

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public User32.RECT rc;
        public IntPtr lParam;
    }

    /// <summary>
    /// Registering (ABM_NEW) without ABM_SETPOS reserves no screen space; it only subscribes the window to shell
    /// notifications such as ABN_FULLSCREENAPP.
    /// </summary>
    [DllImport("shell32.dll")]
    public static extern UIntPtr SHAppBarMessage(uint message, ref APPBARDATA data);
}
