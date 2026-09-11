using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using PKHeX.Core.Searching;
using PKHeX.Drawing.PokeSprite;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Encounter database browser (port of the WinForms <c>SAV_Encounters</c>).
/// </summary>
public sealed class EncounterDatabaseWindow : Window
{
    private const int GridWidth = 6;
    private const int GridHeight = 11;

    private readonly PKMEditorView PKME_Tabs;
    private SaveFile SAV => PKME_Tabs.RequestSaveFile;
    private readonly TrainerDatabase Trainers;
    private readonly CancellationTokenSource TokenSource = new();
    private readonly EntityInstructionBuilderView UC_Builder;

    // Criteria backing value (edited via the property grid)
    private EncounterCriteria _criteriaValue = EncounterCriteria.Unrestricted;

    private List<IEncounterInfo> Results = [];
    private int slotSelected = -1;
    private SlotTouchType slotColor = SlotTouchType.None;
    private readonly string Counter;

    private readonly PokeGrid EncounterPokeGrid = new() { Name = "EncounterPokeGrid" };
    private readonly ScrollBar SCR_Box = new() { Orientation = Orientation.Vertical, Minimum = 0, Maximum = 0, Width = 18, Visibility = ScrollBarVisibility.Visible };
    private readonly TabControl TC_SearchOptions = new() { Name = "TC_SearchOptions" };
    private readonly TabItem Tab_General = new() { Name = "Tab_General", Header = "General" };
    private readonly TabItem Tab_Advanced = new() { Name = "Tab_Advanced", Header = "Advanced" };
    private readonly TabItem Tab_Criteria = new() { Name = "Tab_Criteria", Header = "Criteria" };
    private readonly TabItem Tab_Settings = new() { Name = "Tab_Settings", Header = "Settings" };
    private readonly PropertyGridView PG_Settings = new();
    private readonly PropertyGridView PG_Criteria = new();
    private readonly TextBox RTB_Instructions = new() { Name = "RTB_Instructions", AcceptsReturn = true, MinHeight = 120, FontFamily = new global::Avalonia.Media.FontFamily("monospace") };

