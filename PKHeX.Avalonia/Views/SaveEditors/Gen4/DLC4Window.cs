using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4;

/// <summary>
/// Battle Video viewer/importer for Generation 4 save files (port of the WinForms <c>SAV_DLC4</c>).
/// </summary>
public sealed class DLC4Window : SaveEditorWindow
{
    private const int BattleVideoCount = 4;

    private readonly SAV4 Origin;
    private readonly SAV4 SAV;

    private readonly ObservableCollection<string> Slots = [];
    private readonly ListBox LB_BattleVideos = new() { Name = "LB_BattleVideos", Width = 190, Height = 120 };
    private readonly TextBlock L_VideoTitle = UiFactory.Label("L_VideoTitle", "Battle Video Title");
    private readonly TextBlock L_VideoStatus = UiFactory.Label("L_VideoStatus", "Select a Battle Video.");
    private readonly StackPanel TL_Teams = new() { Orientation = Orientation.Vertical, Spacing = 6 };
    private readonly CheckBox CHK_ExportDecrypted = UiFactory.Check("CHK_ExportDecrypted", "Force decrypted export");
    private readonly Button B_Import = UiFactory.Button("B_Import", "Import");
    private readonly Button B_Export = UiFactory.Button("B_Export", "Export");

    public DLC4Window(SAV4 sav) : base("SAV_DLC4", "Battle Videos")
    {
        Origin = sav;
        SAV = (SAV4)sav.Clone();

        L_VideoTitle.FontWeight = FontWeight.Bold;
        LB_BattleVideos.ItemsSource = Slots;

        var slots = UiFactory.Column(UiFactory.Label("L_Slots", "Battle Videos"), LB_BattleVideos);
        var teams = new ScrollViewer { Content = TL_Teams, MaxHeight = 400, MinWidth = 480, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var actions = UiFactory.Row(CHK_ExportDecrypted, B_Import, B_Export);
        actions.Spacing = 8;
        var detail = UiFactory.Column(L_VideoTitle, L_VideoStatus, teams, actions);
        SetBody(UiFactory.Row(slots, detail));

        LB_BattleVideos.SelectionChanged += (_, _) => LoadSelectedBattleVideo();
        B_Import.Click += async (_, _) => await ClickImport();
        B_Export.Click += async (_, _) => await ClickExport();

        LoadBattleVideos();
        if (Slots.Count != 0)
            LB_BattleVideos.SelectedIndex = 0;
    }

    private void LoadBattleVideos()
    {
        var index = LB_BattleVideos.SelectedIndex;
        Slots.Clear();
        for (int i = 0; i < BattleVideoCount; i++)
        {
            var video = SAV.GetBattleVideo(i);
            Slots.Add(video is null ? $"{i + 1:00} - N/A" : $"{i + 1:00} - {video.GetName()}");
        }
        if ((uint)index < Slots.Count)
            LB_BattleVideos.SelectedIndex = index;
    }

    private void LoadSelectedBattleVideo()
    {
        TL_Teams.Children.Clear();

        int index = LB_BattleVideos.SelectedIndex;
        if ((uint)index >= BattleVideoCount)
        {
            L_VideoTitle.Text = MsgBattleVideo;
            L_VideoStatus.Text = MsgBattleVideoInvalidIndex;
            SetBattleVideoControlsEnabled(false);
            return;
        }

        var video = SAV.GetBattleVideo(index);
        if (video is null)
        {
            L_VideoTitle.Text = string.Format(MsgBattleVideoIndex, index + 1);
            L_VideoStatus.Text = MsgBattleVideoInvalidSlot;
            SetBattleVideoControlsEnabled(false);
            return;
        }

        SetBattleVideoControlsEnabled(true);

        var names = video.GetTrainerNames();
        var teams = video.GetTeams();
        var activePlayers = Math.Max(2, teams.Count(z => z.Length != 0));

        L_VideoTitle.Text = string.Format(MsgBattleVideoIndex, index + 1);
        L_VideoStatus.Text = GetStatusText(video, names, teams);

        // WinForms lays the players out two per row; a wrap panel gives the same shape without a fixed table.
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = 520 };
        for (int trainer = 0; trainer < BattleVideo4.TrainerCount; trainer++)
        {
            var team = teams[trainer];
            if (team.Length == 0 && trainer >= activePlayers)
                continue;

            var name = names[trainer];
            if (string.IsNullOrWhiteSpace(name))
                name = team.FirstOrDefault()?.OriginalTrainerName;
            if (string.IsNullOrWhiteSpace(name))
                name = "Unknown Player";

            wrap.Children.Add(CreatePlayerPanel(trainer + 1, name, team));
        }
        TL_Teams.Children.Add(wrap);
    }

