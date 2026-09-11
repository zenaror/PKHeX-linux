using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
/// Entity database browser (port of the WinForms <c>SAV_Database</c>).
/// </summary>
public sealed class DatabaseWindow : Window
{
    private const int GridWidth = 6;
    private const int GridHeight = 11;

    private readonly SaveFile SAV;
    private readonly SAVEditorView BoxView;
    private readonly PKMEditorView PKME_Tabs;
    private readonly EntityInstructionBuilderView UC_Builder;
    private readonly string DatabasePath = MainWindow.DatabasePath;

    private List<SlotCache> Results = [];
    private List<SlotCache> RawDB = [];
    private int slotSelected = -1;
    private SlotTouchType slotColor = SlotTouchType.None;
    private readonly string Counter;
    private readonly string Viewed;
    private readonly CancellationTokenSource cts = new();

    private readonly PokeGrid DatabasePokeGrid = new() { Name = "DatabasePokeGrid" };
    private readonly ScrollBar SCR_Box = new() { Orientation = Orientation.Vertical, Minimum = 0, Maximum = 0, Width = 18, Visibility = ScrollBarVisibility.Visible };
    private readonly EntitySearchView UC_EntitySearch = new() { Name = "UC_EntitySearch" };
    private readonly TextBox RTB_Instructions = new() { Name = "RTB_Instructions", AcceptsReturn = true, MinHeight = 120, FontFamily = new global::Avalonia.Media.FontFamily("monospace") };
    private readonly TabControl TC_SearchSettings = new() { Name = "TC_SearchSettings" };
    private readonly TabItem Tab_General = new() { Name = "Tab_General", Header = "General" };
    private readonly TabItem Tab_Advanced = new() { Name = "Tab_Advanced", Header = "Advanced" };
    private readonly TabItem Tab_Settings = new() { Name = "Tab_Settings", Header = "Settings" };
    private readonly PropertyGridView PG_Settings = new();
    private readonly Button B_Search = UiFactory.Button("B_Search", "Search!");
    private readonly Button B_Reset = UiFactory.Button("B_Reset", "Reset Filters");
    private readonly Button B_Add = UiFactory.Button("B_Add", "Add");
    private readonly TextBlock L_Count = UiFactory.Label("L_Count", "Count: {0}");
    private readonly TextBlock L_Viewed = UiFactory.Label("L_Viewed", "Last Viewed: {0}");

    // Menu
    private readonly MenuItem Menu_SearchBoxes = new() { Name = "Menu_SearchBoxes", Header = "Search Within Boxes", ToggleType = MenuItemToggleType.CheckBox, IsChecked = true };
    private readonly MenuItem Menu_SearchDatabase = new() { Name = "Menu_SearchDatabase", Header = "Search Within Database", ToggleType = MenuItemToggleType.CheckBox, IsChecked = true };
    private readonly MenuItem Menu_SearchBackups = new() { Name = "Menu_SearchBackups", Header = "Search Within Backups", ToggleType = MenuItemToggleType.CheckBox, IsChecked = true };
    private readonly MenuItem Menu_SearchLegal = new() { Name = "Menu_SearchLegal", Header = "Show Legal", ToggleType = MenuItemToggleType.CheckBox, IsChecked = true };
    private readonly MenuItem Menu_SearchIllegal = new() { Name = "Menu_SearchIllegal", Header = "Show Illegal", ToggleType = MenuItemToggleType.CheckBox, IsChecked = true };
    private readonly MenuItem Menu_SearchClones = new() { Name = "Menu_SearchClones", Header = "Clones Only", ToggleType = MenuItemToggleType.CheckBox };
    private readonly MenuItem Menu_OpenDB = new() { Name = "Menu_OpenDB", Header = "Open Database Folder" };
    private readonly MenuItem Menu_Report = new() { Name = "Menu_Report", Header = "Create Data Report" };
    private readonly MenuItem Menu_Export = new() { Name = "Menu_Export", Header = "Export Results to Folder" };
    private readonly MenuItem Menu_Import = new() { Name = "Menu_Import", Header = "Import Results to SaveFile" };
    private readonly MenuItem Menu_DeleteClones = new() { Name = "Menu_DeleteClones", Header = "Delete Clones" };
    private readonly MenuItem Menu_Exit = new() { Name = "Menu_Exit", Header = "_Close" };

    private readonly ContextMenu SlotMenu = new();

