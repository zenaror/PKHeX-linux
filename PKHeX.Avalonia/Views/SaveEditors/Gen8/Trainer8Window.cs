using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views.EntityEditors;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen8;

/// <summary>
/// Trainer editor for Sword / Shield (port of the WinForms <c>SAV_Trainer8</c>).
/// </summary>
/// <remarks>
/// Covers the trainer card (its own name, number and Roto Rally score), Watts, Battle Tower streaks,
/// the party shown on the card and title screen, the fashion unlocks and the Diglett hunt.
/// </remarks>
public sealed class Trainer8Window : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV8SWSH SAV;
    private bool MapUpdated;

    private readonly TextBox TB_OTName = UiFactory.Text("TB_OTName", 12, 140);
    private readonly TextBox TB_TrainerCardName = UiFactory.Text("TB_TrainerCardName", 12, 140);
    private readonly TextBox TB_TrainerCardNumber = UiFactory.Text("TB_TrainerCardNumber", 8, 120);
    private readonly NumericTextBox MT_TrainerCardID = UiFactory.Numeric("MT_TrainerCardID", 6, 90);
    private readonly NumericTextBox MT_RotoRally = UiFactory.Numeric("MT_RotoRally", 7, 100);
    private readonly ComboBox CB_Gender = UiFactory.StringCombo("CB_Gender", 60);
    private readonly ComboBox CB_Game = UiFactory.StringCombo("CB_Game", 120);
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 140);
    private readonly ComboBox CB_SkinColor = UiFactory.StringCombo("CB_SkinColor", 160);
    private readonly TrainerIDView trainerID1 = new() { Name = "trainerID1" };
    private readonly NumericTextBox MT_Money = UiFactory.Numeric("MT_Money", 8, 110);
    private readonly Button B_MaxCash = UiFactory.Button("B_MaxCash", "Max");
    private readonly NumericTextBox MT_Watt = UiFactory.Numeric("MT_Watt", 8, 110);
    private readonly Button B_MaxWatt = UiFactory.Button("B_MaxWatt", "Max");
    private readonly NumericUpDown NUD_BP = UiFactory.NumericUpDown("NUD_BP", 0, 9999, 110);
    private readonly NumericTextBox MT_Hours = UiFactory.Numeric("MT_Hours", 5, 60);
    private readonly NumericTextBox MT_Minutes = UiFactory.Numeric("MT_Minutes", 2, 44);
    private readonly NumericTextBox MT_Seconds = UiFactory.Numeric("MT_Seconds", 2, 44);

    private readonly DatePicker CAL_AdventureStartDate = new() { Name = "CAL_AdventureStartDate" };
    private readonly DatePicker CAL_LastSavedDate = new() { Name = "CAL_LastSavedDate" };
    private readonly TimePicker CAL_LastSavedTime = new() { Name = "CAL_LastSavedTime" };
    private readonly TextBlock L_LastSaved = UiFactory.Label("L_LastSaved", "Last Saved:");

    private readonly NumericUpDown NUD_M = UiFactory.NumericUpDown("NUD_M", 0, uint.MaxValue, 110);
    private readonly NumericUpDown NUD_X = UiFactory.NumericUpDown("NUD_X", -100000, 100000, 100);
    private readonly NumericUpDown NUD_Z = UiFactory.NumericUpDown("NUD_Z", -100000, 100000, 100);
    private readonly NumericUpDown NUD_Y = UiFactory.NumericUpDown("NUD_Y", -100000, 100000, 100);
    private readonly NumericUpDown NUD_SX = UiFactory.NumericUpDown("NUD_SX", -100000, 100000, 100);
    private readonly NumericUpDown NUD_SZ = UiFactory.NumericUpDown("NUD_SZ", -100000, 100000, 100);
    private readonly NumericUpDown NUD_SY = UiFactory.NumericUpDown("NUD_SY", -100000, 100000, 100);
    private readonly NumericUpDown NUD_R = UiFactory.NumericUpDown("NUD_R", -360, 360, 100);
    private GroupBoxView GB_Map = null!;

    private readonly NumericTextBox MT_BattleTowerSinglesWin = UiFactory.Numeric("MT_BattleTowerSinglesWin", 7, 100);
    private readonly NumericTextBox MT_BattleTowerDoublesWin = UiFactory.Numeric("MT_BattleTowerDoublesWin", 7, 100);
    private readonly NumericTextBox MT_BattleTowerSinglesStreak = UiFactory.Numeric("MT_BattleTowerSinglesStreak", 4, 80);
    private readonly NumericTextBox MT_BattleTowerDoublesStreak = UiFactory.Numeric("MT_BattleTowerDoublesStreak", 4, 80);

    private readonly NumericUpDown NUD_ShowTrainerCard = UiFactory.NumericUpDown("NUD_ShowTrainerCard", 1, 6, 90);
    private readonly NumericUpDown NUD_ShowTitleScreen = UiFactory.NumericUpDown("NUD_ShowTitleScreen", 1, 6, 90);
    private readonly PropertyGridView PG_ShowTrainerCard = new() { Name = "PG_ShowTrainerCard", Width = 380, Height = 200 };
    private readonly PropertyGridView PG_ShowTitleScreen = new() { Name = "PG_ShowTitleScreen", Width = 380, Height = 200 };
    private readonly Button B_CopyFromPartyToTrainerCard = UiFactory.Button("B_CopyFromPartyToTrainerCard", "Copy Party");
    private readonly Button B_CopyFromPartyToTitleScreen = UiFactory.Button("B_CopyFromPartyToTitleScreen", "Copy Party");

    private readonly ComboBox CB_Fashion = UiFactory.StringCombo("CB_Fashion", 160);
    private readonly Button B_Fashion = UiFactory.Button("B_Fashion", "Apply Fashion");
    private readonly Button B_ResetAppearance = UiFactory.Button("B_ResetAppearance", "Reset Appearance");
    private readonly Button B_CollectDiglett = UiFactory.Button("B_CollectDiglett", "Collect all Diglett");

    private readonly TrainerStatView TrainerStats = new();

    public Trainer8Window(SAV8SWSH sav) : base("SAV_Trainer8", "Trainer Data Editor")
    {
        SAV = (SAV8SWSH)(Origin = sav).Clone();

        foreach (var s in GameInfo.GenderSymbolUnicode.Take(2))
            CB_Gender.Items.Add(s);
        foreach (var v in new[] { GameVersion.SW, GameVersion.SH })
            CB_Game.Items.Add(v.ToString());
        foreach (var s in new[] { "Base Fashion", "Full Legal", "Everything" })
            CB_Fashion.Items.Add(s);

        BuildLayout();
        GetComboBoxes();
        GetTextBoxes();
        GetMiscValues();

        // after SetBody: translation would otherwise overwrite the computed offset label
        TrainerStats.LoadRecords(SAV, RecordLists.RecordList_8);
        NUD_BP.SetValueClamped(Math.Min(SAV.Misc.BP, 9999));

        ChangeTitleScreenIndex();
        ChangeTrainerCardIndex();
        CB_Fashion.SelectedIndex = 1;
        if (SAV.SaveRevision == 0)
            B_CollectDiglett.IsVisible = false;
    }

    #region Layout

    private void BuildLayout()
    {
        var main = UiFactory.FormGrid(11);
        UiFactory.AddFormRow(main, 0, UiFactory.Label("L_TrainerName", "Trainer Name:"), UiFactory.Row(TB_OTName, CB_Gender));
        UiFactory.AddFormRow(main, 1, UiFactory.Label("L_Game", "Game:"), CB_Game);
        UiFactory.AddFormRow(main, 2, UiFactory.Label("L_TrainerID", "Trainer ID:"), trainerID1);
        UiFactory.AddFormRow(main, 3, UiFactory.Label("L_Money", "Money:"), UiFactory.Row(MT_Money, B_MaxCash));
        UiFactory.AddFormRow(main, 4, UiFactory.Label("L_Watt", "Watts:"), UiFactory.Row(MT_Watt, B_MaxWatt));
        UiFactory.AddFormRow(main, 5, UiFactory.Label("L_BP", "Battle Points:"), NUD_BP);
        UiFactory.AddFormRow(main, 6, UiFactory.Label("L_Language", "Language:"), CB_Language);
        UiFactory.AddFormRow(main, 7, UiFactory.Label("L_SkinColor", "Skin Color:"), CB_SkinColor);
        UiFactory.AddFormRow(main, 8, UiFactory.Label("L_PlayTime", "Play Time:"), UiFactory.Row(MT_Hours, MT_Minutes, MT_Seconds));
        UiFactory.AddFormRow(main, 9, UiFactory.Label("L_AdventureStart", "Adventure Started:"), CAL_AdventureStartDate);
        UiFactory.AddFormRow(main, 10, L_LastSaved, UiFactory.Row(CAL_LastSavedDate, CAL_LastSavedTime));

        var card = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(card, 0, UiFactory.Label("L_TrainerCardName", "Card Name:"), TB_TrainerCardName);
        UiFactory.AddFormRow(card, 1, UiFactory.Label("L_TrainerCardNumber", "Card Number:"), TB_TrainerCardNumber);
        UiFactory.AddFormRow(card, 2, UiFactory.Label("L_TrainerCardID", "Card ID:"), MT_TrainerCardID);
        UiFactory.AddFormRow(card, 3, UiFactory.Label("L_RotoRally", "Roto Rally:"), MT_RotoRally);

        GB_Map = new GroupBoxView("GB_Map", "Map Position", UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_M", "M:"), NUD_M, UiFactory.Label("L_R", "R:"), NUD_R),
            UiFactory.Row(UiFactory.Label("L_X", "X:"), NUD_X, UiFactory.Label("L_Z", "Z:"), NUD_Z, UiFactory.Label("L_Y", "Y:"), NUD_Y),
            UiFactory.Row(UiFactory.Label("L_SX", "SX:"), NUD_SX, UiFactory.Label("L_SZ", "SZ:"), NUD_SZ, UiFactory.Label("L_SY", "SY:"), NUD_SY)));

        var appearance = UiFactory.Column(
            UiFactory.Row(CB_Fashion, B_Fashion),
            UiFactory.Row(B_ResetAppearance, B_CollectDiglett));

        var overview = UiFactory.Column(main, new GroupBoxView("GB_TrainerCard", "Trainer Card", card), GB_Map, appearance);

        var tower = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(tower, 0, UiFactory.Label("L_BTSinglesWin", "Singles Wins:"), MT_BattleTowerSinglesWin);
        UiFactory.AddFormRow(tower, 1, UiFactory.Label("L_BTDoublesWin", "Doubles Wins:"), MT_BattleTowerDoublesWin);
        UiFactory.AddFormRow(tower, 2, UiFactory.Label("L_BTSinglesStreak", "Singles Streak:"), MT_BattleTowerSinglesStreak);
        UiFactory.AddFormRow(tower, 3, UiFactory.Label("L_BTDoublesStreak", "Doubles Streak:"), MT_BattleTowerDoublesStreak);

        var showcase = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        showcase.Children.Add(UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_ShowTrainerCard", "Trainer Card Slot:"), NUD_ShowTrainerCard, B_CopyFromPartyToTrainerCard),
            PG_ShowTrainerCard));
        showcase.Children.Add(UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_ShowTitleScreen", "Title Screen Slot:"), NUD_ShowTitleScreen, B_CopyFromPartyToTitleScreen),
            PG_ShowTitleScreen));

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Name = "Tab_Overview", Header = "Overview", Content = new ScrollViewer { Content = overview, MaxHeight = 540 } });
        tabs.Items.Add(new TabItem { Name = "Tab_Records", Header = "Records", Content = TrainerStats });
        tabs.Items.Add(new TabItem { Name = "Tab_Tower", Header = "Battle Tower", Content = tower });
        tabs.Items.Add(new TabItem { Name = "Tab_Showcase", Header = "Showcase", Content = new ScrollViewer { Content = showcase, MaxHeight = 540 } });
        SetBody(tabs);

        B_MaxCash.Click += (_, _) => MT_Money.Text = SAV.MaxMoney.ToString();
        B_MaxWatt.Click += (_, _) => MT_Watt.Text = MyStatus8.MaxWatt.ToString();
        NUD_ShowTrainerCard.ValueChanged += (_, _) => ChangeTrainerCardIndex();
        NUD_ShowTitleScreen.ValueChanged += (_, _) => ChangeTitleScreenIndex();
        B_CopyFromPartyToTrainerCard.Click += (_, _) => { SAV.Blocks.TrainerCard.SetPartyData(); ChangeTrainerCardIndex(); };
        B_CopyFromPartyToTitleScreen.Click += (_, _) => { SAV.Blocks.TitleScreen.SetPartyData(); ChangeTitleScreenIndex(); };
        CB_Gender.SelectionChanged += async (_, _) => await ChangeGender();
        CB_SkinColor.SelectionChanged += (_, _) =>
        {
            if (CB_SkinColor.SelectedIndex >= 0)
                SAV.MyStatus.SetSkinColor((PlayerSkinColor8)CB_SkinColor.SelectedIndex);
        };
        B_Fashion.Click += async (_, _) => await ClickFashion();
        B_ResetAppearance.Click += async (_, _) => await ResetAppearance();
        B_CollectDiglett.Click += (_, _) => SAV.UnlockAllDiglett();
        foreach (var nud in new[] { NUD_M, NUD_X, NUD_Z, NUD_Y, NUD_SX, NUD_SZ, NUD_SY, NUD_R })
            nud.ValueChanged += (_, _) => MapUpdated = true;
        TB_OTName.AttachClick(async mods =>
        {
            if (mods == KeyModifiers.Control)
                await TrashEditorWindow.ShowAsync(this, TB_OTName, SAV, SAV.MyStatus.OriginalTrainerTrash.ToArray());
        });
        TB_TrainerCardName.AttachClick(async mods =>
        {
            if (mods == KeyModifiers.Control)
                await TrashEditorWindow.ShowAsync(this, TB_TrainerCardName, SAV, SAV.Blocks.TrainerCard.OriginalTrainerTrash.ToArray());
        });
    }

    #endregion

    #region Load

    private void GetComboBoxes()
    {
        CB_Language.SetItems(GameInfo.LanguageDataSource(SAV.Generation, SAV.Context));
        CB_SkinColor.Items.Clear();
        foreach (var s in Translator.GetEnumTranslation<PlayerSkinColor8>(MainWindow.CurrentLanguage))
            CB_SkinColor.Items.Add(s);
        CB_SkinColor.SelectedIndex = (int)PlayerSkinColor8Extensions.GetSkinColorFromSkin(SAV.MyStatus.Skin);
    }

    private void GetTextBoxes()
    {
        CB_Game.SelectedIndex = Math.Clamp(SAV.Version - GameVersion.SW, 0, CB_Game.ItemCount - 1);
        CB_Gender.SelectedIndex = SAV.Gender;

        TB_OTName.Text = SAV.OT;
        TB_TrainerCardName.Text = SAV.Blocks.TrainerCard.OT;
        TB_TrainerCardNumber.Text = SAV.Blocks.TrainerCard.Number;
        MT_TrainerCardID.Text = SAV.Blocks.TrainerCard.TrainerID.ToString("000000");
        MT_RotoRally.Text = SAV.Blocks.TrainerCard.RotoRallyScore.ToString();
        trainerID1.LoadTrainer(SAV);
        MT_Money.Text = SAV.Money.ToString();
        MT_Watt.Text = SAV.MyStatus.Watt.ToString();
        CB_Language.SetValue(SAV.Language);

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

        if (SAV.Played.LastSavedDate is { } lastSaved)
        {
            CAL_LastSavedDate.SelectedDate = UiFactory.ToOffset(lastSaved);
            CAL_LastSavedTime.SelectedTime = lastSaved.TimeOfDay;
        }
        else
        {
            L_LastSaved.IsVisible = CAL_LastSavedDate.IsVisible = CAL_LastSavedTime.IsVisible = false;
        }

        var card = SAV.TrainerCard;
        CAL_AdventureStartDate.SelectedDate = UiFactory.ToOffset(new DateTime(card.StartedYear, card.StartedMonth, card.StartedDay));
    }

    private void GetMiscValues()
    {
        MT_BattleTowerSinglesWin.Text = SAV.GetValue<uint>(SaveBlockAccessor8SWSH.KBattleTowerSinglesVictory).ToString();
        MT_BattleTowerDoublesWin.Text = SAV.GetValue<uint>(SaveBlockAccessor8SWSH.KBattleTowerDoublesVictory).ToString();
        MT_BattleTowerSinglesStreak.Text = SAV.GetValue<ushort>(SaveBlockAccessor8SWSH.KBattleTowerSinglesStreak).ToString();
        MT_BattleTowerDoublesStreak.Text = SAV.GetValue<ushort>(SaveBlockAccessor8SWSH.KBattleTowerDoublesStreak).ToString();
    }

    private void ChangeTrainerCardIndex()
        => PG_ShowTrainerCard.SetObject(SAV.Blocks.TrainerCard.ViewPoke((int)(NUD_ShowTrainerCard.Value ?? 1) - 1));

    private void ChangeTitleScreenIndex()
        => PG_ShowTitleScreen.SetObject(SAV.Blocks.TitleScreen.ViewPoke((int)(NUD_ShowTitleScreen.Value ?? 1) - 1));

    #endregion

    #region Handlers

    private async Task ChangeGender()
    {
        if (CB_Gender.SelectedIndex < 0 || SAV.Gender == (byte)CB_Gender.SelectedIndex)
            return;
        SAV.Gender = SAV.MyStatus.GenderAppearance = (byte)CB_Gender.SelectedIndex;
        await ResetAppearance();
    }

    private async Task ResetAppearance()
    {
        var index = (CB_SkinColor.SelectedIndex & ~0x1) | (CB_Gender.SelectedIndex & 1);
        CB_SkinColor.SelectedIndex = index;
        SAV.MyStatus.ResetAppearance((PlayerSkinColor8)index);
        await AppDialogs.Alert(this, "Trainer appearance has been reset.");
    }

    private async Task ClickFashion()
    {
        var prompt = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo,
            "Modifying Fashion Items will clear existing fashion unlock data.", "Continue?");
        if (prompt != DialogResult.Yes)
            return;

        SAV.Fashion.Clear();
        switch (CB_Fashion.SelectedIndex)
        {
            case 0: SAV.Fashion.Reset(); break;
            case 1: SAV.Fashion.UnlockAllLegal(); break;
            case 2: SAV.Fashion.UnlockAll(); break;
        }
    }

    #endregion

    #region Save

    private void SaveTrainerInfo()
    {
        SAV.Version = (GameVersion)(CB_Game.SelectedIndex + (int)GameVersion.SW);
        SAV.Gender = (byte)Math.Max(0, CB_Gender.SelectedIndex);
        SAV.Money = Util.ToUInt32(MT_Money.Text ?? string.Empty);
        SAV.Language = CB_Language.GetSelectedItem()?.Value ?? 0;
        trainerID1.SaveTrainer(SAV);

        // only modify if changed, to preserve trash bytes
        if (SAV.OT != TB_OTName.Text)
            SAV.OT = TB_OTName.Text ?? string.Empty;
        if (SAV.Blocks.TrainerCard.OT != TB_TrainerCardName.Text)
            SAV.Blocks.TrainerCard.OT = TB_TrainerCardName.Text ?? string.Empty;

        SAV.Blocks.MyStatus.Number = SAV.Blocks.TrainerCard.Number = TB_TrainerCardNumber.Text ?? string.Empty;
        SAV.Blocks.TrainerCard.TrainerID = Util.ToInt32(MT_TrainerCardID.Text ?? string.Empty);
        SAV.Blocks.TrainerCard.RotoRallyScore = Util.ToInt32(MT_RotoRally.Text ?? string.Empty);

        var watt = Util.ToUInt32(MT_Watt.Text ?? string.Empty);
        SAV.MyStatus.Watt = watt;
        if (SAV.GetRecord(Record8.WattTotal) < watt)
            SAV.SetRecord(Record8.WattTotal, (int)watt);

        SAV.Misc.BP = (int)(NUD_BP.Value ?? 0);

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

        var start = CAL_AdventureStartDate.SelectedDate?.Date ?? new DateTime(2000, 1, 1);
        SAV.TrainerCard.StartedYear = (ushort)start.Year;
        SAV.TrainerCard.StartedMonth = (byte)start.Month;
        SAV.TrainerCard.StartedDay = (byte)start.Day;

        if (SAV.Played.LastSavedDate.HasValue)
        {
            var d = CAL_LastSavedDate.SelectedDate?.Date ?? start;
            SAV.Played.LastSavedDate = d + (CAL_LastSavedTime.SelectedTime ?? TimeSpan.Zero);
        }
    }

    private void SaveMiscValues()
    {
        var singles = Math.Min(9_999_999u, Util.ToUInt32(MT_BattleTowerSinglesWin.Text ?? string.Empty));
        var doubles = Math.Min(9_999_999u, Util.ToUInt32(MT_BattleTowerDoublesWin.Text ?? string.Empty));
        SAV.SetValue(SaveBlockAccessor8SWSH.KBattleTowerSinglesVictory, singles);
        SAV.SetValue(SaveBlockAccessor8SWSH.KBattleTowerDoublesVictory, doubles);
        SAV.SetValue(SaveBlockAccessor8SWSH.KBattleTowerSinglesStreak, (ushort)Math.Min(300, Util.ToUInt32(MT_BattleTowerSinglesStreak.Text ?? string.Empty)));
        SAV.SetValue(SaveBlockAccessor8SWSH.KBattleTowerDoublesStreak, (ushort)Math.Min(300, Util.ToUInt32(MT_BattleTowerDoublesStreak.Text ?? string.Empty)));

        SAV.SetRecord(RecordLists.G8BattleTowerSingleWin, (int)singles);
        SAV.SetRecord(RecordLists.G8BattleTowerDoubleWin, (int)doubles);
    }

    #endregion

    protected override void OnSave()
    {
        SaveTrainerInfo();
        SaveMiscValues();
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
