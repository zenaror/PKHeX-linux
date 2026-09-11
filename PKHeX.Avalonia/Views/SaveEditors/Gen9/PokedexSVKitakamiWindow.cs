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
/// Dex editor for the Scarlet / Violet DLC dexes (port of the WinForms <c>SAV_PokedexSVKitakami</c>).
/// </summary>
/// <remarks>
/// The Teal Mask block records four separate form bit sets (seen, obtained, heard of, viewed) and keeps one
/// displayed form, gender and shiny flag per regional dex, so each region has its own group here.
/// </remarks>
public sealed class PokedexSVKitakamiWindow : SaveEditorWindow
{
    private const int FormBits = sizeof(uint) * 8;

    private static readonly LanguageID[] Languages =
    [
        LanguageID.Japanese, LanguageID.English, LanguageID.French, LanguageID.Italian, LanguageID.German,
        LanguageID.Spanish, LanguageID.Korean, LanguageID.ChineseS, LanguageID.ChineseT,
    ];

    private readonly SAV9SV Origin;
    private readonly SAV9SV SAV;
    private readonly Zukan9 Dex;
    private readonly DexMap[] ListBoxToSpecies;
    private readonly CheckBox[] CL;

    private int lastIndex;
    private readonly bool CanSave;
    private readonly bool Loading;
    private bool IgnoreChangeEvent;

