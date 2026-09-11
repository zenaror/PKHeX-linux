using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Batch editor (port of the WinForms <c>BatchEditor</c>).
/// </summary>
public sealed class BatchEditorWindow : Window
{
    private readonly SaveFile _sav;
    private readonly SlotChangelog _changelog;

    // Cached source data. The cache is intentionally mutable; batch edits are accumulated here until the user chooses Save.
    private IReadOnlyList<SlotCache>? _boxData;
    private IReadOnlyList<SlotCache>? _party;
    private IReadOnlyList<SlotCache>? _folder;
    private readonly Dictionary<ISlotInfo, string> _folderPaths = new();
    private readonly HashSet<ISlotInfo> _modifiedSlots = [];
    private readonly string _matchingCountFormat;

    private EntityBatchProcessor _editor = new();
    private readonly EntityInstructionBuilderView _builder;

    /// <summary>
    /// Remember the last used commands so that they can be restored when the form is reopened.
    /// </summary>
    private static string _lastUsedCommands = string.Empty;

    /// <summary>True if the user chose to save the changes.</summary>
    public bool Accepted { get; private set; }

    private readonly RadioButton RB_Boxes = new() { Name = "RB_Boxes", Content = "Boxes", IsChecked = true, GroupName = "src", MinHeight = 0 };
    private readonly RadioButton RB_Party = new() { Name = "RB_Party", Content = "Party", GroupName = "src", MinHeight = 0 };
    private readonly RadioButton RB_Path = new() { Name = "RB_Path", Content = "Folder...", GroupName = "src", MinHeight = 0 };
    private readonly TextBox TB_Folder = new() { Name = "TB_Folder", IsVisible = false, IsReadOnly = true, MinWidth = 260, MinHeight = 0, Padding = new Thickness(4, 2) };
    private readonly TextBox RTB_Instructions = new() { Name = "RTB_Instructions", AcceptsReturn = true, TextWrapping = global::Avalonia.Media.TextWrapping.NoWrap, MinHeight = 200, FontFamily = new global::Avalonia.Media.FontFamily("monospace") };
    private readonly TextBlock L_Count = UiFactory.Label("L_Count", "Matching: {0} / {1}");
    private readonly Button B_Add = UiFactory.Button("B_Add", "Add");
    private readonly Button B_Run = UiFactory.Button("B_Run", "Run");
    private readonly Button B_Save = UiFactory.Button("B_Save", "Save");
    private readonly Button B_Reset = UiFactory.Button("B_Reset", "Reset");
    private readonly Button B_Cancel = UiFactory.Button("B_Cancel", "Cancel");

