using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen8;

/// <summary>
/// Pokédex editor for Sword / Shield (port of the WinForms <c>SAV_PokedexSWSH</c>).
/// </summary>
/// <remarks>
/// Sword/Shield records, per dex entry, which form was seen as male, female, shiny male and shiny female, so the
/// editor shows those four 64-entry form lists. The last two slots are the Gigantamax forms; Urshifu uses both.
/// </remarks>
public sealed class PokedexSWSHWindow : SaveEditorWindow
{
    private const int FormSlots = 64;

    private readonly SAV8SWSH Origin;
    private readonly SAV8SWSH SAV;
    private readonly Zukan8 Dex;
    private readonly CheckBox[] CL;
    private readonly CheckedListView[] CHK;
    private readonly IReadOnlyList<Zukan8EntryInfo> Indexes;

    private int lastIndex = -1;
    private bool CanSave;
    private bool Loading;

    private readonly ListBox LB_Species = new() { Name = "LB_Species", Width = 250, Height = 420 };
    private readonly ObservableCollection<string> SpeciesItems = [];
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 190);
    private readonly ComboBox CB_Gender = UiFactory.StringCombo("CB_Gender", 120);
    private readonly NumericUpDown NUD_Form = UiFactory.NumericUpDown("NUD_Form", 0, 63, 100);
    private readonly NumericUpDown NUD_Battled = UiFactory.NumericUpDown("NUD_Battled", 0, int.MaxValue, 130);
    private readonly CheckBox CHK_Caught = UiFactory.Check("CHK_Caught", "Owned");
    private readonly CheckBox CHK_Gigantamaxed = UiFactory.Check("CHK_Gigantamaxed", "Gigantamaxed");
    private readonly CheckBox CHK_Gigantamaxed1 = UiFactory.Check("CHK_Gigantamaxed1", "Gigantamaxed 1");
    private readonly CheckBox CHK_G = UiFactory.Check("CHK_G", "Gigantamax");
    private readonly CheckBox CHK_S = UiFactory.Check("CHK_S", "Shiny");
    private readonly Button B_GiveAll = UiFactory.Button("B_GiveAll", "Check All");
    private readonly Button B_Modify = UiFactory.Button("B_Modify", "Modify...");

    public PokedexSWSHWindow(SAV8SWSH sav) : base("SAV_PokedexSWSH", "Pokédex Editor")
    {
        SAV = (SAV8SWSH)(Origin = sav).Clone();
        Dex = SAV.Blocks.Zukan;

        var speciesNames = GameInfo.Strings.Species;
        var indexes = Zukan8.GetRawIndexes(PersonalTable.SWSH, Dex.GetRevision(), Zukan8Index.TotalCount);
        Indexes = [.. indexes.OrderBy(z => z.GetEntryName(speciesNames))];

        CL = [
            UiFactory.Check("CHK_L1", "Japanese"), UiFactory.Check("CHK_L2", "English"),
            UiFactory.Check("CHK_L3", "French"), UiFactory.Check("CHK_L4", "Italian"),
            UiFactory.Check("CHK_L5", "German"), UiFactory.Check("CHK_L6", "Spanish"),
            UiFactory.Check("CHK_L7", "Korean"), UiFactory.Check("CHK_L8", "ChineseS"),
            UiFactory.Check("CHK_L9", "ChineseT"),
        ];
        CHK = [
            new CheckedListView { Name = "CLB_1", Width = 150, Height = 300 },
            new CheckedListView { Name = "CLB_2", Width = 150, Height = 300 },
            new CheckedListView { Name = "CLB_3", Width = 150, Height = 300 },
            new CheckedListView { Name = "CLB_4", Width = 150, Height = 300 },
        ];

        Loading = true;
        BuildLayout();

        foreach (var c in CHK)
        {
            c.ClearItems();
            for (int j = 0; j < FormSlots - 1; j++)
                c.Add($"{j:00} - N/A");
            c.Add("Gigantamax (0)");
        }

        var species = GameInfo.FilteredSources.Species.Where(z => Dex.DexLookup.ContainsKey((ushort)z.Value)).ToArray();
        CB_Species.SetItems(species);
        CB_Species.SelectedIndex = 0; // the WinForms binding shows the first entry; the list handler never syncs it
        foreach (var z in Indexes)
            SpeciesItems.Add(z.GetEntryName(speciesNames) + (Dex.DexLookup[z.Species].DexType == z.Entry.DexType ? string.Empty : "***"));

        Loading = false;
        LB_Species.SelectedIndex = 0;
        CanSave = true;
    }

    private void BuildLayout()
    {
        LB_Species.ItemsSource = SpeciesItems;
        foreach (var s in new[] { "\u2642", "\u2640", "-" }) // WinForms CB_Gender items
            CB_Gender.Items.Add(s);

        // One list per seen-state column, headed like the WinForms table: Male / Female / *Male* / *Female*.
        string[] headerNames = ["L_Male", "L_Female", "L_MaleShiny", "L_FemaleShiny"];
        string[] headerText = ["Male", "Female", "*Male*", "*Female*"];
        var lists = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        for (int i = 0; i < CHK.Length; i++)
            lists.Children.Add(UiFactory.Column(UiFactory.Label(headerNames[i], headerText[i]), CHK[i]));

        var displayed = UiFactory.Column(CHK_G, CHK_S, CB_Gender);
        var right = UiFactory.Column(
            new GroupBoxView("GB_Language", "Languages", UiFactory.Column(CL)),
            UiFactory.Label("L_DisplayedForm", "Displayed Form:"),
            NUD_Form,
            new GroupBoxView("GB_Displayed", "Displayed", displayed),
            UiFactory.Label("L_Battled", "Battled:"),
            NUD_Battled);

        var middle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        middle.Children.Add(LB_Species);
        middle.Children.Add(lists);
        middle.Children.Add(right);

        var body = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_goto", "goto:"), CB_Species, B_GiveAll, B_Modify),
            UiFactory.Row(CHK_Caught, CHK_Gigantamaxed, CHK_Gigantamaxed1),
            middle);
        SetBody(new ScrollViewer { Content = body, MaxHeight = 620 });

        LB_Species.SelectionChanged += (_, _) => ChangeLBSpecies();
        CB_Species.SelectionChanged += (_, _) => ChangeCBSpecies();
        B_GiveAll.AttachClickHandled(mods =>
        {
            SetEntry(lastIndex);
            Dex.SetDexEntryAll(Indexes[lastIndex].Species, mods == KeyModifiers.Shift);
            GetEntry(lastIndex);
        });
        B_Modify.Flyout = BuildModifyMenu();
    }

    private MenuFlyout BuildModifyMenu()
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.Bottom };
        AddItem(flyout, "mnuSeenNone", "Seen none", _ => Dex.SeenNone());
        AddItem(flyout, "mnuSeenAll", "Seen all", mods => Dex.SeenAll(mods == KeyModifiers.Shift));
        AddItem(flyout, "mnuCaughtNone", "Caught none", _ => Dex.CaughtNone());
        AddItem(flyout, "mnuCaughtAll", "Caught all", mods => Dex.CaughtAll(mods == KeyModifiers.Shift));
        AddItem(flyout, "mnuComplete", "Complete Dex", mods => Dex.CompleteDex(mods == KeyModifiers.Shift));
        AddItem(flyout, "mnuBattleCount", "Change All Battle Count", _ => Dex.SetAllBattledCount((uint)(NUD_Battled.Value ?? 0)));
        return flyout;
    }

    private void AddItem(MenuFlyout flyout, string name, string header, Action<KeyModifiers> action)
    {
        var item = new MenuItem { Name = name, Header = header };
        item.Click += (_, _) =>
        {
            SetEntry(lastIndex);
            action(MainWindow.CurrentModifiers);
            GetEntry(lastIndex);
        };
        flyout.Items.Add(item);
    }

    #region Selection

    private void ChangeCBSpecies()
    {
        if (Loading || CB_Species.GetSelectedItem() is not { } item)
            return;
        var info = Dex.DexLookup[(ushort)item.Value];
        var index = info.AbsoluteIndex - 1;
        if (LB_Species.SelectedIndex != index)
            LB_Species.SelectedIndex = index; // triggers the list handler
    }

    private void ChangeLBSpecies()
    {
        if (Loading || LB_Species.SelectedIndex < 0)
            return;
        SetEntry(lastIndex);
        lastIndex = LB_Species.SelectedIndex;
        GetEntry(lastIndex);
    }

    #endregion

    #region Entry read / write

    private void GetEntry(int index)
    {
        if ((uint)index >= Indexes.Count)
            return;
        var entry = Indexes[index].Entry;
        if (entry.DexType == Zukan8Type.None)
            return;

        var species = Indexes[index].Species;
        var forms = GetFormList(species);
        if (forms[0].Length == 0)
            forms[0] = GameInfo.Strings.Types[0];

        for (int i = 0; i < CHK.Length; i++)
        {
            var c = CHK[i];
            c.ClearItems();
            for (byte j = 0; j < FormSlots; j++)
            {
                var label = j < FormSlots - 1
                    ? $"{j:00} - {(j < forms.Length ? forms[j] : "N/A")}"
                    : "Gigantamax";
                c.Add(label, Dex.GetSeenRegion(entry, j, i));
            }

            if (species == (int)Species.Urshifu)
            {
                // Urshifu stores both of its Gigantamax forms in the last two slots.
                c.SetItemText(62, $"Gmax-{forms[1]}");
                c.SetItemText(63, $"Gmax-{forms[0]}");
            }
        }

        for (int i = 0; i < CL.Length; i++)
            CL[i].IsChecked = Dex.GetIsLanguageIndexObtained(entry, i);

        NUD_Form.SetValueClamped(Dex.GetFormDisplayed(entry));
        CHK_Caught.IsChecked = Dex.GetCaught(entry);
        CHK_Gigantamaxed.IsChecked = Dex.GetCaughtGigantamaxed(entry);
        CHK_G.IsChecked = Dex.GetDisplayDynamaxInstead(entry);
        CHK_S.IsChecked = Dex.GetDisplayShiny(entry);
        CB_Gender.SelectedIndex = (int)Dex.GetGenderDisplayed(entry);

        CHK_Gigantamaxed1.IsVisible = species == (int)Species.Urshifu;
        if (CHK_Gigantamaxed1.IsVisible)
            CHK_Gigantamaxed1.IsChecked = Dex.GetCaughtGigantamax1(entry);

        NUD_Battled.SetValueClamped(Dex.GetBattledCount(entry));
    }

    private static string[] GetFormList(ushort species)
    {
        var s = GameInfo.Strings;
        if (species == (int)Species.Alcremie)
            return FormConverter.GetAlcremieFormList(s.forms);
        return FormConverter.GetFormList(species, s.Types, s.forms, GameInfo.GenderSymbolASCII, EntityContext.Gen8);
    }

    private void SetEntry(int index)
    {
        if (!CanSave || Loading || index < 0 || (uint)index >= Indexes.Count)
            return;
        var entry = Indexes[index].Entry;
        if (entry.DexType == Zukan8Type.None)
            return;

        for (int i = 0; i < CHK.Length; i++)
        {
            var c = CHK[i];
            for (byte j = 0; j < FormSlots && j < c.Count; j++)
                Dex.SetSeenRegion(entry, j, i, c.GetItemChecked(j));
        }

        for (int i = 0; i < CL.Length; i++)
            Dex.SetIsLanguageIndexObtained(entry, i, CL[i].IsChecked == true);

        Dex.SetFormDisplayed(entry, (uint)(NUD_Form.Value ?? 0));
        Dex.SetCaught(entry, CHK_Caught.IsChecked == true);
        Dex.SetCaughtGigantamax(entry, CHK_Gigantamaxed.IsChecked == true);
        Dex.SetGenderDisplayed(entry, (uint)Math.Max(0, CB_Gender.SelectedIndex));
        Dex.SetDisplayDynamaxInstead(entry, CHK_G.IsChecked == true);
        Dex.SetDisplayShiny(entry, CHK_S.IsChecked == true);
        Dex.SetBattledCount(entry, (uint)(NUD_Battled.Value ?? 0));

        if (CHK_Gigantamaxed1.IsVisible)
            Dex.SetCaughtGigantamax1(entry, CHK_Gigantamaxed1.IsChecked == true);
    }

    #endregion

    protected override void OnSave()
    {
        SetEntry(lastIndex);
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
