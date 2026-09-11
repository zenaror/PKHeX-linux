using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Views.EntityEditors;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen8;

/// <summary>
/// Trainer editor for Brilliant Diamond / Shining Pearl (port of the WinForms <c>SAV_Trainer8b</c>).
/// </summary>
/// <remarks>
/// Badges live in the system flag block rather than in a dedicated field, and the play timestamps keep their
/// sub-second ticks so a save edited here still matches what the game wrote.
/// </remarks>
public sealed class Trainer8bWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV8BS SAV;
    private readonly bool Loading;
    private bool MapUpdated;

    private readonly TextBox TB_OTName = UiFactory.Text("TB_OTName", 12, 140);
    private readonly TextBox TB_Rival = UiFactory.Text("TB_Rival", 12, 140);
    private readonly ComboBox CB_Gender = UiFactory.StringCombo("CB_Gender", 60);
    private readonly ComboBox CB_Game = UiFactory.StringCombo("CB_Game", 120);
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 140);
    private readonly TrainerIDView trainerID1 = new() { Name = "trainerID1" };
    private readonly NumericTextBox MT_Money = UiFactory.Numeric("MT_Money", 8, 110);
    private readonly Button B_MaxCash = UiFactory.Button("B_MaxCash", "Max");
    private readonly NumericUpDown NUD_BP = UiFactory.NumericUpDown("NUD_BP", 0, uint.MaxValue, 120);
    private readonly NumericTextBox MT_Hours = UiFactory.Numeric("MT_Hours", 5, 60);
    private readonly NumericTextBox MT_Minutes = UiFactory.Numeric("MT_Minutes", 2, 44);
    private readonly NumericTextBox MT_Seconds = UiFactory.Numeric("MT_Seconds", 2, 44);
    private readonly CheckBox[] Badges;

    private readonly DatePicker CAL_AdventureStartDate = new() { Name = "CAL_AdventureStartDate" };
    private readonly TimePicker CAL_AdventureStartTime = new() { Name = "CAL_AdventureStartTime" };
    private readonly DatePicker CAL_LastSavedDate = new() { Name = "CAL_LastSavedDate" };
    private readonly TimePicker CAL_LastSavedTime = new() { Name = "CAL_LastSavedTime" };

    private readonly NumericUpDown NUD_M = UiFactory.NumericUpDown("NUD_M", short.MinValue, short.MaxValue, 110);
    private readonly NumericUpDown NUD_X = UiFactory.NumericUpDown("NUD_X", int.MinValue, int.MaxValue, 110);
    private readonly NumericUpDown NUD_Z = UiFactory.NumericUpDown("NUD_Z", -100000, 100000, 110);
    private readonly NumericUpDown NUD_Y = UiFactory.NumericUpDown("NUD_Y", int.MinValue, int.MaxValue, 110);
    private readonly NumericUpDown NUD_R = UiFactory.NumericUpDown("NUD_R", -360, 360, 110);
    private GroupBoxView GB_Map = null!;

    private readonly TrainerStatView TrainerStats = new();

    public Trainer8bWindow(SAV8BS sav) : base("SAV_Trainer8b", "Trainer Data Editor")
    {
        SAV = (SAV8BS)(Origin = sav).Clone();
        Loading = true;

        Badges = [.. Enumerable.Range(1, 8).Select(i => UiFactory.Check($"CHK_Badge{i}", i.ToString()))];
        foreach (var s in GameInfo.GenderSymbolUnicode.Take(2))
            CB_Gender.Items.Add(s);
        foreach (var v in new[] { GameVersion.BD, GameVersion.SP })
            CB_Game.Items.Add(v.ToString());

        BuildLayout();
        GetComboBoxes();
        GetTextBoxes();

        // after SetBody: translation would otherwise overwrite the computed offset label
        TrainerStats.LoadRecords(SAV, Record8b.RecordList_8b);
        Loading = false;

        B_MaxCash.Click += (_, _) => MT_Money.Text = SAV.MaxMoney.ToString();
        foreach (var nud in new[] { NUD_M, NUD_X, NUD_Z, NUD_Y, NUD_R })
            nud.ValueChanged += (_, _) => { if (!Loading) MapUpdated = true; };
        TB_OTName.AttachClick(async mods =>
        {
            if (mods == KeyModifiers.Control)
                await TrashEditorWindow.ShowAsync(this, TB_OTName, SAV, SAV.MyStatus.OriginalTrainerTrash.ToArray());
        });
        TB_Rival.AttachClick(async mods =>
        {
            if (mods == KeyModifiers.Control)
                await TrashEditorWindow.ShowAsync(this, TB_Rival, SAV, SAV.RivalNameTrash.ToArray());
        });
    }

    private void BuildLayout()
    {
        var main = UiFactory.FormGrid(8);
        UiFactory.AddFormRow(main, 0, UiFactory.Label("L_TrainerName", "Trainer Name:"), UiFactory.Row(TB_OTName, CB_Gender));
        UiFactory.AddFormRow(main, 1, UiFactory.Label("L_Rival", "Rival Name:"), TB_Rival);
        UiFactory.AddFormRow(main, 2, UiFactory.Label("L_Game", "Game:"), CB_Game);
        UiFactory.AddFormRow(main, 3, UiFactory.Label("L_TrainerID", "Trainer ID:"), trainerID1);
        UiFactory.AddFormRow(main, 4, UiFactory.Label("L_Money", "Money:"), UiFactory.Row(MT_Money, B_MaxCash));
        UiFactory.AddFormRow(main, 5, UiFactory.Label("L_Language", "Language:"), CB_Language);
        UiFactory.AddFormRow(main, 6, UiFactory.Label("L_BP", "Battle Points:"), NUD_BP);
        UiFactory.AddFormRow(main, 7, UiFactory.Label("L_PlayTime", "Play Time:"), UiFactory.Row(MT_Hours, MT_Minutes, MT_Seconds));

        var dates = UiFactory.FormGrid(2);
        UiFactory.AddFormRow(dates, 0, UiFactory.Label("L_AdventureStart", "Adventure Started:"), UiFactory.Row(CAL_AdventureStartDate, CAL_AdventureStartTime));
        UiFactory.AddFormRow(dates, 1, UiFactory.Label("L_LastSaved", "Last Saved:"), UiFactory.Row(CAL_LastSavedDate, CAL_LastSavedTime));

        GB_Map = new GroupBoxView("GB_Map", "Map Position", UiFactory.Row(
            UiFactory.Label("L_M", "Zone:"), NUD_M,
            UiFactory.Label("L_X", "X:"), NUD_X,
            UiFactory.Label("L_Z", "Height:"), NUD_Z,
            UiFactory.Label("L_Y", "Y:"), NUD_Y,
            UiFactory.Label("L_R", "R:"), NUD_R));

        var overview = UiFactory.Column(main, new GroupBoxView("GB_Badges", "Badges", UiFactory.Row(Badges)), dates, GB_Map);

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Name = "Tab_Overview", Header = "Overview", Content = new ScrollViewer { Content = overview, MaxHeight = 540 } });
        tabs.Items.Add(new TabItem { Name = "Tab_Records", Header = "Records", Content = TrainerStats });
        SetBody(tabs);
    }

    private void GetComboBoxes() => CB_Language.SetItems(GameInfo.LanguageDataSource(SAV.Generation, SAV.Context));

    private void GetTextBoxes()
    {
        CB_Game.SelectedIndex = Math.Clamp((byte)SAV.Version - (byte)GameVersion.BD, 0, 1);
        CB_Gender.SelectedIndex = SAV.Gender;
        NUD_BP.SetValueClamped(SAV.BattleTower.BP);

        TB_OTName.Text = SAV.OT;
        trainerID1.LoadTrainer(SAV);
        MT_Money.Text = SAV.Money.ToString();
        CB_Language.SetValue(SAV.Language);
        TB_Rival.Text = SAV.RivalName;

        NUD_M.SetValueClamped(SAV.ZoneID);
        NUD_X.SetValueClamped(SAV.MyStatus.X);
        NUD_Z.SetValueClamped((decimal)SAV.MyStatus.Height);
        NUD_Y.SetValueClamped(SAV.MyStatus.Y);
        NUD_R.SetValueClamped((decimal)SAV.MyStatus.Rotation);

        MT_Hours.Text = SAV.PlayedHours.ToString();
        MT_Minutes.Text = SAV.PlayedMinutes.ToString();
        MT_Seconds.Text = SAV.PlayedSeconds.ToString();

        var latest = SAV.System.LocalTimestampLatest;
        CAL_LastSavedDate.SelectedDate = UiFactory.ToOffset(latest);
        CAL_LastSavedTime.SelectedTime = latest.TimeOfDay;

        var start = SAV.System.LocalTimestampStart;
        CAL_AdventureStartDate.SelectedDate = UiFactory.ToOffset(start);
        CAL_AdventureStartTime.SelectedTime = start.TimeOfDay;

        for (int i = 0; i < Badges.Length; i++)
            Badges[i].IsChecked = SAV.FlagWork.GetSystemFlag(124 + i);
    }

    private void SaveTrainerInfo()
    {
        SAV.Version = (GameVersion)(CB_Game.SelectedIndex + (int)GameVersion.BD);
        SAV.Gender = (byte)Math.Max(0, CB_Gender.SelectedIndex);
        SAV.Money = Util.ToUInt32(MT_Money.Text ?? string.Empty);
        SAV.Language = CB_Language.GetSelectedItem()?.Value ?? 0;
        trainerID1.SaveTrainer(SAV);

        // only modify if changed, to preserve trash bytes
        if (SAV.OT != TB_OTName.Text)
            SAV.OT = TB_OTName.Text ?? string.Empty;
        if (SAV.RivalName != TB_Rival.Text)
            SAV.RivalName = TB_Rival.Text ?? string.Empty;

        SAV.BattleTower.BP = (uint)(NUD_BP.Value ?? 0);

        if (GB_Map.IsEnabled && MapUpdated)
        {
            SAV.ZoneID = (short)(NUD_M.Value ?? 0);
            SAV.MyStatus.X = (int)(NUD_X.Value ?? 0);
            SAV.MyStatus.Height = (float)(NUD_Z.Value ?? 0);
            SAV.MyStatus.Y = (int)(NUD_Y.Value ?? 0);
            SAV.MyStatus.Rotation = (float)(NUD_R.Value ?? 0);
        }

        SAV.PlayedHours = (ushort)Util.ToUInt32(MT_Hours.Text ?? string.Empty);
        SAV.PlayedMinutes = (ushort)(Util.ToUInt32(MT_Minutes.Text ?? string.Empty) % 60);
        SAV.PlayedSeconds = (ushort)(Util.ToUInt32(MT_Seconds.Text ?? string.Empty) % 60);

        SAV.System.LocalTimestampStart = ReviseTimestamp(SAV.System.LocalTimestampStart, CAL_AdventureStartDate, CAL_AdventureStartTime);
        SAV.System.LocalTimestampLatest = ReviseTimestamp(SAV.System.LocalTimestampLatest, CAL_LastSavedDate, CAL_LastSavedTime);

        for (int i = 0; i < Badges.Length; i++)
            SAV.FlagWork.SetSystemFlag(124 + i, Badges[i].IsChecked == true);
    }

    /// <summary>Rebuilds a timestamp from the pickers while keeping the original sub-second ticks.</summary>
    private static DateTime ReviseTimestamp(DateTime original, DatePicker date, TimePicker time)
    {
        var revised = (date.SelectedDate?.Date ?? original.Date) + (time.SelectedTime ?? original.TimeOfDay);
        return revised.AddTicks(original.Ticks % TimeSpan.TicksPerSecond);
    }

    protected override void OnSave()
    {
        SaveTrainerInfo();
        if (SAV is { TID16: 0, SID16: 0 })
            SAV.SID16 = 1; // an all-zero ID is not valid

        // Trickle the changes down to the extra record block.
        if (SAV.HasFirstSaveFileExpansion && (SAV.OT != Origin.OT || SAV.TID16 != ((SAV8BS)Origin).TID16 || SAV.SID16 != ((SAV8BS)Origin).SID16))
            SAV.RecordAdd.ReplaceOT(Origin, SAV);

        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
