using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views.EntityEditors;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen7;

/// <summary>
/// Festival Plaza editor for Sun/Moon and Ultra Sun/Ultra Moon (port of the WinForms <c>SAV_FestivalPlaza</c>).
/// </summary>
public sealed class FestivalPlazaWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV7 SAV;

    private int entry;
    private bool editing;

    private readonly int typeMAX;
    private readonly FestaFacility[] f = new FestaFacility[JoinFesta7.FestaFacilityCount];
    private readonly string[] RES_Color = Translator.GetEnumTranslation<FestivalPlazaFacilityColor>(MainWindow.CurrentLanguage);

    private static ReadOnlySpan<byte> RewardState => [0, 2, 1]; // Indeterminate <-> Checked
    private static readonly string[] GenderSymbols = ["♂", "♀"];

    private readonly byte[][] RES_FacilityColor = // facility appearance
    [
        [0,1,2,3],//Lottery
        [4,0,5,3],//Haunted
        [1,0,5,3],//Goody
        [6,7,0,3],//Food
        [4,5,8,3],//Bouncy
        [0,1,2,3],//Fortune
        [0,7,8,4,5,1,9,10],//Dye
        [11,1,5,3],//Exchange
    ];

    private readonly byte[][] RES_FacilityLevelType = //3:123 4:135 5:12345
    [
        [5,5,5],
        [5,5,5],
        [3,5,3,3,3],
        [5,4,5,5],
        [5,5,5],
        [4,4,4,4,4,4,4],
        [4,4,4,4,4,4,4,4],
        [3],
    ];

    #region Controls

    private readonly TabControl TC_Editor = new() { Name = "TC_Editor" };

    // Overview
    private readonly TextBox TB_PlazaName = UiFactory.Text("TB_PlazaName", 20, 220);
    private readonly NumericUpDown NUD_Rank = UiFactory.NumericUpDown("NUD_Rank", 0, 999, 110);
    private readonly TextBlock L_RankFC = UiFactory.Label("L_RankFC", "9999999 - 9999999");
    private readonly NumericUpDown NUD_FC_Current = UiFactory.NumericUpDown("NUD_FC_Current", 0, 9999999, 130);
    private readonly NumericUpDown NUD_FC_Used = UiFactory.NumericUpDown("NUD_FC_Used", 0, 9999999, 130);
    private readonly TextBlock L_FC_CollectedV = UiFactory.Label("L_FC_CollectedV", "9999999");
    private readonly NumericUpDown NUD_MyMessageMeet = UiFactory.NumericUpDown("NUD_MyMessageMeet", 0, 9999, 110);
    private readonly NumericUpDown NUD_MyMessagePart = UiFactory.NumericUpDown("NUD_MyMessagePart", 0, 9999, 110);
    private readonly NumericUpDown NUD_MyMessageMoved = UiFactory.NumericUpDown("NUD_MyMessageMoved", 0, 9999, 110);
    private readonly NumericUpDown NUD_MyMessageDissapointed = UiFactory.NumericUpDown("NUD_MyMessageDissapointed", 0, 9999, 110);
    private readonly DatePicker CAL_FestaStartDate = new() { Name = "CAL_FestaStartDate", MinWidth = 0 };
    private readonly TimePicker CAL_FestaStartTime = new() { Name = "CAL_FestaStartTime", ClockIdentifier = "24HourClock", UseSeconds = true, MinWidth = 0 };

    // Unlock
    private readonly CheckedListView CLB_Phrases = new() { Name = "CLB_Phrases", Width = 330, Height = 360 };
    private readonly Button B_AllPhrases = UiFactory.Button("B_AllPhrases", "Check All");
    private readonly TriCheckedListView CLB_Reward = new() { Name = "CLB_Reward", Width = 250, Height = 300 };
    private readonly Button B_AllReceiveReward = UiFactory.Button("B_AllReceiveReward", "All received");
    private readonly Button B_AllReadyReward = UiFactory.Button("B_AllReadyReward", "All ready to receive");

    // Facility
    private readonly ListBox LB_FacilityIndex = new() { Name = "LB_FacilityIndex", Width = 60, Height = 200 };
    private readonly ComboBox CB_FacilityType = UiFactory.StringCombo("CB_FacilityType", 240);
    private readonly NumericUpDown NUD_FacilityColor = UiFactory.NumericUpDown("NUD_FacilityColor", 0, 7, 100);
    private readonly TextBlock L_FacilityColorV = UiFactory.Label("L_FacilityColorV", "colorvalue");
    private readonly ComboBox CB_FacilityNPC = UiFactory.StringCombo("CB_FacilityNPC", 180);
    private readonly CheckBox CHK_FacilityIntroduced = UiFactory.Check("CHK_FacilityIntroduced", "Visitor introduced this facility");
    private readonly TextBox TB_OTName = UiFactory.Text("TB_OTName", 12, 180);
    private readonly TextBlock Label_OTGender = UiFactory.Label("Label_OTGender", "G", clickable: true);
    private readonly ComboBox CB_FacilityMessage = UiFactory.StringCombo("CB_FacilityMessage", 140);
    private readonly NumericUpDown NUD_FacilityMessage = UiFactory.NumericUpDown("NUD_FacilityMessage", 0, 9999, 110);
    private readonly NumericTextBox TB_UsedFlags = UiFactory.Numeric("TB_UsedFlags", 8, 110, hex: true);
    private readonly NumericTextBox TB_UsedStats = UiFactory.Numeric("TB_UsedStats", 8, 110, hex: true);
    private readonly NumericTextBox TB_FacilityID = UiFactory.Numeric("TB_FacilityID", 24, 220, hex: true);
    private readonly TextBlock L_LuckyResult = UiFactory.Label("L_LuckyResult", "Result:");
    private readonly ComboBox CB_LuckyResult = UiFactory.StringCombo("CB_LuckyResult", 200);
    private readonly TextBlock L_Exchangable = UiFactory.Label("L_Exchangable", "exchange left count:");
    private readonly NumericUpDown NUD_Exchangable = UiFactory.NumericUpDown("NUD_Exchangable", 0, 255, 100);
    private readonly Button B_DelVisitor = UiFactory.Button("B_DelVisitor", "Delete Visitor Data");

    // Battle Agency (US/UM)
    private readonly SlotView[] PBs = [new(68, 56), new(68, 56), new(68, 56)];
    private readonly PKM[] p = new PKM[3];
    private readonly NumericUpDown[] NUD_Trainers =
    [
        UiFactory.NumericUpDown("NUD_Trainer1", 0, 210, 110),
        UiFactory.NumericUpDown("NUD_Trainer2", 0, 210, 110),
        UiFactory.NumericUpDown("NUD_Trainer3", 0, 210, 110),
    ];
    private readonly NumericUpDown NUD_Grade = UiFactory.NumericUpDown("NUD_Grade", 0, 50, 110);
    private readonly NumericUpDown NUD_Defeated = UiFactory.NumericUpDown("NUD_Defeated", 0, 15, 110);
    private readonly NumericUpDown NUD_DefeatMon = UiFactory.NumericUpDown("NUD_DefeatMon", 0, 65535, 110);
    private readonly CheckBox CHK_Choosed = UiFactory.Check("CHK_Choosed", "Choosed");
    private readonly CheckBox CHK_TrainerInvited = UiFactory.Check("CHK_TrainerInvited", "Invited");
    private readonly Button B_ImportParty = UiFactory.Button("B_ImportParty", "Import Party");
    private readonly Button B_AgentGlass = UiFactory.Button("B_AgentGlass", "Give Agent Sunglasses");

    #endregion

    public FestivalPlazaWindow(SAV7 sav) : base("SAV_FestivalPlaza", "Festival Plaza")
    {
        SAV = (SAV7)(Origin = sav).Clone();
        editing = true;
        entry = -1;
        typeMAX = SAV is SAV7USUM ? 0x7F : 0x7C;
        TB_PlazaName.Text = SAV.Festa.FestivalPlazaName;

        BuildLayout();

        var cc = SAV.Festa.FestaCoins;
        var cu = SAV.GetRecord(038);
        NUD_FC_Current.SetValueClamped(cc);
        NUD_FC_Used.SetValueClamped(cu);
        L_FC_CollectedV.Text = (cc + cu).ToString();

        var res = MainWindow.CurrentLanguage == "ja" ? PhrasesJapanese : PhrasesDefault;
        CLB_Phrases.ClearItems();
        CLB_Phrases.Add(res[^1], SAV.Festa.GetFestaPhraseUnlocked(106)); // add Lv100 before TentPhrases
        for (int i = 0; i < res.Length - 1; i++)
            CLB_Phrases.Add(res[i], SAV.Festa.GetFestaPhraseUnlocked(i));

        var dt = SAV.Festa.FestaDate ?? new DateTime(2000, 1, 1);
        CAL_FestaStartDate.SelectedDate = UiFactory.ToOffset(dt);
        CAL_FestaStartTime.SelectedTime = dt.TimeOfDay;

        string[] res2 = ["Rank 4: missions", "Rank 8: facility", "Rank 10: fashion", "Rank 20: rename", "Rank 30: special menu", "Rank 40: BGM", "Rank 50: theme Glitz", "Rank 60: theme Fairy", "Rank 70: theme Tone", "Rank 100: phrase", "Current Rank"];
        CLB_Reward.Add(res2[^1], ToState(SAV.Festa.GetFestPrizeReceived(10))); // add CurrentRank before const-rewards
        for (int i = 0; i < res2.Length - 1; i++)
            CLB_Reward.Add(res2[i], ToState(SAV.Festa.GetFestPrizeReceived(i)));

        for (int i = 0; i < JoinFesta7.FestaFacilityCount; i++)
            f[i] = SAV.Festa.GetFestaFacility(i);

        string[] res3 = ["Meet", "Part", "Moved", "Disappointed"];
        foreach (var s in res3)
            CB_FacilityMessage.Items.Add(s);

        string[] res5 =
        [
            "Ace Trainer" + GenderSymbols[1],
            "Ace Trainer" + GenderSymbols[0],
            "Veteran" + GenderSymbols[1],
            "Veteran" + GenderSymbols[0],
            "Office Worker" + GenderSymbols[0],
            "Office Worker" + GenderSymbols[1],
            "Punk Guy",
            "Punk Girl",
            "Breeder" + GenderSymbols[0],
            "Breeder" + GenderSymbols[1],
            "Youngster",
            "Lass",
        ];
        foreach (var s in res5)
            CB_FacilityNPC.Items.Add(s);

        string[] res6 = ["Lottery", "Haunted", "Goody", "Food", "Bouncy", "Fortune", "Dye", "Exchange"];
        string[][] res7 =
        [
            ["BigDream","GoldRush","TreasureHunt"],
            ["GhostsDen","TrickRoom","ConfuseRay"],
            ["Ball","General","Battle","SoftDrink","Pharmacy"],
            ["Rare","Battle", "FriendshipCafé", "FriendshipParlor"],
            ["Thump","Clink","Stomp"],
            ["Kanto","Johto","Hoenn","Sinnoh","Unova","Kalos","Pokémon"],
            ["Red","Yellow","Green","Blue","Orange","NavyBlue","Purple","Pink"],
            ["Switcheroo"],
        ];

        for (int k = 0; k < RES_FacilityLevelType.Length - (SAV is SAV7USUM ? 0 : 1); k++) // Exchange is US/UM only
        {
            var arr = RES_FacilityLevelType[k];
            for (int j = 0; j < arr.Length; j++)
            {
                var name = $"{res6[k]} {res7[k][j]}";
                var count = arr[j];
                if (count == 4)
                {
                    CB_FacilityType.Items.Add($"{name} 1");
                    CB_FacilityType.Items.Add($"{name} 3");
                    CB_FacilityType.Items.Add($"{name} 5");
                }
                else
                {
                    for (int i = 0; i < count; i++)
                        CB_FacilityType.Items.Add($"{name} {i + 1}");
                }
            }
        }

        string[] types = ["GTS", "Wonder Trade", "Battle Spot", "Festival Plaza", "mission", "lottery shop", "haunted house"];
        string[] lvl = ["+", "++", "+++"];
        CB_LuckyResult.Items.Add("none");
        foreach (var type in types)
        {
            foreach (var lv in lvl)
                CB_LuckyResult.Items.Add($"{lv} {type}");
        }

        NUD_Rank.SetValueClamped(SAV.Festa.FestaRank);
        LoadRankLabel(SAV.Festa.FestaRank);
        for (int i = 0; i < 4; i++)
            MyMessages[i].SetValueClamped(SAV.Festa.GetFestaMessage(i));

        if (SAV is SAV7USUM)
            LoadBattleAgency();

        AttachEvents();

        LB_FacilityIndex.SelectedIndex = 0;
        CB_FacilityMessage.SelectedIndex = 0;
        editing = false;

        entry = 0;
        LoadFacility();
    }

    private NumericUpDown[] MyMessages => [NUD_MyMessageMeet, NUD_MyMessagePart, NUD_MyMessageMoved, NUD_MyMessageDissapointed];

    private static bool? ToState(byte value) => value switch { 0 => false, 1 => null, _ => true };

    #region Layout

    private void BuildLayout()
    {
        TC_Editor.Items.Add(new TabItem { Name = "Tab_Overview", Header = "Overview", Content = BuildOverview() });
        TC_Editor.Items.Add(new TabItem { Name = "Tab_Unlock", Header = "Unlock", Content = BuildUnlock() });
        TC_Editor.Items.Add(new TabItem { Name = "Tab_Facility", Header = "Facility", Content = BuildFacility() });
        if (SAV is SAV7USUM)
            TC_Editor.Items.Add(new TabItem { Name = "Tab_BattleAgency", Header = "BattleAgency", Content = BuildBattleAgency() });
        TC_Editor.SelectedIndex = 0;
        SetBody(TC_Editor);
    }

    private Control BuildOverview()
    {
        var top = UiFactory.FormGrid(2);
        UiFactory.AddFormRow(top, 0, UiFactory.Label("L_PlazaName", "Festival Plaza Name:"), TB_PlazaName);
        UiFactory.AddFormRow(top, 1, UiFactory.Label("L_Rank", "Rank"), UiFactory.Row(NUD_Rank, L_RankFC));

        var coins = UiFactory.FormGrid(3);
        UiFactory.AddFormRow(coins, 0, UiFactory.Label("L_FC_Current", "Current:"), NUD_FC_Current);
        UiFactory.AddFormRow(coins, 1, UiFactory.Label("L_FC_Used", "Used:"), NUD_FC_Used);
        UiFactory.AddFormRow(coins, 2, UiFactory.Label("L_FC_CollectedL", "Collected:"), L_FC_CollectedV);

        var messages = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(messages, 0, UiFactory.Label("L_MyMessageMeet", "Meet:"), NUD_MyMessageMeet);
        UiFactory.AddFormRow(messages, 1, UiFactory.Label("L_MyMessagePart", "Part:"), NUD_MyMessagePart);
        UiFactory.AddFormRow(messages, 2, UiFactory.Label("L_MyMessageMoved", "Moved:"), NUD_MyMessageMoved);
        UiFactory.AddFormRow(messages, 3, UiFactory.Label("L_MyMessageDisappointed", "Disappointed:"), NUD_MyMessageDissapointed);

        var groups = UiFactory.Row(
            new GroupBoxView("GB_FC", "Festa Coins", coins),
            new GroupBoxView("GB_MyMessage", "Message Settings", messages),
            new GroupBoxView("GB_FestaStartTime", "Latest Start Time", UiFactory.Column(CAL_FestaStartDate, CAL_FestaStartTime)));
        groups.Spacing = 10;
        groups.VerticalAlignment = VerticalAlignment.Top;

        return new StackPanel { Orientation = Orientation.Vertical, Spacing = 10, Margin = new global::Avalonia.Thickness(6), Children = { top, groups } };
    }

    private Control BuildUnlock()
    {
        var phrases = new GroupBoxView("GB_Phrase", "Common Phrases+", UiFactory.Column(CLB_Phrases, B_AllPhrases));
        var rewards = new GroupBoxView("GB_Reward", "RankUP rewards", UiFactory.Column(CLB_Reward, B_AllReceiveReward, B_AllReadyReward));
        var row = UiFactory.Row(phrases, rewards);
        row.Spacing = 10;
        row.Margin = new global::Avalonia.Thickness(6);
        row.VerticalAlignment = VerticalAlignment.Top;
        return row;
    }

    private Control BuildFacility()
    {
        var form = UiFactory.FormGrid(8);
        UiFactory.AddFormRow(form, 0, UiFactory.Label("L_FacilityType", "type:"), CB_FacilityType);
        UiFactory.AddFormRow(form, 1, UiFactory.Label("L_FacilityColor", "color:"), UiFactory.Row(NUD_FacilityColor, L_FacilityColorV));
        UiFactory.AddFormRow(form, 2, UiFactory.Label("L_FacilityNPC", "NPC:"), CB_FacilityNPC);
        UiFactory.AddFormRow(form, 3, L_LuckyResult, CB_LuckyResult);
        UiFactory.AddFormRow(form, 4, L_Exchangable, NUD_Exchangable);
        UiFactory.AddFormRow(form, 5, UiFactory.Label("L_VisitorName", "Visitor Name:"), UiFactory.Row(TB_OTName, Label_OTGender));
        UiFactory.AddFormRow(form, 6, UiFactory.Label("L_UsedFlags", "Used Flags:"), TB_UsedFlags);
        UiFactory.AddFormRow(form, 7, UiFactory.Label("L_UsedStats", "Used Stats:"), TB_UsedStats);

        var id = UiFactory.Column(UiFactory.Label("L_FestaID", "Unknown value / Visitor FesID:"), TB_FacilityID);
        var messages = new GroupBoxView("GB_FacilityMessage", "Visitor message", UiFactory.Row(CB_FacilityMessage, NUD_FacilityMessage));

        var right = UiFactory.Column(form, CHK_FacilityIntroduced, id, messages, B_DelVisitor);
        right.Spacing = 6;

        foreach (var s in new[] { "1", "2", "3", "4", "5", "6", "7" })
            LB_FacilityIndex.Items.Add(s);

        var row = UiFactory.Row(LB_FacilityIndex, right);
        row.Spacing = 10;
        row.Margin = new global::Avalonia.Thickness(6);
        row.VerticalAlignment = VerticalAlignment.Top;
        return row;
    }

    private Control BuildBattleAgency()
    {
        var slots = UiFactory.Row(PBs[0], PBs[1], PBs[2]);
        slots.Spacing = 6;

        var form = UiFactory.FormGrid(3);
        UiFactory.AddFormRow(form, 0, UiFactory.Label("L_Grade", "Grade:"), NUD_Grade);
        UiFactory.AddFormRow(form, 1, UiFactory.Label("L_Defeated", "Defeated Trainer Count for Next Grade:"), NUD_Defeated);
        UiFactory.AddFormRow(form, 2, UiFactory.Label("L_DefeatMon", "Today's Defeated Pokemon Count:"), NUD_DefeatMon);

        var trainers = UiFactory.Column(
            UiFactory.Label("L_Note", "Upcoming 3 Trainers:\n(210: unset)"),
            UiFactory.Row(NUD_Trainers[0], NUD_Trainers[1], NUD_Trainers[2]));

        var others = new GroupBoxView("GB_Others", "Others", UiFactory.Row(CHK_Choosed, CHK_TrainerInvited));

        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Margin = new global::Avalonia.Thickness(6),
            Children = { UiFactory.Row(slots, B_ImportParty), form, trainers, others, B_AgentGlass },
        };
    }

    #endregion

    #region Events

    private void AttachEvents()
    {
        NUD_FC_Current.ValueChanged += (_, _) => RefreshCollected();
        NUD_FC_Used.ValueChanged += (_, _) => RefreshCollected();
        NUD_Rank.ValueChanged += (_, _) => ChangeRank();
        for (int i = 0; i < 4; i++)
        {
            var index = i;
            MyMessages[i].ValueChanged += (s, _) =>
            {
                if (!editing)
                    SAV.Festa.SetFestaMessage(index, (ushort)(((NumericUpDown)s!).Value ?? 0));
            };
        }

        B_AllPhrases.Click += (_, _) => CLB_Phrases.SetAllChecked(true);
        B_AllReceiveReward.Click += (_, _) => CLB_Reward.SetAllCheckState(true);
        B_AllReadyReward.Click += (_, _) => CLB_Reward.SetAllCheckState(null);

        LB_FacilityIndex.SelectionChanged += (_, _) => ChangeFacilityIndex();
        CB_FacilityType.SelectionChanged += (_, _) => ChangeFacilityType();
        NUD_FacilityColor.ValueChanged += (_, _) => ChangeFacilityColor();
        CHK_FacilityIntroduced.IsCheckedChanged += (_, _) =>
        {
            if (!editing && entry >= 0)
                f[entry].IsIntroduced = CHK_FacilityIntroduced.IsChecked == true;
        };
        TB_OTName.TextChanged += (_, _) =>
        {
            if (!editing && entry >= 0)
                f[entry].OriginalTrainerName = TB_OTName.Text ?? string.Empty;
        };
        TB_OTName.AttachClick(async mods =>
        {
            if (mods == KeyModifiers.Control)
                await TrashEditorWindow.ShowAsync(this, TB_OTName, SAV, []);
        });
        Label_OTGender.AttachClick(_ => ClickGender());
        CB_FacilityMessage.SelectionChanged += (_, _) => ChangeFacilityMessageIndex();
        NUD_FacilityMessage.ValueChanged += (_, _) => ChangeFacilityMessage();
        TB_UsedFlags.TextChanged += (_, _) => ChangeHex(TB_UsedFlags);
        TB_UsedStats.TextChanged += (_, _) => ChangeHex(TB_UsedStats);
        TB_FacilityID.TextChanged += (_, _) => ChangeHex(TB_FacilityID);
        CB_LuckyResult.SelectionChanged += (_, _) => ChangeLucky();
        NUD_Exchangable.ValueChanged += (_, _) =>
        {
            if (!editing && entry >= 0)
                f[entry].ExchangeLeftCount = (byte)(NUD_Exchangable.Value ?? 0);
        };
        B_DelVisitor.Click += (_, _) => ClickDeleteVisitor();

        if (SAV is not SAV7USUM)
            return;
        NUD_Grade.ValueChanged += (_, _) => ChangeGrade();
        B_ImportParty.Click += async (_, _) => await ClickImportParty();
        B_AgentGlass.Click += async (_, _) => await ClickAgentGlasses();
        for (int i = 0; i < PBs.Length; i++)
        {
            var index = i;
            PBs[i].AttachClickHandled(async _ => await FileDialogs.SavePKMDialog(this, p[index]));
        }
    }

    private void RefreshCollected()
    {
        if (editing)
            return;
        L_FC_CollectedV.Text = ((NUD_FC_Current.Value ?? 0) + (NUD_FC_Used.Value ?? 0)).ToString(CultureInfo.InvariantCulture);
    }

    private void ChangeRank()
    {
        if (editing)
            return;
        var rank = (int)(NUD_Rank.Value ?? 0);
        SAV.Festa.FestaRank = (ushort)rank;
        LoadRankLabel(rank);
    }

    private void ClickGender()
    {
        if (entry < 0)
            return;
        var b = f[entry].Gender;
        b ^= 1;
        f[entry].Gender = b;
        LoadOTLabel(b);
    }

    private void ChangeFacilityIndex()
    {
        if (editing)
            return;
        SaveFacility();
        entry = LB_FacilityIndex.SelectedIndex;
        if (entry < 0)
            return;
        LoadFacility();
    }

    private void ChangeFacilityMessageIndex()
    {
        if (editing)
            return;
        var index = CB_FacilityMessage.SelectedIndex;
        if (index < 0 || entry < 0)
            return;
        editing = true;
        LoadFacilityMessage(index);
        editing = false;
    }

    private void ChangeFacilityMessage()
    {
        if (editing)
            return;
        var index = CB_FacilityMessage.SelectedIndex;
        if (index < 0 || entry < 0)
            return;
        f[entry].SetMessage(index, (ushort)(NUD_FacilityMessage.Value ?? 0));
    }

    private void ChangeHex(NumericTextBox tb)
    {
        if (editing || entry < 0)
            return;

        var t = Util.GetOnlyHex(tb.Text ?? string.Empty);
        if (string.IsNullOrWhiteSpace(t))
            t = "0";
        var maxlen = ReferenceEquals(tb, TB_FacilityID) ? 12 << 1 : 4 << 1;
        if (t.Length > maxlen)
        {
            t = t[..maxlen];
            editing = true;
            tb.Text = t;
            editing = false;
        }

        if (ReferenceEquals(tb, TB_UsedFlags))
            f[entry].UsedFlags = Util.GetHexValue(t);
        else if (ReferenceEquals(tb, TB_UsedStats))
            f[entry].UsedRandStat = Util.GetHexValue(t);
        else if (ReferenceEquals(tb, TB_FacilityID))
        {
            var dest = f[entry].TrainerFesID;
            dest.Clear();
            Util.GetBytesFromHexString(t, dest);
        }
    }

    private void ChangeLucky()
    {
        if (editing || entry < 0)
            return;
        var lucky = CB_LuckyResult.SelectedIndex;
        if (lucky-- < 0)
            return;
        // both 0 if "none"
        f[entry].UsedLuckyRank = lucky < 0 ? 0 : (lucky % 3) + 1;
        f[entry].UsedLuckyPlace = lucky < 0 ? 0 : (lucky / 3) + 1;
    }

    private void ClickDeleteVisitor()
    {
        if (entry < 0)
            return;
        var facility = f[entry];
        // there is an unknown value when not introduced...no reproducibility, just mistake?
        if (facility.IsIntroduced)
            facility.ClearTrainerFesID();
        facility.IsIntroduced = false;
        facility.OriginalTrainerName = string.Empty;
        facility.Gender = 0;
        for (int i = 0; i < 4; i++)
            facility.SetMessage(i, 0);
        LoadFacility();
    }

    #endregion

    #region Facility

    private int TypeIndexToType(int typeIndex)
    {
        if ((uint)typeIndex > typeMAX + 1)
            return -1;
        return typeIndex switch
        {
            < 0x0F => 0,
            < 0x1E => 1,
            < 0x2F => 2,
            < 0x41 => 3,
            < 0x50 => 4,
            < 0x65 => 5,
            < 0x7D => 6,
            _ => 7,
        };
    }

    private int GetColorCount(int type)
    {
        var colors = RES_FacilityColor;
        if (type >= 0 && type < colors.Length - (SAV is SAV7USUM ? 0 : 1))
            return colors[type].Length - 1;
        return 3;
    }

    private void LoadFacility()
    {
        editing = true;
        var facility = f[entry];
        CB_FacilityType.SelectedIndex = CB_FacilityType.Items.Count > facility.Type ? facility.Type : -1;
        var type = TypeIndexToType(CB_FacilityType.SelectedIndex);
        NUD_FacilityColor.Maximum = GetColorCount(type);
        NUD_FacilityColor.SetValueClamped(facility.Color);
        if (type >= 0)
            LoadColorLabel(type);
        CB_LuckyResult.IsEnabled = CB_LuckyResult.IsVisible = L_LuckyResult.IsVisible = type == 5;
        NUD_Exchangable.IsEnabled = NUD_Exchangable.IsVisible = L_Exchangable.IsVisible = type == 7;
        switch (type)
        {
            case 5:
                var lucky = (facility.UsedLuckyPlace * 3) + facility.UsedLuckyRank - 3;
                if ((uint)lucky >= CB_LuckyResult.Items.Count)
                    lucky = 0;
                CB_LuckyResult.SelectedIndex = lucky;
                break;
            case 7:
                NUD_Exchangable.SetValueClamped(facility.ExchangeLeftCount);
                break;
        }
        CB_FacilityNPC.SelectedIndex = CB_FacilityNPC.Items.Count > facility.NPC ? facility.NPC : 0;
        CHK_FacilityIntroduced.IsChecked = facility.IsIntroduced;
        TB_OTName.Text = facility.OriginalTrainerName;
        LoadOTLabel(facility.Gender);
        if (CB_FacilityMessage.SelectedIndex >= 0)
            LoadFacilityMessage(CB_FacilityMessage.SelectedIndex);

        TB_UsedFlags.Text = facility.UsedFlags.ToString("X8");
        TB_UsedStats.Text = facility.UsedRandStat.ToString("X8");
        TB_FacilityID.Text = Util.GetHexStringFromBytes(facility.TrainerFesID);
        editing = false;
    }

    private void SaveFacility()
    {
        if (entry < 0)
            return;
        var facility = f[entry];
        if (CB_FacilityType.SelectedIndex >= 0)
            facility.Type = CB_FacilityType.SelectedIndex;
        facility.Color = (byte)(NUD_FacilityColor.Value ?? 0);
        facility.OriginalTrainerName = TB_OTName.Text ?? string.Empty;
        if (CB_FacilityNPC.SelectedIndex >= 0)
            facility.NPC = CB_FacilityNPC.SelectedIndex;
        facility.IsIntroduced = CHK_FacilityIntroduced.IsChecked == true;
        var type = TypeIndexToType(facility.Type);
        facility.ExchangeLeftCount = type == 7 ? (byte)(NUD_Exchangable.Value ?? 0) : 0;
        var lucky = CB_LuckyResult.SelectedIndex - 1;
        var writeLucky = type == 5 && lucky >= 0;
        facility.UsedLuckyRank = writeLucky ? (lucky % 3) + 1 : 0;
        facility.UsedLuckyPlace = writeLucky ? (lucky / 3) + 1 : 0;
    }

    private void ChangeFacilityColor()
    {
        if (editing || entry < 0)
            return;
        f[entry].Color = (byte)(NUD_FacilityColor.Value ?? 0);
        var type = TypeIndexToType(CB_FacilityType.SelectedIndex);
        if (type < 0)
            return;
        editing = true;
        LoadColorLabel(type);
        editing = false;
    }

    private void ChangeFacilityType()
    {
        if (editing || entry < 0)
            return;
        var typeIndex = CB_FacilityType.SelectedIndex;
        if (typeIndex < 0)
            return;

        var facility = f[entry];
        facility.Type = typeIndex;
        // reset color
        var type = TypeIndexToType(typeIndex);
        var colorCount = GetColorCount(type);
        editing = true;
        if (colorCount < NUD_FacilityColor.Value)
        {
            NUD_FacilityColor.Value = colorCount;
            facility.Color = colorCount;
        }
        NUD_FacilityColor.Maximum = colorCount;
        LoadColorLabel(type);
        // reset forms
        CB_LuckyResult.IsEnabled = CB_LuckyResult.IsVisible = L_LuckyResult.IsVisible = type == 5;
        NUD_Exchangable.IsEnabled = NUD_Exchangable.IsVisible = L_Exchangable.IsVisible = type == 7;
        switch (type)
        {
            case 5:
                var lucky = (facility.UsedLuckyPlace * 3) + facility.UsedLuckyRank - 3;
                if (lucky < 0 || lucky >= CB_LuckyResult.Items.Count)
                    lucky = 0;
                CB_LuckyResult.SelectedIndex = lucky;
                break;
            case 7:
                NUD_Exchangable.SetValueClamped(facility.ExchangeLeftCount);
                break;
        }
        editing = false;
    }

    private void LoadFacilityMessage(int index) => NUD_FacilityMessage.SetValueClamped(f[entry].GetMessage(index));

    private void LoadColorLabel(int type) => L_FacilityColorV.Text = RES_Color[RES_FacilityColor[type][(int)(NUD_FacilityColor.Value ?? 0)]];

    private void LoadOTLabel(int b)
    {
        Label_OTGender.Text = GenderSymbols[b & 1];
        Label_OTGender.Foreground = b == 1 ? Brushes.Red : Brushes.Blue;
    }

    private void LoadRankLabel(int rank) => L_RankFC.Text = GetRankText(rank);

    private static string GetRankText(int rank)
    {
        if (rank < 1) return string.Empty;
        if (rank == 1) return "0 - 5";
        if (rank == 2) return "6 - 15";
        if (rank == 3) return "16 - 30";
        if (rank <= 10)
        {
            int i = ((rank - 1) * (rank - 2) * 5) + 1;
            return $"{i} - {i + ((rank - 1) * 10) - 1}";
        }
        if (rank <= 20)
        {
            int i = (rank * 100) - 649;
            return $"{i} - {i + 99}";
        }
        if (rank <= 70)
        {
            int j = (rank - 1) / 10;
            int i = (rank * ((j * 30) + 60)) - ((j * j * 150) + (j * 180) + 109); // 30 * (rank - 5 * j + 4) * (j + 2) - 349;
            return $"{i} - {i + (j * 30) + 59}";
        }
        if (rank <= 100)
        {
            int i = (rank * 270) - 8719;
            return $"{i} - {i + 269}";
        }
        if (rank <= 998)
        {
            int i = (rank * 300) - 11749;
            return $"{i} - {i + 299}";
        }
        if (rank == 999)
            return "287951 - ";
        return string.Empty;
    }

    #endregion

    #region Battle Agency

    private const ushort InvitedValue = 0x7DFF;

    private ushort GetSavData16(int offset) => ReadUInt16LittleEndian(SAV.Data[offset..]);
    private bool IsTrainerInvited() => (GetSavData16(0x6C3EE) & InvitedValue) == InvitedValue && (GetSavData16(0x6C526) & InvitedValue) == InvitedValue;

    private void LoadBattleAgency()
    {
        p[0] = SAV.GetStoredSlot(SAV.Data[0x6C200..]);
        p[1] = SAV.GetPartySlot(SAV.Data[0x6C2E8..]);
        p[2] = SAV.GetPartySlot(SAV.Data[0x6C420..]);
        LoadSprites();
        B_ImportParty.IsVisible = SAV.HasParty;
        CHK_Choosed.IsChecked = SAV.GetFlag(0x6C55E, 1);
        CHK_TrainerInvited.IsChecked = IsTrainerInvited();
        var valus = ReadUInt16LittleEndian(SAV.Data[0x6C55C..]);
        var grade = (valus >> 6) & 0x3F;
        NUD_Grade.SetValueClamped(grade);
        var max = (Math.Min(49, grade) / 10 * 3) + 2;
        var defeated = valus >> 12;
        NUD_Defeated.Maximum = max;
        NUD_Defeated.SetValueClamped(defeated);
        NUD_DefeatMon.SetValueClamped(ReadUInt16LittleEndian(SAV.Data[0x6C558..]));
        for (int i = 0; i < NUD_Trainers.Length; i++)
            NUD_Trainers[i].SetValueClamped(GetSavData16(0x6C56C + (0x14 * i)));
        B_AgentGlass.IsEnabled = (SAV.Fashion.Data[0xD0] & 1) == 0;
    }

    private void LoadSprites()
    {
        for (int i = 0; i < PBs.Length; i++)
            PBs[i].Sprite = p[i].Sprite(SAV, visibility: SlotVisibilityType.CheckLegalityIndicate).ToAvaloniaBitmapAndDispose();
    }

    private void SaveBattleAgency()
    {
        SAV.SetFlag(0x6C55E, 1, CHK_Choosed.IsChecked == true);
        var invited = CHK_TrainerInvited.IsChecked == true;
        if (IsTrainerInvited() != invited)
        {
            WriteUInt16LittleEndian(SAV.Data[0x6C3EE..], (ushort)(invited ? GetSavData16(0x6C3EE) | InvitedValue : 0));
            WriteUInt16LittleEndian(SAV.Data[0x6C526..], (ushort)(invited ? GetSavData16(0x6C526) | InvitedValue : 0));
        }
        p[0].WriteEncryptedDataStored(SAV.Data[0x6C200..]); // BattleFesSave
        p[1].WriteEncryptedDataParty(SAV.Data[0x6C2E8..]);
        p[2].WriteEncryptedDataParty(SAV.Data[0x6C420..]);

        var gradeDefeated = ((((int)(NUD_Defeated.Value ?? 0) & 0xF) << 12) | (((int)(NUD_Grade.Value ?? 0) & 0x3F) << 6) | (SAV.Data[0x6C55C] & 0x3F));
        WriteUInt16LittleEndian(SAV.Data[0x6C558..], (ushort)(NUD_DefeatMon.Value ?? 0));
        WriteUInt16LittleEndian(SAV.Data[0x6C55C..], (ushort)gradeDefeated);
        for (int i = 0; i < NUD_Trainers.Length; i++)
            WriteUInt16LittleEndian(SAV.Data[(0x6C56C + (0x14 * i))..], (ushort)(NUD_Trainers[i].Value ?? 0));
        SAV.Festa.FestivalPlazaName = TB_PlazaName.Text ?? string.Empty;
    }

    private void ChangeGrade()
    {
        if (editing)
            return;
        var max = (Math.Min(49, (int)(NUD_Grade.Value ?? 0)) / 10 * 3) + 2;
        editing = true;
        if (NUD_Defeated.Value > max)
            NUD_Defeated.Value = max;
        NUD_Defeated.Maximum = max;
        editing = false;
    }

    private string GetSpeciesNameFromPKM(PKM pk) => SpeciesName.GetSpeciesNameGeneration(pk.Species, SAV.Language, 7);

    private async Task ClickImportParty()
    {
        if (!SAV.HasParty)
            return;
        var party = SAV.PartyData;
        var msg = string.Empty;
        for (int i = 0; i < 3; i++)
        {
            if (i < party.Count)
                msg += $"{Environment.NewLine}{GetSpeciesNameFromPKM(p[i])} -> {GetSpeciesNameFromPKM(party[i])}";
            else
                msg += $"{Environment.NewLine}not replaced: {GetSpeciesNameFromPKM(p[i])}";
        }
        if (DialogResult.Yes != await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, "Replace PKM?", msg))
            return;

        for (int i = 0, min = Math.Min(3, party.Count); i < min; i++)
            p[i] = party[i];
        LoadSprites();
    }

    private async Task ClickAgentGlasses()
    {
        if (NUD_Grade.Value < 30 && DialogResult.Yes != await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, "Agent Sunglasses is reward of Grade 30.", "Continue?"))
            return;
        SAV.Fashion.GiveAgentSunglasses();
        B_AgentGlass.IsEnabled = false;
    }

    #endregion

    protected override void OnSave()
    {
        SAV.Festa.SetFestaPhraseUnlocked(106, CLB_Phrases.GetItemChecked(0));
        for (int i = 1; i < CLB_Phrases.Count; i++)
            SAV.Festa.SetFestaPhraseUnlocked(i - 1, CLB_Phrases.GetItemChecked(i));

        SAV.SetRecord(038, (int)(NUD_FC_Used.Value ?? 0));
        SAV.Festa.FestaCoins = (int)(NUD_FC_Current.Value ?? 0);
        var date = CAL_FestaStartDate.SelectedDate?.Date ?? new DateTime(2000, 1, 1);
        var time = CAL_FestaStartTime.SelectedTime ?? TimeSpan.Zero;
        SAV.Festa.FestaDate = new DateTime(date.Year, date.Month, date.Day, time.Hours, time.Minutes, time.Seconds);

        SAV.Festa.SetFestaPrizeReceived(10, RewardState[ToIndex(CLB_Reward.GetItemCheckState(0))]);
        for (int i = 1; i < CLB_Reward.Count; i++)
            SAV.Festa.SetFestaPrizeReceived(i - 1, RewardState[ToIndex(CLB_Reward.GetItemCheckState(i))]);

        SaveFacility();
        if (SAV is SAV7USUM)
            SaveBattleAgency();

        Origin.CopyChangesFrom(SAV);
        Close();
    }

    /// <summary>WinForms <c>CheckState</c> ordering: Unchecked, Checked, Indeterminate.</summary>
    private static int ToIndex(bool? state) => state switch { false => 0, true => 1, _ => 2 };

    #region Phrases

    private static readonly string[] PhrasesJapanese = 
    [
    "おじさんの きんのたま だからね！","かがくの ちからって すげー","1 2の …… ポカン！","おーす！ みらいの チャンピオン！","おお！ あんたか！","みんな げんきに なりましたよ！","とっても 幸せそう！","なんでも ないです","いあいぎりで きりますか？","レポートを かきこんでいます",
    "…… ぼくも もう いかなきゃ！","ボンジュール！","バイビー！","ばか はずれです……","やけどなおしの よういは いいか！","ウー！ ハーッ！","ポケモンは たたかわせるものさ","ヤドランは そっぽを むいた！","マサラは まっしろ はじまりのいろ","10000こうねん はやいんだよ！","おーい！ まてー！ まつんじゃあ！","こんちわ！ ぼく ポケモン……！","っだと こらあ！","ぐ ぐーッ！ そんな ばかなーッ！","みゅう！","タチサレ…… タチサレ……",
    "カイリュー はかいこうせん","どっちか 遊んでくれないか？","ぬいぐるみ かっておいたわよ","ひとのこと じろじろ みてんなよ","なんのことだか わかんない","みんな ポケモン やってるやん","きょうから 24時間 とっくんだ！","あたいが ホンモノ！","でんげきで いちころ……","スイクンを おいかけて 10ねん","かんどうが よみがえるよ！","われわれ ついに やりましたよー！","ヤドンのシッポを うるなんて……","ショオーッ!!","ギャーアアス!!","だいいっぽを ふみだした！",
    "いちばん つよくて すごいんだよね","にくらしいほど エレガント！","そうぞうりょくが たりないよ","キミは ビッグウェーブ！","おまえさんには しびれた わい","なに いってんだろ…… てへへ……","ぬいぐるみ なんか かってないよ","ここで ゆっくり して おいき！","はじけろ！ ポケモン トレーナー！","はいが はいに はいった……","…できる！","ぶつかった かいすう 5かい！","たすけて おくれーっ!!","マボロシじま みえんのう……","ひゅああーん！","しゅわーん！",
    "あつい きもち つたわってくる！","こいつが！ おれの きりふだ！","ひとりじめとか そういうの ダメよ！","ワーオ！ ぶんせきどーり！","ぱるぱるぅ!!!","グギュグバァッ!!!","ばっきん 100まんえん な！","オレ つよくなる……","ながれる 時間は とめられない！","ぜったいに お願いだからね","きみたちから はどうを かんじる！","あたしのポケモンに なにすんのさ！","リングは おれの うみ～♪","オレの おおごえの ひとりごとを","そう コードネームは ハンサム！","……わたしが まけるかも だと!?",
    "やめたげてよぉ！","ブラボー！ スーパー ブラボー！","ボクは チャンピオンを こえる","オレは いまから いかるぜッ！","ライモンで ポケモン つよいもん","キミ むしポケモン つかいなよ","ストップ！","ひとよんで メダルおやじ！","トレーナーさんも がんばれよ！","おもうぞんぶん きそおーぜ！","プラズマズイ！","ワタクシを とめることは できない！","けいさんずみ ですとも！","ババリバリッシュ！","ンバーニンガガッ！","ヒュラララ！",
    "お友達に なっちゃお♪","じゃあ みんな またねえ！","このひとたち ムチャクチャです……","トレーナーとは なにか しりたい","スマートに くずれおちるぜ","いのち ばくはつッ!!","いいんじゃない いいんじゃないの！","あれだよ あれ おみごとだよ！","ぜんりょくでいけー！ ってことよ！","おまちなさいな！","つまり グッド ポイント なわけ！","ざんねん ですが さようなら","にくすぎて むしろ 好きよ","この しれもの が！","イクシャア!!","イガレッカ!!",
    "フェスサークル ランク 100！",
    ];

    private const string musical8note = "\u266a";
    private const string linedP = "\u20bd"; //currency Ruble

    private static readonly string[] PhrasesDefault = 
    [ //source:UltraMoon
    /* (SM)Pokémon House */"There's nothing funny about Nuggets.","The Power of science is awesome.","1, 2, and... Ta-da!","How's the future Champ today?","Why, you!","There! All happy and healthy!","Your Pokémon seems to be very happy!","No thanks!","Would you like to use Cut?","Saving...",
    /* (SM)Kanto Tent */"Well, I better get going!","Bonjour!","Smell ya later!","Sorry! Bad call!","You better have Burn Heal!","Hoo hah!","Pokémon are for battling!","Slowbro took a snooze...","Shades of your journey await!","You're 10,000 light-years from facing Brock!","Hey! Wait! Don't go out!","Hiya! I'm a Pokémon...","What do you want?","WHAT! This can't be!","Mew!","Be gone... Intruders...",
    /* (SM)Joht Tent */"Dragonite, Hymer Beam.","Spread the fun around.","I bought an adorable doll with your money.","What are you staring at?","I just don't understand.","Everyone is into Pokémon.","I'm going to train 24 hours a day!","I'm the real deal!","With a jolt of electricity...","For 10 years I chased Suicune.","I am just so deeply moved!","We have finally made it!","...But selling Slowpoke Tails?","Shaoooh!","Gyaaas!","you've taken your first step!",
    /* (SM)Hoenn Tent */"I'm just the strongest there is right now.","And confoundedly elegant!","You guys need some imagination.","You made a much bigger splash!","You ended up giving me a thrill!","So what am I talking about...","I'm not buying any Dolls.","Take your time and rest up!","Have a blast, Pokémon Trainers!","I got ashes in my eyelashes!","You're sharp!","Number of collisions: 5 times!","Please! Help me out!","I can't see Mirage Island today...","Hyahhn!","Shwahhn!",
    /* (SM)Sinnoh Tent */"Your will is overwhelming me!","This is it! My trump card!","Trying to monopolize Pokémon just isn't...","See? Just as analyzed.","Gagyagyaah!","Gugyugubah!","It's a "+linedP+"10 million fine if you're late!","I'm going to get tougher...","You'll never be able to stem the flow of time!","Please come!","Your team! I sense your strong aura!","What do you think you're doing?!","The ring is my rolling sea. "+musical8note,"I was just thinking out loud.","My code name, it is Looker.","It's not possible that I lose!",
    /* (SM)Unova Tent */"Knock it off!","Bravo! Excellent!!","I'll defeat the Champion.","You're about to feel my rage!","Nimbasa's Pokémon can dance a nimble bossa!","Use Bug-type Pokémon!","Stop!","People call me Mr. Medal!","Trainer, do your best, too!","See who's stronger!","Plasbad, for short!","I won't allow anyone to stop me!","I was expecting exactly that kind of move!","Bazzazzazzash!","Preeeeaah!","Haaahraaan!",
    /* (SM)Kalos Tent */"We'll become friends. "+musical8note,"I'll see you all later!","These people have a few screws loose...","I want to know what a \"Trainer\" is.","When I lose, I go out in style!","Let's give it all we've got!","Fantastic! Just fantastic!","Outstanding!","Try as hard as possible!","Stop right there!","That really hit me right here...","But this is adieu to you all.","You're just too much, you know?","Fool! You silly, unseeing child!","Xsaaaaaah!","Yvaaaaaar!",
    "I reached Festival Plaza Rank 100!",
    ];

    #endregion
}