    public BatchEditorWindow(PKM pk, SaveFile sav, SlotChangelog changelog)
    {
        Name = "BatchEditor";
        Title = "Batch Editor";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 800;
        Height = 520;

        _sav = sav;
        _changelog = changelog;

        // Builder needs to be late-bound to the input PKM from the main form.
        _builder = new EntityInstructionBuilderView(() => pk);

        var source = UiFactory.Row(RB_Boxes, RB_Party, RB_Path, TB_Folder);
        var bottom = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
        var builderRow = new DockPanel();
        DockPanel.SetDock(B_Add, Dock.Right);
        builderRow.Children.Add(B_Add);
        builderRow.Children.Add(_builder);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var b in new[] { B_Run, B_Reset, B_Cancel, B_Save })
        {
            b.MinWidth = 80;
            b.Padding = new Thickness(8, 4);
            buttons.Children.Add(b);
        }
        var footer = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Right);
        footer.Children.Add(buttons);
        footer.Children.Add(L_Count);
        bottom.Children.Add(builderRow);
        bottom.Children.Add(footer);

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(source, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(source);
        root.Children.Add(bottom);
        root.Children.Add(RTB_Instructions);
        Content = root;

        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
        _matchingCountFormat = L_Count.Text ?? "Matching: {0} / {1}"; // cache the translated string

        B_Add.Click += async (_, _) => await B_Add_Click();
        B_Run.Click += async (_, _) => await B_Run_Click();
        B_Save.Click += (_, _) => B_Save_Click();
        B_Reset.Click += (_, _) => B_Reset_Click();
        B_Cancel.Click += (_, _) => Close();
        RB_Path.Click += async (_, _) => await B_Open_Click();
        RB_Boxes.IsCheckedChanged += (_, _) => SourceChanged();
        RB_Party.IsCheckedChanged += (_, _) => SourceChanged();
        RTB_Instructions.OnTextChanged(_ => UpdateFilterCountDebounced());

        // Boxes are the default source and are immediately available for filter analysis.
        _boxData = CreateBoxData();
        UpdateFilterCountDebounced();
        UpdateButtons();

        RTB_Instructions.Text = _lastUsedCommands;
        Closing += (_, _) => _lastUsedCommands = RTB_Instructions.Text ?? string.Empty;
    }

    public IReadOnlyList<ISlotInfo> GetModifiedSlots() => [.. _modifiedSlots];

    private IReadOnlyList<SlotCache> CreateBoxData()
    {
        var data = new List<SlotCache>(_sav.SlotCount);
        SlotInfoLoader.AddBoxData(_sav, data);
        return data;
    }

    private IReadOnlyList<SlotCache> CreatePartyData()
    {
        var data = new List<SlotCache>(_sav.PartyCount);
        SlotInfoLoader.AddPartyData(_sav, data);
        return data;
    }

    private IReadOnlyList<SlotCache> CreateFolderData()
    {
        var path = TB_Folder.Text ?? string.Empty;
        if (!Directory.Exists(path))
            return [];

        var result = new List<SlotCache>();
        IEnumerable<string> files;
        try
        {
            files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        }
        catch (IOException)
        {
            return result;
        }
        catch (UnauthorizedAccessException)
        {
            return result;
        }

        foreach (var source in files)
        {
            var fi = new FileInfo(source);
            if (!EntityDetection.IsSizePlausible(fi.Length))
                continue;

            try
            {
                var data = File.ReadAllBytes(source);
                if (FileUtil.TryGetPKM(data, out var pk, fi.Extension, _sav))
                {
                    var info = new SlotInfoFileSingle(source);
                    result.Add(new SlotCache(info, pk));
                    _folderPaths[info] = source;
                }
            }
            catch (IOException)
            {
                // A file that cannot be read is simply not a processable source entity.
            }
            catch (UnauthorizedAccessException)
            {
                // A file that cannot be read is simply not a processable source entity.
            }
        }

        return result;
    }

    private IReadOnlyList<SlotCache> GetCurrentData()
    {
        if (RB_Party.IsChecked == true)
            return _party ??= CreatePartyData();

        if (RB_Path.IsChecked == true)
            return _folder ??= CreateFolderData();

        return _boxData ??= CreateBoxData();
    }

    private void SourceChanged()
    {
        if (RB_Path.IsChecked == true)
            return;
        TB_Folder.IsVisible = false;
        UpdateFilterCountDebounced();
        UpdateButtons();
    }

    private async Task B_Open_Click()
    {
        var folder = await FileDialogs.PickFolder(this);
        if (folder is null)
        {
            RB_Boxes.IsChecked = true;
            return;
        }

        TB_Folder.Text = folder;
        TB_Folder.IsVisible = true;
        RB_Path.IsChecked = true;
        _folder = null;
        _folderPaths.Clear();
        UpdateFilterCountDebounced();
        UpdateButtons();
    }

    private void B_Reset_Click()
    {
        // Reset only discards the in-memory save-file work. Folder operations have already
        // been written to disk and intentionally cannot be reverted by this form.
        _modifiedSlots.Clear();
        _boxData = null;
        _party = null;
        _folder = null;
        _folderPaths.Clear();
        _editor = new EntityBatchProcessor();

        RB_Boxes.IsChecked = true;
        TB_Folder.Text = string.Empty;
        TB_Folder.IsVisible = false;

        _boxData = CreateBoxData();
        UpdateFilterCountDebounced();
        UpdateButtons();
    }

    private async Task B_Run_Click()
    {
        var text = RTB_Instructions.Text ?? string.Empty;
        var sets = await TryGetInstructionSets(text, promptForEmptyValues: true, showErrors: true);
        if (sets is null)
            return;

        foreach (var set in sets)
        {
            EntityBatchEditor.ScreenStrings(set.Filters);
            EntityBatchEditor.ScreenStrings(set.Instructions);
        }

        if (RB_Path.IsChecked == true)
        {
            await RunBatchEditFolder(sets);
            return;
        }

        await RunBatchEditSaveFile(sets);
    }

    private void B_Save_Click()
    {
        Accepted = true;
        if (_modifiedSlots.Count == 0)
        {
            Close();
            return;
        }

        // Flush all modified savedata slots back to the save.
        var slots = GetChangelogSlots();
        using var change = _changelog.Begin(slots);
        var settings = default(EntityImportSettings) with { UpdateRecord = EntityImportOption.Disable };
        foreach (var slot in slots)
        {
            if (TryGetCachedSlot(slot, out var cache))
                slot.WriteTo(_sav, cache.Entity, settings);
        }

        change.Commit();
        Close();
    }

    private IReadOnlyList<ISlotInfo> GetChangelogSlots() => [.. _modifiedSlots];

    private bool TryGetCachedSlot(ISlotInfo source, [NotNullWhen(true)] out SlotCache? cache)
    {
        cache = _boxData?.FirstOrDefault(z => ReferenceEquals(z.Source, source))
               ?? _party?.FirstOrDefault(z => ReferenceEquals(z.Source, source));
        return cache is not null;
    }

    private async Task B_Add_Click()
    {
        var s = _builder.Create();
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
        RTB_Instructions.CaretIndex = RTB_Instructions.Text.Length;
    }

    private CancellationTokenSource _filterCountCancellation = new();
    private int _filterCountGeneration;

    private async void UpdateFilterCountDebounced()
    {
        try
        {
            var text = RTB_Instructions.Text ?? string.Empty;
            await _filterCountCancellation.CancelAsync();
            _filterCountCancellation.Dispose();

            var cancellation = new CancellationTokenSource();
            _filterCountCancellation = cancellation;

            var generation = ++_filterCountGeneration;
            await Task.Delay(250, cancellation.Token);
            if (cancellation.IsCancellationRequested)
                return;

            var data = GetCurrentData();
            var result = await Task.Run(() => TryGetFilterMessage(text, data, cancellation.Token, out var message)
                    ? message
                    : null, cancellation.Token); // return to GUI thread

            if (cancellation.IsCancellationRequested)
                return;
            if (generation != _filterCountGeneration)
                return;
            L_Count.Text = result;
            UpdateButtons();
        }
        catch
        {
            // Don't care.
        }
    }

    private bool TryGetFilterMessage(ReadOnlySpan<char> text, IReadOnlyList<SlotCache> data, CancellationToken token, [NotNullWhen(true)] out string? result)
    {
        result = null;
        int total = data.Count(z => z.Entity.Species != 0);
        if (total == 0)
        {
            result = string.Format(_matchingCountFormat, 0, 0);
            return true;
        }

        if (!TryGetInstructionSetsQuiet(text, out var sets, allowOnlyFilters: true))
        {
            result = string.Format(_matchingCountFormat, "-", total);
            return true;
        }

        foreach (var set in sets)
            EntityBatchEditor.ScreenStrings(set.Filters);

        if (token.IsCancellationRequested)
            return false;

        int matched = 0;
        var max = _sav.MaxSpeciesID;
        foreach (var entry in data)
        {
            var pk = entry.Entity;
            if (pk.Species == 0 || pk.Species > max)
                continue;
            if (entry.Source is SlotInfoBox info && _sav.GetBoxSlotFlags(info.Box, info.Slot).IsOverwriteProtected())
                continue;

            if (token.IsCancellationRequested)
                return false;

            if (sets.Any(set => IsFilterMatch(entry, set)))
                matched++;
        }

        result = string.Format(_matchingCountFormat, matched, total);
        return true;
    }

    private static bool IsFilterMatch(SlotCache entry, StringInstructionSet set)
    {
        var filterMeta = set.Filters.Where(IsMetaFilter).ToArray();
        var filters = set.Filters.Where(z => !IsMetaFilter(z)).ToArray();

        if (!EntityBatchEditor.IsFilterMatchMeta(filterMeta, entry))
            return false;

        return filters.Length == 0 || BatchEditingUtil.IsFilterMatch(filters, entry.Entity);
    }

    private static bool IsMetaFilter(StringInstruction filter) => BatchFilters.FilterMeta.Any(z => z.IsMatch(filter.PropertyName));

    /// <summary>
    /// Parses the instruction text without any user prompt (used by the background filter count).
    /// </summary>
    private static bool TryGetInstructionSetsQuiet(ReadOnlySpan<char> text, out StringInstructionSet[] sets, bool allowOnlyFilters = false)
    {
        sets = [];
        if (text.IsEmpty)
            return false;
        if (StringInstructionSet.HasEmptyLine(text))
            return false;

        try
        {
            sets = StringInstructionSet.GetBatchSets(text);
        }
        catch
        {
            return false;
        }

        if (Array.Exists(sets, s => s.Filters.Any(z => string.IsNullOrWhiteSpace(z.PropertyValue))))
            return false;
        if (Array.Exists(sets, z => z.Instructions.Count == 0))
            return allowOnlyFilters && sets.Any(z => z.Filters.Count != 0);
        return true;
    }

    /// <summary>
    /// Parses the instruction text, reporting problems to the user.
    /// </summary>
    /// <returns>Parsed sets, or null if the text cannot be used.</returns>
    private async Task<StringInstructionSet[]?> TryGetInstructionSets(string text, bool promptForEmptyValues, bool showErrors)
    {
        if (text.Length == 0)
            return null;
        if (StringInstructionSet.HasEmptyLine(text))
        {
            if (showErrors)
                await AppDialogs.Error(this, MsgBEInstructionInvalid);
            return null;
        }

        StringInstructionSet[] sets;
        try
        {
            sets = StringInstructionSet.GetBatchSets(text);
        }
        catch
        {
            if (showErrors)
                await AppDialogs.Error(this, MsgBEInstructionInvalid);
            return null;
        }

        if (Array.Exists(sets, s => s.Filters.Any(z => string.IsNullOrWhiteSpace(z.PropertyValue))))
        {
            if (showErrors)
                await AppDialogs.Error(this, MsgBEFilterEmpty);
            return null;
        }
        if (Array.Exists(sets, z => z.Instructions.Count == 0))
        {
            if (showErrors)
                await AppDialogs.Error(this, MsgBEInstructionNone);
            return null;
        }

        if (!promptForEmptyValues)
            return sets;

        var emptyVal = sets.SelectMany(s => s.Instructions.Where(z => string.IsNullOrWhiteSpace(z.PropertyValue))).ToArray();
        if (emptyVal.Length == 0)
            return sets;

        string props = string.Join(", ", emptyVal.Select(z => z.PropertyName));
        string invalid = MsgBEPropertyEmpty + Environment.NewLine + props;
        var dr = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, invalid, MsgContinue);
        return dr == DialogResult.Yes ? sets : null;
    }

    private async Task RunBatchEditSaveFile(IReadOnlyCollection<StringInstructionSet> sets)
    {
        var data = GetCurrentData();
        if (data.Count == 0)
            return;

        _editor = new EntityBatchProcessor();
        foreach (var set in sets)
            ProcessSAV(data, set.Filters, set.Instructions);

        UpdateFilterCountDebounced();
        UpdateButtons();

        string result = _editor.GetEditorResults(sets);
        await AppDialogs.Alert(this, result);
    }

    private void ProcessSAV(IReadOnlyList<SlotCache> data, IReadOnlyList<StringInstruction> filters, IReadOnlyList<StringInstruction> instructions)
    {
        var filterMeta = filters.Where(IsMetaFilter).ToArray();
        if (filterMeta.Length != 0)
            filters = [.. filters.Where(z => !IsMetaFilter(z))];

        var max = _sav.MaxSpeciesID;
        foreach (var entry in data)
        {
            var pk = entry.Entity;
            var spec = pk.Species;
            if (spec == 0 || spec > max)
                continue;

            if (entry.Source is SlotInfoBox info && _sav.GetBoxSlotFlags(info.Box, info.Slot).IsOverwriteProtected())
                continue;
            if (!EntityBatchEditor.IsFilterMatchMeta(filterMeta, entry))
                continue;

            if (_editor.Process(pk, filters, instructions))
                _modifiedSlots.Add(entry.Source);
        }
    }

    private async Task RunBatchEditFolder(IReadOnlyCollection<StringInstructionSet> sets)
    {
        if (string.IsNullOrWhiteSpace(TB_Folder.Text))
            return;

        await AppDialogs.Alert(this, MsgExportFolder, MsgExportFolderAdvice);
        var destination = await FileDialogs.PickFolder(this);
        if (destination is null)
            return;

        var data = GetCurrentData();
        if (data.Count == 0)
            return;

        _editor = new EntityBatchProcessor();
        foreach (var set in sets)
            ProcessFolder(data, destination, set.Filters, set.Instructions);

        string result = _editor.GetEditorResults(sets);
        await AppDialogs.Alert(this, result);
        UpdateFilterCountDebounced();
    }

    private void ProcessFolder(IReadOnlyList<SlotCache> data, string destDir, IReadOnlyList<StringInstruction> pkFilters, IReadOnlyList<StringInstruction> instructions)
    {
        var filterMeta = pkFilters.Where(IsMetaFilter).ToArray();
        if (filterMeta.Length != 0)
            pkFilters = [.. pkFilters.Where(z => !IsMetaFilter(z))];

        Span<byte> maxEntity = stackalloc byte[0x800]; // lol too big, futureproof for now
        foreach (var entry in data)
        {
            if (!EntityBatchEditor.IsFilterMatchMeta(filterMeta, entry))
                continue;

            if (!_editor.Process(entry.Entity, pkFilters, instructions))
                continue;

            if (!_folderPaths.TryGetValue(entry.Source, out var source))
                continue;

            // We might have mixed size files, so we can't have a shared stackalloc
            var result = maxEntity[..entry.Entity.SIZE_PARTY];
            entry.Entity.ForcePartyData();
            entry.Entity.WriteDecryptedDataParty(result);
            File.WriteAllBytes(Path.Combine(destDir, Path.GetFileName(source)), result);
        }
    }

    private void UpdateButtons()
    {
        bool isOperatingOnFolder = RB_Path.IsChecked == true;
        B_Run.IsEnabled = (RTB_Instructions.Text ?? string.Empty).Length != 0 && (isOperatingOnFolder || GetCurrentData().Count(z => z.Entity.Species != 0) != 0);
        B_Save.IsEnabled = isOperatingOnFolder || _modifiedSlots.Count != 0;
        B_Reset.IsEnabled = _modifiedSlots.Count != 0;
    }
}
