using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Core;
using static System.Buffers.Binary.BinaryPrimitives;
using static PKHeX.Avalonia.Drawing.PoketchDotMatrix;
using Color = System.Drawing.Color;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4;

/// <summary>
/// Miscellaneous editor for Generation 4 saves (port of the WinForms <c>SAV_Misc4</c>).
/// </summary>
/// <remarks>
/// Keeps the original tab set and its per-game trimming: Sinnoh saves get the Pokétch and the Poffin case,
/// HG/SS gets the Pokéwalker, the PokéGear rolodex and the Pokéathlon points, and Diamond/Pearl hides the
/// Battle Frontier facilities that only exist from Platinum onwards.
/// </remarks>
public sealed class Misc4Window : SaveEditorWindow
{
    private readonly SAV4 Origin;
    private readonly SAV4 SAV;
    private readonly Hall4? Hall;
    private readonly Record4 Record;

    private readonly string[] seals, accessories, backdrops, poketchapps;
    private readonly string[] backdropsSorted;

    private readonly TabControl TC_Misc = new() { Name = "TC_Misc" };
    private readonly List<TabItem> Tabs = [];

    #region Main

    private readonly NumericUpDown NUD_Coin = UiFactory.NumericUpDown("NUD_Coin", 0, ushort.MaxValue, 120);
    private readonly NumericUpDown NUD_BP = UiFactory.NumericUpDown("NUD_BP", 0, 9999, 120);
    private readonly TextBlock L_Coin = UiFactory.Label("L_Coin", "Coin:");
    private readonly TextBlock L_BP = UiFactory.Label("L_BP", "BP:");
    private readonly TextBlock L_CurrentMap = UiFactory.Label("L_CurrentMap", "Current Map");
    private readonly ComboBox CB_UpgradeMap = UiFactory.StringCombo("CB_UpgradeMap", 200);
    private readonly CheckedListView CLB_FlyDest = new() { Name = "CLB_FlyDest", Width = 240, Height = 300 };
    private readonly Button B_AllFlyDest = UiFactory.Button("B_AllFlyDest", "Check All");
    private readonly TextBlock L_UGFlags = UiFactory.Label("L_UGFlags", "Flags Obtained:");
    private readonly NumericUpDown NUD_UGFlags = UiFactory.NumericUpDown("NUD_UGFlags", 0, 999999, 120);
    private readonly TextBlock L_PokeathlonPoints = UiFactory.Label("L_PokeathlonPoints", "Pokeathlon Points:");
    private readonly NumericUpDown NUD_PokeathlonPoints = UiFactory.NumericUpDown("NUD_PokeathlonPoints", 0, 9999999, 120);

    private readonly CheckedListView CLB_Poketch = new() { Name = "CLB_Poketch", Width = 220, Height = 280 };
    private readonly ComboBox CB_CurrentApp = UiFactory.StringCombo("CB_CurrentApp", 180);
    private readonly Button B_AllPoketch = UiFactory.Button("B_AllPoketch", "Give All");
    private readonly Image PB_DotArtist = new() { Name = "PB_DotArtist", Width = DotMatrixWidth * DotMatrixUpscaleFactor, Height = DotMatrixHeight * DotMatrixUpscaleFactor };
    private GroupBoxView GB_Poketch = null!;
    private byte[] DotArtistByte = [];

    #endregion

    #region Battle Frontier

