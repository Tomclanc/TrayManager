using Microsoft.Win32;
using System.Diagnostics;

namespace TrayManager;

internal sealed record TrayEntry(string Key, string Name, string Path, Guid Guid, nint Window, uint Uid, uint ProcessId, byte[]? Image);

internal static class TrayCatalog
{
    // The registry is a cache, not a live list. Every returned entry must pass the shell's live-identity check.
    internal static List<TrayEntry> Read(HashSet<string>? desiredHidden = null)
    {
        var processes = new Dictionary<uint, string>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try { if (Native.ProcessPath((uint)process.Id) is string path) processes[(uint)process.Id] = path; }
                catch { /* Protected processes are deliberately not guessed by name. */ }
            }
        }
        var windows = new List<(nint Window, uint Pid)>();
        Native.EnumWindows((h, _) => { Native.GetWindowThreadProcessId(h, out var pid); windows.Add((h, pid)); return true; }, 0);
        nint previous = 0;
        while ((previous = Native.FindWindowEx(-3, previous, null, null)) != 0)
        {
            Native.GetWindowThreadProcessId(previous, out var pid); windows.Add((previous, pid));
        }
        var entries = new List<TrayEntry>();
        using var root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");
        if (root == null) return entries;
        foreach (var key in root.GetSubKeyNames())
        {
            using var item = root.OpenSubKey(key);
            if (item?.GetValue("ExecutablePath") is not string raw) continue;
            var path = Native.ExpandPath(raw);
            var name = AppNames.Resolve(path, item.GetValue("InitialTooltip") as string);
            var snapshot = item.GetValue("IconSnapshot") as byte[];
            if (Guid.TryParse(item.GetValue("IconGuid")?.ToString(), out var guid) && guid != Guid.Empty)
            {
                var owners = processes.Where(p => string.Equals(p.Value, path, StringComparison.OrdinalIgnoreCase)).ToArray();
                var entry = new TrayEntry("guid:" + guid, name, path, guid, 0, 0, owners.Length == 1 ? owners[0].Key : 0, snapshot);
                if (Native.Exists(entry) || (desiredHidden?.Contains(entry.Key) == true && owners.Length == 1)) entries.Add(entry);
                continue;
            }
            if (item.GetValue("UID") is not int number) continue;
            var uid = unchecked((uint)number);
            var candidates = windows.Where(w => processes.TryGetValue(w.Pid, out var exe) && string.Equals(exe, path, StringComparison.OrdinalIgnoreCase))
                .Select(w => new TrayEntry("path:" + path.ToLowerInvariant() + ":" + uid, name, path, Guid.Empty, w.Window, uid, w.Pid, snapshot))
                .Where(Native.Exists).ToArray();
            if (candidates.Length == 1) entries.Add(candidates[0]);
        }
        return entries.DistinctBy(e => e.Key).OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
