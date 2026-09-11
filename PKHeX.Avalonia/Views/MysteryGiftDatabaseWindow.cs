using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
/// Mystery gift database browser (port of the WinForms <c>SAV_MysteryGiftDB</c>).
/// </summary>
public sealed class MysteryGiftDatabaseWindow : Window
{
    private const int GridWidth = 6;
    private const int GridHeight = 11;
    private const int MAXFORMAT = Latest.Generation;

    private readonly PKMEditorView PKME_Tabs;
    private readonly SaveFile SAV;
    private readonly SAVEditorView BoxView;
    private readonly EntityInstructionBuilderView UC_Builder;
    private readonly string DatabasePath = MainWindow.MGDatabasePath;

    private List<MysteryGift> Results = [];
    private List<MysteryGift> RawDB = [];
    private int slotSelected = -1;
    private SlotTouchType slotColor = SlotTouchType.None;
    private readonly string Counter;
    private readonly string Viewed;

    private readonly PokeGrid MysteryPokeGrid = new() { Name = "MysteryPokeGrid" };
    private readonly ScrollBar SCR_Box = new() { Orientation = Orientation.Vertical, Minimum = 0, Maximum = 0, Width = 18, Visibility = ScrollBarVisibility.Visible };
    private readonly TabControl TC_SearchSettings = new() { Name = "TC_SearchSettings" };
    private readonly TabItem Tab_General = new() { Name = "Tab_General", Header = "General" };
    private readonly TabItem Tab_Advanced = new() { Name = "Tab_Advanced", Header = "Advanced" };
    private readonly TabItem Tab_Settings = new() { Name = "Tab_Settings", Header = "Settings" };
    private readonly PropertyGridView PG_Settings = new();
    private readonly TextBox RTB_Instructions = new() { Name = "RTB_Instructions", AcceptsReturn = true, MinHeight = 120, FontFamily = new global::Avalonia.Media.FontFamily("monospace") };

