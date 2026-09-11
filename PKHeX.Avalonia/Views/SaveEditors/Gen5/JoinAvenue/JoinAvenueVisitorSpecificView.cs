using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen5.JoinAvenue;

/// <summary>
/// Visitor-only fields (port of <c>JoinAvenueVisitorSpecificEditor</c>).
/// </summary>
/// <remarks>
/// The repeated arrays (shop counts, records, trivia, activities and their dates) are edited as
/// comma-separated text, exactly as the WinForms editor does, rather than as grids.
/// </remarks>
public sealed class JoinAvenueVisitorSpecificView : StackPanel, IJoinAvenueSpecificView<JoinAvenueVisitor5>
{
    private static readonly List<ComboItem> ShopTypeList = [new("None", -1), .. Util.GetCBList<JoinAvenueShopType5>()];
    private static readonly List<ComboItem> OriginList = [new("NPC", 0), new("Human Player", 1)];

    private readonly CheckBox CHK_IsFlag2C = UiFactory.Check("CHK_IsFlag2C", "0x2C flag");
    private readonly NumericUpDown NUD_AvenueLevel = Byte("NUD_AvenueLevel");
    private readonly NumericUpDown NUD_Unused2D = Byte("NUD_Unused2D");
    private readonly TextBox TB_ShopCounts = Text("TB_ShopCounts");
    private readonly NumericUpDown NUD_DexSeen = Word("NUD_DexSeen");
    private readonly ComboBox CB_FavoriteSpecies = UiFactory.Combo("CB_FavoriteSpecies", 180);
    private readonly NumericUpDown NUD_MedalRank = Byte("NUD_MedalRank");
    private readonly NumericUpDown NUD_MedalHint = Byte("NUD_MedalHint");
    private readonly NumericUpDown NUD_MedalCount = Byte("NUD_MedalCount");
    private readonly TextBox TB_Date1 = Text("TB_Date1");
    private readonly TextBox TB_DateStart = Text("TB_DateStart");
    private readonly TextBox TB_DateHall = Text("TB_DateHall");
    private readonly TextBox TB_Records = Text("TB_Records");
    private readonly TextBox TB_Trivia = Text("TB_Trivia");
    private readonly TextBox TB_Activities = Text("TB_Activities");
    private readonly TextBox TB_ActivityDates = Text("TB_ActivityDates");
    private readonly ComboBox CB_Origin = UiFactory.Combo("CB_Origin", 160);
    private readonly NumericUpDown NUD_MetHour = Byte("NUD_MetHour");
    private readonly NumericUpDown NUD_MetMinute = Byte("NUD_MetMinute");
    private readonly NumericUpDown NUD_UnknownA8 = Byte("NUD_UnknownA8");
    private readonly CheckBox CHK_IsShopChangeAllowed = UiFactory.Check("CHK_IsShopChangeAllowed", "Shop Change Allowed");
    private readonly CheckBox CHK_IsFlagA9_1 = UiFactory.Check("CHK_IsFlagA9_1", "0xA9.1");
    private readonly CheckBox CHK_IsFlagA9_2 = UiFactory.Check("CHK_IsFlagA9_2", "0xA9.2");
    private readonly CheckBox CHK_InteractedToday = UiFactory.Check("CHK_InteractedToday", "Interacted Today");
    private readonly CheckBox CHK_IsFlagAA = UiFactory.Check("CHK_IsFlagAA", "0xAA");
    private readonly NumericUpDown NUD_JoinAvenueRank = Byte("NUD_JoinAvenueRank");
    private readonly NumericUpDown NUD_UnknownAC = Byte("NUD_UnknownAC");
    private readonly NumericUpDown NUD_ShopRank = Byte("NUD_ShopRank");
    private readonly NumericUpDown NUD_ShopExperience = Word("NUD_ShopExperience");
    private readonly NumericUpDown NUD_IsInventory = Dword("NUD_IsInventory");
    private readonly NumericUpDown NUD_ShopWork = Word("NUD_ShopWork");
    private readonly NumericUpDown NUD_UnusedB8 = Dword("NUD_UnusedB8");
    private readonly NumericUpDown NUD_UnknownBits0_8 = Word("NUD_UnknownBits0_8");
    private readonly CheckBox CHK_UnknownBit9 = UiFactory.Check("CHK_UnknownBit9", "bit 9");
    private readonly NumericUpDown NUD_UnknownBits10 = Byte("NUD_UnknownBits10");
    private readonly NumericUpDown NUD_UnknownBits13_20 = Byte("NUD_UnknownBits13_20");
    private readonly NumericUpDown NUD_UnknownBits21_27 = Byte("NUD_UnknownBits21_27");
    private readonly NumericUpDown NUD_UnknownBits28_31 = Byte("NUD_UnknownBits28_31");

