namespace TrayManager;

internal readonly record struct LayoutMetrics(double FontScale, double TargetHeight, bool Compact)
{
    // XAML dimensions are already DPI-independent. Never multiply by system DPI here.
    internal static LayoutMetrics For(bool large, double width, double height)
    {
        bool compact = width < 1000 || height < 700;
        return new(large && width >= 1200 && height >= 850 ? 1.15 : 1,
            large ? (compact ? 40 : 44) : 36, compact);
    }
}