    private readonly TextBlock Label_Species = UiFactory.Label("Label_Species", "Species:");
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 170);
    private readonly TextBlock Label_HeldItem = UiFactory.Label("Label_HeldItem", "Held Item:");
    private readonly ComboBox CB_HeldItem = UiFactory.Combo("CB_HeldItem", 170);
    private readonly TextBlock L_Move1 = UiFactory.Label("L_Move1", "Move 1:");
    private readonly ComboBox CB_Move1 = UiFactory.Combo("CB_Move1", 170);
    private readonly TextBlock L_Move2 = UiFactory.Label("L_Move2", "Move 2:");
    private readonly ComboBox CB_Move2 = UiFactory.Combo("CB_Move2", 170);
    private readonly TextBlock L_Move3 = UiFactory.Label("L_Move3", "Move 3:");
    private readonly ComboBox CB_Move3 = UiFactory.Combo("CB_Move3", 170);
    private readonly TextBlock L_Move4 = UiFactory.Label("L_Move4", "Move 4:");
    private readonly ComboBox CB_Move4 = UiFactory.Combo("CB_Move4", 170);
    private readonly TextBlock L_Format = UiFactory.Label("L_Format", "Format:");
    private readonly ComboBox CB_FormatComparator = UiFactory.StringCombo("CB_FormatComparator", 70, "Any", "==", ">=", "<=");
    private readonly ComboBox CB_Format = UiFactory.StringCombo("CB_Format", 130, "Any", ".wc9", ".wc8", ".wc7", ".wc6", ".pgf", ".pcd/pgt/.wc4");
    private readonly CheckBox CHK_Shiny = UiFactory.Check("CHK_Shiny", "Shiny");
    private readonly CheckBox CHK_IsEgg = UiFactory.Check("CHK_IsEgg", "Egg");
    private readonly Button B_Search = UiFactory.Button("B_Search", "Search!");
    private readonly Button B_Reset = UiFactory.Button("B_Reset", "Reset Filters");
    private readonly Button B_Add = UiFactory.Button("B_Add", "Add");
    private readonly TextBlock L_Count = UiFactory.Label("L_Count", "Count: {0}");
    private readonly TextBlock L_Viewed = UiFactory.Label("L_Viewed", "Last Viewed: {0}");

    private readonly MenuItem Menu_OpenDB = new() { Name = "Menu_OpenDB", Header = "Open Database Folder" };
    private readonly MenuItem Menu_Export = new() { Name = "Menu_Export", Header = "Export Results to Folder" };
    private readonly MenuItem Menu_Import = new() { Name = "Menu_Import", Header = "Import Results to SaveFile" };
    private readonly MenuItem Menu_Exit = new() { Name = "Menu_Exit", Header = "_Close" };
    private readonly ContextMenu SlotMenu = new();
    private SlotView? menuSlot;

    public MysteryGiftDatabaseWindow(PKMEditorView tabs, SAVEditorView sav)
    {
        Name = "SAV_MysteryGiftDB";
        Title = "Mystery Gift Database";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 1030;
        Height = 690;

        SAV = sav.SAV;
        BoxView = sav;
        PKME_Tabs = tabs;
        UC_Builder = new EntityInstructionBuilderView(() => tabs.PreparePKM()) { ReadOnly = true };
        CHK_Shiny.IsThreeState = CHK_IsEgg.IsThreeState = true;

        var menuFile = new MenuItem { Name = "Menu_Close", Header = "File" };
        menuFile.Items.Add(Menu_Exit);
        var menuTools = new MenuItem { Name = "Menu_Tools", Header = "Tools" };
        foreach (var item in new[] { Menu_OpenDB, Menu_Export, Menu_Import })
            menuTools.Items.Add(item);
        var menu = new Menu();
        menu.Items.Add(menuFile);
        menu.Items.Add(menuTools);

        var filters = UiFactory.FormGrid(7);
        UiFactory.AddFormRow(filters, 0, L_Format, UiFactory.Row(CB_FormatComparator, CB_Format));
        UiFactory.AddFormRow(filters, 1, Label_Species, CB_Species);
        UiFactory.AddFormRow(filters, 2, Label_HeldItem, CB_HeldItem);
        UiFactory.AddFormRow(filters, 3, L_Move1, CB_Move1);
        UiFactory.AddFormRow(filters, 4, L_Move2, CB_Move2);
        UiFactory.AddFormRow(filters, 5, L_Move3, CB_Move3);
        UiFactory.AddFormRow(filters, 6, L_Move4, CB_Move4);
        Tab_General.Content = UiFactory.Column(filters, UiFactory.Row(CHK_Shiny, CHK_IsEgg));
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
        gridPanel.Children.Add(MysteryPokeGrid);

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
        PG_Settings.SetObject(MainWindow.Settings.MysteryDb);

        MysteryPokeGrid.InitializeGrid(GridWidth, GridHeight, SpriteUtil.Spriter);
        MysteryPokeGrid.SetBackground(AppResources.GetSkBitmap("box_wp_clean") ?? SpriteUtil.Spriter.Transparent);
        foreach (var slot in MysteryPokeGrid.Entries)
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
        BuildSlotMenu();

        MysteryPokeGrid.PointerWheelChanged += (_, e) =>
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

        // Preset Filters to only show gifts available for loaded save
        CB_FormatComparator.SelectionChanged += (_, _) => ChangeFormatFilter();
        CB_FormatComparator.SelectedIndex = 3; // <= (fires the handler, which picks the save's format)

        Counter = L_Count.Text ?? "Count: {0}";
        Viewed = L_Viewed.Text ?? "Last Viewed: {0}";
        L_Viewed.Text = string.Empty; // invisible for now

        B_Search.Click += async (_, _) => await B_Search_Click();
        B_Reset.Click += (_, _) => ResetFilters();
        B_Add.Click += async (_, _) => await B_Add_Click();
        Menu_Exit.Click += (_, _) => Close();
        Menu_OpenDB.Click += (_, _) => OpenDB();
        Menu_Export.Click += async (_, _) => await Menu_Export_Click();
        Menu_Import.Click += async (_, _) => await Menu_Import_Click();

        // Load Data
        B_Search.IsEnabled = false;
        L_Count.Text = "Loading...";
        _ = Task.Run(LoadDatabase);
    }

    private void BuildSlotMenu()
    {
        var mnuView = new MenuItem { Name = "mnuView", Header = "View" };
        var mnuSaveMG = new MenuItem { Name = "mnuSaveMG", Header = "Save Gift" };
        var mnuSavePK = new MenuItem { Name = "mnuSavePK", Header = "Save PKM" };
        mnuView.Click += async (_, _) => await ClickView(menuSlot);
        mnuSaveMG.Click += async (_, _) => await ClickSaveMG(menuSlot);
        mnuSavePK.Click += async (_, _) => await ClickSavePK(menuSlot);
        SlotMenu.Items.Add(mnuView);
        SlotMenu.Items.Add(mnuSaveMG);
        SlotMenu.Items.Add(mnuSavePK);
        Translator.TranslateControls(SlotMenu, "SAV_MysteryGiftDB", MainWindow.CurrentLanguage);
    }

    private int GetSenderIndex(SlotView? pb)
    {
        if (pb is null)
            return -1;
        int index = MysteryPokeGrid.Entries.IndexOf(pb);
        if (index < 0 || index >= MysteryPokeGrid.Entries.Count)
            return -1;
        index += (int)SCR_Box.Value * GridWidth;
        if (index >= Results.Count)
            return -1;
        return index;
    }

    private async Task ClickView(SlotView? pb)
    {
        int index = GetSenderIndex(pb);
        if (index < 0)
            return;
        var temp = Results[index].ConvertToPKM(SAV, EncounterCriteria.Unrestricted);
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
        UpdateSlotColor((int)SCR_Box.Value);
        L_Viewed.Text = string.Format(Viewed, Results[index].FileName);
    }

    private async Task ClickSavePK(SlotView? pb)
    {
        int index = GetSenderIndex(pb);
        if (index < 0)
            return;
        var gift = Results[index];
        var pk = gift.ConvertToPKM(SAV);
        await FileDialogs.SavePKMDialog(this, pk);
    }

    private async Task ClickSaveMG(SlotView? pb)
    {
        int index = GetSenderIndex(pb);
        if (index < 0)
            return;
        var gift = Results[index];
        if (gift is not DataMysteryGift g) // e.g. WC3
        {
            await AppDialogs.Alert(this, MsgExportWC3DataFail);
            return;
        }
        await FileDialogs.ExportMGDialog(this, g);
    }

    private void PopulateComboBoxes()
    {
        var comboAny = new ComboItem(MsgAny, -1);

        var source = GameInfo.FilteredSources;
        var species = new List<ComboItem>(source.Species);
        species.RemoveAll(z => RawDB.All(mg => mg.Species != z.Value));
        species.Insert(0, comboAny);
        CB_Species.SetItems(species);

        var items = new List<ComboItem>(source.Items);
        items.Insert(0, comboAny);
        CB_HeldItem.SetItems(items);

        // Set the Move ComboBoxes too.
        var moves = new List<ComboItem>(source.Moves);
        moves.RemoveAt(0);
        moves.Insert(0, comboAny);
        foreach (var cb in new[] { CB_Move1, CB_Move2, CB_Move3, CB_Move4 })
            cb.SetItems(moves);

        ResetFilters();

        // Trigger a Reset
        B_Search.IsEnabled = true;
    }

    private void ResetFilters()
    {
        CHK_Shiny.IsChecked = CHK_IsEgg.IsChecked = null; // indeterminate
        CB_HeldItem.SelectedIndex = 0;
        CB_Species.SelectedIndex = 0;

        CB_Move1.SelectedIndex = CB_Move2.SelectedIndex = CB_Move3.SelectedIndex = CB_Move4.SelectedIndex = 0;
        RTB_Instructions.Text = string.Empty;
    }

    private void LoadDatabase()
    {
        var db = EncounterEvent.GetAllEvents();

        if (MainWindow.Settings.MysteryDb.FilterUnavailableSpecies)
        {
            var filter = EntityPresenceFilters.GetFilterGift<MysteryGift>(SAV.Context, SAV.Generation);
            if (filter != null)
                db = db.Where(filter);
        }

        RawDB = [.. db];
        foreach (var mg in RawDB)
            mg.GiftUsed = false;

        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            SetResults(RawDB);
            PopulateComboBoxes();
        });
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

        var folder = await FileDialogs.PickFolder(this);
        if (folder is null)
            return;
        Directory.CreateDirectory(folder);

        foreach (var gift in Results.OfType<DataMysteryGift>()) // WC3 have no data
        {
            var fileName = PathUtil.CleanFileName(gift.FileName);
            var path = Path.Combine(folder, fileName);
            var data = gift.Write();
            File.WriteAllBytes(path, data);
        }
    }

    private async Task Menu_Import_Click()
    {
        var settings = await BoxView.GetBulkImportSettings();
        if (settings is not { } s)
            return;

        int box = BoxView.Box.CurrentBox;
        int ctr = SAV.LoadBoxes(Results, out var result, box, s.ClearAll, s.Overwrite, s.Settings);
        if (ctr <= 0)
            return;

        BoxView.SetPKMBoxes();
        BoxView.UpdateBoxViewers();
        await AppDialogs.Alert(this, result);
    }

    // View Updates
    private async Task B_Search_Click()
    {
        // Populate Search Query Result
        IEnumerable<MysteryGift> res = RawDB;

        byte format = (byte)(MAXFORMAT + 1 - CB_Format.SelectedIndex);

        switch (CB_FormatComparator.SelectedIndex)
        {
            case 0: /* Do nothing */ break;
            case 1: res = res.Where(mg => mg.Generation >= format); break;
            case 2: res = res.Where(mg => mg.Generation == format); break;
            case 3: res = res.Where(mg => mg.Generation <= format); break;
        }

        // Primary Searchables
        var species = CB_Species.GetValue();
        int item = CB_HeldItem.GetValue();
        if (species != -1) res = res.Where(pk => pk.Species == species);
        if (item != -1) res = res.Where(pk => pk.HeldItem == item);

        // Secondary Searchables
        int move1 = CB_Move1.GetValue();
        int move2 = CB_Move2.GetValue();
        int move3 = CB_Move3.GetValue();
        int move4 = CB_Move4.GetValue();
        if (move1 != -1) res = res.Where(mg => mg.HasMove((ushort)move1));
        if (move2 != -1) res = res.Where(mg => mg.HasMove((ushort)move2));
        if (move3 != -1) res = res.Where(mg => mg.HasMove((ushort)move3));
        if (move4 != -1) res = res.Where(mg => mg.HasMove((ushort)move4));

        if (CHK_Shiny.IsChecked is { } shiny)
            res = shiny ? res.Where(pk => pk.IsShiny) : res.Where(pk => !pk.IsShiny);

        if (CHK_IsEgg.IsChecked is { } egg)
            res = egg ? res.Where(pk => pk.IsEgg) : res.Where(pk => !pk.IsEgg);

        slotSelected = -1; // reset the slot last viewed

        ReadOnlySpan<char> batchText = RTB_Instructions.Text ?? string.Empty;
        if (batchText.Length != 0 && !StringInstructionSet.HasEmptyLine(batchText))
        {
            var filters = StringInstruction.GetFilters(batchText);
            EntityBatchEditor.ScreenStrings(filters);
            res = res.Where(pk => BatchEditingUtil.IsFilterMatch(filters, pk)); // Compare across all filters
        }

        var results = res.ToArray();
        if (results.Length == 0)
            await AppDialogs.Alert(this, MsgDBSearchNone);

        SetResults([.. results]); // updates Count Label as well.
    }

    private void SetResults(List<MysteryGift> res)
    {
        Results = [.. res];

        SCR_Box.Maximum = (int)Math.Ceiling((decimal)Results.Count / GridWidth);
        if (SCR_Box.Maximum > 0)
            SCR_Box.Maximum--;

        SCR_Box.Value = 0;
        FillPKXBoxes(0);

        L_Count.Text = string.Format(Counter, Results.Count);
    }

    private void FillPKXBoxes(int start)
    {
        var entries = MysteryPokeGrid.Entries;
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
            entries[i].Sprite = Results[i + begin].Sprite().ToAvaloniaBitmapAndDispose();
        }
        for (int i = end; i < entries.Count; i++)
            entries[i].Sprite = null;
        UpdateSlotColor(start);
    }

    private void UpdateSlotColor(int start)
    {
        var entries = MysteryPokeGrid.Entries;
        foreach (var slot in entries)
            slot.BackgroundBitmap = null;
        int begin = GridWidth * start;
        if (slotSelected != -1 && slotSelected >= begin && slotSelected < begin + entries.Count)
            entries[slotSelected - begin].BackgroundBitmap = SlotUtil.GetTouchTypeBackground(slotColor);
    }

    private void ChangeFormatFilter()
    {
        if (CB_FormatComparator.SelectedIndex == 0)
        {
            CB_Format.IsVisible = false; // !any
            CB_Format.SelectedIndex = 0;
        }
        else
        {
            CB_Format.IsVisible = true;
            int index = MAXFORMAT - SAV.Generation + 1;
            CB_Format.SelectedIndex = index < CB_Format.Items.Count ? index : 0; // SAV generation (offset by 1 for "Any")
        }
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
