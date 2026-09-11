using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PKHeX.Core;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// File dialogs (port of the WinForms <c>WinFormsUtil</c> file dialog helpers) using the Avalonia storage provider.
/// </summary>
public static class FileDialogs
{
    public static bool DetectSaveFileOnFileOpen { private get; set; } = true;

    /// <summary>
    /// Reads in custom extension types that allow the program to open more extensions.
    /// </summary>
    /// <param name="exts">Extensions to add</param>
    public static void AddSaveFileExtensions(IEnumerable<string> exts)
    {
        // Only add new (unique) extensions
        var dest = CustomSaveExtensions;
        foreach (var ext in exts)
        {
            if (!dest.Contains(ext))
                dest.Add(ext);
        }
    }

    private static List<string> CustomSaveExtensions => SaveFileMetadata.CustomSaveExtensions;

    public static bool IsFileExtensionSAV(ReadOnlySpan<char> file)
    {
        var ext = Path.GetExtension(file);
        foreach (var other in CustomSaveExtensions)
        {
            if (ext.EndsWith(other))
                return true;
        }
        return false;
    }

    private static string ExtraSaveExtensions => ";" + string.Join(";", CustomSaveExtensions.Select(z => $"*.{z}"));

    /// <summary>
    /// Converts a WinForms style filter string (<c>Name|*.ext;*.ext2|Name2|...</c>) into file picker types.
    /// </summary>
    public static List<FilePickerFileType> ParseFilter(string filter)
    {
        var result = new List<FilePickerFileType>();
        if (string.IsNullOrWhiteSpace(filter))
            return result;
        var parts = filter.Split('|');
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            var name = parts[i];
            var patterns = parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (patterns.Length == 1 && patterns[0] == "*.*")
            {
                result.Add(FilePickerFileTypes.All);
                continue;
            }
            result.Add(new FilePickerFileType(name) { Patterns = patterns });
        }
        return result;
    }

    private static async Task<IStorageFolder?> GetFolder(IStorageProvider provider, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return null;
        try { return await provider.TryGetFolderFromPathAsync(path); }
        catch { return null; }
    }

    /// <summary>
    /// Opens a dialog to open a <see cref="SaveFile"/>, <see cref="PKM"/> file, or any other supported file.
    /// </summary>
    /// <param name="owner">Window to anchor the dialog to.</param>
    /// <param name="extensions">Misc extensions of <see cref="PKM"/> files supported by the Save File.</param>
    /// <returns>Path of the file to load, or null if cancelled.</returns>
    public static async Task<string?> OpenSAVPKMDialog(Window owner, IEnumerable<string> extensions)
    {
        var sb = new System.Text.StringBuilder(128);
        foreach (var type in extensions)
            sb.Append($"*.{type};");

        string supported = sb.ToString();
        var filter = "All Files|*.*" +
                     $"|Supported Files (*.*)|main;*.bin;{supported};*.bak" + ExtraSaveExtensions +
                     "|Save Files (*.sav)|main" + ExtraSaveExtensions +
                     "|Decrypted PKM File (*.pk)|" + supported +
                     "|Binary File|*.bin" +
                     "|Backup File|*.bak";

        var provider = owner.StorageProvider;
        var options = new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = ParseFilter(filter),
        };

        var suggestion = await SuggestInitialFileName(owner);
        if (suggestion is not null)
        {
            options.SuggestedFileName = Path.GetFileName(suggestion);
            options.SuggestedStartLocation = await GetFolder(provider, Path.GetDirectoryName(suggestion));
        }

        var files = await provider.OpenFilePickerAsync(options);
        if (files.Count == 0)
            return null;
        return files[0].TryGetLocalPath();
    }

    private static async Task<string?> SuggestInitialFileName(Window owner)
    {
        if (!DetectSaveFileOnFileOpen)
            return null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var token = cts.Token;
            var sav = await Task.Run(() => SaveFinder.FindMostRecentSaveFile(token), token);
            return sav?.Metadata.FilePath;
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(owner, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Opens a dialog to save a <see cref="PKM"/> file.
    /// </summary>
    /// <returns>True if the file was saved.</returns>
    public static async Task<bool> SavePKMDialog(Window owner, PKM pk)
    {
        string pkx = pk.Extension;
        bool allowEncrypted = pk.Format >= 3 && pkx.StartsWith('p');
        var genericFilter = $"Decrypted PKM File|*.{pkx}" +
                            (allowEncrypted ? $"|Encrypted PKM File|*.e{pkx.AsSpan(1)}" : string.Empty) +
                            "|Binary File|*.bin" +
                            "|All Files|*.*";
        var options = new FilePickerSaveOptions
        {
            FileTypeChoices = ParseFilter(genericFilter),
            DefaultExtension = pkx,
            SuggestedFileName = PathUtil.CleanFileName(pk.FileName),
            ShowOverwritePrompt = true,
        };
        var file = await owner.StorageProvider.SaveFilePickerAsync(options);
        var path = file?.TryGetLocalPath();
        if (path is null)
            return false;

        SaveExport.SavePKM(pk, path, pkx);
        return true;
    }

    /// <summary>
    /// Opens a dialog to save a <see cref="SaveFile"/> file.
    /// </summary>
    /// <param name="owner">Window to anchor the dialog to.</param>
    /// <param name="sav"><see cref="SaveFile"/> to be saved.</param>
    /// <param name="currentBox">Box the player will be greeted with when accessing the PC in-game.</param>
    /// <param name="forceSaveAs">Whether to force the Save As dialog even if the file exists.</param>
    /// <returns>True if the file was saved.</returns>
    public static async Task<bool> ExportSAVDialog(Window owner, SaveFile sav, int currentBox = 0, bool forceSaveAs = false)
    {
        // Try to request an overwrite first; if they defer, do the save file dialog.
        if (!forceSaveAs && !sav.Metadata.IsBackup && File.Exists(sav.Metadata.FilePath))
        {
            var exist = sav.Metadata.FilePath;
            var task = await AppDialogs.RequestOverwrite(owner, exist);
            if (task == DialogResult.Cancel)
                return false;
            if (task == DialogResult.Yes)
            {
                await SaveExport.ExportSAV(owner, sav, exist);
                return true;
            }
        }

        var provider = owner.StorageProvider;
        var options = new FilePickerSaveOptions
        {
            FileTypeChoices = ParseFilter(sav.Metadata.Filter),
            SuggestedFileName = sav.Metadata.FileName,
            SuggestedStartLocation = await GetFolder(provider, sav.Metadata.FileFolder),
            ShowOverwritePrompt = true,
        };

        var file = await provider.SaveFilePickerAsync(options);
        var path = file?.TryGetLocalPath();
        if (path is null)
            return false;

        // Set box now that we're saving
        if (sav.HasBox)
            sav.CurrentBox = currentBox;

        await SaveExport.ExportSAV(owner, sav, path);
        return true;
    }

    /// <summary>
    /// Opens a dialog to save a <see cref="MysteryGift"/> file.
    /// </summary>
    /// <returns>True if the file was saved.</returns>
    public static async Task<bool> ExportMGDialog(Window owner, DataMysteryGift gift)
    {
        var options = new FilePickerSaveOptions
        {
            FileTypeChoices = ParseFilter(GetMysterGiftFilter(gift.Context)),
            SuggestedFileName = PathUtil.CleanFileName(gift.FileName),
            ShowOverwritePrompt = true,
        };
        var file = await owner.StorageProvider.SaveFilePickerAsync(options);
        var path = file?.TryGetLocalPath();
        if (path is null)
            return false;

        SaveExport.SaveBackup(path);
        File.WriteAllBytes(path, gift.Write());
        return true;
    }

    /// <summary>
    /// Opens a single-file picker with a WinForms style filter string.
    /// </summary>
    /// <returns>Selected path, or null if cancelled.</returns>
    public static async Task<string?> OpenSingleFile(Window owner, string? filter = null)
    {
        var options = new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = filter is null ? null : ParseFilter(filter),
        };
        var files = await owner.StorageProvider.OpenFilePickerAsync(options);
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    /// <summary>
    /// Opens a generic "save file" dialog.
    /// </summary>
    /// <param name="owner">Window to anchor the dialog to.</param>
    /// <param name="filter">WinForms style filter string (<c>Name|*.ext|...</c>), or null for no filter.</param>
    /// <param name="suggestedName">Suggested file name.</param>
    /// <returns>Selected path, or null if cancelled.</returns>
    public static async Task<string?> SaveFileDialog(Window owner, string? filter, string suggestedName)
    {
        var options = new FilePickerSaveOptions
        {
            FileTypeChoices = filter is null ? null : ParseFilter(filter),
            SuggestedFileName = suggestedName,
            ShowOverwritePrompt = true,
        };
        var file = await owner.StorageProvider.SaveFilePickerAsync(options);
        return file?.TryGetLocalPath();
    }

    /// <summary>
    /// Opens a folder picker.
    /// </summary>
    /// <returns>Selected folder path, or null if cancelled.</returns>
    public static async Task<string?> PickFolder(Window owner, string? title = null, string? startPath = null)
    {
        var provider = owner.StorageProvider;
        var options = new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = title,
            SuggestedStartLocation = await GetFolder(provider, startPath),
        };
        var folders = await provider.OpenFolderPickerAsync(options);
        if (folders.Count == 0)
            return null;
        return folders[0].TryGetLocalPath();
    }

    /// <summary>
    /// Gets the File Dialog filter for a Mystery Gift I/O operation.
    /// </summary>
    /// <param name="context">Context specifier for the </param>
    public static string GetMysterGiftFilter(EntityContext context) => context switch
    {
        EntityContext.Gen4 => "Gen4 Mystery Gift|*.pgt;*.pcd;*.wc4" + all,
        EntityContext.Gen5 => "Gen5 Mystery Gift|*.pgf;*.wc5full" + all,
        EntityContext.Gen6 => "Gen6 Mystery Gift|*.wc6;*.wc6full" + all,
        EntityContext.Gen7 => "Gen7 Mystery Gift|*.wc7;*.wc7full" + all,
        EntityContext.Gen8 => "Gen8 Mystery Gift|*.wc8" + all,
        EntityContext.Gen9 => "Gen9 Mystery Gift|*.wc9" + all,

        EntityContext.Gen7b => "Beluga Gift Record|*.wr7" + all,
        EntityContext.Gen8b => "BD/SP Gift|*.wb8" + all,
        EntityContext.Gen8a => "Legends: Arceus Gift|*.wa8" + all,
        EntityContext.Gen9a => "Legends: Z-A Gift|*.wa9" + all,
        _ => string.Empty,
    };

    private const string all = "|All Files|*.*";
}
