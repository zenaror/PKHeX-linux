using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using PKHeX.Core;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// File writing logic for entities and save files (port of the WinForms <c>WinFormsUtil</c> export helpers).
/// </summary>
public static class SaveExport
{
    public static void SavePKM(PKM pk, string path, ReadOnlySpan<char> pkx)
    {
        SaveBackup(path);
        var ext = Path.GetExtension(path);
        Span<byte> data = stackalloc byte[pk.SIZE_PARTY];
        if (ext == $".{pkx}")
            pk.WriteDecryptedDataParty(data);
        else
            pk.WriteEncryptedDataParty(data);
        File.WriteAllBytes(path, data);
    }

    public static void SaveBackup(string path)
    {
        if (!File.Exists(path))
            return;

        // File already exists, save a .bak
        string bakpath = $"{path}.bak";
        if (!File.Exists(bakpath))
            File.Move(path, bakpath);
    }

    public static async Task ExportSAV(Window? owner, SaveFile sav, string path)
    {
        var ext = Path.GetExtension(path.AsSpan());
        var flags = sav.Metadata.GetSuggestedFlags(ext);

        try
        {
            var data = sav.Write(flags).Span;
            ExportSAVInternal(data, path, sav.Metadata.FilePath);
            sav.State.Edited = false;
            sav.Metadata.SetExtraInfo(path);
        }
        catch (Exception x)
        {
            if (x is UnauthorizedAccessException or FileNotFoundException or IOException)
            {
                await AppDialogs.Error(owner, MsgFileWriteFail + Environment.NewLine + x.Message, MsgFileWriteProtectedAdvice);
                return;
            }
            throw; // Don't know what threw, but it wasn't I/O related.
        }
        await AppDialogs.Alert(owner, MsgSaveExportSuccessPath, path);
    }

    private static void ExportSAVInternal(ReadOnlySpan<byte> data, string path, string? exist)
    {
        // If it originated from a zip, and a zip is being written, update the zip.
        if (Path.GetExtension(path) is ".zip")
        {
            if (Path.Exists(exist) && Path.GetExtension(exist) is ".zip")
            {
                // If the paths are different, copy the original zip to the new location first.
                if (path != exist)
                    File.Copy(exist, path, true);

                ZipReader.Update(path, data);
                return;
            }
        }

        // Otherwise, just write the raw data.
        File.WriteAllBytes(path, data);
    }
}
