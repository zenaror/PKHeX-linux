using System;
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
/// Pokédex editor for Brilliant Diamond / Shining Pearl (port of the WinForms <c>SAV_PokedexBDSP</c>).
/// </summary>
/// <remarks>
/// The Generation 8b dex keeps one state per species (none / seen / caught), four gender-and-shiny seen flags,
/// nine language flags, and separate regular / shiny lists of the forms that were seen.
/// </remarks>
public sealed class PokedexBDSPWindow : SaveEditorWindow
{
    private static readonly LanguageID[] Languages =
    [
        LanguageID.Japanese, LanguageID.English, LanguageID.French, LanguageID.Italian, LanguageID.German,
        LanguageID.Spanish, LanguageID.Korean, LanguageID.ChineseS, LanguageID.ChineseT,
    ];

    private readonly SaveFile Origin;
    private readonly SAV8BS SAV;
    private readonly Zukan8b Zukan;

    private readonly ListBox LB_Species = new() { Name = "LB_Species", Width = 220, Height = 400 };
    private readonly ObservableCollection<string> SpeciesItems = [];
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);
    private readonly ComboBox CB_State = UiFactory.StringCombo("CB_State", 140);
    private readonly CheckBox CHK_M = UiFactory.Check("CHK_M", "Male");
    private readonly CheckBox CHK_F = UiFactory.Check("CHK_F", "Female");
    private readonly CheckBox CHK_MS = UiFactory.Check("CHK_MS", "Shiny Male");
    private readonly CheckBox CHK_FS = UiFactory.Check("CHK_FS", "Shiny Female");
    private readonly CheckBox[] CL;
    private readonly CheckedListView CLB_FormRegular = new() { Name = "CLB_FormRegular", Width = 190, Height = 160 };
    private readonly CheckedListView CLB_FormShiny = new() { Name = "CLB_FormShiny", Width = 190, Height = 160 };
    private readonly CheckBox CHK_National = UiFactory.Check("CHK_National", "National Dex");
    private readonly Button B_GiveAll = UiFactory.Button("B_GiveAll", "Check All");
    private readonly Button B_Modify = UiFactory.Button("B_Modify", "Modify...");
    private readonly Button B_ModifyForms = UiFactory.Button("B_ModifyForms", "Modify...");

    private bool editing;
    private ushort species = ushort.MaxValue;

    public PokedexBDSPWindow(SAV8BS sav) : base("SAV_PokedexBDSP", "Pokédex Editor")
    {
        SAV = (SAV8BS)(Origin = sav).Clone();
        Zukan = SAV.Zukan;

        CL = [
            UiFactory.Check("CHK_LangJPN", "Japanese"), UiFactory.Check("CHK_LangENG", "English"),
            UiFactory.Check("CHK_LangFRE", "French"), UiFactory.Check("CHK_LangITA", "Italian"),
            UiFactory.Check("CHK_LangGER", "German"), UiFactory.Check("CHK_LangSPA", "Spanish"),
            UiFactory.Check("CHK_LangKOR", "Korean"), UiFactory.Check("CHK_LangCHS", "ChineseS"),
            UiFactory.Check("CHK_LangCHT", "ChineseT"),
        ];
        foreach (var s in Enum.GetNames<ZukanState8b>())
            CB_State.Items.Add(s);

        BuildLayout();

        editing = true;
        CB_Species.SetItems(GameInfo.FilteredSources.Species.Skip(1).ToList());
        for (int i = 1; i < SAV.MaxSpeciesID + 1; i++)
            SpeciesItems.Add($"{i:000} - {GameInfo.Strings.specieslist[i]}");
        editing = false;

        LB_Species.SelectedIndex = 0;
        CHK_National.IsChecked = Zukan.HasNationalDex;
    }

    private void BuildLayout()
    {
        LB_Species.ItemsSource = SpeciesItems;

        var seen = new GroupBoxView("GB_Seen", "Seen", UiFactory.Column(CHK_M, CHK_F, CHK_MS, CHK_FS));
        var languages = new GroupBoxView("GB_Language", "Languages", UiFactory.Column(CL));
        var flags = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        flags.Children.Add(seen);
        flags.Children.Add(languages);

        var forms = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        forms.Children.Add(UiFactory.Column(UiFactory.Label("L_FormRegular", "Forms"), CLB_FormRegular));
        forms.Children.Add(UiFactory.Column(UiFactory.Label("L_FormShiny", "Forms (Shiny)"), CLB_FormShiny));

        var right = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_goto", "goto:"), CB_Species),
            UiFactory.Row(UiFactory.Label("L_State", "State:"), CB_State),
            flags,
            UiFactory.Row(B_GiveAll, B_Modify),
            forms,
            B_ModifyForms,
            CHK_National);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        body.Children.Add(LB_Species);
        body.Children.Add(new ScrollViewer { Content = right, MaxHeight = 620 });
        SetBody(body);

        LB_Species.SelectionChanged += (_, _) => ChangeLBSpecies();
        CB_Species.SelectionChanged += (_, _) => ChangeCBSpecies();
        B_GiveAll.AttachClickHandled(ClickGiveAll);
        B_Modify.Flyout = BuildModifyMenu();
        B_ModifyForms.Flyout = BuildModifyFormsMenu();
    }

    private MenuFlyout BuildModifyMenu()
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.Bottom };
        AddItem(flyout, "mnuSeenNone", "Seen none", _ => Zukan.SetAllSeen(false));
        AddItem(flyout, "mnuSeenAll", "Seen all", mods => Zukan.SetAllSeen(shinyToo: mods == KeyModifiers.Control));
        AddItem(flyout, "mnuCaughtNone", "Caught none", _ => Zukan.CaughtNone());
        AddItem(flyout, "mnuCaughtAll", "Caught all", _ => Zukan.CaughtAll());
        AddItem(flyout, "mnuComplete", "Complete Dex", mods => Zukan.CompleteDex(mods == KeyModifiers.Control));
        return flyout;
    }

    private void AddItem(MenuFlyout flyout, string name, string header, Action<KeyModifiers> action)
    {
        var item = new MenuItem { Name = name, Header = header };
        item.Click += (_, _) =>
        {
            SetEntry();
            action(MainWindow.CurrentModifiers);
            GetEntry();
        };
        flyout.Items.Add(item);
    }

    private MenuFlyout BuildModifyFormsMenu()
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.Bottom };
        AddFormItem(flyout, "mnuFormAllRegular", "All regular", regular: true, shiny: false);
        AddFormItem(flyout, "mnuFormAllShinies", "All shiny", regular: false, shiny: true);
        AddFormItem(flyout, "mnuFormNone", "None", regular: false, shiny: false, clear: true);
        return flyout;
    }

    private void AddFormItem(MenuFlyout flyout, string name, string header, bool regular, bool shiny, bool clear = false)
    {
        var item = new MenuItem { Name = name, Header = header };
        item.Click += (_, _) =>
        {
            for (int i = 0; i < CLB_FormRegular.Count; i++)
            {
                if (clear)
                {
                    CLB_FormRegular.SetItemChecked(i, false);
                    CLB_FormShiny.SetItemChecked(i, false);
                    continue;
                }
                if (regular)
                    CLB_FormRegular.SetItemChecked(i, true);
                if (shiny)
                    CLB_FormShiny.SetItemChecked(i, true);
            }
        };
        flyout.Items.Add(item);
    }

    private void ClickGiveAll(KeyModifiers mods)
    {
        bool all = mods != KeyModifiers.Control;
        CB_State.SelectedIndex = all ? (int)ZukanState8b.Caught : 0;
        CHK_M.IsChecked = CHK_F.IsChecked = CHK_MS.IsChecked = CHK_FS.IsChecked = all;
        foreach (var c in CL)
            c.IsChecked = all;
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

    #endregion

    #region Entry read / write

    private void GetEntry()
    {
        CB_State.SelectedIndex = (int)Zukan.GetState(species);
        Zukan.GetGenderFlags(species, out var m, out var f, out var ms, out var fs);
        CHK_M.IsChecked = m;
        CHK_F.IsChecked = f;
        CHK_MS.IsChecked = ms;
        CHK_FS.IsChecked = fs;

        for (int i = 0; i < CL.Length; i++)
            CL[i].IsChecked = Zukan.GetLanguageFlag(species, (int)Languages[i]);

        CLB_FormRegular.ClearItems();
        CLB_FormShiny.ClearItems();
        var fc = Zukan8b.GetFormCount(species);
        if (fc <= 0)
            return;

        var forms = FormConverter.GetFormList(species, GameInfo.Strings.types, GameInfo.Strings.forms, GameInfo.GenderSymbolUnicode, SAV.Context).Take(fc).ToArray();
        for (byte i = 0; i < forms.Length; i++)
        {
            CLB_FormRegular.Add(forms[i], Zukan.GetHasFormFlag(species, i, false));
            CLB_FormShiny.Add(forms[i], Zukan.GetHasFormFlag(species, i, true));
        }
    }

    private void SetEntry()
    {
        if (species > 493)
            return;

        Zukan.SetState(species, (ZukanState8b)Math.Max(0, CB_State.SelectedIndex));
        Zukan.SetGenderFlags(species, CHK_M.IsChecked == true, CHK_F.IsChecked == true, CHK_MS.IsChecked == true, CHK_FS.IsChecked == true);

        for (int i = 0; i < CL.Length; i++)
            Zukan.SetLanguageFlag(species, (int)Languages[i], CL[i].IsChecked == true);

        for (byte i = 0; i < CLB_FormRegular.Count; i++)
        {
            Zukan.SetHasFormFlag(species, i, false, CLB_FormRegular.GetItemChecked(i));
            Zukan.SetHasFormFlag(species, i, true, CLB_FormShiny.GetItemChecked(i));
        }
    }

    #endregion

    protected override void OnSave()
    {
        SetEntry();
        Zukan.HasNationalDex = CHK_National.IsChecked == true;
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
