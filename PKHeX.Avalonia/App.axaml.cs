using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using PKHeX.Avalonia.Views;

namespace PKHeX.Avalonia;

public sealed class App : Application
{
    public static new App Current => (App)Application.Current!;

    /// <summary>
    /// True if the dark theme is active.
    /// </summary>
    public static bool IsDarkModeEnabled => Current.ActualThemeVariant == ThemeVariant.Dark;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = Program.Settings;
            if (settings.IsNew) // auto-detect for new settings, json load preserves any choice.
                settings.Startup.DarkMode = PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;
            RequestedThemeVariant = settings.Startup.DarkMode ? ThemeVariant.Dark : ThemeVariant.Light;

#if !DEBUG
            Dispatcher.UIThread.UnhandledException += UIThreadException;
#endif
            var main = new MainWindow();
            desktop.MainWindow = main;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        base.OnFrameworkInitializationCompleted();
    }

#if !DEBUG
    private static async void UIThreadException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            e.Handled = true;
            var owner = (Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var result = await ErrorWindow.ShowErrorDialog(owner, "An error occurred in PKHeX. Please report this error to the PKHeX author.", e.Exception, true);
            if (result == Services.DialogResult.Abort)
                (Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(1);
        }
        catch (Exception reportingException)
        {
            Console.Error.WriteLine(reportingException);
        }
    }
#endif
}
