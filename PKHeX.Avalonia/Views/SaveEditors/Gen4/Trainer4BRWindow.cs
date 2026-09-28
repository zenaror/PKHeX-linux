using System;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4;

/// <summary>
/// Trainer info editor for Battle Revolution (port of the WinForms <c>SAV_Trainer4BR</c>).
/// </summary>
/// <remarks>
/// Battle Revolution keeps its own profile (self-introduction, birthday, player ID) plus the battle records and
/// colosseum unlock flags, so it does not use the generic <see cref="SimpleTrainerWindow"/>.
/// </remarks>
public sealed class Trainer4BRWindow : SaveEditorWindow
{
    private readonly SAV4BR Origin;
    private readonly SAV4BR SAV;

    // Profile
    private readonly TextBlock L_TrainerName = UiFactory.Label("L_TrainerName", "Name:");
    private readonly RenderedString TB_OTName = UiFactory.Name("TB_OTName", 7, 180);
    private readonly TextBlock L_BirthMonth = UiFactory.Label("L_BirthMonth", "Birth Month:");
    private readonly TextBox TB_BirthMonth = UiFactory.Text("TB_BirthMonth", 4, 60);
    private readonly TextBlock L_BirthDay = UiFactory.Label("L_BirthDay", "Birth Day:");
    private readonly TextBox TB_BirthDay = UiFactory.Text("TB_BirthDay", 4, 60);
    private readonly TextBlock L_Country = UiFactory.Label("L_Country", "Country:");
    private readonly ComboBox CB_Country = UiFactory.Combo("CB_Country", 180);
    private readonly TextBlock L_Region = UiFactory.Label("L_Region", "Sub Region:");
    private readonly ComboBox CB_Region = UiFactory.Combo("CB_Region", 180);
    private readonly TextBlock L_SelfIntroduction = UiFactory.Label("L_SelfIntroduction", "Self-Introduction:");
    private readonly TextBox TB_SelfIntroduction = new()
    {
        Name = "TB_SelfIntroduction",
        AcceptsReturn = true,
        MaxLength = 53,
        Width = 380,
        Height = 56,
        TextWrapping = global::Avalonia.Media.TextWrapping.NoWrap,
    };
    private readonly TextBlock L_PlayerID = UiFactory.Label("L_PlayerID", "Player ID:");
    private readonly NumericTextBox MT_PlayerID = new()
    {
        Name = "MT_PlayerID",
        MaxLength = 16,
        Width = 150,
        IsHex = true,
        VerticalAlignment = VerticalAlignment.Center,
        FontFamily = new global::Avalonia.Media.FontFamily("monospace"), // WinForms uses Courier New for the 16 hex digits
    };
    private readonly TextBlock L_Language = UiFactory.Label("L_Language", "Language:");
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 120);

    // Records
    private readonly TextBlock L_Hours = UiFactory.Label("L_Hours", "Hrs:");
    private readonly NumericTextBox MT_Hours = UiFactory.Numeric("MT_Hours", 5, 60);
    private readonly TextBlock L_Minutes = UiFactory.Label("L_Minutes", "Min:");
    private readonly NumericTextBox MT_Minutes = UiFactory.Numeric("MT_Minutes", 2, 40);
    private readonly TextBlock L_Seconds = UiFactory.Label("L_Seconds", "Sec:");
    private readonly NumericTextBox MT_Seconds = UiFactory.Numeric("MT_Seconds", 2, 40);
    private readonly TextBlock L_Coupons = UiFactory.Label("L_Coupons", "Poké Coupons:");
    private readonly NumericTextBox MT_Money = UiFactory.Numeric("MT_Money", 6, 80);
    private readonly Button B_MaxCash = UiFactory.Button("B_MaxCash", "+");
    private readonly TextBlock L_TID = UiFactory.Label("L_TID", "Nintendo DS TID:");
    private readonly NumericTextBox MT_TID = UiFactory.Numeric("MT_TID", 5, 85);
    private readonly TextBlock L_SID = UiFactory.Label("L_SID", "Nintendo DS SID:");
    private readonly NumericTextBox MT_SID = UiFactory.Numeric("MT_SID", 5, 85);

    private const decimal MaxBattleCount = 16777215; // 3 bytes, as in the WinForms designer
    private readonly TextBlock L_RecordTotalBattles = UiFactory.Label("L_RecordTotalBattles", "Total Battles:");
    private readonly NumericUpDown NUD_RecordTotalBattles = UiFactory.NumericUpDown("NUD_RecordTotalBattles", 0, MaxBattleCount, 110);
    private readonly TextBlock L_RecordColosseumBattles = UiFactory.Label("L_RecordColosseumBattles", "Colosseum Battles:");
    private readonly NumericUpDown NUD_RecordColosseumBattles = UiFactory.NumericUpDown("NUD_RecordColosseumBattles", 0, MaxBattleCount, 110);
    private readonly TextBlock L_RecordFreeBattles = UiFactory.Label("L_RecordFreeBattles", "Free Battles:");
    private readonly NumericUpDown NUD_RecordFreeBattles = UiFactory.NumericUpDown("NUD_RecordFreeBattles", 0, MaxBattleCount, 110);
    private readonly TextBlock L_RecordWiFiBattles = UiFactory.Label("L_RecordWiFiBattles", "Wi-Fi Battles:");
    private readonly NumericUpDown NUD_RecordWiFiBattles = UiFactory.NumericUpDown("NUD_RecordWiFiBattles", 0, MaxBattleCount, 110);

    private readonly TextBlock L_RecordGatewayColosseumClears = UiFactory.Label("L_RecordGatewayColosseumClears", "Gateway Colosseum:");
    private readonly NumericUpDown NUD_RecordGatewayColosseumClears = UiFactory.NumericUpDown("NUD_RecordGatewayColosseumClears", 0, byte.MaxValue, 110);
    private readonly TextBlock L_RecordMainStreetColosseumClears = UiFactory.Label("L_RecordMainStreetColosseumClears", "Main Street Colosseum:");
    private readonly NumericUpDown NUD_RecordMainStreetColosseumClears = UiFactory.NumericUpDown("NUD_RecordMainStreetColosseumClears", 0, byte.MaxValue, 110);
    private readonly TextBlock L_RecordWaterfallColosseumClears = UiFactory.Label("L_RecordWaterfallColosseumClears", "Waterfall Colosseum:");
    private readonly NumericUpDown NUD_RecordWaterfallColosseumClears = UiFactory.NumericUpDown("NUD_RecordWaterfallColosseumClears", 0, byte.MaxValue, 110);
    private readonly TextBlock L_RecordNeonColosseumClears = UiFactory.Label("L_RecordNeonColosseumClears", "Neon Colosseum:");
    private readonly NumericUpDown NUD_RecordNeonColosseumClears = UiFactory.NumericUpDown("NUD_RecordNeonColosseumClears", 0, byte.MaxValue, 110);
    private readonly TextBlock L_RecordCrystalColosseumClears = UiFactory.Label("L_RecordCrystalColosseumClears", "Crystal Colosseum:");
    private readonly NumericUpDown NUD_RecordCrystalColosseumClears = UiFactory.NumericUpDown("NUD_RecordCrystalColosseumClears", 0, byte.MaxValue, 110);
    private readonly TextBlock L_RecordSunnyParkColosseumClears = UiFactory.Label("L_RecordSunnyParkColosseumClears", "Sunny Park Colosseum:");
    private readonly NumericUpDown NUD_RecordSunnyParkColosseumClears = UiFactory.NumericUpDown("NUD_RecordSunnyParkColosseumClears", 0, byte.MaxValue, 110);
    private readonly TextBlock L_RecordMagmaColosseumClears = UiFactory.Label("L_RecordMagmaColosseumClears", "Magma Colosseum:");
    private readonly NumericUpDown NUD_RecordMagmaColosseumClears = UiFactory.NumericUpDown("NUD_RecordMagmaColosseumClears", 0, byte.MaxValue, 110);
    private readonly TextBlock L_RecordSunsetColosseumClears = UiFactory.Label("L_RecordSunsetColosseumClears", "Sunset Colosseum:");
    private readonly NumericUpDown NUD_RecordSunsetColosseumClears = UiFactory.NumericUpDown("NUD_RecordSunsetColosseumClears", 0, byte.MaxValue, 110);
    private readonly TextBlock L_RecordCourtyardColosseumClears = UiFactory.Label("L_RecordCourtyardColosseumClears", "Courtyard Colosseum:");
    private readonly NumericUpDown NUD_RecordCourtyardColosseumClears = UiFactory.NumericUpDown("NUD_RecordCourtyardColosseumClears", 0, byte.MaxValue, 110);
    private readonly TextBlock L_RecordStargazerColosseumClears = UiFactory.Label("L_RecordStargazerColosseumClears", "Stargazer Colosseum:");
    private readonly NumericUpDown NUD_RecordStargazerColosseumClears = UiFactory.NumericUpDown("NUD_RecordStargazerColosseumClears", 0, byte.MaxValue, 110);

    // Colosseum unlock flags; the WinForms form shows them as bare checkboxes in colosseum order.
    private readonly CheckBox CHK_1 = UiFactory.Check("CHK_1", string.Empty);
    private readonly CheckBox CHK_2 = UiFactory.Check("CHK_2", string.Empty);
    private readonly CheckBox CHK_3 = UiFactory.Check("CHK_3", string.Empty);
    private readonly CheckBox CHK_4 = UiFactory.Check("CHK_4", string.Empty);
    private readonly CheckBox CHK_5 = UiFactory.Check("CHK_5", string.Empty);
    private readonly CheckBox CHK_6 = UiFactory.Check("CHK_6", string.Empty);
    private readonly CheckBox CHK_7 = UiFactory.Check("CHK_7", string.Empty);
    private readonly CheckBox CHK_8 = UiFactory.Check("CHK_8", string.Empty);
    private readonly CheckBox CHK_9 = UiFactory.Check("CHK_9", string.Empty);
    private readonly CheckBox CHK_10 = UiFactory.Check("CHK_10", string.Empty);
    private readonly CheckBox CHK_PostGame = UiFactory.Check("CHK_PostGame", "Post-Game");

    public Trainer4BRWindow(SAV4BR sav) : base("SAV_Trainer4BR", "Trainer Data Editor")
    {
        SAV = (SAV4BR)(Origin = sav).Clone();

        BuildLayout();

        // Behavior (attached before the values load, so the cascades below run like the WinForms designer events)
        TB_OTName.MaxLength = SAV.MaxStringLengthTrainer;
        TB_OTName.DisplayContext = EntityContext.Gen4;
        B_MaxCash.Click += (_, _) => MT_Money.Text = SAV.MaxMoney.ToString();
        MT_TID.OnTextChanged(ChangeFFFF);
        MT_SID.OnTextChanged(ChangeFFFF);
        MT_PlayerID.LostFocus += (_, _) => ValidatePlayerID();
        TB_SelfIntroduction.LostFocus += (_, _) => ValidateCatchphrase();
        CB_Country.SelectionChanged += (_, _) => UpdateCountry();
        CB_Language.SelectionChanged += (_, _) => UpdateLanguage();

        CB_Country.SetCountrySubRegion("gen4_countries");
        CB_Language.SetItems(GameInfo.LanguageDataSource(3, EntityContext.Gen4));

        TB_OTName.Text = SAV.CurrentOT;
        TB_BirthMonth.Text = SAV.BirthMonth;
        TB_BirthDay.Text = SAV.BirthDay;
        CB_Country.SetValue(SAV.Country);
        CB_Region.SetValue(SAV.Region);
        TB_SelfIntroduction.Text = JoinLines(SAV.SelfIntroduction.TrimStart(StringConverter4GC.Proportional));
        MT_PlayerID.Text = SAV.PlayerID.ToString("X16");
        CB_Language.SetValue(SAV.Language);

        MT_Hours.Text = SAV.PlayedHours.ToString();
        MT_Minutes.Text = SAV.PlayedMinutes.ToString();
        MT_Seconds.Text = SAV.PlayedSeconds.ToString();
        MT_Money.Text = SAV.Money.ToString();
        MT_TID.Text = SAV.TID16.ToString("00000");
        MT_SID.Text = SAV.SID16.ToString("00000");

        NUD_RecordTotalBattles.Value = SAV.RecordTotalBattles;
        NUD_RecordColosseumBattles.Value = SAV.RecordColosseumBattles;
        NUD_RecordFreeBattles.Value = SAV.RecordFreeBattles;
        NUD_RecordWiFiBattles.Value = SAV.RecordWiFiBattles;
        NUD_RecordGatewayColosseumClears.Value = SAV.RecordGatewayColosseumClears;
        NUD_RecordMainStreetColosseumClears.Value = SAV.RecordMainStreetColosseumClears;
        NUD_RecordWaterfallColosseumClears.Value = SAV.RecordWaterfallColosseumClears;
        NUD_RecordNeonColosseumClears.Value = SAV.RecordNeonColosseumClears;
        NUD_RecordCrystalColosseumClears.Value = SAV.RecordCrystalColosseumClears;
        NUD_RecordSunnyParkColosseumClears.Value = SAV.RecordSunnyParkColosseumClears;
        NUD_RecordMagmaColosseumClears.Value = SAV.RecordMagmaColosseumClears;
        NUD_RecordCourtyardColosseumClears.Value = SAV.RecordCourtyardColosseumClears;
        NUD_RecordSunsetColosseumClears.Value = SAV.RecordSunsetColosseumClears;
        NUD_RecordStargazerColosseumClears.Value = SAV.RecordStargazerColosseumClears;

        CHK_1.IsChecked = SAV.UnlockedGatewayColosseum;
        CHK_2.IsChecked = SAV.UnlockedMainStreetColosseum;
        CHK_3.IsChecked = SAV.UnlockedWaterfallColosseum;
        CHK_4.IsChecked = SAV.UnlockedNeonColosseum;
        CHK_5.IsChecked = SAV.UnlockedCrystalColosseum;
        CHK_6.IsChecked = SAV.UnlockedSunnyParkColosseum;
        CHK_7.IsChecked = SAV.UnlockedMagmaColosseum;
        CHK_8.IsChecked = SAV.UnlockedSunsetColosseum;
        CHK_9.IsChecked = SAV.UnlockedCourtyardColosseum;
        CHK_10.IsChecked = SAV.UnlockedStargazerColosseum;
        CHK_PostGame.IsChecked = SAV.UnlockedPostGame;
    }

    #region Layout

    private void BuildLayout()
    {
        var profile = FourColumnGrid(5);
        AddRow(profile, 0, L_TrainerName, TB_OTName, L_BirthMonth, TB_BirthMonth);
        AddRow(profile, 1, L_Country, CB_Country, L_BirthDay, TB_BirthDay);
        AddRow(profile, 2, L_Region, CB_Region, null, null);
        AddSpanRow(profile, 3, L_SelfIntroduction, TB_SelfIntroduction);
        AddRow(profile, 4, L_PlayerID, MT_PlayerID, L_Language, CB_Language);

        var records = FourColumnGrid(9);
        AddSplitRow(records, 0, UiFactory.Row(L_Hours, MT_Hours, L_Minutes, MT_Minutes, L_Seconds, MT_Seconds), L_Coupons, UiFactory.Row(MT_Money, B_MaxCash));
        AddRow(records, 1, L_TID, MT_TID, L_SID, MT_SID);
        AddRow(records, 2, L_RecordTotalBattles, NUD_RecordTotalBattles, L_RecordColosseumBattles, NUD_RecordColosseumBattles);
        AddRow(records, 3, L_RecordFreeBattles, NUD_RecordFreeBattles, L_RecordWiFiBattles, NUD_RecordWiFiBattles);
        AddRow(records, 4, L_RecordGatewayColosseumClears, NUD_RecordGatewayColosseumClears, L_RecordMainStreetColosseumClears, NUD_RecordMainStreetColosseumClears);
        AddRow(records, 5, L_RecordWaterfallColosseumClears, NUD_RecordWaterfallColosseumClears, L_RecordNeonColosseumClears, NUD_RecordNeonColosseumClears);
        AddRow(records, 6, L_RecordCrystalColosseumClears, NUD_RecordCrystalColosseumClears, L_RecordSunnyParkColosseumClears, NUD_RecordSunnyParkColosseumClears);
        AddRow(records, 7, L_RecordMagmaColosseumClears, NUD_RecordMagmaColosseumClears, L_RecordSunsetColosseumClears, NUD_RecordSunsetColosseumClears);
        AddRow(records, 8, L_RecordCourtyardColosseumClears, NUD_RecordCourtyardColosseumClears, L_RecordStargazerColosseumClears, NUD_RecordStargazerColosseumClears);

        var flags = UiFactory.Row(CHK_1, CHK_2, CHK_3, CHK_4, CHK_5, CHK_6, CHK_7, CHK_8, CHK_9, CHK_10);
        flags.Spacing = 8;
        var colosseums = UiFactory.Row(flags, CHK_PostGame);
        colosseums.Spacing = 20;

        var body = UiFactory.Column(
            new GroupBoxView("GB_Profile", "Profile", profile),
            new GroupBoxView("GB_Records", "Records", records),
            new GroupBoxView("GB_Colosseums", "Colosseums", colosseums));
        body.Spacing = 8;
        SetBody(body);
    }

    private static Grid FourColumnGrid(int rows)
    {
        var g = new Grid { ColumnSpacing = 6, RowSpacing = 3 };
        for (int i = 0; i < 4; i++)
            g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (int i = 0; i < rows; i++)
            g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        return g;
    }

    private static void AddRow(Grid g, int row, Control label, Control field, Control? label2, Control? field2)
    {
        Place(g, label, row, 0, HorizontalAlignment.Right);
        Place(g, field, row, 1, HorizontalAlignment.Left);
        if (label2 is not null)
            Place(g, label2, row, 2, HorizontalAlignment.Right);
        if (field2 is not null)
            Place(g, field2, row, 3, HorizontalAlignment.Left);
    }

    /// <summary>Adds a row whose left cell spans the label and field columns.</summary>
    private static void AddSplitRow(Grid g, int row, Control left, Control? label2 = null, Control? field2 = null)
    {
        Place(g, left, row, 0, HorizontalAlignment.Left);
        Grid.SetColumnSpan(left, label2 is null ? 4 : 2);
        if (label2 is not null)
            Place(g, label2, row, 2, HorizontalAlignment.Right);
        if (field2 is not null)
            Place(g, field2, row, 3, HorizontalAlignment.Left);
    }

    private static void AddSpanRow(Grid g, int row, Control label, Control field)
    {
        Place(g, label, row, 0, HorizontalAlignment.Right);
        Place(g, field, row, 1, HorizontalAlignment.Left);
        Grid.SetColumnSpan(field, 3);
    }

    private static void Place(Grid g, Control c, int row, int col, HorizontalAlignment align)
    {
        c.HorizontalAlignment = align;
        UiFactory.SetRowCol(c, row, col);
        g.Children.Add(c);
    }

    #endregion

    #region Behavior

    private static void ChangeFFFF(TextBox box)
    {
        if ((box.Text ?? string.Empty).Length == 0) box.Text = "0";
        if (Util.ToInt32(box.Text ?? string.Empty) > 65535) box.Text = "65535";
    }

    private void ValidatePlayerID() => MT_PlayerID.Text = Util.GetHexValue64(MT_PlayerID.Text ?? string.Empty).ToString("X16");

    /// <summary>
    /// Truncates the self-introduction to the in-game character budget (port of <c>ValidateCatchphrase</c>).
    /// </summary>
    /// <remarks>Line breaks and the special glyphs cost two in-game characters each.</remarks>
    private void ValidateCatchphrase()
    {
        var text = TB_SelfIntroduction.Text ?? string.Empty;
        int max = TB_SelfIntroduction.MaxLength;
        int length = 0;
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r')
                continue; // half of a CRLF pair; the '\n' accounts for the break
            if (c == '\n')
            {
                length += 2;
                continue;
            }
            length += c switch
            {
                StringConverter4GC.LineBreak or
                    StringConverter4GC.Proportional or
                    StringConverter4GC.PokemonName => 2,
                _ => 1,
            };
            if (length > max)
            {
                TB_SelfIntroduction.Text = text[..i];
                return;
            }
        }
    }

    private void UpdateCountry()
    {
        int index = CB_Country.GetValue();
        CB_Region.SetCountrySubRegion($"gen4_sr_{index:000}");
        if (CB_Region.GetItemCount() == 0)
            CB_Region.SetCountrySubRegion("gen4_sr_default");
    }

    private void UpdateLanguage()
    {
        TB_SelfIntroduction.MaxLength = CB_Language.GetValue() != (int)LanguageID.Japanese ? 51 : 53;
        ValidateCatchphrase();
    }

    private static string JoinLines(string value) => value.Replace(StringConverter4GC.LineBreak, '\n');
    private static string SplitLines(string? value) => (value ?? string.Empty).Replace("\r\n", "\n").Replace('\n', StringConverter4GC.LineBreak);

    #endregion

    protected override void OnSave()
    {
        var ot = TB_OTName.Text ?? string.Empty;
        if (SAV.CurrentOT != ot) // only modify if changed (preserve trash bytes?)
            SAV.CurrentOT = ot;

        SAV.BirthMonth = TB_BirthMonth.Text ?? string.Empty;
        SAV.BirthDay = TB_BirthDay.Text ?? string.Empty;
        SAV.Country = CB_Country.GetValue();
        SAV.Region = CB_Region.GetValue();
        SAV.SelfIntroduction = (SAV.Japanese ? string.Empty : StringConverter4GC.Proportional.ToString()) + SplitLines(TB_SelfIntroduction.Text);
        SAV.PlayerID = Util.GetHexValue64(MT_PlayerID.Text ?? string.Empty);
        SAV.Language = CB_Language.GetValue();

        // The text boxes accept more digits than the fields hold; clamp instead of wrapping around.
        SAV.PlayedHours = (int)Math.Min(MT_Hours.UIntValue, ushort.MaxValue);
        SAV.PlayedMinutes = (int)(MT_Minutes.UIntValue % 60);
        SAV.PlayedSeconds = (int)(MT_Seconds.UIntValue % 60);
        SAV.Money = MT_Money.UIntValue;
        SAV.TID16 = (ushort)MT_TID.UIntValue;
        SAV.SID16 = (ushort)MT_SID.UIntValue;

        SAV.RecordTotalBattles = (uint)(NUD_RecordTotalBattles.Value ?? 0);
        SAV.RecordColosseumBattles = (uint)(NUD_RecordColosseumBattles.Value ?? 0);
        SAV.RecordFreeBattles = (uint)(NUD_RecordFreeBattles.Value ?? 0);
        SAV.RecordWiFiBattles = (uint)(NUD_RecordWiFiBattles.Value ?? 0);
        SAV.RecordGatewayColosseumClears = (byte)(NUD_RecordGatewayColosseumClears.Value ?? 0);
        SAV.RecordMainStreetColosseumClears = (byte)(NUD_RecordMainStreetColosseumClears.Value ?? 0);
        SAV.RecordWaterfallColosseumClears = (byte)(NUD_RecordWaterfallColosseumClears.Value ?? 0);
        SAV.RecordNeonColosseumClears = (byte)(NUD_RecordNeonColosseumClears.Value ?? 0);
        SAV.RecordCrystalColosseumClears = (byte)(NUD_RecordCrystalColosseumClears.Value ?? 0);
        SAV.RecordSunnyParkColosseumClears = (byte)(NUD_RecordSunnyParkColosseumClears.Value ?? 0);
        SAV.RecordMagmaColosseumClears = (byte)(NUD_RecordMagmaColosseumClears.Value ?? 0);
        SAV.RecordCourtyardColosseumClears = (byte)(NUD_RecordCourtyardColosseumClears.Value ?? 0);
        SAV.RecordSunsetColosseumClears = (byte)(NUD_RecordSunsetColosseumClears.Value ?? 0);
        SAV.RecordStargazerColosseumClears = (byte)(NUD_RecordStargazerColosseumClears.Value ?? 0);

        SAV.UnlockedGatewayColosseum = CHK_1.IsChecked == true;
        SAV.UnlockedMainStreetColosseum = CHK_2.IsChecked == true;
        SAV.UnlockedWaterfallColosseum = CHK_3.IsChecked == true;
        SAV.UnlockedNeonColosseum = CHK_4.IsChecked == true;
        SAV.UnlockedCrystalColosseum = CHK_5.IsChecked == true;
        SAV.UnlockedSunnyParkColosseum = CHK_6.IsChecked == true;
        SAV.UnlockedMagmaColosseum = CHK_7.IsChecked == true;
        SAV.UnlockedSunsetColosseum = CHK_8.IsChecked == true;
        SAV.UnlockedCourtyardColosseum = CHK_9.IsChecked == true;
        SAV.UnlockedStargazerColosseum = CHK_10.IsChecked == true;
        SAV.UnlockedPostGame = CHK_PostGame.IsChecked == true;

        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
