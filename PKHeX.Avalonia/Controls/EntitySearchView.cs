using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Core;
using PKHeX.Core.Searching;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Filter panel for the entity databases (port of the WinForms <c>EntitySearchControl</c>).
/// </summary>
public sealed class EntitySearchView : UserControl
{
    private EntityContext SaveContext { get; set; } = Latest.Context;

    private readonly TextBlock Label_Species = UiFactory.Label("Label_Species", "Species:");
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 170);
    private readonly TextBlock Label_Nickname = UiFactory.Label("Label_Nickname", "Nickname:");
    private readonly TextBox TB_Nickname = UiFactory.Text("TB_Nickname", 24, 170);
    private readonly TextBlock Label_HeldItem = UiFactory.Label("Label_HeldItem", "Held Item:");
    private readonly ComboBox CB_HeldItem = UiFactory.Combo("CB_HeldItem", 170);
    private readonly TextBlock Label_Ability = UiFactory.Label("Label_Ability", "Ability:");
    private readonly ComboBox CB_Ability = UiFactory.Combo("CB_Ability", 170);
    private readonly TextBlock Label_Nature = UiFactory.Label("Label_Nature", "Nature:");
    private readonly ComboBox CB_Nature = UiFactory.Combo("CB_Nature", 170);
    private readonly TextBlock Label_HiddenPowerPrefix = UiFactory.Label("Label_HiddenPowerPrefix", "Hidden Power:");
    private readonly ComboBox CB_HPType = UiFactory.Combo("CB_HPType", 170);
    private readonly TextBlock L_Version = UiFactory.Label("L_Version", "OT Version:");
    private readonly ComboBox CB_GameOrigin = UiFactory.Combo("CB_GameOrigin", 170);
    private readonly TextBlock L_Generation = UiFactory.Label("L_Generation", "Generation:");
    private readonly ComboBox CB_Generation = UiFactory.StringCombo("CB_Generation", 170, "Any", "Gen 1 (RBY/GSC)", "Gen 2 (RBY/GSC)", "Gen 3 (RSE/FRLG/CXD)", "Gen 4 (DPPt/HGSS)", "Gen 5 (BW/B2W2)", "Gen 6 (XY/ORAS)", "Gen 7 (SM/USUM/LGPE)", "Gen 8 (SWSH/BDSP/LA)", "Gen 9 (SV)");
    private readonly TextBlock L_Format = UiFactory.Label("L_Format", "Format:");
    private readonly ComboBox CB_FormatComparator = UiFactory.StringCombo("CB_FormatComparator", 70, "Any", "==", ">=", "<=");
    private readonly ComboBox CB_Format = UiFactory.Combo("CB_Format", 110);
    private readonly TextBlock Label_CurLevel = UiFactory.Label("Label_CurLevel", "Level:");
    private readonly ComboBox CB_Level = UiFactory.StringCombo("CB_Level", 70, "Any", "==", ">=", "<=");
    private readonly NumericTextBox TB_Level = UiFactory.Numeric("TB_Level", 3, 56);
    private readonly TextBlock L_EVTraining = UiFactory.Label("L_EVTraining", "EV Training:");
    private readonly ComboBox CB_EVTrain = UiFactory.StringCombo("CB_EVTrain", 170, "Any", "None (0)", "Some (127-1)", "Half (128-507)", "Full (508+)");
    private readonly TextBlock L_Potential = UiFactory.Label("L_Potential", "IV Potential:");
    private readonly ComboBox CB_IV = UiFactory.StringCombo("CB_IV", 170, "Any", "<= 90", "91-120", "121-150", "151-179", "180+", "== 186");
    private readonly TextBlock L_Move1 = UiFactory.Label("L_Move1", "Move 1:");
    private readonly ComboBox CB_Move1 = UiFactory.Combo("CB_Move1", 170);
    private readonly TextBlock L_Move2 = UiFactory.Label("L_Move2", "Move 2:");
    private readonly ComboBox CB_Move2 = UiFactory.Combo("CB_Move2", 170);
    private readonly TextBlock L_Move3 = UiFactory.Label("L_Move3", "Move 3:");
    private readonly ComboBox CB_Move3 = UiFactory.Combo("CB_Move3", 170);
    private readonly TextBlock L_Move4 = UiFactory.Label("L_Move4", "Move 4:");
    private readonly ComboBox CB_Move4 = UiFactory.Combo("CB_Move4", 170);
    private readonly CheckBox CHK_Shiny = UiFactory.Check("CHK_Shiny", "Shiny");
    private readonly CheckBox CHK_IsEgg = UiFactory.Check("CHK_IsEgg", "Egg");
    private readonly TextBlock L_ESV = UiFactory.Label("L_ESV", "ESV:");
    private readonly NumericTextBox MT_ESV = UiFactory.Numeric("MT_ESV", 4, 60);

    public EntitySearchView()
    {
        CHK_Shiny.IsThreeState = CHK_IsEgg.IsThreeState = true;

        var left = UiFactory.FormGrid(8);
        UiFactory.AddFormRow(left, 0, Label_Species, CB_Species);
        UiFactory.AddFormRow(left, 1, Label_Nickname, TB_Nickname);
        UiFactory.AddFormRow(left, 2, Label_HeldItem, CB_HeldItem);
        UiFactory.AddFormRow(left, 3, Label_Ability, CB_Ability);
        UiFactory.AddFormRow(left, 4, Label_Nature, CB_Nature);
        UiFactory.AddFormRow(left, 5, Label_HiddenPowerPrefix, CB_HPType);
        UiFactory.AddFormRow(left, 6, L_Version, CB_GameOrigin);
        UiFactory.AddFormRow(left, 7, L_Generation, CB_Generation);

        var right = UiFactory.FormGrid(8);
        UiFactory.AddFormRow(right, 0, L_Format, UiFactory.Row(CB_FormatComparator, CB_Format));
        UiFactory.AddFormRow(right, 1, Label_CurLevel, UiFactory.Row(CB_Level, TB_Level));
        UiFactory.AddFormRow(right, 2, L_EVTraining, CB_EVTrain);
        UiFactory.AddFormRow(right, 3, L_Potential, CB_IV);
        UiFactory.AddFormRow(right, 4, L_Move1, CB_Move1);
        UiFactory.AddFormRow(right, 5, L_Move2, CB_Move2);
        UiFactory.AddFormRow(right, 6, L_Move3, CB_Move3);
        UiFactory.AddFormRow(right, 7, L_Move4, CB_Move4);

        var flags = UiFactory.Row(CHK_Shiny, CHK_IsEgg, L_ESV, MT_ESV);
        var TLP_Filters = UiFactory.Row(left, right);
        TLP_Filters.Spacing = 16;
        left.VerticalAlignment = right.VerticalAlignment = VerticalAlignment.Top;
        Content = UiFactory.Column(TLP_Filters, flags);

        CHK_IsEgg.IsCheckedChanged += (_, _) => ToggleESV();
        CB_Level.SelectionChanged += (_, _) => ChangeLevel();
        CB_GameOrigin.SelectionChanged += (_, _) => ChangeGame();
        CB_Generation.SelectionChanged += (_, _) => ChangeGeneration();
        CB_FormatComparator.SelectionChanged += (_, _) => ChangeFormatFilter();
    }

    /// <summary>
    /// Creates a filter function based on the current search settings, including any batch instructions.
    /// </summary>
    public Func<PKM, bool> GetFilter(string batchInstructions = "")
        => CreateSearchSettings(batchInstructions).CreateSearchPredicate();

    /// <summary>
    /// Populates combo box bindings with game data sources.
    /// </summary>
    public void PopulateComboBoxes(FilteredGameDataSource filtered)
    {
        var comboAny = new ComboItem(MsgAny, -1);

        var source = filtered.Source;
        var species = new List<ComboItem>(source.SpeciesDataSource)
        {
            [0] = comboAny,
        };
        CB_Species.SetItems(species);

        var items = new List<ComboItem>(filtered.Items);
        items.Insert(0, comboAny);
        CB_HeldItem.SetItems(items);

        var natures = new List<ComboItem>(source.NatureDataSource);
        natures.Insert(0, comboAny with { Value = (int)Nature.Random });
        CB_Nature.SetItems(natures);

        var abilities = new List<ComboItem>(source.AbilityDataSource);
        abilities.Insert(0, comboAny);
        CB_Ability.SetItems(abilities);

        var versions = new List<ComboItem>(source.VersionDataSource);
        versions.Insert(0, comboAny);
        versions.RemoveAt(versions.Count - 1);
        CB_GameOrigin.SetItems(versions);

        var hptypes = source.Strings.HiddenPowerTypes;
        var types = Util.GetCBList(hptypes);
        types.Insert(0, comboAny);
        CB_HPType.SetItems(types);

        var moves = new List<ComboItem>(filtered.Moves);
        moves.RemoveAt(0);
        moves.Insert(0, comboAny);
        foreach (var cb in new[] { CB_Move1, CB_Move2, CB_Move3, CB_Move4 })
            cb.SetItems(moves);

        var contexts = new List<ComboItem>
        {
            new(MsgAny, (int)EntityContext.None),
        };

        foreach (var context in Enum.GetValues<EntityContext>())
        {
            if (context is EntityContext.None or EntityContext.SplitInvalid or EntityContext.MaxInvalid)
                continue;
            if (!context.IsValid || !context.ToString().StartsWith("Gen", StringComparison.Ordinal))
                continue;
            contexts.Add(new ComboItem(context.ToString(), (int)context));
        }

        CB_Format.SetItems(contexts);

        ResetFilters();
    }

    public void InitializeSelections(SaveFile sav, bool showContext = true)
    {
        SaveContext = sav.Context;
        if (sav.Generation >= 8)
        {
            CB_FormatComparator.SelectedIndex = 1; // ==
            CB_Format.SetValue((int)sav.Context);
        }
        else
        {
            CB_FormatComparator.SelectedIndex = 3; // <=
        }
        L_Format.IsVisible = CB_FormatComparator.IsVisible = CB_Format.IsVisible = showContext;
    }

    /// <summary>
    /// Sets the localized text for the format "Any" option.
    /// </summary>
    public void SetFormatAnyText(string text)
    {
        if (CB_Format.ItemsSource is not IReadOnlyList<ComboItem> { Count: > 0 } list)
            return;
        var updated = new List<ComboItem>(list) { [0] = new(text, list[0].Value) };
        var index = CB_Format.SelectedIndex;
        CB_Format.SetItems(updated);
        CB_Format.SelectedIndex = index;
    }

    /// <summary>
    /// Resets filters to their default state.
    /// </summary>
    public void ResetFilters()
    {
        CHK_Shiny.IsChecked = CHK_IsEgg.IsChecked = null; // indeterminate
        MT_ESV.Text = string.Empty;
        CB_HeldItem.SelectedIndex = 0;
        CB_Species.SelectedIndex = 0;
        CB_Ability.SelectedIndex = 0;
        CB_Nature.SelectedIndex = 0;
        CB_HPType.SelectedIndex = 0;
        TB_Nickname.Text = string.Empty;

        CB_Level.SelectedIndex = 0;
        TB_Level.Text = string.Empty;
        CB_EVTrain.SelectedIndex = 0;
        CB_IV.SelectedIndex = 0;

        CB_Move1.SelectedIndex = CB_Move2.SelectedIndex = CB_Move3.SelectedIndex = CB_Move4.SelectedIndex = 0;

        CB_GameOrigin.SelectedIndex = 0;
        CB_Generation.SelectedIndex = 0;

        MT_ESV.IsVisible = L_ESV.IsVisible = false;
    }

    /// <summary>
    /// Creates search settings based on the current filter selection.
    /// </summary>
    public SearchSettings CreateSearchSettings(string batchInstructions)
    {
        var settings = new SearchSettings
        {
            Context = (EntityContext)CB_Format.GetValue(),
            SearchContext = (SearchComparison)Math.Max(0, CB_FormatComparator.SelectedIndex),
            Generation = (byte)Math.Max(0, CB_Generation.SelectedIndex),

            Version = (GameVersion)CB_GameOrigin.GetValue(),
            HiddenPowerType = CB_HPType.GetValue(),

            Species = GetU16(CB_Species),
            Ability = CB_Ability.GetValue(),
            Nature = (Nature)CB_Nature.GetValue(),
            Item = CB_HeldItem.GetValue(),
            Nickname = (TB_Nickname.Text ?? string.Empty).Trim(),

            BatchInstructions = batchInstructions,

            Level = byte.TryParse(TB_Level.Text, out var lvl) ? lvl : null,
            SearchLevel = (SearchComparison)Math.Max(0, CB_Level.SelectedIndex),
            EVType = Math.Max(0, CB_EVTrain.SelectedIndex),
            IVType = Math.Max(0, CB_IV.SelectedIndex),
        };

        settings.AddMove(GetU16(CB_Move1));
        settings.AddMove(GetU16(CB_Move2));
        settings.AddMove(GetU16(CB_Move3));
        settings.AddMove(GetU16(CB_Move4));

        if (CHK_Shiny.IsChecked is { } shiny)
            settings.SearchShiny = shiny;

        if (CHK_IsEgg.IsChecked is { } egg)
        {
            settings.SearchEgg = egg;
            if (int.TryParse(MT_ESV.Text, out int esv))
                settings.ESV = esv;
        }

        return settings;

        static ushort GetU16(ComboBox cb)
        {
            var val = cb.GetValue();
            if (val <= 0)
                return 0;
            return (ushort)val;
        }
    }

    private void ToggleESV() => L_ESV.IsVisible = MT_ESV.IsVisible = CHK_IsEgg.IsChecked == true;

    private void ChangeLevel()
    {
        if (CB_Level.SelectedIndex == 0)
            TB_Level.Text = string.Empty;
    }

    private void ChangeGame()
    {
        if (CB_GameOrigin.SelectedIndex != 0)
            CB_Generation.SelectedIndex = 0;
    }

    private void ChangeGeneration()
    {
        if (CB_Generation.SelectedIndex != 0)
            CB_GameOrigin.SelectedIndex = 0;
    }

    private void ChangeFormatFilter()
    {
        if (CB_FormatComparator.SelectedIndex == 0)
        {
            CB_Format.IsVisible = false;
            CB_Format.SelectedIndex = 0;
        }
        else
        {
            CB_Format.IsVisible = true;
            CB_Format.SetValue((int)SaveContext);
        }
    }
}
