using System.Runtime.InteropServices;
using TrayManager;

internal static class Tests
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint ex, string cls, string name, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint hwnd);
    private static void Check(bool result, string name) { if (!result) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); }
    private static void Main()
    {
        var priority = Efficiency.GetPriorityClass(Efficiency.GetCurrentProcess());
        try
        {
            Check(Efficiency.Apply(true), "enable own-process efficiency mode");
            var power = new Efficiency.PowerState { Version = 1 };
            Check(Efficiency.GetProcessInformation(Efficiency.GetCurrentProcess(), 4, ref power, 12) && (power.StateMask & 1) != 0, "EcoQoS readback enabled");
            Check(Efficiency.GetPriorityClass(Efficiency.GetCurrentProcess()) == 0x40, "background idle priority");
        }
        finally { Efficiency.Apply(false); }
        var normalPower = new Efficiency.PowerState { Version = 1 };
        Check(Efficiency.GetProcessInformation(Efficiency.GetCurrentProcess(), 4, ref normalPower, 12) && (normalPower.StateMask & 1) == 0, "EcoQoS restored");
        Check(Efficiency.GetPriorityClass(Efficiency.GetCurrentProcess()) == priority, "original priority restored");
        Check(LayoutMetrics.For(true, 1920 / 2, 1080 / 2).FontScale == 1, "1080p 200% no extra font zoom");
        Check(LayoutMetrics.For(true, 1920 / 2, 1080 / 2).Compact, "1080p 200% compact layout");
        Check(LayoutMetrics.For(true, 2880 / 2, 1800 / 2).FontScale == 1.15, "spacious 200% bounded enlargement");
        Check(LayoutMetrics.For(false, 1920, 1080).FontScale == 1, "desktop uses native DIP sizing");
        Check(Marshal.SizeOf<Native.Data>() == 976, "NOTIFYICONDATA x64 layout");
        Check(Marshal.SizeOf<Native.Identifier>() == 40, "NOTIFYICONIDENTIFIER x64 layout");
        Check(string.Equals(Native.ProcessPath((uint)Environment.ProcessId), Environment.ProcessPath, StringComparison.OrdinalIgnoreCase), "limited-access process identity");
        Check(Native.ExpandPath("{6D809377-6AF0-444B-8957-A3773F02200E}\\test").EndsWith("Program Files\\test", StringComparison.OrdinalIgnoreCase), "known-folder expansion");
        var window = CreateWindowEx(0, "STATIC", "TrayManager test fixture", 0, 0, 0, 1, 1, 0, 0, 0, 0);
        Check(window != 0, "test fixture window");
        Check(Hotkey.Valid(3, 0x54), "Ctrl Alt T accepted");
        Check(!Hotkey.Valid(0, 0x54), "bare key rejected");
        Check(!Hotkey.Valid(8, 0x54), "Windows modifier excluded");
        Check(!Hotkey.Valid(3, 0x7B), "reserved F12 excluded");
        Check(Hotkey.Label(3, 0x54) == "Ctrl + Alt + T", "hotkey label");
        uint testKey = Enumerable.Range(0x70, 11).Select(x => (uint)x).FirstOrDefault(k => Native.RegisterHotKey(window, 901, 7 | 0x4000, k));
        Check(testKey != 0, "register unused test hotkey");
        try { Check(!Native.RegisterHotKey(window, 902, 7 | 0x4000, testKey), "hotkey conflict detected"); }
        finally { Native.UnregisterHotKey(window, 901); Native.UnregisterHotKey(window, 902); }
        Check(Native.RegisterHotKey(window, 903, 7 | 0x4000, testKey), "hotkey released and reusable");
        Native.UnregisterHotKey(window, 903);
        var data = new Native.Data { cbSize = (uint)Marshal.SizeOf<Native.Data>(), hWnd = window, uID = 27182, uFlags = 2 | 4,
            hIcon = Native.LoadIcon(0, 32512), szTip = "TrayManager temporary self-test" };
        try
        {
            Check(Native.Shell_NotifyIconW(0, ref data), "add disposable tray fixture");
            var entry = new TrayEntry("test", "test", Environment.ProcessPath!, Guid.Empty, window, 27182, (uint)Environment.ProcessId, null);
            Check(Native.Exists(entry), "live identity");
            Check(!Native.Exists(entry with { ProcessId = uint.MaxValue }), "reject wrong process identity");
            Check(Native.Hide(entry, true), "hide test icon");
            Thread.Sleep(300);
            Check(Native.OwnerAlive(entry), "hidden icon owner remains addressable");
            var saved = SavedIdentity.From(entry);
            var restored = System.Text.Json.JsonSerializer.Deserialize<SavedIdentity>(System.Text.Json.JsonSerializer.Serialize(saved))!;
            Check(restored.Resolve("test") is not null, "saved hidden identity survives JSON roundtrip");
            Check((restored with { Started = 1 }).Resolve("test") is null, "reject stale process session");
            Check(Native.Hide(entry, false), "restore test icon");
            Check(Native.Shell_NotifyIconW(2, ref data), "remove test icon");
            Check(!Native.Exists(entry), "reject removed icon");
        }
        finally { Native.Shell_NotifyIconW(2, ref data); DestroyWindow(window); }
        var live = TrayCatalog.Read();
        Console.WriteLine("Windows Xbox FSE active: " + GamingExperience.IsActive());
        Console.WriteLine("Live Awake icons: " + live.Count(e => e.Path.EndsWith("PowerToys.Awake.exe", StringComparison.OrdinalIgnoreCase)));
        Console.WriteLine("All tests passed; no user application icons changed.");
    }
}
