using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Localization;

namespace PKHeX.Avalonia.Controls;

public sealed partial class PKMEditorView
{
    // Tabs
    private readonly TabControl TC_Editor = new() { Name = "TC_Editor", TabStripPlacement = Dock.Left };
    private readonly TabItem Tab_Main = new() { Name = "Tab_Main", Header = "Main" };
    private readonly TabItem Tab_Met = new() { Name = "Tab_Met", Header = "Met" };
    private readonly TabItem Tab_Stats = new() { Name = "Tab_Stats", Header = "Stats" };
    private readonly TabItem Tab_Moves = new() { Name = "Tab_Moves", Header = "Moves" };
    private readonly TabItem Tab_Cosmetic = new() { Name = "Tab_Cosmetic", Header = "Cosmetic" };
    private readonly TabItem Tab_OTMisc = new() { Name = "Tab_OTMisc", Header = "OT/Misc" };

    // Main tab
    private readonly TextBlock Label_PID = UiFactory.Label("Label_PID", "PID:");
    private readonly Button BTN_Shinytize = UiFactory.Button("BTN_Shinytize", "☆");
    private readonly Image PB_ShinyStar = UiFactory.Picture("PB_ShinyStar");
    private readonly Image PB_ShinySquare = UiFactory.Picture("PB_ShinySquare");
    private readonly NumericTextBox TB_PID = UiFactory.Numeric("TB_PID", 8, 80, hex: true);
    private readonly GenderToggleView UC_Gender = new() { Name = "UC_Gender" };
    private readonly Button BTN_RerollPID = UiFactory.Button("BTN_RerollPID", "Reroll");
    private readonly TextBlock Label_Species = UiFactory.Label("Label_Species", "Species:", true);
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 160);
    private readonly TextBlock CHK_Nicknamed = UiFactory.Label("CHK_Nicknamed", "Nickname:", true);
    private readonly CheckBox CHK_NicknamedFlag = UiFactory.Check("CHK_NicknamedFlag", string.Empty);
    private readonly TextBox TB_Nickname = UiFactory.Text("TB_Nickname", 12, 130);
    private readonly Button BTN_NicknameWarn = UiFactory.Button("BTN_NicknameWarn", "?");
    private readonly TextBlock Label_EXP = UiFactory.Label("Label_EXP", "EXP:");
    private readonly NumericTextBox TB_EXP = UiFactory.Numeric("TB_EXP", 7, 72);
    private readonly TextBlock Label_CurLevel = UiFactory.Label("Label_CurLevel", "Level:", true);
    private readonly NumericTextBox TB_Level = UiFactory.Numeric("TB_Level", 3, 44);
    private readonly ExperienceBarView ExperienceBar = new() { Name = "ExperienceBar" };
    private readonly TextBlock Label_Nature = UiFactory.Label("Label_Nature", "Nature:", true);
    private readonly ComboBox CB_Nature = UiFactory.Combo("CB_Nature");
    private readonly TextBlock L_StatAlignment = UiFactory.Label("L_StatAlignment", "Stat Alignment:", true);
    private readonly ComboBox CB_StatAlignment = UiFactory.Combo("CB_StatAlignment");
    private readonly TextBlock Label_Form = UiFactory.Label("Label_Form", "Form:");
    private readonly ComboBox CB_Form = UiFactory.StringCombo("CB_Form", 140);
    private readonly TextBlock L_FormArgument = UiFactory.Label("L_FormArgument", "Form Argument:");
    private readonly FormArgumentEditorView FA_Form = new() { Name = "FA_Form" };
    private readonly TextBlock Label_HeldItem = UiFactory.Label("Label_HeldItem", "Held Item:");
    private readonly ComboBox CB_HeldItem = UiFactory.Combo("CB_HeldItem");
    private readonly TextBlock Label_Ability = UiFactory.Label("Label_Ability", "Ability:");
    private readonly ComboBox CB_Ability = UiFactory.Combo("CB_Ability");
    private readonly ComboBox DEV_Ability = UiFactory.Combo("DEV_Ability");
    private readonly NumericTextBox TB_AbilityNumber = UiFactory.Numeric("TB_AbilityNumber", 1, 30);
    private StackPanel FLP_AbilityRight = null!;
    private readonly TextBlock Label_Language = UiFactory.Label("Label_Language", "Language:");
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 110);
    private readonly CheckBox CHK_IsEgg = UiFactory.Check("CHK_IsEgg", "Is Egg");
    private StackPanel FLP_EggPKRSRight = null!;
    private readonly CheckBox CHK_Infected = UiFactory.Check("CHK_Infected", "Infected");
    private readonly CheckBox CHK_Cured = UiFactory.Check("CHK_Cured", "Cured");
    private readonly TextBlock Label_PKRS = UiFactory.Label("Label_PKRS", "PkRs:");
    private StackPanel FLP_PKRSRight = null!;
    private readonly ComboBox CB_PKRSStrain = UiFactory.StringCombo("CB_PKRSStrain", 50, "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15");
    private readonly TextBlock Label_PKRSdays = UiFactory.Label("Label_PKRSdays", "d:");
    private readonly ComboBox CB_PKRSDays = UiFactory.StringCombo("CB_PKRSDays", 50);
    private readonly TextBlock L_NSparkle = UiFactory.Label("L_NSparkle", "N's Sparkle:");
    private readonly CheckBox CHK_NSparkle = UiFactory.Check("CHK_NSparkle", "Active");
    private readonly TextBlock L_ShadowID = UiFactory.Label("L_ShadowID", "Shadow ID:");
    private readonly NumericUpDown NUD_ShadowID = UiFactory.NumericUpDown("NUD_ShadowID", 0, 127);
    private readonly TextBlock L_HeartGauge = UiFactory.Label("L_HeartGauge", "Heart Gauge:");
    private StackPanel FLP_Purification = null!;
    private readonly NumericUpDown NUD_Purification = UiFactory.NumericUpDown("NUD_Purification", -100, int.MaxValue, 110);
    private readonly CheckBox CHK_Shadow = UiFactory.Check("CHK_Shadow", "Shadow");
    private readonly TextBlock L_CatchRate = UiFactory.Label("L_CatchRate", "Catch Rate:");
    private readonly CatchRateView CR_PK1 = new() { Name = "CR_PK1" };

    // Met tab
    private StackPanel FLP_OriginGame = null!, FLP_BattleVersion = null!, FLP_MetLocation = null!, FLP_Ball = null!, FLP_MetDate = null!, FLP_MetLevel = null!, FLP_ObedienceLevel = null!, FLP_GroundTile = null!, FLP_TimeOfDay = null!;
    private readonly TextBlock Label_OriginGame = UiFactory.Label("Label_OriginGame", "Origin Game:");
    private readonly ComboBox CB_GameOrigin = UiFactory.Combo("CB_GameOrigin");
    private readonly TextBlock L_BattleVersion = UiFactory.Label("L_BattleVersion", "Battle Version:");
    private readonly ComboBox CB_BattleVersion = UiFactory.Combo("CB_BattleVersion");
    private readonly TextBlock Label_MetLocation = UiFactory.Label("Label_MetLocation", "Met Location:", true);
    private readonly ComboBox CB_MetLocation = UiFactory.Combo("CB_MetLocation", 200);
    private readonly TextBlock Label_Ball = UiFactory.Label("Label_Ball", "Ball:", true);
    private readonly Image PB_Ball = UiFactory.Picture("PB_Ball", 20);
    private readonly ComboBox CB_Ball = UiFactory.Combo("CB_Ball");
    private readonly TextBlock Label_MetDate = UiFactory.Label("Label_MetDate", "Met Date:");
    private readonly DatePicker CAL_MetDate = new() { Name = "CAL_MetDate", MinWidth = 0, Width = 230 };
    private readonly TextBlock Label_MetLevel = UiFactory.Label("Label_MetLevel", "Met Level:");
    private readonly NumericTextBox TB_MetLevel = UiFactory.Numeric("TB_MetLevel", 3, 44);
    private readonly CheckBox CHK_Fateful = UiFactory.Check("CHK_Fateful", "Fateful Encounter");
    private readonly TextBlock L_ObedienceLevel = UiFactory.Label("L_ObedienceLevel", "Obedience:", true);
    private readonly NumericTextBox TB_ObedienceLevel = UiFactory.Numeric("TB_ObedienceLevel", 3, 44);
    private readonly TextBlock Label_GroundTile = UiFactory.Label("Label_GroundTile", "Encounter:");
    private readonly ComboBox CB_GroundTile = UiFactory.Combo("CB_GroundTile");
    private readonly TextBlock L_MetTimeOfDay = UiFactory.Label("L_MetTimeOfDay", "Time of Day:");
    private readonly ComboBox CB_MetTimeOfDay = UiFactory.StringCombo("CB_MetTimeOfDay", 100, "(None)", "Morning", "Day", "Night");
    private readonly CheckBox CHK_AsEgg = UiFactory.Check("CHK_AsEgg", "As Egg");
    private Border GB_EggConditions = null!;
    private readonly TextBlock GB_EggConditionsHeader = UiFactory.Header("GB_EggConditions", "Egg Met Conditions");
    private readonly TextBlock Label_EggLocation = UiFactory.Label("Label_EggLocation", "Location:");
    private readonly ComboBox CB_EggLocation = UiFactory.Combo("CB_EggLocation", 200);
    private readonly TextBlock Label_EggDate = UiFactory.Label("Label_EggDate", "Date:");
    private readonly DatePicker CAL_EggDate = new() { Name = "CAL_EggDate", MinWidth = 0, Width = 230 };

    // Stats tab
    private readonly StatEditorView Stats = new() { Name = "Stats" };

    // Moves tab
    private readonly TextBlock GB_CurrentMoves = UiFactory.Header("GB_CurrentMoves", "Current Moves");
    private readonly TextBlock Label_CurPP = UiFactory.Label("Label_CurPP", "PP", true);
    private readonly TextBlock Label_PPups = UiFactory.Label("Label_PPups", "Ups", true);
    private readonly MoveChoiceView MC_Move1 = new() { Name = "MC_Move1" };
    private readonly MoveChoiceView MC_Move2 = new() { Name = "MC_Move2" };
    private readonly MoveChoiceView MC_Move3 = new() { Name = "MC_Move3" };
    private readonly MoveChoiceView MC_Move4 = new() { Name = "MC_Move4" };
    private readonly TextBlock GB_RelearnMoves = UiFactory.Header("GB_RelearnMoves", "Relearn Moves");
    private readonly Image PB_WarnRelearn1 = UiFactory.Picture("PB_WarnRelearn1");
    private readonly Image PB_WarnRelearn2 = UiFactory.Picture("PB_WarnRelearn2");
    private readonly Image PB_WarnRelearn3 = UiFactory.Picture("PB_WarnRelearn3");
    private readonly Image PB_WarnRelearn4 = UiFactory.Picture("PB_WarnRelearn4");
    private readonly ComboBox CB_RelearnMove1 = UiFactory.Combo("CB_RelearnMove1", 150);
    private readonly ComboBox CB_RelearnMove2 = UiFactory.Combo("CB_RelearnMove2", 150);
    private readonly ComboBox CB_RelearnMove3 = UiFactory.Combo("CB_RelearnMove3", 150);
    private readonly ComboBox CB_RelearnMove4 = UiFactory.Combo("CB_RelearnMove4", 150);
    private StackPanel FLP_Relearn1 = null!, FLP_Relearn2 = null!, FLP_Relearn3 = null!, FLP_Relearn4 = null!;
    private StackPanel FLP_MoveFlags = null!;
    private readonly Button B_RelearnFlags = UiFactory.Button("B_RelearnFlags", "Relearn Flags");
    private readonly Button B_MoveShop = UiFactory.Button("B_MoveShop", "Move Shop");
    private readonly Button B_PlusRecord = UiFactory.Button("B_PlusRecord", "Plus Flags");
    private StackPanel FLP_AlphaMove = null!;
    private readonly TextBlock L_AlphaMastered = UiFactory.Label("L_AlphaMastered", "Alpha Mastered:");
    private readonly ComboBox CB_AlphaMastered = UiFactory.Combo("CB_AlphaMastered", 150);

    // Cosmetic tab
    private Border GB_Markings = null!;
    private readonly TextBlock GB_MarkingsHeader = UiFactory.Header("GB_Markings", "Markings");
    private readonly Image PB_Mark1 = UiFactory.Picture("PB_Mark1"), PB_Mark2 = UiFactory.Picture("PB_Mark2"), PB_Mark3 = UiFactory.Picture("PB_Mark3");
    private readonly Image PB_Mark4 = UiFactory.Picture("PB_Mark4"), PB_Mark5 = UiFactory.Picture("PB_Mark5"), PB_Mark6 = UiFactory.Picture("PB_Mark6");
    private readonly Image PB_MarkShiny = UiFactory.Picture("PB_MarkShiny"), PB_MarkCured = UiFactory.Picture("PB_MarkCured");
    private readonly Image PB_Affixed = UiFactory.Picture("PB_Affixed", 24);
    private readonly Image PB_Favorite = UiFactory.Picture("PB_Favorite", 24), PB_Origin = UiFactory.Picture("PB_Origin", 24), PB_BattleVersion = UiFactory.Picture("PB_BattleVersion", 24);
    private StackPanel FLP_Spirit7b = null!, FLP_Mood7b = null!, FLP_WalkingMood = null!, FLP_PokeStarFame = null!;
    private readonly TextBlock L_Spirit7b = UiFactory.Label("L_Spirit7b", "Spirit:");
    private readonly NumericUpDown NUD_Spirit7b = UiFactory.NumericUpDown("NUD_Spirit7b", 0, 255);
    private readonly TextBlock L_Mood7b = UiFactory.Label("L_Mood7b", "Mood:");
    private readonly NumericUpDown NUD_Mood7b = UiFactory.NumericUpDown("NUD_Mood7b", 0, 255);
    private readonly TextBlock L_WalkingMood = UiFactory.Label("L_WalkingMood", "Walking Mood:");
    private readonly NumericUpDown NUD_WalkingMood = UiFactory.NumericUpDown("NUD_WalkingMood", -127, 127);
    private readonly TextBlock L_PokeStarFame = UiFactory.Label("L_PokeStarFame", "Fame:");
    private readonly NumericUpDown NUD_PokeStarFame = UiFactory.NumericUpDown("NUD_PokeStarFame", 0, 255);
    private readonly SizeCPView SizeCP = new() { Name = "SizeCP" };
    private readonly ShinyLeafView ShinyLeaf = new() { Name = "ShinyLeaf" };
    private StackPanel FLP_PKMEditors = null!;
    private readonly Button BTN_Ribbons = UiFactory.Button("BTN_Ribbons", "Ribbons");
    private readonly Button BTN_Medals = UiFactory.Button("BTN_Medals", "Medals");
    private readonly Button BTN_History = UiFactory.Button("BTN_History", "Memories");
    private readonly ContestStatView Contest = new() { Name = "Contest" };

    // OT/Misc tab
    private readonly TextBlock GB_OT = UiFactory.Header("GB_OT", "Trainer Information");
    private readonly TrainerIDView TID_Trainer = new() { Name = "TID_Trainer" };
    private readonly TextBlock Label_OT = UiFactory.Label("Label_OT", "OT:", true);
    private readonly TextBox TB_OT = UiFactory.Text("TB_OT", 12, 130);
    private readonly GenderToggleView UC_OTGender = new() { Name = "UC_OTGender" };
    private readonly Button BTN_OTNameWarn = UiFactory.Button("BTN_OTNameWarn", "?");
    private StackPanel FLP_FriendshipLeft = null!;
    private readonly TextBlock Label_Friendship = UiFactory.Label("Label_Friendship", "Friendship:", true);
    private readonly TextBlock Label_HatchCounter = UiFactory.Label("Label_HatchCounter", "Hatch Counter:", true);
    private readonly NumericTextBox TB_Friendship = UiFactory.Numeric("TB_Friendship", 3, 44);
    private readonly TextBlock Label_Country = UiFactory.Label("Label_Country", "Country:");
    private readonly ComboBox CB_Country = UiFactory.Combo("CB_Country");
    private readonly TextBlock Label_SubRegion = UiFactory.Label("Label_SubRegion", "Sub Region:");
    private readonly ComboBox CB_SubRegion = UiFactory.Combo("CB_SubRegion");
    private readonly TextBlock Label_3DSRegion = UiFactory.Label("Label_3DSRegion", "3DS Region:");
    private readonly ComboBox CB_3DSReg = UiFactory.Combo("CB_3DSReg");
    private StackPanel FLP_Handler = null!;
    private readonly TextBlock L_CurrentHandler = UiFactory.Label("L_CurrentHandler", "Current Handler:");
    private readonly ComboBox CB_Handler = UiFactory.StringCombo("CB_Handler", 60, "OT", "HT");
    private readonly TextBlock GB_nOT = UiFactory.Header("GB_nOT", "Latest (not OT) Handler");
    private readonly TextBlock Label_PrevOT = UiFactory.Label("Label_PrevOT", "OT:", true);
    private StackPanel FLP_HT = null!;
    private readonly TextBox TB_HT = UiFactory.Text("TB_HT", 12, 130);
    private readonly GenderToggleView UC_HTGender = new() { Name = "UC_HTGender" };
    private readonly TextBlock L_LanguageHT = UiFactory.Label("L_LanguageHT", "Language:");
    private readonly ComboBox CB_HTLanguage = UiFactory.Combo("CB_HTLanguage", 110);
    private readonly TextBlock L_FriendshipHT = UiFactory.Label("L_FriendshipHT", "Friendship:");
    private readonly NumericTextBox TB_FriendshipHT = UiFactory.Numeric("TB_FriendshipHT", 3, 44);
    private readonly TextBlock L_ExtraBytes = UiFactory.Label("L_ExtraBytes", "Extra Bytes:");
    private StackPanel FLP_ExtraBytes = null!;
    private readonly ComboBox CB_ExtraBytes = UiFactory.StringCombo("CB_ExtraBytes", 70);
    private readonly NumericTextBox TB_ExtraByte = UiFactory.Numeric("TB_ExtraByte", 3, 44);
    private readonly TextBlock L_HomeTracker = UiFactory.Label("L_HomeTracker", "HOME Tracker:");
    private readonly NumericTextBox TB_HomeTracker = UiFactory.Numeric("TB_HomeTracker", 16, 140, hex: true);
    private readonly TextBlock Label_EncryptionConstant = UiFactory.Label("Label_EncryptionConstant", "Encryption Constant:");
    private StackPanel FLP_EncryptionConstant = null!;
    private readonly NumericTextBox TB_EC = UiFactory.Numeric("TB_EC", 8, 80, hex: true);
    private readonly Button BTN_RerollEC = UiFactory.Button("BTN_RerollEC", "Reroll");
    private readonly TextBlock L_ArrivedDateTime = UiFactory.Label("L_ArrivedDateTime", "Acquired by current handler at...");
    private readonly DatePicker CAL_ReceivedDate = new() { Name = "CAL_ReceivedDate", MinWidth = 0, Width = 230 };
    private readonly TimePicker CAL_ReceivedTime = new() { Name = "CAL_ReceivedTime", ClockIdentifier = "24HourClock", UseSeconds = true };
    private StackPanel FLP_ReceivedDateTime = null!;

    private readonly StatusConditionView StatusView = new() { Name = "StatusView" };

    private void BuildLayout()
    {
        // ---- Main ----
        var main = UiFactory.FormGrid(18);
        var pidLeft = UiFactory.Row(PB_ShinySquare, PB_ShinyStar, BTN_Shinytize, Label_PID);
        UiFactory.AddFormRow(main, 0, pidLeft, UiFactory.Row(TB_PID, UC_Gender, BTN_RerollPID));
        UiFactory.AddFormRow(main, 1, Label_Species, CB_Species);
        UiFactory.AddFormRow(main, 2, UiFactory.Row(CHK_NicknamedFlag, CHK_Nicknamed), UiFactory.Row(TB_Nickname, BTN_NicknameWarn));
        UiFactory.AddFormRow(main, 3, Label_EXP, UiFactory.Row(TB_EXP, Label_CurLevel, TB_Level));
        ExperienceBar.Margin = new Thickness(0, 0, 0, 2);
        UiFactory.AddFormRow(main, 4, null, ExperienceBar);
        UiFactory.AddFormRow(main, 5, Label_Nature, CB_Nature);
        UiFactory.AddFormRow(main, 6, L_StatAlignment, CB_StatAlignment);
        UiFactory.AddFormRow(main, 7, Label_Form, CB_Form);
        UiFactory.AddFormRow(main, 8, L_FormArgument, FA_Form);
        UiFactory.AddFormRow(main, 9, Label_HeldItem, CB_HeldItem);
        FLP_AbilityRight = UiFactory.Row(CB_Ability, DEV_Ability, TB_AbilityNumber);
        UiFactory.AddFormRow(main, 10, Label_Ability, FLP_AbilityRight);
        UiFactory.AddFormRow(main, 11, Label_Language, CB_Language);
        FLP_EggPKRSRight = UiFactory.Row(CHK_Infected, CHK_Cured);
        UiFactory.AddFormRow(main, 12, CHK_IsEgg, FLP_EggPKRSRight);
        FLP_PKRSRight = UiFactory.Row(CB_PKRSStrain, Label_PKRSdays, CB_PKRSDays);
        UiFactory.AddFormRow(main, 13, Label_PKRS, FLP_PKRSRight);
        UiFactory.AddFormRow(main, 14, L_NSparkle, CHK_NSparkle);
        UiFactory.AddFormRow(main, 15, L_ShadowID, NUD_ShadowID);
        FLP_Purification = UiFactory.Row(NUD_Purification, CHK_Shadow);
        UiFactory.AddFormRow(main, 16, L_HeartGauge, FLP_Purification);
        UiFactory.AddFormRow(main, 17, L_CatchRate, CR_PK1);
        // The status condition indicator sits in the main window next to the sprite in WinForms; host it at the top-right of the Main tab here.
        StatusView.HorizontalAlignment = HorizontalAlignment.Right;
        StatusView.VerticalAlignment = VerticalAlignment.Top;
        StatusView.Margin = new Thickness(0, 6, 12, 0);
        var mainHost = new Panel();
        mainHost.Children.Add(Scroll(main));
        mainHost.Children.Add(StatusView);
        Tab_Main.Content = mainHost;

        // ---- Met ----
        FLP_OriginGame = UiFactory.Row(Label_OriginGame, CB_GameOrigin);
        FLP_BattleVersion = UiFactory.Row(L_BattleVersion, CB_BattleVersion);
        FLP_MetLocation = UiFactory.Row(Label_MetLocation, CB_MetLocation);
        FLP_Ball = UiFactory.Row(Label_Ball, PB_Ball, CB_Ball);
        FLP_MetDate = UiFactory.Row(Label_MetDate, CAL_MetDate);
        FLP_MetLevel = UiFactory.Row(Label_MetLevel, TB_MetLevel, CHK_Fateful);
        FLP_ObedienceLevel = UiFactory.Row(L_ObedienceLevel, TB_ObedienceLevel);
        FLP_GroundTile = UiFactory.Row(Label_GroundTile, CB_GroundTile);
        FLP_TimeOfDay = UiFactory.Row(L_MetTimeOfDay, CB_MetTimeOfDay);
        var eggGrid = UiFactory.FormGrid(2);
        UiFactory.AddFormRow(eggGrid, 0, Label_EggLocation, CB_EggLocation);
        UiFactory.AddFormRow(eggGrid, 1, Label_EggDate, CAL_EggDate);
        GB_EggConditions = GroupBox(GB_EggConditionsHeader, eggGrid);
        var met = UiFactory.Column(FLP_OriginGame, FLP_BattleVersion, FLP_MetLocation, FLP_Ball, FLP_MetDate, FLP_MetLevel, FLP_ObedienceLevel, FLP_GroundTile, FLP_TimeOfDay, CHK_AsEgg, GB_EggConditions);
        Tab_Met.Content = Scroll(met);

        // ---- Stats ----
        Tab_Stats.Content = Scroll(Stats);

        // ---- Moves ----
        var ppHeader = UiFactory.Row(Label_CurPP, Label_PPups);
        ppHeader.Margin = new Thickness(180, 0, 0, 0);
        FLP_Relearn1 = UiFactory.Row(PB_WarnRelearn1, CB_RelearnMove1);
        FLP_Relearn2 = UiFactory.Row(PB_WarnRelearn2, CB_RelearnMove2);
        FLP_Relearn3 = UiFactory.Row(PB_WarnRelearn3, CB_RelearnMove3);
        FLP_Relearn4 = UiFactory.Row(PB_WarnRelearn4, CB_RelearnMove4);
        FLP_MoveFlags = UiFactory.Row(B_RelearnFlags, B_MoveShop, B_PlusRecord);
        FLP_AlphaMove = UiFactory.Row(L_AlphaMastered, CB_AlphaMastered);
        var moves = UiFactory.Column(GB_CurrentMoves, ppHeader, MC_Move1, MC_Move2, MC_Move3, MC_Move4, GB_RelearnMoves, FLP_Relearn1, FLP_Relearn2, FLP_Relearn3, FLP_Relearn4, FLP_MoveFlags, FLP_AlphaMove);
        Tab_Moves.Content = Scroll(moves);

        // ---- Cosmetic ----
        var marks = new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = 4 * 26 };
        foreach (var pb in new[] { PB_Mark6, PB_Mark3, PB_Mark5, PB_MarkCured, PB_Mark2, PB_MarkShiny, PB_Mark1, PB_Mark4 })
        {
            pb.Margin = new Thickness(4);
            pb.Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand);
            marks.Children.Add(pb);
        }
        GB_Markings = GroupBox(GB_MarkingsHeader, marks);
        var bigMarks = UiFactory.Row(PB_Favorite, PB_Origin, PB_BattleVersion);
        foreach (var pb in new[] { PB_Favorite, PB_Origin, PB_BattleVersion, PB_Affixed })
            pb.Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand);
        FLP_Spirit7b = UiFactory.Row(L_Spirit7b, NUD_Spirit7b);
        FLP_Mood7b = UiFactory.Row(L_Mood7b, NUD_Mood7b);
        FLP_WalkingMood = UiFactory.Row(L_WalkingMood, NUD_WalkingMood);
        FLP_PokeStarFame = UiFactory.Row(L_PokeStarFame, NUD_PokeStarFame);
        FLP_PKMEditors = UiFactory.Row(BTN_Ribbons, BTN_Medals, BTN_History);
        var cosmetic = UiFactory.Column(UiFactory.Row(GB_Markings, PB_Affixed, bigMarks), FLP_Spirit7b, FLP_Mood7b, FLP_WalkingMood, FLP_PokeStarFame, SizeCP, ShinyLeaf, FLP_PKMEditors, Contest);
        Tab_Cosmetic.Content = Scroll(cosmetic);

        // ---- OT/Misc ----
        var ot = UiFactory.FormGrid(17);
        UiFactory.AddFormRow(ot, 0, null, GB_OT);
        UiFactory.AddFormRow(ot, 1, null, TID_Trainer);
        UiFactory.AddFormRow(ot, 2, Label_OT, UiFactory.Row(TB_OT, UC_OTGender, BTN_OTNameWarn));
        FLP_FriendshipLeft = UiFactory.Row(Label_Friendship, Label_HatchCounter);
        UiFactory.AddFormRow(ot, 3, FLP_FriendshipLeft, TB_Friendship);
        UiFactory.AddFormRow(ot, 4, Label_Country, CB_Country);
        UiFactory.AddFormRow(ot, 5, Label_SubRegion, CB_SubRegion);
        UiFactory.AddFormRow(ot, 6, Label_3DSRegion, CB_3DSReg);
        FLP_Handler = UiFactory.Row(L_CurrentHandler, CB_Handler);
        UiFactory.AddFormRow(ot, 7, null, FLP_Handler);
        UiFactory.AddFormRow(ot, 8, null, GB_nOT);
        FLP_HT = UiFactory.Row(TB_HT, UC_HTGender);
        UiFactory.AddFormRow(ot, 9, Label_PrevOT, FLP_HT);
        UiFactory.AddFormRow(ot, 10, L_LanguageHT, CB_HTLanguage);
        UiFactory.AddFormRow(ot, 11, L_FriendshipHT, TB_FriendshipHT);
        FLP_ExtraBytes = UiFactory.Row(CB_ExtraBytes, TB_ExtraByte);
        UiFactory.AddFormRow(ot, 12, L_ExtraBytes, FLP_ExtraBytes);
        UiFactory.AddFormRow(ot, 13, L_HomeTracker, TB_HomeTracker);
        FLP_EncryptionConstant = UiFactory.Row(TB_EC, BTN_RerollEC);
        UiFactory.AddFormRow(ot, 14, Label_EncryptionConstant, FLP_EncryptionConstant);
        UiFactory.AddFormRow(ot, 15, null, L_ArrivedDateTime);
        FLP_ReceivedDateTime = UiFactory.Row(CAL_ReceivedDate, CAL_ReceivedTime);
        UiFactory.AddFormRow(ot, 16, null, FLP_ReceivedDateTime);
        Tab_OTMisc.Content = Scroll(ot);

        foreach (var tab in new[] { Tab_Main, Tab_Met, Tab_Stats, Tab_Moves, Tab_Cosmetic, Tab_OTMisc })
        {
            tab.FontSize = 15;
            tab.Padding = new Thickness(10, 6);
            tab.MinHeight = 0;
        }
        TC_Editor.Items.Add(Tab_Main);
        TC_Editor.Items.Add(Tab_Met);
        TC_Editor.Items.Add(Tab_Stats);
        TC_Editor.Items.Add(Tab_Moves);
        TC_Editor.Items.Add(Tab_Cosmetic);
        TC_Editor.Items.Add(Tab_OTMisc);
        Content = TC_Editor;
    }

    private static ScrollViewer Scroll(Control content) => new()
    {
        Content = content,
        Padding = new Thickness(6),
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    };

    private static Border GroupBox(TextBlock header, Control content)
    {
        header.Margin = new Thickness(0, 0, 0, 4);
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(header);
        panel.Children.Add(content);
        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x80, 0x80, 0x80)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 6),
            Child = panel,
        };
    }

    /// <summary>
    /// Original (unmodified) marking images keyed by control (WinForms <c>InitialImage</c>).
    /// </summary>
    private void InitializeMarkingImages()
    {
        SetInitial(PB_Mark1, "box_mark_01"); SetInitial(PB_Mark2, "box_mark_02"); SetInitial(PB_Mark3, "box_mark_03");
        SetInitial(PB_Mark4, "box_mark_04"); SetInitial(PB_Mark5, "box_mark_05"); SetInitial(PB_Mark6, "box_mark_06");
        SetInitial(PB_MarkShiny, "rare_icon"); SetInitial(PB_MarkCured, "anti_pokerus_icon");
        SetInitial(PB_ShinyStar, "rare_icon"); SetInitial(PB_ShinySquare, "rare_icon_2");
        SetInitial(PB_Favorite, "icon_favo"); SetInitial(PB_BattleVersion, "icon_btlrom");
        PB_WarnRelearn1.Source = PB_WarnRelearn2.Source = PB_WarnRelearn3.Source = PB_WarnRelearn4.Source = AppResources.GetImage("warn");

        void SetInitial(Image pb, string resource)
        {
            InitialImages[pb] = resource;
            pb.Source = AppResources.GetImage(resource);
        }
    }
}
