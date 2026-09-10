using System.Runtime.InteropServices;

namespace TrayManager;

internal static class GamingExperience
{
    private static bool unavailable;

    // Query the Windows gaming shell, not Xbox process presence or window geometry.
    internal static bool IsActive()
    {
        if (unavailable) return false;
        try { return IsGamingFullScreenExperienceActive(); }
        catch (DllNotFoundException) { unavailable = true; return false; }
        catch (EntryPointNotFoundException) { unavailable = true; return false; }
    }

    [DllImport("api-ms-win-gaming-experience-l1-1-0.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsGamingFullScreenExperienceActive();
}