    private readonly ListBox LB_Species = new() { Name = "LB_Species", Width = 250, Height = 440 };
    private readonly ObservableCollection<string> SpeciesItems = [];
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 190);

    private readonly CheckedListView CLB_FormSeen = new() { Name = "CLB_FormSeen", Width = 180, Height = 180 };
    private readonly CheckedListView CLB_FormObtained = new() { Name = "CLB_FormObtained", Width = 180, Height = 180 };
    private readonly CheckedListView CLB_FormHeard = new() { Name = "CLB_FormHeard", Width = 180, Height = 180 };
    private readonly CheckedListView CLB_FormViewed = new() { Name = "CLB_FormViewed", Width = 180, Height = 180 };

    private readonly CheckBox CHK_SeenMale = UiFactory.Check("CHK_SeenMale", "Male");
    private readonly CheckBox CHK_SeenFemale = UiFactory.Check("CHK_SeenFemale", "Female");
    private readonly CheckBox CHK_SeenGenderless = UiFactory.Check("CHK_SeenGenderless", "Genderless");
    private readonly CheckBox CHK_SeenShiny = UiFactory.Check("CHK_SeenShiny", "Shiny");

    private readonly ComboBox CB_PaldeaForm = UiFactory.StringCombo("CB_PaldeaForm", 160);
    private readonly ComboBox CB_PaldeaGender = UiFactory.StringCombo("CB_PaldeaGender", 130);
    private readonly CheckBox CHK_PaldeaShiny = UiFactory.Check("CHK_PaldeaShiny", "Shiny");
    private readonly ComboBox CB_KitakamiForm = UiFactory.StringCombo("CB_KitakamiForm", 160);
    private readonly ComboBox CB_KitakamiGender = UiFactory.StringCombo("CB_KitakamiGender", 130);
    private readonly CheckBox CHK_KitakamiShiny = UiFactory.Check("CHK_KitakamiShiny", "Shiny");
    private readonly ComboBox CB_BlueberryForm = UiFactory.StringCombo("CB_BlueberryForm", 160);
    private readonly ComboBox CB_BlueberryGender = UiFactory.StringCombo("CB_BlueberryGender", 130);
    private readonly CheckBox CHK_BlueberryShiny = UiFactory.Check("CHK_BlueberryShiny", "Shiny");
    private GroupBoxView GB_Paldea = null!, GB_Kitakami = null!, GB_Blueberry = null!;

    private readonly Button B_GiveAll = UiFactory.Button("B_GiveAll", "Check All");
    private readonly Button B_Modify = UiFactory.Button("B_Modify", "Modify...");

    public PokedexSVKitakamiWindow(SAV9SV sav) : base("SAV_PokedexSVKitakami", "Pokédex Editor")
    {
        SAV = (SAV9SV)(Origin = sav).Clone();
        Dex = SAV.Blocks.Zukan;

        CL = [
            UiFactory.Check("CHK_LangJPN", "Japanese"), UiFactory.Check("CHK_LangENG", "English"),
            UiFactory.Check("CHK_LangFRE", "French"), UiFactory.Check("CHK_LangITA", "Italian"),
            UiFactory.Check("CHK_LangGER", "German"), UiFactory.Check("CHK_LangSPA", "Spanish"),
            UiFactory.Check("CHK_LangKOR", "Korean"), UiFactory.Check("CHK_LangCHS", "ChineseS"),
            UiFactory.Check("CHK_LangCHT", "ChineseT"),
        ];

        Loading = true;
        BuildLayout();

        var species = GameInfo.FilteredSources.Species;
        CB_Species.SetItems(species);
        CB_Species.SelectedIndex = 0; // the WinForms binding shows the first entry; the list handler never syncs it

        var list = species.Select(z => new DexMap(z))
            .OrderByDescending(z => z.IsInAnyDex)
            .ThenBy(z => z.Dex)
            .ToArray();
        for (var i = 0; i < list.Length; i++)
        {
            var n = list[i];
            SpeciesItems.Add($"{n.GetDexString()} - {n.Name}");
            n.ListIndex = i;
        }
        ListBoxToSpecies = list;

        LB_Species.SelectedIndex = 0;
        Loading = false;
        CanSave = true;
        lastIndex = 0;
        GetEntry(0);
    }

    private void BuildLayout()
    {
        LB_Species.ItemsSource = SpeciesItems;
        foreach (var cb in new[] { CB_PaldeaGender, CB_KitakamiGender, CB_BlueberryGender })
        {
            foreach (var s in new[] { "Male", "Female", "Genderless" })
                cb.Items.Add(s);
        }

        var formLists = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        formLists.Children.Add(UiFactory.Column(UiFactory.Label("L_FormSeen", "Seen"), CLB_FormSeen));
        formLists.Children.Add(UiFactory.Column(UiFactory.Label("L_FormObtained", "Obtained"), CLB_FormObtained));
        formLists.Children.Add(UiFactory.Column(UiFactory.Label("L_FormHeard", "Heard Of"), CLB_FormHeard));
        formLists.Children.Add(UiFactory.Column(UiFactory.Label("L_FormViewed", "Viewed"), CLB_FormViewed));

        GB_Paldea = new GroupBoxView("GB_Paldea", "Paldea", UiFactory.Column(CB_PaldeaForm, CB_PaldeaGender, CHK_PaldeaShiny));
        GB_Kitakami = new GroupBoxView("GB_Kitakami", "Kitakami", UiFactory.Column(CB_KitakamiForm, CB_KitakamiGender, CHK_KitakamiShiny));
        GB_Blueberry = new GroupBoxView("GB_Blueberry", "Blueberry", UiFactory.Column(CB_BlueberryForm, CB_BlueberryGender, CHK_BlueberryShiny));
        var regions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        regions.Children.Add(GB_Paldea);
        regions.Children.Add(GB_Kitakami);
        regions.Children.Add(GB_Blueberry);

        var right = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_goto", "goto:"), CB_Species),
            new GroupBoxView("GB_Seen", "Seen", UiFactory.Row(CHK_SeenMale, CHK_SeenFemale, CHK_SeenGenderless, CHK_SeenShiny)),
            formLists,
            regions,
            new GroupBoxView("GB_Language", "Languages", UiFactory.Row(CL)),
            UiFactory.Row(B_GiveAll, B_Modify));

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        body.Children.Add(LB_Species);
        body.Children.Add(new ScrollViewer { Content = right, MaxHeight = 640 });
        SetBody(body);

        LB_Species.SelectionChanged += (_, _) => ChangeLBSpecies();
        CB_Species.SelectionChanged += (_, _) => ChangeCBSpecies();
        B_GiveAll.AttachClickHandled(mods =>
        {
            SetEntry(lastIndex);
            var species = GetSpecies(lastIndex);
            Dex.SetDexEntryAll(species, mods == KeyModifiers.Shift);
            GetEntry(species);
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

    #region Selection

    private ushort GetSpecies(int listBoxIndex) => Array.Find(ListBoxToSpecies, z => z.ListIndex == listBoxIndex)?.Species ?? 0;
    private int GetIndex(ushort species) => Array.Find(ListBoxToSpecies, z => z.Species == species)?.ListIndex ?? 0;

    private void ChangeCBSpecies()
    {
        if (Loading || IgnoreChangeEvent || CB_Species.GetSelectedItem() is not { } item)
            return;
        var index = GetIndex((ushort)item.Value);
        if (LB_Species.SelectedIndex != index)
            LB_Species.SelectedIndex = index; // triggers the list handler
    }

    private void ChangeLBSpecies()
    {
        if (Loading || IgnoreChangeEvent || LB_Species.SelectedIndex < 0)
            return;
        SetEntry(lastIndex);
        lastIndex = LB_Species.SelectedIndex;
        GetEntry(lastIndex);

        IgnoreChangeEvent = true;
        CB_Species.SetValue(GetSpecies(lastIndex));
        IgnoreChangeEvent = false;
    }

    #endregion

    #region Entry read / write

    private void GetEntry(int index)
    {
        if (!CanSave || Loading || index < 0)
            return;
        GetEntry(GetSpecies(index));
    }

    private void GetEntry(ushort species)
    {
        var entry = SAV.Zukan.DexKitakami.Get(species);
        var forms = GetFormList(species);
        if (forms[0].Length == 0)
            forms[0] = GameInfo.Strings.Types[0];

        foreach (var cb in new[] { CB_PaldeaForm, CB_KitakamiForm, CB_BlueberryForm })
        {
            cb.Items.Clear();
            foreach (var f in forms)
                cb.Items.Add(f);
        }

        CLB_FormSeen.ClearItems();
        CLB_FormObtained.ClearItems();
        CLB_FormHeard.ClearItems();
        CLB_FormViewed.ClearItems();
        for (byte i = 0; i < FormBits; i++)
        {
            var name = i < forms.Length ? forms[i] : $"--{i:00}--";
            CLB_FormSeen.Add(name, entry.GetSeenForm(i));
            CLB_FormObtained.Add(name, entry.GetObtainedForm(i));
            CLB_FormHeard.Add(name, entry.GetHeardForm(i));
            CLB_FormViewed.Add(name, entry.GetCheckedForm(i));
        }

        CHK_SeenMale.IsChecked = entry.GetIsGenderSeen(0);
        CHK_SeenFemale.IsChecked = entry.GetIsGenderSeen(1);
        CHK_SeenGenderless.IsChecked = entry.GetIsGenderSeen(2);
        CHK_SeenShiny.IsChecked = entry.GetIsModelSeen(true);

        for (int i = 0; i < CL.Length; i++)
            CL[i].IsChecked = entry.GetLanguageFlag((int)Languages[i]);

        SetRegion(CB_PaldeaForm, CB_PaldeaGender, CHK_PaldeaShiny, entry.DisplayedPaldeaForm, entry.DisplayedPaldeaGender, entry.DisplayedPaldeaShiny);
        SetRegion(CB_KitakamiForm, CB_KitakamiGender, CHK_KitakamiShiny, entry.DisplayedKitakamiForm, entry.DisplayedKitakamiGender, entry.DisplayedKitakamiShiny);
        SetRegion(CB_BlueberryForm, CB_BlueberryGender, CHK_BlueberryShiny, entry.DisplayedBlueberryForm, entry.DisplayedBlueberryGender, entry.DisplayedBlueberryShiny);

        // Only the dexes this species belongs to are editable.
        var pi = SAV.Personal[species];
        bool paldea = false, kitakami = false, blueberry = false;
        for (byte i = 0; i < pi.FormCount; i++)
        {
            var form = SAV.Personal.GetFormEntry(species, i);
            paldea |= form.DexPaldea != 0;
            kitakami |= form.DexKitakami != 0;
            blueberry |= form.DexBlueberry != 0;
        }
        GB_Paldea.IsEnabled = paldea;
        GB_Kitakami.IsEnabled = kitakami;
        GB_Blueberry.IsEnabled = blueberry;

        static void SetRegion(ComboBox form, ComboBox gender, CheckBox shiny, int formIndex, int genderIndex, int shinyValue)
        {
            form.SelectedIndex = Math.Clamp(formIndex, 0, Math.Max(0, form.ItemCount - 1));
            gender.SelectedIndex = Math.Clamp(genderIndex, 0, Math.Max(0, gender.ItemCount - 1));
            shiny.IsChecked = shinyValue != 0;
        }
    }

    private static string[] GetFormList(ushort species)
    {
        // Alcremie's formarg forms are not stored as bit flags here, so no special handling is needed.
        var s = GameInfo.Strings;
        return FormConverter.GetFormList(species, s.Types, s.forms, GameInfo.GenderSymbolASCII, EntityContext.Gen9);
    }

    private void SetEntry(int index)
    {
        if (!CanSave || Loading || index < 0)
            return;
        SetEntry(GetSpecies(index));
    }

    private void SetEntry(ushort species)
    {
        var entry = SAV.Zukan.DexKitakami.Get(species);

        for (byte i = 0; i < FormBits && i < CLB_FormSeen.Count; i++)
        {
            entry.SetSeenForm(i, CLB_FormSeen.GetItemChecked(i));
            entry.SetObtainedForm(i, CLB_FormObtained.GetItemChecked(i));
            entry.SetHeardForm(i, CLB_FormHeard.GetItemChecked(i));
            entry.SetCheckedForm(i, CLB_FormViewed.GetItemChecked(i));
        }

        entry.SetIsGenderSeen(0, CHK_SeenMale.IsChecked == true);
        entry.SetIsGenderSeen(1, CHK_SeenFemale.IsChecked == true);
        entry.SetIsGenderSeen(2, CHK_SeenGenderless.IsChecked == true);
        entry.SetIsModelSeen(true, CHK_SeenShiny.IsChecked == true);

        for (int i = 0; i < CL.Length; i++)
            entry.SetLanguageFlag((int)Languages[i], CL[i].IsChecked == true);

        entry.SetLocalPaldea((byte)Math.Max(0, CB_PaldeaForm.SelectedIndex), (byte)Math.Max(0, CB_PaldeaGender.SelectedIndex), CHK_PaldeaShiny.IsChecked == true ? (byte)1 : (byte)0);
        entry.SetLocalKitakami((byte)Math.Max(0, CB_KitakamiForm.SelectedIndex), (byte)Math.Max(0, CB_KitakamiGender.SelectedIndex), CHK_KitakamiShiny.IsChecked == true ? (byte)1 : (byte)0);
        entry.SetLocalBlueberry((byte)Math.Max(0, CB_BlueberryForm.SelectedIndex), (byte)Math.Max(0, CB_BlueberryGender.SelectedIndex), CHK_BlueberryShiny.IsChecked == true ? (byte)1 : (byte)0);
    }

    #endregion

    protected override void OnSave()
    {
        SetEntry(lastIndex);
        Origin.CopyChangesFrom(SAV);
        Close();
    }

    /// <summary>Where one species sits across the three regional dexes, used to order the list.</summary>
    private sealed record DexMap
    {
        public ushort Species { get; }
        public bool IsInAnyDex => Dex != default;
        public (int Group, int Index) Dex { get; }
        public string Name { get; }
        public int ListIndex { get; set; }

        public DexMap(ComboItem c)
        {
            Species = (ushort)c.Value;
            Name = c.Text;
            Dex = GetDexIndex(Species);
        }

        private static (int Group, int Index) GetDexIndex(ushort species)
        {
            var entry = PersonalTable.SV.GetFormEntry(species, 0);
            for (byte i = 0; i < entry.FormCount; i++)
            {
                entry = PersonalTable.SV.GetFormEntry(species, i);
                if (entry.DexPaldea != 0)
                    return (1, entry.DexPaldea);
                if (entry.DexKitakami != 0)
                    return (2, entry.DexKitakami);
                if (entry.DexBlueberry != 0)
                    return (3, entry.DexBlueberry);
            }
            return default;
        }

        public string GetDexString()
        {
            if (!IsInAnyDex)
                return "***";
            var prefix = Dex.Group switch { 1 => "P", 2 => "K", 3 => "B", _ => "?" };
            return $"{prefix}-{Dex.Index:000}";
        }
    }
}
