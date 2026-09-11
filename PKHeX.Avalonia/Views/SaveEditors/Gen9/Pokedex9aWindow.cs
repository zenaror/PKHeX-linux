using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen9;

/// <summary>
/// Pokédex editor for Pokémon Legends: Z-A (port of the WinForms <c>SAV_Pokedex9a</c>).
/// </summary>
/// <remarks>
/// The Z-A dex stores one fixed-size entry per species with 32 form bits for caught/seen/shiny, plus the
/// "seen mega" bits whose meaning depends on the species (X/Y, Z, Meowstic, Magearna, Tatsugiri).
/// </remarks>
public sealed class Pokedex9aWindow : SaveEditorWindow
{
    private const int FormCount = 8 * sizeof(uint);

    private readonly SAV9ZA Origin;
    private readonly SAV9ZA SAV;
    private readonly Zukan9a Dex;
    private readonly MegaFormNames MegaNames;
    private readonly DexMap[] ListBoxToSpecies;

    private int lastIndex;
    private bool Loading;

    private readonly ListBox LB_Species = new() { Name = "LB_Species", Width = 240, Height = 460 };
    private readonly ObservableCollection<string> SpeciesItems = [];
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);

    private readonly CheckedListView CLB_FormCaught = new() { Name = "CLB_FormCaught", Width = 150, Height = 220 };
    private readonly CheckedListView CLB_FormSeen = new() { Name = "CLB_FormSeen", Width = 150, Height = 220 };
    private readonly CheckedListView CLB_FormShiny = new() { Name = "CLB_FormShiny", Width = 150, Height = 220 };

    private readonly CheckBox CHK_IsNew = UiFactory.Check("CHK_IsNew", "New");
    private readonly CheckBox CHK_SeenMale = UiFactory.Check("CHK_SeenMale", "Male");
    private readonly CheckBox CHK_SeenFemale = UiFactory.Check("CHK_SeenFemale", "Female");
    private readonly CheckBox CHK_SeenGenderless = UiFactory.Check("CHK_SeenGenderless", "Genderless");
    private readonly CheckBox CHK_SeenAlpha = UiFactory.Check("CHK_SeenAlpha", "Alpha");
    private readonly CheckBox CHK_SeenMega0 = UiFactory.Check("CHK_SeenMega0", "Mega0");
    private readonly CheckBox CHK_SeenMega1 = UiFactory.Check("CHK_SeenMega1", "Mega1");
    private readonly CheckBox CHK_SeenMega2 = UiFactory.Check("CHK_SeenMega2", "Mega2");

    private readonly ComboBox CB_DisplayForm = UiFactory.Combo("CB_DisplayForm", 150);
    private readonly ObservableCollection<string> DisplayFormItems = [];
    private readonly ComboBox CB_Gender = UiFactory.StringCombo("CB_Gender", 80, "-", "♂", "♀", "♂/♀");
    private readonly CheckBox CHK_DisplayShiny = UiFactory.Check("CHK_DisplayShiny", "Shiny:");

    private readonly CheckBox CHK_LangJPN = UiFactory.Check("CHK_LangJPN", "Japanese");
    private readonly CheckBox CHK_LangENG = UiFactory.Check("CHK_LangENG", "English");
    private readonly CheckBox CHK_LangFRE = UiFactory.Check("CHK_LangFRE", "French");
    private readonly CheckBox CHK_LangITA = UiFactory.Check("CHK_LangITA", "Italian");
    private readonly CheckBox CHK_LangGER = UiFactory.Check("CHK_LangGER", "German");
    private readonly CheckBox CHK_LangSPA = UiFactory.Check("CHK_LangSPA", "Spanish (EU)");
    private readonly CheckBox CHK_LangKOR = UiFactory.Check("CHK_LangKOR", "Korean");
    private readonly CheckBox CHK_LangCHS = UiFactory.Check("CHK_LangCHS", "ChineseS");
    private readonly CheckBox CHK_LangCHT = UiFactory.Check("CHK_LangCHT", "ChineseT");
    private readonly CheckBox CHK_LangLATAM = UiFactory.Check("CHK_LangLATAM", "Spanish (LA)");

    private readonly Button B_GiveAll = UiFactory.Button("B_GiveAll", "Check All");
    private readonly Button B_Modify = UiFactory.Button("B_Modify", "Modify All...");

    public Pokedex9aWindow(SAV9ZA sav) : base("SAV_Pokedex9a", "Pokédex Editor")
    {
        SAV = (SAV9ZA)(Origin = sav).Clone();
        Dex = SAV.Blocks.Zukan;

        Loading = true;

        foreach (var clb in Lists)
        {
            for (int i = 0; i < FormCount; i++)
                clb.Add(string.Empty);
        }

        var filtered = GameInfo.FilteredSources;
        var strings = filtered.Source.Strings;
        MegaNames = FormConverter.GetMegaFormNames(strings.forms, GameInfo.GenderSymbolUnicode, strings.Types);
        int maxSpecies = sav.MaxSpeciesID; // no DLC species

        var species = filtered.Species.Where(z => z.Value <= maxSpecies).ToArray();
        CB_Species.SetItems(species);
        CB_Species.SelectedIndex = 0; // the WinForms binding shows the first entry; the list handler never syncs it

        var list = species
            .Select(z => new DexMap(z))
            .OrderByDescending(z => z.IsInAnyDex)
            .ThenBy(z => z.Dex).ToArray();
        for (var i = 0; i < list.Length; i++)
        {
            var n = list[i];
            SpeciesItems.Add($"{n.GetDexString()} - {n.Name}");
            n.ListIndex = i;
        }
        ListBoxToSpecies = list;

        BuildLayout();

        LB_Species.SelectedIndex = 0;
        Loading = false;
        lastIndex = 0;
        GetEntry(0);
    }

    private CheckedListView[] Lists => [CLB_FormCaught, CLB_FormSeen, CLB_FormShiny];

    private void BuildLayout()
    {
        LB_Species.ItemsSource = SpeciesItems;
        CB_DisplayForm.ItemsSource = DisplayFormItems;

        var seen = new GroupBoxView("GB_Seen", "Seen", UiFactory.Column(
            CHK_IsNew, CHK_SeenMale, CHK_SeenFemale, CHK_SeenGenderless, CHK_SeenAlpha,
            CHK_SeenMega0, CHK_SeenMega1, CHK_SeenMega2));

        var displayGrid = UiFactory.FormGrid(3);
        UiFactory.AddFormRow(displayGrid, 0, UiFactory.Label("L_DisplayForm", "Form:"), CB_DisplayForm);
        UiFactory.AddFormRow(displayGrid, 1, UiFactory.Label("L_DisplayGendered", "Gender:"), CB_Gender);
        UiFactory.AddFormRow(displayGrid, 2, null, CHK_DisplayShiny);
        var displayed = new GroupBoxView("GB_Displayed", "Displayed", displayGrid);

        var languages = new GroupBoxView("GB_Language", "Languages", UiFactory.Column(
            CHK_LangJPN, CHK_LangENG, CHK_LangFRE, CHK_LangITA, CHK_LangGER,
            CHK_LangSPA, CHK_LangLATAM, CHK_LangKOR, CHK_LangCHS, CHK_LangCHT));

        var flagLists = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        flagLists.Children.Add(UiFactory.Column(UiFactory.Label("L_Caught", "Caught:"), CLB_FormCaught));
        flagLists.Children.Add(UiFactory.Column(UiFactory.Label("L_Seen", "Seen:"), CLB_FormSeen));
        flagLists.Children.Add(UiFactory.Column(UiFactory.Label("L_SeenShiny", "Seen (Shiny):"), CLB_FormShiny));

        var right = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_goto", "goto:"), CB_Species),
            UiFactory.Row(seen, UiFactory.Column(displayed, languages)),
            flagLists,
            UiFactory.Row(B_GiveAll, B_Modify));

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(LB_Species);
        body.Children.Add(new ScrollViewer { Content = right, MaxHeight = 660 });
        SetBody(body);

        LB_Species.SelectionChanged += (_, _) => ChangeLBSpecies();
        CB_Species.SelectionChanged += (_, _) => ChangeCBSpecies();
        B_GiveAll.AttachClickHandled(ClickGiveAll);
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
        return flyout;
    }

    private void AddItem(MenuFlyout flyout, string name, string header, Action<KeyModifiers> action)
    {
        var item = new MenuItem { Name = name, Header = header };
        item.Click += (_, _) =>
        {
            var species = GetSpecies(lastIndex);
            SetEntry(species);
            action(MainWindow.CurrentModifiers);
            GetEntry(species);
        };
        flyout.Items.Add(item);
    }

    private void ClickGiveAll(KeyModifiers mods)
    {
        SetEntry(lastIndex);
        var species = GetSpecies(lastIndex);
        Dex.SetDexEntryAll(species, mods == KeyModifiers.Shift);
        GetEntry(species);
    }

    private sealed record DexMap
    {
        public ushort Species { get; }
        public bool IsInAnyDex => Dex != 0;
        public ushort Dex { get; }
        public string Name { get; }
        public int ListIndex { get; set; }

        public DexMap(ComboItem c)
        {
            Species = (ushort)c.Value;
            Name = c.Text;
            Dex = GetDexIndex(Species);
        }

        private static ushort GetDexIndex(ushort species)
        {
            var entry = PersonalTable.ZA.GetFormEntry(species, 0);
            for (byte i = 0; i < entry.FormCount; i++)
            {
                entry = PersonalTable.ZA.GetFormEntry(species, i);
                if (entry.DexIndex != 0)
                    return entry.DexIndex;
            }
            return 0;
        }

        public string GetDexString() => IsInAnyDex ? $"{Dex:000}" : "***";
    }

    private ushort GetSpecies(int listBoxIndex) => Array.Find(ListBoxToSpecies, z => z.ListIndex == listBoxIndex)?.Species ?? 0;
    private int GetIndex(ushort species) => Array.Find(ListBoxToSpecies, z => z.Species == species)?.ListIndex ?? 0;

    private void ChangeCBSpecies()
    {
        if (Loading || CB_Species.GetSelectedItem() is not { } item)
            return;
        var index = GetIndex((ushort)item.Value);
        if (LB_Species.SelectedIndex != index)
            LB_Species.SelectedIndex = index; // triggers the list event
    }

    private void ChangeLBSpecies()
    {
        if (Loading || LB_Species.SelectedIndex < 0)
            return;

        SetEntry(lastIndex);
        lastIndex = LB_Species.SelectedIndex;
        GetEntry(lastIndex);
    }

    private void GetEntry(int index)
    {
        if (Loading || index < 0)
            return;
        GetEntry(GetSpecies(index));
    }

    private void GetEntry(ushort species)
    {
        var entry = SAV.Zukan.GetEntry(species);
        var forms = GetFormList(species);
        if (forms[0].Length == 0)
            forms[0] = GameInfo.Strings.Types[0];

        CHK_IsNew.IsChecked = entry.GetDisplayIsNew();
        CHK_SeenMale.IsChecked = entry.GetIsGenderSeen(0);
        CHK_SeenFemale.IsChecked = entry.GetIsGenderSeen(1);
        CHK_SeenGenderless.IsChecked = entry.GetIsGenderSeen(2);
        CHK_SeenAlpha.IsChecked = entry.GetIsSeenAlpha();

        CHK_SeenMega0.IsChecked = entry.GetIsSeenMega(0);
        CHK_SeenMega1.IsChecked = entry.GetIsSeenMega(1);
        CHK_SeenMega2.IsChecked = entry.GetIsSeenMega(2);
        SetMegaLabels(species);

        for (byte i = 0; i < forms.Length; i++)
        {
            foreach (var clb in Lists)
                clb.SetItemText(i, forms[i]);
            CLB_FormCaught.SetItemChecked(i, entry.GetIsFormCaught(i));
            CLB_FormSeen.SetItemChecked(i, entry.GetIsFormSeen(i));
            CLB_FormShiny.SetItemChecked(i, entry.GetIsShinySeen(i));
        }

        DisplayFormItems.Clear();
        foreach (var f in forms)
            DisplayFormItems.Add(f);
        CB_DisplayForm.SelectedIndex = Math.Clamp(entry.DisplayForm, 0, DisplayFormItems.Count - 1);
        CB_Gender.SelectedIndex = Math.Clamp((byte)entry.DisplayGender, 0, CB_Gender.ItemCount - 1);
        CHK_DisplayShiny.IsChecked = entry.GetDisplayIsShiny();

        CHK_LangJPN.IsChecked = entry.GetLanguageFlag((int)LanguageID.Japanese);
        CHK_LangENG.IsChecked = entry.GetLanguageFlag((int)LanguageID.English);
        CHK_LangFRE.IsChecked = entry.GetLanguageFlag((int)LanguageID.French);
        CHK_LangITA.IsChecked = entry.GetLanguageFlag((int)LanguageID.Italian);
        CHK_LangGER.IsChecked = entry.GetLanguageFlag((int)LanguageID.German);
        CHK_LangSPA.IsChecked = entry.GetLanguageFlag((int)LanguageID.Spanish);
        CHK_LangKOR.IsChecked = entry.GetLanguageFlag((int)LanguageID.Korean);
        CHK_LangCHS.IsChecked = entry.GetLanguageFlag((int)LanguageID.ChineseS);
        CHK_LangCHT.IsChecked = entry.GetLanguageFlag((int)LanguageID.ChineseT);
        CHK_LangLATAM.IsChecked = entry.GetLanguageFlag((int)LanguageID.SpanishL);
    }

    private void SetMegaLabels(ushort species)
    {
        if (Zukan9a.IsMegaFormXY(species, SAV.SaveRevision))
        {
            Show(MegaNames.X, MegaNames.Y, null);
        }
        else if (Zukan9a.IsMegaFormZA(species, SAV.SaveRevision))
        {
            Show(MegaNames.Regular, MegaNames.Z, null);
        }
        else if (species is (int)Species.Meowstic)
        {
            Show(MegaNames.MeowsticM, MegaNames.MeowsticF, null);
        }
        else if (species is (int)Species.Magearna)
        {
            Show(MegaNames.Magearna0, MegaNames.Magearna1, null);
        }
        else if (species is (int)Species.Tatsugiri)
        {
            Show(MegaNames.Tatsu0, MegaNames.Tatsu1, MegaNames.Tatsu2);
        }
        else
        {
            Show(MegaNames.Regular, null, null);
        }

        void Show(string? mega0, string? mega1, string? mega2)
        {
            Apply(CHK_SeenMega0, mega0);
            Apply(CHK_SeenMega1, mega1);
            Apply(CHK_SeenMega2, mega2);
        }

        static void Apply(CheckBox chk, string? text)
        {
            chk.IsVisible = text is not null;
            if (text is not null)
                chk.Content = text;
        }
    }

    private static string[] GetFormList(ushort species)
    {
        var s = GameInfo.Strings;
        var result = new string[FormCount];
        var regular = FormConverter.GetFormList(species, s.Types, s.forms, GameInfo.GenderSymbolASCII, EntityContext.Gen9a);
        for (int i = 0; i < regular.Length; i++)
            result[i] = regular[i];
        for (int i = regular.Length; i < result.Length; i++)
            result[i] = $"{i:00} - N/A";
        return result;
    }

    private void SetEntry(int index)
    {
        if (Loading || index < 0)
            return;
        SetEntry(GetSpecies(index));
    }

    private void SetEntry(ushort species)
    {
        var entry = SAV.Zukan.GetEntry(species);
        entry.SetDisplayIsNew(CHK_IsNew.IsChecked == true);
        entry.SetIsGenderSeen(0, CHK_SeenMale.IsChecked == true);
        entry.SetIsGenderSeen(1, CHK_SeenFemale.IsChecked == true);
        entry.SetIsGenderSeen(2, CHK_SeenGenderless.IsChecked == true);
        entry.SetIsSeenAlpha(CHK_SeenAlpha.IsChecked == true);

        entry.SetIsSeenMega(0, CHK_SeenMega0.IsChecked == true);
        if (Zukan9a.IsMegaFormXY(species, SAV.SaveRevision) || Zukan9a.IsMegaFormZA(species, SAV.SaveRevision) || species is (int)Species.Magearna or (int)Species.Meowstic)
        {
            entry.SetIsSeenMega(1, CHK_SeenMega1.IsChecked == true);
        }
        else if (species == (int)Species.Tatsugiri)
        {
            entry.SetIsSeenMega(1, CHK_SeenMega1.IsChecked == true);
            entry.SetIsSeenMega(2, CHK_SeenMega2.IsChecked == true);
        }

        for (byte i = 0; i < FormCount; i++)
        {
            entry.SetIsFormSeen(i, CLB_FormSeen.GetItemChecked(i));
            entry.SetIsFormCaught(i, CLB_FormCaught.GetItemChecked(i));
            entry.SetIsShinySeen(i, CLB_FormShiny.GetItemChecked(i));
        }

        entry.DisplayForm = (byte)Math.Max(0, CB_DisplayForm.SelectedIndex);
        entry.DisplayGender = (DisplayGender9a)Math.Max(0, CB_Gender.SelectedIndex);
        entry.SetDisplayIsShiny(CHK_DisplayShiny.IsChecked == true);

        entry.SetLanguageFlag((int)LanguageID.Japanese, CHK_LangJPN.IsChecked == true);
        entry.SetLanguageFlag((int)LanguageID.English, CHK_LangENG.IsChecked == true);
        entry.SetLanguageFlag((int)LanguageID.French, CHK_LangFRE.IsChecked == true);
        entry.SetLanguageFlag((int)LanguageID.Italian, CHK_LangITA.IsChecked == true);
        entry.SetLanguageFlag((int)LanguageID.German, CHK_LangGER.IsChecked == true);
        entry.SetLanguageFlag((int)LanguageID.Spanish, CHK_LangSPA.IsChecked == true);
        entry.SetLanguageFlag((int)LanguageID.Korean, CHK_LangKOR.IsChecked == true);
        entry.SetLanguageFlag((int)LanguageID.ChineseS, CHK_LangCHS.IsChecked == true);
        entry.SetLanguageFlag((int)LanguageID.ChineseT, CHK_LangCHT.IsChecked == true);
        entry.SetLanguageFlag((int)LanguageID.SpanishL, CHK_LangLATAM.IsChecked == true);
    }

    protected override void OnSave()
    {
        SetEntry(lastIndex);
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
