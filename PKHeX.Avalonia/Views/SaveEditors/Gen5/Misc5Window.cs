using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen5;

/// <summary>
/// Miscellaneous editor for Generation 5 saves (port of the WinForms <c>SAV_Misc5</c>).
/// </summary>
/// <remarks>
/// Groups everything the WinForms form does into the same tabs: fly destinations and the game-specific
/// unlocks, the Black City / White Forest block, Entralink and Funfest missions, the Dream World Entree
/// Forest slots, the Battle Subway records, the Musical props and the raw record counters.
/// </remarks>
public sealed class Misc5Window : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV5 SAV;
    private readonly BattleSubwayPlay5 swp;
    private readonly BattleSubway5 sw;
    private readonly string[] PropNames;

    private bool editing;
    private int ofsFly;
    private int[] FlyDestC = [];

    // Main tab
    private readonly CheckedListView CLB_FlyDest = new() { Name = "CLB_FlyDest", Width = 260, Height = 260 };
    private readonly Button B_AllFlyDest = UiFactory.Button("B_AllFlyDest", "All");
    private readonly ComboBox CB_Roamer641 = UiFactory.Combo("CB_Roamer641", 170);
    private readonly ComboBox CB_Roamer642 = UiFactory.Combo("CB_Roamer642", 170);
    private readonly ComboBox CB_RoamStatus = UiFactory.Combo("CB_RoamStatus", 170);
    private readonly CheckBox CHK_LibertyPass = UiFactory.Check("CHK_LibertyPass", "Liberty Pass");
    private readonly CheckedListView CLB_KeySystem = new() { Name = "CLB_KeySystem", Width = 260, Height = 220 };
    private readonly Button B_AllKeys = UiFactory.Button("B_AllKeys", "All");
    private GroupBoxView GB_Roamer = null!;
    private GroupBoxView GB_KeySystem = null!;

    // Entralink
    private readonly NumericUpDown NUD_EntreeWhiteLV = UiFactory.NumericUpDown("NUD_EntreeWhiteLV", 0, 999, 110);
    private readonly NumericUpDown NUD_EntreeBlackLV = UiFactory.NumericUpDown("NUD_EntreeBlackLV", 0, 999, 110);
    private readonly NumericUpDown NUD_EntreeWhiteEXP = UiFactory.NumericUpDown("NUD_EntreeWhiteEXP", 0, byte.MaxValue, 110);
    private readonly NumericUpDown NUD_EntreeBlackEXP = UiFactory.NumericUpDown("NUD_EntreeBlackEXP", 0, byte.MaxValue, 110);
    private readonly ComboBox CB_PassPower1 = UiFactory.Combo("CB_PassPower1", 200);
    private readonly ComboBox CB_PassPower2 = UiFactory.Combo("CB_PassPower2", 200);
    private readonly ComboBox CB_PassPower3 = UiFactory.Combo("CB_PassPower3", 200);
    private readonly ListBox LB_FunfestMissions = new() { Name = "LB_FunfestMissions", Width = 240, Height = 240 };
    private readonly ObservableCollection<string> MissionItems = [];
    private readonly Button B_FunfestMissions = UiFactory.Button("B_FunfestMissions", "Unlock All");
    private readonly TextBlock L_FMUnlocked = UiFactory.Label("L_FMUnlocked", "Unlocked");
    private readonly TextBlock L_FMLocked = UiFactory.Label("L_FMLocked", "Locked");
    private readonly CheckBox CHK_FMNew = UiFactory.Check("CHK_FMNew", "New");
    private readonly ComboBox CB_FMLevel = UiFactory.Combo("CB_FMLevel", 140);
    private readonly NumericUpDown NUD_FMBestScore = UiFactory.NumericUpDown("NUD_FMBestScore", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_FMBestTotal = UiFactory.NumericUpDown("NUD_FMBestTotal", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_FMHosted = UiFactory.NumericUpDown("NUD_FMHosted", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_FMParticipated = UiFactory.NumericUpDown("NUD_FMParticipated", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_FMCompleted = UiFactory.NumericUpDown("NUD_FMCompleted", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_FMTopScores = UiFactory.NumericUpDown("NUD_FMTopScores", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_FMMostParticipants = UiFactory.NumericUpDown("NUD_FMMostParticipants", 0, byte.MaxValue, 110);
    private GroupBoxView GB_PassPowers = null!;
    private GroupBoxView GB_FunfestMissions = null!;
    private StackPanel PAN_MissionMeta = null!;

    // Entree forest
    private readonly NumericUpDown NUD_Unlocked = UiFactory.NumericUpDown("NUD_Unlocked", 2, 8, 110);
    private readonly CheckBox CHK_Area9 = UiFactory.Check("CHK_Area9", "9th Area");
    private readonly ComboBox CB_Areas = UiFactory.Combo("CB_Areas", 180);
    private readonly ListBox LB_Slots = new() { Name = "LB_Slots", Width = 180, Height = 240 };
    private readonly ObservableCollection<string> SlotItems = [];
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);
    private readonly ComboBox CB_Move = UiFactory.Combo("CB_Move", 180);
    private readonly ComboBox CB_Gender = UiFactory.Combo("CB_Gender", 140);
    private readonly ComboBox CB_Form = UiFactory.StringCombo("CB_Form", 140);
    private readonly TextBlock L_Form = UiFactory.Label("L_Form", "Form:");
    private readonly NumericUpDown NUD_Animation = UiFactory.NumericUpDown("NUD_Animation", 0, byte.MaxValue, 110);
    private readonly Image PB_SlotPreview = UiFactory.Picture("PB_SlotPreview", 68);
    private readonly Button B_RandForest = UiFactory.Button("B_RandForest", "Randomize");
    private readonly Button B_DumpFC = UiFactory.Button("B_DumpFC", "Export");
    private readonly Button B_ImportFC = UiFactory.Button("B_ImportFC", "Import");
    private TabItem TAB_BWCityForest = null!;

    // Subway
    private readonly NumericUpDown NUD_CurrentType = UiFactory.NumericUpDown("NUD_CurrentType", 0, byte.MaxValue, 110);
    private readonly NumericUpDown NUD_CurrentBattle = UiFactory.NumericUpDown("NUD_CurrentBattle", 0, ushort.MaxValue, 110);
    private readonly CheckBox CHK_Subway0 = UiFactory.Check("CHK_Subway0", "Flag 0");
    private readonly CheckBox CHK_Subway1 = UiFactory.Check("CHK_Subway1", "Flag 1");
    private readonly CheckBox CHK_Subway2 = UiFactory.Check("CHK_Subway2", "Flag 2");
    private readonly CheckBox CHK_Subway7 = UiFactory.Check("CHK_Subway7", "Flag 7");
    private readonly CheckBox CHK_SuperSingle = UiFactory.Check("CHK_SuperSingle", "Super Single");
    private readonly CheckBox CHK_SuperDouble = UiFactory.Check("CHK_SuperDouble", "Super Double");
    private readonly CheckBox CHK_SuperMulti = UiFactory.Check("CHK_SuperMulti", "Super Multi");
    private readonly CheckBox CHK_SWNPCMet = UiFactory.Check("CHK_SWNPCMet", "NPC Met");
    private readonly SubwayRow[] SubwayRows;

    // Musical
    private readonly CheckedListView CLB_MusicalProps = new() { Name = "CLB_MusicalProps", Width = 300, Height = 420 };
    private readonly Button B_UnlockAllProps = UiFactory.Button("B_UnlockAllProps", "Unlock All");

    // Records
    private readonly NumericUpDown NUD_Record16 = UiFactory.NumericUpDown("NUD_Record16", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_Record16V = UiFactory.NumericUpDown("NUD_Record16V", 0, ushort.MaxValue, 130);
    private readonly NumericUpDown NUD_Record32 = UiFactory.NumericUpDown("NUD_Record32", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_Record32V = UiFactory.NumericUpDown("NUD_Record32V", 0, uint.MaxValue, 130);

    private EntreeForest Forest = null!;
    private IList<EntreeSlot> AllSlots = null!;
    private IList<EntreeSlot> CurrentSlots = [];
    private EntreeSlot? CurrentSlot;
    private int currentIndex = -1;

    public Misc5Window(SAV5 sav) : base("SAV_Misc5", "Misc Editor")
    {
        SAV = (SAV5)(Origin = sav).Clone();
        swp = SAV.BattleSubwayPlay;
        sw = SAV.BattleSubway;
        PropNames = Util.GetStringList("props", MainWindow.CurrentLanguage);
        SubwayRows =
        [
            new("Single", "L_SinglePast"), new("Double", "L_DoublePast"),
            new("Multi NPC", "L_MultiNpcPast"), new("Multi Friends", "L_MultiFriendsPast"),
            new("Super Single", "L_SSinglePast"), new("Super Double", "L_SDoublePast"),
            new("Super Multi NPC", "L_SMultiNpcPast"), new("Super Multi Friends", "L_SMultiFriendsPast"),
        ];

        BuildLayout();

        foreach (var name in PropNames)
            CLB_MusicalProps.Add(name);

        ReadMain();
        LoadForest();
        ReadSubway();
        ReadEntralink();
        ReadMusical();
        ReadRecord();
    }

    #region Layout

    private void BuildLayout()
    {
        var tabs = new TabControl { Name = "TC_Misc" };

        GB_Roamer = new GroupBoxView("GB_Roamer", "Roamers", UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_Roamer1", "Tornadus/Thundurus:"), CB_Roamer641),
            UiFactory.Row(UiFactory.Label("L_Roamer2", "Second:"), CB_Roamer642),
            UiFactory.Row(UiFactory.Label("L_RoamStatus", "Status:"), CB_RoamStatus)));
        GB_KeySystem = new GroupBoxView("GB_KeySystem", "Key System", UiFactory.Column(CLB_KeySystem, B_AllKeys));

        var main = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        main.Children.Add(UiFactory.Column(UiFactory.Label("L_FlyDest", "Fly Destinations"), CLB_FlyDest, B_AllFlyDest));
        main.Children.Add(UiFactory.Column(GB_Roamer, CHK_LibertyPass, GB_KeySystem));
        tabs.Items.Add(new TabItem { Name = "TAB_Main", Header = "Main", Content = main });

        // Black City / White Forest
        TAB_BWCityForest = new TabItem
        {
            Name = "TAB_BWCityForest",
            Header = "City/Forest",
            Content = UiFactory.Column(
                UiFactory.Label("L_ForestCity", "Black City / White Forest block"),
                UiFactory.Row(B_DumpFC, B_ImportFC)),
        };
        tabs.Items.Add(TAB_BWCityForest);

        // Entralink
        var levels = UiFactory.FormGrid(2);
        UiFactory.AddFormRow(levels, 0, UiFactory.Label("L_EntreeWhite", "White Forest Lv:"), UiFactory.Row(NUD_EntreeWhiteLV, NUD_EntreeWhiteEXP));
        UiFactory.AddFormRow(levels, 1, UiFactory.Label("L_EntreeBlack", "Black City Lv:"), UiFactory.Row(NUD_EntreeBlackLV, NUD_EntreeBlackEXP));

        GB_PassPowers = new GroupBoxView("GB_PassPowers", "Pass Powers", UiFactory.Column(CB_PassPower1, CB_PassPower2, CB_PassPower3));

        PAN_MissionMeta = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_FMHosted", "Hosted:"), NUD_FMHosted),
            UiFactory.Row(UiFactory.Label("L_FMParticipated", "Participated:"), NUD_FMParticipated),
            UiFactory.Row(UiFactory.Label("L_FMCompleted", "Completed:"), NUD_FMCompleted),
            UiFactory.Row(UiFactory.Label("L_FMTopScores", "Top Scores:"), NUD_FMTopScores),
            UiFactory.Row(UiFactory.Label("L_FMMostParticipants", "Most Participants:"), NUD_FMMostParticipants));

        var missionDetail = UiFactory.Column(
            UiFactory.Row(L_FMUnlocked, L_FMLocked),
            CHK_FMNew,
            UiFactory.Row(UiFactory.Label("L_FMLevel", "Level:"), CB_FMLevel),
            UiFactory.Row(UiFactory.Label("L_FMBestScore", "Best Score:"), NUD_FMBestScore),
            UiFactory.Row(UiFactory.Label("L_FMBestTotal", "Best Total:"), NUD_FMBestTotal),
            B_FunfestMissions);
        var missions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        missions.Children.Add(LB_FunfestMissions);
        missions.Children.Add(missionDetail);
        GB_FunfestMissions = new GroupBoxView("GB_FunfestMissions", "Funfest Missions", missions);

        var entralink = UiFactory.Column(levels, GB_PassPowers, PAN_MissionMeta, GB_FunfestMissions);
        tabs.Items.Add(new TabItem { Name = "TAB_Entralink", Header = "Entralink", Content = new ScrollViewer { Content = entralink, MaxHeight = 560 } });

        // Entree Forest
        var slotDetail = UiFactory.FormGrid(6);
        UiFactory.AddFormRow(slotDetail, 0, UiFactory.Label("L_Species", "Species:"), UiFactory.Row(CB_Species, PB_SlotPreview));
        UiFactory.AddFormRow(slotDetail, 1, L_Form, CB_Form);
        UiFactory.AddFormRow(slotDetail, 2, UiFactory.Label("L_Gender", "Gender:"), CB_Gender);
        UiFactory.AddFormRow(slotDetail, 3, UiFactory.Label("L_Move", "Move:"), CB_Move);
        UiFactory.AddFormRow(slotDetail, 4, UiFactory.Label("L_Animation", "Animation:"), NUD_Animation);
        UiFactory.AddFormRow(slotDetail, 5, UiFactory.Label("L_Unlocked", "Areas Unlocked:"), UiFactory.Row(NUD_Unlocked, CHK_Area9));

        var forest = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        forest.Children.Add(UiFactory.Column(CB_Areas, LB_Slots, B_RandForest));
        forest.Children.Add(slotDetail);
        tabs.Items.Add(new TabItem { Name = "TAB_Forest", Header = "Entree Forest", Content = forest });

        // Subway
        var subwayGrid = new Grid { ColumnSpacing = 6, RowSpacing = 3 };
        for (int i = 0; i < 4; i++)
            subwayGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (int i = 0; i < SubwayRows.Length; i++)
        {
            subwayGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var row = SubwayRows[i];
            UiFactory.SetRowCol(row.Title, i, 0);
            UiFactory.SetRowCol(row.Set, i, 1);
            UiFactory.SetRowCol(row.Past, i, 2);
            UiFactory.SetRowCol(row.Record, i, 3);
            subwayGrid.Children.Add(row.Title);
            subwayGrid.Children.Add(row.Set);
            subwayGrid.Children.Add(row.Past);
            subwayGrid.Children.Add(row.Record);
        }
        var subway = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_CurrentType", "Current Type:"), NUD_CurrentType, UiFactory.Label("L_CurrentBattle", "Battle:"), NUD_CurrentBattle),
            UiFactory.Row(CHK_Subway0, CHK_Subway1, CHK_Subway2, CHK_Subway7),
            UiFactory.Row(CHK_SuperSingle, CHK_SuperDouble, CHK_SuperMulti, CHK_SWNPCMet),
            subwayGrid);
        tabs.Items.Add(new TabItem { Name = "TAB_Subway", Header = "Battle Subway", Content = new ScrollViewer { Content = subway, MaxHeight = 560 } });

        // Musical
        tabs.Items.Add(new TabItem { Name = "TAB_Musical", Header = "Musical", Content = UiFactory.Column(CLB_MusicalProps, B_UnlockAllProps) });

        // Records
        var records = UiFactory.FormGrid(2);
        UiFactory.AddFormRow(records, 0, UiFactory.Label("L_Record16", "Record (16-bit):"), UiFactory.Row(NUD_Record16, NUD_Record16V));
        UiFactory.AddFormRow(records, 1, UiFactory.Label("L_Record32", "Record (32-bit):"), UiFactory.Row(NUD_Record32, NUD_Record32V));
        tabs.Items.Add(new TabItem { Name = "TAB_Records", Header = "Records", Content = records });

        SetBody(tabs);

        LB_FunfestMissions.ItemsSource = MissionItems;
        LB_Slots.ItemsSource = SlotItems;

        B_AllFlyDest.Click += (_, _) => CLB_FlyDest.SetAllChecked(true);
        B_AllKeys.Click += (_, _) => CLB_KeySystem.SetAllChecked(true);
        B_UnlockAllProps.Click += (_, _) =>
        {
            SAV.Musical.UnlockAllMusicalProps();
            B_UnlockAllProps.IsEnabled = false;
            ReadMusical();
        };
        B_DumpFC.Click += async (_, _) => await DumpForestCity();
        B_ImportFC.Click += async (_, _) => await ImportForestCity();
        B_RandForest.Click += (_, _) => RandomizeForest();
        CB_Areas.SelectionChanged += (_, _) => ChangeArea();
        LB_Slots.SelectionChanged += (_, _) => ChangeSlot();
        CB_Species.SelectionChanged += (_, _) => UpdateSlotValue(CB_Species);
        CB_Move.SelectionChanged += (_, _) => UpdateSlotValue(CB_Move);
        CB_Gender.SelectionChanged += (_, _) => UpdateSlotValue(CB_Gender);
        CB_Form.SelectionChanged += (_, _) => UpdateSlotValue(CB_Form);
        NUD_Animation.ValueChanged += (_, _) => UpdateSlotValue(NUD_Animation);
        LB_FunfestMissions.SelectionChanged += (_, _) => { editing = true; LoadFestaMissionRecord(); editing = false; };
        foreach (var c in new Control[] { CHK_FMNew, CB_FMLevel, NUD_FMBestScore, NUD_FMBestTotal })
        {
            switch (c)
            {
                case CheckBox chk: chk.IsCheckedChanged += (_, _) => ChangeFestaMissionValue(); break;
                case ComboBox cb: cb.SelectionChanged += (_, _) => ChangeFestaMissionValue(); break;
                case NumericUpDown nud: nud.ValueChanged += (_, _) => ChangeFestaMissionValue(); break;
            }
        }
        B_FunfestMissions.Click += (_, _) =>
        {
            ((SAV5B2W2)SAV).Festa.UnlockAllFunfestMissions();
            L_FMUnlocked.IsVisible = true;
            L_FMLocked.IsVisible = false;
        };
        NUD_EntreeWhiteLV.ValueChanged += (_, _) => { if (!editing) { SetNudMax(false); SetEntreeExpTip(false); } };
        NUD_EntreeBlackLV.ValueChanged += (_, _) => { if (!editing) { SetNudMax(true); SetEntreeExpTip(true); } };
        NUD_EntreeWhiteEXP.ValueChanged += (_, _) => { if (!editing) SetEntreeExpTip(false); };
        NUD_EntreeBlackEXP.ValueChanged += (_, _) => { if (!editing) SetEntreeExpTip(true); };
        foreach (var row in SubwayRows)
            row.Set.IsCheckedChanged += (_, _) => row.UpdateTitleSuffix();
    }

    #endregion

    #region Main tab

    private void ReadMain()
    {
        string[] flyDestA;
        switch (SAV.Version)
        {
            case GameVersion.B or GameVersion.W or GameVersion.BW:
                ofsFly = 0x204B2;
                flyDestA = [
                    "Nuvema Town", "Accumula Town", "Striaton City", "Nacrene City",
                    "Castelia City", "Nimbasa City", "Driftveil City", "Mistralton City",
                    "Icirrus City", "Opelucid City", "Victory Road", "Pokemon League",
                    "Lacunosa Town", "Undella Town", "Black City/White Forest", "(Unity Tower)",
                ];
                FlyDestC = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 15, 11, 10, 13, 12, 14];
                break;
            case GameVersion.B2 or GameVersion.W2 or GameVersion.B2W2:
                ofsFly = 0x20392;
                flyDestA = [
                    "Aspertia City", "Floccesy Town", "Virbank City",
                    "Nuvema Town", "Accumula Town", "Striaton City", "Nacrene City",
                    "Castelia City", "Nimbasa City", "Driftveil City", "Mistralton City",
                    "Icirrus City", "Opelucid City",
                    "Lacunosa Town", "Undella Town", "Black City/White Forest",
                    "Lentimas Town", "Humilau City", "Victory Road", "Pokemon League",
                    "Pokestar Studios", "Join Avenue", "PWT", "(Unity Tower)",
                ];
                FlyDestC = [24, 27, 25, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 21, 20, 28, 26, 66, 19, 5, 6, 7, 22];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(SAV), SAV.Version, "Unsupported version.");
        }

        uint valFly = BinaryPrimitives.ReadUInt32LittleEndian(SAV.Data[ofsFly..]);
        CLB_FlyDest.ClearItems();
        for (int i = 0; i < flyDestA.Length; i++)
        {
            bool set = FlyDestC[i] < 32
                ? (valFly & (1u << FlyDestC[i])) != 0
                : (SAV.Data[ofsFly + (FlyDestC[i] >> 3)] & (1 << (FlyDestC[i] & 7))) != 0;
            CLB_FlyDest.Add(flyDestA[i], set);
        }

        if (SAV is SAV5BW bw)
        {
            GB_KeySystem.IsVisible = false;
            var roamers = new[] { CB_Roamer642, CB_Roamer641 };
            for (int i = 0; i < roamers.Length; i++)
            {
                byte c = bw.Encount.GetRoamerState(i);
                var states = GetStates();
                if (states.All(z => z.Value != c))
                    states.Add(new ComboItem($"Unknown (0x{c:X2})", c));
                roamers[i].SetItems(states.Where(v => v.Value >= 2 || v.Value == c).ToList());
                roamers[i].SetValue(c);
            }

            var current = bw.EventWork.GetWorkRoamer();
            var statuses = GetRoamStatusStates();
            if (statuses.All(z => z.Value != current))
                statuses.Add(new ComboItem($"Unknown (0x{current:X2})", current));
            CB_RoamStatus.SetItems(statuses);
            CB_RoamStatus.SetValue(current);

            CHK_LibertyPass.IsChecked = bw.Misc.IsLibertyTicketActivated;
        }
        else if (SAV is SAV5B2W2 b2w2)
        {
            RemoveTab("TAB_BWCityForest");
            GB_Roamer.IsVisible = CHK_LibertyPass.IsVisible = false;

            var keys = b2w2.Keys;
            string[] keySystemA =
            [
                "Obtain EasyKey", "Obtain ChallengeKey", "Obtain CityKey", "Obtain IronKey", "Obtain IcebergKey",
                "Unlock EasyMode", "Unlock ChallengeMode", "Unlock City", "Unlock IronChamber", "Unlock IcebergChamber",
            ];
            CLB_KeySystem.ClearItems();
            for (int i = 0; i < 5; i++)
            {
                CLB_KeySystem.Add(keySystemA[i], keys.GetIsKeyObtained((KeyType5)i));
                CLB_KeySystem.Add(keySystemA[i + 5], keys.GetIsKeyUnlocked((KeyType5)i));
            }
        }
        else
        {
            RemoveTab("TAB_BWCityForest");
            GB_KeySystem.IsVisible = GB_Roamer.IsVisible = CHK_LibertyPass.IsVisible = false;
        }
    }

    private void RemoveTab(string name)
    {
        if (Body is not TabControl tabs)
            return;
        var tab = tabs.Items.OfType<TabItem>().FirstOrDefault(z => z.Name == name);
        if (tab is not null)
            tabs.Items.Remove(tab);
    }

    private static List<ComboItem> GetStates() =>
    [
        new("Not roamed", 0), new("Roaming", 1), new("Defeated", 2), new("Captured", 3),
    ];

    private static List<ComboItem> GetRoamStatusStates() =>
    [
        new("Not happened", 0), new("Go to route 7", 1), new("Event finished", 3),
    ];

    private void SaveMain()
    {
        uint valFly = BinaryPrimitives.ReadUInt32LittleEndian(SAV.Data[ofsFly..]);
        for (int i = 0; i < CLB_FlyDest.Count; i++)
        {
            bool set = CLB_FlyDest.GetItemChecked(i);
            if (FlyDestC[i] < 32)
            {
                if (set)
                    valFly |= 1u << FlyDestC[i];
                else
                    valFly &= ~(1u << FlyDestC[i]);
            }
            else
            {
                var ofs = ofsFly + (FlyDestC[i] >> 3);
                SAV.Data[ofs] = (byte)((SAV.Data[ofs] & ~(1 << (FlyDestC[i] & 7))) | ((set ? 1 : 0) << (FlyDestC[i] & 7)));
            }
        }
        BinaryPrimitives.WriteUInt32LittleEndian(SAV.Data[ofsFly..], valFly);

        if (SAV is SAV5BW bw)
        {
            var encount = bw.Encount;
            var roamers = new[] { CB_Roamer642, CB_Roamer641 };
            for (int i = 0; i < roamers.Length; i++)
            {
                int c = encount.GetRoamerState(i);
                var d = (byte)(roamers[i].GetSelectedItem()?.Value ?? 0);
                if (c == d)
                    continue;
                encount.SetRoamerState(i, d);
                if (c != 1)
                    continue;
                var roamer = i == 0 ? encount.Roamer1 : encount.Roamer2;
                roamer.Clear();
                encount.SetRoamerState2C(i, 0);
            }

            bw.EventWork.SetWorkRoamer((ushort)(CB_RoamStatus.GetSelectedItem()?.Value ?? 0));

            var liberty = CHK_LibertyPass.IsChecked == true;
            if (liberty != bw.Misc.IsLibertyTicketActivated)
                bw.Misc.IsLibertyTicketActivated = liberty;
        }
        else if (SAV is SAV5B2W2 b2w2)
        {
            var keys = b2w2.Keys;
            for (int i = 0; i < 5; i++)
            {
                var index = i * 2;
                var obtain = CLB_KeySystem.GetItemChecked(index);
                if (obtain != keys.GetIsKeyObtained((KeyType5)i))
                    keys.SetIsKeyObtained((KeyType5)i, obtain);
                var unlock = CLB_KeySystem.GetItemChecked(index + 1);
                if (unlock != keys.GetIsKeyUnlocked((KeyType5)i))
                    keys.SetIsKeyUnlocked((KeyType5)i, unlock);
            }
        }
    }

    #endregion

    #region Entralink

    private void ReadEntralink()
    {
        var entree = SAV.Entralink;
        editing = true;

        NUD_EntreeWhiteLV.SetValueClamped(entree.WhiteForestLevel);
        NUD_EntreeBlackLV.SetValueClamped(entree.BlackCityLevel);

        if (SAV is SAV5B2W2 b2w2)
        {
            var pass = (Entralink5B2W2)entree;
            var ppv = Enum.GetValues<PassPower5>();
            var ppn = Translator.GetEnumTranslation<PassPower5>(MainWindow.CurrentLanguage);
            var powers = new ComboItem[ppv.Length];
            for (int i = 0; i < ppv.Length; i++)
                powers[i] = new ComboItem(ppn[i], (int)ppv[i]);
            foreach (var cb in new[] { CB_PassPower1, CB_PassPower2, CB_PassPower3 })
                cb.SetItems(powers);

            CB_PassPower1.SetValue(pass.PassPower1);
            CB_PassPower2.SetValue(pass.PassPower2);
            CB_PassPower3.SetValue(pass.PassPower3);

            var block = b2w2.Festa;
            NUD_FMHosted.SetValueClamped(block.Hosted);
            NUD_FMParticipated.SetValueClamped(block.Participated);
            NUD_FMCompleted.SetValueClamped(block.Completed);
            NUD_FMTopScores.SetValueClamped(block.TopScores);
            NUD_FMMostParticipants.SetValueClamped(block.Participants);
            NUD_EntreeWhiteEXP.SetValueClamped(block.WhiteEXP);
            NUD_EntreeBlackEXP.SetValueClamped(block.BlackEXP);

            MissionItems.Clear();
            foreach (var title in Translator.GetEnumTranslation<Funfest5Mission>(MainWindow.CurrentLanguage))
                MissionItems.Add(title);

            CB_FMLevel.SetItems([
                new("Lv.1", 0), new("Lv.2 +", 1), new("Lv.3 ++", 2), new("Lv.3 +++", 3),
                new(GameInfo.Strings.specieslist[0], 7),
            ]);
            SetNudMax(null);
            SetEntreeExpTip(null);
            LB_FunfestMissions.SelectedIndex = 0;
            LoadFestaMissionRecord();
        }
        else
        {
            GB_PassPowers.IsVisible = false;
            PAN_MissionMeta.IsVisible = false;
            GB_FunfestMissions.IsVisible = false;
            NUD_EntreeWhiteEXP.IsVisible = NUD_EntreeBlackEXP.IsVisible = false;
        }
        editing = false;
    }

    private void SaveEntralink()
    {
        var entree = SAV.Entralink;
        entree.WhiteForestLevel = (ushort)(NUD_EntreeWhiteLV.Value ?? 0);
        entree.BlackCityLevel = (ushort)(NUD_EntreeBlackLV.Value ?? 0);

        if (SAV is not SAV5B2W2 b2w2)
            return;

        var pass = (Entralink5B2W2)entree;
        if (CB_PassPower1.SelectedIndex >= 0)
            pass.PassPower1 = (byte)(CB_PassPower1.GetSelectedItem()?.Value ?? 0);
        if (CB_PassPower2.SelectedIndex >= 0)
            pass.PassPower2 = (byte)(CB_PassPower2.GetSelectedItem()?.Value ?? 0);
        if (CB_PassPower3.SelectedIndex >= 0)
            pass.PassPower3 = (byte)(CB_PassPower3.GetSelectedItem()?.Value ?? 0);

        var block = b2w2.Festa;
        block.Hosted = (ushort)(NUD_FMHosted.Value ?? 0);
        block.Participated = (ushort)(NUD_FMParticipated.Value ?? 0);
        block.Completed = (ushort)(NUD_FMCompleted.Value ?? 0);
        block.TopScores = (ushort)(NUD_FMTopScores.Value ?? 0);
        block.WhiteEXP = (byte)(NUD_EntreeWhiteEXP.Value ?? 0);
        block.BlackEXP = (byte)(NUD_EntreeBlackEXP.Value ?? 0);
        block.Participants = (byte)(NUD_FMMostParticipants.Value ?? 0);
    }

    /// <param name="isBlack">null updates both sides; true skips the update (matching the WinForms guard).</param>
    private void SetNudMax(bool? isBlack)
    {
        if (isBlack == true)
            return;
        for (int i = 0; i < 2; i++)
        {
            var lvl = i == 0 ? NUD_EntreeWhiteLV : NUD_EntreeBlackLV;
            var exp = i == 0 ? NUD_EntreeWhiteEXP : NUD_EntreeBlackEXP;
            var lv = (int)(lvl.Value ?? 0);
            var expMax = FestaBlock5.GetExpNeededForLevelUp(lv) - 1;
            if (exp.Value > expMax)
                exp.Value = expMax;
            exp.Maximum = expMax;
        }
    }

    private void SetEntreeExpTip(bool? isBlack)
    {
        for (int i = 0; i < 2; i++)
        {
            if (isBlack == true)
                continue;
            var lvl = i == 0 ? NUD_EntreeWhiteLV : NUD_EntreeBlackLV;
            var exp = i == 0 ? NUD_EntreeWhiteEXP : NUD_EntreeBlackEXP;
            var lv = (int)(lvl.Value ?? 0);
            var totalExp = FestaBlock5.GetTotalEntreeExp(lv) + (int)(exp.Value ?? 0);
            var toNext = lv == 999 ? -1 : FestaBlock5.GetExpNeededForLevelUp(lv) - (int)(exp.Value ?? 0);
            var tip = $"{(i == 0 ? "White" : "Black")} LV {lv}{Environment.NewLine}Exp.Points: {totalExp}{Environment.NewLine}To Next Lv: {toNext}";
            ToolTip.SetTip(lvl, tip);
            ToolTip.SetTip(exp, tip);
        }
    }

    private void LoadFestaMissionRecord()
    {
        if (SAV is not SAV5B2W2 b2w2)
            return;
        var block = b2w2.Festa;
        int mission = LB_FunfestMissions.SelectedIndex;
        if ((uint)mission > FestaBlock5.MaxMissionIndex)
            return;
        bool unlocked = block.IsFunfestMissionUnlocked(mission);
        L_FMUnlocked.IsVisible = unlocked;
        L_FMLocked.IsVisible = !unlocked;

        var record = block.GetMissionRecord(mission);
        CHK_FMNew.IsChecked = record.IsNew;
        CB_FMLevel.SetValue(record.Level);
        NUD_FMBestScore.SetValueClamped(record.Score);
        NUD_FMBestTotal.SetValueClamped(record.Total);
    }

    private void ChangeFestaMissionValue()
    {
        if (editing || SAV is not SAV5B2W2 b2w2)
            return;
        int mission = LB_FunfestMissions.SelectedIndex;
        if ((uint)mission > FestaBlock5.MaxMissionIndex)
            return;
        var score = new Funfest5Score(
            (int)(NUD_FMBestTotal.Value ?? 0),
            (int)(NUD_FMBestScore.Value ?? 0),
            CB_FMLevel.GetSelectedItem()?.Value ?? 0,
            CHK_FMNew.IsChecked == true);
        b2w2.Festa.SetMissionRecord(mission, score);
    }

    #endregion

    #region Entree Forest

    private void LoadForest()
    {
        Forest = SAV.EntreeForest;
        Forest.EnsureDecrypted();
        AllSlots = Forest.Slots;
        NUD_Unlocked.SetValueClamped(Forest.Unlock38Areas + 2);
        CHK_Area9.IsChecked = Forest.Unlock9thArea;

        var areas = AllSlots.Select(z => z.Area).Distinct().Select(z => new ComboItem(z.ToString(), (int)z)).ToList();
        var filtered = GameInfo.FilteredSources;
        CB_Species.SetItems(filtered.Species);
        CB_Move.SetItems(filtered.Moves);
        CB_Areas.SetItems(areas);
        CB_Areas.SelectedIndex = 0;
    }

    private void SaveForest()
    {
        Forest.Unlock38Areas = (int)(NUD_Unlocked.Value ?? 2) - 2;
        Forest.Unlock9thArea = CHK_Area9.IsChecked == true;
    }

    private void ChangeArea()
    {
        var area = CB_Areas.GetSelectedItem()?.Value ?? 0;
        CurrentSlots = AllSlots.Where(z => (int)z.Area == area).ToArray();
        SlotItems.Clear();
        foreach (var z in CurrentSlots)
            SlotItems.Add(GetSpeciesName(z.Species));
        LB_Slots.SelectedIndex = currentIndex = 0;
    }

    private void ChangeSlot()
    {
        CurrentSlot = null;
        if (LB_Slots.SelectedIndex >= 0)
            currentIndex = LB_Slots.SelectedIndex;
        if ((uint)currentIndex >= CurrentSlots.Count)
            return;
        var current = CurrentSlots[currentIndex];
        CB_Species.SetValue(current.Species);
        SetForms(current);
        SetGenders(current);
        CB_Move.SetValue(current.Move);
        CB_Gender.SetValue(current.Gender);
        CB_Form.SelectedIndex = CB_Form.ItemCount <= current.Form ? 0 : current.Form;
        NUD_Animation.SetValueClamped((int)current.Animation);
        CurrentSlot = current;
        SetSprite(current);
    }

    private static string GetSpeciesName(ushort species)
    {
        var arr = GameInfo.Strings.Species;
        return species >= arr.Count ? $"Invalid: {species}" : arr[species];
    }

    private void UpdateSlotValue(object sender)
    {
        if (CurrentSlot is null)
            return;

        if (ReferenceEquals(sender, CB_Species))
        {
            CurrentSlot.Species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
            if ((uint)currentIndex < SlotItems.Count)
                SlotItems[currentIndex] = GetSpeciesName(CurrentSlot.Species);
            SetForms(CurrentSlot);
            SetGenders(CurrentSlot);
        }
        else if (ReferenceEquals(sender, CB_Move))
        {
            CurrentSlot.Move = (ushort)(CB_Move.GetSelectedItem()?.Value ?? 0);
        }
        else if (ReferenceEquals(sender, CB_Gender))
        {
            CurrentSlot.Gender = (byte)(CB_Gender.GetSelectedItem()?.Value ?? 0);
        }
        else if (ReferenceEquals(sender, CB_Form))
        {
            CurrentSlot.Form = (byte)Math.Max(0, CB_Form.SelectedIndex);
        }
        else if (ReferenceEquals(sender, NUD_Animation))
        {
            CurrentSlot.Animation = (EntreeForestAnimation)(NUD_Animation.Value ?? 0);
        }

        SetSprite(CurrentSlot);
    }

    private void SetSprite(EntreeSlot slot)
    {
        var sprite = SpriteUtil.GetSprite(slot.Species, slot.Form, slot.Gender, 0, 0, false, Shiny.Never, EntityContext.Gen5);
        PB_SlotPreview.Source = sprite.ToAvaloniaBitmapAndDispose();
    }

    private void SetGenders(EntreeSlot slot) => CB_Gender.SetItems(GetGenderChoices(slot.Species));

    private static List<ComboItem> GetGenderChoices(ushort species)
    {
        if (species == 0)
            return [new("-", 0)];
        var pi = PersonalTable.B2W2[species];
        if (pi.Genderless)
            return [new("Genderless", 2)];

        var list = new List<ComboItem>();
        if (!pi.OnlyFemale)
            list.Add(new ComboItem("Male", 0));
        if (!pi.OnlyMale)
            list.Add(new ComboItem("Female", 1));
        return list;
    }

    private void SetForms(EntreeSlot slot)
    {
        bool hasForms = PersonalTable.B2W2[slot.Species].HasForms || slot.Species == (int)Species.Mothim;
        L_Form.IsVisible = CB_Form.IsEnabled = CB_Form.IsVisible = hasForms;

        CB_Form.Items.Clear();
        foreach (var f in FormConverter.GetFormList(slot.Species, GameInfo.Strings.types, GameInfo.Strings.forms, GameInfo.GenderSymbolUnicode, SAV.Context))
            CB_Form.Items.Add(f);
    }

    private void RandomizeForest()
    {
        var source = (SAV is SAV5BW ? Encounters5BW.DreamWorld_BW : Encounters5B2W2.DreamWorld_B2W2).Concat(Encounters5DR.DreamWorld_Common).ToList();
        var rnd = Util.Rand;
        foreach (var s in AllSlots)
        {
            int index = rnd.Next(source.Count);
            var slot = source[index];
            source.Remove(slot);
            s.Species = slot.Species;
            s.Form = slot.Form;
            s.Gender = !((IFixedGender)slot).IsFixedGender ? PersonalTable.B2W2[slot.Species].RandomGender() : slot.Gender;

            ReadOnlySpan<ushort> moves = slot.Moves;
            var count = moves.Length - moves.Count<ushort>(0);
            s.Move = count == 0 ? (ushort)0 : moves[rnd.Next(count)];
        }
        ChangeArea();
        NUD_Unlocked.Value = 8;
        CHK_Area9.IsChecked = true;
    }

    private const string ForestCityBinFilter = "Forest City Bin|*.fc5";

    private async Task DumpForestCity()
    {
        if (SAV is not SAV5BW bw)
            return;
        var path = await FileDialogs.SaveFileDialog(this, ForestCityBinFilter, $"{SAV.Version}.fc5");
        if (path is null)
            return;
        await File.WriteAllBytesAsync(path, bw.Forest.ForestCity.Span.ToArray());
    }

    private async Task ImportForestCity()
    {
        if (SAV is not SAV5BW bw)
            return;
        var path = await FileDialogs.OpenSingleFile(this, ForestCityBinFilter);
        if (path is null)
            return;

        var fi = new FileInfo(path);
        if (fi.Length != WhiteBlack5BW.ForestCitySize)
        {
            await AppDialogs.Alert(this, string.Format(MessageStrings.MsgFileSizeIncorrect, fi.Length, WhiteBlack5BW.ForestCitySize));
            return;
        }
        var data = await File.ReadAllBytesAsync(path);
        bw.SetData(bw.Forest.ForestCity.Span, data);
    }

    #endregion

    #region Subway

    private void ReadSubway()
    {
        NUD_CurrentType.SetValueClamped(swp.CurrentType);
        NUD_CurrentBattle.SetValueClamped(swp.CurrentBattle);

        CHK_Subway0.IsChecked = sw.Flag0;
        CHK_Subway1.IsChecked = sw.Flag1;
        CHK_Subway2.IsChecked = sw.Flag2;
        CHK_Subway7.IsChecked = sw.Flag7;
        CHK_SuperSingle.IsChecked = sw.SuperSingle;
        CHK_SuperDouble.IsChecked = sw.SuperDouble;
        CHK_SuperMulti.IsChecked = sw.SuperMulti;
        CHK_SWNPCMet.IsChecked = sw.NPCMet;

        ReadSubwayRow(0, sw.SingleSet, sw.SinglePast, sw.SingleRecord);
        ReadSubwayRow(1, sw.DoubleSet, sw.DoublePast, sw.DoubleRecord);
        ReadSubwayRow(2, sw.MultiNPCSet, sw.MultiNPCPast, sw.MultiNPCRecord);
        ReadSubwayRow(3, sw.MultiFriendsSet, sw.MultiFriendsPast, sw.MultiFriendsRecord);
        ReadSubwayRow(4, sw.SuperSingleSet, sw.SuperSinglePast, sw.SuperSingleRecord);
        ReadSubwayRow(5, sw.SuperDoubleSet, sw.SuperDoublePast, sw.SuperDoubleRecord);
        ReadSubwayRow(6, sw.SuperMultiNPCSet, sw.SuperMultiNPCPast, sw.SuperMultiNPCRecord);
        ReadSubwayRow(7, sw.SuperMultiFriendsSet, sw.SuperMultiFriendsPast, sw.SuperMultiFriendsRecord);
    }

    private void ReadSubwayRow(int index, int set, int past, int record)
    {
        var row = SubwayRows[index];
        row.Set.IsChecked = set == (past / 7) + 1;
        row.Past.SetValueClamped(past);
        row.Record.SetValueClamped(record);
        row.UpdateTitleSuffix();
    }

    private void SaveSubway()
    {
        swp.CurrentType = (int)(NUD_CurrentType.Value ?? 0);
        swp.CurrentBattle = (int)(NUD_CurrentBattle.Value ?? 0);

        sw.Flag0 = CHK_Subway0.IsChecked == true;
        sw.Flag1 = CHK_Subway1.IsChecked == true;
        sw.Flag2 = CHK_Subway2.IsChecked == true;
        sw.Flag3 = CHK_Subway7.IsChecked == true;
        sw.SuperSingle = CHK_SuperSingle.IsChecked == true;
        sw.SuperDouble = CHK_SuperDouble.IsChecked == true;
        sw.SuperMulti = CHK_SuperMulti.IsChecked == true;
        sw.Flag7 = CHK_Subway7.IsChecked == true;
        sw.NPCMet = CHK_SWNPCMet.IsChecked == true;

        sw.SinglePast = Past(0); sw.SingleRecord = Record(0);
        sw.DoublePast = Past(1); sw.DoubleRecord = Record(1);
        sw.MultiNPCPast = Past(2); sw.MultiNPCRecord = Record(2);
        sw.MultiFriendsPast = Past(3); sw.MultiFriendsRecord = Record(3);
        sw.SuperSinglePast = Past(4); sw.SuperSingleRecord = Record(4);
        sw.SuperDoublePast = Past(5); sw.SuperDoubleRecord = Record(5);
        sw.SuperMultiNPCPast = Past(6); sw.SuperMultiNPCRecord = Record(6);
        sw.SuperMultiFriendsPast = Past(7); sw.SuperMultiFriendsRecord = Record(7);

        sw.SingleSet = SetValue(0, sw.SinglePast);
        sw.DoubleSet = SetValue(1, sw.DoublePast);
        sw.MultiNPCSet = SetValue(2, sw.MultiNPCPast);
        sw.MultiFriendsSet = SetValue(3, sw.MultiFriendsPast);
        sw.SuperSingleSet = SetValue(4, sw.SuperSinglePast);
        sw.SuperDoubleSet = SetValue(5, sw.SuperDoublePast);
        sw.SuperMultiNPCSet = SetValue(6, sw.SuperMultiNPCPast);
        sw.SuperMultiFriendsSet = SetValue(7, sw.SuperMultiFriendsPast);

        int Past(int i) => (int)(SubwayRows[i].Past.Value ?? 0);
        int Record(int i) => (int)(SubwayRows[i].Record.Value ?? 0);
        int SetValue(int i, int past) => SubwayRows[i].Set.IsChecked == true ? (past / 7) + 1 : 0;
    }

    #endregion

    #region Musical / records

    private void ReadMusical()
    {
        for (int i = 0; i < PropNames.Length && i < CLB_MusicalProps.Count; i++)
            CLB_MusicalProps.SetItemChecked(i, SAV.Musical.GetHasProp(i));
    }

    private void SaveMusical()
    {
        for (int i = 0; i < PropNames.Length && i < CLB_MusicalProps.Count; i++)
            SAV.Musical.SetHasProp(i, CLB_MusicalProps.GetItemChecked(i));
    }

    private void ReadRecord()
    {
        var record = SAV.Records;
        NUD_Record16.Maximum = Record5.Record16 - 1;
        NUD_Record32.Maximum = Record5.Record32 - 1;
        NUD_Record16V.Value = record.GetRecord16(0);
        NUD_Record32V.Value = record.GetRecord32(0);
        NUD_Record16V.ValueChanged += (_, _) => record.SetRecord16((int)(NUD_Record16.Value ?? 0), (ushort)(NUD_Record16V.Value ?? 0));
        NUD_Record32V.ValueChanged += (_, _) => record.SetRecord32((int)(NUD_Record32.Value ?? 0), (uint)(NUD_Record32V.Value ?? 0));
        NUD_Record16.ValueChanged += (_, _) => NUD_Record16V.Value = record.GetRecord16((int)(NUD_Record16.Value ?? 0));
        NUD_Record32.ValueChanged += (_, _) => NUD_Record32V.Value = record.GetRecord32((int)(NUD_Record32.Value ?? 0));
    }

    #endregion

    protected override void OnSave()
    {
        SaveMain();
        SaveForest();
        SaveSubway();
        SaveEntralink();
        SaveMusical();
        SAV.Records.EndAccess();

        Forest.EnsureDecrypted(false);
        Origin.CopyChangesFrom(SAV);
        Close();
    }

    /// <summary>One Battle Subway discipline: whether the current streak is live, plus its past and record streaks.</summary>
    private sealed class SubwayRow
    {
        public TextBlock Title { get; }
        public CheckBox Set { get; }
        public NumericUpDown Past { get; }
        public NumericUpDown Record { get; }
        private readonly string BaseText;

        public SubwayRow(string title, string name)
        {
            BaseText = title;
            Title = UiFactory.Label($"L_{name}", title);
            Title.MinWidth = 140;
            Set = UiFactory.Check($"CHK_{name}Set", "Current");
            Past = UiFactory.NumericUpDown($"NUD_{name}", 0, ushort.MaxValue, 110);
            Record = UiFactory.NumericUpDown($"NUD_{name}Record", 0, ushort.MaxValue, 110);
        }

        /// <summary>Mirrors the WinForms label flip between "Current" and "Past".</summary>
        public void UpdateTitleSuffix() => Title.Text = $"{BaseText} ({(Set.IsChecked == true ? "Current" : "Past")})";
    }
}