    private readonly ComboBox CB_DesiredShopType = UiFactory.Combo("CB_DesiredShopType", 170);
    private readonly NumericUpDown NUD_DesiredShopLevel = Byte("NUD_DesiredShopLevel");
    private readonly NumericUpDown NUD_DesiredShopVersion = Byte("NUD_DesiredShopVersion");
    private readonly ComboBox CB_ShopType = UiFactory.Combo("CB_ShopType", 170);
    private readonly NumericUpDown NUD_ShopTypeLevel = Byte("NUD_ShopTypeLevel");
    private readonly NumericUpDown NUD_ShopTypeVersion = Byte("NUD_ShopTypeVersion");

    private static NumericUpDown Byte(string name) => UiFactory.NumericUpDown(name, 0, byte.MaxValue, 110);
    private static NumericUpDown Word(string name) => UiFactory.NumericUpDown(name, 0, ushort.MaxValue, 120);
    private static NumericUpDown Dword(string name) => UiFactory.NumericUpDown(name, 0, uint.MaxValue, 140);
    private static TextBox Text(string name) => UiFactory.Text(name, 200, 320);

    public JoinAvenueVisitorSpecificView()
    {
        Orientation = Orientation.Vertical;
        Spacing = 2;

        CB_DesiredShopType.SetItems(ShopTypeList);
        CB_FavoriteSpecies.SetItems([.. GameInfo.FilteredSources.Species]);
        CB_Origin.SetItems(OriginList);
        CB_ShopType.SetItems(ShopTypeList);

        var grid = UiFactory.FormGrid(34);
        int r = 0;
        Row(grid, r++, "L_IsFlag2C", string.Empty, CHK_IsFlag2C);
        Row(grid, r++, "L_AvenueLevel", "Avenue Level:", NUD_AvenueLevel);
        Row(grid, r++, "L_Unused2D", "0x2D:", NUD_Unused2D);
        Row(grid, r++, "L_ShopCounts", "Shop Counts:", TB_ShopCounts);
        Row(grid, r++, "L_DexSeen", "Dex Seen:", NUD_DexSeen);
        Row(grid, r++, "L_FavoriteSpecies", "Favorite Species:", CB_FavoriteSpecies);
        Row(grid, r++, "L_MedalRank", "Medal Rank:", NUD_MedalRank);
        Row(grid, r++, "L_MedalHint", "Medal Hint:", NUD_MedalHint);
        Row(grid, r++, "L_MedalCount", "Medal Count:", NUD_MedalCount);
        Row(grid, r++, "L_Date1", "Date:", TB_Date1);
        Row(grid, r++, "L_DateStart", "Adventure Start:", TB_DateStart);
        Row(grid, r++, "L_DateHall", "Hall of Fame:", TB_DateHall);
        Row(grid, r++, "L_Records", "Records:", TB_Records);
        Row(grid, r++, "L_Trivia", "Trivia:", TB_Trivia);
        Row(grid, r++, "L_Activities", "Activities:", TB_Activities);
        Row(grid, r++, "L_ActivityDates", "Activity Dates:", TB_ActivityDates);
        Row(grid, r++, "L_Origin", "Origin:", CB_Origin);
        Row(grid, r++, "L_MetHour", "Met Hour:", NUD_MetHour);
        Row(grid, r++, "L_MetMinute", "Met Minute:", NUD_MetMinute);
        Row(grid, r++, "L_UnknownA8", "0xA8:", NUD_UnknownA8);
        Row(grid, r++, "L_Flags", string.Empty, UiFactory.Row(CHK_IsShopChangeAllowed, CHK_IsFlagA9_1, CHK_IsFlagA9_2));
        Row(grid, r++, "L_Flags2", string.Empty, UiFactory.Row(CHK_InteractedToday, CHK_IsFlagAA));
        Row(grid, r++, "L_JoinAvenueRank", "Avenue Rank:", NUD_JoinAvenueRank);
        Row(grid, r++, "L_UnknownAC", "0xAC:", NUD_UnknownAC);
        Row(grid, r++, "L_ShopRank", "Shop Rank:", NUD_ShopRank);
        Row(grid, r++, "L_ShopExperience", "Shop Experience:", NUD_ShopExperience);
        Row(grid, r++, "L_IsInventory", "Inventory:", NUD_IsInventory);
        Row(grid, r++, "L_ShopWork", "Shop Work:", NUD_ShopWork);
        Row(grid, r++, "L_UnusedB8", "0xB8:", NUD_UnusedB8);
        Row(grid, r++, "L_UnknownBits0_8", "bits 0-8:", UiFactory.Row(NUD_UnknownBits0_8, CHK_UnknownBit9));
        Row(grid, r++, "L_UnknownBits10", "bits 10-12:", NUD_UnknownBits10);
        Row(grid, r++, "L_UnknownBits13_20", "bits 13-20:", NUD_UnknownBits13_20);
        Row(grid, r++, "L_UnknownBits21_27", "bits 21-27:", NUD_UnknownBits21_27);
        Row(grid, r, "L_UnknownBits28_31", "bits 28-31:", NUD_UnknownBits28_31);
        Children.Add(grid);

        Children.Add(new GroupBoxView("GB_DesiredShop", "Desired Shop",
            UiFactory.Row(CB_DesiredShopType, NUD_DesiredShopLevel, NUD_DesiredShopVersion)));
        Children.Add(new GroupBoxView("GB_Shop", "Shop",
            UiFactory.Row(CB_ShopType, NUD_ShopTypeLevel, NUD_ShopTypeVersion)));
        return;

        static void Row(Grid g, int row, string name, string text, Control editor)
            => UiFactory.AddFormRow(g, row, UiFactory.Label(name, text), editor);
    }

