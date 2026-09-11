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
/// Pokédex editor for Scarlet / Violet (port of the WinForms <c>SAV_PokedexSV</c>).
/// </summary>
/// <remarks>
/// The list is ordered by dex membership: species in the Paldea, Kitakami or Blueberry dex come first with a
/// prefixed number, and anything outside every dex is shown with <c>***</c>.
/// </remarks>
public sealed class PokedexSVWindow : SaveEditorWindow
{
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

    private readonly ListBox LB_Species = new() { Name = "LB_Species", Width = 250, Height = 420 };
    private readonly ObservableCollection<string> SpeciesItems = [];
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 190);
    private readonly ComboBox CB_State = UiFactory.StringCombo("CB_State", 140);
    private readonly ComboBox CB_Gender = UiFactory.StringCombo("CB_Gender", 130);
    private readonly ComboBox CB_DisplayForm = UiFactory.StringCombo("CB_DisplayForm", 170);
    private readonly CheckBox CHK_IsNew = UiFactory.Check("CHK_IsNew", "New");
    private readonly CheckBox CHK_SeenMale = UiFactory.Check("CHK_SeenMale", "Male");
    private readonly CheckBox CHK_SeenFemale = UiFactory.Check("CHK_SeenFemale", "Female");
    private readonly CheckBox CHK_SeenGenderless = UiFactory.Check("CHK_SeenGenderless", "Genderless");
    private readonly CheckBox CHK_SeenShiny = UiFactory.Check("CHK_SeenShiny", "Shiny");
    private readonly CheckBox CHK_DisplayShiny = UiFactory.Check("CHK_DisplayShiny", "Display Shiny");
    private readonly CheckBox CHK_G = UiFactory.Check("CHK_G", "Display Other Gender");
    private readonly CheckedListView CLB_FormSeen = new() { Name = "CLB_FormSeen", Width = 220, Height = 200 };
    private readonly Button B_GiveAll = UiFactory.Button("B_GiveAll", "Check All");
    private readonly Button B_Modify = UiFactory.Button("B_Modify", "Modify...");

    public PokedexSVWindow(SAV9SV sav) : base("SAV_PokedexSV", "Pokédex Editor")
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

        const int maxSpecies = (int)Species.IronLeaves; // 1010, before the DLC species
        var species = GameInfo.FilteredSources.Species.Where(z => z.Value <= maxSpecies).ToArray();
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
        foreach (var s in new[] { "None", "Heard Of", "Seen", "Captured" })
            CB_State.Items.Add(s);
        foreach (var s in new[] { "Male", "Female", "Genderless" })
            CB_Gender.Items.Add(s);

        var seen = new GroupBoxView("GB_Seen", "Seen", UiFactory.Column(CHK_SeenMale, CHK_SeenFemale, CHK_SeenGenderless, CHK_SeenShiny));
        var display = new GroupBoxView("GB_Display", "Displayed", UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_DisplayForm", "Form:"), CB_DisplayForm),
            UiFactory.Row(UiFactory.Label("L_Gender", "Gender:"), CB_Gender),
            CHK_DisplayShiny, CHK_G, CHK_IsNew));

        var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        top.Children.Add(seen);
        top.Children.Add(display);

        var right = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_goto", "goto:"), CB_Species),
            UiFactory.Row(UiFactory.Label("L_State", "State:"), CB_State),
            top,
            UiFactory.Column(UiFactory.Label("L_FormSeen", "Forms Seen"), CLB_FormSeen),
            new GroupBoxView("GB_Language", "Languages", UiFactory.Row(CL)),
            UiFactory.Row(B_GiveAll, B_Modify));

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        body.Children.Add(LB_Species);
        body.Children.Add(new ScrollViewer { Content = right, MaxHeight = 620 });
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
        if (Loading || CB_Species.GetSelectedItem() is not { } item)
            return;
        var index = GetIndex((ushort)item.Value);
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
        if (!CanSave || Loading || index < 0)
            return;
        GetEntry(GetSpecies(index));
    }

    private void GetEntry(ushort species)
    {
        var entry = SAV.Zukan.DexPaldea.Get(species);
        var forms = GetFormList(species);
        if (forms[0].Length == 0)
            forms[0] = GameInfo.Strings.Types[0];

        CB_State.SelectedIndex = (int)entry.GetState();
        CHK_IsNew.IsChecked = entry.GetDisplayIsNew();

        CHK_SeenMale.IsChecked = entry.GetIsGenderSeen(0);
        CHK_SeenFemale.IsChecked = entry.GetIsGenderSeen(1);
        CHK_SeenGenderless.IsChecked = entry.GetIsGenderSeen(2);
        CHK_SeenShiny.IsChecked = entry.GetSeenIsShiny();

        CLB_FormSeen.ClearItems();
        for (byte i = 0; i < forms.Length; i++)
            CLB_FormSeen.Add(forms[i], entry.GetIsFormSeen(i));

        CB_DisplayForm.Items.Clear();
        foreach (var f in forms)
            CB_DisplayForm.Items.Add(f);
        CB_Gender.SelectedIndex = (int)entry.GetDisplayGender();
        CHK_DisplayShiny.IsChecked = entry.GetDisplayIsShiny();
        CHK_G.IsChecked = entry.GetDisplayGenderIsDifferent();
        CB_DisplayForm.SelectedIndex = Math.Clamp((int)entry.GetDisplayForm(), 0, Math.Max(0, CB_DisplayForm.ItemCount - 1));

        for (int i = 0; i < CL.Length; i++)
            CL[i].IsChecked = entry.GetLanguageFlag((int)Languages[i]);
    }

    private static string[] GetFormList(ushort species)
    {
        var s = GameInfo.Strings;
        if (species == (int)Species.Alcremie)
            return FormConverter.GetAlcremieFormList(s.forms);
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
        var entry = SAV.Zukan.DexPaldea.Get(species);
        entry.SetState((uint)Math.Max(0, CB_State.SelectedIndex));
        entry.SetDisplayIsNew(CHK_IsNew.IsChecked == true);

        entry.SetIsGenderSeen(0, CHK_SeenMale.IsChecked == true);
        entry.SetIsGenderSeen(1, CHK_SeenFemale.IsChecked == true);
        entry.SetIsGenderSeen(2, CHK_SeenGenderless.IsChecked == true);
        entry.SetSeenIsShiny(CHK_SeenShiny.IsChecked == true);

        for (byte i = 0; i < CLB_FormSeen.Count; i++)
            entry.SetIsFormSeen(i, CLB_FormSeen.GetItemChecked(i));

        entry.SetDisplayGender(Math.Max(0, CB_Gender.SelectedIndex));
        entry.SetDisplayIsShiny(CHK_DisplayShiny.IsChecked == true);
        entry.SetDisplayGenderIsDifferent(CHK_G.IsChecked == true);
        entry.SetDisplayForm((uint)Math.Max(0, CB_DisplayForm.SelectedIndex));

        for (int i = 0; i < CL.Length; i++)
            entry.SetLanguageFlag((int)Languages[i], CL[i].IsChecked == true);
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
