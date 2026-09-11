using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors;

/// <summary>
/// Chatter recording editor (port of the WinForms <c>SAV_Chatter</c>).
/// </summary>
/// <remarks>Playback uses an external audio player (<c>paplay</c>, <c>aplay</c> or <c>ffplay</c>) instead of the Windows-only <c>SoundPlayer</c>.</remarks>
public sealed class ChatterWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SaveFile SAV;
    private readonly IChatter Chatter;

    private readonly CheckBox CHK_Initialized = UiFactory.Check("CHK_Initialized", "Initialized");
    private readonly TextBlock L_Confusion = UiFactory.Label("L_Confusion", "Confusion %:");
    private readonly TextBox MT_Confusion = UiFactory.Text("MT_Confusion", 3, 60);
    private readonly Button B_ImportPCM = UiFactory.Button("B_ImportPCM", "Import .pcm");
    private readonly Button B_ExportPCM = UiFactory.Button("B_ExportPCM", "Export .pcm");
    private readonly Button B_ExportWAV = UiFactory.Button("B_ExportWAV", "Export .wav");
    private readonly Button B_PlayRecording = UiFactory.Button("B_PlayRecording", "Play Recording");
    private string? PlaybackFile;

    public ChatterWindow(SaveFile sav) : base("SAV_Chatter", "Chatter Editor")
    {
        SAV = (Origin = sav).Clone();
        Chatter = SAV is SAV5 s5 ? s5.Chatter : ((SAV4)SAV).Chatter;

        MT_Confusion.IsReadOnly = true;
        var grid = UiFactory.FormGrid(2);
        UiFactory.AddFormRow(grid, 0, null, CHK_Initialized);
        UiFactory.AddFormRow(grid, 1, L_Confusion, MT_Confusion);
        var buttons = UiFactory.Column(UiFactory.Row(B_ImportPCM, B_ExportPCM, B_ExportWAV), B_PlayRecording);
        SetBody(UiFactory.Column(grid, buttons));

        CHK_Initialized.IsChecked = Chatter.Initialized;
        MT_Confusion.Text = Chatter.ConfusionChance.ToString();

        CHK_Initialized.IsCheckedChanged += (_, _) =>
        {
            Chatter.Initialized = CHK_Initialized.IsChecked == true;
            MT_Confusion.Text = Chatter.ConfusionChance.ToString();
        };
        B_ImportPCM.Click += async (_, _) => await ImportPCM();
        B_ExportPCM.Click += async (_, _) => await ExportPCM();
        B_ExportWAV.Click += async (_, _) => await ExportWAV();
        B_PlayRecording.Click += async (_, _) => await PlayRecording();
        Closed += (_, _) => DeletePlaybackFile();
    }

    protected override void OnSave()
    {
        Origin.CopyChangesFrom(SAV);
        Close();
    }

    private async Task ImportPCM()
    {
        var path = await FileDialogs.OpenSingleFile(this, "PCM File|*.pcm");
        if (path is null)
            return;

        var len = new FileInfo(path).Length;
        if (len != IChatter.SIZE_PCM)
        {
            await AppDialogs.Error(this, $"Incorrect size, got {len} bytes, expected {IChatter.SIZE_PCM} bytes.");
            return;
        }

        byte[] data = File.ReadAllBytes(path);
        data.CopyTo(Chatter.Recording);
        CHK_Initialized.IsChecked = Chatter.Initialized = true;
        MT_Confusion.Text = Chatter.ConfusionChance.ToString();
    }

    private async Task ExportPCM()
    {
        var path = await FileDialogs.SaveFileDialog(this, "PCM File|*.pcm", "Recording.pcm");
        if (path is null)
            return;
        File.WriteAllBytes(path, Chatter.Recording.ToArray());
    }

    private async Task ExportWAV()
    {
        var path = await FileDialogs.SaveFileDialog(this, "WAV File|*.wav", "Recording.wav");
        if (path is null)
            return;
        File.WriteAllBytes(path, ConvertPCMToWAV(Chatter.Recording));
    }

    private async Task PlayRecording()
    {
        if (!Chatter.Initialized && !Chatter.Recording.ContainsAnyExcept<byte>(0x00))
            return;

        var data = ConvertPCMToWAV(Chatter.Recording);
        PlaybackFile ??= Path.Combine(Path.GetTempPath(), $"pkhex-chatter-{Environment.ProcessId}.wav");
        try
        {
            File.WriteAllBytes(PlaybackFile, data);
        }
        catch (IOException ex)
        {
            await AppDialogs.Error(this, "Failed to write the recording.", ex.Message);
            return;
        }

        foreach (var player in (string[])["paplay", "aplay", "ffplay"])
        {
            var args = player == "ffplay" ? $"-nodisp -autoexit \"{PlaybackFile}\"" : $"\"{PlaybackFile}\"";
            try
            {
                using var proc = Process.Start(new ProcessStartInfo(player, args) { UseShellExecute = false, RedirectStandardError = true });
                if (proc is not null)
                    return;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                // player not installed; try the next one
            }
        }
        await AppDialogs.Alert(this, "No audio player found.", "Install paplay, aplay or ffplay to preview the recording, or export it to a .wav file.");
    }

    private void DeletePlaybackFile()
    {
        if (PlaybackFile is null || !File.Exists(PlaybackFile))
            return;
        try { File.Delete(PlaybackFile); }
        catch (IOException) { /* still in use by the player */ }
    }

    private static int GetWAVExpectedLength() => WAVHeader.Length + (IChatter.SIZE_PCM * 2);

    /// <summary>
    /// Size: 2x <see cref="IChatter.SIZE_PCM"/>
    /// </summary>
    private static ReadOnlySpan<byte> WAVHeader =>
    [
        // RIFF chunk
        0x52, 0x49, 0x46, 0x46, // chunk name: "RIFF"
        0xF4, 0x07, 0x00, 0x00, // chunk size: 2036
        0x57, 0x41, 0x56, 0x45, // format: "WAVE"

        // fmt subchunk
        0x66, 0x6D, 0x74, 0x20, // subchunk name: "fmt "
        0x10, 0x00, 0x00, 0x00, // subchunk size: 16
        0x01, 0x00,             // wFormatTag: WAVE_FORMAT_PCM (1)
        0x01, 0x00,             // nChannels: mono (1)
        0xD0, 0x07, 0x00, 0x00, // nSamplesPerSec: 2000
        0xD0, 0x07, 0x00, 0x00, // nAvgBytesPerSec: 2000
        0x01, 0x00,             // nBlockAlign: 1
        0x08, 0x00,             // wBitsPerSample: 8

        // data subchunk
        0x64, 0x61, 0x74, 0x61, // subchunk name: "data"
        0xD0, 0x07, 0x00, 0x00, // subchunk size: 2000
    ];

    /// <summary>
    /// Convert 4-bit PCM to 8-bit PCM and adds the WAV file header.
    /// </summary>
    private static byte[] ConvertPCMToWAV(ReadOnlySpan<byte> pcm)
    {
        byte[] data = new byte[GetWAVExpectedLength()];
        ConvertPCMToWAV(pcm, data);
        return data;
    }

    private static void ConvertPCMToWAV(ReadOnlySpan<byte> pcm, Span<byte> result)
    {
        WAVHeader.CopyTo(result);
        var i = WAVHeader.Length;
        foreach (byte b in pcm)
        {
            result[i++] = (byte)((b & 0x0F) << 4);
            result[i++] = (byte)(b & 0xF0);
        }
    }
}
