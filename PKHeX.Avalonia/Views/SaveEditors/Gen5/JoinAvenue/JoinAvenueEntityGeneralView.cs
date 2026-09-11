using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen5.JoinAvenue;

/// <summary>
/// The fields every Join Avenue entity shares (port of <c>JoinAvenueEntityGeneralEditor</c>).
/// </summary>
public sealed class JoinAvenueEntityGeneralView : StackPanel
{
    private static readonly IReadOnlyList<ComboItem> VersionList = GameInfo.FilteredSources.Games.ToList();
    private static readonly IReadOnlyList<ComboItem> LanguageList = GameInfo.LanguageDataSource(5, EntityContext.Gen5);

    private readonly TextBox TB_Name = UiFactory.Text("TB_Name", 8, 150);
    private readonly NumericUpDown NUD_Country = Byte("NUD_Country");
    private readonly NumericUpDown NUD_Subregion = Byte("NUD_Subregion");
    private readonly TextBox TB_Shout = UiFactory.Text("TB_Shout", 20, 220);
    private readonly ComboBox CB_Version = UiFactory.Combo("CB_Version", 160);
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 160);
    private readonly NumericUpDown NUD_Unknown22 = UiFactory.NumericUpDown("NUD_Unknown22", 0, 15, 110);
    private readonly GenderToggleView UC_Gender = new() { Name = "UC_Gender" };
    private readonly NumericUpDown NUD_Unused23 = Byte("NUD_Unused23");
    private readonly NumericUpDown NUD_TID16 = UiFactory.NumericUpDown("NUD_TID16", 0, ushort.MaxValue, 120);
    private readonly NumericUpDown NUD_Unknown26 = Byte("NUD_Unknown26");
    private readonly NumericUpDown NUD_Unknown27 = Byte("NUD_Unknown27");
    private readonly NumericUpDown NUD_PlayedHours = UiFactory.NumericUpDown("NUD_PlayedHours", 0, 1023, 120);
    private readonly NumericUpDown NUD_PlayedMinutes = UiFactory.NumericUpDown("NUD_PlayedMinutes", 0, 63, 110);
    private readonly NumericUpDown NUD_Sprite = UiFactory.NumericUpDown("NUD_Sprite", 0, ushort.MaxValue, 120);
    private readonly TextBox TB_Greeting = UiFactory.Text("TB_Greeting", 20, 220);
    private readonly TextBox TB_Farewell = UiFactory.Text("TB_Farewell", 20, 220);
    private readonly NumericUpDown NUD_MetYear = Byte("NUD_MetYear");
    private readonly NumericUpDown NUD_MetMonth = Byte("NUD_MetMonth");
    private readonly NumericUpDown NUD_MetDay = Byte("NUD_MetDay");
    private readonly NumericUpDown NUD_Seed = UiFactory.NumericUpDown("NUD_Seed", 0, uint.MaxValue, 140);

    private static NumericUpDown Byte(string name) => UiFactory.NumericUpDown(name, 0, byte.MaxValue, 110);

    public JoinAvenueEntityGeneralView()
    {
        Orientation = Orientation.Vertical;
        Spacing = 2;

        CB_Version.SetItems(VersionList);
        CB_Language.SetItems(LanguageList);

        var grid = UiFactory.FormGrid(21);
        int r = 0;
        Row(grid, r++, "L_Name", "Name:", TB_Name);
        Row(grid, r++, "L_Country", "Country:", NUD_Country);
        Row(grid, r++, "L_Subregion", "Subregion:", NUD_Subregion);
        Row(grid, r++, "L_Shout", "Shout:", TB_Shout);
        Row(grid, r++, "L_Version", "Version:", CB_Version);
        Row(grid, r++, "L_Language", "Language:", CB_Language);
        Row(grid, r++, "L_Unknown22", "0x22:", NUD_Unknown22);
        Row(grid, r++, "L_Gender", "Gender:", UC_Gender);
        Row(grid, r++, "L_Unused23", "0x23:", NUD_Unused23);
        Row(grid, r++, "L_TID16", "Trainer ID:", NUD_TID16);
        Row(grid, r++, "L_Unknown26", "0x26:", NUD_Unknown26);
        Row(grid, r++, "L_Unknown27", "0x27:", NUD_Unknown27);
        Row(grid, r++, "L_PlayedHours", "Played Hours:", NUD_PlayedHours);
        Row(grid, r++, "L_PlayedMinutes", "Played Minutes:", NUD_PlayedMinutes);
        Row(grid, r++, "L_Sprite", "Sprite:", NUD_Sprite);
        Row(grid, r++, "L_Greeting", "Greeting:", TB_Greeting);
        Row(grid, r++, "L_Farewell", "Farewell:", TB_Farewell);
        Row(grid, r++, "L_MetYear", "Met Year:", NUD_MetYear);
        Row(grid, r++, "L_MetMonth", "Met Month:", NUD_MetMonth);
        Row(grid, r++, "L_MetDay", "Met Day:", NUD_MetDay);
        Row(grid, r, "L_Seed", "Seed:", NUD_Seed);
        Children.Add(grid);
        return;

        static void Row(Grid g, int row, string name, string text, Control editor)
            => UiFactory.AddFormRow(g, row, UiFactory.Label(name, text), editor);
    }

    public void LoadObject(IJoinAvenueEntity5 entity)
    {
        TB_Name.Text = entity.Name;
        NUD_Country.SetValueClamped(entity.Country);
        NUD_Subregion.SetValueClamped(entity.Subregion);
        TB_Shout.Text = entity.Shout;
        CB_Version.SetValue(entity.Version);
        CB_Language.SetValue(entity.Language);
        NUD_Unknown22.SetValueClamped(entity.Unknown22);
        UC_Gender.Gender = entity.Gender;
        NUD_Unused23.SetValueClamped(entity.Unused23);
        NUD_TID16.SetValueClamped(entity.TID16);
        NUD_Unknown26.SetValueClamped(entity.Unknown26);
        NUD_Unknown27.SetValueClamped(entity.Unknown27);
        NUD_PlayedHours.SetValueClamped(entity.PlayedHours);
        NUD_PlayedMinutes.SetValueClamped(entity.PlayedMinutes);
        NUD_Sprite.SetValueClamped(entity.Sprite);
        TB_Greeting.Text = entity.Greeting;
        TB_Farewell.Text = entity.Farewell;
        NUD_MetYear.SetValueClamped(entity.MetYear);
        NUD_MetMonth.SetValueClamped(entity.MetMonth);
        NUD_MetDay.SetValueClamped(entity.MetDay);
        NUD_Seed.SetValueClamped(entity.Seed);
    }

    public void SaveObject(IJoinAvenueEntity5 entity)
    {
        entity.Name = TB_Name.Text ?? string.Empty;
        entity.Country = (byte)(NUD_Country.Value ?? 0);
        entity.Subregion = (byte)(NUD_Subregion.Value ?? 0);
        entity.Shout = TB_Shout.Text ?? string.Empty;
        entity.Version = (byte)(CB_Version.GetSelectedItem()?.Value ?? 0);
        entity.Language = (byte)(CB_Language.GetSelectedItem()?.Value ?? 0);
        entity.Unknown22 = (byte)(NUD_Unknown22.Value ?? 0);
        entity.Gender = UC_Gender.Gender;
        entity.Unused23 = (byte)(NUD_Unused23.Value ?? 0);
        entity.TID16 = (ushort)(NUD_TID16.Value ?? 0);
        entity.Unknown26 = (byte)(NUD_Unknown26.Value ?? 0);
        entity.Unknown27 = (byte)(NUD_Unknown27.Value ?? 0);
        entity.PlayedHours = (ushort)(NUD_PlayedHours.Value ?? 0);
        entity.PlayedMinutes = (byte)(NUD_PlayedMinutes.Value ?? 0);
        entity.Sprite = (ushort)(NUD_Sprite.Value ?? 0);
        entity.Greeting = TB_Greeting.Text ?? string.Empty;
        entity.Farewell = TB_Farewell.Text ?? string.Empty;
        entity.MetYear = (byte)(NUD_MetYear.Value ?? 0);
        entity.MetMonth = (byte)(NUD_MetMonth.Value ?? 0);
        entity.MetDay = (byte)(NUD_MetDay.Value ?? 0);
        entity.Seed = (uint)(NUD_Seed.Value ?? 0);
    }
}
