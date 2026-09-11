using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Save file editor: box and party viewers with slot interactions.
/// </summary>
/// <remarks>Port of the WinForms <c>SAVEditor</c> (box/party/SAV tabs and <c>ContextMenuSAV</c>); the daycare group and most SAV sub-editors are not ported yet.</remarks>
public sealed partial class SAVEditorView : UserControl, ISaveHost, ISaveFileProvider
{
    public SaveDataEditor<SlotView> EditEnv = null!;

    public void SetEditEnvironment(SaveDataEditor<SlotView> value)
    {
        EditEnv = value;
        SAV = value.SAV;
        value.Slots.Publisher.Subscribe(SL_Party);
        value.Slots.Publisher.Subscribe(Box);
    }

    public SaveFile SAV { get; private set; } = FakeSaveFile.Default;
    public SlotPublisher<SlotView> Publisher => EditEnv.Slots.Publisher;
    public int CurrentBox => Box.CurrentBox;

    public bool HaX;
    public bool ModifyPKM { private get; set; }
    public MainWindow? Owner { get; set; }
    public bool IsControlHeld => Owner?.ModifierKeys.HasFlag(KeyModifiers.Control) == true;

    public MenuItem Menu_Redo { get; set; } = null!;
    public MenuItem Menu_Undo { get; set; } = null!;

    public Action<LegalityAnalysis>? RequestEditorLegality;

    public readonly TabControl tabBoxMulti = new() { Name = "tabBoxMulti" };
    public readonly TabItem Tab_Box = new() { Name = "Tab_Box", Header = "Box" };
    public readonly TabItem Tab_PartyBattle = new() { Name = "Tab_PartyBattle", Header = "Party" };
    public readonly BoxView Box = new() { Name = "Box" };
    public readonly PartyView SL_Party = new() { Name = "SL_Party" };

    /// <summary>Box manipulation menu (right click the Box tab header).</summary>
    public BoxManipMenu SortMenu { get; private set; } = null!;

    private readonly ContextMenu menu = new();
    private readonly MenuItem mnuView = new() { Name = "mnuView", Header = "_View" };
    private readonly MenuItem mnuSet = new() { Name = "mnuSet", Header = "_Set" };
    private readonly MenuItem mnuDelete = new() { Name = "mnuDelete", Header = "_Delete" };
    private readonly MenuItem mnuLegality = new() { Name = "mnuLegality", Header = "_Legality" };
    private SlotViewInfo<SlotView>? menuTarget;

    public bool FlagIllegal
    {
        get => Box.FlagIllegal;
        set
        {
            SL_Party.FlagIllegal = Box.FlagIllegal = value && !HaX;
            if (SAV is FakeSaveFile)
                return;
            ReloadSlots();
        }
    }

