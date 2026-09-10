namespace TrayManager;

internal static class Hotkey
{
    internal static bool Valid(uint modifiers, uint key) => modifiers is > 0 and <= 7 &&
        ((key >= 0x41 && key <= 0x5A) || (key >= 0x30 && key <= 0x39) || (key >= 0x70 && key <= 0x7A));
    internal static string Label(uint modifiers, uint key)
    {
        if (!Valid(modifiers, key)) return "未设置";
        var parts = new List<string>();
        if ((modifiers & 2) != 0) parts.Add("Ctrl");
        if ((modifiers & 1) != 0) parts.Add("Alt");
        if ((modifiers & 4) != 0) parts.Add("Shift");
        parts.Add(key >= 0x70 ? "F" + (key - 0x70 + 1) : ((char)key).ToString());
        return string.Join(" + ", parts);
    }
}
