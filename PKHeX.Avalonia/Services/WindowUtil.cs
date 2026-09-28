using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PKHeX.Avalonia.Localization;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Helpers for the single-instance tool windows.
/// </summary>
public static class WindowUtil
{
    /// <summary>
    /// Focuses an already open window of type <typeparamref name="T"/>, if there is one.
    /// </summary>
    /// <returns>True if such a window existed (and was focused), so the caller should not open another.</returns>
    /// <remarks>Port of <c>WinFormsUtil.OpenWindowExists</c>; these tool windows are single-instance upstream.</remarks>
    public static bool OpenWindowExists<T>() where T : Window
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return false;
        foreach (var window in desktop.Windows)
        {
            if (window is not T)
                continue;
            window.Activate();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gives a menu item the WinForms designer's icon, inverted in dark mode as <c>InvertToolStripIcons</c> does.
    /// </summary>
    public static void SetMenuIcon(MenuItem item, string icon)
    {
        var bmp = App.IsDarkModeEnabled ? AppResources.GetImageBlackToWhite(icon) : AppResources.GetImage(icon);
        if (bmp is not null)
            item.Icon = new Image { Source = bmp, Width = 16, Height = 16 };
    }
}
