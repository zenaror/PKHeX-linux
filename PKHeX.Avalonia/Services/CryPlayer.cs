using System;
using System.Diagnostics;
using System.IO;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;
using PKHeX.Avalonia.Views;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Plays Pokémon cries from the user's cry folder (port of the WinForms <c>CryPlayer</c>).
/// </summary>
/// <remarks>
/// The WinForms player uses <c>System.Media.SoundPlayer</c>, which is Windows-only. There is no cross-platform
/// audio API in the base framework and the port does not take an audio dependency for a single optional cue, so the
/// wave file is handed to the first command-line player found on the system. If none is installed, nothing is played;
/// every other hover behaviour is unaffected.
/// </remarks>
public sealed class CryPlayer
{
    private static readonly string[] Players = ["paplay", "aplay", "pw-play", "ffplay"];
    private static readonly string? Player = FindPlayer();
    private Process? Current;

    private static string? FindPlayer()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
            return null;
        foreach (var player in Players)
        {
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                if (dir.Length != 0 && File.Exists(Path.Combine(dir, player)))
                    return player;
            }
        }
        return null;
    }

    /// <summary>
    /// Plays the cry for the specified species and form.
    /// </summary>
    public void PlayCry(ISpeciesForm pk, EntityContext context)
    {
        if (Player is null || pk.Species == 0)
            return;

        var path = GetCryPath(pk, MainWindow.CryPath, context);
        if (!File.Exists(path))
            return;

        Stop();
        try
        {
            var args = Player == "ffplay" ? $"-nodisp -autoexit -loglevel quiet \"{path}\"" : $"\"{path}\"";
            Current = Process.Start(new ProcessStartInfo(Player, args) { UseShellExecute = false, CreateNoWindow = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to play cry: {ex.Message}");
        }
    }

    /// <summary>Stops a cry that is still playing.</summary>
    public void Stop()
    {
        var process = Current;
        Current = null;
        if (process is null)
            return;
        try
        {
            if (!process.HasExited)
                process.Kill();
            process.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to stop cry: {ex.Message}");
        }
    }

    private static string GetCryPath(ISpeciesForm pk, string cryFolder, EntityContext context)
    {
        var name = GetCryFileName(pk, context);
        var path = Path.Combine(cryFolder, $"{name}.wav");
        if (!File.Exists(path))
            path = Path.Combine(cryFolder, $"{pk.Species}.wav");
        return path;
    }

    private static string GetCryFileName(ISpeciesForm pk, EntityContext context)
    {
        if (pk is { Species: (int)Species.Urshifu, Form: 1 }) // same sprite for both forms, but different cries
            return "892-1";

        // don't grab sprite of pk, no gender-specific cries
        var res = SpriteName.GetResourceStringSprite(pk.Species, pk.Form, 0, 0, context);

        // people like - instead of _ file names ;)
        return res.Replace('_', '-')[1..]; // skip leading underscore
    }
}
