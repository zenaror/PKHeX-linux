using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Borderless window shown while the main window is being built (port of the WinForms <c>SplashScreen</c>).
/// </summary>
/// <remarks>
/// WinForms runs the splash on its own STA thread with its own message loop and blocks closing until
/// <c>ForceClose</c>. Avalonia builds the main window on the UI thread before it is shown, so the splash is a plain
/// window on the same thread that <see cref="Close"/> dismisses once the main window is up.
/// </remarks>
public sealed class SplashWindow : Window
{
    public SplashWindow()
    {
        Name = "SplashScreen";
        Title = "PKHeX";
        Icon = AppIcon.Get();
        WindowDecorations = WindowDecorations.None;
        ShowInTaskbar = false;
        CanResize = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Width = 240;
        Height = 72;

        var icon = new Image
        {
            Source = AppIcon.GetBitmap(),
            Width = 40,
            Height = 40,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var text = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 2,
            Children =
            {
                UiFactory.Label("L_Status", "Starting up PKHeX..."),
                UiFactory.Label("L_Site", "ProjectPokemon.org"),
            },
        };

        Content = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x80, 0x80, 0x80)),
            BorderThickness = new global::Avalonia.Thickness(1),
            Padding = new global::Avalonia.Thickness(12, 8),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { icon, text } },
        };
    }
}