    public SAVEditorView()
    {
        Tab_Box.Content = Box;
        Tab_PartyBattle.Content = SL_Party;
        tabBoxMulti.Items.Add(Tab_Box);
        tabBoxMulti.Items.Add(Tab_PartyBattle);
        BuildSavTab();
        tabBoxMulti.Items.Add(Tab_SAV);
        Content = tabBoxMulti;

        Box.Host = this;
        SL_Party.Host = this;

        // Box manipulation menu: right click the Box tab header (WinForms: Tab_Box.ContextMenuStrip).
        SortMenu = new BoxManipMenu(this);
        Tab_Box.ContextMenu = SortMenu;
        Tab_Box.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(Tab_Box).Properties.IsRightButtonPressed)
                return;
            if (e.Source is Visual v && v.FindAncestorOfType<SlotView>() is not null)
                return; // slot clicks are handled by the slot itself
            if (e.ClickCount == 2)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    OpenBoxList();
                else
                    OpenBoxViewer();
                return;
            }
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                _ = SortMenu.Clear();
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                _ = SortMenu.Sort();
        }, RoutingStrategies.Tunnel);

        menu.Items.Add(mnuView);
        menu.Items.Add(mnuSet);
        menu.Items.Add(mnuDelete);
        menu.Items.Add(mnuLegality);
        mnuView.Click += async (_, _) => await ClickView();
        mnuSet.Click += async (_, _) => await ClickSet();
        mnuDelete.Click += async (_, _) => await ClickDelete();
        mnuLegality.Click += (_, _) => ClickShowLegality();
    }

    private BoxViewerWindow? Viewer;
    private BoxListWindow? ViewerList;

    /// <summary>
    /// Opens (or focuses) the second box viewer (WinForms: double click the Box tab).
    /// </summary>
    public void OpenBoxViewer()
    {
        if (Owner is null || !SAV.HasBox)
            return;
        if (Viewer is { } open)
        {
            open.Activate();
            return;
        }
        Viewer = new BoxViewerWindow(this, Box.CurrentBox);
        Viewer.Closed += (_, _) => Viewer = null;
        Viewer.Show(Owner);
    }

    /// <summary>
    /// Opens the all-boxes window (WinForms: Shift + double click the Box tab).
    /// </summary>
    public void OpenBoxList()
    {
        if (Owner is null || !SAV.HasBox)
            return;
        Viewer?.Close();
        if (ViewerList is { } open)
        {
            open.Activate();
            return;
        }
        ViewerList = new BoxListWindow(this);
        ViewerList.Closed += (_, _) => ViewerList = null;
        ViewerList.Show(Owner);
    }

    public void ReloadSlots()
    {
        UpdateBoxViewers(all: true);
        ResetNonBoxSlots();
    }

    public void UpdateBoxViewers(bool all = false)
    {
        if (!SAV.HasBox)
            return;
        Box.FlagIllegal = FlagIllegal;
        Box.ResetSlots();
        Box.ResetBoxNames();
        Viewer?.ReloadSlots();
        ViewerList?.ReloadSlots();
    }

    /// <summary>
    /// Swaps the box shown in a popout viewer with the main box view (WinForms <c>SwapBoxesViewer</c>).
    /// </summary>
    /// <returns>Box the popout should display.</returns>
    public int SwapBoxesViewer(int viewBox)
    {
        int mainBox = Box.CurrentBox;
        Box.CurrentBox = viewBox;
        return mainBox;
    }

    public void SetPKMBoxes()
    {
        if (SAV.HasBox)
            Box.ResetSlots();

        ResetNonBoxSlots();
    }

    private void ResetNonBoxSlots() => ResetParty();

    private void ResetParty()
    {
        if (SAV.HasParty)
            SL_Party.ResetSlots();
    }

    public void SetParty() => ResetParty();

    /// <summary>
    /// Reconfigures the editor for the current save file.
    /// </summary>
    /// <returns>True if the window needs to be re-translated.</returns>
    public bool ToggleInterface()
    {
        var sav = SAV;
        ToggleViewSubEditors(sav);
        SortMenu.ToggleVisibility();
        bool hasBox = sav.HasBox;
        Tab_Box.IsVisible = hasBox;
        if (hasBox)
        {
            Box.InitializeFromSAV(sav);
            Box.CanSetCurrentBox = true;
            Box.ControlsVisible = true;
            Box.ControlsEnabled = sav.BoxCount > 1;
        }

        bool hasParty = sav.HasParty && sav.State.Exportable;
        Tab_PartyBattle.IsVisible = hasParty;
        if (hasParty)
            SL_Party.InitializeFromSAV(sav);

        if (tabBoxMulti.SelectedItem is TabItem { IsVisible: false } || tabBoxMulti.SelectedItem is null)
            tabBoxMulti.SelectedItem = hasBox ? Tab_Box : hasParty ? Tab_PartyBattle : null;

        SetPKMBoxes(); // Reload all Entity picture boxes

        ToggleViewMisc(sav);
        return false;
    }

    public void ClickUndo()
    {
        EditEnv.Slots.Undo();
        UpdateUndoRedo();
    }

    public void ClickRedo()
    {
        EditEnv.Slots.Redo();
        UpdateUndoRedo();
    }

    public void UpdateUndoRedo()
    {
        Menu_Undo.IsEnabled = EditEnv.Slots.Changelog.CanUndo;
        Menu_Redo.IsEnabled = EditEnv.Slots.Changelog.CanRedo;
    }

    public async Task<bool> ExportSaveFile()
    {
        bool reload = SAV is IStorageCleanup b && b.FixStoragePreWrite();
        if (reload)
            ReloadSlots();
        bool forceSaveAs = MainWindow.Settings.Advanced.SaveExportForceSaveAs;
        return await FileDialogs.ExportSAVDialog(Owner!, SAV, SAV.CurrentBox, forceSaveAs);
    }

    public bool OpenPCBoxBin(ReadOnlySpan<byte> input, out string c)
    {
        if (SAV.GetPCBinary().Length == input.Length)
        {
            if (SAV.IsAnySlotLockedInBox(0, SAV.BoxCount - 1))
            { c = MsgSaveBoxImportPCFailBattle; return false; }
            if (!SAV.SetPCBinary(input))
            { c = string.Format(MsgSaveCurrentGeneration, SAV.Generation); return false; }

            c = MsgSaveBoxImportPCBinary;
        }
        else if (SAV.GetBoxBinary(Box.CurrentBox).Length == input.Length)
        {
            if (SAV.IsAnySlotLockedInBox(Box.CurrentBox, Box.CurrentBox))
            { c = MsgSaveBoxImportBoxFailBattle; return false; }
            if (!SAV.SetBoxBinary(input, Box.CurrentBox))
            { c = string.Format(MsgSaveCurrentGeneration, SAV.Generation); return false; }

            c = MsgSaveBoxImportBoxBinary;
        }
        else
        {
            c = string.Format(MsgSaveCurrentGeneration, SAV.Generation);
            return false;
        }
        SetPKMBoxes();
        UpdateBoxViewers();
        return true;
    }

    public async Task<(bool Result, string Message)> OpenGroup(IPokeGroup b)
    {
        var msg = string.Format(MsgSaveBoxImportGroup, Box.CurrentBoxName);
        var prompt = await AppDialogs.Prompt(Owner, MessageBoxButtons.YesNo, msg, MsgSaveBoxImportOverwrite);
        if (prompt != DialogResult.Yes)
            return (false, string.Empty);

        var settings = GetImportSettingsOverride();
        var slotSkipped = ImportGroup(b.Contents, SAV, Box.CurrentBox, settings);

        SetPKMBoxes();
        UpdateBoxViewers();

        var c = slotSkipped > 0 ? string.Format(MsgSaveBoxImportSkippedLocked, slotSkipped) : MsgSaveBoxImportGroupSuccess;
        return (true, c);
    }

    private static int ImportGroup(IEnumerable<PKM> data, SaveFile sav, int box, EntityImportSettings settings)
    {
        var type = sav.PKMType;
        int slotSkipped = 0;
        int index = 0;
        foreach (var x in data)
        {
            var i = index++;
            if (sav.IsBoxSlotOverwriteProtected(box, i))
            {
                slotSkipped++;
                continue;
            }

            var convert = EntityConverter.ConvertToType(x, type, out _);
            if (convert?.GetType() != type)
            {
                slotSkipped++;
                continue;
            }
            sav.SetBoxSlotAtIndex(x, box, i, settings);
        }

        return slotSkipped;
    }

    private EntityImportSettings GetImportSettingsOverride() => ModifyPKM ? default : EntityImportSettings.None;

    /// <summary>
    /// Loads all entity files in a folder into the boxes.
    /// </summary>
    public async Task<(bool Result, string Message)> LoadBoxes(string? path = null)
    {
        if (!SAV.HasBox)
            return (false, string.Empty);

        path ??= await GetFolderPath();
        if (path is null || !Directory.Exists(path))
            return (false, string.Empty);

        var bulk = await GetBulkImportSettings();
        if (bulk is not { } b)
            return (false, string.Empty);

        SAV.LoadBoxes(path, out var result, Box.CurrentBox, b.ClearAll, b.Overwrite, b.Settings);
        SetPKMBoxes();
        UpdateBoxViewers();
        return (true, result);
    }

    private async Task<string?> GetFolderPath()
    {
        var directory = await ClipboardService.GetText(Owner);
        if (directory is not null && Directory.Exists(directory))
        {
            // Ask user if they want to use clipboard directory before showing folder browser
            if (await AppDialogs.Prompt(Owner, MessageBoxButtons.YesNo, string.Format(MsgSaveBoxUseClipboard, directory)) == DialogResult.Yes)
                return directory;
        }
        return await FileDialogs.PickFolder(Owner!);
    }

    public async Task<(bool ClearAll, bool Overwrite, EntityImportSettings Settings)?> GetBulkImportSettings()
    {
        var dr = await AppDialogs.Prompt(Owner, MessageBoxButtons.YesNoCancel, MsgSaveBoxImportClear, MsgSaveBoxImportClearNo);
        if (dr == DialogResult.Cancel)
            return null;

        bool clearAll = dr == DialogResult.Yes;
        var settings = GetImportSettingsOverride();
        return (clearAll, false, settings);
    }

    public Task ClickShowdownExportParty() => ExportShowdownText(SAV, MsgSimulatorExportParty, sav => sav.PartyData);

    public Task ClickShowdownExportCurrentBox()
    {
        if (!SAV.HasBox)
            return Task.CompletedTask;
        return ExportShowdownText(SAV, MsgSimulatorExportList,
            sav => IsControlHeld ? sav.BoxData : sav.GetBoxData(CurrentBox));
    }

    private async Task ExportShowdownText(SaveFile sav, string success, Func<SaveFile, IEnumerable<PKM>> fetch)
    {
        var list = fetch(sav);
        var programLanguage = Language.GetLanguageValue(MainWindow.Settings.Startup.Language);
        var settings = MainWindow.Settings.BattleTemplate.Export.GetSettings(programLanguage, sav.Context);
        var result = ShowdownParsing.GetShowdownSets(list, Environment.NewLine + Environment.NewLine, settings);
        if (string.IsNullOrWhiteSpace(result))
            return;
        if (await ClipboardService.SetText(Owner, result))
            await AppDialogs.Alert(Owner, success);
    }

    public async Task SetClonesToBox(PKM pk)
    {
        if (await AppDialogs.Prompt(Owner, MessageBoxButtons.YesNo, string.Format(MsgSaveBoxCloneFromTabs, Box.CurrentBoxName)) != DialogResult.Yes)
            return;

        int slotSkipped = SetClonesToCurrentBox(pk, Box.CurrentBox);
        if (slotSkipped > 0)
            await AppDialogs.Alert(Owner, string.Format(MsgSaveBoxImportSkippedLocked, slotSkipped));

        UpdateBoxViewers();
    }

    private int SetClonesToCurrentBox(PKM pk, int box)
    {
        var arr = new PKM[SAV.BoxSlotCount];
        for (int i = 0; i < SAV.BoxSlotCount; i++) // set to every slot in box
            arr[i] = pk;

        int slotSkipped = SAV.SetBoxData(arr, box);
        Box.ResetSlots();
        return slotSkipped;
    }

    // Slot interaction (port of ContextMenuSAV / SlotChangeManager click handling)

    public void SlotPointerPressed(ISlotViewer<SlotView> viewer, SlotView view, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(view).Properties;
        var info = new SlotViewInfo<SlotView>(viewer.GetSlotData(view), viewer);
        view.Focus();
        if (props.IsRightButtonPressed)
        {
            e.Handled = true;
            OpenContextMenu(info, view, e.KeyModifiers);
            return;
        }
        if (!props.IsLeftButtonPressed)
            return;

        // A plain left press may become a drag; modifier clicks keep the view/set/delete shortcuts.
        if (e.KeyModifiers == KeyModifiers.None)
            SlotPointerPressedForDrag(viewer, view, e);

        e.Handled = true;
        _ = OmniClick(info, e.KeyModifiers);
    }

    /// <summary>Pointer moved over a slot; starts a drag once the threshold is passed.</summary>
    public void SlotPointerMoved(ISlotViewer<SlotView> viewer, SlotView view, PointerEventArgs e) => SlotPointerMoved(view, e);

    /// <summary>An entity (or file) is being dragged over a slot.</summary>
    public void SlotDragOver(SlotView view, DragEventArgs e) => SlotDragOverCore(view, e);

    /// <summary>An entity (or file) was dropped on a slot.</summary>
    public void SlotDrop(SlotView view, DragEventArgs e) => _ = SlotDropCore(view, e);

    private readonly Hover.SummaryPreviewer HoverPreview = new();

    public void SlotPointerEntered(ISlotViewer<SlotView> viewer, SlotView view)
    {
        view.Cursor = new Cursor(StandardCursorType.Hand);
        try
        {
            var info = viewer.GetSlotData(view);
            HoverPreview.Show(view, info.Read(viewer.SAV), info.Type);
        }
        catch (Exception ex)
        {
            // A hover must never take the editor down; the slot keeps whatever tooltip it already had.
            System.Diagnostics.Debug.WriteLine($"Hover preview failed: {ex.Message}");
        }
    }

    public void SlotPointerExited(ISlotViewer<SlotView> viewer, SlotView view)
    {
        view.Cursor = Cursor.Default;
        HoverPreview.Clear();
    }

    private async Task OmniClick(SlotViewInfo<SlotView> info, KeyModifiers z)
    {
        switch (z)
        {
            case KeyModifiers.Control: await ClickView(info); break;
            case KeyModifiers.Shift: await ClickSet(info); break;
            case KeyModifiers.Alt: await ClickDelete(info); break;
        }
    }

    private void OpenContextMenu(SlotViewInfo<SlotView> info, SlotView view, KeyModifiers modifiers)
    {
        menuTarget = info;
        bool canView = !info.IsEmpty() || HaX;
        bool canSet = info.CanWriteTo();
        bool canDelete = canSet && canView;
        bool canLegality = (modifiers == KeyModifiers.Control || MainWindow.Settings.Display.SlotLegalityAlwaysVisible) && canView && RequestEditorLegality is not null;

        mnuView.IsVisible = canView;
        mnuSet.IsVisible = canSet;
        mnuDelete.IsVisible = canDelete;
        mnuLegality.IsVisible = canLegality;

        if (!canView && !canSet && !canDelete)
            return;
        menu.Open(view);
    }

    private Task ClickView() => menuTarget is { } t ? ClickView(t) : Task.CompletedTask;
    private Task ClickSet() => menuTarget is { } t ? ClickSet(t) : Task.CompletedTask;
    private Task ClickDelete() => menuTarget is { } t ? ClickDelete(t) : Task.CompletedTask;

    private Task ClickView(SlotViewInfo<SlotView> info)
    {
        if (info.IsEmpty())
            return Task.CompletedTask;

        var pk = EditEnv.Slots.Get(info.Slot);
        EditEnv.PKMEditor.PopulateFields(pk, false, true);
        return Task.CompletedTask;
    }

    private async Task ClickSet(SlotViewInfo<SlotView> info)
    {
        var editor = EditEnv.PKMEditor;
        if (!editor.EditsComplete)
            return;
        PKM pk = editor.PreparePKM();
        var preModify = pk.Clone();

        var sav = info.View.SAV;

        if (!await CheckDest(info, sav, pk))
            return;

        var errata = sav.EvaluateCompatibility(pk);
        if (errata.Count != 0)
        {
            var msg = string.Join(Environment.NewLine, errata);
            var prompt = await AppDialogs.Prompt(Owner, MessageBoxButtons.YesNo, msg, MsgContinue);
            if (prompt != DialogResult.Yes)
                return;
        }

        editor.NotifyWasExported(preModify);
        EditEnv.Slots.Set(info.Slot, pk);
        UpdateUndoRedo();
    }

    private async Task ClickDelete(SlotViewInfo<SlotView> info)
    {
        if (info.IsEmpty())
            return;

        var sav = info.View.SAV;
        var pk = sav.BlankPKM;
        if (!await CheckDest(info, sav, pk))
            return;

        EditEnv.Slots.Delete(info.Slot);
        UpdateUndoRedo();
    }

    private async Task<bool> CheckDest(SlotViewInfo<SlotView> info, SaveFile sav, PKM pk)
    {
        var msg = info.Slot.CanWriteTo(sav, pk);
        if (msg == WriteBlockedMessage.None)
            return true;

        switch (msg)
        {
            case WriteBlockedMessage.InvalidPartyConfiguration:
                await AppDialogs.Alert(Owner, MsgSaveSlotEmpty);
                break;
            case WriteBlockedMessage.IncompatibleFormat:
                break;
            case WriteBlockedMessage.InvalidDestination:
                await AppDialogs.Alert(Owner, MsgSaveSlotLocked);
                break;
            default:
                throw new IndexOutOfRangeException(nameof(msg));
        }
        return false;
    }

    private void ClickShowLegality()
    {
        if (menuTarget is not { } info)
            return;
        var sav = info.View.SAV;
        var pk = info.Slot.Read(sav);
        var type = info.Slot.Type;
        var la = new LegalityAnalysis(pk, sav.Personal, type);
        RequestEditorLegality?.Invoke(la);
    }
}