    public DatabaseWindow(PKMEditorView f1, SAVEditorView saveditor)
    {
        Name = "SAV_Database";
        Title = "Database";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 1030;
        Height = 690;

        SAV = saveditor.SAV;
        BoxView = saveditor;
        PKME_Tabs = f1;

        UC_Builder = new EntityInstructionBuilderView(() => f1.PreparePKM()) { ReadOnly = true };

        // Menus
        var menuFile = new MenuItem { Name = "Menu_Close", Header = "File" };
        menuFile.Items.Add(Menu_Exit);
        var menuTools = new MenuItem { Name = "Menu_Tools", Header = "Tools" };
        var menuSearchSettings = new MenuItem { Name = "Menu_SearchSettings", Header = "Search Settings" };
        foreach (var item in new[] { Menu_SearchBoxes, Menu_SearchDatabase, Menu_SearchBackups, Menu_SearchLegal, Menu_SearchIllegal, Menu_SearchClones })
            menuSearchSettings.Items.Add(item);
        foreach (var item in new[] { menuSearchSettings, Menu_OpenDB, Menu_Report, Menu_Export, Menu_Import, Menu_DeleteClones })
            menuTools.Items.Add(item);
        var menu = new Menu();
        menu.Items.Add(menuFile);
        menu.Items.Add(menuTools);

        // Search panel
        Tab_General.Content = UC_EntitySearch;
        Tab_Advanced.Content = UiFactory.Column(UiFactory.Row(UC_Builder, B_Add), RTB_Instructions);
        Tab_Settings.Content = PG_Settings;
        TC_SearchSettings.Items.Add(Tab_General);
        TC_SearchSettings.Items.Add(Tab_Advanced);
        TC_SearchSettings.Items.Add(Tab_Settings);

        var buttons = UiFactory.Row(B_Search, B_Reset, L_Count);
        var left = new DockPanel { Margin = new Thickness(6) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        left.Children.Add(buttons);
        left.Children.Add(TC_SearchSettings);

        var gridPanel = new DockPanel { Margin = new Thickness(6) };
        DockPanel.SetDock(SCR_Box, Dock.Right);
        DockPanel.SetDock(L_Viewed, Dock.Bottom);
        gridPanel.Children.Add(SCR_Box);
        gridPanel.Children.Add(L_Viewed);
        gridPanel.Children.Add(DatabasePokeGrid);

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
        PG_Settings.SetObject(MainWindow.Settings.EntityDb);

        // Slot grid
        DatabasePokeGrid.InitializeGrid(GridWidth, GridHeight, SpriteUtil.Spriter);
        DatabasePokeGrid.SetBackground(AppResources.GetSkBitmap("box_wp_clean") ?? SpriteUtil.Spriter.Transparent);
        foreach (var slot in DatabasePokeGrid.Entries)
        {
            slot.AttachClickHandled(mods => _ = SlotClick(slot, mods));
            slot.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(slot).Properties.IsRightButtonPressed)
                    OpenSlotMenu(slot);
            };
        }
        BuildSlotMenu();

        DatabasePokeGrid.PointerWheelChanged += (_, e) =>
        {
            int newval = (int)SCR_Box.Value + (e.Delta.Y < 0 ? 1 : -1);
            if (newval < SCR_Box.Minimum || SCR_Box.Maximum < newval)
                return;
            SCR_Box.Value = newval;
            e.Handled = true;
        };
        SCR_Box.Scroll += (_, _) => FillPKXBoxes((int)SCR_Box.Value);

        if (!Directory.Exists(DatabasePath))
            Menu_OpenDB.IsVisible = false;

        // Preset Filters to only show PKM available for loaded save
        // (populate first: the format selection needs the item list to exist)
        UC_EntitySearch.PopulateComboBoxes(GameInfo.FilteredSources);
        UC_EntitySearch.InitializeSelections(SAV);
        UC_EntitySearch.SetFormatAnyText(MsgAny);

        Counter = L_Count.Text ?? "Count: {0}";
        Viewed = L_Viewed.Text ?? "Last Viewed: {0}";
        L_Viewed.Text = string.Empty; // invisible for now

        B_Search.Click += async (_, _) => await B_Search_Click();
        B_Reset.Click += (_, _) => ResetFilters();
        B_Add.Click += async (_, _) => await B_Add_Click();
        Menu_Exit.Click += (_, _) => Close();
        Menu_OpenDB.Click += (_, _) => OpenDB();
        Menu_Report.Click += async (_, _) => await GenerateDBReport();
        Menu_Export.Click += async (_, _) => await Menu_Export_Click();
        Menu_Import.Click += async (_, _) => await Menu_Import_Click();
        Menu_DeleteClones.Click += async (_, _) => await Menu_DeleteClones_Click();

