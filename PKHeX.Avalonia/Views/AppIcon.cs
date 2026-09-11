using Avalonia.Controls;
using PKHeX.Avalonia.Localization;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Shared program window icon.
/// </summary>
public static class AppIcon
{
    private static WindowIcon? Cached;
    private static global::Avalonia.Media.Imaging.Bitmap? CachedBitmap;

    public static WindowIcon? Get()
    {
        if (Cached is not null)
            return Cached;
        using var stream = AppResources.OpenProgramIcon();
        if (stream is null)
            return null;
        return Cached = new WindowIcon(stream);
    }

    /// <summary>Program icon as a bitmap, for placing inside a window's content.</summary>
    public static global::Avalonia.Media.Imaging.Bitmap? GetBitmap()
    {
        if (CachedBitmap is not null)
            return CachedBitmap;
        using var stream = AppResources.OpenProgramIcon();
        if (stream is null)
            return null;
        return CachedBitmap = new global::Avalonia.Media.Imaging.Bitmap(stream);
    }
}
