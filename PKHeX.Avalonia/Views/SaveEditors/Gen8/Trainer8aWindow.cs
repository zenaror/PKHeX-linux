using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Views.EntityEditors;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen8;

/// <summary>
/// Trainer editor for Legends: Arceus (port of the WinForms <c>SAV_Trainer8a</c>).
/// </summary>
/// <remarks>
/// Besides the usual trainer data this holds the Galaxy Team rank, satchel upgrades and the merit point
/// counters, all stored as individual save blocks.
/// </remarks>
public sealed class Trainer8aWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV8LA SAV;
    private bool MapUpdated;

    private readonly TextBox TB_OTName = UiFactory.Text("TB_OTName", 12, 140);
    private readonly ComboBox CB_Gender = UiFactory.StringCombo("CB_Gender", 60);
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 140);
    private readonly TrainerIDView trainerID1 = new() { Name = "trainerID1" };
    private readonly NumericTextBox MT_Money = UiFactory.Numeric("MT_Money", 8, 110);
    private readonly Button B_MaxCash = UiFactory.Button("B_MaxCash", "Max");
    private readonly NumericTextBox MT_Hours = UiFactory.Numeric("MT_Hours", 5, 60);
    private readonly NumericTextBox MT_Minutes = UiFactory.Numeric("MT_Minutes", 2, 44);
    private readonly NumericTextBox MT_Seconds = UiFactory.Numeric("MT_Seconds", 2, 44);

    private readonly DatePicker CAL_AdventureStartDate = new() { Name = "CAL_AdventureStartDate" };
    private readonly TimePicker CAL_AdventureStartTime = new() { Name = "CAL_AdventureStartTime" };
    private readonly DatePicker CAL_LastSavedDate = new() { Name = "CAL_LastSavedDate" };
    private readonly TimePicker CAL_LastSavedTime = new() { Name = "CAL_LastSavedTime" };

    private readonly TextBox TB_M = UiFactory.Text("TB_M", 40, 200);
    private readonly NumericUpDown NUD_X = UiFactory.NumericUpDown("NUD_X", -100000, 100000, 110);
    private readonly NumericUpDown NUD_Z = UiFactory.NumericUpDown("NUD_Z", -100000, 100000, 110);
    private readonly NumericUpDown NUD_Y = UiFactory.NumericUpDown("NUD_Y", -100000, 100000, 110);
    private readonly NumericUpDown NUD_R = UiFactory.NumericUpDown("NUD_R", -360, 360, 110);
    private GroupBoxView GB_Map = null!;

    private readonly NumericUpDown NUD_MeritCurrent = UiFactory.NumericUpDown("NUD_MeritCurrent", 0, 999_999_999, 140);
    private readonly NumericUpDown NUD_MeritEarned = UiFactory.NumericUpDown("NUD_MeritEarned", 0, 999_999_999, 140);
    private readonly NumericUpDown NUD_Rank = UiFactory.NumericUpDown("NUD_Rank", 0, 999_999_999, 140);
    private readonly NumericUpDown NUD_Satchel = UiFactory.NumericUpDown("NUD_Satchel", 0, 999_999_999, 140);

    public Trainer8aWindow(SAV8LA sav) : base("SAV_Trainer8a", "Trainer Data Editor")
    {
        SAV = (SAV8LA)(Origin = sav).Clone();

        foreach (var s in GameInfo.GenderSymbolUnicode.Take(2))
            CB_Gender.Items.Add(s);

        BuildLayout();
        CB_Language.SetItems(GameInfo.LanguageDataSource(SAV.Generation, SAV.Context));
        GetTextBoxes();

        B_MaxCash.Click += (_, _) => MT_Money.Text = SAV.MaxMoney.ToString();
        foreach (var nud in new[] { NUD_X, NUD_Z, NUD_Y, NUD_R })
            nud.ValueChanged += (_, _) => MapUpdated = true;
        TB_M.OnTextChanged(_ => MapUpdated = true);
        TB_OTName.AttachClick(async mods =>
        {
            if (mods == KeyModifiers.Control)
                await TrashEditorWindow.ShowAsync(this, TB_OTName, SAV, SAV.MyStatus.OriginalTrainerTrash.ToArray());
        });
    }

    private void BuildLayout()
    {
        var main = UiFactory.FormGrid(6);
        UiFactory.AddFormRow(main, 0, UiFactory.Label("L_TrainerName", "Trainer Name:"), UiFactory.Row(TB_OTName, CB_Gender));
        UiFactory.AddFormRow(main, 1, UiFactory.Label("L_TrainerID", "Trainer ID:"), trainerID1);
        UiFactory.AddFormRow(main, 2, UiFactory.Label("L_Money", "Money:"), UiFactory.Row(MT_Money, B_MaxCash));
        UiFactory.AddFormRow(main, 3, UiFactory.Label("L_Language", "Language:"), CB_Language);
        UiFactory.AddFormRow(main, 4, UiFactory.Label("L_PlayTime", "Play Time:"), UiFactory.Row(MT_Hours, MT_Minutes, MT_Seconds));
        UiFactory.AddFormRow(main, 5, UiFactory.Label("L_LastSaved", "Last Saved:"), UiFactory.Row(CAL_LastSavedDate, CAL_LastSavedTime));

        var dates = UiFactory.FormGrid(1);
        UiFactory.AddFormRow(dates, 0, UiFactory.Label("L_AdventureStart", "Adventure Started:"), UiFactory.Row(CAL_AdventureStartDate, CAL_AdventureStartTime));

        var galaxy = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(galaxy, 0, UiFactory.Label("L_Rank", "Team Rank:"), NUD_Rank);
        UiFactory.AddFormRow(galaxy, 1, UiFactory.Label("L_Satchel", "Satchel Upgrades:"), NUD_Satchel);
        UiFactory.AddFormRow(galaxy, 2, UiFactory.Label("L_MeritCurrent", "Merit Points:"), NUD_MeritCurrent);
        UiFactory.AddFormRow(galaxy, 3, UiFactory.Label("L_MeritEarned", "Merit Earned:"), NUD_MeritEarned);

        GB_Map = new GroupBoxView("GB_Map", "Map Position", UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_M", "Map:"), TB_M),
            UiFactory.Row(UiFactory.Label("L_X", "X:"), NUD_X, UiFactory.Label("L_Z", "Z:"), NUD_Z),
            UiFactory.Row(UiFactory.Label("L_Y", "Y:"), NUD_Y, UiFactory.Label("L_R", "R:"), NUD_R)));

        SetBody(UiFactory.Column(main, dates, new GroupBoxView("GB_Galaxy", "Galaxy Team", galaxy), GB_Map));
    }

    private void GetTextBoxes()
    {
        CB_Gender.SelectedIndex = SAV.Gender;
        TB_OTName.Text = SAV.OT;
        trainerID1.LoadTrainer(SAV);
        MT_Money.Text = SAV.Money.ToString();
        CB_Language.SetValue(SAV.Language);

        TB_M.Text = SAV.Coordinates.M;
        try
        {
            NUD_X.SetValueClamped((decimal)(double)SAV.Coordinates.X);
            NUD_Z.SetValueClamped((decimal)(double)SAV.Coordinates.Z);
            NUD_Y.SetValueClamped((decimal)(double)SAV.Coordinates.Y);
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

        var saved = SAV.LastSaved.Timestamp;
        CAL_LastSavedDate.SelectedDate = UiFactory.ToOffset(saved);
        CAL_LastSavedTime.SelectedTime = saved.TimeOfDay;
        var start = SAV.AdventureStart.Timestamp;
        CAL_AdventureStartDate.SelectedDate = UiFactory.ToOffset(start);
        CAL_AdventureStartTime.SelectedTime = start.TimeOfDay;

        LoadClamp(NUD_MeritCurrent, SaveBlockAccessor8LA.KMeritCurrent);
        LoadClamp(NUD_MeritEarned, SaveBlockAccessor8LA.KMeritEarnedTotal);
        LoadClamp(NUD_Rank, SaveBlockAccessor8LA.KExpeditionTeamRank);
        LoadClamp(NUD_Satchel, SaveBlockAccessor8LA.KSatchelUpgrades);
    }

    private void LoadClamp(NumericUpDown nud, uint key) => nud.SetValueClamped((uint)SAV.Blocks.GetBlockValue(key));

    protected override void OnSave()
    {
        SAV.Gender = (byte)Math.Max(0, CB_Gender.SelectedIndex);
        SAV.Money = Util.ToUInt32(MT_Money.Text ?? string.Empty);
        SAV.Language = CB_Language.GetSelectedItem()?.Value ?? 0;
        trainerID1.SaveTrainer(SAV);

        if (SAV.OT != TB_OTName.Text) // only modify if changed, to preserve trash bytes
            SAV.OT = TB_OTName.Text ?? string.Empty;

        if (GB_Map.IsEnabled && MapUpdated)
        {
            SAV.Coordinates.M = TB_M.Text ?? string.Empty;
            SAV.Coordinates.X = (float)(NUD_X.Value ?? 0);
            SAV.Coordinates.Z = (float)(NUD_Z.Value ?? 0);
            SAV.Coordinates.Y = (float)(NUD_Y.Value ?? 0);
            var angle = (double)(NUD_R.Value ?? 0) * Math.PI / 360.0;
            SAV.Coordinates.RX = 0;
            SAV.Coordinates.RZ = (float)Math.Sin(angle);
            SAV.Coordinates.RY = 0;
            SAV.Coordinates.RW = (float)Math.Cos(angle);
        }

        SAV.PlayedHours = (ushort)Util.ToUInt32(MT_Hours.Text ?? string.Empty);
        SAV.PlayedMinutes = (ushort)(Util.ToUInt32(MT_Minutes.Text ?? string.Empty) % 60);
        SAV.PlayedSeconds = (ushort)(Util.ToUInt32(MT_Seconds.Text ?? string.Empty) % 60);

        SAV.AdventureStart.Timestamp = Combine(CAL_AdventureStartDate, CAL_AdventureStartTime, SAV.AdventureStart.Timestamp);
        SAV.LastSaved.Timestamp = Combine(CAL_LastSavedDate, CAL_LastSavedTime, SAV.LastSaved.Timestamp);

        SAV.Blocks.SetBlockValue(SaveBlockAccessor8LA.KMeritCurrent, (uint)(NUD_MeritCurrent.Value ?? 0));
        SAV.Blocks.SetBlockValue(SaveBlockAccessor8LA.KMeritEarnedTotal, (uint)(NUD_MeritEarned.Value ?? 0));
        SAV.Blocks.SetBlockValue(SaveBlockAccessor8LA.KExpeditionTeamRank, (uint)(NUD_Rank.Value ?? 0));
        SAV.Blocks.SetBlockValue(SaveBlockAccessor8LA.KSatchelUpgrades, (uint)(NUD_Satchel.Value ?? 0));

        Origin.CopyChangesFrom(SAV);
        Close();

        static DateTime Combine(DatePicker date, TimePicker time, DateTime fallback)
            => (date.SelectedDate?.Date ?? fallback.Date) + (time.SelectedTime ?? fallback.TimeOfDay);
    }
}
