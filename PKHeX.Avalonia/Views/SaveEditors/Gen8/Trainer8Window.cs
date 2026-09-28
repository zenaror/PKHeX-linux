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

    private readonly RenderedString TB_OTName = UiFactory.Name("TB_OTName", 12, 140);
    private readonly RenderedString TB_TrainerCardName = UiFactory.Name("TB_TrainerCardName", 12, 140);
    private readonly TextBox TB_TrainerCardNumber = UiFactory.Text("TB_TrainerCardNumber", 12, 120);
    private readonly NumericTextBox MT_TrainerCardID = UiFactory.Numeric("MT_TrainerCardID", 6, 90);
    private readonly NumericTextBox MT_RotoRally = UiFactory.Numeric("MT_RotoRally", 5, 100);
    private readonly ComboBox CB_Gender = UiFactory.StringCombo("CB_Gender", 60);
    private readonly ComboBox CB_Game = UiFactory.StringCombo("CB_Game", 120);
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 140);
    private readonly ComboBox CB_SkinColor = UiFactory.StringCombo("CB_SkinColor", 160);
    private readonly TrainerIDView trainerID1 = new() { Name = "trainerID1" };
    private readonly NumericTextBox MT_Money = UiFactory.Numeric("MT_Money", 7, 110);
    private readonly Button B_MaxCash = UiFactory.Button("B_MaxCash", "+");
    private readonly NumericTextBox MT_Watt = UiFactory.Numeric("MT_Watt", 7, 110);
    private readonly Button B_MaxWatt = UiFactory.Button("B_MaxWatt", "+");
    private readonly NumericUpDown NUD_BP = UiFactory.NumericUpDown("NUD_BP", 0, 9999, 110);
    private readonly NumericTextBox MT_Hours = UiFactory.Numeric("MT_Hours", 5, 60);
    private readonly NumericTextBox MT_Minutes = UiFactory.Numeric("MT_Minutes", 2, 44);
    private readonly NumericTextBox MT_Seconds = UiFactory.Numeric("MT_Seconds", 2, 44);

    private readonly DatePicker CAL_AdventureStartDate = new() { Name = "CAL_AdventureStartDate" };
    private readonly DatePicker CAL_LastSavedDate = new() { Name = "CAL_LastSavedDate" };
    private readonly TimePicker CAL_LastSavedTime = new() { Name = "CAL_LastSavedTime", ClockIdentifier = "24HourClock" };
    private readonly TextBlock L_LastSaved = UiFactory.Label("L_LastSaved", "Last Saved:");

    private readonly NumericUpDown NUD_M = UiFactory.NumericUpDown("NUD_M", 0, ulong.MaxValue, 140);
    private readonly NumericUpDown NUD_X = Coordinate("NUD_X");
    private readonly NumericUpDown NUD_Z = Coordinate("NUD_Z");
    private readonly NumericUpDown NUD_Y = Coordinate("NUD_Y");
    private readonly NumericUpDown NUD_SX = Coordinate("NUD_SX");
    private readonly NumericUpDown NUD_SZ = Coordinate("NUD_SZ");
    private readonly NumericUpDown NUD_SY = Coordinate("NUD_SY");
    private readonly NumericUpDown NUD_R = Coordinate("NUD_R");
    private GroupBoxView GB_Map = null!;

    private readonly NumericTextBox MT_BattleTowerSinglesWin = UiFactory.Numeric("MT_BattleTowerSinglesWin", 7, 100);
    private readonly NumericTextBox MT_BattleTowerDoublesWin = UiFactory.Numeric("MT_BattleTowerDoublesWin", 7, 100);
    private readonly NumericTextBox MT_BattleTowerSinglesStreak = UiFactory.Numeric("MT_BattleTowerSinglesStreak", 3, 80);
    private readonly NumericTextBox MT_BattleTowerDoublesStreak = UiFactory.Numeric("MT_BattleTowerDoublesStreak", 3, 80);

    private readonly NumericUpDown NUD_ShowTrainerCard = UiFactory.NumericUpDown("NUD_ShowTrainerCard", 1, 6, 90);
    private readonly NumericUpDown NUD_ShowTitleScreen = UiFactory.NumericUpDown("NUD_ShowTitleScreen", 1, 6, 90);
    private readonly PropertyGridView PG_ShowTrainerCard = new() { Name = "PG_ShowTrainerCard", Width = 380, Height = 200 };
    private readonly PropertyGridView PG_ShowTitleScreen = new() { Name = "PG_ShowTitleScreen", Width = 380, Height = 200 };
    private readonly Button B_CopyFromPartyToTrainerCard = UiFactory.Button("B_CopyFromPartyToTrainerCard", "Copy From Party");
    private readonly Button B_CopyFromPartyToTitleScreen = UiFactory.Button("B_CopyFromPartyToTitleScreen", "Copy From Party");

    private readonly ComboBox CB_Fashion = UiFactory.StringCombo("CB_Fashion", 160);
    private readonly Button B_Fashion = UiFactory.Button("B_Fashion", "Give all Fashion Items");
    private readonly Button B_ResetAppearance = UiFactory.Button("B_ResetAppearance", "Reset Appearance");
    private readonly Button B_CollectDiglett = UiFactory.Button("B_CollectDiglett", "Collect All Diglett");

    private readonly TrainerStatView TrainerStats = new();

    public Trainer8Window(SAV8SWSH sav) : base("SAV_Trainer8", "Trainer Data Editor")
    {
        SAV = (SAV8SWSH)(Origin = sav).Clone();

        if (!MainWindow.Unicode)
            TB_OTName.DisableInGameFont = TB_TrainerCardName.DisableInGameFont = true;

        foreach (var s in MainWindow.GenderSymbols.Take(2)) // m/f depending on unicode selection
            CB_Gender.Items.Add(s);
        foreach (var s in new[] { "Sword", "Shield" })
            CB_Game.Items.Add(s);
        foreach (var s in new[] { "New Game", "All Legal", "Everything" })
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
        // Overview: WinForms splits the page in two halves -- trainer details and the Adventure group on the left,
        // the League Card fields and the Stats group on the right (SAV_Trainer8.Designer.cs:714-736).
        var main = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(main, 0, UiFactory.Label("L_TrainerName", "Trainer Name:"), TB_OTName);
        UiFactory.AddFormRow(main, 1, trainerID1, UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_Money", "$:"), MT_Money, B_MaxCash),
            UiFactory.Row(UiFactory.Label("L_Watt", "W:"), MT_Watt, B_MaxWatt)));
        UiFactory.AddFormRow(main, 2, null, UiFactory.Row(CB_Gender, CB_Game));
        UiFactory.AddFormRow(main, 3, UiFactory.Label("L_Language", "Language:"), CB_Language);

        var adventure = UiFactory.FormGrid(3);
        var playTime = UiFactory.Row(
            UiFactory.Label("L_Hours", "Hrs:"), MT_Hours,
            UiFactory.Label("L_Minutes", "Min:"), MT_Minutes,
            UiFactory.Label("L_Seconds", "Sec:"), MT_Seconds);
        UiFactory.SetRowCol(playTime, 0, 0, 2); // flush left, as in WinForms
        adventure.Children.Add(playTime);
        UiFactory.AddFormRow(adventure, 1, UiFactory.Label("L_Started", "Game Started:"), CAL_AdventureStartDate);
        UiFactory.AddFormRow(adventure, 2, L_LastSaved, UiFactory.Row(CAL_LastSavedDate, CAL_LastSavedTime));

        var card = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(card, 0, UiFactory.Label("L_TRCardName", "League Card Name:"), TB_TrainerCardName);
        UiFactory.AddFormRow(card, 1, UiFactory.Label("L_TRCardNumber", "League Uniform ID:"), TB_TrainerCardNumber);
        UiFactory.AddFormRow(card, 2, UiFactory.Label("L_TRCardID", "League TrainerID:"), MT_TrainerCardID);
        UiFactory.AddFormRow(card, 3, UiFactory.Label("L_RotoRally", "Roto Rally Score:"), MT_RotoRally);

        var stats = UiFactory.Column(UiFactory.Row(UiFactory.Label("L_BP", "BP:"), NUD_BP), TrainerStats);

        var left = UiFactory.Column(main, new GroupBoxView("GB_Adventure", "Adventure Info", adventure));
        var right = UiFactory.Column(card, new GroupBoxView("GB_Stats", "Stats", stats));
        left.VerticalAlignment = right.VerticalAlignment = VerticalAlignment.Top;
        var overview = UiFactory.Row(left, right);
        overview.Spacing = 10;

        // Map tab: eight rows, in the WinForms order (M, X, Z, Y, then the scales, then the rotation).
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
        GB_Map.HorizontalAlignment = HorizontalAlignment.Left;
        GB_Map.VerticalAlignment = VerticalAlignment.Top;

        // Misc tab: the Battle Tower grid plus the appearance buttons.
        var tower = new Grid { ColumnSpacing = 6, RowSpacing = 3 };
        for (int i = 0; i < 3; i++)
        {
            tower.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            tower.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        Place(tower, UiFactory.Label("L_BattleTowerWins", "Wins"), 0, 1);
        Place(tower, UiFactory.Label("L_BattleTowerStreak", "Streak"), 0, 2);
        Place(tower, UiFactory.Label("L_Singles", "Singles:"), 1, 0);
        Place(tower, MT_BattleTowerSinglesWin, 1, 1);
        Place(tower, MT_BattleTowerSinglesStreak, 1, 2);
        Place(tower, UiFactory.Label("L_Doubles", "Doubles:"), 2, 0);
        Place(tower, MT_BattleTowerDoublesWin, 2, 1);
        Place(tower, MT_BattleTowerDoublesStreak, 2, 2);

        var appearance = UiFactory.Column(
            UiFactory.Row(CB_Fashion, B_Fashion),
            UiFactory.Row(UiFactory.Label("L_SkinColor", "Skin Color:"), CB_SkinColor),
            UiFactory.Row(B_ResetAppearance, B_CollectDiglett));
        var misc = UiFactory.Column(new GroupBoxView("GB_BattleTower", "Battle Tower", tower), appearance);
        misc.HorizontalAlignment = HorizontalAlignment.Left;

        // Team tab: the copy button sits under each grid, as in WinForms.
        var showcase = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        showcase.Children.Add(UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_ShowTrainerCard", "Shown on Trainer Card:"), NUD_ShowTrainerCard),
            PG_ShowTrainerCard,
            B_CopyFromPartyToTrainerCard));
        showcase.Children.Add(UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_ShowTitleScreen", "Shown on Title Screen:"), NUD_ShowTitleScreen),
            PG_ShowTitleScreen,
            B_CopyFromPartyToTitleScreen));

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Name = "Tab_Overview", Header = "Overview", Content = new ScrollViewer { Content = overview, MaxHeight = 540 } });
        tabs.Items.Add(new TabItem { Name = "Tab_BadgeMap", Header = "Map", Content = new ScrollViewer { Content = GB_Map, MaxHeight = 540 } });
        tabs.Items.Add(new TabItem { Name = "Tab_MiscValues", Header = "Misc", Content = misc });
        tabs.Items.Add(new TabItem { Name = "Tab_Team", Header = "Team", Content = new ScrollViewer { Content = showcase, MaxHeight = 540 } });
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
            if (mods != KeyModifiers.Control)
                return;
            var trash = await TrashEditorWindow.ShowAsync(this, TB_OTName, SAV, SAV.MyStatus.OriginalTrainerTrash.ToArray());
            trash?.CopyTo(SAV.MyStatus.OriginalTrainerTrash); // WinForms writes the edited bytes back into the save
        });
        TB_TrainerCardName.AttachClick(async mods =>
        {
            if (mods != KeyModifiers.Control)
                return;
            var trash = await TrashEditorWindow.ShowAsync(this, TB_TrainerCardName, SAV, SAV.Blocks.TrainerCard.OriginalTrainerTrash.ToArray());
            trash?.CopyTo(SAV.Blocks.TrainerCard.OriginalTrainerTrash);
        });
    }

    /// <summary>Map coordinate box: WinForms shows six decimals over the full float range.</summary>
    private static NumericUpDown Coordinate(string name)
    {
        var nud = UiFactory.NumericUpDown(name, -99_999_999m, 99_999_999m, 130);
        nud.FormatString = "0.000000";
        return nud;
    }

    private static void Place(Grid g, Control c, int row, int col)
    {
        UiFactory.SetRowCol(c, row, col);
        g.Children.Add(c);
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
