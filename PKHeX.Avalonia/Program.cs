using System;
using System.IO;
using System.Reflection;
using Avalonia;
using PKHeX.Avalonia.Settings;
using PKHeX.Avalonia.Startup;
using PKHeX.Core;

namespace PKHeX.Avalonia;

/// <summary>
/// Application entry point (port of the WinForms <c>Program</c>).
/// </summary>
internal static class Program
{
    public static readonly Version CurrentVersion = GetCurrentVersion();

    public static string PathConfig => AppPaths.ConfigFilePath;

    /// <summary>
    /// Global settings instance, loaded before any windows are created.
    /// </summary>
    public static PKHeXSettings Settings { get; private set; } = null!;

    public static bool HaX { get; private set; }

    /// <summary>
    /// Startup values computed before the main window exists.
    /// </summary>
    public static StartupArguments StartupArgs { get; private set; } = null!;
    public static ProgramInit Init { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        // Load settings first
        AppPaths.EnsureDirectories();
        var settings = Settings = PKHeXSettings.GetSettings(PathConfig);
        settings.LocalResources.SetLocalPath(AppPaths.DataDirectory);
        StartupUtil.ReloadSettings(settings);

        // Prepare initial values for the main window.
        StartupArgs = StartupUtil.GetStartup(args, settings);
        Init = StartupUtil.FormLoadInitialActions(args, settings, CurrentVersion);
        HaX = Init.HaX;

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static Version GetCurrentVersion()
    {
        var asm = typeof(Program).Assembly;
        var informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (informational is not null && Version.TryParse(GetSaneVersionTag(informational), out var v))
            return v;
        return asm.GetName().Version ?? new Version(0, 0, 0);
    }

    private static ReadOnlySpan<char> GetSaneVersionTag(ReadOnlySpan<char> productVersion)
    {
        for (int i = 0; i < productVersion.Length; i++)
        {
            char c = productVersion[i];
            if (c == '.')
                continue;
            if (char.IsNumber(c))
                continue;
            return productVersion[..i];
        }
        return productVersion;
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // Last resort: write an error report next to the configuration so the user can report it.
        try
        {
            var message = e.ExceptionObject?.ToString() ?? "null exception";
            var path = Path.Combine(AppPaths.ConfigDirectory, $"PKHeX_Error_Report {DateTime.Now:yyyyMMddHHmmss}.txt");
            File.WriteAllText(path, message);
            Console.Error.WriteLine(message);
        }
        catch
        {
            // Do nothing. If we can't log the error, there's not much else we can do.
        }
    }
}
