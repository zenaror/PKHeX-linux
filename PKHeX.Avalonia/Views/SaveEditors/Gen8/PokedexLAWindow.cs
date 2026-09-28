using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen8;

/// <summary>
/// Pokédex editor for Legends: Arceus (port of the WinForms <c>SAV_PokedexLA</c>).
/// </summary>
/// <remarks>
/// Each entry tracks eight seen / obtained / caught bits per form, the research task progress that feeds the
/// research level, and the recorded size extremes. Tasks the game computes on its own are shown read-only.
/// </remarks>
public sealed class PokedexLAWindow : SaveEditorWindow
{
    private const int FlagCount = 8;

    /// <summary>Captions of the eight per-form flags, in the WinForms order (CHK_S0..CHK_S7).</summary>
    private static readonly string[] FlagCaptions =
    [
        "Male", "Female", "Alpha Male", "Alpha Female",
        "Shiny Male", "Shiny Female", "Shiny Alpha Male", "Shiny Alpha Female",
    ];
    private const int MaxTasks = 10;

    private readonly SAV8LA Origin;
    private readonly SAV8LA SAV;
    private readonly PokedexSave8a Dex;
    private readonly CheckBox[] CHK_SeenWild = new CheckBox[FlagCount];
    private readonly CheckBox[] CHK_Obtained = new CheckBox[FlagCount];
    private readonly CheckBox[] CHK_CaughtWild = new CheckBox[FlagCount];
    private readonly PokedexResearchTask8aView[] TaskControls = new PokedexResearchTask8aView[MaxTasks];
    private readonly ushort[] SpeciesToDex;
    private readonly ushort[] DexToSpecies;
    private readonly List<ComboItem> DisplayedForms = [];

    private readonly string[] TaskDescriptions;
    private readonly string[] SpeciesQuests;
    private readonly string[] TimeTaskDescriptions;

    private int lastIndex = -1;
    private int lastForm = -1;
    private bool Editing;
    private readonly bool CanSave;