    public void LoadObject(JoinAvenueVisitor5 entity)
    {
        CHK_IsFlag2C.IsChecked = entity.IsFlag2C;
        NUD_AvenueLevel.SetValueClamped(entity.JoinAvenueLevel);
        NUD_Unused2D.SetValueClamped(entity.Unused2D);

        byte[] counts =
        [
            entity.ShopCountRaffle, entity.ShopCountSalon, entity.ShopCountMarket, entity.ShopCountFlorist,
            entity.ShopCountDojo, entity.ShopCountNurse, entity.ShopCountAntique, entity.ShopCountCafe,
        ];
        TB_ShopCounts.Text = string.Join(", ", counts);
        NUD_DexSeen.SetValueClamped(entity.DexSeen);
        CB_FavoriteSpecies.SetValue(entity.FavoriteSpecies);
        NUD_MedalRank.SetValueClamped(entity.MedalRank);
        NUD_MedalHint.SetValueClamped(entity.MedalHint);
        NUD_MedalCount.SetValueClamped(entity.MedalCount);
        TB_Date1.Text = FormatDate(entity.Date1.RawValue);
        TB_DateStart.Text = FormatDate(entity.DateAdventureStart.RawValue);
        TB_DateHall.Text = FormatDate(entity.DateHallOfFame.RawValue);

        var records = new uint[(int)JoinAvenueRecordIndex5.COUNT_MAX];
        for (int i = 0; i < records.Length; i++)
            records[i] = entity.GetRecord((JoinAvenueRecordIndex5)i);
        TB_Records.Text = string.Join(", ", records);

        var trivia = new byte[JoinAvenueVisitor5.TriviaCount];
        for (int i = 0; i < trivia.Length; i++)
            trivia[i] = entity.GetTrivia(i);
        TB_Trivia.Text = string.Join(", ", trivia);

        var activities = new byte[JoinAvenueVisitor5.ActivityCount];
        var dates = new string[JoinAvenueVisitor5.ActivityCount];
        for (int i = 0; i < activities.Length; i++)
        {
            activities[i] = entity.GetActivity(i);
            dates[i] = FormatDate(entity.GetActivityDate(i).RawValue);
        }
        TB_Activities.Text = string.Join(", ", activities);
        TB_ActivityDates.Text = string.Join(", ", dates);

        CB_Origin.SetValue(entity.Origin);
        NUD_MetHour.SetValueClamped(entity.MetHour);
        NUD_MetMinute.SetValueClamped(entity.MetMinute);
        NUD_UnknownA8.SetValueClamped(entity.UnknownA8);
        CHK_IsShopChangeAllowed.IsChecked = entity.IsShopChangeAllowed;
        CHK_IsFlagA9_1.IsChecked = entity.IsFlagA9_1;
        CHK_IsFlagA9_2.IsChecked = entity.IsFlagA9_2;
        CHK_InteractedToday.IsChecked = entity.IsInteractedToday;
        CHK_IsFlagAA.IsChecked = entity.IsFlagAA;
        NUD_JoinAvenueRank.SetValueClamped(entity.JoinAvenueRank);
        NUD_UnknownAC.SetValueClamped(entity.UnknownAC);
        NUD_ShopRank.SetValueClamped(entity.ShopRank);
        NUD_ShopExperience.SetValueClamped(entity.ShopExperience);
        NUD_IsInventory.SetValueClamped(entity.IsInventory);
        NUD_ShopWork.SetValueClamped(entity.ShopWork);
        NUD_UnusedB8.SetValueClamped(entity.UnusedB8);
        NUD_UnknownBits0_8.SetValueClamped(entity.UnknownBits0_8);
        CHK_UnknownBit9.IsChecked = entity.IsUnknownBits9;
        NUD_UnknownBits10.SetValueClamped(entity.UnknownBits10);
        NUD_UnknownBits13_20.SetValueClamped(entity.UnknownBits13_20);
        NUD_UnknownBits21_27.SetValueClamped(entity.UnknownBits21_27);
        NUD_UnknownBits28_31.SetValueClamped(entity.UnknownBits28_31);

        GetShopTuple(entity.DesiredShopTypeTuple, CB_DesiredShopType, NUD_DesiredShopLevel, NUD_DesiredShopVersion);
        GetShopTuple(entity.ShopTypeTuple, CB_ShopType, NUD_ShopTypeLevel, NUD_ShopTypeVersion);
    }

