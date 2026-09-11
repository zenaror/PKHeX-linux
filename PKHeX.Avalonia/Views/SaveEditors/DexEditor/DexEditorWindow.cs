using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.DexEditor;

/// <summary>
/// Pokédex editor shared by the Generation 5 and Generation 6 dex blocks
/// (port of the WinForms <c>SAV_Pokedex5</c>, <c>SAV_PokedexXY</c> and <c>SAV_PokedexORAS</c>, which share this layout).
/// </summary>
/// <remarks>
/// These generations track four seen states per species (male, female and their shiny variants), which of those the dex
/// currently displays, the languages the entry was recorded in, and per-form seen/displayed flags. The generation-specific
/// extras (foreign-origin flag, DexNav counters) come from <see cref="IDexSource"/>.
/// <para>Deviation: the WinForms X/Y form indexes the form check lists with the form's <c>TabIndex</c> instead of the
/// dex form index, which reads the wrong flags for any species whose forms are not first in the table. This port uses
/// the dex form index, matching the OR/AS and Generation 5 forms.</para>
/// </remarks>
public class DexEditorWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SaveFile SAV;
    private readonly IDexSource Dex;

    private readonly ListBox LB_Species = new() { Name = "LB_Species", Width = 220, Height = 420 };
    private readonly ObservableCollection<string> SpeciesItems = [];
    private readonly TextBlock L_goto = UiFactory.Label("L_goto", "goto:");
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);

    private readonly CheckBox[] CP;
    private readonly CheckBox[] CL;
    private readonly CheckBox CHK_F1 = UiFactory.Check("CHK_F1", "Foreign");
    private readonly CheckedListView CLB_FormsSeen = new() { Name = "CLB_FormsSeen", Width = 190, Height = 170 };
    private readonly CheckedListView CLB_FormDisplayed = new() { Name = "CLB_FormDisplayed", Width = 190, Height = 170 };

    private readonly CheckBox CHK_NationalDexUnlocked = UiFactory.Check("CHK_NationalDexUnlocked", "National Mode Unlocked");
    private readonly CheckBox CHK_NationalDexActive = UiFactory.Check("CHK_NationalDexActive", "National Mode Active");
    private readonly TextBlock L_Spinda = UiFactory.Label("L_Spinda", "Spinda:");
    private readonly NumericTextBox TB_Spinda = UiFactory.Numeric("TB_Spinda", 8, 90, hex: true);
    private readonly TextBlock L_Seen = UiFactory.Label("L_Seen", "Seen:");
    private readonly NumericTextBox MT_Seen = UiFactory.Numeric("MT_Seen", 5, 70);
    private readonly TextBlock L_Obtained = UiFactory.Label("L_Obtained", "Obtained:");
    private readonly NumericTextBox MT_Obtained = UiFactory.Numeric("MT_Obtained", 5, 70);
    private StackPanel CountRow = null!;

    private readonly Button B_GiveAll = UiFactory.Button("B_GiveAll", "Check All");
    private readonly Button B_Modify = UiFactory.Button("B_Modify", "Modify...");
    private readonly Button B_ModifyForms = UiFactory.Button("B_ModifyForms", "Modify...");

    private GroupBoxView GB_Language = null!;
    private StackPanel SpindaRow = null!;
    private bool editing;
    private ushort species = ushort.MaxValue;

    protected DexEditorWindow(SaveFile sav, SaveFile clone, IDexSource dex, string formName) : base(formName, "Pokédex Editor")
    {
        Origin = sav;
        SAV = clone;
        Dex = dex;

        CP = [
            UiFactory.Check("CHK_P1", "Owned"),
            UiFactory.Check("CHK_P2", "Male"), UiFactory.Check("CHK_P3", "Female"),
            UiFactory.Check("CHK_P4", "Shiny Male"), UiFactory.Check("CHK_P5", "Shiny Female"),
            UiFactory.Check("CHK_P6", "Male"), UiFactory.Check("CHK_P7", "Female"),
            UiFactory.Check("CHK_P8", "Shiny Male"), UiFactory.Check("CHK_P9", "Shiny Female"),
        ];
        CL = [
            UiFactory.Check("CHK_L1", "Japanese"), UiFactory.Check("CHK_L2", "English"),
            UiFactory.Check("CHK_L3", "French"), UiFactory.Check("CHK_L4", "Italian"),
            UiFactory.Check("CHK_L5", "German"), UiFactory.Check("CHK_L6", "Spanish"),
            UiFactory.Check("CHK_L7", "Korean"),
        ];

        BuildLayout();

        editing = true;
        CB_Species.SetItems(GameInfo.FilteredSources.Species.Skip(1).ToList());
        for (int i = 1; i < SAV.MaxSpeciesID + 1; i++)
            SpeciesItems.Add($"{i:000} - {GameInfo.Strings.Species[i]}");

        CHK_NationalDexUnlocked.IsChecked = Dex.IsNationalDexUnlocked;
        CHK_NationalDexActive.IsChecked = Dex.IsNationalDexMode;
        CHK_NationalDexUnlocked.IsCheckedChanged += (_, _) => CHK_NationalDexActive.IsChecked = CHK_NationalDexUnlocked.IsChecked;
        TB_Spinda.Text = Dex.Spinda.ToString("X8");
        editing = false;

        if (Dex.InitialSpecies is not 0 && Dex.InitialSpecies <= Dex.MaxSpecies)
            CB_Species.SetValue(Dex.InitialSpecies);
        else
            LB_Species.SelectedIndex = 0;
    }

    private void BuildLayout()
    {
        LB_Species.ItemsSource = SpeciesItems;

        var owned = new GroupBoxView("GB_Owned", "Owned", UiFactory.Column(CP[0], CHK_F1));
        var seen = new GroupBoxView("GB_Encountered", "Seen", UiFactory.Column(CP[1], CP[2], CP[3], CP[4]));
        var displayed = new GroupBoxView("GB_Displayed", "Displayed", UiFactory.Column(CP[5], CP[6], CP[7], CP[8]));
        GB_Language = new GroupBoxView("GB_Language", "Languages", UiFactory.Column(CL));
        CHK_F1.IsVisible = Dex.SupportsForeignFlag;

        var flags = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        flags.Children.Add(UiFactory.Column(owned, seen, displayed));
        flags.Children.Add(GB_Language);

        var forms = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        forms.Children.Add(UiFactory.Column(UiFactory.Label("L_FormsSeen", "Seen Forms:"), CLB_FormsSeen));
        forms.Children.Add(UiFactory.Column(UiFactory.Label("L_FormsDisplayed", "Forms Displayed"), CLB_FormDisplayed));

        SpindaRow = UiFactory.Row(L_Spinda, TB_Spinda);
        CountRow = UiFactory.Row(L_Seen, MT_Seen, L_Obtained, MT_Obtained);
        CountRow.IsVisible = Dex.SupportsCounts;

        var right = UiFactory.Column(
            UiFactory.Row(L_goto, CB_Species),
            flags,
            CountRow,
            UiFactory.Row(B_GiveAll, B_Modify),
            forms,
            B_ModifyForms,
            CHK_NationalDexUnlocked,
            CHK_NationalDexActive,
            SpindaRow);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        body.Children.Add(LB_Species);
        body.Children.Add(new ScrollViewer { Content = right, MaxHeight = 620 });
        SetBody(body);

        LB_Species.SelectionChanged += (_, _) => ChangeLBSpecies();
        CB_Species.SelectionChanged += (_, _) => ChangeCBSpecies();
        foreach (var c in CP.Skip(5))
            c.IsCheckedChanged += (s, _) => ChangeDisplayed((CheckBox)s!);
        for (int i = 1; i <= 4; i++)
        {
            var c = CP[i];
            c.IsCheckedChanged += (_, _) => ChangeEncountered(c);
        }
        CLB_FormDisplayed.ItemCheckChanged += UpdateDisplayedForm;

        B_GiveAll.AttachClickHandled(ClickGiveAll);
        B_Modify.Flyout = BuildModifyMenu();
        B_ModifyForms.Flyout = BuildModifyFormsMenu();
    }

    private MenuFlyout BuildModifyMenu()
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.Bottom };
        AddModifyItem(flyout, "mnuSeenNone", "Seen none", _ => Dex.SeenNone());
        AddModifyItem(flyout, "mnuSeenAll", "Seen all", mods => Dex.SeenAll(shinyToo: mods.HasFlag(KeyModifiers.Shift)));
        AddModifyItem(flyout, "mnuCaughtNone", "Caught none", _ => Dex.CaughtNone());
        AddModifyItem(flyout, "mnuCaughtAll", "Caught all", mods => Dex.CaughtAll((LanguageID)SAV.Language, allLanguages: mods.HasFlag(KeyModifiers.Control)));
        AddModifyItem(flyout, "mnuComplete", "Complete Dex", mods =>
        {
            Dex.SeenAll(shinyToo: mods.HasFlag(KeyModifiers.Shift));
            Dex.CaughtAll((LanguageID)SAV.Language, allLanguages: mods.HasFlag(KeyModifiers.Control));
        });
        if (Dex.SupportsCounts)
            AddModifyItem(flyout, "mnuDexNav", "Max DexNav", _ => Dex.SetAllCountSeen(999));
        return flyout;
    }

    private MenuFlyout BuildModifyFormsMenu()
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.Bottom };
        AddModifyItem(flyout, "mnuFormNone", "Seen none", _ => Dex.ClearFormSeen());
        AddModifyItem(flyout, "mnuForm1", "Seen one", mods => Dex.SetFormsSeen1(shinyToo: mods.HasFlag(KeyModifiers.Shift)));
        AddModifyItem(flyout, "mnuFormAll", "Seen all", mods => Dex.SetFormsSeen(shinyToo: mods.HasFlag(KeyModifiers.Shift)));
        return flyout;
    }

    private void AddModifyItem(MenuFlyout flyout, string name, string header, Action<KeyModifiers> action)
    {
        var item = new MenuItem { Name = name, Header = header };
        item.Click += (_, _) =>
        {
            SetEntry();
            action(MainWindow.CurrentModifiers);
            GetEntry(skipFormRepop: true);
        };
        flyout.Items.Add(item);
    }

    private void ClickGiveAll(KeyModifiers mods)
    {
        SetEntry();
        var language = (LanguageID)SAV.Language;
        Dex.GiveAll(species, mods != KeyModifiers.Alt, mods.HasFlag(KeyModifiers.Shift), language, mods.HasFlag(KeyModifiers.Control));
        GetEntry(skipFormRepop: true);
    }

    #region Selection

    private void ChangeCBSpecies()
    {
        if (editing)
            return;
        SetEntry();

        editing = true;
        species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
        if (species >= 1)
            LB_Species.SelectedIndex = species - 1; // index 0 is not offered in the combo box
        LB_Species.ScrollIntoView(LB_Species.SelectedIndex);
        GetEntry();
        editing = false;
    }

    private void ChangeLBSpecies()
    {
        if (editing || LB_Species.SelectedIndex < 0)
            return;
        SetEntry();

        editing = true;
        species = (ushort)(LB_Species.SelectedIndex + 1);
        CB_Species.SetValue(species);
        GetEntry();
        editing = false;
    }

    /// <summary>Only one gender/shiny variant can be the displayed one.</summary>
    private void ChangeDisplayed(CheckBox sender)
    {
        if (sender.IsChecked != true)
            return;

        for (int i = 5; i < CP.Length; i++)
            CP[i].IsChecked = ReferenceEquals(CP[i], sender);

        for (int i = 0; i < 4; i++)
        {
            if (CP[i + 5].IsChecked == true)
                CP[i + 1].IsChecked = true;
        }
    }

    /// <summary>Keeps the displayed flags consistent with the seen flags.</summary>
    private void ChangeEncountered(CheckBox sender)
    {
        bool anySeen = CP[1].IsChecked == true || CP[2].IsChecked == true || CP[3].IsChecked == true || CP[4].IsChecked == true;
        if (!anySeen)
        {
            for (int i = 5; i < CP.Length; i++)
                CP[i].IsChecked = false;
            return;
        }

        bool anyDisplayed = CP[5].IsChecked == true || CP[6].IsChecked == true || CP[7].IsChecked == true || CP[8].IsChecked == true;
        if (anyDisplayed)
            return;

        for (int i = 1; i <= 4; i++)
        {
            if (ReferenceEquals(CP[i], sender) && sender.IsChecked == true)
            {
                CP[i + 4].IsChecked = true;
                return;
            }
        }
    }

    /// <summary>Only one form may be displayed; checking one clears the others and marks it seen.</summary>
    private void UpdateDisplayedForm(int index, bool value)
    {
        if (editing || !value)
            return;

        editing = true;
        for (int i = 0; i < CLB_FormDisplayed.Count; i++)
        {
            if (i != index)
                CLB_FormDisplayed.SetItemChecked(i, false);
        }
        CLB_FormsSeen.SetItemChecked(index, true);
        editing = false;
    }

    #endregion

    #region Entry read / write

    private void GetEntry(bool skipFormRepop = false)
    {
        SpindaRow.IsVisible = Dex.ShowSpinda(species);

        CP[0].IsChecked = Dex.GetCaught(species);
        for (int i = 0; i < 4; i++)
            CP[i + 1].IsChecked = Dex.GetSeen(species, i);
        for (int i = 0; i < 4; i++)
            CP[i + 5].IsChecked = Dex.GetDisplayed(species, i);

        if (Dex.LanguageEditable(species))
        {
            for (int i = 0; i < CL.Length; i++)
                CL[i].IsChecked = Dex.GetLanguageFlag(species, i);
            GB_Language.IsEnabled = true;
        }
        else
        {
            foreach (var c in CL)
                c.IsChecked = false;
            GB_Language.IsEnabled = false;
        }

        if (Dex.ForeignFlagApplies(species))
        {
            CHK_F1.IsEnabled = true;
            CHK_F1.IsChecked = Dex.GetForeignFlag(species);
        }
        else
        {
            CHK_F1.IsEnabled = false;
            CHK_F1.IsChecked = false;
        }

        if (Dex.SupportsCounts)
        {
            MT_Seen.Text = Dex.GetCountSeen(species).ToString();
            MT_Obtained.Text = Dex.GetCountObtained(species).ToString();
        }

        var pi = SAV.Personal[species];
        CP[1].IsEnabled = CP[3].IsEnabled = CP[5].IsEnabled = CP[7].IsEnabled = !pi.OnlyFemale;
        CP[2].IsEnabled = CP[4].IsEnabled = CP[6].IsEnabled = CP[8].IsEnabled = !(pi.OnlyMale || pi.Genderless);

        var (index, count) = Dex.GetFormIndex(species);
        if (skipFormRepop)
        {
            if (count == 0)
                return;
            for (int i = 0; i < count; i++)
            {
                CLB_FormsSeen.SetItemChecked(i, Dex.GetFormFlag(index + i, 0));
                CLB_FormsSeen.SetItemChecked(i + count, Dex.GetFormFlag(index + i, 1));
                CLB_FormDisplayed.SetItemChecked(i, Dex.GetFormFlag(index + i, 2));
                CLB_FormDisplayed.SetItemChecked(i + count, Dex.GetFormFlag(index + i, 3));
            }
            return;
        }

        CLB_FormsSeen.ClearItems();
        CLB_FormDisplayed.ClearItems();
        if (count == 0)
            return;

        var forms = FormConverter.GetFormList(species, GameInfo.Strings.types, GameInfo.Strings.forms, GameInfo.GenderSymbolUnicode, SAV.Context);
        if (forms.Length < 1)
            return;

        for (int i = 0; i < forms.Length; i++)
            CLB_FormsSeen.Add(forms[i], Dex.GetFormFlag(index + i, 0));
        for (int i = 0; i < forms.Length; i++)
            CLB_FormsSeen.Add($"* {forms[i]}", Dex.GetFormFlag(index + i, 1));
        for (int i = 0; i < forms.Length; i++)
            CLB_FormDisplayed.Add(forms[i], Dex.GetFormFlag(index + i, 2));
        for (int i = 0; i < forms.Length; i++)
            CLB_FormDisplayed.Add($"* {forms[i]}", Dex.GetFormFlag(index + i, 3));
    }

    private void SetEntry()
    {
        if (species is 0 || species > Dex.MaxSpecies)
            return;

        Dex.SetCaught(species, CP[0].IsChecked == true);
        for (int i = 0; i < 4; i++)
            Dex.SetSeen(species, i, CP[i + 1].IsChecked == true);
        for (int i = 0; i < 4; i++)
            Dex.SetDisplayed(species, i, CP[i + 5].IsChecked == true);

        if (Dex.LanguageEditable(species))
        {
            for (int i = 0; i < CL.Length; i++)
                Dex.SetLanguageFlag(species, i, CL[i].IsChecked == true);
        }

        if (CHK_F1.IsEnabled)
            Dex.SetForeignFlag(species, CHK_F1.IsChecked == true);

        if (Dex.SupportsCounts)
        {
            Dex.SetCountSeen(species, Clamp(MT_Seen.Text));
            Dex.SetCountObtained(species, Clamp(MT_Obtained.Text));
        }

        var (index, count) = Dex.GetFormIndex(species);
        if (count == 0)
            return;

        int half = CLB_FormsSeen.Count / 2;
        for (int i = 0; i < half; i++)
            Dex.SetFormFlag(index + i, 0, CLB_FormsSeen.GetItemChecked(i));
        for (int i = 0; i < half; i++)
            Dex.SetFormFlag(index + i, 1, CLB_FormsSeen.GetItemChecked(i + half));

        editing = true;
        int halfD = CLB_FormDisplayed.Count / 2;
        for (int i = 0; i < halfD; i++)
            Dex.SetFormFlag(index + i, 2, CLB_FormDisplayed.GetItemChecked(i));
        for (int i = 0; i < halfD; i++)
            Dex.SetFormFlag(index + i, 3, CLB_FormDisplayed.GetItemChecked(i + halfD));
        editing = false;

        static ushort Clamp(string? text) => (ushort)Math.Clamp(Util.ToUInt32(text ?? string.Empty), 0, ushort.MaxValue);
    }

    #endregion

    protected override void OnSave()
    {
        SetEntry();
        if (species is not 0)
            Dex.InitialSpecies = species;
        Dex.IsNationalDexUnlocked = CHK_NationalDexUnlocked.IsChecked == true;
        Dex.IsNationalDexMode = CHK_NationalDexActive.IsChecked == true;
        Dex.Spinda = Util.GetHexValue(TB_Spinda.Text ?? string.Empty);

        Origin.CopyChangesFrom(SAV);
        Close();
    }
}