    private readonly Button[] PrintButtonA;
    private readonly ComboBox CB_Stats1 = UiFactory.StringCombo("CB_Stats1", 160);
    private readonly ComboBox CB_Stats2 = UiFactory.StringCombo("CB_Stats2", 160);
    private readonly RadioButton RB_Stats3_01 = new() { Name = "RB_Stats3_01", Content = "Lv. 50", GroupName = "BF4Record" };
    private readonly RadioButton RB_Stats3_02 = new() { Name = "RB_Stats3_02", Content = "Open", GroupName = "BF4Record" };
    private readonly CheckBox CHK_Continue = UiFactory.Check("CHK_Continue", "Continue");
    private readonly NumericUpDown[] StatNUDA;
    private readonly TextBlock[] StatLabelA;
    private readonly NumericUpDown[] HallNUDA;
    private readonly CheckBox CHK_HallCurrent = UiFactory.Check("CHK_HallCurrent", "Current:");
    private readonly NumericUpDown NUD_HallStreaks = UiFactory.NumericUpDown("NUD_HallStreaks", 0, 9999, 120);
    private readonly TextBlock L_SumHall = UiFactory.Label("L_SumHall", "170");
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);
    private readonly NumericUpDown NUD_CastleRankRcv = UiFactory.NumericUpDown("NUD_CastleRankRcv", 0, 3, 95);
    private readonly NumericUpDown NUD_CastleRankItem = UiFactory.NumericUpDown("NUD_CastleRankItem", 0, 3, 95);
    private readonly NumericUpDown NUD_CastleRankInfo = UiFactory.NumericUpDown("NUD_CastleRankInfo", 0, 2, 95);
    private GroupBoxView GB_Prints = null!;
    private GroupBoxView GB_Hall = null!;
    private GroupBoxView GB_Castle = null!;

    private readonly int[][] BFF;
    private readonly int PrintIndexStart;
    private bool editing;
    private string[][] BFT = null!;
    private int[][] BFV = null!;
    private bool HallStatUpdated;
    private ushort species = ushort.MaxValue;

    #endregion

    #region Pokéwalker

    private readonly CheckedListView CLB_WalkerCourses = new() { Name = "CLB_WalkerCourses", Width = 240, Height = 300 };
    private readonly Button B_AllWalkerCourses = UiFactory.Button("B_AllWalkerCourses", "Check All");
    private readonly NumericUpDown NUD_Watts = UiFactory.NumericUpDown("NUD_Watts", 0, 9999999, 120);
    private readonly NumericUpDown NUD_Steps = UiFactory.NumericUpDown("NUD_Steps", 0, 9999999, 120);

    #endregion

    #region Seals / Fashion

    private readonly ObservableCollection<CountRow> SealRows = [];
    private readonly ObservableCollection<CountRow> AccessoryRows = [];
    private readonly ObservableCollection<NameRow> BackdropRows = [];

    #endregion

    #region Poffins / PokéGear

    private PoffinCase4 PoffinCase = null!;
    private readonly ListBox LB_Poffins = new() { Name = "LB_Poffins", Width = 220, Height = 320 };
    private readonly ObservableCollection<string> PoffinItems = [];
    private readonly PropertyGridView PG_Poffins = new() { Name = "PG_Poffins", Width = 320, Height = 320 };
    private readonly Button B_PoffinAll = UiFactory.Button("B_PoffinAll", "Give All");
    private readonly Button B_PoffinDel = UiFactory.Button("B_PoffinDel", "Delete All");
    private readonly string[] PoffinNames = Util.GetStringList("poffin4", MainWindow.CurrentLanguage);
    private int CurrentPoffinIndex = -1;
    private bool UpdatingPoffins;

    private readonly ObservableCollection<CallerRow> RolodexRows = [];
    private readonly Button B_GearGiveAll = UiFactory.Button("B_GiveAll", "Give All");
    private readonly Button B_GearGiveAllNoTrainers = UiFactory.Button("B_GiveAllNoTrainers", "Give All Non-Trainers");
    private readonly Button B_GearDeleteAll = UiFactory.Button("B_DeleteAll", "Delete All");

    #endregion

    #region Records

    private readonly NumericUpDown NUD_Record16 = UiFactory.NumericUpDown("NUD_Record16", 0, ushort.MaxValue, 120);
    private readonly NumericUpDown NUD_Record16V = UiFactory.NumericUpDown("NUD_Record16V", 0, ushort.MaxValue, 140);
    private readonly NumericUpDown NUD_Record32 = UiFactory.NumericUpDown("NUD_Record32", 0, ushort.MaxValue, 120);
    private readonly NumericUpDown NUD_Record32V = UiFactory.NumericUpDown("NUD_Record32V", 0, uint.MaxValue, 140);

    #endregion

    public Misc4Window(SAV4 sav) : base("SAV_Misc4", "Misc Edits")
    {
        SAV = (SAV4)(Origin = sav).Clone();

        seals = GameInfo.Strings.seals;
        accessories = GameInfo.Strings.accessories;
        backdrops = GameInfo.Strings.backdrops;
        poketchapps = GameInfo.Strings.poketchapps;
        backdropsSorted = [.. backdrops.Order()];

        StatNUDA = [
            UiFactory.NumericUpDown("NUD_Stat0", 0, 9999, 100), UiFactory.NumericUpDown("NUD_Stat1", 0, 9999, 100),
            UiFactory.NumericUpDown("NUD_Stat2", 0, 9999, 100), UiFactory.NumericUpDown("NUD_Stat3", 0, 9999, 100),
        ];
        StatLabelA = [
            UiFactory.Label("L_Stat0", "Current"), UiFactory.Label("L_Stat1", "Trade"),
            UiFactory.Label("L_Stat2", "Record"), UiFactory.Label("L_Stat3", "Trade"),
        ];
        HallNUDA = new NumericUpDown[17];
        for (int i = 0; i < HallNUDA.Length; i++)
            HallNUDA[i] = UiFactory.NumericUpDown($"NUD_HallType{i + 1:00}", 0, 10, 95);
        PrintButtonA = [
            MakePrint("BTN_PrintTower", "Tower"), MakePrint("BTN_PrintFactory", "Factory"),
            MakePrint("BTN_PrintHall", "Hall"), MakePrint("BTN_PrintCastle", "Castle"),
            MakePrint("BTN_PrintArcade", "Arcade"),
        ];

        Record = SAV.Records;
        switch (sav)
        {
            case SAV4DP:
                BFF = [[0, 1, 0x5FCA, 0x04, 0x6601]];
                break;
            case SAV4Pt:
                PrintIndexStart = 79;
                BFF = [
                    [0, 1, 0x68E0, 0x04, 0x723D],
                    [1, 0, 0x68F4, 0x10, 0x7EF8],
                    [0, 0, 0x6924, 0x18, 0x7EFC],
                    [2, 0, 0x696C, 0x10, 0x7F00],
                    [0, 0, 0x699C, 0x04, 0x7F04],
                ];
                Hall = SAV.GetHall();
                break;
            case SAV4HGSS:
                PrintIndexStart = 77;
                BFF = [
                    // { BFV, BFT, addr, 1BFTlen, checkBit
                    [0, 1, 0x5264, 0x04, 0x5BC1],
                    [1, 0, 0x5278, 0x10, 0x687C],
                    [0, 0, 0x52A8, 0x18, 0x6880],
                    [2, 0, 0x52F0, 0x10, 0x6884],
                    [0, 0, 0x5320, 0x04, 0x6888],
                ];
                Hall = SAV.GetHall();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(sav), sav, null);
        }

        BuildLayout();

        if (SAV is SAV4DP)
        {
            L_CurrentMap.IsVisible = CB_UpgradeMap.IsVisible = false;
            GB_Prints.IsVisible = GB_Hall.IsVisible = GB_Castle.IsVisible = false;
        }
        else if (SAV is SAV4Pt)
        {
            L_CurrentMap.IsVisible = CB_UpgradeMap.IsVisible = false;
        }
        else
        {
            GB_Poketch.IsVisible = false;
        }

        ReadMain();
        ReadBattleFrontier();
        if (SAV is SAV4Sinnoh s)
        {
            RemoveTab("TAB_Walker");
            RemoveTab("Tab_PokeGear");
            InitializePoffins(s);
        }
        else if (SAV is SAV4HGSS hgss)
        {
            RemoveTab("Tab_Poffins");
            InitializeGear(hgss);
        }
    }

    private static Button MakePrint(string name, string text) => new()
    {
        Name = name,
        Content = text,
        Width = 80,
        BorderThickness = new global::Avalonia.Thickness(1),
        BorderBrush = global::Avalonia.Media.Brushes.Gray,
        HorizontalContentAlignment = HorizontalAlignment.Center,
    };

    private void RemoveTab(string name)
    {
        var tab = Tabs.FirstOrDefault(z => z.Name == name);
        if (tab is null)
            return;
        Tabs.Remove(tab);
        TC_Misc.Items.Remove(tab);
    }

    private void AddTab(string name, string header, Control content)
    {
        var tab = new TabItem { Name = name, Header = header, Content = content };
        Tabs.Add(tab);
        TC_Misc.Items.Add(tab);
    }

    #region Layout

    private void BuildLayout()
    {
        AddTab("TAB_Main", "Main", BuildMain());
        AddTab("TAB_BF", "Battle Frontier", BuildBattleFrontier());
        AddTab("TAB_Walker", "Pokewalker", BuildWalker());
        AddTab("Tab_Seals", "Seals", BuildSeals());
        AddTab("Tab_FashionCase", "Fashion Case", BuildFashion());
        AddTab("Tab_Poffins", "Poffins", BuildPoffins());
        AddTab("Tab_PokeGear", "PokeGear", BuildGear());
        AddTab("Tab_Records", "Records", BuildRecords());
        SetBody(TC_Misc);
    }

    private Control BuildMain()
    {
        var values = UiFactory.FormGrid(5);
        UiFactory.AddFormRow(values, 0, L_Coin, NUD_Coin);
        UiFactory.AddFormRow(values, 1, L_BP, NUD_BP);
        UiFactory.AddFormRow(values, 2, L_CurrentMap, CB_UpgradeMap);
        UiFactory.AddFormRow(values, 3, L_UGFlags, NUD_UGFlags);
        UiFactory.AddFormRow(values, 4, L_PokeathlonPoints, NUD_PokeathlonPoints);

        var fly = new GroupBoxView("GB_FlyDest", "Fly Destination", UiFactory.Column(CLB_FlyDest, B_AllFlyDest));
        GB_Poketch = new GroupBoxView("GB_Poketch", "Pokétch", UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_CurrentApp", "Current App"), CB_CurrentApp),
            UiFactory.Row(CLB_Poketch, UiFactory.Column(PB_DotArtist)),
            B_AllPoketch));

        B_AllFlyDest.Click += (_, _) => CLB_FlyDest.SetAllChecked(true);
        B_AllPoketch.Click += (_, _) => CLB_Poketch.SetAllChecked(true);
        PB_DotArtist.PointerPressed += ClickDotArtist;

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(UiFactory.Column(values, fly));
        body.Children.Add(GB_Poketch);

        // Dropping a 24x20 image on the tab replaces the dot art, as in WinForms.
        DragDrop.SetAllowDrop(body, true);
        body.AddHandler(DragDrop.DragOverEvent, OnDotArtDragOver);
        body.AddHandler(DragDrop.DropEvent, OnDotArtDrop);
        ToolTip.SetTip(PB_DotArtist, """
                                     Guide about D&D ImageFile Format
                                      width = 24px
                                      height = 20px
                                      used color count <= 4
                                      file size < 2058byte
                                     """);
        return new ScrollViewer { Content = body, MaxHeight = 620 };
    }

    private Control BuildBattleFrontier()
    {
        var prints = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var b in PrintButtonA)
        {
            b.Click += (s, _) => ClickPrint((Button)s!);
            prints.Children.Add(b);
        }
        GB_Prints = new GroupBoxView("GB_Prints", "Print", prints);

        var statGrid = UiFactory.FormGrid(4);
        for (int i = 0; i < StatNUDA.Length; i++)
            UiFactory.AddFormRow(statGrid, i, StatLabelA[i], StatNUDA[i]);
        var streaks = new GroupBoxView("GB_Streaks", "Streaks", UiFactory.Column(
            CB_Stats1, CB_Stats2,
            UiFactory.Row(RB_Stats3_01, RB_Stats3_02),
            statGrid, CHK_Continue));

        var hallTypes = UiFactory.FormGrid(HallNUDA.Length);
        ReadOnlySpan<byte> typenameIndex = [0, 9, 10, 12, 11, 14, 1, 3, 4, 2, 13, 6, 5, 7, 15, 16, 8];
        var typeNames = GameInfo.Strings.types;
        for (int i = 0; i < HallNUDA.Length; i++)
            UiFactory.AddFormRow(hallTypes, i, UiFactory.Label($"L_HallType{i + 1:00}", typeNames[typenameIndex[i]]), HallNUDA[i]);
        GB_Hall = new GroupBoxView("GB_Hall", "Battle Hall ()", UiFactory.Column(
            CB_Species,
            UiFactory.Row(CHK_HallCurrent, NUD_HallStreaks),
            new ScrollViewer { Content = hallTypes, MaxHeight = 260 },
            UiFactory.Row(UiFactory.Label("L_SumHallText", "Σ"), L_SumHall)));

        var castleGrid = UiFactory.FormGrid(1);
        UiFactory.AddFormRow(castleGrid, 0, UiFactory.Label("L_CastleRank01", "Recovery / Item / Info"),
            UiFactory.Row(NUD_CastleRankRcv, NUD_CastleRankItem, NUD_CastleRankInfo));
        GB_Castle = new GroupBoxView("GB_Castle", "Battle Castle", castleGrid);

        CB_Stats1.SelectionChanged += (_, _) => ChangeStat1();
        CB_Stats2.SelectionChanged += (_, _) => ChangeStat();
        RB_Stats3_01.IsCheckedChanged += (_, _) => ChangeStat();
        RB_Stats3_02.IsCheckedChanged += (_, _) => ChangeStat();
        CB_Species.SelectionChanged += (_, _) => ChangeSpecies();
        CHK_Continue.IsCheckedChanged += (_, _) => { if (!editing) StatAddrControl(SetValToSav: -1); };
        CHK_HallCurrent.IsCheckedChanged += (_, _) => ChangeHallCurrent();
        NUD_HallStreaks.ValueChanged += (_, _) => ChangeHallStreaks();
        foreach (var nud in StatNUDA)
            nud.ValueChanged += (s, _) => ChangeStatVal((NumericUpDown)s!);
        foreach (var nud in HallNUDA)
            nud.ValueChanged += (s, _) => ChangeHallType((NumericUpDown)s!);
        foreach (var nud in new[] { NUD_CastleRankRcv, NUD_CastleRankItem, NUD_CastleRankInfo })
            nud.ValueChanged += (s, _) => ChangeCastleRank((NumericUpDown)s!);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(UiFactory.Column(GB_Prints, streaks, GB_Castle));
        body.Children.Add(GB_Hall);
        return new ScrollViewer { Content = body, MaxHeight = 620 };
    }

    private Control BuildWalker()
    {
        B_AllWalkerCourses.Click += (_, _) => ClickAllWalkerCourses();
        var grid = UiFactory.FormGrid(2);
        UiFactory.AddFormRow(grid, 0, UiFactory.Label("L_Watts", "Watts:"), NUD_Watts);
        UiFactory.AddFormRow(grid, 1, UiFactory.Label("L_Steps", "Steps:"), NUD_Steps);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(new GroupBoxView("GB_WalkerCourses", "Pokewalker Courses", UiFactory.Column(CLB_WalkerCourses, B_AllWalkerCourses)));
        body.Children.Add(grid);
        return body;
    }

    private Control BuildSeals()
    {
        var grid = CountGrid("DGV_Seals", SealRows, 200);
        var clear = UiFactory.Button("B_ClearSeals", "Clear All Seals");
        var legal = UiFactory.Button("B_AllSealsLegal", "Give All Seals (Legal)");
        var illegal = UiFactory.Button("B_AllSealsIllegal", "Give All Seals (Illegal)");
        clear.Click += (_, _) => ClearSeals();
        legal.Click += (_, _) => SetAllSeals(false);
        illegal.Click += (_, _) => SetAllSeals(true);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(grid);
        body.Children.Add(UiFactory.Column(clear, legal, illegal));
        return body;
    }

    private Control BuildFashion()
    {
        var accGrid = CountGrid("DGV_Accessories", AccessoryRows, 200);
        var accClear = UiFactory.Button("B_ClearAccessories", "Clear All Accessories");
        var accLegal = UiFactory.Button("B_AllAccessoriesLegal", "Give All Accessories (Legal)");
        var accIllegal = UiFactory.Button("B_AllAccessoriesIllegal", "Give All Accessories (Illegal)");
        accClear.Click += (_, _) => ClearAccessories();
        accLegal.Click += (_, _) => SetAllAccessories(false);
        accIllegal.Click += (_, _) => SetAllAccessories(true);

        var bdGrid = new DataGrid
        {
            Name = "DGV_Backdrops",
            ItemsSource = BackdropRows,
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            CanUserReorderColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Width = 260,
            Height = 320,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        bdGrid.Columns.Add(DataGridUtil.StringComboColumn("Slot", backdropsSorted, nameof(NameRow.Name), 230));

        var bdClear = UiFactory.Button("B_ClearBackdrops", "Clear All Backdrops");
        var bdLegal = UiFactory.Button("B_AllBackdropsLegal", "Give All Backdrops (Legal)");
        var bdIllegal = UiFactory.Button("B_AllBackdropsIllegal", "Give All Backdrops (Illegal)");
        bdClear.Click += (_, _) => ClearBackdrops();
        bdLegal.Click += (_, _) => SetAllBackdrops(false);
        bdIllegal.Click += (_, _) => SetAllBackdrops(true);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(UiFactory.Column(accGrid, accClear, accLegal, accIllegal));
        body.Children.Add(UiFactory.Column(bdGrid, bdClear, bdLegal, bdIllegal));
        return new ScrollViewer { Content = body, MaxHeight = 620 };
    }

    private static DataGrid CountGrid(string name, ObservableCollection<CountRow> rows, double nameWidth)
    {
        var grid = new DataGrid
        {
            Name = name,
            ItemsSource = rows,
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            CanUserReorderColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Width = nameWidth + 80,
            Height = 320,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Slot", Binding = new Binding(nameof(CountRow.Name)), IsReadOnly = true, Width = new DataGridLength(nameWidth) });
        grid.Columns.Add(new DataGridTextColumn { Header = string.Empty, Binding = new Binding(nameof(CountRow.Count)) { Mode = BindingMode.TwoWay }, Width = new DataGridLength(70) });
        return grid;
    }

    private Control BuildPoffins()
    {
        LB_Poffins.ItemsSource = PoffinItems;
        LB_Poffins.SelectionChanged += (_, _) => ChangePoffinIndex();
        B_PoffinAll.Click += (_, _) => { PoffinCase.FillCase(); RefreshPoffinView(); };
        B_PoffinDel.Click += (_, _) => { PoffinCase.DeleteAll(); RefreshPoffinView(); };
        PG_Poffins.PropertyValueChanged += () => SavePoffinIndex(LB_Poffins.SelectedIndex);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(UiFactory.Column(LB_Poffins, UiFactory.Row(B_PoffinAll, B_PoffinDel)));
        body.Children.Add(PG_Poffins);
        return body;
    }

    private Control BuildGear()
    {
        var grid = new DataGrid
        {
            Name = "PG_Rolodex",
            ItemsSource = RolodexRows,
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            CanUserReorderColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Width = 320,
            Height = 340,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var callers = Enum.GetValues<PokegearNumber>().Select(z => new ComboItem(z.ToString(), (int)z)).ToList();
        grid.Columns.Add(new DataGridTextColumn { Header = "#", Binding = new Binding(nameof(CallerRow.Index)), IsReadOnly = true, Width = new DataGridLength(50) });
        grid.Columns.Add(DataGridUtil.ComboColumn("Caller", callers, nameof(CallerRow.Value), 250));

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(grid);
        body.Children.Add(UiFactory.Column(B_GearGiveAll, B_GearGiveAllNoTrainers, B_GearDeleteAll));
        return body;
    }

    private Control BuildRecords()
    {
        var grid = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(grid, 0, UiFactory.Label("L_Record16", "Record:"), NUD_Record16);
        UiFactory.AddFormRow(grid, 1, UiFactory.Label("L_Record16V", "Value:"), NUD_Record16V);
        UiFactory.AddFormRow(grid, 2, UiFactory.Label("L_Record32", "Record:"), NUD_Record32);
        UiFactory.AddFormRow(grid, 3, UiFactory.Label("L_Record32V", "Value:"), NUD_Record32V);
        return grid;
    }

    #endregion

    #region Main tab data

    private const int FlyFlagStart = 2480;
    private static ReadOnlySpan<byte> FlyWorkFlagSinnoh => [000, 001, 002, 003, 004, 005, 006, 007, 008, 009, 010, 011, 012, 013, 014, 015, 016, 017,   067, 068];
    private static ReadOnlySpan<byte> LocationIDsSinnoh => [001, 002, 003, 004, 005, 082, 083, 006, 007, 008, 009, 010, 011, 012, 013, 014, 054, 081,   055, 015];
    private static ReadOnlySpan<byte> FlyWorkFlagHGSS   => [000, 001, 002, 003, 004, 005, 006, 007, 008, 009, 010, 011, 012, 013, 014, 015, 016, 017, 018, 019, 020, 021, 022,   027, 030, 033, 035];
    private static ReadOnlySpan<byte> LocationIDsHGSS   => [138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137,   229, 227, 221, 225];

    private void ReadMain()
    {
        NUD_Coin.Maximum = SAV.MaxCoins;
        NUD_Coin.SetValueClamped(Math.Clamp(SAV.Coin, 0, (uint)SAV.MaxCoins));
        NUD_BP.SetValueClamped(Math.Clamp(SAV.BP, 0, 9999));

        var locations = SAV is SAV4Sinnoh ? LocationIDsSinnoh : LocationIDsHGSS;
        var flags = SAV is SAV4Sinnoh ? FlyWorkFlagSinnoh : FlyWorkFlagHGSS;
        for (int i = 0; i < locations.Length; i++)
        {
            var state = SAV.GetEventFlag(FlyFlagStart + flags[i]);
            CLB_FlyDest.Add(GameInfo.Strings.Gen4.Met0[locations[i]], state);
        }

        if (SAV is SAV4Sinnoh sinnoh)
        {
            ReadPoketch(sinnoh);
            NUD_UGFlags.SetValueClamped(Math.Clamp(sinnoh.UG_FlagsCaptured, 0, SAV4Sinnoh.UG_MAX));
            L_PokeathlonPoints.IsVisible = NUD_PokeathlonPoints.IsVisible = false;
        }
        else if (SAV is SAV4HGSS hgss)
        {
            ReadWalker(hgss);
            NUD_PokeathlonPoints.SetValueClamped(hgss.Pokeathlon.Points);
            L_UGFlags.IsVisible = NUD_UGFlags.IsVisible = false;
            string[] items = ["Map Johto", "Map Johto+", "Map Johto & Kanto"];
            var index = hgss.MapUnlockState;
            if (index >= MapUnlockState4.Invalid)
                index = MapUnlockState4.JohtoKanto;
            CB_UpgradeMap.ItemsSource = items;
            CB_UpgradeMap.SelectedIndex = (int)index;
        }

        ReadSeals();
        ReadAccessories();
        ReadBackdrops();
        ReadRecord();
    }

    private void SaveMain()
    {
        SAV.Coin = (uint)(NUD_Coin.Value ?? 0);
        SAV.BP = (ushort)(NUD_BP.Value ?? 0);

        var flags = SAV is SAV4Sinnoh ? FlyWorkFlagSinnoh : FlyWorkFlagHGSS;
        for (int i = 0; i < CLB_FlyDest.Count; i++)
            SAV.SetEventFlag(FlyFlagStart + flags[i], CLB_FlyDest.GetItemChecked(i));

        if (SAV is SAV4Sinnoh sinnoh)
        {
            SavePoketch(sinnoh);
            sinnoh.UG_FlagsCaptured = (uint)(NUD_UGFlags.Value ?? 0);
        }
        else if (SAV is SAV4HGSS hgss)
        {
            SaveWalker(hgss);
            var pokeathlon = hgss.Pokeathlon;
            pokeathlon.Points = (uint)(NUD_PokeathlonPoints.Value ?? 0);
            hgss.MapUnlockState = (MapUnlockState4)Math.Max(0, CB_UpgradeMap.SelectedIndex);
        }

        SaveSeals();
        SaveAccessories();
        SaveBackdrops();
        SaveRecord();
    }

    #endregion

    #region Pokétch

    private void ReadPoketch(SAV4Sinnoh s)
    {
        var apps = new List<string>();
        for (PoketchApp i = 0; i <= PoketchApp.Alarm_Clock; i++)
        {
            var name = poketchapps[(int)i];
            apps.Add(name);
            CLB_Poketch.Add($"{(int)i:00} - {name}", s.GetPoketchAppUnlocked(i));
        }
        CB_CurrentApp.ItemsSource = apps;
        CB_CurrentApp.SelectedIndex = Math.Clamp(s.CurrentPoketchApp, 0, apps.Count - 1);

        DotArtistByte = s.GetPoketchDotArtistData();
        RefreshDotArt();
    }

    private void SavePoketch(SAV4Sinnoh s)
    {
        int unlockedCount = 0;
        s.CurrentPoketchApp = (sbyte)Math.Max(0, CB_CurrentApp.SelectedIndex);
        for (int i = 0; i < CLB_Poketch.Count; i++)
        {
            var b = CLB_Poketch.GetItemChecked(i);
            s.SetPoketchAppUnlocked((PoketchApp)i, b);
            if (b)
                unlockedCount++;
        }
        s.SetPoketchDotArtistData(DotArtistByte);
        s.PoketchUnlockedCount = (byte)unlockedCount;
    }

    private void RefreshDotArt()
    {
        if (DotArtistByte.Length != DotMatrixPixelCount / 4)
            return;
        using var bmp = GetDotArt(DotArtistByte);
        PB_DotArtist.Source = bmp.ToAvaloniaBitmap();
    }

    private void ClickDotArtist(object? sender, PointerPressedEventArgs e)
    {
        var p = e.GetPosition(PB_DotArtist);
        SetFlagsFromClickPoint((int)p.X, (int)p.Y);
        RefreshDotArt();
    }

    private void SetFlagsFromClickPoint(int inpX, int inpY)
    {
        inpX = Math.Clamp(inpX, 0, (DotMatrixWidth * DotMatrixUpscaleFactor) - 1);
        inpY = Math.Clamp(inpY, 0, (DotMatrixHeight * DotMatrixUpscaleFactor) - 1);
        int i = (inpX >> 2) + (DotMatrixWidth * (inpY >> 2));
        Span<byte> ndab = stackalloc byte[DotMatrixPixelCount / 4];
        DotArtistByte.CopyTo(ndab);

        byte c = (byte)((ndab[i >> 2] >> ((i % 4) << 1)) & 3);
        if (++c >= 4)
            c = 0;

        ndab[i >> 2] &= (byte)~(3 << ((i % 4) << 1));
        ndab[i >> 2] |= (byte)((c & 3) << ((i % 4) << 1));

        ndab.CopyTo(DotArtistByte);
    }

    private void OnDotArtDragOver(object? sender, DragEventArgs e)
    {
        var ok = GB_Poketch.IsVisible && e.DataTransfer.TryGetFiles() is { Length: not 0 };
        e.DragEffects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDotArtDrop(object? sender, DragEventArgs e)
    {
        if (!GB_Poketch.IsVisible)
            return;
        e.Handled = true;
        if (e.DataTransfer.TryGetFiles() is not { Length: not 0 } files)
            return;
        if (files[0].TryGetLocalPath() is not { } path)
            return;
        TryBuild(path, DotArtistByte);
        RefreshDotArt();
    }

    #endregion

    #region Battle Frontier

    private void ReadBattleFrontier()
    {
        BFV = [
            [2, 0], // Max, Current
            [2, 0, 3, 1], // Max, Current, Max(Trade), Current(Trade)
            [2, 0, 1, -1, 3], // Max, Current, Current(CP), (UsedCP), Max(CP)
        ];
        BFT = [
            ["Singles", "Doubles", "Multi"],
            ["Singles", "Doubles", "Multi (Trainer)", "Multi (Friend)", "Wi-Fi"],
        ];

        if (SAV is not SAV4DP)
            SetPrintColors();
        if (Hall is null)
            NUD_HallStreaks.IsVisible = NUD_HallStreaks.IsEnabled = false;

        editing = true;
        var facilities = new List<string>();
        for (BattleFrontierFacility4 i = 0; i <= SAV.MaxFacility; i++)
            facilities.Add(i.ToString());
        CB_Stats1.ItemsSource = facilities;
        RB_Stats3_01.IsChecked = true;

        CB_Species.SetItems(GameInfo.FilteredSources.Species.Skip(1).ToList());
        CB_Species.SelectedIndex = 0; // the WinForms binding selects the first entry, seeding the species field
        editing = false;
        CB_Stats1.SelectedIndex = 0;
    }

    private void SaveBattleFrontier()
    {
        if (HallStatUpdated)
            Hall?.RefreshChecksum();
    }

    private void SetPrintColors()
    {
        for (int i = 0; i < PrintButtonA.Length; i++)
            SetPrintColor(PrintButtonA[i], (BattleFrontierPrintStatus4)SAV.GetWork(PrintIndexStart + i));
    }

    private static void SetPrintColor(Control pb, BattleFrontierPrintStatus4 value)
    {
        bool ready = value is BattleFrontierPrintStatus4.FirstReady or BattleFrontierPrintStatus4.SecondReady;
        if (ready)
            pb.SetForeColor(Color.Red);
        else if (value != 0)
            pb.SetForeColor(Color.Green);
        else
            pb.ClearValue(global::Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty);

        if (value is BattleFrontierPrintStatus4.FirstReady or BattleFrontierPrintStatus4.FirstReceived)
            pb.SetBackColor(Color.Silver);
        else if (value is BattleFrontierPrintStatus4.SecondReady or BattleFrontierPrintStatus4.SecondReceived)
            pb.SetBackColor(Color.Gold);
        else
            pb.ClearValue(global::Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty);
    }

    private void ClickPrint(Button b)
    {
        int index = Array.IndexOf(PrintButtonA, b);
        if (index < 0)
            return;
        index += PrintIndexStart;
        var current = SAV.GetWork(index);
        current++;
        if (current > (int)BattleFrontierPrintStatus4.SecondReceived)
            current = 0;
        SAV.SetWork(index, current);
        SetPrintColor(b, (BattleFrontierPrintStatus4)current);
    }

    private void ChangeStat1()
    {
        if (editing)
            return;
        int facility = CB_Stats1.SelectedIndex;
        if (facility < 0)
            return;

        editing = true;
        CB_Stats2.ItemsSource = BFT[BFF[facility][1]];

        RB_Stats3_01.IsChecked = true;
        RB_Stats3_01.IsVisible = RB_Stats3_02.IsVisible = facility == 1;

        for (int i = 0; i < StatLabelA.Length; i++)
        {
            var visible = BFV[BFF[facility][0]].Contains(i);
            StatLabelA[i].IsVisible = StatNUDA[i].IsVisible = StatNUDA[i].IsEnabled = visible;
        }
        if (facility == 0)
        {
            StatLabelA[1].IsVisible = StatNUDA[1].IsVisible = StatNUDA[1].IsEnabled = true;
            StatLabelA[1].Text = "Continue";
            StatNUDA[1].Maximum = 65535;
        }
        else
        {
            if (StatNUDA[1].Value > 9999)
                StatNUDA[1].Value = 9999;
            StatNUDA[1].Maximum = 9999;
        }

        if (facility == 1)
            StatLabelA[1].Text = StatLabelA[3].Text = "Trade";
        else if (facility == 3)
            StatLabelA[1].Text = StatLabelA[3].Text = "CP";

        GB_Hall.IsVisible = facility == 2;
        GB_Castle.IsVisible = facility == 3;

        editing = false;
        // Assigning ItemsSource already selected index 0, which raises no event; drive the reload explicitly.
        if (CB_Stats2.SelectedIndex == 0)
            ChangeStat();
        else
            CB_Stats2.SelectedIndex = 0;
    }

    private void ChangeStat()
    {
        if (editing)
            return;
        StatAddrControl(SetValToSav: -2, SetSavToVal: true);
        if (GB_Hall.IsVisible && CB_Stats2.SelectedItem is string sH)
        {
            GB_Hall.Header = $"Battle Hall ({sH})";
            editing = true;
            GetHallStat();
            editing = false;
        }
        else if (GB_Castle.IsVisible && CB_Stats2.SelectedItem is string sC)
        {
            GB_Castle.Header = $"Battle Castle ({sC})";
            editing = true;
            GetCastleStat();
            editing = false;
        }
    }

    private void StatAddrControl(int SetValToSav = -2, bool SetSavToVal = false)
    {
        int Facility = CB_Stats1.SelectedIndex;
        int BattleType = CB_Stats2.SelectedIndex;
        if (Facility < 0 || BattleType < 0)
            return;
        int RBi = RB_Stats3_02.IsChecked == true ? 1 : 0;
        int addrVal = BFF[Facility][2] + (BFF[Facility][3] * BattleType) + (RBi << 3);
        int addrFlag = BFF[Facility][4];
        byte maskFlag = (byte)(1 << (BattleType + (RBi << 2)));
        int TowerContinueCountOfs = SAV is SAV4DP ? 3 : 1;

        var general = SAV.General;
        if (SetSavToVal)
        {
            editing = true;
            for (int i = 0; i < BFV[BFF[Facility][0]].Length; i++)
            {
                if (BFV[BFF[Facility][0]][i] < 0)
                    continue;
                int vali = ReadUInt16LittleEndian(general[(addrVal + (i << 1))..]);
                StatNUDA[BFV[BFF[Facility][0]][i]].SetValueClamped(vali > 9999 ? 9999 : vali);
            }
            CHK_Continue.IsChecked = (general[addrFlag] & maskFlag) != 0;

            if (Facility == 0) // tower continue count
                StatNUDA[1].SetValueClamped(ReadUInt16LittleEndian(general[(addrFlag + TowerContinueCountOfs + (BattleType << 1))..]));

            editing = false;
            return;
        }
        if (SetValToSav >= 0)
        {
            ushort val = (ushort)(StatNUDA[SetValToSav].Value ?? 0);

            if (Facility == 0 && SetValToSav == 1) // tower continue count
            {
                var offset = addrFlag + TowerContinueCountOfs + (BattleType << 1);
                WriteUInt16LittleEndian(general[offset..], val);
            }

            SetValToSav = Array.IndexOf(BFV[BFF[Facility][0]], SetValToSav);
            if (SetValToSav < 0)
                return;
            var clamp = Math.Min((ushort)9999, val);
            WriteUInt16LittleEndian(general[(addrVal + (SetValToSav << 1))..], clamp);
            return;
        }
        if (SetValToSav == -1)
        {
            if (CHK_Continue.IsChecked == true)
            {
                general[addrFlag] |= maskFlag;
                if (Facility == 3)
                    general[addrFlag + 1] |= 0x01; // not found what this flag means
            }
            else
            {
                general[addrFlag] &= (byte)~maskFlag;
            }
        }
    }

    private void ChangeStatVal(NumericUpDown sender)
    {
        if (editing)
            return;
        int n = Array.IndexOf(StatNUDA, sender);
        if (n < 0)
            return;

        StatAddrControl(SetValToSav: n);

        if (CB_Stats1.SelectedIndex != 0)
            return;

        const int bias = 7;
        var n0 = StatNUDA[0];
        var n1 = StatNUDA[1];
        var v0 = n0.Value ?? 0;
        var v1 = n1.Value ?? 0;
        if (Math.Floor(v0 / bias) == v1)
            return;

        if (n == 0)
        {
            n1.Value = Math.Floor(v0 / bias);
        }
        else if (n == 1)
        {
            if (n0.Maximum > v1 * bias)
                n0.Value = v1 * bias;
            else if (v0 < n0.Maximum)
                n0.Value = n0.Maximum;
        }
    }

    private void ChangeSpecies()
    {
        species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
        if (editing)
            return;
        editing = true;
        GetHallStat();
        editing = false;
    }

    private void GetCastleStat()
    {
        int ofs = BFF[3][2] + (BFF[3][3] * CB_Stats2.SelectedIndex) + 0x0A;
        NumericUpDown[] na = [NUD_CastleRankRcv, NUD_CastleRankItem, NUD_CastleRankInfo];
        for (int i = 0; i < na.Length; i++)
        {
            int val = ReadInt16LittleEndian(SAV.General[(ofs + (i << 1))..]);
            na[i].SetValueClamped(val);
        }
    }

    private void ChangeCastleRank(NumericUpDown sender)
    {
        if (editing)
            return;
        NumericUpDown[] na = [NUD_CastleRankRcv, NUD_CastleRankItem, NUD_CastleRankInfo];
        int i = Array.IndexOf(na, sender);
        if (i < 0)
            return;
        var offset = BFF[3][2] + (BFF[3][3] * CB_Stats2.SelectedIndex) + 0x0A + (i << 1);
        WriteInt32LittleEndian(SAV.General[offset..], (int)(na[i].Value ?? 0));
    }

    private void GetHallStat()
    {
        if (CB_Stats2.SelectedIndex < 0)
            return;
        int ofscur = BFF[2][2] + (BFF[2][3] * CB_Stats2.SelectedIndex);
        var curspe = ReadUInt16LittleEndian(SAV.General[(ofscur + 4)..]);
        bool c = curspe == species;
        CHK_HallCurrent.IsChecked = c;
        CHK_HallCurrent.Content = curspe > 0 && curspe <= SAV.MaxSpeciesID
            ? $"Current: {SpeciesName.GetSpeciesNameGeneration(curspe, GameLanguage.GetLanguageIndex(MainWindow.CurrentLanguage), 4)}"
            : "Current: (None)";

        int s = 0;
        for (int i = 0; i < HallNUDA.Length; i++)
        {
            var d = c ? Math.Min(10, (SAV.General[ofscur + 6 + ((i >> 1) << 1)] >> ((i & 1) << 2)) & 0x0F) : 0;
            HallNUDA[i].Value = d;
            HallNUDA[i].IsEnabled = c;
            s += d;
        }
        L_SumHall.Text = s.ToString();

        if (Hall is not null)
            NUD_HallStreaks.SetValueClamped(Math.Min((ushort)9999, Hall.GetCount(CB_Stats2.SelectedIndex, species)));
    }

    private void ChangeHallCurrent()
    {
        if (editing || CB_Stats2.SelectedIndex < 0)
            return;
        var offset = BFF[2][2] + (BFF[2][3] * CB_Stats2.SelectedIndex) + 4;
        ushort value = (ushort)(CHK_HallCurrent.IsChecked == true ? species : 0);
        WriteUInt16LittleEndian(SAV.General[offset..], value);
        editing = true;
        GetHallStat();
        editing = false;
    }

    private void ChangeHallType(NumericUpDown sender)
    {
        if (editing || CB_Stats2.SelectedIndex < 0)
            return;
        int i = Array.IndexOf(HallNUDA, sender);
        if (i < 0)
            return;
        int ofs = BFF[2][2] + (BFF[2][3] * CB_Stats2.SelectedIndex) + 6 + ((i >> 1) << 1);
        SAV.General[ofs] = (byte)((SAV.General[ofs] & ~(0xF << ((i & 1) << 2))) | ((int)(HallNUDA[i].Value ?? 0) << ((i & 1) << 2)));
        L_SumHall.Text = HallNUDA.Sum(x => x.Value ?? 0).ToString(CultureInfo.InvariantCulture);
    }

    private void ChangeHallStreaks()
    {
        if (editing || Hall is null || CB_Stats2.SelectedIndex < 0)
            return;
        Hall.SetCount(CB_Stats2.SelectedIndex, species, (ushort)(NUD_HallStreaks.Value ?? 0));
        HallStatUpdated = true;
    }

    #endregion

    #region Pokéwalker

    private void ReadWalker(SAV4HGSS s)
    {
        foreach (var name in GameInfo.Sources.Strings.walkercourses)
            CLB_WalkerCourses.Add(name);
        ReadWalkerCourseUnlockFlags(s);

        NUD_Watts.SetValueClamped(s.PokewalkerWatts);
        NUD_Steps.SetValueClamped(s.PokewalkerSteps);
    }

    private void ReadWalkerCourseUnlockFlags(SAV4HGSS s)
    {
        Span<bool> courses = stackalloc bool[SAV4HGSS.PokewalkerCourseFlagCount];
        s.GetPokewalkerCoursesUnlocked(courses);
        for (int i = 0; i < CLB_WalkerCourses.Count && i < courses.Length; i++)
            CLB_WalkerCourses.SetItemChecked(i, courses[i]);
    }

    private void SaveWalker(SAV4HGSS s)
    {
        Span<bool> courses = stackalloc bool[SAV4HGSS.PokewalkerCourseFlagCount];
        for (int i = 0; i < CLB_WalkerCourses.Count && i < courses.Length; i++)
            courses[i] = CLB_WalkerCourses.GetItemChecked(i);
        s.SetPokewalkerCoursesUnlocked(courses);

        s.PokewalkerWatts = (uint)(NUD_Watts.Value ?? 0);
        s.PokewalkerSteps = (uint)(NUD_Steps.Value ?? 0);
    }

    private void ClickAllWalkerCourses()
    {
        if (SAV is not SAV4HGSS s)
            return;
        s.PokewalkerCoursesUnlockAll();
        ReadWalkerCourseUnlockFlags(s);
    }

    #endregion

    #region Seals / Accessories / Backdrops

    private sealed class CountRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private string _count = "0";
        public required string Name { get; init; }

        public string Count
        {
            get => _count;
            set
            {
                if (_count == value)
                    return;
                _count = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            }
        }
    }

    private sealed class NameRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }
    }

    private sealed class CallerRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private int _value;
        public required int Index { get; init; }

        public int Value
        {
            get => _value;
            set
            {
                if (_value == value)
                    return;
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    private void ReadSeals()
    {
        const int count = (int)Seal4.MAX;
        for (int i = 0; i < count; i++)
            SealRows.Add(new CountRow { Name = seals[i], Count = SAV.GetSealCount((Seal4)i).ToString() });
    }

    private void ClearSeals()
    {
        foreach (var row in SealRows)
            row.Count = "0";
    }

    private void SetAllSeals(bool unreleased)
    {
        var sealIndexCount = (int)(unreleased ? Seal4.MAX : Seal4.MAXLEGAL);
        for (int i = 0; i < sealIndexCount && i < SealRows.Count; i++)
            SealRows[i].Count = SAV4.SealMaxCount.ToString();
    }

    private void SaveSeals()
    {
        for (int i = 0; i < (int)Seal4.MAX && i < SealRows.Count; i++)
        {
            var count = int.TryParse(SealRows[i].Count, out var val) ? val : 0;
            SAV.SetSealCount((Seal4)i, (byte)Math.Clamp(count, 0, byte.MaxValue));
        }
    }

    private void ReadAccessories()
    {
        for (int i = 0; i < AccessoryInfo.Count; i++)
            AccessoryRows.Add(new CountRow { Name = accessories[i], Count = SAV.GetAccessoryOwnedCount((Accessory4)i).ToString() });
    }

    private void ClearAccessories()
    {
        foreach (var row in AccessoryRows)
            row.Count = "0";
    }

    private void SetAllAccessories(bool unreleased)
    {
        for (int i = 0; i <= AccessoryInfo.MaxMulti && i < AccessoryRows.Count; i++)
            AccessoryRows[i].Count = AccessoryInfo.AccessoryMaxCount.ToString();

        var count = unreleased ? AccessoryInfo.Count : (AccessoryInfo.MaxLegal + 1);
        for (int i = AccessoryInfo.MaxMulti + 1; i < count && i < AccessoryRows.Count; i++)
            AccessoryRows[i].Count = "1";
    }

    private void SaveAccessories()
    {
        for (int i = 0; i < AccessoryInfo.Count && i < AccessoryRows.Count; i++)
        {
            var count = int.TryParse(AccessoryRows[i].Count, out var val) ? val : 0;
            SAV.SetAccessoryOwnedCount((Accessory4)i, (byte)Math.Clamp(count, 0, byte.MaxValue));
        }
    }

    private void ReadBackdrops()
    {
        for (int i = 0; i < BackdropInfo.Count; i++)
            BackdropRows.Add(new NameRow { Name = backdrops[(int)Backdrop4.Unset] });

        for (int i = 0; i < BackdropInfo.Count; i++)
        {
            var pos = SAV.GetBackdropPosition((Backdrop4)i);
            if (pos < BackdropInfo.Count)
                BackdropRows[pos].Name = backdrops[i];
        }
    }

    private void ClearBackdrops()
    {
        foreach (var row in BackdropRows)
            row.Name = backdrops[(int)Backdrop4.Unset];
    }

    private void SetAllBackdrops(bool unreleased)
    {
        var count = unreleased ? BackdropInfo.Count : ((int)BackdropInfo.MaxLegal + 1);
        for (int i = 0; i < count && i < BackdropRows.Count; i++)
            BackdropRows[i].Name = backdrops[i];
    }

    private void SaveBackdrops()
    {
        for (int i = 0; i < BackdropInfo.Count; i++)
            SAV.RemoveBackdrop((Backdrop4)i); // clear all slots

        byte ctr = 0;
        for (int i = 0; i < BackdropInfo.Count && i < BackdropRows.Count; i++)
        {
            var bd = (Backdrop4)Array.IndexOf(backdrops, BackdropRows[i].Name);
            if (bd.IsUnset()) // skip empty slots
                continue;

            SAV.SetBackdropPosition(bd, ctr);
            ctr++;
        }
    }

    #endregion

    #region Poffins

    private void InitializePoffins(SAV4Sinnoh sav)
    {
        PoffinCase = new PoffinCase4(sav);
        for (int i = 0; i < PoffinCase.Poffins.Length; i++)
            PoffinItems.Add(GetPoffinText(i));
        LB_Poffins.SelectedIndex = 0;
    }

    private string GetPoffinName(PoffinFlavor4 flavor)
    {
        var index = (uint)flavor;
        if (index >= PoffinNames.Length)
            index = 0;
        return PoffinNames[index];
    }

    private string GetPoffinText(int index) => $"{index + 1:000} - {GetPoffinName(PoffinCase.Poffins[index].Type)}";

    private void RefreshPoffinView()
    {
        PG_Poffins.SetObject(PG_Poffins.SelectedObject);
        UpdatingPoffins = true;
        var selected = LB_Poffins.SelectedIndex;
        for (int i = 0; i < PoffinCase.Poffins.Length; i++)
            PoffinItems[i] = GetPoffinText(i);
        LB_Poffins.SelectedIndex = selected; // replacing the item drops the selection
        UpdatingPoffins = false;
    }

    private void SavePoffinIndex(int index)
    {
        if (index < 0)
            return;
        UpdatingPoffins = true;
        var selected = LB_Poffins.SelectedIndex;
        PoffinItems[index] = GetPoffinText(index);
        LB_Poffins.SelectedIndex = selected;
        UpdatingPoffins = false;
    }

    private void ChangePoffinIndex()
    {
        if (UpdatingPoffins)
            return;
        SavePoffinIndex(CurrentPoffinIndex);
        CurrentPoffinIndex = LB_Poffins.SelectedIndex;
        if (CurrentPoffinIndex < 0)
        {
            LB_Poffins.SelectedIndex = 0;
            return;
        }
        PG_Poffins.SetObject(PoffinCase.Poffins[CurrentPoffinIndex]);
    }

    #endregion

    #region PokéGear

    private void InitializeGear(SAV4HGSS sav)
    {
        B_GearGiveAll.Click += (_, _) => { sav.PokeGearUnlockAllCallers(); RefreshRolodex(sav); };
        B_GearGiveAllNoTrainers.Click += (_, _) => { sav.PokeGearUnlockAllCallersNoTrainers(); RefreshRolodex(sav); };
        B_GearDeleteAll.Click += (_, _) => { sav.PokeGearClearAllCallers(); RefreshRolodex(sav); };
        RefreshRolodex(sav);
    }

    private void RefreshRolodex(SAV4HGSS sav)
    {
        var dex = sav.GetPokeGearRoloDex();
        if (RolodexRows.Count != dex.Length)
        {
            RolodexRows.Clear();
            for (int i = 0; i < dex.Length; i++)
                RolodexRows.Add(new CallerRow { Index = i, Value = (int)dex[i] });
            return;
        }
        for (int i = 0; i < dex.Length; i++)
            RolodexRows[i].Value = (int)dex[i];
    }

    private void SaveGear(SAV4HGSS sav)
    {
        var dex = sav.GetPokeGearRoloDex();
        for (int i = 0; i < dex.Length && i < RolodexRows.Count; i++)
            dex[i] = (PokegearNumber)RolodexRows[i].Value;
    }

    #endregion

    #region Records

    private void ReadRecord()
    {
        NUD_Record16.Maximum = Record4.Record16 - 1;
        NUD_Record32.Maximum = Record.Record32 - 1;
        NUD_Record16V.SetValueClamped(Record.GetRecord16(0));
        NUD_Record32V.SetValueClamped(Record.GetRecord32(0));
        NUD_Record16V.ValueChanged += (_, _) => Record.SetRecord16((int)(NUD_Record16.Value ?? 0), (ushort)(NUD_Record16V.Value ?? 0));
        NUD_Record32V.ValueChanged += (_, _) => Record.SetRecord32((int)(NUD_Record32.Value ?? 0), (uint)(NUD_Record32V.Value ?? 0));
        NUD_Record16.ValueChanged += (_, _) => NUD_Record16V.SetValueClamped(Record.GetRecord16((int)(NUD_Record16.Value ?? 0)));
        NUD_Record32.ValueChanged += (_, _) => NUD_Record32V.SetValueClamped(Record.GetRecord32((int)(NUD_Record32.Value ?? 0)));
    }

    private void SaveRecord() => Record.EndAccess();

    #endregion

    protected override void OnSave()
    {
        SaveMain();
        SaveBattleFrontier();
        if (SAV is SAV4HGSS hgss)
            SaveGear(hgss);
        else if (SAV is SAV4Sinnoh)
        {
            SavePoffinIndex(CurrentPoffinIndex);
            PoffinCase.Save();
        }

        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
