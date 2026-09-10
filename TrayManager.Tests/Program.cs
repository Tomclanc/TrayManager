using System.Runtime.InteropServices;
using TrayManager;

internal static class Tests
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint ex, string cls, string name, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint hwnd);
    private static void Check(bool result, string name) { if (!result) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); }
    private static void Main()
    {
        Check(Marshal.SizeOf<Native.Data>() == 976, "NOTIFYICONDATA x64 layout");
        Check(Marshal.SizeOf<Native.Identifier>() == 40, "NOTIFYICONIDENTIFIER x64 layout");
        Check(string.Equals(Native.ProcessPath((uint)Environment.ProcessId), Environment.ProcessPath, StringComparison.OrdinalIgnoreCase), "limited-access process identity");
        Check(Native.ExpandPath("{6D809377-6AF0-444B-8957-A3773F02200E}\\test").EndsWith("Program Files\\test", StringComparison.OrdinalIgnoreCase), "known-folder expansion");
        var window = CreateWindowEx(0, "STATIC", "TrayManager test fixture", 0, 0, 0, 1, 1, 0, 0, 0, 0);
        Check(window != 0, "test fixture window");
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
        Console.WriteLine("Live Awake icons: " + live.Count(e => e.Path.EndsWith("PowerToys.Awake.exe", StringComparison.OrdinalIgnoreCase)));
        Console.WriteLine("All tests passed; no user application icons changed.");
    }
}
