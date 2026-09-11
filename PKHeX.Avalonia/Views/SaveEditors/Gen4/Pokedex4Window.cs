using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using static PKHeX.Core.Zukan4;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4;

/// <summary>
/// Pokédex editor for Generation 4 saves (port of the WinForms <c>SAV_Pokedex4</c>).
/// </summary>
/// <remarks>
/// Generation 4 stores which genders and forms were seen as an ordered list rather than as flags, so the
/// editor moves entries between a "seen" and a "not seen" list and lets the order be changed; the first
/// entry is the one the dex displays.
/// </remarks>
public sealed class Pokedex4Window : SaveEditorWindow
{
    private const int LangCount = 6; // no Korean in Generation 4

    private readonly SaveFile Origin;
    private readonly SAV4 SAV;
    private readonly CheckBox[] CL;

    private readonly ListBox LB_Species = new() { Name = "LB_Species", Width = 220, Height = 400 };
    private readonly ObservableCollection<string> SpeciesItems = [];
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);
    private readonly CheckBox CHK_Seen = UiFactory.Check("CHK_Seen", "Seen");
    private readonly CheckBox CHK_Caught = UiFactory.Check("CHK_Caught", "Caught");

    private readonly ListBox LB_Gender = new() { Name = "LB_Gender", Width = 120, Height = 90 };
    private readonly ListBox LB_NGender = new() { Name = "LB_NGender", Width = 120, Height = 90 };
    private readonly ObservableCollection<string> GenderSeen = [];
    private readonly ObservableCollection<string> GenderNotSeen = [];
    private readonly Button B_GLeft = UiFactory.Button("B_GLeft", "<");
    private readonly Button B_GRight = UiFactory.Button("B_GRight", ">");
    private readonly Button B_GUp = UiFactory.Button("B_GUp", "^");
    private readonly Button B_GDown = UiFactory.Button("B_GDown", "v");

    private readonly ListBox LB_Form = new() { Name = "LB_Form", Width = 150, Height = 150 };
    private readonly ListBox LB_NForm = new() { Name = "LB_NForm", Width = 150, Height = 150 };
    private readonly ObservableCollection<string> FormSeen = [];
    private readonly ObservableCollection<string> FormNotSeen = [];
    private readonly Button B_FLeft = UiFactory.Button("B_FLeft", "<");
    private readonly Button B_FRight = UiFactory.Button("B_FRight", ">");
    private readonly Button B_FUp = UiFactory.Button("B_FUp", "^");
    private readonly Button B_FDown = UiFactory.Button("B_FDown", "v");

    private readonly ComboBox CB_DexUpgraded = UiFactory.StringCombo("CB_DexUpgraded", 170);
    private readonly Button B_GiveAll = UiFactory.Button("B_GiveAll", "Check All");
    private readonly Button B_Modify = UiFactory.Button("B_Modify", "Modify...");
    private GroupBoxView GB_Language = null!;

    private bool editing;
    private ushort species = ushort.MaxValue;

    public Pokedex4Window(SAV4 sav) : base("SAV_Pokedex4", "Pokédex Editor")
    {
        SAV = (SAV4)(Origin = sav).Clone();
        CL = [
            UiFactory.Check("CHK_L1", "Japanese"), UiFactory.Check("CHK_L2", "English"),
            UiFactory.Check("CHK_L3", "French"), UiFactory.Check("CHK_L5", "German"),
            UiFactory.Check("CHK_L4", "Italian"), UiFactory.Check("CHK_L6", "Spanish"),
        ]; // stored order: JPN, ENG, FRA, GER, ITA, SPA

        BuildLayout();

        editing = true;
        CB_Species.SetItems(GameInfo.FilteredSources.Species.Skip(1).ToList());
        for (int i = 1; i < SAV.MaxSpeciesID + 1; i++)
            SpeciesItems.Add($"{i:000} - {GameInfo.Strings.specieslist[i]}");
        editing = false;

        LB_Species.SelectedIndex = 0;

        string[] dexMode = ["not given", "simple mode", "detect forms", "national dex", "other languages"];
        if (SAV is SAV4HGSS)
            dexMode = dexMode.Where((_, i) => i != 2).ToArray();
        foreach (var mode in dexMode)
            CB_DexUpgraded.Items.Add(mode);
        if (SAV.DexUpgraded < CB_DexUpgraded.ItemCount)
            CB_DexUpgraded.SelectedIndex = SAV.DexUpgraded;
    }

    private void BuildLayout()
    {
        LB_Species.ItemsSource = SpeciesItems;
        LB_Gender.ItemsSource = GenderSeen;
        LB_NGender.ItemsSource = GenderNotSeen;
        LB_Form.ItemsSource = FormSeen;
        LB_NForm.ItemsSource = FormNotSeen;

        GB_Language = new GroupBoxView("GB_Language", "Languages", UiFactory.Column(CL));

        var genders = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        genders.Children.Add(UiFactory.Column(UiFactory.Label("L_GenderSeen", "Seen"), LB_Gender));
        genders.Children.Add(UiFactory.Column(B_GLeft, B_GRight, B_GUp, B_GDown));
        genders.Children.Add(UiFactory.Column(UiFactory.Label("L_GenderNot", "Not Seen"), LB_NGender));

        var forms = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        forms.Children.Add(UiFactory.Column(UiFactory.Label("L_FormSeen", "Seen"), LB_Form));
        forms.Children.Add(UiFactory.Column(B_FLeft, B_FRight, B_FUp, B_FDown));
        forms.Children.Add(UiFactory.Column(UiFactory.Label("L_FormNot", "Not Seen"), LB_NForm));

        var right = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_goto", "goto:"), CB_Species),
            UiFactory.Row(CHK_Seen, CHK_Caught),
            new GroupBoxView("GB_Gender", "Genders", genders),
            new GroupBoxView("GB_Forms", "Forms", forms),
            GB_Language,
            UiFactory.Row(B_GiveAll, B_Modify),
            UiFactory.Row(UiFactory.Label("L_DexUpgraded", "Dex Mode:"), CB_DexUpgraded));

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        body.Children.Add(LB_Species);
        body.Children.Add(new ScrollViewer { Content = right, MaxHeight = 620 });
        SetBody(body);

        LB_Species.SelectionChanged += (_, _) => ChangeLBSpecies();
        CB_Species.SelectionChanged += (_, _) => ChangeCBSpecies();
        CHK_Seen.IsCheckedChanged += (_, _) => ChangeSeen();
        B_GLeft.Click += async (_, _) => await ToggleSeen(fromNotSeen: true);
        B_GRight.Click += async (_, _) => await ToggleSeen(fromNotSeen: false);
        B_GUp.Click += async (_, _) => await MoveGender(-1);
        B_GDown.Click += async (_, _) => await MoveGender(1);
        B_FLeft.Click += async (_, _) => await ToggleForm(fromNotSeen: true);
        B_FRight.Click += async (_, _) => await ToggleForm(fromNotSeen: false);
        B_FUp.Click += async (_, _) => await MoveForm(-1);
        B_FDown.Click += async (_, _) => await MoveForm(1);
        B_GiveAll.AttachClickHandled(ClickGiveAll);
        B_Modify.Flyout = BuildModifyMenu();
    }

    private MenuFlyout BuildModifyMenu()
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.Bottom };
        AddItem(flyout, "mnuSeenNone", "Seen none", SetDexArgs.None);
        AddItem(flyout, "mnuSeenAll", "Seen all", SetDexArgs.SeenAll);
        AddItem(flyout, "mnuCaughtNone", "Caught none", SetDexArgs.CaughtNone);
        AddItem(flyout, "mnuCaughtAll", "Caught all", SetDexArgs.CaughtAll);
        AddItem(flyout, "mnuComplete", "Complete Dex", SetDexArgs.Complete);
        return flyout;
    }

    private void AddItem(MenuFlyout flyout, string name, string header, SetDexArgs args)
    {
        var item = new MenuItem { Name = name, Header = header };
        item.Click += (_, _) =>
        {
            SetEntry();
            var lang = GetGen4LanguageBitIndex(SAV.Language);
            for (ushort i = 1; i <= 493; i++)
                SAV.Dex.ModifyAll(i, args, lang);
            GetEntry();
        };
        flyout.Items.Add(item);
    }

    private void ClickGiveAll(KeyModifiers mods)
    {
        var args = mods != KeyModifiers.Control ? SetDexArgs.Complete : SetDexArgs.None;
        SAV.Dex.ModifyAll(species, args, GetGen4LanguageBitIndex(SAV.Language));
        GetEntry();
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
        var dex = SAV.Dex;
        CHK_Caught.IsChecked = dex.GetCaught(species);
        CHK_Seen.IsChecked = dex.GetSeen(species);
        LoadGenders(CHK_Seen.IsChecked == true);
        LoadForms();
        LoadLanguage();
    }

    private void LoadLanguage()
    {
        var dex = SAV.Dex;
        if (dex.HasLanguage(species))
        {
            GB_Language.IsEnabled = true;
            for (int i = 0; i < LangCount; i++)
                CL[i].IsChecked = dex.GetLanguageBitIndex(species, i);
        }
        else
        {
            GB_Language.IsEnabled = false;
            foreach (var c in CL)
                c.IsChecked = false;
        }
    }

    private void LoadForms()
    {
        FormSeen.Clear();
        FormNotSeen.Clear();

        var forms = SAV.Dex.GetForms(species);
        if (forms.Length == 0)
            return;

        var formNames = GetFormNames4Dex(species);
        var seenChecked = SAV.Dex.GetSeen(species);
        var seen = forms.Where(z => seenChecked && z != FORM_NONE && z < forms.Length).Distinct().Select((_, i) => formNames[forms[i]]).ToArray();
        foreach (var s in seen)
            FormSeen.Add(s);
        foreach (var s in formNames.Except(seen))
            FormNotSeen.Add(s);
    }

    private void LoadGenders(bool seen)
    {
        var dex = SAV.Dex;
        GenderSeen.Clear();
        GenderNotSeen.Clear();

        var first = seen ? GenderSeen : GenderNotSeen;
        var second = dex.GetSeenSingleGender(species) ? GenderNotSeen : first;

        var pi = SAV.Personal[species];
        switch (pi.Gender)
        {
            case PersonalInfo.RatioMagicGenderless:
                first.Add(GENDERLESS);
                break;
            case PersonalInfo.RatioMagicMale:
                first.Add(MALE);
                break;
            case PersonalInfo.RatioMagicFemale:
                first.Add(FEMALE);
                break;
            default:
                var firstFem = dex.GetSeenGenderFirst(species) == 1;
                first.Add(firstFem ? FEMALE : MALE);
                second.Add(firstFem ? MALE : FEMALE);
                break;
        }
    }

    private void SetEntry()
    {
        if (species > 493)
            return;

        var dex = SAV.Dex;
        dex.SetCaught(species, CHK_Caught.IsChecked == true);
        dex.SetSeen(species, CHK_Seen.IsChecked == true);
        dex.SetSeenGenderNeither(species);
        if (GenderSeen.Count != 0)
        {
            var femaleFirst = GenderSeen[0] == FEMALE;
            var firstGender = femaleFirst ? (byte)1 : (byte)0;
            dex.SetSeenGenderNewFlag(species, firstGender);
            if (GenderSeen.Count != 1)
                dex.SetSeenGenderSecond(species, (byte)(firstGender ^ 1));
        }

        if (dex.HasLanguage(species))
        {
            for (int i = 0; i < LangCount; i++)
                dex.SetLanguageBitIndex(species, i, CL[i].IsChecked == true);
        }

        var forms = SAV.Dex.GetForms(species);
        if (forms.Length == 0)
            return;

        var formNames = GetFormNames4Dex(species);
        Span<byte> arr = stackalloc byte[FormSeen.Count];
        for (int i = 0; i < FormSeen.Count; i++)
            arr[i] = (byte)Array.IndexOf(formNames, FormSeen[i]);
        SAV.Dex.SetForms(species, arr);
    }

    #endregion

    #region List manipulation

    private void ChangeSeen()
    {
        if (!editing)
        {
            if (CHK_Seen.IsChecked != true)
            {
                SAV.Dex.ClearSeen(species);
                GetEntry();
            }
            else if (GenderNotSeen.Count > 0)
            {
                while (GenderNotSeen.Count > 0)
                {
                    GenderSeen.Add(GenderNotSeen[0]);
                    GenderNotSeen.RemoveAt(0);
                }
                while (FormNotSeen.Count > 0)
                {
                    FormSeen.Add(FormNotSeen[0]);
                    FormNotSeen.RemoveAt(0);
                }
            }
        }
        bool seen = CHK_Seen.IsChecked == true;
        LB_Gender.IsEnabled = LB_NGender.IsEnabled = LB_Form.IsEnabled = LB_NForm.IsEnabled = seen;
        CHK_Caught.IsEnabled = seen;
    }

    private async Task ToggleSeen(bool fromNotSeen)
    {
        if (editing)
            return;
        var source = fromNotSeen ? GenderNotSeen : GenderSeen;
        var lb = fromNotSeen ? LB_NGender : LB_Gender;
        if (lb.SelectedIndex < 0)
        {
            await AppDialogs.Alert(this, "No Gender selected.");
            return;
        }
        var dest = fromNotSeen ? GenderSeen : GenderNotSeen;
        var destBox = fromNotSeen ? LB_Gender : LB_NGender;
        var item = source[lb.SelectedIndex];
        source.RemoveAt(lb.SelectedIndex);
        dest.Add(item);
        destBox.SelectedIndex = dest.Count - 1;
    }

    private async Task MoveGender(int delta) => await Move(LB_Gender, GenderSeen, delta, "No Gender selected.");

    private async Task ToggleForm(bool fromNotSeen)
    {
        if (editing)
            return;
        var source = fromNotSeen ? FormNotSeen : FormSeen;
        var lb = fromNotSeen ? LB_NForm : LB_Form;
        if (lb.SelectedIndex < 0)
        {
            await AppDialogs.Alert(this, "No Form selected.");
            return;
        }
        var dest = fromNotSeen ? FormSeen : FormNotSeen;
        var destBox = fromNotSeen ? LB_Form : LB_NForm;
        var item = source[lb.SelectedIndex];
        source.RemoveAt(lb.SelectedIndex);
        dest.Add(item);
        destBox.SelectedIndex = dest.Count - 1;
    }

    private async Task MoveForm(int delta) => await Move(LB_Form, FormSeen, delta, "No Form selected.");

    private async Task Move(ListBox lb, ObservableCollection<string> items, int delta, string emptyMessage)
    {
        if (editing)
            return;
        int index = lb.SelectedIndex;
        if (index < 0)
        {
            await AppDialogs.Alert(this, emptyMessage);
            return;
        }
        if (index == 0 && items.Count == 1)
            return;

        int newIndex = index + delta;
        if (newIndex < 0 || newIndex >= items.Count)
            return;

        items.Move(index, newIndex);
        lb.SelectedIndex = newIndex;
    }

    #endregion

    protected override void OnSave()
    {
        SetEntry();
        if (CB_DexUpgraded.SelectedIndex >= 0)
            SAV.DexUpgraded = CB_DexUpgraded.SelectedIndex;

        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