        Closing += (_, _) => cts.Cancel();

        // Load Data
        B_Search.IsEnabled = false;
        L_Count.Text = "Loading...";
        var token = cts.Token;
        _ = Task.Run(() => LoadDatabase(token), token);
    }

    private void BuildSlotMenu()
    {
        var mnuView = new MenuItem { Name = "mnuView", Header = "_View" };
        var mnuSet = new MenuItem { Name = "mnuSet", Header = "_Set" };
        var mnuDelete = new MenuItem { Name = "mnuDelete", Header = "_Delete" };
        mnuView.Click += async (_, _) => await ClickView(menuSlot);
        mnuSet.Click += async (_, _) => await ClickSet();
        mnuDelete.Click += async (_, _) => await ClickDelete(menuSlot);
        SlotMenu.Items.Add(mnuView);
        SlotMenu.Items.Add(mnuSet);
        SlotMenu.Items.Add(mnuDelete);
        Translator.TranslateControls(SlotMenu, "SAV_Database", MainWindow.CurrentLanguage);
    }

    private SlotView? menuSlot;

    private void OpenSlotMenu(SlotView slot)
    {
        menuSlot = slot;
        SlotMenu.Open(slot);
    }

    private async Task SlotClick(SlotView slot, KeyModifiers mods)
    {
        switch (mods)
        {
            case KeyModifiers.Control: await ClickView(slot); break;
            case KeyModifiers.Alt: await ClickDelete(slot); break;
            case KeyModifiers.Shift: await ClickSet(); break;
        }
    }

    private async Task ClickView(SlotView? pb)
    {
        if (pb is null)
            return;
        int index = DatabasePokeGrid.Entries.IndexOf(pb);
        if (!GetShiftedIndex(ref index))
            return;

        var slot = Results[index];
        var temp = slot.Entity;
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
        L_Viewed.Text = string.Format(Viewed, slot.Identify());
    }

    private async Task ClickDelete(SlotView? pb)
    {
        if (pb is null)
            return;
        int index = DatabasePokeGrid.Entries.IndexOf(pb);
        if (!GetShiftedIndex(ref index))
            return;

        var entry = Results[index];
        var pk = entry.Entity;

        if (entry.Source is SlotInfoFileSingle(var path))
        {
            // Data from Database: Delete file from disk
            if (File.Exists(path))
                File.Delete(path);
        }
        else if (entry.Source is SlotInfoBox b && entry.SAV == SAV)
        {
            // Data from Box: Delete from save file
            var exist = b.Read(SAV);
            if (!exist.EqualsStored(pk)) // data modified already?
            {
                await AppDialogs.Error(this, MsgDBDeleteFailModified, MsgDBDeleteFailWarning);
                return;
            }
            BoxView.EditEnv.Slots.Delete(b);
        }
        else
        {
            await AppDialogs.Error(this, MsgDBDeleteFailBackup, MsgDBDeleteFailWarning);
            return;
        }
        // Remove from database.
        RawDB.Remove(entry);
        Results.Remove(entry);
        // Refresh database view.
        L_Count.Text = string.Format(Counter, Results.Count);
        slotSelected = -1;
        FillPKXBoxes((int)SCR_Box.Value);
    }

    private async Task ClickSet()
    {
        // Don't care what slot was clicked, just add it to the database
        if (!PKME_Tabs.EditsComplete)
            return;

        PKM pk = PKME_Tabs.PreparePKM();
        Directory.CreateDirectory(DatabasePath);

        string path = Path.Combine(DatabasePath, PathUtil.CleanFileName(pk.FileName));

        if (File.Exists(path))
        {
            await AppDialogs.Alert(this, MsgDBAddFailExistsFile);
            return;
        }

        var data = new byte[pk.SIZE_PARTY];
        pk.WriteDecryptedDataParty(data);
        File.WriteAllBytes(path, data);

        var info = new SlotInfoFileSingle(path);
        var entry = new SlotCache(info, pk);
        Results.Add(entry);

        // Refresh database view.
        L_Count.Text = string.Format(Counter, Results.Count);
        slotSelected = Results.Count - 1;
        slotColor = SlotTouchType.Set;
        if ((SCR_Box.Maximum + 1) * GridWidth < Results.Count)
            SCR_Box.Maximum++;
        SCR_Box.Value = Math.Max(0, SCR_Box.Maximum - (DatabasePokeGrid.Entries.Count / GridWidth) + 1);
        FillPKXBoxes((int)SCR_Box.Value);
        await AppDialogs.Alert(this, MsgDBAddFromTabsSuccess);
    }

    private bool GetShiftedIndex(ref int index)
    {
        if ((uint)index >= DatabasePokeGrid.Entries.Count)
            return false;
        index += (int)SCR_Box.Value * GridWidth;
        return index < Results.Count;
    }

    private void ResetFilters()
    {
        UC_EntitySearch.ResetFilters();
        RTB_Instructions.Text = string.Empty;
    }

    private async Task GenerateDBReport()
    {
        if (await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgDBCreateReportPrompt, MsgDBCreateReportWarning) != DialogResult.Yes)
            return;

        var reportGrid = new ReportGridWindow();
        var settings = MainWindow.Settings.Report;
        var extra = CollectionsMarshal.AsSpan(settings.ExtraProperties);
        var hide = CollectionsMarshal.AsSpan(settings.HiddenProperties);
        reportGrid.PopulateData(Results, extra, hide);
        reportGrid.Show(this);
    }

    private sealed class SearchFolderDetail(string path, bool ignoreBackupFiles)
    {
        public string Path { get; } = path;
        public bool IgnoreBackupFiles { get; } = ignoreBackupFiles;
    }

    private void LoadDatabase(CancellationToken token)
    {
        var settings = MainWindow.Settings;
        var otherPaths = new List<SearchFolderDetail>();
        if (settings.EntityDb.SearchExtraSaves)
            otherPaths.AddRange(settings.Backup.OtherBackupPaths.Where(Directory.Exists).Select(z => new SearchFolderDetail(z, true)));
        if (settings.EntityDb.SearchBackups)
            otherPaths.Add(new SearchFolderDetail(MainWindow.BackupPath, false));

        RawDB = LoadEntitiesFromFolder(DatabasePath, SAV, otherPaths, settings.EntityDb.SearchExtraSavesDeep, token);
        if (token.IsCancellationRequested)
            return;

        // Load stats for pk who do not have any
        foreach (var entry in RawDB)
        {
            var pk = entry.Entity;
            pk.ForcePartyData();
        }

        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!token.IsCancellationRequested)
                SetResults(RawDB);
        });
    }

    private static List<SlotCache> LoadEntitiesFromFolder(string databaseFolder, SaveFile sav, List<SearchFolderDetail> otherPaths, bool otherDeep, CancellationToken token)
    {
        var dbTemp = new ConcurrentBag<SlotCache>();
        var extensions = new HashSet<string>(EntityFileExtension.GetExtensionsAll().Select(z => $".{z}"));

        if (Directory.Exists(databaseFolder))
        {
            var files = Directory.EnumerateFiles(databaseFolder, "*", SearchOption.AllDirectories);
            Parallel.ForEach(files, file => SlotInfoLoader.AddFromLocalFile(file, dbTemp, sav, extensions));
        }

        foreach (var folder in otherPaths)
        {
            if (!SaveUtil.GetSavesFromFolder(folder.Path, otherDeep, token, out var paths, folder.IgnoreBackupFiles))
                continue;

            Parallel.ForEach(paths, file => TryAddPKMsFromSaveFilePath(dbTemp, file));
        }

        // Fetch from save file
        SlotInfoLoader.AddFromSaveFile(sav, dbTemp);
        var result = new List<SlotCache>(dbTemp);
        result.RemoveAll(z => !z.IsDataValid());

        if (MainWindow.Settings.EntityDb.FilterUnavailableSpecies)
        {
            var filter = EntityPresenceFilters.GetFilterEntity(sav.Context);
            if (filter is not null)
                result.RemoveAll(z => !filter(z.Entity));
        }

        var sort = MainWindow.Settings.EntityDb.InitialSortMode;
        if (sort is DatabaseSortMode.SlotIdentity)
            result.Sort();
        else if (sort is DatabaseSortMode.SpeciesForm)
            result.Sort((first, second) => first.CompareToSpeciesForm(second));

        // Finalize the Database
        return result;
    }

    private static void TryAddPKMsFromSaveFilePath(ConcurrentBag<SlotCache> dbTemp, string file)
    {
        if (SaveUtil.TryGetSaveFile(file, out var sav))
        {
            SlotInfoLoader.AddFromSaveFile(sav, dbTemp);
            return;
        }

        if (FileUtil.TryGetMemoryCard(file, out var mc))
            TryAddPKMsFromMemoryCard(dbTemp, mc, file);
        else
            Debug.WriteLine($"Unable to load SaveFile: {file}");
    }

    private static void TryAddPKMsFromMemoryCard(ConcurrentBag<SlotCache> dbTemp, SAV3GCMemoryCard mc, string file)
    {
        var state = mc.GetMemoryCardState();
        if (state == MemoryCardSaveStatus.Invalid)
            return;

        if (mc.HasCOLO)
            TryAdd(dbTemp, mc, file, SaveFileType.Colosseum);
        if (mc.HasXD)
            TryAdd(dbTemp, mc, file, SaveFileType.XD);
        if (mc.HasRSBOX)
            TryAdd(dbTemp, mc, file, SaveFileType.RSBox);

        static void TryAdd(ConcurrentBag<SlotCache> dbTemp, SAV3GCMemoryCard mc, string path, SaveFileType game)
        {
            mc.SelectSaveGame(game);
            if (!SaveUtil.TryGetSaveFile(mc, out var sav))
                return;
            sav.Metadata.SetExtraInfo(path);
            SlotInfoLoader.AddFromSaveFile(sav, dbTemp);
        }
    }

    // IO Usage
    private void OpenDB()
    {
        if (Directory.Exists(DatabasePath))
            Process.Start(new ProcessStartInfo(DatabasePath) { UseShellExecute = true });
    }

    private async Task Menu_Export_Click()
    {
        if (Results.Count == 0)
        {
            await AppDialogs.Alert(this, MsgDBCreateReportFail);
            return;
        }

        if (DialogResult.Yes != await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgDBExportResultsPrompt))
            return;

        var path = await FileDialogs.PickFolder(this);
        if (path is null)
            return;
        Directory.CreateDirectory(path);

        var data = new byte[SAV.SIZE_PARTY];
        foreach (var pk in Results.Select(z => z.Entity))
        {
            var fileName = Path.Combine(path, PathUtil.CleanFileName(pk.FileName));
            pk.ForcePartyData();
            pk.WriteDecryptedDataParty(data);
            File.WriteAllBytes(fileName, data);
        }
    }

    private async Task Menu_Import_Click()
    {
        var settings = await BoxView.GetBulkImportSettings();
        if (settings is not { } s)
            return;

        int box = BoxView.Box.CurrentBox;
        int ctr = SAV.LoadBoxes(Results.Select(z => z.Entity), out var result, box, s.ClearAll, s.Overwrite, s.Settings);
        if (ctr <= 0)
            return;

        BoxView.SetPKMBoxes();
        BoxView.UpdateBoxViewers();
        await AppDialogs.Alert(this, result);
    }

    // View Updates
    private IEnumerable<SlotCache> SearchDatabase()
    {
        var settings = GetSearchSettings();

        IEnumerable<SlotCache> res = RawDB;

        // pre-filter based on the file path (if specified)
        if (Menu_SearchBoxes.IsChecked != true)
            res = res.Where(z => z.SAV != SAV);
        if (Menu_SearchDatabase.IsChecked != true)
            res = res.Where(z => !IsIndividualFilePKMDB(z));
        if (Menu_SearchBackups.IsChecked != true)
            res = res.Where(z => !IsBackupSaveFile(z));

        // return filtered results
        return settings.Search(res);
    }

    private SearchSettings GetSearchSettings()
    {
        var settings = UC_EntitySearch.CreateSearchSettings(RTB_Instructions.Text ?? string.Empty);

        if (Menu_SearchLegal.IsChecked != Menu_SearchIllegal.IsChecked)
            settings.SearchLegal = Menu_SearchLegal.IsChecked == true;

        if (Menu_SearchClones.IsChecked == true)
        {
            settings.SearchClones = MainWindow.CurrentModifiers switch
            {
                KeyModifiers.Control => CloneDetectionMethod.HashPID,
                _ => CloneDetectionMethod.HashDetails,
            };
        }

        return settings;
    }

    private async Task B_Search_Click()
    {
        B_Search.IsEnabled = false;
        var search = SearchDatabase();

        bool legalSearch = Menu_SearchLegal.IsChecked != Menu_SearchIllegal.IsChecked;
        bool wordFilter = ParseSettings.Settings.WordFilter.CheckWordFilter;
        if (wordFilter && legalSearch && await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgDBSearchLegalityWordfilter) == DialogResult.No)
            ParseSettings.Settings.WordFilter.CheckWordFilter = false;
        var results = await Task.Run(search.ToList).ConfigureAwait(true);
        ParseSettings.Settings.WordFilter.CheckWordFilter = wordFilter;

        if (results.Count == 0)
        {
            if (Menu_SearchBoxes.IsChecked != true && Menu_SearchDatabase.IsChecked != true && Menu_SearchBackups.IsChecked != true)
                await AppDialogs.Alert(this, MsgDBSearchFail, MsgDBSearchNone);
            else
                await AppDialogs.Alert(this, MsgDBSearchNone);
        }
        SetResults(results); // updates Count Label as well.
        B_Search.IsEnabled = true;
    }

    private void SetResults(List<SlotCache> res)
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
        var entries = DatabasePokeGrid.Entries;
        if (Results.Count == 0)
        {
            foreach (var slot in entries)
            {
                slot.Sprite = null;
                slot.BackgroundBitmap = null;
            }
            return;
        }
        int begin = start * GridWidth;
        int end = Math.Min(entries.Count, Results.Count - begin);
        for (int i = 0; i < end; i++)
        {
            var slot = Results[i + begin];
            var pk = slot.Entity;
            entries[i].Sprite = pk.Sprite(SAV, visibility: SlotVisibilityType.CheckLegalityIndicate, storage: slot.Source.Type).ToAvaloniaBitmapAndDispose();
        }
        for (int i = end; i < entries.Count; i++)
            entries[i].Sprite = null;

        foreach (var slot in entries)
            slot.BackgroundBitmap = null;
        if (slotSelected != -1 && slotSelected >= begin && slotSelected < begin + entries.Count)
            entries[slotSelected - begin].BackgroundBitmap = SlotUtil.GetTouchTypeBackground(slotColor);
    }

    private async Task Menu_DeleteClones_Click()
    {
        var dr = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo,
            MsgDBDeleteCloneWarning + Environment.NewLine +
            MsgDBDeleteCloneAdvice, MsgContinue);

        if (dr != DialogResult.Yes)
            return;

        var deleted = 0;
        var db = RawDB.Where(IsIndividualFilePKMDB)
            .OrderByDescending(GetRevisedTime);

        const CloneDetectionMethod method = CloneDetectionMethod.HashDetails;
        var hasher = SearchUtil.GetCloneDetectMethod(method);
        var duplicates = SearchUtil.GetExtraClones(db, z => hasher(z.Entity));
        foreach (var entry in duplicates)
        {
            var src = entry.Source;
            if (src is not SlotInfoFileSingle(var path) || !File.Exists(path))
                continue;

            try { File.Delete(path); ++deleted; }
            catch (Exception ex) { await AppDialogs.Error(this, MsgDBDeleteCloneFail + Environment.NewLine + ex.Message + Environment.NewLine + path); }
        }

        var boxClear = new BoxManipClearDuplicate<string>(BoxManipType.DeleteClones, pk => SearchUtil.GetCloneDetectMethod(method)(pk));
        var param = new BoxManipParam(0, SAV.BoxCount - 1);
        int count = boxClear.Execute(SAV, param);
        deleted += count;

        if (deleted == 0)
        {
            await AppDialogs.Alert(this, MsgDBDeleteCloneNone);
            return;
        }

        await AppDialogs.Alert(this, string.Format(MsgFileDeleteCount, deleted), MsgWindowClose);
        BoxView.ReloadSlots();

        Close();
    }

    private static DateTime GetRevisedTime(SlotCache arg)
    {
        // This isn't displayed to the user, so just return the quickest -- Utc (not local time).
        var src = arg.Source;
        if (src is not SlotInfoFileSingle(var path))
            return DateTime.UtcNow;
        return File.GetLastWriteTimeUtc(path);
    }

    private bool IsBackupSaveFile(SlotCache pk) => pk.SAV is not FakeSaveFile && pk.SAV != SAV;
    private bool IsIndividualFilePKMDB(SlotCache pk) => pk.Source is SlotInfoFileSingle(var path) && path.StartsWith(DatabasePath + Path.DirectorySeparatorChar, StringComparison.Ordinal);

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
