using System;
using System.Collections.ObjectModel;
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

namespace PKHeX.Avalonia.Views.SaveEditors.Gen7;

/// <summary>
/// Trainer editor for Sun/Moon and Ultra Sun/Ultra Moon (port of the WinForms <c>SAV_Trainer7</c>).
/// </summary>
/// <remarks>
/// Besides the usual trainer data this covers the Poké Finder counters, Battle Tree streaks, the ball throw
/// style unlocks, the Alola fly destinations and map reveal flags, and the Ultra-only surf scores and Rotom data.
/// </remarks>
public sealed class Trainer7Window : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV7 SAV;
    private readonly string[] BattleStyles;
    private readonly bool Loading;
    private bool MapUpdated;

    private int[] FlyDestFlagOfs = [];
    private int[] MapUnmaskFlagOfs = [];
    private int SkipFlag => SAV is SAV7USUM ? 4160 : 3200; // FlagMax - 768

    // Overview
    private readonly TextBox TB_OTName = UiFactory.Text("TB_OTName", 12, 140);
    private readonly ComboBox CB_Gender = UiFactory.StringCombo("CB_Gender", 60);
    private readonly ComboBox CB_Game = UiFactory.StringCombo("CB_Game", 130);
    private readonly TrainerIDView trainerID1 = new() { Name = "trainerID1" };
    private readonly NumericTextBox MT_Money = UiFactory.Numeric("MT_Money", 8, 100);
    private readonly Button B_MaxCash = UiFactory.Button("B_MaxCash", "Max");
    private readonly ComboBox CB_Country = UiFactory.Combo("CB_Country", 180);
    private readonly ComboBox CB_Region = UiFactory.Combo("CB_Region", 180);
    private readonly ComboBox CB_3DSReg = UiFactory.Combo("CB_3DSReg", 180);
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 140);
    private readonly ComboBox CB_AlolaTime = UiFactory.Combo("CB_AlolaTime", 140);
    private readonly NumericTextBox MT_Hours = UiFactory.Numeric("MT_Hours", 5, 60);
    private readonly NumericTextBox MT_Minutes = UiFactory.Numeric("MT_Minutes", 2, 44);
    private readonly NumericTextBox MT_Seconds = UiFactory.Numeric("MT_Seconds", 2, 44);
    private readonly NumericUpDown NUD_BP = UiFactory.NumericUpDown("NUD_BP", 0, 9999, 110);
    private readonly NumericUpDown NUD_FC = UiFactory.NumericUpDown("NUD_FC", 0, 9999, 110);
    private readonly NumericUpDown NUD_DaysFromRefreshed = UiFactory.NumericUpDown("NUD_DaysFromRefreshed", 0, byte.MaxValue, 110);
    private readonly ComboBox CB_Vivillon = UiFactory.StringCombo("CB_Vivillon", 160);
    private readonly TextBlock L_Vivillon = UiFactory.Label("L_Vivillon", "Vivillon:");
    private readonly TextBox TB_PlazaName = UiFactory.Text("TB_PlazaName", 20, 200);
    private readonly ComboBox CB_SkinColor = UiFactory.StringCombo("CB_SkinColor", 160);
    private readonly ComboBox CB_BallThrowType = UiFactory.StringCombo("CB_BallThrowType", 160);
    private readonly CheckBox CHK_UnlockMega = UiFactory.Check("CHK_UnlockMega", "Mega Unlocked");
    private readonly CheckBox CHK_UnlockZMove = UiFactory.Check("CHK_UnlockZMove", "Z-Move Unlocked");
    private readonly CheckBox CHK_UnlockSuperSingles = UiFactory.Check("CHK_UnlockSuperSingles", "Super Singles");
    private readonly CheckBox CHK_UnlockSuperDoubles = UiFactory.Check("CHK_UnlockSuperDoubles", "Super Doubles");
    private readonly CheckBox CHK_UnlockSuperMulti = UiFactory.Check("CHK_UnlockSuperMulti", "Super Multi");
    private readonly ComboBox CB_Fashion = UiFactory.StringCombo("CB_Fashion", 160);
    private readonly Button B_Fashion = UiFactory.Button("B_Fashion", "Apply Fashion");

    private readonly DatePicker CAL_AdventureStartDate = new() { Name = "CAL_AdventureStartDate" };
    private readonly TimePicker CAL_AdventureStartTime = new() { Name = "CAL_AdventureStartTime" };
    private readonly DatePicker CAL_HoFDate = new() { Name = "CAL_HoFDate" };
    private readonly TimePicker CAL_HoFTime = new() { Name = "CAL_HoFTime" };
    private readonly DatePicker CAL_LastSavedDate = new() { Name = "CAL_LastSavedDate" };
    private readonly TimePicker CAL_LastSavedTime = new() { Name = "CAL_LastSavedTime" };
    private readonly TextBlock L_LastSaved = UiFactory.Label("L_LastSaved", "Last Saved:");

    // Map
    private readonly NumericUpDown NUD_M = UiFactory.NumericUpDown("NUD_M", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_X = UiFactory.NumericUpDown("NUD_X", -100000, 100000, 110);
    private readonly NumericUpDown NUD_Z = UiFactory.NumericUpDown("NUD_Z", -100000, 100000, 110);
    private readonly NumericUpDown NUD_Y = UiFactory.NumericUpDown("NUD_Y", -100000, 100000, 110);
    private readonly NumericUpDown NUD_R = UiFactory.NumericUpDown("NUD_R", -360, 360, 110);
    private GroupBoxView GB_Map = null!;

    // Poké Finder / Battle Tree
    private readonly NumericUpDown NUD_SnapCount = UiFactory.NumericUpDown("NUD_SnapCount", 0, uint.MaxValue, 130);
    private readonly NumericUpDown NUD_ThumbsTotal = UiFactory.NumericUpDown("NUD_ThumbsTotal", 0, uint.MaxValue, 130);
    private readonly NumericUpDown NUD_ThumbsRecord = UiFactory.NumericUpDown("NUD_ThumbsRecord", 0, uint.MaxValue, 130);
    private readonly ComboBox CB_CameraVersion = UiFactory.StringCombo("CB_CameraVersion", 100);
    private readonly CheckBox CHK_Gyro = UiFactory.Check("CHK_Gyro", "Gyro");
    private readonly NumericUpDown[] TreeStreaks = new NumericUpDown[12];

    // Flags
    private readonly CheckedListView CLB_FlyDest = new() { Name = "CLB_FlyDest", Width = 280, Height = 280 };
    private readonly CheckedListView CLB_MapUnmask = new() { Name = "CLB_MapUnmask", Width = 280, Height = 280 };
    private readonly Button B_AllFlyDest = UiFactory.Button("B_AllFlyDest", "All");
    private readonly Button B_AllMapUnmask = UiFactory.Button("B_AllMapUnmask", "All");
    private readonly ListBox LB_Stamps = new() { Name = "LB_Stamps", Width = 240, Height = 220, SelectionMode = SelectionMode.Multiple };
    private readonly ObservableCollection<string> StampItems = [];
    private readonly ListBox LB_BallThrowTypeUnlocked = new() { Name = "LB_BallThrowTypeUnlocked", Width = 200, Height = 160, SelectionMode = SelectionMode.Multiple };
    private readonly ListBox LB_BallThrowTypeLearned = new() { Name = "LB_BallThrowTypeLearned", Width = 200, Height = 160, SelectionMode = SelectionMode.Multiple };
    private readonly ObservableCollection<string> UnlockedItems = [];
    private readonly ObservableCollection<string> LearnedItems = [];
    private readonly ComboBox CB_BallThrowTypeListMode = UiFactory.StringCombo("CB_BallThrowTypeListMode", 140);

    // Ultra only
    private readonly NumericUpDown[] NUD_Surf = new NumericUpDown[4];
    private readonly TextBox TB_RotomOT = UiFactory.Text("TB_RotomOT", 12, 140);
    private readonly NumericUpDown NUD_RotomAffection = UiFactory.NumericUpDown("NUD_RotomAffection", 0, ushort.MaxValue, 110);
    private readonly CheckBox CHK_RotoLoto1 = UiFactory.Check("CHK_RotoLoto1", "Roto Loto 1");
    private readonly CheckBox CHK_RotoLoto2 = UiFactory.Check("CHK_RotoLoto2", "Roto Loto 2");

    private readonly TrainerStatView TrainerStats = new();

    public Trainer7Window(SAV7 sav) : base("SAV_Trainer7", "Trainer Data Editor")
    {
        var lang = MainWindow.CurrentLanguage;
        BattleStyles = Translator.GetEnumTranslation<PlayerBattleStyle7>(lang);
        if (sav is not SAV7USUM)
            BattleStyles = BattleStyles[..^1]; // Nihilist is Ultra-only

        SAV = (SAV7)(Origin = sav).Clone();
        Loading = true;

        for (int i = 0; i < TreeStreaks.Length; i++)
            TreeStreaks[i] = UiFactory.NumericUpDown($"NUD_TreeStreak{i}", 0, ushort.MaxValue, 100);
        for (int i = 0; i < NUD_Surf.Length; i++)
            NUD_Surf[i] = UiFactory.NumericUpDown($"NUD_Surf{i}", 0, ushort.MaxValue, 110);

        foreach (var s in GameInfo.GenderSymbolUnicode.Take(2))
            CB_Gender.Items.Add(s);
        foreach (var v in new[] { GameVersion.SN, GameVersion.MN, GameVersion.US, GameVersion.UM })
            CB_Game.Items.Add(v.ToString());
        foreach (var s in new[] { "Unlocked", "Learned" })
            CB_BallThrowTypeListMode.Items.Add(s);
        foreach (var s in new[] { "Base Fashion", "Full Legal", "Everything" })
            CB_Fashion.Items.Add(s);
        foreach (var s in new[] { "0", "1", "2" })
            CB_CameraVersion.Items.Add(s);

        BuildLayout();
        GetComboBoxes();
        GetTextBoxes();

        TrainerStats.GetToolTipText = UpdateTip;
        CB_Fashion.SelectedIndex = 1;

        if (SAV is SAV7USUM)
            LoadUltraData();
        else
            RemoveTab("Tab_Ultra");

        // after SetBody: translation would otherwise overwrite the computed offset label
        TrainerStats.LoadRecords(SAV, RecordLists.RecordList_7);
        Loading = false;
    }

    #region Layout

    private void BuildLayout()
    {
        var tabs = new TabControl { Name = "TC_Editor" };

        var main = UiFactory.FormGrid(13);
        UiFactory.AddFormRow(main, 0, UiFactory.Label("L_TrainerName", "Trainer Name:"), UiFactory.Row(TB_OTName, CB_Gender));
        UiFactory.AddFormRow(main, 1, UiFactory.Label("L_Game", "Game:"), CB_Game);
        UiFactory.AddFormRow(main, 2, UiFactory.Label("L_TrainerID", "Trainer ID:"), trainerID1);
        UiFactory.AddFormRow(main, 3, UiFactory.Label("L_Money", "Money:"), UiFactory.Row(MT_Money, B_MaxCash));
        UiFactory.AddFormRow(main, 4, UiFactory.Label("L_Language", "Language:"), CB_Language);
        UiFactory.AddFormRow(main, 5, UiFactory.Label("L_Country", "Country:"), CB_Country);
        UiFactory.AddFormRow(main, 6, UiFactory.Label("L_Region", "Region:"), CB_Region);
        UiFactory.AddFormRow(main, 7, UiFactory.Label("L_3DSReg", "3DS Region:"), CB_3DSReg);
        UiFactory.AddFormRow(main, 8, UiFactory.Label("L_AlolaTime", "Alola Time:"), CB_AlolaTime);
        UiFactory.AddFormRow(main, 9, UiFactory.Label("L_PlayTime", "Play Time:"), UiFactory.Row(MT_Hours, MT_Minutes, MT_Seconds));
        UiFactory.AddFormRow(main, 10, UiFactory.Label("L_BP", "BP / Festa Coins:"), UiFactory.Row(NUD_BP, NUD_FC));
        UiFactory.AddFormRow(main, 11, L_Vivillon, CB_Vivillon);
        UiFactory.AddFormRow(main, 12, UiFactory.Label("L_PlazaName", "Plaza Name:"), TB_PlazaName);

        var dates = UiFactory.FormGrid(3);
        UiFactory.AddFormRow(dates, 0, UiFactory.Label("L_AdventureStart", "Adventure Started:"), UiFactory.Row(CAL_AdventureStartDate, CAL_AdventureStartTime));
        UiFactory.AddFormRow(dates, 1, UiFactory.Label("L_HoF", "Hall of Fame:"), UiFactory.Row(CAL_HoFDate, CAL_HoFTime));
        UiFactory.AddFormRow(dates, 2, L_LastSaved, UiFactory.Row(CAL_LastSavedDate, CAL_LastSavedTime));

        GB_Map = new GroupBoxView("GB_Map", "Map Position", UiFactory.Row(
            UiFactory.Label("L_M", "M:"), NUD_M,
            UiFactory.Label("L_X", "X:"), NUD_X,
            UiFactory.Label("L_Z", "Z:"), NUD_Z,
            UiFactory.Label("L_Y", "Y:"), NUD_Y,
            UiFactory.Label("L_R", "R:"), NUD_R));

        var appearance = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_SkinColor", "Skin Color:"), CB_SkinColor),
            UiFactory.Row(UiFactory.Label("L_BallThrowType", "Ball Throw:"), CB_BallThrowType),
            UiFactory.Row(UiFactory.Label("L_DaysFromRefreshed", "Days From Refreshed:"), NUD_DaysFromRefreshed),
            UiFactory.Row(CB_Fashion, B_Fashion),
            UiFactory.Row(CHK_UnlockMega, CHK_UnlockZMove),
            UiFactory.Row(CHK_UnlockSuperSingles, CHK_UnlockSuperDoubles, CHK_UnlockSuperMulti));

        var overview = UiFactory.Column(main, dates, GB_Map, appearance);
        tabs.Items.Add(new TabItem { Name = "Tab_Overview", Header = "Overview", Content = new ScrollViewer { Content = overview, MaxHeight = 560 } });

        tabs.Items.Add(new TabItem { Name = "Tab_Records", Header = "Records", Content = TrainerStats });

        // Poké Finder + Battle Tree
        var finder = UiFactory.FormGrid(5);
        UiFactory.AddFormRow(finder, 0, UiFactory.Label("L_SnapCount", "Snaps:"), NUD_SnapCount);
        UiFactory.AddFormRow(finder, 1, UiFactory.Label("L_ThumbsTotal", "Thumbs Total:"), NUD_ThumbsTotal);
        UiFactory.AddFormRow(finder, 2, UiFactory.Label("L_ThumbsRecord", "Thumbs Record:"), NUD_ThumbsRecord);
        UiFactory.AddFormRow(finder, 3, UiFactory.Label("L_CameraVersion", "Camera Version:"), CB_CameraVersion);
        UiFactory.AddFormRow(finder, 4, null, CHK_Gyro);

        var treeGrid = new Grid { ColumnSpacing = 6, RowSpacing = 3 };
        for (int i = 0; i < 4; i++)
            treeGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        string[] rowNames = ["Regular Current", "Regular Max", "Super Current", "Super Max"];
        for (int r = 0; r < 4; r++)
        {
            treeGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var label = UiFactory.Label($"L_Tree{r}", rowNames[r]);
            label.MinWidth = 130;
            UiFactory.SetRowCol(label, r, 0);
            treeGrid.Children.Add(label);
            for (int c = 0; c < 3; c++)
            {
                var nud = TreeStreaks[(r * 3) + c];
                UiFactory.SetRowCol(nud, r, c + 1);
                treeGrid.Children.Add(nud);
            }
        }
        var finderTab = UiFactory.Column(
            new GroupBoxView("GB_PokeFinder", "Poké Finder", finder),
            new GroupBoxView("GB_BattleTree", "Battle Tree", treeGrid));
        tabs.Items.Add(new TabItem { Name = "Tab_Finder", Header = "Poké Finder", Content = new ScrollViewer { Content = finderTab, MaxHeight = 560 } });

        // Flags
        LB_Stamps.ItemsSource = StampItems;
        LB_BallThrowTypeUnlocked.ItemsSource = UnlockedItems;
        LB_BallThrowTypeLearned.ItemsSource = LearnedItems;
        var flags = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        flags.Children.Add(UiFactory.Column(UiFactory.Label("L_FlyDest", "Fly Destinations"), CLB_FlyDest, B_AllFlyDest));
        flags.Children.Add(UiFactory.Column(UiFactory.Label("L_MapUnmask", "Map Reveal"), CLB_MapUnmask, B_AllMapUnmask));
        flags.Children.Add(UiFactory.Column(
            UiFactory.Label("L_Stamps", "Stamps"), LB_Stamps,
            CB_BallThrowTypeListMode, LB_BallThrowTypeUnlocked, LB_BallThrowTypeLearned));
        tabs.Items.Add(new TabItem { Name = "Tab_Flags", Header = "Flags", Content = new ScrollViewer { Content = flags, MaxHeight = 560 } });

        // Ultra
        var ultra = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(ultra, 0, UiFactory.Label("L_Surf", "Surf Scores:"), UiFactory.Row(NUD_Surf));
        UiFactory.AddFormRow(ultra, 1, UiFactory.Label("L_RotomOT", "Rotom OT:"), TB_RotomOT);
        UiFactory.AddFormRow(ultra, 2, UiFactory.Label("L_RotomAffection", "Rotom Affection:"), NUD_RotomAffection);
        UiFactory.AddFormRow(ultra, 3, null, UiFactory.Row(CHK_RotoLoto1, CHK_RotoLoto2));
        tabs.Items.Add(new TabItem { Name = "Tab_Ultra", Header = "Ultra", Content = ultra });

        SetBody(tabs);

        B_MaxCash.Click += (_, _) => MT_Money.Text = "9999999";
        CB_Country.SelectionChanged += (_, _) => UpdateCountry();
        CB_Gender.SelectionChanged += (_, _) => UpdateSkinColor();
        CB_BallThrowTypeListMode.SelectionChanged += (_, _) => UpdateBattleStyleList();
        LB_BallThrowTypeUnlocked.SelectionChanged += (_, _) => KeepFirstSelected(LB_BallThrowTypeUnlocked, 2);
        LB_BallThrowTypeLearned.SelectionChanged += (_, _) => KeepFirstSelected(LB_BallThrowTypeLearned, 1);
        B_AllFlyDest.Click += (_, _) => CLB_FlyDest.SetAllChecked(true);
        B_AllMapUnmask.Click += (_, _) => CLB_MapUnmask.SetAllChecked(true);
        B_Fashion.Click += async (_, _) => await ClickFashion();
        TB_OTName.AttachClick(async mods =>
        {
            if (mods == KeyModifiers.Control)
                await TrashEditorWindow.ShowAsync(this, TB_OTName, SAV, SAV.MyStatus.OriginalTrainerTrash.ToArray());
        });
        foreach (var nud in new[] { NUD_M, NUD_X, NUD_Z, NUD_Y, NUD_R })
            nud.ValueChanged += (_, _) => { if (!Loading) MapUpdated = true; };
    }

    private void RemoveTab(string name)
    {
        if (Body is not TabControl tabs)
            return;
        var tab = tabs.Items.OfType<TabItem>().FirstOrDefault(z => z.Name == name);
        if (tab is not null)
            tabs.Items.Remove(tab);
    }

    #endregion

    #region Load

    private void GetComboBoxes()
    {
        var sources = GameInfo.Sources;
        CB_3DSReg.SetItems(sources.Regions);
        CB_Language.SetItems(GameInfo.LanguageDataSource(SAV.Generation, SAV.Context));
        CB_AlolaTime.SetItems(GetAlolaTimeList());
        CB_Country.SetCountrySubRegion("countries");

        CB_SkinColor.Items.Clear();
        foreach (var s in Translator.GetEnumTranslation<PlayerSkinColor7>(MainWindow.CurrentLanguage))
            CB_SkinColor.Items.Add(s);

        var strings = GameInfo.Strings;
        L_Vivillon.Text = strings.Species[(int)Species.Vivillon] + ":";
        CB_Vivillon.Items.Clear();
        foreach (var f in FormConverter.GetFormList((int)Species.Vivillon, strings.types, strings.forms, GameInfo.GenderSymbolUnicode, SAV.Context))
            CB_Vivillon.Items.Add(f);

        CB_BallThrowType.Items.Clear();
        UnlockedItems.Clear();
        LearnedItems.Clear();
        foreach (var t in BattleStyles)
        {
            CB_BallThrowType.Items.Add(t);
            UnlockedItems.Add(t);
            LearnedItems.Add(t);
        }

        StampItems.Clear();
        foreach (var s in Translator.GetEnumTranslation<Stamp7>(MainWindow.CurrentLanguage))
            StampItems.Add(s);
    }

    private static ComboItem[] GetAlolaTimeList()
    {
        var list = new ComboItem[24];
        for (int i = 1; i < list.Length; i++)
            list[i] = new ComboItem($"+{i:00} Hours", i * 60 * 60);
        list[0] = new ComboItem("Sun Time", 24 * 60 * 60);
        list[12] = new ComboItem("Moon Time", 12 * 60 * 60);
        return list;
    }

    private void GetTextBoxes()
    {
        CB_Game.SelectedIndex = Math.Clamp(SAV.Version - GameVersion.SN, 0, CB_Game.ItemCount - 1);
        CB_Gender.SelectedIndex = SAV.Gender;
        TB_OTName.Text = SAV.OT;
        trainerID1.LoadTrainer(SAV);
        MT_Money.Text = SAV.Money.ToString();

        CB_Country.SetValue(SAV.Country);
        UpdateCountry();
        CB_Region.SetValue(SAV.Region);
        CB_3DSReg.SetValue(SAV.ConsoleRegion);
        CB_Language.SetValue(SAV.Language);

        var timeA = SAV.GameTime.AlolaTime;
        if (timeA == 0)
            timeA = 24 * 60 * 60; // patch up bad values written by older program versions
        if (timeA == 9_999_999)
            CB_AlolaTime.IsEnabled = false; // alola time does not exist yet
        else if (!CB_AlolaTime.SetValue((int)timeA))
            CB_AlolaTime.IsEnabled = false;

        NUD_M.Value = SAV.Situation.M;
        try
        {
            NUD_X.Value = (decimal)(SAV.Situation.X / 60.0);
            NUD_Z.Value = (decimal)(SAV.Situation.Z / 60.0);
            NUD_Y.Value = (decimal)(SAV.Situation.Y / 60.0);
            NUD_R.Value = (decimal)(Math.Atan2(SAV.Situation.RZ, SAV.Situation.RW) * 360.0 / Math.PI);
        }
        catch (OverflowException)
        {
            GB_Map.IsEnabled = false;
        }

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

        DateUtil.GetDateTime2000(SAV.SecondsToStart, out var date, out var time);
        CAL_AdventureStartDate.SelectedDate = UiFactory.ToOffset(date);
        CAL_AdventureStartTime.SelectedTime = time.TimeOfDay;
        DateUtil.GetDateTime2000(SAV.SecondsToFame, out date, out time);
        CAL_HoFDate.SelectedDate = UiFactory.ToOffset(date);
        CAL_HoFTime.SelectedTime = time.TimeOfDay;

        NUD_BP.SetValueClamped(SAV.Misc.BP);
        NUD_FC.SetValueClamped(SAV.Festa.FestaCoins);

        NUD_SnapCount.SetValueClamped(SAV.PokeFinder.SnapCount);
        NUD_ThumbsTotal.SetValueClamped(SAV.PokeFinder.ThumbsTotalValue);
        NUD_ThumbsRecord.SetValueClamped(SAV.PokeFinder.ThumbsHighValue);
        CB_CameraVersion.SelectedIndex = Math.Min(CB_CameraVersion.ItemCount - 1, SAV.PokeFinder.CameraVersion);
        CHK_Gyro.IsChecked = SAV.PokeFinder.GyroFlag;

        var bt = SAV.BattleTree;
        for (int i = 0; i < 3; i++)
        {
            TreeStreaks[i].SetValueClamped(bt.GetTreeStreak(i, super: false, max: false));
            TreeStreaks[3 + i].SetValueClamped(bt.GetTreeStreak(i, super: false, max: true));
            TreeStreaks[6 + i].SetValueClamped(bt.GetTreeStreak(i, super: true, max: false));
            TreeStreaks[9 + i].SetValueClamped(bt.GetTreeStreak(i, super: true, max: true));
        }

        CB_SkinColor.SelectedIndex = Math.Clamp(SAV.MyStatus.DressUpSkinColor, 0, CB_SkinColor.ItemCount - 1);
        TB_PlazaName.Text = SAV.Festa.FestivalPlazaName;
        CB_Vivillon.SelectedIndex = SAV.Misc.Vivillon < CB_Vivillon.ItemCount ? SAV.Misc.Vivillon : -1;
        NUD_DaysFromRefreshed.SetValueClamped(SAV.Misc.DaysFromRefreshed);

        if ((sbyte)SAV.MyStatus.BallThrowType >= 0 && SAV.MyStatus.BallThrowType < CB_BallThrowType.ItemCount)
            CB_BallThrowType.SelectedIndex = SAV.MyStatus.BallThrowType;

        if (SAV is SAV7SM)
            LoadThrowTypeLists();
        else
            CB_BallThrowTypeListMode.IsVisible = LB_BallThrowTypeLearned.IsVisible = LB_BallThrowTypeUnlocked.IsVisible = false;

        uint stampBits = SAV.Misc.Stamps;
        LB_Stamps.SelectedItems?.Clear();
        for (int i = 0; i < StampItems.Count; i++)
        {
            if ((stampBits & (1 << i)) != 0)
                LB_Stamps.SelectedItems?.Add(StampItems[i]);
        }

        CHK_UnlockSuperSingles.IsChecked = SAV.EventWork.GetEventFlag(333);
        CHK_UnlockSuperDoubles.IsChecked = SAV.EventWork.GetEventFlag(334);
        CHK_UnlockSuperMulti.IsChecked = SAV.EventWork.GetEventFlag(335);
        CHK_UnlockMega.IsChecked = SAV.MyStatus.MegaUnlocked;
        CHK_UnlockZMove.IsChecked = SAV.MyStatus.ZMoveUnlocked;

        LoadMapFlyToData();
    }

    private void LoadThrowTypeLists()
    {
        const int unlockStart = 292;
        const int learnedStart = 3479;
        SelectIndex(LB_BallThrowTypeUnlocked, UnlockedItems, 0, true);
        SelectIndex(LB_BallThrowTypeUnlocked, UnlockedItems, 1, true);
        for (int i = 2; i < BattleStyles.Length; i++)
            SelectIndex(LB_BallThrowTypeUnlocked, UnlockedItems, i, SAV.EventWork.GetEventFlag(unlockStart + i));

        SelectIndex(LB_BallThrowTypeLearned, LearnedItems, 0, true);
        for (int i = 1; i < BattleStyles.Length; i++)
            SelectIndex(LB_BallThrowTypeLearned, LearnedItems, i, SAV.EventWork.GetEventFlag(learnedStart + i));

        CB_BallThrowTypeListMode.SelectedIndex = 0;
    }

    private static void SelectIndex(ListBox lb, ObservableCollection<string> items, int index, bool selected)
    {
        if ((uint)index >= items.Count || lb.SelectedItems is not { } sel)
            return;
        var item = items[index];
        if (selected && !sel.Contains(item))
            sel.Add(item);
        else if (!selected && sel.Contains(item))
            sel.Remove(item);
    }

    private static bool IsSelected(ListBox lb, ObservableCollection<string> items, int index)
        => (uint)index < items.Count && lb.SelectedItems?.Contains(items[index]) == true;

    private void LoadMapFlyToData()
    {
        var metLocationList = GameInfo.GetLocationList(GameVersion.US, EntityContext.Gen7, false);
        int[] flyDestNameIndex = [
            -1,24,34,8,20,38,12,46,40,30,
            70,68,78,86,74,104,82,58,90,72,76,92,62,
            132,136,138,114,118,144,130,154,140,
            172,184,180,174,176,156,186,
            188,-1,-1,
            198,202,110,204,
        ];
        if (SAV.Version is GameVersion.UM or GameVersion.MN)
        {
            flyDestNameIndex[28] = 142;
            flyDestNameIndex[36] = 178;
        }
        FlyDestFlagOfs = [
            44,43,45,40,41,49,42,47,46,48,
            50,54,39,57,51,55,59,52,58,53,61,60,56,
            62,66,67,64,65,273,270,37,38,
            69,74,72,71,276,73,70,
            75,332,334,
            331,333,335,336,
        ];
        string[] flyDestAltName = ["My House", "Photo Club (Hau'oli)", "Photo Club (Konikoni)"];
        CLB_FlyDest.ClearItems();
        for (int i = 0, u = 0, m = flyDestNameIndex.Length - (SAV is SAV7USUM ? 0 : 6); i < m; i++)
        {
            var dest = flyDestNameIndex[i];
            var name = dest < 0 ? flyDestAltName[u++] : metLocationList.First(v => v.Value == dest).Text;
            CLB_FlyDest.Add(name, SAV.EventWork.GetEventFlag(SkipFlag + FlyDestFlagOfs[i]));
        }

        int[] mapUnmaskNameIndex = [
            6,8,24,-1,18,-1,20,22,12,10,14,
            70,50,68,52,74,54,56,58,60,72,62,64,
            132,192,106,108,122,112,114,126,116,118,120,154,
            172,158,160,162,164,166,168,170,
            188,
            198,202,110,204,
        ];
        MapUnmaskFlagOfs = [
            5,76,82,91,79,84,80,81,77,78,83,
            19,10,18,11,21,12,13,14,15,20,16,17,
            33,34,30,31,98,92,93,94,95,96,97,141,
            173,144,145,146,147,148,149,172,
            181,
            409,297,32,296,
        ];
        string[] mapUnmaskAltName = ["Melemele Sea (East)", "Melemele Sea (West)"];
        CLB_MapUnmask.ClearItems();
        for (int i = 0, u = 0, m = mapUnmaskNameIndex.Length - (SAV is SAV7USUM ? 0 : 4); i < m; i++)
        {
            var dest = mapUnmaskNameIndex[i];
            var name = dest < 0 ? mapUnmaskAltName[u++] : metLocationList.First(v => v.Value == dest).Text;
            CLB_MapUnmask.Add(name, SAV.EventWork.GetEventFlag(SkipFlag + MapUnmaskFlagOfs[i]));
        }
    }

    private void LoadUltraData()
    {
        for (int i = 0; i < NUD_Surf.Length; i++)
            NUD_Surf[i].SetValueClamped(SAV.Misc.GetSurfScore(i));
        TB_RotomOT.Text = SAV.FieldMenu.RotomOT;
        NUD_RotomAffection.SetValueClamped(SAV.FieldMenu.RotomAffection);
        CHK_RotoLoto1.IsChecked = SAV.FieldMenu.RotomLoto1;
        CHK_RotoLoto2.IsChecked = SAV.FieldMenu.RotomLoto2;
    }

    #endregion

    #region Handlers

    private void UpdateCountry()
    {
        var index = CB_Country.GetSelectedItem()?.Value ?? 0;
        if (index > 0)
            CB_Region.SetCountrySubRegion($"sr_{index:000}");
    }

    private void UpdateSkinColor()
    {
        if (Loading)
            return;
        CB_SkinColor.SelectedIndex = (CB_SkinColor.SelectedIndex & ~0x1) | (CB_Gender.SelectedIndex & 1);
    }

    private void UpdateBattleStyleList()
    {
        bool unlocked = CB_BallThrowTypeListMode.SelectedIndex == 0;
        LB_BallThrowTypeUnlocked.IsVisible = unlocked;
        LB_BallThrowTypeLearned.IsVisible = !unlocked;
    }

    /// <summary>The first entries are always available and cannot be deselected.</summary>
    private void KeepFirstSelected(ListBox lb, int count)
    {
        if (Loading)
            return;
        var items = ReferenceEquals(lb, LB_BallThrowTypeUnlocked) ? UnlockedItems : LearnedItems;
        for (int i = 0; i < count; i++)
            SelectIndex(lb, items, i, true);
    }

    private string? UpdateTip(int index)
    {
        if (index != 2) // Storyline Completed Time
            return null;
        var seconds = DateUtil.GetSecondsFrom2000(GetDate(CAL_AdventureStartDate), GetTime(CAL_AdventureStartTime));
        return DateUtil.ConvertDateValueToString(SAV.GetRecord(index), seconds);
    }

    private static DateTime GetDate(DatePicker picker) => picker.SelectedDate?.Date ?? new DateTime(2000, 1, 1);
    private static DateTime GetTime(TimePicker picker) => new DateTime(2000, 1, 1) + (picker.SelectedTime ?? TimeSpan.Zero);

    private async Task ClickFashion()
    {
        var prompt = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo,
            "Modifying Fashion Items will clear existing fashion unlock data.", "Continue?");
        if (prompt != DialogResult.Yes)
            return;

        SAV.Fashion.Clear();
        switch (CB_Fashion.SelectedIndex)
        {
            case 0:
                SAV.Fashion.Reset();
                break;
            case 1:
            case 2:
            {
                var suffix = CB_Fashion.SelectedIndex == 2 ? "_illegal" : string.Empty;
                var game = SAV is SAV7USUM ? "uu" : "sm";
                var gender = SAV.Gender == 0 ? "m" : "f";
                var payload = AppResources.GetBytes($"fashion_{gender}_{game}{suffix}");
                if (payload is null)
                {
                    await AppDialogs.Error(this, "Fashion payload resource not found.");
                    return;
                }
                SAV.Fashion.ImportPayload(payload);
                break;
            }
        }
    }

    #endregion

    #region Save

    private void SaveTrainerInfo()
    {
        SAV.Version = (GameVersion)(CB_Game.SelectedIndex + 30);
        SAV.Gender = (byte)Math.Max(0, CB_Gender.SelectedIndex);
        SAV.Money = Util.ToUInt32(MT_Money.Text ?? string.Empty);
        SAV.Region = (byte)(CB_Region.GetSelectedItem()?.Value ?? 0);
        SAV.Country = (byte)(CB_Country.GetSelectedItem()?.Value ?? 0);
        SAV.ConsoleRegion = (byte)(CB_3DSReg.GetSelectedItem()?.Value ?? 0);
        SAV.Language = CB_Language.GetSelectedItem()?.Value ?? 0;
        if (CB_AlolaTime.IsEnabled)
            SAV.GameTime.AlolaTime = (ulong)(CB_AlolaTime.GetSelectedItem()?.Value ?? 0);

        trainerID1.SaveTrainer(SAV);
        if (SAV.OT != TB_OTName.Text) // only modify if changed, to preserve trash bytes
            SAV.OT = TB_OTName.Text ?? string.Empty;

        if (GB_Map.IsEnabled && MapUpdated)
        {
            SAV.Situation.M = (int)(NUD_M.Value ?? 0);
            SAV.Situation.X = (float)((NUD_X.Value ?? 0) * 60);
            SAV.Situation.Z = (float)((NUD_Z.Value ?? 0) * 60);
            SAV.Situation.Y = (float)((NUD_Y.Value ?? 0) * 60);
            var angle = (double)(NUD_R.Value ?? 0) * Math.PI / 360.0;
            SAV.Situation.RX = 0;
            SAV.Situation.RZ = (float)Math.Sin(angle);
            SAV.Situation.RY = 0;
            SAV.Situation.RW = (float)Math.Cos(angle);
            SAV.Situation.UpdateOverworldCoordinates();
        }

        SAV.PlayedHours = (ushort)Util.ToUInt32(MT_Hours.Text ?? string.Empty);
        SAV.PlayedMinutes = (ushort)(Util.ToUInt32(MT_Minutes.Text ?? string.Empty) % 60);
        SAV.PlayedSeconds = (ushort)(Util.ToUInt32(MT_Seconds.Text ?? string.Empty) % 60);

        SAV.SecondsToStart = (uint)DateUtil.GetSecondsFrom2000(GetDate(CAL_AdventureStartDate), GetTime(CAL_AdventureStartTime));
        SAV.SecondsToFame = (uint)DateUtil.GetSecondsFrom2000(GetDate(CAL_HoFDate), GetTime(CAL_HoFTime));

        if (SAV.Played.LastSavedDate.HasValue)
        {
            var d = GetDate(CAL_LastSavedDate);
            var t = CAL_LastSavedTime.SelectedTime ?? TimeSpan.Zero;
            SAV.Played.LastSavedDate = new DateTime(d.Year, d.Month, d.Day, t.Hours, t.Minutes, 0);
        }

        SAV.Misc.BP = (uint)(NUD_BP.Value ?? 0);
        SAV.Festa.FestaCoins = (int)(NUD_FC.Value ?? 0);
    }

    private void SavePokeFinder()
    {
        SAV.PokeFinder.SnapCount = (uint)(NUD_SnapCount.Value ?? 0);
        SAV.PokeFinder.ThumbsTotalValue = (uint)(NUD_ThumbsTotal.Value ?? 0);
        SAV.PokeFinder.ThumbsHighValue = (uint)(NUD_ThumbsRecord.Value ?? 0);
        SAV.PokeFinder.CameraVersion = (ushort)Math.Max(0, CB_CameraVersion.SelectedIndex);
        SAV.PokeFinder.GyroFlag = CHK_Gyro.IsChecked == true;
    }

    private void SaveBattleTree()
    {
        var bt = SAV.BattleTree;
        for (int i = 0; i < 3; i++)
        {
            bt.SetTreeStreak((int)(TreeStreaks[i].Value ?? 0), i, super: false, max: false);
            bt.SetTreeStreak((int)(TreeStreaks[3 + i].Value ?? 0), i, super: false, max: true);
            bt.SetTreeStreak((int)(TreeStreaks[6 + i].Value ?? 0), i, super: true, max: false);
            bt.SetTreeStreak((int)(TreeStreaks[9 + i].Value ?? 0), i, super: true, max: true);
        }
    }

    private async Task SaveTrainerAppearance()
    {
        byte gender = (byte)(CB_Gender.SelectedIndex & 1);
        int skin = CB_SkinColor.SelectedIndex & 1;
        if (SAV.MyStatus.DressUpSkinColor == CB_SkinColor.SelectedIndex)
            return;

        if (SAV.Gender == skin)
        {
            SAV.MyStatus.DressUpSkinColor = CB_SkinColor.SelectedIndex;
            return;
        }

        var gStr = CB_Gender.Items[gender]?.ToString();
        var sStr = CB_Gender.Items[skin]?.ToString();
        var prompt = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo,
            $"Gender-Skin mismatch:{Environment.NewLine}Gender: {gStr}, Skin: {sStr}", "Save selected Skin Color?");
        if (prompt == DialogResult.Yes)
            SAV.MyStatus.DressUpSkinColor = CB_SkinColor.SelectedIndex;
    }

    private void SaveThrowType()
    {
        if (CB_BallThrowType.SelectedIndex >= 0)
            SAV.MyStatus.BallThrowType = (byte)CB_BallThrowType.SelectedIndex;

        if (SAV is not SAV7SM) // Ultra keeps the unlock flags in the flag editor
            return;

        const int unlockStart = 292;
        const int learnedStart = 3479;
        for (int i = 2; i < BattleStyles.Length; i++)
            SAV.EventWork.SetEventFlag(unlockStart + i, IsSelected(LB_BallThrowTypeUnlocked, UnlockedItems, i));
        for (int i = 1; i < BattleStyles.Length; i++)
            SAV.EventWork.SetEventFlag(learnedStart + i, IsSelected(LB_BallThrowTypeLearned, LearnedItems, i));
    }

    private void SaveFlags()
    {
        uint bits = 0;
        for (int i = 0; i < StampItems.Count; i++)
        {
            if (LB_Stamps.SelectedItems?.Contains(StampItems[i]) == true)
                bits |= 1u << i;
        }
        SAV.Misc.Stamps = bits;

        SAV.EventWork.SetEventFlag(333, CHK_UnlockSuperSingles.IsChecked == true);
        SAV.EventWork.SetEventFlag(334, CHK_UnlockSuperDoubles.IsChecked == true);
        SAV.EventWork.SetEventFlag(335, CHK_UnlockSuperMulti.IsChecked == true);
        SAV.MyStatus.MegaUnlocked = CHK_UnlockMega.IsChecked == true;
        SAV.MyStatus.ZMoveUnlocked = CHK_UnlockZMove.IsChecked == true;

        for (int i = 0; i < CLB_FlyDest.Count; i++)
            SAV.EventWork.SetEventFlag(SkipFlag + FlyDestFlagOfs[i], CLB_FlyDest.GetItemChecked(i));
        for (int i = 0; i < CLB_MapUnmask.Count; i++)
            SAV.EventWork.SetEventFlag(SkipFlag + MapUnmaskFlagOfs[i], CLB_MapUnmask.GetItemChecked(i));
    }

    private async Task SaveUltraData()
    {
        for (int i = 0; i < NUD_Surf.Length; i++)
            SAV.Misc.SetSurfScore(i, (int)(NUD_Surf[i].Value ?? 0));

        SAV.FieldMenu.RotomAffection = (ushort)(NUD_RotomAffection.Value ?? 0);
        SAV.FieldMenu.RotomLoto1 = CHK_RotoLoto1.IsChecked == true;
        SAV.FieldMenu.RotomLoto2 = CHK_RotoLoto2.IsChecked == true;

        if (TB_RotomOT.Text != TB_OTName.Text && TB_OTName.Text != SAV.OT)
        {
            var prompt = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo,
                "Rotom OT does not match OT name. Update Rotom OT name with OT name?");
            if (prompt == DialogResult.Yes)
            {
                SAV.FieldMenu.RotomOT = TB_OTName.Text ?? string.Empty;
                return;
            }
        }
        SAV.FieldMenu.RotomOT = TB_RotomOT.Text ?? string.Empty;
    }

    #endregion

    protected override void OnSave() => _ = SaveAsync();

    private async Task SaveAsync()
    {
        SaveTrainerInfo();
        SavePokeFinder();
        SaveBattleTree();
        await SaveTrainerAppearance();
        SAV.Misc.DaysFromRefreshed = (byte)(NUD_DaysFromRefreshed.Value ?? 0);
        SaveThrowType();

        SAV.Festa.FestivalPlazaName = TB_PlazaName.Text ?? string.Empty;
        if (CB_Vivillon.SelectedIndex >= 0)
            SAV.Misc.Vivillon = CB_Vivillon.SelectedIndex;

        SaveFlags();
        if (SAV is SAV7USUM)
            await SaveUltraData();

        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
