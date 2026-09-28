using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
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

    private readonly RenderedString TB_OTName = UiFactory.Name("TB_OTName", 12, 140);
    private readonly RenderedString TB_Rival = UiFactory.Name("TB_Rival", 12, 140);
    private readonly ComboBox CB_Gender = UiFactory.StringCombo("CB_Gender", 60);
    private readonly ComboBox CB_Game = UiFactory.StringCombo("CB_Game", 120);
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 140);
    private readonly TrainerIDView trainerID1 = new() { Name = "trainerID1" };
    private readonly NumericTextBox MT_Money = UiFactory.Numeric("MT_Money", 8, 110);
    private readonly Button B_MaxCash = UiFactory.Button("B_MaxCash", "+");
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

        if (!MainWindow.Unicode)
            TB_OTName.DisableInGameFont = TB_Rival.DisableInGameFont = true;
        Loading = true;

        Badges = [.. Enumerable.Range(1, 8).Select(i => UiFactory.Check($"CHK_Badge{i}", i.ToString()))];
        foreach (var s in MainWindow.GenderSymbols.Take(2)) // m/f depending on unicode selection
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
            {
                var trash = await TrashEditorWindow.ShowAsync(this, TB_OTName, SAV, SAV.MyStatus.OriginalTrainerTrash.ToArray());
                trash?.CopyTo(SAV.MyStatus.OriginalTrainerTrash); // WinForms writes the edited bytes back into the save
            }
        });
        TB_Rival.AttachClick(async mods =>
        {
            if (mods == KeyModifiers.Control)
            {
                var trash = await TrashEditorWindow.ShowAsync(this, TB_Rival, SAV, SAV.RivalNameTrash.ToArray());
                trash?.CopyTo(SAV.RivalNameTrash); // WinForms writes the edited bytes back into the save
            }
        });
    }

    private void BuildLayout()
    {
        // Overview: trainer details on the left, the Stats and Adventure groups on the right
        // (SAV_Trainer8b.Designer.cs: Tab_Overview).
        var main = UiFactory.FormGrid(5);
        UiFactory.AddFormRow(main, 0, UiFactory.Label("L_TrainerName", "Trainer Name:"),
            UiFactory.Row(TB_OTName, UiFactory.Label("L_RivalName", "Rival Name:"), TB_Rival)); // both names share a line upstream
        UiFactory.AddFormRow(main, 1, trainerID1, UiFactory.Row());
        UiFactory.AddFormRow(main, 2, UiFactory.Label("L_Money", "$:"), UiFactory.Row(MT_Money, B_MaxCash));
        UiFactory.AddFormRow(main, 3, null, UiFactory.Row(CB_Gender, CB_Game));
        UiFactory.AddFormRow(main, 4, UiFactory.Label("L_Language", "Language:"), CB_Language);

        var adventure = UiFactory.FormGrid(5);
        UiFactory.AddFormRow(adventure, 0, UiFactory.Label("L_Started", "Game Started:"), UiFactory.Row(CAL_AdventureStartDate, CAL_AdventureStartTime));
        UiFactory.AddFormRow(adventure, 1, UiFactory.Label("L_LastSaved", "Last Saved:"), UiFactory.Row(CAL_LastSavedDate, CAL_LastSavedTime));
        UiFactory.AddFormRow(adventure, 2, UiFactory.Label("L_Hours", "Hrs:"), MT_Hours);
        UiFactory.AddFormRow(adventure, 3, UiFactory.Label("L_Minutes", "Min:"), MT_Minutes);
        UiFactory.AddFormRow(adventure, 4, UiFactory.Label("L_Seconds", "Sec:"), MT_Seconds);
        // WinForms keeps the Hall of Fame pickers hidden (SAV_Trainer8b.cs:76), so they are not built here.

        var stats = UiFactory.Column(UiFactory.Row(UiFactory.Label("L_BP", "BP:"), NUD_BP), TrainerStats);

        var left = UiFactory.Column(main, new GroupBoxView("GB_Adventure", "Adventure Info", adventure));
        var right = new GroupBoxView("GB_Stats", "Stats", stats);
        left.VerticalAlignment = right.VerticalAlignment = VerticalAlignment.Top;
        var overview = UiFactory.Row(left, right);
        overview.Spacing = 10;

        // Map tab: the badges and the map position group, as in WinForms.
        var map = UiFactory.FormGrid(5);
        UiFactory.AddFormRow(map, 0, UiFactory.Label("L_CurrentMap", "Zone ID:"), NUD_M);
        UiFactory.AddFormRow(map, 1, UiFactory.Label("L_X", "X Coordinate:"), NUD_X);
        UiFactory.AddFormRow(map, 2, UiFactory.Label("L_Y", "Y Coordinate:"), NUD_Y);
        UiFactory.AddFormRow(map, 3, UiFactory.Label("L_Height", "Height:"), NUD_Z);
        UiFactory.AddFormRow(map, 4, UiFactory.Label("L_Rotation", "Rotation:"), NUD_R);
        GB_Map = new GroupBoxView("GB_Map", "Map Position", map);
        GB_Map.HorizontalAlignment = HorizontalAlignment.Left;
        var badgeMap = UiFactory.Column(UiFactory.Row(Badges), GB_Map);

        var tabs = new TabControl { Name = "TC_Editor" };
        tabs.Items.Add(new TabItem { Name = "Tab_Overview", Header = "Overview", Content = new ScrollViewer { Content = overview, MaxHeight = 540 } });
        tabs.Items.Add(new TabItem { Name = "Tab_BadgeMap", Header = "Map", Content = new ScrollViewer { Content = badgeMap, MaxHeight = 540 } });
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
