using Avalonia.Controls;
using PKHeX.Avalonia.Localization;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Shared program window icon.
/// </summary>
public static class AppIcon
{
    private static WindowIcon? Cached;

    public static WindowIcon? Get()
    {
        if (Cached is not null)
            return Cached;
        using var stream = AppResources.OpenProgramIcon();
        if (stream is null)
            return null;
        return Cached = new WindowIcon(stream);
    }
}