    public void SaveObject(JoinAvenueVisitor5 entity)
    {
        entity.IsFlag2C = CHK_IsFlag2C.IsChecked == true;
        entity.JoinAvenueLevel = (byte)(NUD_AvenueLevel.Value ?? 0);
        entity.Unused2D = (byte)(NUD_Unused2D.Value ?? 0);

        var shopCounts = ParseByteList(TB_ShopCounts.Text, 8, 0x0F);
        entity.ShopCountRaffle = shopCounts[0];
        entity.ShopCountSalon = shopCounts[1];
        entity.ShopCountMarket = shopCounts[2];
        entity.ShopCountFlorist = shopCounts[3];
        entity.ShopCountDojo = shopCounts[4];
        entity.ShopCountNurse = shopCounts[5];
        entity.ShopCountAntique = shopCounts[6];
        entity.ShopCountCafe = shopCounts[7];

        entity.DexSeen = (ushort)(NUD_DexSeen.Value ?? 0);
        entity.FavoriteSpecies = (ushort)(CB_FavoriteSpecies.GetSelectedItem()?.Value ?? 0);
        entity.MedalRank = (byte)(NUD_MedalRank.Value ?? 0);
        entity.MedalHint = (byte)(NUD_MedalHint.Value ?? 0);
        entity.MedalCount = (byte)(NUD_MedalCount.Value ?? 0);
        entity.Date1 = new JoinAvenueDate5(ParseDate(TB_Date1.Text));
        entity.DateAdventureStart = new JoinAvenueDate5(ParseDate(TB_DateStart.Text));
        entity.DateHallOfFame = new JoinAvenueDate5(ParseDate(TB_DateHall.Text));

        var records = ParseUIntList(TB_Records.Text, (int)JoinAvenueRecordIndex5.COUNT_MAX);
        for (int i = 0; i < records.Length; i++)
            entity.SetRecord((JoinAvenueRecordIndex5)i, records[i]);

        var trivia = ParseByteList(TB_Trivia.Text, JoinAvenueVisitor5.TriviaCount, byte.MaxValue);
        for (int i = 0; i < trivia.Length; i++)
            entity.SetTrivia(i, trivia[i]);

        var activities = ParseByteList(TB_Activities.Text, JoinAvenueVisitor5.ActivityCount, byte.MaxValue);
        var activityDates = ParseDateList(TB_ActivityDates.Text, JoinAvenueVisitor5.ActivityCount);
        for (int i = 0; i < activities.Length; i++)
        {
            entity.SetActivity(i, activities[i]);
            entity.SetActivityDate(i, new JoinAvenueDate5(activityDates[i]));
        }

        entity.Origin = (ushort)(CB_Origin.GetSelectedItem()?.Value ?? 0);
        entity.MetHour = (byte)(NUD_MetHour.Value ?? 0);
        entity.MetMinute = (byte)(NUD_MetMinute.Value ?? 0);
        entity.UnknownA8 = (byte)(NUD_UnknownA8.Value ?? 0);
        entity.IsShopChangeAllowed = CHK_IsShopChangeAllowed.IsChecked == true;
        entity.IsFlagA9_1 = CHK_IsFlagA9_1.IsChecked == true;
        entity.IsFlagA9_2 = CHK_IsFlagA9_2.IsChecked == true;
        entity.IsInteractedToday = CHK_InteractedToday.IsChecked == true;
        entity.IsFlagAA = CHK_IsFlagAA.IsChecked == true;
        entity.JoinAvenueRank = (byte)(NUD_JoinAvenueRank.Value ?? 0);
        entity.UnknownAC = (byte)(NUD_UnknownAC.Value ?? 0);
        entity.ShopRank = (byte)(NUD_ShopRank.Value ?? 0);
        entity.ShopExperience = (ushort)(NUD_ShopExperience.Value ?? 0);
        entity.IsInventory = (uint)(NUD_IsInventory.Value ?? 0);
        entity.ShopWork = (ushort)(NUD_ShopWork.Value ?? 0);
        entity.UnusedB8 = (uint)(NUD_UnusedB8.Value ?? 0);
        entity.UnknownBits0_8 = (ushort)(NUD_UnknownBits0_8.Value ?? 0);
        entity.IsUnknownBits9 = CHK_UnknownBit9.IsChecked == true;
        entity.UnknownBits10 = (byte)(NUD_UnknownBits10.Value ?? 0);
        entity.UnknownBits13_20 = (byte)(NUD_UnknownBits13_20.Value ?? 0);
        entity.UnknownBits21_27 = (byte)(NUD_UnknownBits21_27.Value ?? 0);
        entity.UnknownBits28_31 = (byte)(NUD_UnknownBits28_31.Value ?? 0);

        entity.DesiredShopTypeTuple = SetShopTuple(CB_DesiredShopType, NUD_DesiredShopLevel, NUD_DesiredShopVersion);
        entity.ShopTypeTuple = SetShopTuple(CB_ShopType, NUD_ShopTypeLevel, NUD_ShopTypeVersion);
    }

