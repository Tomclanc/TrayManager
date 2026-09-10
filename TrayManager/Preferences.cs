using System.Text.Json;

namespace TrayManager;

internal sealed class Preferences
{
    public HashSet<string> Hidden { get; set; } = [];
    public bool HideOwnIcon { get; set; }
    public Dictionary<string, SavedIdentity> Identities { get; set; } = [];
    internal static string DirectoryPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tomclanc", "TrayManager");
    internal static string FilePath => System.IO.Path.Combine(DirectoryPath, "settings.json");
    internal static Preferences Load() => System.IO.File.Exists(FilePath)
        ? JsonSerializer.Deserialize<Preferences>(System.IO.File.ReadAllText(FilePath)) ?? new() : new();
    internal void Save()
    {
        System.IO.Directory.CreateDirectory(DirectoryPath);
        System.IO.File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        System.IO.File.Move(FilePath + ".tmp", FilePath, true);
    }
}

internal sealed record SavedIdentity(string Name, string Path, Guid Guid, long Window, uint Uid, uint ProcessId, long Started)
{
    internal static long StartTime(uint pid)
    {
        try { using var process = System.Diagnostics.Process.GetProcessById((int)pid); return process.StartTime.ToUniversalTime().Ticks; }
        catch { return 0; }
    }
    internal static SavedIdentity From(TrayEntry e) => new(e.Name, e.Path, e.Guid, e.Window.ToInt64(), e.Uid, e.ProcessId, StartTime(e.ProcessId));
    internal TrayEntry? Resolve(string key)
    {
        if (Started == 0 || StartTime(ProcessId) != Started) return null;
        var entry = new TrayEntry(key, Name, Path, Guid, (nint)Window, Uid, ProcessId, null);
        return Native.OwnerAlive(entry) ? entry : null;
    }
}