    private readonly ListBox LB_Species = new() { Name = "LB_Species", Width = 230, Height = 440 };
    private readonly ObservableCollection<string> SpeciesItems = [];
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);
    private readonly ListBox LB_Forms = new() { Name = "LB_Forms", Width = 170, Height = 140 };
    private readonly ObservableCollection<string> FormItems = [];
    private readonly ComboBox CB_DisplayForm = UiFactory.Combo("CB_DisplayForm", 170);

    private readonly CheckBox CHK_Solitude = UiFactory.Check("CHK_Solitude", "Solitude Complete");
    private readonly CheckBox CHK_A = UiFactory.Check("CHK_A", "Alpha");
    private readonly CheckBox CHK_S = UiFactory.Check("CHK_S", "Shiny");
    private readonly CheckBox CHK_G = UiFactory.Check("CHK_G", "Female");
    private readonly CheckBox CHK_Seen = UiFactory.Check("CHK_Seen", "Seen");
    private readonly CheckBox CHK_Complete = UiFactory.Check("CHK_Complete", "Complete");
    private readonly CheckBox CHK_Perfect = UiFactory.Check("CHK_Perfect", "Perfect");
    private readonly NumericTextBox MTB_UpdateIndex = UiFactory.Numeric("MTB_UpdateIndex", 5, 80);
    private readonly NumericTextBox MTB_ResearchLevelReported = UiFactory.Numeric("MTB_ResearchLevelReported", 5, 80);
    private readonly NumericTextBox MTB_ResearchLevelUnreported = UiFactory.Numeric("MTB_ResearchLevelUnreported", 5, 80);

    private readonly CheckBox CHK_MinAndMax = UiFactory.Check("CHK_MinAndMax", "Has Both Min & Max");
    private readonly TextBox TB_MinHeight = UiFactory.Text("TB_MinHeight", 10, 90);
    private readonly TextBox TB_MaxHeight = UiFactory.Text("TB_MaxHeight", 10, 90);
    private readonly TextBox TB_MinWeight = UiFactory.Text("TB_MinWeight", 10, 90);
    private readonly TextBox TB_MaxWeight = UiFactory.Text("TB_MaxWeight", 10, 90);
    private readonly TextBlock L_TheoryHeight = UiFactory.Label("L_TheoryHeight", "-");
    private readonly TextBlock L_TheoryWeight = UiFactory.Label("L_TheoryWeight", "-");

    private readonly Button B_Report = UiFactory.Button("B_Report", "Report Data");
    private readonly Button B_AdvancedResearch = UiFactory.Button("B_AdvancedResearch", "Edit All Tasks...");
    private readonly StackPanel TaskPanel = new() { Orientation = Orientation.Vertical, Spacing = 3 };

    public PokedexLAWindow(SAV8LA sav) : base("SAV_PokedexLA", "Pokédex Editor")
    {
        SAV = (SAV8LA)(Origin = sav).Clone();
        Dex = SAV.Blocks.PokedexSave;

        var lang = MainWindow.CurrentLanguage;
        TaskDescriptions = Util.GetStringList("tasks8a", lang);
        SpeciesQuests = Util.GetStringList("species_tasks8a", lang);
        TimeTaskDescriptions = Util.GetStringList("time_tasks8a", lang);

        for (int i = 0; i < FlagCount; i++)
        {
            var caption = FlagCaptions[i];
            CHK_SeenWild[i] = UiFactory.Check($"CHK_S{i}", caption);
            CHK_Obtained[i] = UiFactory.Check($"CHK_O{i}", caption);
            CHK_CaughtWild[i] = UiFactory.Check($"CHK_C{i}", caption);
        }
        for (int i = 0; i < MaxTasks; i++)
        {
            TaskControls[i] = new PokedexResearchTask8aView { Name = $"PRT_{i + 1}", IsVisible = false };
            TaskControls[i].SetStrings(TaskDescriptions, SpeciesQuests, TimeTaskDescriptions);
            TaskPanel.Children.Add(TaskControls[i]);
        }

        (SpeciesToDex, DexToSpecies) = BuildDexMaps();

        Editing = true;
        BuildLayout();

        var speciesNames = GameInfo.Strings.Species;
        var species = GameInfo.FilteredSources.Species
            .Where(z => PokedexSave8a.GetDexIndex(PokedexType8a.Hisui, (ushort)z.Value) != 0).ToArray();
        CB_Species.SetItems(species);
        CB_Species.SelectedIndex = 0; // the WinForms binding shows the first entry; the list handler never syncs it

        DisplayedForms.Add(new ComboItem(GameInfo.Strings.types[0], 0));
        CB_DisplayForm.SetItems(DisplayedForms);

        for (var d = 1; d < DexToSpecies.Length; d++)
            SpeciesItems.Add($"{d:000} - {speciesNames[DexToSpecies[d]]}");

        Editing = false;
        LB_Species.SelectedIndex = 0;
        CanSave = true;
    }

    private (ushort[] SpeciesToDex, ushort[] DexToSpecies) BuildDexMaps()
    {
        var toDex = new ushort[SAV.Personal.MaxSpeciesID + 1];
        var maxDex = 0;
        for (ushort s = 1; s <= SAV.Personal.MaxSpeciesID; s++)
        {
            var hisuiDex = PokedexSave8a.GetDexIndex(PokedexType8a.Hisui, s);
            if (hisuiDex == 0)
                continue;
            toDex[s] = hisuiDex;
            if (hisuiDex > maxDex)
                maxDex = hisuiDex;
        }

        var toSpecies = new ushort[maxDex + 1];
        for (ushort s = 1; s <= SAV.Personal.MaxSpeciesID; s++)
        {
            if (toDex[s] != 0)
                toSpecies[toDex[s]] = s;
        }
        return (toDex, toSpecies);
    }

    #region Layout

    private void BuildLayout()
    {
        LB_Species.ItemsSource = SpeciesItems;
        LB_Forms.ItemsSource = FormItems;

        // Three flag groups, each with the eight gender/alpha/shiny checkboxes (SAV_PokedexLA.Designer.cs).
        var seenWild = new GroupBoxView("GB_SeenInWild", "Seen in the Wild", UiFactory.Column(CHK_SeenWild));
        var obtained = new GroupBoxView("GB_Obtained", "Obtained", UiFactory.Column(CHK_Obtained));
        var caughtWild = new GroupBoxView("GB_CaughtInWild", "Caught in the Wild", UiFactory.Column(CHK_CaughtWild));

        var displayed = new GroupBoxView("GB_Displayed", "Displayed", UiFactory.Column(CHK_G, CHK_S, CHK_A));
        var displayColumn = UiFactory.Column(
            UiFactory.Label("L_DisplayedForm", "Displayed Form:"),
            CB_DisplayForm,
            displayed);

        // Research tasks group: the ten task rows, then the summary row along the bottom.
        var researchSummary = UiFactory.Row(
            UiFactory.Label("L_UpdateIndex", "Index:"), MTB_UpdateIndex,
            UiFactory.Label("L_ResearchLevelReported", "Reported:"), MTB_ResearchLevelReported,
            UiFactory.Label("L_ResearchLevelUnreported", "Unreported:"), MTB_ResearchLevelUnreported,
            CHK_Seen, CHK_Complete, CHK_Perfect, B_Report, B_AdvancedResearch);
        var researchTasks = new GroupBoxView("GB_ResearchTasks", "Research Tasks", UiFactory.Column(TaskPanel, researchSummary));

        // Statistics group: the recorded extremes, with the possible range under each pair.
        var height = new GroupBoxView("GB_Height", "Height", UiFactory.Column(
            UiFactory.Row(TB_MinHeight, UiFactory.Label("L_ConnectHeight", "-"), TB_MaxHeight),
            L_TheoryHeight));
        var weight = new GroupBoxView("GB_Weight", "Weight", UiFactory.Column(
            UiFactory.Row(TB_MinWeight, UiFactory.Label("L_ConnectWeight", "-"), TB_MaxWeight),
            L_TheoryWeight));
        var statistics = new GroupBoxView("GB_Statistics", "Statistics", UiFactory.Column(CHK_MinAndMax, height, weight));

        var groups = UiFactory.Row(seenWild, obtained, caughtWild, statistics, displayColumn);
        groups.Spacing = 8;

        var right = UiFactory.Column(CHK_Solitude, researchTasks, groups);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_goto", "goto:"), CB_Species),
            LB_Species,
            LB_Forms));
        body.Children.Add(new ScrollViewer { Content = right, MaxHeight = 640 });
        SetBody(body);

        LB_Species.SelectionChanged += (_, _) => ChangeLBSpecies();
        LB_Forms.SelectionChanged += (_, _) => ChangeLBForms();
        CB_Species.SelectionChanged += (_, _) => ChangeCBSpecies();
        foreach (var chk in CHK_Obtained)
            chk.IsCheckedChanged += (_, _) => ObtainFlagChanged();
        B_Report.Click += (_, _) => ClickReport();
        B_AdvancedResearch.Click += async (_, _) => await ClickAdvancedResearch();
    }

    #endregion

    #region Selection

    private void ChangeCBSpecies()
    {
        if (Editing || CB_Species.GetSelectedItem() is not { } item)
            return;
        var index = SpeciesToDex[item.Value] - 1;
        if (LB_Species.SelectedIndex != index)
            LB_Species.SelectedIndex = index; // triggers the list handler
    }

    private void ChangeLBSpecies()
    {
        if (Editing || LB_Species.SelectedIndex < 0)
            return;
        SetEntry(lastIndex, lastForm);

        Editing = true;
        lastIndex = LB_Species.SelectedIndex;
        FillFormList(lastIndex);
        FillResearchTasks(lastIndex);
        GetEntry(lastIndex, lastForm);
        Editing = false;
    }

    private void ChangeLBForms()
    {
        if (Editing || LB_Forms.SelectedIndex < 0)
            return;
        SetEntry(lastIndex, lastForm);
        lastForm = LB_Forms.SelectedIndex;

        Editing = true;
        GetEntry(lastIndex, lastForm);
        Editing = false;
    }

    private void FillFormList(int index)
    {
        FormItems.Clear();
        DisplayedForms.Clear();
        DisplayedForms.Add(new ComboItem(GameInfo.Strings.types[0], 0));
        CB_DisplayForm.SetItems(DisplayedForms);
        lastForm = 0;

        ushort species = DexToSpecies[index + 1];
        bool hasForms = FormInfo.HasFormSelection(SAV.Personal[species], species, 8);
        LB_Forms.IsEnabled = CB_DisplayForm.IsEnabled = hasForms;
        if (!hasForms)
            return;

        var ds = FormConverter.GetFormList(species, GameInfo.Strings.types, GameInfo.Strings.forms, GameInfo.GenderSymbolUnicode, SAV.Context);
        if (ds.Length == 1 && string.IsNullOrEmpty(ds[0]))
        {
            LB_Forms.IsEnabled = CB_DisplayForm.IsEnabled = false;
            return;
        }

        // Only forms with their own dex storage are listed.
        DisplayedForms.Clear();
        var formCount = SAV.Personal[species].FormCount;
        for (byte form = 0; form < formCount && form < ds.Length; form++)
        {
            if (!Dex.HasFormStorage(species, form) || Dex.IsBlacklisted(species, form))
                continue;
            FormItems.Add(ds[form]);
            DisplayedForms.Add(new ComboItem(ds[form], form));
        }

        CB_DisplayForm.SetItems(DisplayedForms);
        if (FormItems.Count > 0)
            LB_Forms.SelectedIndex = 0;
    }

    private void FillResearchTasks(int index)
    {
        var species = DexToSpecies[index + 1];
        var tasks = PokedexConstants8a.ResearchTasks[index];

        for (var i = 0; i < tasks.Length && i < TaskControls.Length; i++)
        {
            var tc = TaskControls[i];
            tc.IsVisible = false;
            Dex.GetResearchTaskLevel(species, i, out var repLevel, out _, out _);
            tc.SetTask(species, tasks[i], repLevel);
            tc.IsVisible = true;
        }
        for (var i = tasks.Length; i < TaskControls.Length; i++)
            TaskControls[i].IsVisible = false;
    }

    #endregion

    #region Entry read / write

    private void GetEntry(int index, int formIndex)
    {
        if (index < 0 || formIndex < 0 || formIndex >= DisplayedForms.Count)
            return;
        var species = DexToSpecies[index + 1];
        var form = (byte)DisplayedForms[formIndex].Value;

        var seenWild = Dex.GetPokeSeenInWildFlags(species, form);
        var obtain = Dex.GetPokeObtainFlags(species, form);
        var caughtWild = Dex.GetPokeCaughtInWildFlags(species, form);
        CHK_Solitude.IsChecked = Dex.GetSolitudeComplete(species);
        for (var i = 0; i < FlagCount; ++i)
        {
            CHK_SeenWild[i].IsChecked = (seenWild & (1 << i)) != 0;
            CHK_Obtained[i].IsChecked = (obtain & (1 << i)) != 0;
            CHK_CaughtWild[i].IsChecked = (caughtWild & (1 << i)) != 0;
        }

        if (CB_DisplayForm.IsEnabled)
        {
            var selectedForm = Dex.GetSelectedForm(species);
            if (!CB_DisplayForm.SetValue(selectedForm))
                CB_DisplayForm.SelectedIndex = 0;
        }

        CHK_A.IsChecked = Dex.GetSelectedAlpha(species);
        CHK_S.IsChecked = Dex.GetSelectedShiny(species);
        CHK_G.IsEnabled = PokedexSave8a.HasMultipleGenders(species);
        CHK_G.IsChecked = Dex.GetSelectedGender1(species);

        var reportedRate = Dex.GetPokeResearchRate(species);
        var unreportedRate = reportedRate;
        var tasks = PokedexConstants8a.ResearchTasks[index];
        for (var i = 0; i < tasks.Length && i < TaskControls.Length; i++)
        {
            var unreportedLevels = Dex.GetResearchTaskLevel(species, i, out _, out var taskValue, out _);
            TaskControls[i].CurrentValue = taskValue;
            unreportedRate += unreportedLevels * TaskControls[i].PointsPerLevel;
        }

        MTB_UpdateIndex.Text = Dex.GetUpdateIndex(species).ToString();
        MTB_ResearchLevelReported.Text = reportedRate.ToString();
        MTB_ResearchLevelUnreported.Text = unreportedRate.ToString();

        CHK_Seen.IsChecked = Dex.HasPokeEverBeenUpdated(species);
        CHK_Complete.IsChecked = Dex.IsComplete(species);
        CHK_Perfect.IsChecked = Dex.IsPerfect(species);

        Dex.GetSizeStatistics(species, form, out var hasMax, out var minHeight, out var maxHeight, out var minWeight, out var maxWeight);
        CHK_MinAndMax.IsChecked = hasMax;
        TB_MinHeight.Text = minHeight.ToString(CultureInfo.InvariantCulture);
        TB_MaxHeight.Text = maxHeight.ToString(CultureInfo.InvariantCulture);
        TB_MinWeight.Text = minWeight.ToString(CultureInfo.InvariantCulture);
        TB_MaxWeight.Text = maxWeight.ToString(CultureInfo.InvariantCulture);

        var pi = SAV.Personal.GetFormEntry(species, form);
        L_TheoryHeight.Text = $"Min: {PA8.GetHeightAbsolute(pi, 0x00).ToString(CultureInfo.InvariantCulture)}, Max: {PA8.GetHeightAbsolute(pi, 0xFF).ToString(CultureInfo.InvariantCulture)}";
        L_TheoryWeight.Text = $"Min: {PA8.GetWeightAbsolute(pi, 0x00, 0x00).ToString(CultureInfo.InvariantCulture)}, Max: {PA8.GetWeightAbsolute(pi, 0xFF, 0xFF).ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>True when nothing about this entry has been recorded, so it should not be marked as updated.</summary>
    private bool IsEntryEmpty(int index, int formIndex)
    {
        var species = DexToSpecies[index + 1];
        byte form = (byte)DisplayedForms[formIndex].Value;

        for (var i = 0; i < FlagCount; i++)
        {
            if (CHK_SeenWild[i].IsChecked == true || CHK_Obtained[i].IsChecked == true || CHK_CaughtWild[i].IsChecked == true)
                return false;
        }

        if ((CHK_G.IsEnabled && CHK_G.IsChecked == true) || CHK_S.IsChecked == true || CHK_A.IsChecked == true)
            return false;

        var tasks = PokedexConstants8a.ResearchTasks[index];
        for (var i = 0; i < tasks.Length && i < TaskControls.Length; i++)
        {
            Dex.GetResearchTaskLevel(species, i, out var reportedLevels, out _, out _);
            if (reportedLevels > 1)
                return false;
            if (TaskControls[i].CurrentValue != 0)
                return false;
        }

        if (CHK_Complete.IsChecked == true || CHK_Perfect.IsChecked == true)
            return false;

        Dex.GetSizeStatistics(species, form, out _, out var oldMinHeight, out var oldMaxHeight, out var oldMinWeight, out var oldMaxWeight);
        var minHeight = Parse(TB_MinHeight, oldMinHeight);
        var maxHeight = Parse(TB_MaxHeight, oldMaxHeight);
        var minWeight = Parse(TB_MinWeight, oldMinWeight);
        var maxWeight = Parse(TB_MaxWeight, oldMaxWeight);

        if (CHK_MinAndMax.IsChecked == true)
            return false;
        return minHeight == 0 && maxHeight == 0 && minWeight == 0 && maxWeight == 0;
    }

    private static float Parse(TextBox tb, float fallback)
        => float.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private void SetEntry(int index, int formIndex)
    {
        if (!CanSave || Editing || index < 0 || formIndex < 0 || formIndex >= DisplayedForms.Count)
            return;

        var empty = IsEntryEmpty(index, formIndex);
        if (CHK_Seen.IsChecked != true && empty)
            return;

        var species = DexToSpecies[index + 1];
        var form = (byte)DisplayedForms[formIndex].Value;

        if (!empty)
            Dex.SetPokeHasBeenUpdated(species);

        var seenWild = 0;
        var obtain = 0;
        var caughtWild = 0;
        for (var i = 0; i < FlagCount; i++)
        {
            seenWild |= CHK_SeenWild[i].IsChecked == true ? 1 << i : 0;
            obtain |= CHK_Obtained[i].IsChecked == true ? 1 << i : 0;
            caughtWild |= CHK_CaughtWild[i].IsChecked == true ? 1 << i : 0;
        }

        Dex.SetPokeSeenInWildFlags(species, form, (byte)seenWild);
        Dex.SetPokeObtainFlags(species, form, (byte)obtain);
        Dex.SetPokeCaughtInWildFlags(species, form, (byte)caughtWild);
        Dex.SetSolitudeComplete(species, CHK_Solitude.IsChecked == true);

        var dispForm = form;
        if (CB_DisplayForm.IsEnabled)
            dispForm = (byte)(CB_DisplayForm.GetSelectedItem()?.Value ?? form);
        Dex.SetSelectedGenderForm(species, dispForm, CHK_G.IsChecked == true, CHK_S.IsChecked == true, CHK_A.IsChecked == true);

        var tasks = PokedexConstants8a.ResearchTasks[index];
        for (var i = 0; i < tasks.Length && i < TaskControls.Length; i++)
        {
            if (TaskControls[i].CanSetCurrentValue)
                Dex.SetResearchTaskProgressByForce(species, TaskControls[i].Task, TaskControls[i].CurrentValue);
        }

        Dex.GetSizeStatistics(species, form, out _, out var oldMinHeight, out var oldMaxHeight, out var oldMinWeight, out var oldMaxWeight);
        Dex.SetSizeStatistics(species, form, CHK_MinAndMax.IsChecked == true,
            Parse(TB_MinHeight, oldMinHeight), Parse(TB_MaxHeight, oldMaxHeight),
            Parse(TB_MinWeight, oldMinWeight), Parse(TB_MaxWeight, oldMaxWeight));
    }

    #endregion

    #region Handlers

    /// <summary>The "obtain every form" task tracks the obtained-form checkboxes live.</summary>
    private void ObtainFlagChanged()
    {
        if (Editing || lastIndex < 0 || lastForm < 0 || lastForm >= DisplayedForms.Count)
            return;

        var overrideObtainFlags = 0;
        for (var i = 0; i < CHK_Obtained.Length; i++)
        {
            if (CHK_Obtained[i].IsChecked == true)
                overrideObtainFlags |= 1 << i;
        }

        var tasks = PokedexConstants8a.ResearchTasks[lastIndex];
        var species = DexToSpecies[lastIndex + 1];
        var form = DisplayedForms[lastForm].Value;

        for (var i = 0; i < tasks.Length && i < TaskControls.Length; i++)
        {
            if (tasks[i].Task != PokedexResearchTaskType8a.ObtainForms)
                continue;
            var formCount = Dex.GetObtainedFormCounts(species, form | (overrideObtainFlags << 16));
            if (TaskControls[i].CurrentValue != formCount)
                TaskControls[i].CurrentValue = formCount;
        }
    }

    /// <summary>Opens the full research task editor for the selected species (WinForms <c>B_AdvancedResearch_Click</c>).</summary>
    private async System.Threading.Tasks.Task ClickAdvancedResearch()
    {
        // Set the entry
        SetEntry(lastIndex, lastForm);

        var form = new PokedexResearchEditorLAWindow(SAV, DexToSpecies[lastIndex + 1], lastIndex, TaskDescriptions, TimeTaskDescriptions);
        await form.ShowDialog(this);

        Editing = true;

        // Refresh all tasks and values
        FillResearchTasks(lastIndex);
        GetEntry(lastIndex, lastForm);
        Editing = false;
    }

    private void ClickReport()
    {
        SetEntry(lastIndex, lastForm);
        Editing = true;
        Dex.UpdateSpecificReportPoke(DexToSpecies[lastIndex + 1], out _);
        FillResearchTasks(lastIndex);
        GetEntry(lastIndex, lastForm);
        Editing = false;
    }

    #endregion

    protected override void OnSave()
    {
        SetEntry(lastIndex, lastForm);
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