    private readonly TextBlock Label_Species = UiFactory.Label("Label_Species", "Species:");
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 170);
    private readonly TextBlock L_Move1 = UiFactory.Label("L_Move1", "Move 1:");
    private readonly ComboBox CB_Move1 = UiFactory.Combo("CB_Move1", 170);
    private readonly TextBlock L_Move2 = UiFactory.Label("L_Move2", "Move 2:");
    private readonly ComboBox CB_Move2 = UiFactory.Combo("CB_Move2", 170);
    private readonly TextBlock L_Move3 = UiFactory.Label("L_Move3", "Move 3:");
    private readonly ComboBox CB_Move3 = UiFactory.Combo("CB_Move3", 170);
    private readonly TextBlock L_Move4 = UiFactory.Label("L_Move4", "Move 4:");
    private readonly ComboBox CB_Move4 = UiFactory.Combo("CB_Move4", 170);
    private readonly TextBlock L_Version = UiFactory.Label("L_Version", "OT Version:");
    private readonly ComboBox CB_GameOrigin = UiFactory.Combo("CB_GameOrigin", 170);
    private readonly CheckBox CHK_Shiny = UiFactory.Check("CHK_Shiny", "Shiny");
    private readonly CheckBox CHK_IsEgg = UiFactory.Check("CHK_IsEgg", "Egg");
    private readonly StackPanel TypeFilters = new() { Name = "TypeFilters", Orientation = Orientation.Vertical, Spacing = 1 };
    private readonly Button B_Search = UiFactory.Button("B_Search", "Search!");
    private readonly Button B_Reset = UiFactory.Button("B_Reset", "Reset Filters");
    private readonly Button B_Add = UiFactory.Button("B_Add", "Add");
    private readonly Button B_CriteriaReset = UiFactory.Button("B_CriteriaReset", "Reset");
    private readonly Button B_CriteriaFromTabs = UiFactory.Button("B_CriteriaFromTabs", "From Editor");
    private readonly TextBlock L_Count = UiFactory.Label("L_Count", "Count: {0}");
    private readonly TextBlock L_Viewed = UiFactory.Label("L_Viewed", "Last Viewed: {0}");
    private readonly MenuItem Menu_Exit = new() { Name = "Menu_Exit", Header = "_Close" };
    private readonly ContextMenu SlotMenu = new();
    private SlotView? menuSlot;

    public EncounterDatabaseWindow(PKMEditorView f1, TrainerDatabase db)
    {
        Name = "SAV_Encounters";
        Title = "Database";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 1030;
        Height = 690;

        PKME_Tabs = f1;
        Trainers = db;
        UC_Builder = new EntityInstructionBuilderView(() => f1.PreparePKM()) { ReadOnly = true };
        CHK_Shiny.IsThreeState = CHK_IsEgg.IsThreeState = true;

        var menuFile = new MenuItem { Name = "Menu_Close", Header = "File" };
        menuFile.Items.Add(Menu_Exit);
        var menu = new Menu();
        menu.Items.Add(menuFile);

        var filters = UiFactory.FormGrid(7);
        UiFactory.AddFormRow(filters, 0, Label_Species, CB_Species);
        UiFactory.AddFormRow(filters, 1, L_Move1, CB_Move1);
        UiFactory.AddFormRow(filters, 2, L_Move2, CB_Move2);
        UiFactory.AddFormRow(filters, 3, L_Move3, CB_Move3);
        UiFactory.AddFormRow(filters, 4, L_Move4, CB_Move4);
        UiFactory.AddFormRow(filters, 5, L_Version, CB_GameOrigin);
        UiFactory.AddFormRow(filters, 6, null, UiFactory.Row(CHK_Shiny, CHK_IsEgg));
        Tab_General.Content = new ScrollViewer { Content = UiFactory.Column(filters, TypeFilters) };
        Tab_Advanced.Content = UiFactory.Column(UiFactory.Row(UC_Builder, B_Add), RTB_Instructions);
        Tab_Criteria.Content = new DockPanel().WithTop(UiFactory.Row(B_CriteriaReset, B_CriteriaFromTabs), PG_Criteria);
        Tab_Settings.Content = PG_Settings;
        TC_SearchOptions.Items.Add(Tab_General);
        TC_SearchOptions.Items.Add(Tab_Advanced);
        TC_SearchOptions.Items.Add(Tab_Criteria);
        TC_SearchOptions.Items.Add(Tab_Settings);

        var buttons = UiFactory.Row(B_Search, B_Reset, L_Count);
        var left = new DockPanel { Margin = new Thickness(6) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        left.Children.Add(buttons);
        left.Children.Add(TC_SearchOptions);

        var gridPanel = new DockPanel { Margin = new Thickness(6) };
        DockPanel.SetDock(SCR_Box, Dock.Right);
        DockPanel.SetDock(L_Viewed, Dock.Bottom);
        gridPanel.Children.Add(SCR_Box);
        gridPanel.Children.Add(L_Viewed);
        gridPanel.Children.Add(EncounterPokeGrid);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        Grid.SetColumn(gridPanel, 1);
        body.Children.Add(left);
        body.Children.Add(gridPanel);

        var root = new DockPanel();
        DockPanel.SetDock(menu, Dock.Top);
        root.Children.Add(menu);
        root.Children.Add(body);
        Content = root;

        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
        PG_Settings.SetObject(MainWindow.Settings.EncounterDb);

        EncounterPokeGrid.InitializeGrid(GridWidth, GridHeight, SpriteUtil.Spriter);
        EncounterPokeGrid.SetBackground(AppResources.GetSkBitmap("box_wp_clean") ?? SpriteUtil.Spriter.Transparent);
        foreach (var slot in EncounterPokeGrid.Entries)
        {
            slot.AttachClickHandled(async mods =>
            {
                if (mods == KeyModifiers.Control)
                    await ClickView(slot);
            });
            slot.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(slot).Properties.IsRightButtonPressed)
                {
                    menuSlot = slot;
                    SlotMenu.Open(slot);
                }
            };
        }
        var mnuView = new MenuItem { Name = "mnuView", Header = "View" };
        mnuView.Click += async (_, _) => await ClickView(menuSlot);
        SlotMenu.Items.Add(mnuView);
        Translator.TranslateControls(SlotMenu, "SAV_Encounters", MainWindow.CurrentLanguage);

        EncounterPokeGrid.PointerWheelChanged += (_, e) =>
        {
            int newval = (int)SCR_Box.Value + (e.Delta.Y < 0 ? 1 : -1);
            if (newval < SCR_Box.Minimum || SCR_Box.Maximum < newval)
                return;
            SCR_Box.Value = newval;
            e.Handled = true;
        };
        SCR_Box.Scroll += (_, _) => FillPKXBoxes((int)SCR_Box.Value);

        Counter = L_Count.Text ?? "Count: {0}";
        L_Viewed.Text = string.Empty; // invisible for now
        PopulateComboBoxes();
        GetTypeFilters();

        // Initialize criteria grid with default value
        UpdateCriteriaPropertyGrid(BuildCriteriaFromTabs());

        B_Search.Click += async (_, _) => await B_Search_Click();
        B_Reset.Click += (_, _) => ResetFilters();
        B_Add.Click += async (_, _) => await B_Add_Click();
        B_CriteriaReset.Click += (_, _) => UpdateCriteriaPropertyGrid(EncounterCriteria.Unrestricted);
        B_CriteriaFromTabs.Click += (_, _) => UpdateCriteriaPropertyGrid(BuildCriteriaFromTabs());
        Menu_Exit.Click += (_, _) => Close();
        CB_Species.SelectionChanged += (_, _) => CheckIsSearchAllowed();
        Closing += (_, _) => TokenSource.Cancel();

        // Load Data
        L_Count.Text = "Ready...";
        CheckIsSearchAllowed();
    }

    private void UpdateCriteriaPropertyGrid(EncounterCriteria value)
    {
        _criteriaValue = value;
        // EncounterCriteria is a record struct; edit a boxed copy and read it back when searching.
        CriteriaBox = new CriteriaHolder(value);
        PG_Criteria.SetObject(CriteriaBox);
    }

    private CriteriaHolder CriteriaBox = new(EncounterCriteria.Unrestricted);

    /// <summary>Mutable wrapper so the reflection grid can edit the criteria record.</summary>
    private sealed class CriteriaHolder(EncounterCriteria value)
    {
        public EncounterCriteria Value { get; set; } = value;
    }

    private EncounterCriteria BuildCriteriaFromTabs()
    {
        var editor = PKME_Tabs.Data;
        var set = new ShowdownSet(editor);
        var mutations = EncounterMutationUtil.GetSuggested(editor.Context, set.Level);
        var criteria = EncounterCriteria.GetCriteria(set, editor.PersonalInfo, mutations);
        if (editor.Context.IsHyperTrainingAvailable(100))
            criteria = criteria.ReviseIVsHyperTrainAvailable();
        return criteria;
    }

    private void GetTypeFilters()
    {
        var types = Enum.GetValues<EncounterTypeGroup>();
        foreach (var z in types.Where(z => z != 0))
        {
            var chk = UiFactory.Check(z.ToString(), z.ToString());
            chk.IsChecked = true;
            TypeFilters.Children.Add(chk);
            chk.AttachClick(mods =>
            {
                if ((mods & KeyModifiers.Shift) == 0)
                    return;
                foreach (var c in TypeFilters.Children.OfType<CheckBox>())
                    c.IsChecked = ReferenceEquals(c, chk);
            });
            chk.IsCheckedChanged += (_, _) => CheckIsSearchAllowed();
        }
    }

    private EncounterTypeGroup[] GetTypes() => TypeFilters.Children.OfType<CheckBox>()
        .Where(z => z.IsChecked == true).Select(z => z.Name!)
        .Select(Enum.Parse<EncounterTypeGroup>).ToArray();

    private bool GetShiftedIndex(ref int index)
    {
        if (index >= EncounterPokeGrid.Entries.Count)
            return false;
        index += (int)SCR_Box.Value * GridWidth;
        return index < Results.Count;
    }

    private async Task ClickView(SlotView? pb)
    {
        if (pb is null)
            return;
        int index = EncounterPokeGrid.Entries.IndexOf(pb);
        if (!GetShiftedIndex(ref index))
            return;

        var enc = Results[index];
        var criteria = GetCriteria(enc, MainWindow.Settings.EncounterDb);
        var trainer = Trainers.GetTrainer(enc.Version, enc.Generation <= 2 ? (LanguageID)SAV.Language : null) ?? SAV;
        var temp = enc.ConvertToPKM(trainer, criteria);
        var pk = EntityConverter.ConvertToType(temp, SAV.PKMType, out var c);
        if (pk is null)
        {
            await AppDialogs.Error(this, c.GetDisplayString(temp, SAV.PKMType));
            return;
        }

        SAV.AdaptToSaveFile(pk);
        pk.RefreshChecksum();
        PKME_Tabs.PopulateFields(pk, false);
        slotSelected = index;
        slotColor = SlotTouchType.Get;
        FillPKXBoxes((int)SCR_Box.Value);
    }

    private EncounterCriteria GetCriteria(IEncounterTemplate enc, EncounterDatabaseSettings settings)
    {
        if (!settings.UseTabsAsCriteria)
            return EncounterCriteria.Unrestricted;

        var editor = PKME_Tabs.Data;
        var tree = EvolutionTree.GetEvolutionTree(editor.Context);
        bool isInChain = tree.IsSpeciesDerivedFrom(editor.Species, editor.Form, enc.Species, enc.Form);

        if (!settings.UseTabsAsCriteriaAnySpecies)
        {
            if (!isInChain)
                return EncounterCriteria.Unrestricted;
        }

        var criteria = CriteriaBox.Value;
        // Sanity check gender.
        if (!isInChain || EntityGender.IsSingleGender(enc.Species))
            criteria = criteria with { Gender = Gender.Random }; // Genderless tabs and a gendered enc -> let's play safe.
        // Sanity check ability.
        if (!criteria.Mutations.CanGetAbility(enc.Ability, criteria.Ability))
            criteria = criteria with { Ability = AbilityPermission.Any12H }; // ignore the Ability requested by user, it's impossible.
        return criteria;
    }

    private void PopulateComboBoxes()
    {
        var Any = new ComboItem(MsgAny, 0);
        var filtered = GameInfo.FilteredSources;
        var source = filtered.Source;
        var species = new List<ComboItem>(source.SpeciesDataSource)
        {
            [0] = Any, // Replace (None) with "Any"
        };
        CB_Species.SetItems(species);

        // Set the Move ComboBoxes too.
        var DS_Move = new List<ComboItem>(filtered.Moves);
        DS_Move.RemoveAt(0);
        DS_Move.Insert(0, Any);
        foreach (var cb in new[] { CB_Move1, CB_Move2, CB_Move3, CB_Move4 })
            cb.SetItems(DS_Move);

        var DS_Version = new List<ComboItem>(source.VersionDataSource);
        DS_Version.Insert(0, Any);
        DS_Version.RemoveAt(DS_Version.Count - 1);
        CB_GameOrigin.SetItems(DS_Version);

        ResetFilters();
    }

    private void ResetFilters()
    {
        CB_Species.SelectedIndex = 0;
        CB_Move1.SelectedIndex = CB_Move2.SelectedIndex = CB_Move3.SelectedIndex = CB_Move4.SelectedIndex = 0;
        CB_GameOrigin.SelectedIndex = 0;

        RTB_Instructions.Text = string.Empty;
        CHK_Shiny.IsChecked = CHK_IsEgg.IsChecked = null;
        foreach (var chk in TypeFilters.Children.OfType<CheckBox>())
            chk.IsChecked = true;
    }

    // View Updates
    private IEnumerable<IEncounterInfo> SearchDatabase(CancellationToken token)
    {
        var settings = GetSearchSettings();

        // If nothing is specified, instead of just returning all possible encounters, just return nothing.
        if (!IsSearchAllowed(settings))
            return [];
        var pk = SAV.BlankPKM;

        var moves = settings.Moves.ToArray();
        var versions = settings.GetVersions(SAV);
        var species = settings.Species == 0 ? GetFullRange(SAV.MaxSpeciesID) : [settings.Species];
        var results = GetAllSpeciesFormEncounters(species, SAV.Personal, versions, moves, pk, token);
        if (settings.SearchEgg is not null)
            results = results.Where(z => z.IsEgg == settings.SearchEgg);
        if (settings.SearchShiny is not null)
            results = results.Where(z => z.IsShiny == settings.SearchShiny);

        // return filtered results
        var comparer = new ReferenceComparer<IEncounterInfo>();
        results = results.Distinct(comparer); // only distinct objects

        if (MainWindow.Settings.EncounterDb.FilterUnavailableSpecies)
        {
            var filter = EntityPresenceFilters.GetFilterGeneric<IEncounterInfo>(SAV.Context);
            if (filter != null)
                results = results.Where(filter);
        }

        if (token.IsCancellationRequested)
            return results;

        ReadOnlySpan<char> batchText = RTB_Instructions.Text ?? string.Empty;
        if (batchText.Length != 0 && !StringInstructionSet.HasEmptyLine(batchText))
        {
            var filters = StringInstruction.GetFilters(batchText);
            EntityBatchEditor.ScreenStrings(filters);
            results = results.Where(enc => BatchEditingUtil.IsFilterMatch(filters, enc)); // Compare across all filters
        }

        return results;
    }

    private bool IsSearchAllowed(SearchSettings settings)
    {
        if (!TypeFilters.Children.OfType<CheckBox>().Any(z => z.IsChecked == true))
            return false; // no types selected
        if (settings is { Species: 0, Moves.Count: 0 } && MainWindow.Settings.EncounterDb.ReturnNoneIfEmptySearch)
            return false;
        return true;
    }

    private static IEnumerable<ushort> GetFullRange(int max)
    {
        for (ushort i = 1; i <= max; i++)
            yield return i;
    }

    private IEnumerable<IEncounterInfo> GetAllSpeciesFormEncounters(IEnumerable<ushort> species, IPersonalTable pt, ReadOnlyMemory<GameVersion> versions, ReadOnlyMemory<ushort> moves, PKM pk, CancellationToken token)
    {
        foreach (var s in species)
        {
            if (token.IsCancellationRequested)
                break;

            var pi = pt.GetFormEntry(s, 0);
            var fc = pi.FormCount;
            if (fc == 0 && !MainWindow.Settings.EncounterDb.FilterUnavailableSpecies) // not present in game
            {
                // try again using past-gen table
                pi = PersonalTable.USUM.GetFormEntry(s, 0);
                fc = pi.FormCount;
            }
            for (byte f = 0; f < fc; f++)
            {
                if (FormInfo.IsBattleOnlyForm(s, f, pk.Format))
                    continue;
                var encs = GetEncounters(s, f, moves, pk, versions);
                foreach (var enc in encs)
                    yield return enc;
            }
        }
    }

    private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        public bool Equals([NotNullWhen(true)] T? x, [NotNullWhen(true)] T? y)
        {
            if (x is null)
                return false;
            if (y is null)
                return false;
            return RuntimeHelpers.GetHashCode(x).Equals(RuntimeHelpers.GetHashCode(y));
        }

        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private IEnumerable<IEncounterInfo> GetEncounters(ushort species, byte form, ReadOnlyMemory<ushort> moves, PKM pk, ReadOnlyMemory<GameVersion> vers)
    {
        pk.Species = species;
        pk.Form = form;
        pk.SetGender(pk.GetSaneGender());
        EncounterMovesetGenerator.OptimizeCriteria(pk, SAV);
        return EncounterMovesetGenerator.GenerateEncounters(pk, moves, vers);
    }

    private SearchSettings GetSearchSettings()
    {
        var settings = new SearchSettings
        {
            Context = SAV.Context,
            Generation = SAV.Generation,

            Species = GetU16(CB_Species),

            BatchInstructions = RTB_Instructions.Text ?? string.Empty,
            Version = (GameVersion)CB_GameOrigin.GetValue(),
        };

        static ushort GetU16(ComboBox cb)
        {
            var val = cb.GetValue();
            if (val <= 0)
                return 0;
            return (ushort)val;
        }

        settings.AddMove(GetU16(CB_Move1));
        settings.AddMove(GetU16(CB_Move2));
        settings.AddMove(GetU16(CB_Move3));
        settings.AddMove(GetU16(CB_Move4));

        if (CHK_IsEgg.IsChecked is { } egg)
            settings.SearchEgg = egg;

        if (CHK_Shiny.IsChecked is { } shiny)
            settings.SearchShiny = shiny;

        return settings;
    }

    private async Task B_Search_Click()
    {
        B_Search.IsEnabled = false;
        EncounterMovesetGenerator.PriorityList = GetTypes();

        var token = TokenSource.Token;
        var search = SearchDatabase(token);
        if (token.IsCancellationRequested)
        {
            EncounterMovesetGenerator.ResetFilters();
            return;
        }

        var results = await Task.Run(search.ToList, token).ConfigureAwait(true);
        if (token.IsCancellationRequested)
        {
            EncounterMovesetGenerator.ResetFilters();
            return;
        }

        if (results.Count == 0)
            await AppDialogs.Alert(this, MsgDBSearchNone);

        SetResults(results); // updates Count Label as well.
        B_Search.IsEnabled = true;
        EncounterMovesetGenerator.ResetFilters();
    }

    private void SetResults(List<IEncounterInfo> res)
    {
        Results = res;

        SCR_Box.Maximum = (int)Math.Ceiling((decimal)Results.Count / GridWidth);
        if (SCR_Box.Maximum > 0)
            SCR_Box.Maximum--;

        slotSelected = -1; // reset the slot last viewed
        SCR_Box.Value = 0;
        FillPKXBoxes(0);

        L_Count.Text = string.Format(Counter, Results.Count);
        B_Search.IsEnabled = true;
    }

    private void FillPKXBoxes(int start)
    {
        var boxes = EncounterPokeGrid.Entries;
        if (Results.Count == 0)
        {
            foreach (var slot in boxes)
            {
                slot.Sprite = null;
                slot.BackgroundBitmap = null;
            }
            return;
        }

        // Load new sprites
        int begin = start * GridWidth;
        int end = Math.Min(boxes.Count, Results.Count - begin);
        for (int i = 0; i < end; i++)
        {
            boxes[i].Sprite = Results[i + begin].Sprite().ToAvaloniaBitmapAndDispose();
        }

        // Clear empty slots
        for (int i = end; i < boxes.Count; i++)
            boxes[i].Sprite = null;

        // Reset backgrounds for all
        foreach (var slot in boxes)
            slot.BackgroundBitmap = null;

        // Reload last viewed index's background if still within view
        if (slotSelected != -1 && slotSelected >= begin && slotSelected < begin + boxes.Count)
            boxes[slotSelected - begin].BackgroundBitmap = SlotUtil.GetTouchTypeBackground(slotColor);
    }

    private void CheckIsSearchAllowed()
    {
        var settings = GetSearchSettings();
        B_Search.IsEnabled = IsSearchAllowed(settings);
    }

    private async Task B_Add_Click()
    {
        var s = UC_Builder.Create();
        if (s.Length == 0)
        {
            await AppDialogs.Alert(this, MsgBEPropertyInvalid);
            return;
        }

        // If we already have text, add a new line (except if the last line is blank).
        var batchText = RTB_Instructions.Text ?? string.Empty;
        if (batchText.Length != 0 && !batchText.EndsWith('\n'))
            batchText += Environment.NewLine;
        RTB_Instructions.Text = batchText + s;
    }
}

internal static class DockPanelExtensions
{
    /// <summary>Docks <paramref name="top"/> to the top of the panel and fills the rest with <paramref name="fill"/>.</summary>
    public static DockPanel WithTop(this DockPanel panel, Control top, Control fill)
    {
        DockPanel.SetDock(top, Dock.Top);
        panel.Children.Add(top);
        panel.Children.Add(fill);
        return panel;
    }
}