/// <summary>Pokédex editor for Generation 5 saves (port of the WinForms <c>SAV_Pokedex5</c>).</summary>
public sealed class Pokedex5Window : DexEditorWindow
{
    public Pokedex5Window(SAV5 sav) : this(sav, (SAV5)sav.Clone()) { }

    private Pokedex5Window(SAV5 origin, SAV5 clone)
        : base(origin, clone, new Zukan5Source(clone.Zukan, 649), "SAV_Pokedex5") { }
}

/// <summary>Pokédex editor for X/Y (port of the WinForms <c>SAV_PokedexXY</c>).</summary>
public sealed class PokedexXYWindow : DexEditorWindow
{
    public PokedexXYWindow(SAV6XY sav) : this(sav, (SAV6XY)sav.Clone()) { }

    private PokedexXYWindow(SAV6XY origin, SAV6XY clone)
        : base(origin, clone, new Zukan6Source(clone.Zukan, 721), "SAV_PokedexXY") { }
}

/// <summary>Pokédex editor for OR/AS, including the DexNav counters (port of the WinForms <c>SAV_PokedexORAS</c>).</summary>
public sealed class PokedexORASWindow : DexEditorWindow
{
    public PokedexORASWindow(SAV6AO sav) : this(sav, (SAV6AO)sav.Clone()) { }

    private PokedexORASWindow(SAV6AO origin, SAV6AO clone)
        : base(origin, clone, new Zukan6Source(clone.Zukan, 721), "SAV_PokedexORAS") { }
}
