using System.Runtime.InteropServices;

namespace TrayManager;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Identifier { public uint cbSize; public nint hWnd; public uint uID; public Guid guidItem; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct Data
    {
        public uint cbSize; public nint hWnd; public uint uID, uFlags, uCallbackMessage; public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags; public Guid guidItem; public nint hBalloonIcon;
    }
    internal delegate bool EnumProc(nint hwnd, nint param);
    internal delegate nint SubclassProc(nint hwnd, uint msg, nuint wparam, nint lparam, nuint id, nuint data);
    [DllImport("shell32.dll")] internal static extern int Shell_NotifyIconGetRect(ref Identifier id, out Rect rect);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern bool Shell_NotifyIconW(uint message, ref Data data);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc proc, nint param);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint FindWindowEx(nint parent, nint after, string? cls, string? title);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")] internal static extern nint LoadIcon(nint instance, nint name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint LoadImage(nint instance, string name, uint type, int cx, int cy, uint flags);
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [StructLayout(LayoutKind.Sequential)] internal struct GamepadState
    {
        public uint Packet; public ushort Buttons; public byte LeftTrigger, RightTrigger;
        public short LeftX, LeftY, RightX, RightY;
    }
    [DllImport("xinput1_4.dll")] internal static extern uint XInputGetState(uint index, out GamepadState state);
    [DllImport("kernel32.dll")] private static extern nint OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(nint process, uint flags, System.Text.StringBuilder path, ref uint size);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("comctl32.dll")] internal static extern bool SetWindowSubclass(nint hwnd, SubclassProc proc, nuint id, nuint data);
    [DllImport("comctl32.dll")] internal static extern nint DefSubclassProc(nint hwnd, uint msg, nuint wparam, nint lparam);
    [DllImport("shell32.dll")] private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, nint token, out nint path);

    internal static string ExpandPath(string path)
    {
        if (path.StartsWith('{') && path.IndexOf('}') is int end && end > 0 && Guid.TryParse(path[..(end + 1)], out var id))
        {
            if (SHGetKnownFolderPath(ref id, 0, 0, out var ptr) == 0)
            {
                try { return (Marshal.PtrToStringUni(ptr) ?? "") + path[(end + 1)..]; }
                finally { Marshal.FreeCoTaskMem(ptr); }
            }
        }
        return Environment.ExpandEnvironmentVariables(path);
    }
    internal static string? ProcessPath(uint pid)
    {
        var handle = OpenProcess(0x1000, false, pid);
        if (handle == 0) return null;
        try
        {
            var path = new System.Text.StringBuilder(32768); uint size = 32768;
            return QueryFullProcessImageName(handle, 0, path, ref size) ? path.ToString() : null;
        }
        finally { CloseHandle(handle); }
    }
    internal static bool Exists(TrayEntry e)
    {
        if (e.Guid == Guid.Empty && (GetWindowThreadProcessId(e.Window, out var pid) == 0 || pid != e.ProcessId)) return false;
        var id = new Identifier { cbSize = (uint)Marshal.SizeOf<Identifier>(), guidItem = e.Guid, hWnd = e.Window, uID = e.Uid };
        return Shell_NotifyIconGetRect(ref id, out _) == 0;
    }
    internal static bool Hide(TrayEntry e, bool hidden)
    {
        // Hidden icons can stop exposing a rectangle after Explorer processes NIS_HIDDEN.
        // Validate their owner instead; Shell_NotifyIcon itself validates the icon identity.
        if (e.Guid == Guid.Empty && !OwnerAlive(e)) return false;
        var data = new Data { cbSize = (uint)Marshal.SizeOf<Data>(), hWnd = e.Window, uID = e.Uid, guidItem = e.Guid,
            uFlags = 8u | (e.Guid != Guid.Empty ? 32u : 0u), dwStateMask = 1, dwState = hidden ? 1u : 0u };
        return Shell_NotifyIconW(1, ref data);
    }
    internal static bool OwnerAlive(TrayEntry e)
    {
        if (e.Guid == Guid.Empty)
            return GetWindowThreadProcessId(e.Window, out var pid) != 0 && pid == e.ProcessId &&
                string.Equals(ProcessPath(pid), e.Path, StringComparison.OrdinalIgnoreCase);
        return e.ProcessId != 0 && string.Equals(ProcessPath(e.ProcessId), e.Path, StringComparison.OrdinalIgnoreCase);
    }
}
