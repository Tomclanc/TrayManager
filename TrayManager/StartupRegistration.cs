using Microsoft.Win32;
namespace TrayManager;
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Tomclanc.TrayManager";
    internal static string Command(string executable) => "\"" + executable + "\" --background";
    internal static bool IsEnabled(string? testKey = null)
    {
        using var key = Registry.CurrentUser.OpenSubKey(testKey ?? RunKey);
        return string.Equals(key?.GetValue(ValueName) as string, Command(Environment.ProcessPath!), StringComparison.OrdinalIgnoreCase);
    }
    internal static void SetEnabled(bool enabled, string? testKey = null)
    {
        using var key = Registry.CurrentUser.CreateSubKey(testKey ?? RunKey, true);
        if (enabled) key.SetValue(ValueName, Command(Environment.ProcessPath!), RegistryValueKind.String);
        else key.DeleteValue(ValueName, false);
    }
}
