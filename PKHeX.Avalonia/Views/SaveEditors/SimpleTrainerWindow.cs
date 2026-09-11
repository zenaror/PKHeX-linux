using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors;

/// <summary>
/// Trainer info editor for Generations 1–5 and GameCube saves (port of the WinForms <c>SAV_SimpleTrainer</c>).
/// </summary>
public sealed class SimpleTrainerWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SaveFile SAV;

    // Trainer
    private readonly TextBlock L_TrainerName = UiFactory.Label("L_TrainerName", "Name:");
    private readonly TextBox TB_OTName = UiFactory.Text("TB_OTName", 12, 140);
    private readonly ComboBox CB_Gender = UiFactory.StringCombo("CB_Gender", 60);
    private readonly TextBlock L_TID = UiFactory.Label("L_TID", "TID16:");
    private readonly NumericTextBox MT_TID = UiFactory.Numeric("MT_TID", 5, 64);
    private readonly TextBlock L_SID = UiFactory.Label("L_SID", "SID16:");
    private readonly NumericTextBox MT_SID = UiFactory.Numeric("MT_SID", 5, 64);
    private readonly TextBlock L_Money = UiFactory.Label("L_Money", "$:");
    private readonly NumericTextBox MT_Money = UiFactory.Numeric("MT_Money", 7, 90);
    private readonly Button B_MaxCash = UiFactory.Button("B_MaxCash", "+");
    private readonly TextBlock L_Coins = UiFactory.Label("L_Coins", "Coins:");
    private readonly NumericTextBox MT_Coins = UiFactory.Numeric("MT_Coins", 5, 70);
    private readonly Button B_MaxCoins = UiFactory.Button("B_MaxCoins", "+");
    private readonly TextBlock L_Country = UiFactory.Label("L_Country", "Country:");
    private readonly ComboBox CB_Country = UiFactory.Combo("CB_Country", 160);
    private readonly TextBlock L_Region = UiFactory.Label("L_Region", "Sub Region:");
    private readonly ComboBox CB_Region = UiFactory.Combo("CB_Region", 160);

    // Adventure
    private readonly TextBlock L_Started = UiFactory.Label("L_Started", "Game Started:");
    private readonly DatePicker CAL_AdventureStartDate = new() { Name = "CAL_AdventureStartDate", MinWidth = 0, Width = 230 };
    private readonly TimePicker CAL_AdventureStartTime = new() { Name = "CAL_AdventureStartTime", ClockIdentifier = "24HourClock", UseSeconds = true };
    private readonly TextBlock L_Fame = UiFactory.Label("L_Fame", "Hall of Fame:");
    private readonly DatePicker CAL_HoFDate = new() { Name = "CAL_HoFDate", MinWidth = 0, Width = 230 };
    private readonly TimePicker CAL_HoFTime = new() { Name = "CAL_HoFTime", ClockIdentifier = "24HourClock", UseSeconds = true };
    private readonly TextBlock L_Hours = UiFactory.Label("L_Hours", "Hrs:");
    private readonly NumericTextBox MT_Hours = UiFactory.Numeric("MT_Hours", 5, 60);
    private readonly TextBlock L_Minutes = UiFactory.Label("L_Minutes", "Min:");
    private readonly NumericTextBox MT_Minutes = UiFactory.Numeric("MT_Minutes", 2, 40);
    private readonly TextBlock L_Seconds = UiFactory.Label("L_Seconds", "Sec:");
    private readonly NumericTextBox MT_Seconds = UiFactory.Numeric("MT_Seconds", 2, 40);
    private readonly TextBlock L_PikaFriend = UiFactory.Label("L_PikaFriend", "Pikachu Friendship:");
    private readonly NumericTextBox MT_PikaFriend = UiFactory.Numeric("MT_PikaFriend", 3, 50);
    private readonly TextBlock L_PikaBeach = UiFactory.Label("L_PikaBeach", "Pikachu Beach:");
    private readonly NumericTextBox MT_PikaBeach = UiFactory.Numeric("MT_PikaBeach", 4, 60);

    // Map
    private readonly GroupBoxView GB_Map;
    private readonly TextBlock L_CurrentMap = UiFactory.Label("L_CurrentMap", "Current Map:");
    private readonly NumericUpDown NUD_M = UiFactory.NumericUpDown("NUD_M", 0, 1000, 130);
    private readonly TextBlock L_X = UiFactory.Label("L_X", "X Coordinate:");
    private readonly NumericUpDown NUD_X = UiFactory.NumericUpDown("NUD_X", 0, 65535, 130);
    private readonly TextBlock L_Z = UiFactory.Label("L_Z", "Z Coordinate:");
    private readonly NumericUpDown NUD_Z = UiFactory.NumericUpDown("NUD_Z", -65535, 65535, 130);
    private readonly TextBlock L_Y = UiFactory.Label("L_Y", "Y Coordinate:");
    private readonly NumericUpDown NUD_Y = UiFactory.NumericUpDown("NUD_Y", 0, 65535, 130);

    // Badges
    private readonly GroupBoxView GB_Badges;
    private readonly CheckBox[] AllBadges;
    private CheckBox[] cba;

    // Options
    private readonly GroupBoxView GB_Options;
    private readonly TextBlock LBL_TextSpeed = UiFactory.Label("LBL_TextSpeed", "Text Speed:");
    private readonly ComboBox CB_TextSpeed = UiFactory.StringCombo("CB_TextSpeed", 120);
    private readonly TextBlock LBL_SoundType = UiFactory.Label("LBL_SoundType", "Sound Type:");
    private readonly ComboBox CB_SoundType = UiFactory.StringCombo("CB_SoundType", 120);
    private readonly TextBlock LBL_BattleStyle = UiFactory.Label("LBL_BattleStyle", "Battle Style:");
    private readonly ComboBox CB_BattleStyle = UiFactory.StringCombo("CB_BattleStyle", 120);
    private readonly CheckBox CHK_BattleEffects = UiFactory.Check("CHK_BattleEffects", "Use Battle Effects");
    private readonly GroupBoxView GB_Adventure;

    private readonly bool Loading;
    private bool MapUpdated;

    public SimpleTrainerWindow(SaveFile sav) : base("SAV_SimpleTrainer", "Trainer Data Editor")
    {
        SAV = (Origin = sav).Clone();
        Loading = true;

        AllBadges = new CheckBox[16];
        for (int i = 0; i < 8; i++)
        {
            AllBadges[i] = UiFactory.Check($"CHK_{i + 1}", string.Empty);
            AllBadges[i + 8] = UiFactory.Check($"CHK_H{i + 1}", string.Empty);
        }
        cba = AllBadges[..8];

        // Layout
        var trainer = UiFactory.FormGrid(7);
        UiFactory.AddFormRow(trainer, 0, L_TrainerName, UiFactory.Row(TB_OTName, CB_Gender));
        UiFactory.AddFormRow(trainer, 1, L_TID, MT_TID);
        UiFactory.AddFormRow(trainer, 2, L_SID, MT_SID);
        UiFactory.AddFormRow(trainer, 3, L_Money, UiFactory.Row(MT_Money, B_MaxCash));
        UiFactory.AddFormRow(trainer, 4, L_Coins, UiFactory.Row(MT_Coins, B_MaxCoins));
        UiFactory.AddFormRow(trainer, 5, L_Country, CB_Country);
        UiFactory.AddFormRow(trainer, 6, L_Region, CB_Region);
        var GB_Trainer = new GroupBoxView("GB_Trainer", "Trainer", trainer);

        var adventure = UiFactory.FormGrid(5);
        UiFactory.AddFormRow(adventure, 0, null, UiFactory.Row(L_Hours, MT_Hours, L_Minutes, MT_Minutes, L_Seconds, MT_Seconds));
        UiFactory.AddFormRow(adventure, 1, L_Started, UiFactory.Column(CAL_AdventureStartDate, CAL_AdventureStartTime));
        UiFactory.AddFormRow(adventure, 2, L_Fame, UiFactory.Column(CAL_HoFDate, CAL_HoFTime));
        UiFactory.AddFormRow(adventure, 3, L_PikaFriend, MT_PikaFriend);
        UiFactory.AddFormRow(adventure, 4, L_PikaBeach, MT_PikaBeach);
        GB_Adventure = new GroupBoxView("GB_Adventure", "Adventure Info", adventure);

        var map = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(map, 0, L_CurrentMap, NUD_M);
        UiFactory.AddFormRow(map, 1, L_X, NUD_X);
        UiFactory.AddFormRow(map, 2, L_Z, NUD_Z);
        UiFactory.AddFormRow(map, 3, L_Y, NUD_Y);
        GB_Map = new GroupBoxView("GB_Map", "Map Position", map);

        var badges = new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = 8 * 28 };
        foreach (var chk in AllBadges)
        {
            chk.IsVisible = false;
            chk.Margin = new global::Avalonia.Thickness(2);
            badges.Children.Add(chk);
        }
        GB_Badges = new GroupBoxView("GB_Badges", "Badges", badges);

        var options = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(options, 0, LBL_TextSpeed, CB_TextSpeed);
        UiFactory.AddFormRow(options, 1, LBL_SoundType, CB_SoundType);
        UiFactory.AddFormRow(options, 2, LBL_BattleStyle, CB_BattleStyle);
        UiFactory.AddFormRow(options, 3, null, CHK_BattleEffects);
        GB_Options = new GroupBoxView("GB_Options", "Options", options);
        GB_Options.IsVisible = false;

        var left = UiFactory.Column(GB_Trainer, GB_Badges, GB_Options);
        var right = UiFactory.Column(GB_Adventure, GB_Map);
        var body = UiFactory.Row(left, right);
        body.Spacing = 10;
        foreach (var c in new Control[] { left, right })
            c.VerticalAlignment = VerticalAlignment.Top;
        SetBody(body); // translate now; generation-specific text (e.g. "BP") is applied afterwards

        // Behavior
        TB_OTName.MaxLength = SAV.MaxStringLengthTrainer;
        B_MaxCash.Click += (_, _) => MT_Money.Text = SAV.MaxMoney.ToString();
        B_MaxCoins.Click += (_, _) => MT_Coins.Text = SAV.MaxCoins.ToString();
        MT_Money.MaxLength = (int)Math.Floor(Math.Log10(SAV.MaxMoney) + 1);
        MT_Coins.MaxLength = (int)Math.Floor(Math.Log10(SAV.MaxCoins) + 1);
        MT_TID.OnTextChanged(ChangeFFFF);
        MT_SID.OnTextChanged(ChangeFFFF);
        MT_PikaFriend.OnTextChanged(Change255);
        foreach (var nud in new[] { NUD_M, NUD_X, NUD_Y, NUD_Z })
            nud.ValueChanged += (_, _) => { if (!Loading) MapUpdated = true; };
        CB_Country.SelectionChanged += (_, _) => UpdateCountry();

        foreach (var g in MainWindow.GenderSymbols.Take(2)) // m/f depending on unicode selection
            CB_Gender.Items.Add(g);

        L_SID.IsVisible = MT_SID.IsVisible = SAV.Generation > 2;
        L_Coins.IsVisible = B_MaxCoins.IsVisible = MT_Coins.IsVisible = SAV.Generation < 3;
        CB_Gender.IsVisible = SAV.Generation > 1;
        L_Country.IsVisible = L_Region.IsVisible = CB_Country.IsVisible = CB_Region.IsVisible = SAV.Generation > 3;

        L_PikaFriend.IsVisible = MT_PikaFriend.IsVisible = L_PikaBeach.IsVisible = MT_PikaBeach.IsVisible = SAV.Generation == 1;

        TB_OTName.Text = SAV.OT;
        CB_Gender.SelectedIndex = SAV.Gender;
        MT_TID.Text = SAV.TID16.ToString("00000");
        MT_SID.Text = SAV.SID16.ToString("00000");
        MT_Money.Text = SAV.Money.ToString();
        MT_Hours.Text = SAV.PlayedHours.ToString();
        MT_Minutes.Text = SAV.PlayedMinutes.ToString();
        MT_Seconds.Text = SAV.PlayedSeconds.ToString();

        int badgeval = 0;
        if (SAV is SAV1 sav1)
        {
            MT_Coins.Text = sav1.Coin.ToString();
            badgeval = sav1.Badges;

            HideDates();
            GB_Map.IsVisible = false;
            GB_Options.IsVisible = true;
            AddItems(CB_BattleStyle, "Shift", "Set");
            AddItems(CB_SoundType, "Mono", "Stereo", "Left", "Right");
            AddItems(CB_TextSpeed, "0 (Instant)", "1 (Fast)", "2", "3 (Normal)", "4", "5 (Slow)", "6", "7");

            CHK_BattleEffects.IsChecked = sav1.BattleEffects;
            CB_BattleStyle.SelectedIndex = sav1.BattleStyleSwitch ? 0 : 1;
            CB_SoundType.SelectedIndex = sav1.Sound;
            CB_TextSpeed.SelectedIndex = sav1.TextSpeed;

            MT_PikaFriend.Text = sav1.PikaFriendship.ToString();
            MT_PikaBeach.Text = sav1.PikaBeachScore.ToString();
            if (!sav1.Version.Contains(GameVersion.YW))
            {
                L_PikaFriend.IsVisible = MT_PikaFriend.IsVisible = false;
                L_PikaBeach.IsVisible = MT_PikaBeach.IsVisible = false;
                CB_SoundType.IsVisible = LBL_SoundType.IsVisible = false;
            }
        }

        if (SAV is SAV2 sav2)
        {
            MT_Coins.Text = sav2.Coin.ToString();

            HideDates();
            GB_Map.IsVisible = false;
            GB_Options.IsVisible = true;
            AddItems(CB_BattleStyle, "Shift", "Set");
            AddItems(CB_SoundType, "Mono", "Stereo");
            AddItems(CB_TextSpeed, "0 (Instant)", "1 (Fast)", "2", "3 (Normal)", "4", "5 (Slow)", "6", "7");

            CHK_BattleEffects.IsChecked = sav2.BattleEffects;
            CB_BattleStyle.SelectedIndex = sav2.BattleStyleSwitch ? 0 : 1;
            CB_SoundType.SelectedIndex = sav2.Sound > 0 ? 1 : 0;
            CB_TextSpeed.SelectedIndex = sav2.TextSpeed;
            badgeval = sav2.Badges;
            var b = AllBadges;
            cba = [b[0], b[1], b[2], b[3], b[5], b[4], b[6], b[7], b[8], b[9], b[10], b[11], b[12], b[13], b[14], b[15]];
        }

        if (SAV is SAV3 sav3)
        {
            var small = sav3.SmallBlock;
            GB_Map.IsVisible = false;
            badgeval = sav3.Badges;

            HideDates();

            GB_Options.IsVisible = true;
            AddItems(CB_BattleStyle, "Shift", "Set");
            AddItems(CB_SoundType, "Mono", "Stereo");
            AddItems(CB_TextSpeed, "0 (Slow)", "1 (Mid)", "2 (Fast)", "3", "4", "5", "6", "7");

            CB_TextSpeed.SelectedIndex = small.TextSpeed;
            CB_BattleStyle.SelectedIndex = small.OptionBattleStyle ? 1 : 0;
            CB_SoundType.SelectedIndex = small.OptionSound ? 1 : 0;
            CHK_BattleEffects.IsChecked = !small.OptionBattleScene;
        }
        if (SAV is SAV3Colosseum or SAV3XD)
        {
            GB_Map.IsVisible = false;
            GB_Badges.IsVisible = false;
            HideDates();
            GB_Adventure.IsVisible = false;
            Loading = false;
            return;
        }

        if (SAV is SAV4 sav4)
        {
            NUD_M.Value = sav4.M;
            NUD_X.Value = sav4.X;
            NUD_Z.Value = sav4.Z;
            NUD_Y.Value = sav4.Y;

            badgeval = sav4.Badges;
            if (sav4 is SAV4HGSS hgss)
            {
                badgeval |= hgss.Badges16 << 8;
                cba = AllBadges;
            }

            CB_Country.SetCountrySubRegion("gen4_countries");
            CB_Country.SetValue(sav4.Country);
            CB_Region.SetValue(sav4.Region);
        }
        else if (SAV is SAV5 s)
        {
            L_Coins.IsVisible = B_MaxCoins.IsVisible = MT_Coins.IsVisible = true;
            L_Coins.Text = "BP"; // no translation boo
            MT_Coins.Text = s.BattleSubway.BP.ToString();

            var pd = s.PlayerPosition;
            NUD_M.Value = pd.M;
            NUD_X.Value = pd.X;
            NUD_Z.Value = pd.Z;
            NUD_Y.Value = pd.Y;
            badgeval = s.Misc.Badges;

            CB_Country.SetCountrySubRegion("gen5_countries");
            CB_Country.SetValue(s.Country);
            CB_Region.SetValue(s.Region);
        }

        for (int i = 0; i < cba.Length; i++)
        {
            cba[i].IsVisible = true;
            cba[i].IsChecked = (badgeval & (1 << i)) != 0;
        }

        DateUtil.GetDateTime2000(SAV.SecondsToStart, out var date, out var time);
        SetDate(CAL_AdventureStartDate, date);
        CAL_AdventureStartTime.SelectedTime = time.TimeOfDay;

        DateUtil.GetDateTime2000(SAV.SecondsToFame, out date, out time);
        SetDate(CAL_HoFDate, date);
        CAL_HoFTime.SelectedTime = time.TimeOfDay;
        Loading = false;
    }

    private void HideDates()
    {
        L_Started.IsVisible = L_Fame.IsVisible = false;
        CAL_AdventureStartDate.IsVisible = CAL_HoFDate.IsVisible = false;
        CAL_AdventureStartTime.IsVisible = CAL_HoFTime.IsVisible = false;
    }

    private static void AddItems(ComboBox cb, params string[] items)
    {
        foreach (var item in items)
            cb.Items.Add(item);
    }

    private static void SetDate(DatePicker picker, DateTime value)
    {
        if (value < picker.MinYear.DateTime)
            value = picker.MinYear.DateTime;
        picker.SelectedDate = UiFactory.ToOffset(value);
    }

    private static DateTime GetDate(DatePicker picker) => picker.SelectedDate?.DateTime ?? new DateTime(2000, 1, 1);
    private static DateTime GetTime(TimePicker picker) => new DateTime(2000, 1, 1) + (picker.SelectedTime ?? TimeSpan.Zero);

    private static void ChangeFFFF(TextBox box)
    {
        if ((box.Text ?? string.Empty).Length == 0) box.Text = "0";
        if (Util.ToInt32(box.Text ?? string.Empty) > 65535) box.Text = "65535";
    }

    private static void Change255(TextBox box)
    {
        if ((box.Text ?? string.Empty).Length == 0) box.Text = "0";
        if (Util.ToInt32(box.Text ?? string.Empty) > byte.MaxValue) box.Text = "255";
    }

    protected override void OnSave()
    {
        var ot = TB_OTName.Text ?? string.Empty;
        if (SAV.OT != ot) // only modify if changed (preserve trash bytes?)
            SAV.OT = ot;
        SAV.Gender = (byte)Math.Max(0, CB_Gender.SelectedIndex);

        SAV.TID16 = (ushort)MT_TID.UIntValue;
        SAV.SID16 = (ushort)MT_SID.UIntValue;
        SAV.Money = MT_Money.UIntValue;

        SAV.PlayedHours = (ushort)MT_Hours.UIntValue;
        SAV.PlayedMinutes = (ushort)(MT_Minutes.UIntValue % 60);
        SAV.PlayedSeconds = (ushort)(MT_Seconds.UIntValue % 60);

        // Copy Badges
        int badgeval = 0;
        for (int i = 0; i < cba.Length; i++)
            badgeval |= (cba[i].IsChecked == true ? 1 : 0) << i;

        if (SAV is SAV1 sav1)
        {
            sav1.Coin = (ushort)Math.Min(MT_Coins.UIntValue, SAV.MaxCoins);
            sav1.Badges = badgeval & 0xFF;
            sav1.PikaFriendship = (byte)Math.Min(255, MT_PikaFriend.UIntValue);
            sav1.PikaBeachScore = (ushort)Math.Min(9999, MT_PikaBeach.UIntValue);
            sav1.BattleEffects = CHK_BattleEffects.IsChecked == true;
            sav1.BattleStyleSwitch = CB_BattleStyle.SelectedIndex == 0;
            sav1.Sound = Math.Max(0, CB_SoundType.SelectedIndex);
            sav1.TextSpeed = Math.Max(0, CB_TextSpeed.SelectedIndex);
        }

        if (SAV is SAV2 sav2)
        {
            sav2.Coin = (ushort)Math.Min(MT_Coins.UIntValue, SAV.MaxCoins);
            sav2.Badges = badgeval & 0xFFFF;

            sav2.BattleEffects = CHK_BattleEffects.IsChecked == true;
            sav2.BattleStyleSwitch = CB_BattleStyle.SelectedIndex == 0;
            sav2.Sound = CB_SoundType.SelectedIndex > 0 ? 2 : 0;
            sav2.TextSpeed = Math.Max(0, CB_TextSpeed.SelectedIndex);
        }

        if (SAV is SAV3 sav3)
        {
            var small = sav3.SmallBlock;
            sav3.Badges = badgeval & 0xFF;
            small.OptionBattleStyle = CB_BattleStyle.SelectedIndex == 1;
            small.OptionSound = CB_SoundType.SelectedIndex == 1;
            small.TextSpeed = Math.Max(0, CB_TextSpeed.SelectedIndex);
            small.OptionBattleScene = CHK_BattleEffects.IsChecked != true;
        }

        if (SAV is SAV4 sav4)
        {
            if (MapUpdated)
            {
                sav4.M = (int)(NUD_M.Value ?? 0);
                sav4.X = (int)(NUD_X.Value ?? 0);
                sav4.Z = (int)(NUD_Z.Value ?? 0);
                sav4.Y = (int)(NUD_Y.Value ?? 0);
            }
            sav4.Badges = (byte)badgeval;
            if (sav4 is SAV4HGSS hgss)
            {
                hgss.Badges16 = badgeval >> 8;
            }
            sav4.Country = CB_Country.GetValue();
            sav4.Region = CB_Region.GetValue();
        }
        else if (SAV is SAV5 s)
        {
            if (MapUpdated)
            {
                var pd = s.PlayerPosition;
                pd.M = (int)(NUD_M.Value ?? 0);
                pd.X = (int)(NUD_X.Value ?? 0);
                pd.Z = (int)(NUD_Z.Value ?? 0);
                pd.Y = (int)(NUD_Y.Value ?? 0);
            }
            s.Misc.Badges = badgeval & 0xFF;
            s.BattleSubway.BP = (ushort)Math.Min(MT_Coins.UIntValue, SAV.MaxCoins);
            s.Country = CB_Country.GetValue();
            s.Region = CB_Region.GetValue();
        }

        if (CAL_AdventureStartDate.IsVisible)
        {
            SAV.SecondsToStart = (uint)DateUtil.GetSecondsFrom2000(GetDate(CAL_AdventureStartDate), GetTime(CAL_AdventureStartTime));
            SAV.SecondsToFame = (uint)DateUtil.GetSecondsFrom2000(GetDate(CAL_HoFDate), GetTime(CAL_HoFTime));
        }

        Origin.CopyChangesFrom(SAV);
        Close();
    }

    private void UpdateCountry()
    {
        int index = CB_Country.GetValue();
        if (SAV is SAV4)
        {
            CB_Region.SetCountrySubRegion($"gen4_sr_{index:000}");
            if (CB_Region.GetItemCount() == 0)
                CB_Region.SetCountrySubRegion("gen4_sr_default");
        }
        else if (SAV is SAV5)
        {
            CB_Region.SetCountrySubRegion($"gen5_sr_{index:000}");
            if (CB_Region.GetItemCount() == 0)
                CB_Region.SetCountrySubRegion("gen5_sr_default");
        }
    }
}