    private static string GetStatusText(BattleVideo4 video, IReadOnlyList<string> names, PK4[][] teams)
    {
        var members = teams.Sum(z => z.Length);
        var state = video.IsDecrypted ? "Decrypted view" : "Encrypted";
        var checksum = video.ChecksumValid ? "checksum valid" : "checksum invalid";
        var players = names.Count(z => !string.IsNullOrWhiteSpace(z)).ToString();
        return $"{state} • {players}/4 players • {members} Pokémon - {checksum} - Block ID 0x{video.BlockID:X4}";
    }

    private static Control CreatePlayerPanel(int playerIndex, string playerName, ReadOnlySpan<PK4> team)
    {
        var table = new Grid { ColumnSpacing = 6, RowSpacing = 2, Margin = new Thickness(0, 2, 0, 0) };
        table.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(26)));
        table.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        table.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        table.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(44)));
        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        AddCell(table, "#", 0, 0, bold: true);
        AddCell(table, "Pokémon", 0, 1, bold: true);
        AddCell(table, "Nickname", 0, 2, bold: true);
        AddCell(table, "Level", 0, 3, bold: true);

        if (team.Length == 0)
        {
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddCell(table, "-", 1, 0);
        }
        for (int i = 0; i < team.Length; i++)
        {
            var pk = team[i];
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddCell(table, (i + 1).ToString(), i + 1, 0, align: HorizontalAlignment.Center);
            AddCell(table, GetSpeciesName(pk.Species), i + 1, 1);
            AddCell(table, string.IsNullOrWhiteSpace(pk.Nickname) ? "-" : pk.Nickname, i + 1, 2);
            AddCell(table, pk.CurrentLevel.ToString(), i + 1, 3, align: HorizontalAlignment.Center);
        }

        var group = new GroupBoxView($"GB_Player{playerIndex}", string.Format(MsgBattleVideoPlayerIndex, playerIndex, playerName), table)
        {
            Margin = new Thickness(0, 0, 6, 6),
            MinWidth = 250,
        };
        return group;
    }

    private static string GetSpeciesName(ushort species)
    {
        if (species < GameInfo.Strings.Species.Count)
            return GameInfo.Strings.Species[species];
        return $"{species:000}";
    }

    private static void AddCell(Grid table, string text, int row, int column, bool bold = false, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new TextBlock
        {
            Text = text,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            HorizontalAlignment = align,
            VerticalAlignment = VerticalAlignment.Center,
        };
        UiFactory.SetRowCol(label, row, column);
        table.Children.Add(label);
    }

    private void SetBattleVideoControlsEnabled(bool enabled)
    {
        B_Import.IsEnabled = enabled;
        B_Export.IsEnabled = enabled;
        CHK_ExportDecrypted.IsEnabled = enabled;
    }

    private static string Filter => $"{MsgBattleVideo}|*.{BattleVideo4.Extension}";

    private async Task ClickImport()
    {
        int index = LB_BattleVideos.SelectedIndex;
        if ((uint)index >= BattleVideoCount)
            return;

        var target = SAV.GetBattleVideo(index);
        if (target is null)
        {
            await AppDialogs.Error(this, MsgBattleVideoInvalidSlot);
            return;
        }

        var path = await FileDialogs.OpenSingleFile(this, Filter);
        if (path is null)
            return;

        try
        {
            var data = File.ReadAllBytes(path);
            if (!BattleVideo4.IsValid(data))
            {
                await AppDialogs.Error(this, string.Format(MsgBattleVideoInvalidSize, BattleVideo4.SIZE_USED));
                return;
            }

            var encryptionState = BattleVideo4.DetectEncryption(data);
            if (encryptionState == BattleVideo4DecryptionState.Invalid)
            {
                await AppDialogs.Error(this, MsgBattleVideoInvalid);
                return;
            }

            var imported = new BattleVideo4(data)
            {
                IsDecrypted = encryptionState == BattleVideo4DecryptionState.Decrypted,
                // The slot owns its block metadata. Preserve the destination's magic/revision/id while replacing the actual Battle Video payload.
                Magic = target.Magic,
                Revision = target.Revision,
                BlockSize = target.BlockSize,
                BlockID = target.BlockID,
            };

            imported.RefreshChecksums();
            imported.Encrypt();

            imported.Data.CopyTo(target.Data);

            LoadBattleVideos();
            LB_BattleVideos.SelectedIndex = index;
            LoadSelectedBattleVideo();
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(this, ex.Message, ex);
        }
    }

    private async Task ClickExport()
    {
        int index = LB_BattleVideos.SelectedIndex;
        if ((uint)index >= BattleVideoCount)
            return;

        var video = SAV.GetBattleVideo(index);
        if (video is null)
            return;

        var data = video.Data.ToArray();
        var suggestion = $"{PathUtil.CleanFileName(video.ToString())}.{BattleVideo4.Extension}";
        var path = await FileDialogs.SaveFileDialog(this, Filter, suggestion);
        if (path is null)
            return;

        if (CHK_ExportDecrypted.IsChecked == true)
        {
            var export = new BattleVideo4(data) { IsDecrypted = false };
            export.Decrypt();
        }

        File.WriteAllBytes(path, data);
    }

    protected override void OnSave()
    {
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
