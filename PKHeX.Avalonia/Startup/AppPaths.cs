using System;
using System.IO;

namespace PKHeX.Avalonia.Startup;

/// <summary>
/// Resolves where the application stores its configuration and local resource folders.
/// </summary>
/// <remarks>
/// The WinForms application keeps everything next to the executable.
/// On Linux the executable directory is often read-only (e.g. installed under /usr or /opt), so the port follows the
/// XDG Base Directory conventions by default. "Portable mode" (everything next to the executable, like WinForms)
/// is used when a <c>cfg.json</c> already exists in the executable directory.
/// </remarks>
public static class AppPaths
{
    public const string ConfigFileName = "cfg.json";
    private const string AppFolderName = "PKHeX";

    /// <summary>
    /// Directory containing the executable.
    /// </summary>
    public static string ExecutableDirectory { get; } = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    /// <summary>
    /// True if the configuration is stored next to the executable (WinForms-style layout).
    /// </summary>
    public static bool IsPortable { get; } = File.Exists(Path.Combine(ExecutableDirectory, ConfigFileName));

    /// <summary>
    /// Directory where <c>cfg.json</c> is stored.
    /// </summary>
    public static string ConfigDirectory { get; } = IsPortable ? ExecutableDirectory : GetXdgDirectory("XDG_CONFIG_HOME", ".config");

    /// <summary>
    /// Directory used as the base for relative local resource folders (pkmdb, bak, template, plugins, ...).
    /// </summary>
    public static string DataDirectory { get; } = IsPortable ? ExecutableDirectory : GetXdgDirectory("XDG_DATA_HOME", Path.Combine(".local", "share"));

    /// <summary>
    /// Full path of the configuration file.
    /// </summary>
    public static string ConfigFilePath => Path.Combine(ConfigDirectory, ConfigFileName);

    private static string GetXdgDirectory(string variable, string fallbackRelativeToHome)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            value = Path.Combine(home, fallbackRelativeToHome);
        }
        return Path.Combine(value, AppFolderName);
    }

    /// <summary>
    /// Ensures the configuration and data directories exist.
    /// </summary>
    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(DataDirectory);
    }
}
