using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Core;
using PKHeX.Drawing.Misc;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen6;

/// <summary>
/// Trainer editor for Generation 6 saves (port of the WinForms <c>SAV_Trainer</c>).
/// </summary>
/// <remarks>
/// Tabs follow the WinForms form: overview, records, Battle Maison, multiplayer, and the two X/Y-only tabs
/// (appearance and Battle Chateau), which are removed for OR/AS and the OR/AS demo just like upstream.
/// </remarks>
public sealed class Trainer6Window : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV6 SAV;

    // Overview
    private readonly TextBox TB_OTName = UiFactory.Text("TB_OTName", 12, 140);
    private readonly ComboBox CB_Gender = UiFactory.StringCombo("CB_Gender", 60);
    private readonly ComboBox CB_Game = UiFactory.StringCombo("CB_Game", 120);
    private readonly NumericTextBox MT_TID = UiFactory.Numeric("MT_TID", 5, 70);
    private readonly NumericTextBox MT_SID = UiFactory.Numeric("MT_SID", 5, 70);
    private readonly NumericTextBox MT_Money = UiFactory.Numeric("MT_Money", 8, 100);
    private readonly Button B_MaxCash = UiFactory.Button("B_MaxCash", "Max");
    private readonly ComboBox CB_Country = UiFactory.Combo("CB_Country", 180);
    private readonly ComboBox CB_Region = UiFactory.Combo("CB_Region", 180);
    private readonly ComboBox CB_3DSReg = UiFactory.Combo("CB_3DSReg", 180);
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 140);
    private readonly NumericTextBox MT_Hours = UiFactory.Numeric("MT_Hours", 5, 60);
    private readonly NumericTextBox MT_Minutes = UiFactory.Numeric("MT_Minutes", 2, 44);
    private readonly NumericTextBox MT_Seconds = UiFactory.Numeric("MT_Seconds", 2, 44);
    private readonly NumericTextBox TB_BP = UiFactory.Numeric("TB_BP", 5, 70);
    private readonly NumericTextBox TB_PM = UiFactory.Numeric("TB_PM", 7, 90);
    private readonly NumericTextBox TB_Style = UiFactory.Numeric("TB_Style", 3, 50);
    private readonly TextBlock L_Style = UiFactory.Label("L_Style", "Style:");
    private readonly CheckBox[] cba;
    private readonly CheckBox CHK_MegaUnlocked = UiFactory.Check("CHK_MegaUnlocked", "Mega Unlocked");
    private readonly CheckBox CHK_MegaRayquazaUnlocked = UiFactory.Check("CHK_MegaRayquazaUnlocked", "Mega Rayquaza Unlocked");
    private readonly ComboBox CB_Vivillon = UiFactory.StringCombo("CB_Vivillon", 160);
    private readonly TextBlock L_Vivillon = UiFactory.Label("L_Vivillon", "Vivillon:");

    private readonly DatePicker CAL_AdventureStartDate = new() { Name = "CAL_AdventureStartDate" };
    private readonly TimePicker CAL_AdventureStartTime = new() { Name = "CAL_AdventureStartTime" };
    private readonly DatePicker CAL_HoFDate = new() { Name = "CAL_HoFDate" };
    private readonly TimePicker CAL_HoFTime = new() { Name = "CAL_HoFTime" };
    private readonly DatePicker CAL_LastSavedDate = new() { Name = "CAL_LastSavedDate" };
    private readonly TimePicker CAL_LastSavedTime = new() { Name = "CAL_LastSavedTime" };
    private readonly TextBlock L_LastSaved = UiFactory.Label("L_LastSaved", "Last Saved:");

    // Sayings
    private readonly TextBox[] Sayings =
    [
        UiFactory.Text("TB_Saying1", 16, 220), UiFactory.Text("TB_Saying2", 16, 220),
        UiFactory.Text("TB_Saying3", 16, 220), UiFactory.Text("TB_Saying4", 16, 220),
        UiFactory.Text("TB_Saying5", 16, 220),
    ];

    // Map
    private readonly NumericUpDown NUD_M = UiFactory.NumericUpDown("NUD_M", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_X = UiFactory.NumericUpDown("NUD_X", -1000, 1000, 110);
    private readonly NumericUpDown NUD_Z = UiFactory.NumericUpDown("NUD_Z", -1000, 1000, 110);
    private readonly NumericUpDown NUD_Y = UiFactory.NumericUpDown("NUD_Y", -1000, 1000, 110);
    private readonly NumericUpDown NUD_R = UiFactory.NumericUpDown("NUD_R", 0, ushort.MaxValue, 110);
    private GroupBoxView GB_Map = null!;

    // Records / Maison
    private readonly TrainerStatView TrainerStats = new();
    private readonly NumericTextBox[] MaisonRecords;

    // Multiplayer
    private readonly TextBlock L_MultiplayerSprite = UiFactory.Label("L_MultiplayerSprite", "Multiplayer Sprite:");
    private readonly ComboBox CB_MultiplayerSprite = UiFactory.Combo("CB_MultiplayerSprite", 180);
    private readonly Image PB_Sprite = UiFactory.Picture("PB_Sprite", 64);

    // X/Y only
    private readonly ComboBox CB_BattleChateauRank = UiFactory.StringCombo("CB_BattleChateauRank", 160);
    private readonly NumericUpDown NUD_BattleChateauPoints = UiFactory.NumericUpDown("NUD_BattleChateauPoints", 0, ushort.MaxValue, 110);
    private readonly PropertyGridView PG_CurrentAppearance = new() { Name = "PG_CurrentAppearance" };
    private readonly TextBox TB_TRNick = UiFactory.Text("TB_TRNick", 12, 140);
    private readonly Button B_GiveAllAccessories = UiFactory.Button("B_GiveAllAccessories", "Unlock all Accessories");

    private readonly bool editing;
    private bool MapUpdated;

    public Trainer6Window(SAV6 sav) : base("SAV_Trainer", "Trainer Data Editor")
    {
        SAV = (SAV6)(Origin = sav).Clone();

        cba = [.. Enumerable.Range(1, 8).Select(i => UiFactory.Check($"CHK_Badge{i}", i.ToString()))];
        MaisonRecords = [.. new[]
        {
            "TB_MCSN","TB_MCSS","TB_MBSN","TB_MBSS",
            "TB_MCDN","TB_MCDS","TB_MBDN","TB_MBDS",
            "TB_MCTN","TB_MCTS","TB_MBTN","TB_MBTS",
            "TB_MCRN","TB_MCRS","TB_MBRN","TB_MBRS",
            "TB_MCMN","TB_MCMS","TB_MBMN","TB_MBMS",
        }.Select(n => UiFactory.Numeric(n, 5, 70))];

        CB_Gender.Items.Clear();
        foreach (var s in GameInfo.GenderSymbolUnicode.Take(2))
            CB_Gender.Items.Add(s);
        foreach (var v in Enum.GetValues<GameVersion>().Where(z => z is GameVersion.X or GameVersion.Y or GameVersion.AS or GameVersion.OR))
            CB_Game.Items.Add(v.ToString());

        TrainerStats.GetToolTipText = UpdateTip;

        var tabs = BuildTabs();
        SetBody(tabs);
        // after SetBody: TranslateInterface would otherwise overwrite the computed offset label
        TrainerStats.LoadRecords(SAV, RecordLists.RecordList_6);

        editing = true;
        GetComboBoxes();
        GetTextBoxes();
        editing = false;

        var status = SAV.Status;
        CHK_MegaUnlocked.IsChecked = status.IsMegaEvolutionUnlocked;
        CHK_MegaRayquazaUnlocked.IsChecked = status.IsMegaRayquazaUnlocked;

        B_MaxCash.Click += (_, _) => MT_Money.Text = "9999999";
        MT_TID.OnTextChanged(_ => ShowTSV());
        MT_SID.OnTextChanged(_ => ShowTSV());
        CB_Country.SelectionChanged += (_, _) => UpdateCountry();
        CB_MultiplayerSprite.SelectionChanged += (_, _) => ChangeMultiplayerSprite();
        foreach (var nud in new[] { NUD_M, NUD_X, NUD_Z, NUD_Y, NUD_R })
            nud.ValueChanged += (_, _) => { if (!editing) MapUpdated = true; };
        B_GiveAllAccessories.Click += (_, _) =>
        {
            if (SAV is SAV6XY xy)
                xy.Blocks.Fashion.UnlockAllAccessories();
        };
    }

    #region Layout

    private TabControl BuildTabs()
    {
        var tabs = new TabControl { Name = "TC_Editor" };

        // Overview
        var main = UiFactory.FormGrid(12);
        UiFactory.AddFormRow(main, 0, UiFactory.Label("L_TrainerName", "Trainer Name:"), UiFactory.Row(TB_OTName, CB_Gender));
        UiFactory.AddFormRow(main, 1, UiFactory.Label("L_Game", "Game:"), CB_Game);
        UiFactory.AddFormRow(main, 2, UiFactory.Label("L_TrainerID", "TID/SID:"), UiFactory.Row(MT_TID, MT_SID));
        UiFactory.AddFormRow(main, 3, UiFactory.Label("L_Money", "Money:"), UiFactory.Row(MT_Money, B_MaxCash));
        UiFactory.AddFormRow(main, 4, UiFactory.Label("L_Language", "Language:"), CB_Language);
        UiFactory.AddFormRow(main, 5, UiFactory.Label("L_Country", "Country:"), CB_Country);
        UiFactory.AddFormRow(main, 6, UiFactory.Label("L_Region", "Region:"), CB_Region);
        UiFactory.AddFormRow(main, 7, UiFactory.Label("L_3DSReg", "3DS Region:"), CB_3DSReg);
        UiFactory.AddFormRow(main, 8, UiFactory.Label("L_PlayTime", "Play Time:"), UiFactory.Row(MT_Hours, MT_Minutes, MT_Seconds));
        UiFactory.AddFormRow(main, 9, UiFactory.Label("L_BP", "BP / PokéMiles:"), UiFactory.Row(TB_BP, TB_PM));
        UiFactory.AddFormRow(main, 10, L_Style, TB_Style);
        UiFactory.AddFormRow(main, 11, L_Vivillon, CB_Vivillon);

        var badges = new GroupBoxView("GB_Badges", "Badges", UiFactory.Row(cba));
        var megas = UiFactory.Column(CHK_MegaUnlocked, CHK_MegaRayquazaUnlocked);

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

        var sayings = new GroupBoxView("GB_Sayings", "Sayings", UiFactory.Column(Sayings));

        var overview = UiFactory.Column(main, badges, megas, dates, GB_Map, sayings);
        tabs.Items.Add(new TabItem { Name = "Tab_Overview", Header = "Overview", Content = new ScrollViewer { Content = overview, MaxHeight = 560 } });

        // Records
        tabs.Items.Add(new TabItem { Name = "Tab_Records", Header = "Records", Content = TrainerStats });

        // Battle Maison
        var maison = new Grid { ColumnSpacing = 6, RowSpacing = 4 };
        for (int i = 0; i < 5; i++)
            maison.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        string[] modes = ["Single", "Double", "Triple", "Rotation", "Multi"];
        for (int row = 0; row < 5; row++)
        {
            maison.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var label = UiFactory.Label($"L_Maison{row}", modes[row]);
            UiFactory.SetRowCol(label, row, 0);
            maison.Children.Add(label);
            for (int col = 0; col < 4; col++)
            {
                var box = MaisonRecords[(row * 4) + col];
                UiFactory.SetRowCol(box, row, col + 1);
                maison.Children.Add(box);
            }
        }
        tabs.Items.Add(new TabItem { Name = "Tab_Maison", Header = "Maison", Content = maison });

        // Multiplayer
        var multi = UiFactory.Column(UiFactory.Row(L_MultiplayerSprite, CB_MultiplayerSprite), PB_Sprite);
        tabs.Items.Add(new TabItem { Name = "Tab_Multiplayer", Header = "Multiplayer", Content = multi });

        // X/Y only tabs
        if (SAV is SAV6XY)
        {
            var appearance = UiFactory.Column(
                UiFactory.Row(UiFactory.Label("L_TRNick", "Nickname:"), TB_TRNick),
                B_GiveAllAccessories,
                PG_CurrentAppearance);
            tabs.Items.Add(new TabItem { Name = "Tab_Appearance", Header = "Appearance", Content = new ScrollViewer { Content = appearance, MaxHeight = 520 } });

            var chateau = UiFactory.FormGrid(2);
            UiFactory.AddFormRow(chateau, 0, UiFactory.Label("L_ChateauRank", "Rank:"), CB_BattleChateauRank);
            UiFactory.AddFormRow(chateau, 1, UiFactory.Label("L_ChateauPoints", "Points:"), NUD_BattleChateauPoints);
            tabs.Items.Add(new TabItem { Name = "Tab_BattleChateau", Header = "Battle Chateau", Content = chateau });
        }

        // Visibility rules, matching the WinForms constructor.
        bool notDemo = SAV is not SAV6AODemo;
        L_MultiplayerSprite.IsEnabled = CB_MultiplayerSprite.IsEnabled = notDemo;
        L_MultiplayerSprite.IsVisible = CB_MultiplayerSprite.IsVisible = PB_Sprite.IsVisible = notDemo;
        CHK_MegaRayquazaUnlocked.IsVisible = SAV is SAV6AO;
        L_Style.IsVisible = TB_Style.IsVisible = SAV is SAV6XY;
        if (SAV is SAV6AODemo)
        {
            foreach (var name in new[] { "Tab_Multiplayer", "Tab_Maison" })
            {
                var tab = tabs.Items.OfType<TabItem>().FirstOrDefault(z => z.Name == name);
                if (tab is not null)
                    tabs.Items.Remove(tab);
            }
        }
        return tabs;
    }

    #endregion

    #region Load

    private void GetComboBoxes()
    {
        var sources = GameInfo.Sources;
        CB_3DSReg.SetItems(sources.Regions);
        CB_Language.SetItems(GameInfo.LanguageDataSource(SAV.Generation, SAV.Context));
        CB_Country.SetCountrySubRegion("countries");

        var names = Translator.GetEnumTranslation<TrainerSprite6>(MainWindow.CurrentLanguage);
        var values = Enum.GetValues<TrainerSprite6>();
        var max = SAV is not SAV6AO ? (int)TrainerSprite6.Trevor : names.Length;
        var data = new ComboItem[max];
        for (int i = 0; i < max; i++)
            data[i] = new ComboItem(names[i], (int)values[i]);
        CB_MultiplayerSprite.SetItems(data);

        L_Vivillon.Text = GameInfo.Strings.specieslist[(int)Species.Vivillon] + ":";
        CB_Vivillon.Items.Clear();
        foreach (var form in FormConverter.GetFormList((int)Species.Vivillon, GameInfo.Strings.types, GameInfo.Strings.forms, GameInfo.GenderSymbolUnicode, SAV.Context))
            CB_Vivillon.Items.Add(form);
    }

    private void GetTextBoxes()
    {
        int badges = SAV.Badges;
        for (int i = 0; i < cba.Length; i++)
            cba[i].IsChecked = (badges & (1 << i)) != 0;

        CB_Game.SelectedIndex = Math.Clamp((int)(SAV.Version - 0x18), 0, CB_Game.ItemCount - 1);
        CB_Gender.SelectedIndex = SAV.Gender;
        TB_OTName.Text = SAV.OT;

        MT_TID.Text = SAV.TID16.ToString("00000");
        MT_SID.Text = SAV.SID16.ToString("00000");
        MT_Money.Text = SAV.Money.ToString();

        var status = SAV.Status;
        Sayings[0].Text = status.Saying1;
        Sayings[1].Text = status.Saying2;
        Sayings[2].Text = status.Saying3;
        Sayings[3].Text = status.Saying4;
        Sayings[4].Text = status.Saying5;

        CB_Country.SetValue(SAV.Country);
        UpdateCountry();
        CB_Region.SetValue(SAV.Region);
        CB_3DSReg.SetValue(SAV.ConsoleRegion);
        CB_Language.SetValue(SAV.Language);

        if (SAV is ISaveBlock6Main xyao)
        {
            for (int i = 0; i < MaisonBlock.MaisonStatCount && i < MaisonRecords.Length; i++)
                MaisonRecords[i].Text = xyao.Maison.GetMaisonStat(i).ToString();
        }

        var sit = SAV.Situation;
        NUD_M.Value = sit.M;
        try
        {
            NUD_X.Value = (decimal)(sit.X / 18.0);
            NUD_Z.Value = (decimal)(sit.Z / 18.0);
            NUD_Y.Value = (decimal)(sit.Y / 18.0);
            NUD_R.Value = sit.R;
        }
        catch (OverflowException)
        {
            GB_Map.IsEnabled = false; // coordinates outside the editable range
        }

        TB_BP.Text = SAV.BP.ToString();
        TB_PM.Text = SAV.GetRecord(63).ToString();
        TB_Style.Text = sit.Style.ToString();

        MT_Hours.Text = SAV.PlayedHours.ToString();
        MT_Minutes.Text = SAV.PlayedMinutes.ToString();
        MT_Seconds.Text = SAV.PlayedSeconds.ToString();

        if (SAV is IMultiplayerSprite ms)
            CB_MultiplayerSprite.SetValue(ms.MultiplayerSpriteID);
        RefreshSprite();

        if (SAV is SAV6XY xy)
        {
            var sube = xy.SUBE;
            CB_BattleChateauRank.Items.Clear();
            foreach (var name in Translator.GetEnumTranslation<BattleChateauRank6>(MainWindow.CurrentLanguage))
                CB_BattleChateauRank.Items.Add(name);
            CB_BattleChateauRank.SelectedIndex = Math.Clamp(sube.ChateauRank, 0, CB_BattleChateauRank.ItemCount - 1);
            NUD_BattleChateauPoints.Value = sube.ChateauPoints;
            CB_BattleChateauRank.SelectionChanged += (_, _)
                => NUD_BattleChateauPoints.Value = SubEventLog6XY.GetChateauPointsForRank((ushort)CB_BattleChateauRank.SelectedIndex);

            var xystat = xy.Status;
            PG_CurrentAppearance.SetObject(xystat.Fashion);
            TB_TRNick.Text = xystat.Nickname;
        }

        CB_Vivillon.SelectedIndex = Math.Clamp(SAV.Vivillon, 0, CB_Vivillon.ItemCount - 1);

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
    }

    private void RefreshSprite()
    {
        var sprite = SAV.Sprite();
        PB_Sprite.Source = sprite?.ToAvaloniaBitmapAndDispose();
    }

    #endregion

    #region Handlers

    private void ShowTSV()
    {
        uint tid = Util.ToUInt32(MT_TID.Text ?? string.Empty);
        uint sid = Util.ToUInt32(MT_SID.Text ?? string.Empty);
        var tsv = (tid ^ sid) >> 4;
        ToolTip.SetTip(MT_TID, $"TSV: {tsv:0000}");
        ToolTip.SetTip(MT_SID, $"TSV: {tsv:0000}");
    }

    private void UpdateCountry()
    {
        var index = CB_Country.GetSelectedItem()?.Value ?? 0;
        if (index > 0)
            CB_Region.SetCountrySubRegion($"sr_{index:000}");
    }

    private void ChangeMultiplayerSprite()
    {
        if (editing)
            return;
        if (SAV is IMultiplayerSprite ms)
            ms.MultiplayerSpriteID = (byte)(CB_MultiplayerSprite.GetSelectedItem()?.Value ?? 0);
        RefreshSprite();
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

    #endregion

    protected override void OnSave()
    {
        SAV.Version = (GameVersion)(CB_Game.SelectedIndex + 0x18);
        SAV.Gender = (byte)Math.Max(0, CB_Gender.SelectedIndex);
        SAV.Overworld.ResetPlayerModel();

        SAV.TID16 = (ushort)Util.ToUInt32(MT_TID.Text ?? string.Empty);
        SAV.SID16 = (ushort)Util.ToUInt32(MT_SID.Text ?? string.Empty);
        SAV.Money = Util.ToUInt32(MT_Money.Text ?? string.Empty);
        SAV.Region = (byte)(CB_Region.GetSelectedItem()?.Value ?? 0);
        SAV.Country = (byte)(CB_Country.GetSelectedItem()?.Value ?? 0);
        SAV.ConsoleRegion = (byte)(CB_3DSReg.GetSelectedItem()?.Value ?? 0);
        SAV.Language = CB_Language.GetSelectedItem()?.Value ?? 0;

        if (SAV.OT != TB_OTName.Text) // only modify if changed, to preserve trash bytes
            SAV.OT = TB_OTName.Text ?? string.Empty;

        var status = SAV.Status;
        status.Saying1 = Sayings[0].Text ?? string.Empty;
        status.Saying2 = Sayings[1].Text ?? string.Empty;
        status.Saying3 = Sayings[2].Text ?? string.Empty;
        status.Saying4 = Sayings[3].Text ?? string.Empty;
        status.Saying5 = Sayings[4].Text ?? string.Empty;

        if (SAV is ISaveBlock6Main xyao)
        {
            for (int i = 0; i < MaisonBlock.MaisonStatCount && i < MaisonRecords.Length; i++)
                xyao.Maison.SetMaisonStat(i, (ushort)Util.ToUInt32(MaisonRecords[i].Text ?? string.Empty));
        }

        var sit = SAV.Situation;
        if (GB_Map.IsEnabled && MapUpdated)
        {
            sit.M = (int)(NUD_M.Value ?? 0);
            sit.X = (float)((NUD_X.Value ?? 0) * 18);
            sit.Z = (float)((NUD_Z.Value ?? 0) * 18);
            sit.Y = (float)((NUD_Y.Value ?? 0) * 18);
            sit.R = (int)(NUD_R.Value ?? 0);
        }

        SAV.BP = (ushort)Util.ToUInt32(TB_BP.Text ?? string.Empty);
        var miles = Util.ToInt32(TB_PM.Text ?? string.Empty);
        SAV.SetRecord(63, miles); // current
        SAV.SetRecord(64, miles); // max obtained
        sit.Style = (byte)Util.ToUInt32(TB_Style.Text ?? string.Empty);

        int badgeVal = 0;
        for (int i = 0; i < cba.Length; i++)
            badgeVal |= (cba[i].IsChecked == true ? 1 : 0) << i;
        SAV.Badges = badgeVal;

        SAV.PlayedHours = (ushort)Util.ToUInt32(MT_Hours.Text ?? string.Empty);
        SAV.PlayedMinutes = (ushort)(Util.ToUInt32(MT_Minutes.Text ?? string.Empty) % 60);
        SAV.PlayedSeconds = (ushort)(Util.ToUInt32(MT_Seconds.Text ?? string.Empty) % 60);

        if (SAV is IMultiplayerSprite ms)
            ms.MultiplayerSpriteID = (byte)(CB_MultiplayerSprite.GetSelectedItem()?.Value ?? 0);

        if (SAV is SAV6XY xy)
        {
            var sube = xy.SUBE;
            sube.ChateauRank = (ushort)Math.Max(0, CB_BattleChateauRank.SelectedIndex);
            sube.ChateauPoints = (ushort)(NUD_BattleChateauPoints.Value ?? 0);

            var xystat = xy.Status;
            if (PG_CurrentAppearance.SelectedObject is TrainerFashion6 fashion)
                xystat.Fashion = fashion;
            xystat.Nickname = TB_TRNick.Text ?? string.Empty;
        }

        SAV.Vivillon = Math.Max(0, CB_Vivillon.SelectedIndex);
        SAV.SecondsToStart = (uint)DateUtil.GetSecondsFrom2000(GetDate(CAL_AdventureStartDate), GetTime(CAL_AdventureStartTime));
        SAV.SecondsToFame = (uint)DateUtil.GetSecondsFrom2000(GetDate(CAL_HoFDate), GetTime(CAL_HoFTime));

        if (SAV.Played.LastSavedDate.HasValue)
        {
            var d = GetDate(CAL_LastSavedDate);
            var t = CAL_LastSavedTime.SelectedTime ?? TimeSpan.Zero;
            SAV.Played.LastSavedDate = new DateTime(d.Year, d.Month, d.Day, t.Hours, t.Minutes, 0);
        }

        status.IsMegaEvolutionUnlocked = CHK_MegaUnlocked.IsChecked == true;
        status.IsMegaRayquazaUnlocked = CHK_MegaRayquazaUnlocked.IsChecked == true;

        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
