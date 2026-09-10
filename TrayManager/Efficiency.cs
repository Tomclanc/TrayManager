using System.Runtime.InteropServices;

namespace TrayManager;

internal static class Efficiency
{
    private static bool? applied;
    private static uint originalPriority;
    [StructLayout(LayoutKind.Sequential)]
    internal struct PowerState { public uint Version, ControlMask, StateMask; }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(nint process, int kind, ref PowerState state, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetProcessInformation(nint process, int kind, ref PowerState state, uint size);
    [DllImport("kernel32.dll")] internal static extern nint GetCurrentProcess();
    [DllImport("kernel32.dll")] internal static extern uint GetPriorityClass(nint process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetPriorityClass(nint process, uint priority);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint window);

    internal static bool Apply(bool enabled)
    {
        if (applied == enabled) return true;
        var process = GetCurrentProcess(); // Never target a managed application's process.
        if (originalPriority == 0) originalPriority = GetPriorityClass(process);
        if (originalPriority == 0) return false;
        var state = new PowerState { Version = 1, ControlMask = 1, StateMask = enabled ? 1u : 0u };
        if (!SetProcessInformation(process, 4, ref state, 12)) return false;
        if (!SetPriorityClass(process, enabled ? 0x40u : originalPriority))
        {
            state.StateMask = 0;
            SetProcessInformation(process, 4, ref state, 12);
            applied = null;
            return false;
        }
        applied = enabled;
        return true;
    }
}
