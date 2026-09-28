using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views.EntityEditors;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen7;

/// <summary>
/// Trainer editor for Let's Go Pikachu / Eevee (port of the WinForms <c>SAV_Trainer7GG</c>).
/// </summary>
/// <remarks>
/// The second tab manages the Go Park complex: 1000 transferred entities across areas of 50 slots, each
/// importable and exportable as a <c>.gp1</c> file.
/// </remarks>
public sealed class Trainer7GGWindow : SaveEditorWindow
{
    private const string GoFilter = "Go Park Entity|*.gp1";

    private readonly SaveFile Origin;
    private readonly SAV7b SAV;
    private readonly GoParkStorage Park;
    private bool MapUpdated;

    private readonly RenderedString TB_OTName = UiFactory.Name("TB_OTName", 12, 140);
    private readonly RenderedString TB_RivalName = UiFactory.Name("TB_RivalName", 12, 140);
    private readonly ComboBox CB_Gender = UiFactory.StringCombo("CB_Gender", 60);
    private readonly ComboBox CB_Game = UiFactory.Combo("CB_Game", 160);
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 140);
    private readonly TrainerIDView trainerID1 = new() { Name = "trainerID1" };
    private readonly NumericTextBox MT_Money = UiFactory.Numeric("MT_Money", 8, 110);
    private readonly Button B_MaxCash = UiFactory.Button("B_MaxCash", "+");
    private readonly NumericTextBox MT_Hours = UiFactory.Numeric("MT_Hours", 5, 60);
    private readonly NumericTextBox MT_Minutes = UiFactory.Numeric("MT_Minutes", 2, 44);
    private readonly NumericTextBox MT_Seconds = UiFactory.Numeric("MT_Seconds", 2, 44);

    private readonly DatePicker CAL_AdventureBeginDate = new() { Name = "CAL_AdventureBeginDate" };
    private readonly TimePicker CAL_AdventureBeginTime = new() { Name = "CAL_AdventureBeginTime" };
    private readonly DatePicker CAL_LastSavedDate = new() { Name = "CAL_LastSavedDate" };
    private readonly TimePicker CAL_LastSavedTime = new() { Name = "CAL_LastSavedTime" };

    private readonly NumericUpDown NUD_M = UiFactory.NumericUpDown("NUD_M", 0, uint.MaxValue, 110);
    private readonly NumericUpDown NUD_X = UiFactory.NumericUpDown("NUD_X", -99999999, 99999999, 100);
    private readonly NumericUpDown NUD_Z = UiFactory.NumericUpDown("NUD_Z", -99999999, 99999999, 100);
    private readonly NumericUpDown NUD_Y = UiFactory.NumericUpDown("NUD_Y", -99999999, 99999999, 100);
    private readonly NumericUpDown NUD_SX = UiFactory.NumericUpDown("NUD_SX", -99999999, 99999999, 100);
    private readonly NumericUpDown NUD_SZ = UiFactory.NumericUpDown("NUD_SZ", -99999999, 99999999, 100);
    private readonly NumericUpDown NUD_SY = UiFactory.NumericUpDown("NUD_SY", -99999999, 99999999, 100);
    private readonly NumericUpDown NUD_R = UiFactory.NumericUpDown("NUD_R", -99999999, 99999999, 100);
    private GroupBoxView GB_Map = null!;

    private readonly NumericUpDown NUD_GoIndex = UiFactory.NumericUpDown("NUD_GoIndex", 0, GoParkStorage.Count - 1, 120);
    private readonly TextBlock L_GoSlotSummary = UiFactory.Label("L_GoSlotSummary", "Summary");
    private readonly Button B_Import = UiFactory.Button("B_Import", "Import");
    private readonly Button B_Export = UiFactory.Button("B_Export", "Export");
    private readonly Button B_ImportGoFiles = UiFactory.Button("B_ImportGoFiles", "Import from Folder (start at current slot)");
    private readonly Button B_ExportGoFiles = UiFactory.Button("B_ExportGoFiles", "Export all to Folder");
    private readonly Button B_ExportGoSummary = UiFactory.Button("B_ExportGoSummary", "Dump Text Summary of Go Park Entities");
    private readonly Button B_DeleteGo = UiFactory.Button("B_DeleteGo", "Delete");
    private readonly Button B_DeleteAll = UiFactory.Button("B_DeleteAll", "Delete All");

    private readonly Button B_AllTrainerTitles = UiFactory.Button("B_AllTrainerTitles", "Unlock all Trainer Titles");
    private readonly Button B_AllFashionItems = UiFactory.Button("B_AllFashionItems", "Unlock all Fashion Items");

    public Trainer7GGWindow(SAV7b sav) : base("SAV_Trainer7GG", "Trainer Data Editor")
    {
        SAV = (SAV7b)(Origin = sav).Clone();

        if (!MainWindow.Unicode)
            TB_OTName.DisableInGameFont = TB_RivalName.DisableInGameFont = true;
        Park = SAV.Park;

        BuildLayout();
        GetComboBoxes();
        LoadTrainerInfo();
        UpdateGoSummary(0);

        B_MaxCash.Click += (_, _) => MT_Money.Text = "9999999";
        NUD_GoIndex.ValueChanged += (_, _) => UpdateGoSummary((int)(NUD_GoIndex.Value ?? 0));
        B_Import.Click += async (_, _) => await ImportSlot();
        B_Export.Click += async (_, _) => await ExportSlot();
        B_ImportGoFiles.Click += async (_, _) => await ImportFolder();
        B_ExportGoFiles.Click += async (_, _) => await ExportFolder();
        B_ExportGoSummary.Click += async (_, _) => await CopySummary();
        B_DeleteGo.Click += (_, _) => DeleteSlot();
        B_DeleteAll.Click += async (_, _) => await DeleteAll();
        B_AllTrainerTitles.Click += (_, _) => SAV.Blocks.EventWork.UnlockAllTitleFlags();
        B_AllFashionItems.Click += (_, _) =>
        {
            SAV.Blocks.FashionPlayer.UnlockAllAccessoriesPlayer();
            SAV.Blocks.FashionStarter.UnlockAllAccessoriesStarter();
        };
        foreach (var nud in new[] { NUD_M, NUD_X, NUD_Z, NUD_Y, NUD_SX, NUD_SZ, NUD_SY, NUD_R })
            nud.ValueChanged += (_, _) => MapUpdated = true;
        TB_OTName.AttachClick(async mods =>
        {
            if (mods == KeyModifiers.Control)
            {
                var trash = await TrashEditorWindow.ShowAsync(this, TB_OTName, SAV, SAV.Status.OriginalTrainerTrash.ToArray());
                trash?.CopyTo(SAV.Status.OriginalTrainerTrash); // WinForms writes the edited bytes back into the save
            }
        });
        TB_RivalName.AttachClick(async mods =>
        {
            if (mods == KeyModifiers.Control)
            {
                var trash = await TrashEditorWindow.ShowAsync(this, TB_RivalName, SAV, SAV.Misc.RivalNameTrash.ToArray());
                trash?.CopyTo(SAV.Misc.RivalNameTrash); // WinForms writes the edited bytes back into the save
            }
        });
    }

    private void BuildLayout()
    {
        var main = UiFactory.FormGrid(5);
        UiFactory.AddFormRow(main, 0, UiFactory.Label("L_TrainerName", "Trainer Name:"), UiFactory.Row(TB_OTName, CB_Gender, CB_Game));
        UiFactory.AddFormRow(main, 1, UiFactory.Label("L_RivalName", "Rival Name:"), TB_RivalName);
        UiFactory.AddFormRow(main, 2, null, trainerID1);
        UiFactory.AddFormRow(main, 3, UiFactory.Label("L_Money", "$:"), UiFactory.Row(MT_Money, B_MaxCash));
        UiFactory.AddFormRow(main, 4, UiFactory.Label("L_Language", "Language:"), CB_Language);

        var adventure = UiFactory.FormGrid(3);
        UiFactory.AddFormRow(adventure, 0, UiFactory.Label("L_Started", "Game Started:"), UiFactory.Row(CAL_AdventureBeginDate, CAL_AdventureBeginTime));
        UiFactory.AddFormRow(adventure, 1, UiFactory.Label("L_LastSaved", "Last Saved:"), UiFactory.Row(CAL_LastSavedDate, CAL_LastSavedTime));
        UiFactory.AddFormRow(adventure, 2, null, UiFactory.Row(
            UiFactory.Label("L_Hours", "Hrs:"), MT_Hours,
            UiFactory.Label("L_Minutes", "Min:"), MT_Minutes,
            UiFactory.Label("L_Seconds", "Sec:"), MT_Seconds));
        var gbAdventure = new GroupBoxView("GB_Adventure", "Adventure Info", adventure);

        var overview = UiFactory.Column(main, gbAdventure, UiFactory.Row(B_AllTrainerTitles, B_AllFashionItems));

        var map = UiFactory.FormGrid(8);
        UiFactory.AddFormRow(map, 0, UiFactory.Label("L_CurrentMap", "Current Map:"), NUD_M);
        UiFactory.AddFormRow(map, 1, UiFactory.Label("L_X", "X Coordinate:"), NUD_X);
        UiFactory.AddFormRow(map, 2, UiFactory.Label("L_Z", "Z Coordinate:"), NUD_Z);
        UiFactory.AddFormRow(map, 3, UiFactory.Label("L_Y", "Y Coordinate:"), NUD_Y);
        UiFactory.AddFormRow(map, 4, UiFactory.Label("L_SX", "X Scale:"), NUD_SX);
        UiFactory.AddFormRow(map, 5, UiFactory.Label("L_SZ", "Z Scale:"), NUD_SZ);
        UiFactory.AddFormRow(map, 6, UiFactory.Label("L_SY", "Y Scale:"), NUD_SY);
        UiFactory.AddFormRow(map, 7, UiFactory.Label("L_R", "Rotation:"), NUD_R);
        GB_Map = new GroupBoxView("GB_Map", "Map Position", map);

        L_GoSlotSummary.MinWidth = 380;
        var park = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_GoSlot", "Slot:"), NUD_GoIndex),
            L_GoSlotSummary,
            UiFactory.Row(B_Import, B_Export),
            UiFactory.Row(B_ImportGoFiles, B_ExportGoFiles),
            UiFactory.Row(B_ExportGoSummary, B_DeleteGo, B_DeleteAll));

        var tabs = new TabControl { Name = "TC_Editor" };
        tabs.Items.Add(new TabItem { Name = "Tab_Overview", Header = "Overview", Content = new ScrollViewer { Content = overview, MaxHeight = 540 } });
        tabs.Items.Add(new TabItem { Name = "Tab_BadgeMap", Header = "Map", Content = new ScrollViewer { Content = GB_Map, MaxHeight = 540 } });
        tabs.Items.Add(new TabItem { Name = "Tab_Complex", Header = "GO Complex", Content = park });
        SetBody(tabs);
    }

    #region Load

    private void GetComboBoxes()
    {
        CB_Gender.Items.Clear();
        foreach (var s in MainWindow.GenderSymbols.Take(2)) // m/f depending on unicode selection
            CB_Gender.Items.Add(s);
        CB_Language.SetItems(GameInfo.LanguageDataSource(SAV.Generation, SAV.Context));
        CB_Game.SetItems(GameInfo.Sources.VersionDataSource.Where(z => (GameVersion)z.Value is GameVersion.GP or GameVersion.GE).ToList());
    }

    private void LoadTrainerInfo()
    {
        TB_OTName.Text = SAV.OT;
        TB_RivalName.Text = SAV.Blocks.Misc.RivalName;
        CB_Language.SetValue(SAV.Language);
        MT_Money.Text = SAV.Blocks.Misc.Money.ToString();
        CB_Game.SetValue((int)SAV.Version);
        CB_Gender.SelectedIndex = SAV.Gender;
        trainerID1.LoadTrainer(SAV);

        NUD_M.SetValueClamped(SAV.Coordinates.M);
        try
        {
            NUD_X.SetValueClamped((decimal)(double)SAV.Coordinates.X);
            NUD_Z.SetValueClamped((decimal)(double)SAV.Coordinates.Z);
            NUD_Y.SetValueClamped((decimal)(double)SAV.Coordinates.Y);
            NUD_SX.SetValueClamped((decimal)(double)SAV.Coordinates.SX);
            NUD_SZ.SetValueClamped((decimal)(double)SAV.Coordinates.SZ);
            NUD_SY.SetValueClamped((decimal)(double)SAV.Coordinates.SY);
            NUD_R.SetValueClamped((decimal)(Math.Atan2(SAV.Coordinates.RZ, SAV.Coordinates.RW) * 360.0 / Math.PI));
        }
        catch (OverflowException)
        {
            GB_Map.IsEnabled = false;
        }
        MapUpdated = false;

        MT_Hours.Text = SAV.PlayedHours.ToString();
        MT_Minutes.Text = SAV.PlayedMinutes.ToString();
        MT_Seconds.Text = SAV.PlayedSeconds.ToString();

        var begin = SAV.PlayerGeoLocation.AdventureBegin.Timestamp;
        CAL_AdventureBeginDate.SelectedDate = UiFactory.ToOffset(begin);
        CAL_AdventureBeginTime.SelectedTime = begin.TimeOfDay;

        if (SAV.Played.LastSavedDate is { } d)
        {
            CAL_LastSavedDate.SelectedDate = UiFactory.ToOffset(d);
            CAL_LastSavedTime.SelectedTime = d.TimeOfDay;
        }
        else
        {
            CAL_LastSavedDate.IsEnabled = CAL_LastSavedTime.IsEnabled = false;
        }
    }

    #endregion

    #region Go Park

    private int CurrentSlot => Math.Clamp((int)(NUD_GoIndex.Value ?? 0), 0, GoParkStorage.Count - 1);

    private void UpdateGoSummary(int index)
    {
        index = Math.Clamp(index, 0, GoParkStorage.Count - 1);
        int area = index / GoParkStorage.SlotsPerArea;
        int slot = index % GoParkStorage.SlotsPerArea;

        var data = Park[index];
        var prefix = $"Area: {area + 1:00}, Slot: {slot + 1:00}{Environment.NewLine}";
        var dump = data.Species == 0 ? "Empty" : data.Dump(GameInfo.Strings.Species, index);
        L_GoSlotSummary.Text = prefix + dump;
    }

    private async Task ImportSlot()
    {
        var path = await FileDialogs.OpenSingleFile(this, GoFilter);
        if (path is null)
            return;
        await ImportGP1From(path, CurrentSlot);
    }

    private async Task ImportGP1From(string path, int index)
    {
        var data = await File.ReadAllBytesAsync(path);
        if (data.Length != GP1.SIZE)
        {
            await AppDialogs.Error(this, MessageStrings.MsgFileLoadIncompatible);
            return;
        }
        var gp1 = new GP1();
        data.CopyTo(gp1.Data);
        Park[index] = gp1;
        UpdateGoSummary(CurrentSlot);
    }

    private async Task ExportSlot()
    {
        var data = Park[CurrentSlot];
        var path = await FileDialogs.SaveFileDialog(this, GoFilter, data.FileName);
        if (path is null)
            return;
        await File.WriteAllBytesAsync(path, data.Data.ToArray());
    }

    private async Task ImportFolder()
    {
        var folder = await FileDialogs.PickFolder(this);
        if (folder is null)
            return;

        IEnumerable<string> files = Directory.GetFiles(folder);
        files = files.Where(z => Path.GetExtension(z) == ".gp1" && new FileInfo(z).Length == GP1.SIZE);

        int ctr = CurrentSlot;
        foreach (var f in files)
        {
            // Skip forward over occupied slots.
            while (ctr < GoParkStorage.Count && Park[ctr].Species != 0)
                ctr++;
            if (ctr >= GoParkStorage.Count)
                break;
            Park[ctr] = new GP1(await File.ReadAllBytesAsync(f));
            ctr++;
        }
        UpdateGoSummary(CurrentSlot);
    }

    private async Task ExportFolder()
    {
        var entities = Park.GetAllEntities().Where(z => z.Species != 0).ToArray();
        if (entities.Length == 0)
        {
            await AppDialogs.Alert(this, "No entities present in Go Park to dump.");
            return;
        }
        var folder = await FileDialogs.PickFolder(this);
        if (folder is null)
            return;
        foreach (var gpk in entities)
            await File.WriteAllBytesAsync(Path.Combine(folder, PathUtil.CleanFileName(gpk.FileName)), gpk.Data.ToArray());
        await AppDialogs.Alert(this, $"Dumped {entities.Length} files to {folder}");
    }

    private async Task CopySummary()
    {
        var summary = Park.DumpAll(GameInfo.Strings.Species).ToArray();
        if (summary.Length == 0)
        {
            await AppDialogs.Alert(this, "No entities present in Go Park to dump.");
            return;
        }
        await ClipboardService.SetText(this, string.Join(Environment.NewLine, summary));
    }

    private void DeleteSlot()
    {
        Park[CurrentSlot] = new GP1();
        UpdateGoSummary(CurrentSlot);
    }

    private async Task DeleteAll()
    {
        if (await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, "Delete all slots?") != DialogResult.Yes)
            return;
        Park.DeleteAll();
        UpdateGoSummary(CurrentSlot);
    }

    #endregion

    protected override void OnSave()
    {
        SAV.Version = (GameVersion)(CB_Game.GetSelectedItem()?.Value ?? (int)SAV.Version);
        SAV.Gender = (byte)Math.Max(0, CB_Gender.SelectedIndex);
        SAV.Money = Util.ToUInt32(MT_Money.Text ?? string.Empty);
        SAV.Language = CB_Language.GetSelectedItem()?.Value ?? 0;
        trainerID1.SaveTrainer(SAV);

        // only modify if changed, to preserve trash bytes
        if (SAV.OT != TB_OTName.Text)
            SAV.OT = TB_OTName.Text ?? string.Empty;
        if (SAV.Blocks.Misc.RivalName != TB_RivalName.Text)
            SAV.Blocks.Misc.RivalName = TB_RivalName.Text ?? string.Empty;

        if (GB_Map.IsEnabled && MapUpdated)
        {
            SAV.Coordinates.M = (ulong)(NUD_M.Value ?? 0);
            SAV.Coordinates.X = (float)(NUD_X.Value ?? 0);
            SAV.Coordinates.Z = (float)(NUD_Z.Value ?? 0);
            SAV.Coordinates.Y = (float)(NUD_Y.Value ?? 0);
            SAV.Coordinates.SX = (float)(NUD_SX.Value ?? 0);
            SAV.Coordinates.SZ = (float)(NUD_SZ.Value ?? 0);
            SAV.Coordinates.SY = (float)(NUD_SY.Value ?? 0);
            var angle = (double)(NUD_R.Value ?? 0) * Math.PI / 360.0;
            SAV.Coordinates.RX = 0;
            SAV.Coordinates.RZ = (float)Math.Sin(angle);
            SAV.Coordinates.RY = 0;
            SAV.Coordinates.RW = (float)Math.Cos(angle);
        }

        SAV.PlayedHours = (ushort)Util.ToUInt32(MT_Hours.Text ?? string.Empty);
        SAV.PlayedMinutes = (ushort)(Util.ToUInt32(MT_Minutes.Text ?? string.Empty) % 60);
        SAV.PlayedSeconds = (ushort)(Util.ToUInt32(MT_Seconds.Text ?? string.Empty) % 60);

        var begin = SAV.PlayerGeoLocation.AdventureBegin.Timestamp;
        SAV.PlayerGeoLocation.AdventureBegin.Timestamp =
            (CAL_AdventureBeginDate.SelectedDate?.Date ?? begin.Date) + (CAL_AdventureBeginTime.SelectedTime ?? begin.TimeOfDay);

        if (CAL_LastSavedDate.IsEnabled && SAV.Played.LastSavedDate is { } saved)
            SAV.Played.LastSavedDate = (CAL_LastSavedDate.SelectedDate?.Date ?? saved.Date) + (CAL_LastSavedTime.SelectedTime ?? saved.TimeOfDay);

        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