    private static void GetShopTuple((byte Version, JoinAvenueShopType5 Type, byte Rank)? tuple, ComboBox type, NumericUpDown level, NumericUpDown version)
    {
        if (tuple is not { } x)
        {
            level.Value = 0;
            version.Value = 0;
            type.SetValue(-1);
            return;
        }
        type.SetValue((int)x.Type);
        level.SetValueClamped(x.Rank);
        version.SetValueClamped(x.Version);
    }

    private static (byte Version, JoinAvenueShopType5 Type, byte Rank)? SetShopTuple(ComboBox type, NumericUpDown level, NumericUpDown version)
    {
        var t = type.GetSelectedItem()?.Value ?? -1;
        if (t < 0)
            return null;
        return ((byte)(version.Value ?? 0), (JoinAvenueShopType5)t, (byte)(level.Value ?? 0));
    }

    private static string FormatDate(ushort raw)
    {
        if (raw == 0)
            return string.Empty;

        var date = new JoinAvenueDate5(raw);
        return date.Date is { } value ? value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : $"0x{raw:X4}";
    }

    private static ushort ParseDate(string? text)
    {
        text = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        if (TryParseUInt(text, out var raw))
            return (ushort)Math.Min(raw, ushort.MaxValue);
        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            JoinAvenueDate5 value = default;
            value.Date = date;
            return value.RawValue;
        }
        return 0;
    }

    private static byte[] ParseByteList(string? text, int count, byte max)
    {
        var result = new byte[count];
        var split = Split(text);
        for (int i = 0; i < count && i < split.Length; i++)
        {
            if (TryParseUInt(split[i], out var value))
                result[i] = (byte)Math.Min(value, max);
        }
        return result;
    }

    private static uint[] ParseUIntList(string? text, int count)
    {
        var result = new uint[count];
        var split = Split(text);
        for (int i = 0; i < count && i < split.Length; i++)
        {
            if (TryParseUInt(split[i], out var value))
                result[i] = value;
        }
        return result;
    }

    private static ushort[] ParseDateList(string? text, int count)
    {
        var result = new ushort[count];
        var split = Split(text);
        for (int i = 0; i < count && i < split.Length; i++)
            result[i] = ParseDate(split[i]);
        return result;
    }

    private static string[] Split(string? text)
        => (text ?? string.Empty).Split([',', ';', '|', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool TryParseUInt(string text, out uint value)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        return uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
