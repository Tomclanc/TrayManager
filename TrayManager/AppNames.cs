using System.Collections.Concurrent;
using System.Diagnostics;

namespace TrayManager;

internal static class AppNames
{
    private static readonly ConcurrentDictionary<string, string> cache = new(StringComparer.OrdinalIgnoreCase);
    internal static string Resolve(string path, string? tooltip)
    {
        string friendly = cache.GetOrAdd(path, ReadMetadata);
        if (!string.IsNullOrWhiteSpace(friendly)) return friendly;
        return string.IsNullOrWhiteSpace(tooltip) ? Path.GetFileNameWithoutExtension(path) : tooltip.Replace('\r', ' ').Replace('\n', ' ');
    }
    private static string ReadMetadata(string path)
    {
        if (path.EndsWith(@"\PowerToys.Awake.exe", StringComparison.OrdinalIgnoreCase)) return "PowerToys Awake";
        // Localized display names for Windows components whose EXE metadata exposes a host/process name.
        if (string.Equals(path, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "SecurityHealthSystray.exe"), StringComparison.OrdinalIgnoreCase)) return "Windows 安全中心";
        if (path.Contains(@"\WindowsApps\Microsoft.YourPhone_", StringComparison.OrdinalIgnoreCase) && path.EndsWith(@"\PhoneExperienceHost.exe", StringComparison.OrdinalIgnoreCase)) return "手机连接";
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            var stem = Path.GetFileNameWithoutExtension(path);
            foreach (var text in new[] { info.FileDescription, info.ProductName })
                if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, stem, StringComparison.OrdinalIgnoreCase) &&
                    !text.Contains("Operating System", StringComparison.OrdinalIgnoreCase)) return text.Trim();
        }
        catch { }
        return "";
    }
}
