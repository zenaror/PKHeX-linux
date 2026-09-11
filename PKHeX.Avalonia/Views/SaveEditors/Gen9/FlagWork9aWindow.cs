using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views.SaveEditors.Gen9.EventWork;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen9;

/// <summary>
/// Event flag and work value editor for Legends: Z-A (port of the WinForms <c>SAV_FlagWork9a</c>).
/// </summary>
/// <remarks>
/// Z-A stores its progress as hashed key/value blocks rather than a flat flag array, so each block gets its
/// own searchable grid. A name list dropped in the block key folder turns the hashes into readable names.
/// </remarks>
public sealed class FlagWork9aWindow : SaveEditorWindow
{
    private readonly Dictionary<ulong, string> Lookup = new() { { FnvHash.HashEmpty, "" } };
    private readonly EventWorkLookup Names;
    private readonly IEventWorkGrid[] Grids;

    private readonly TabControl TC_Features = new() { Name = "TC_Features" };
    private readonly TextBox TB_OldSAV = UiFactory.Text("TB_OldSAV", 260, 340);
    private readonly TextBox TB_NewSAV = UiFactory.Text("TB_NewSAV", 260, 340);
    private readonly Button B_LoadOld = UiFactory.Button("B_LoadOld", "Load Old");
    private readonly Button B_LoadNew = UiFactory.Button("B_LoadNew", "Load New");
    private readonly TextBox RTB_Diff = new() { Name = "RTB_Diff", AcceptsReturn = true, IsReadOnly = true, Height = 420, TextWrapping = global::Avalonia.Media.TextWrapping.NoWrap };

    public FlagWork9aWindow(SAV9ZA sav) : base("SAV_FlagWork9a", "Event Flag/Work Editor")
    {
        var path = Path.Combine(MainWindow.Settings.Advanced.PathBlockKeyList, $"{sav.GetType().Name}_flagwork.txt");
        if (File.Exists(path))
            SCBlockMetadata.AddExtraKeyNames64(Lookup, File.ReadLines(path));

        Names = new EventWorkLookup(Lookup);

        var b = sav.Blocks;
        Grids =
        [
            EventWorkGrid64<bool>.CreateFlags(AddTab(nameof(b.Flags)), b.Flags, Names),
            EventWorkGrid64<bool>.CreateFlags(AddTab(nameof(b.Event)), b.Event, Names),
            EventWorkGrid64<ulong>.CreateValues(AddTab(nameof(b.Work)), b.Work, Names),
            EventWorkGrid64<ulong>.CreateValues(AddTab(nameof(b.Quest)), b.Quest, Names),
            EventWorkGrid64<ulong>.CreateValues(AddTab(nameof(b.WorkMable)), b.WorkMable, Names),
            EventWorkGrid64<ulong>.CreateValues(AddTab(nameof(b.CountMable)), b.CountMable, Names),

            EventWorkGrid64<ulong>.CreateValues(AddTab(nameof(b.CountTitle)), b.CountTitle, Names),
            EventWorkGridTuple.CreateValues(AddTab(nameof(b.Report)), b.Report, Names),

            EventWorkGrid64<ulong>.CreateValues(AddTab(nameof(b.WorkSpawn)), b.WorkSpawn, Names),
            EventWorkGrid64<ulong>.CreateValues(AddTab(nameof(b.InfiniteRank)), b.InfiniteRank, Names),
            EventWorkGridTuple.CreateValues(AddTab(nameof(b.Spawner2)), b.Spawner2, Names),
            EventWorkGrid128.CreateValues(AddTab(nameof(b.Spawner4)), b.Spawner4, Names),

            EventWorkGridTuple.CreateValues(AddTab(nameof(b.Obstruction)), b.Obstruction, Names),
            EventWorkGrid64<bool>.CreateFlags(AddTab(nameof(b.FieldItems)), b.FieldItems, Names),
            EventWorkGrid192.CreateValues(AddTab(nameof(b.FieldObjectInteractable)), b.FieldObjectInteractable, Names),
        ];

        AddDiffTab(sav);
        SetBody(TC_Features);

        foreach (var grid in Grids)
            grid.Load();
        TC_Features.SelectedIndex = 0;
    }

    private ContentControl AddTab(string name)
    {
        var host = new ContentControl();
        TC_Features.Items.Add(new TabItem { Name = $"Tab_{name}", Header = name, Content = host });
        return host;
    }

    private void AddDiffTab(SAV9ZA sav)
    {
        B_LoadOld.Click += async (_, _) => await PickSave(TB_OldSAV, sav);
        B_LoadNew.Click += async (_, _) => await PickSave(TB_NewSAV, sav);

        var body = UiFactory.Column(
            UiFactory.Row(B_LoadOld, TB_OldSAV),
            UiFactory.Row(B_LoadNew, TB_NewSAV),
            RTB_Diff);
        TC_Features.Items.Add(new TabItem { Name = "Tab_Diff", Header = "Compare", Content = body });
    }

    private async Task PickSave(TextBox target, SAV9ZA _)
    {
        var path = await FileDialogs.OpenSingleFile(this);
        if (path is null)
            return;
        target.Text = path;
        if ((TB_NewSAV.Text ?? string.Empty).Length != 0 && (TB_OldSAV.Text ?? string.Empty).Length != 0)
            await DiffSaves(TB_NewSAV.Text!, TB_OldSAV.Text!);
    }

    private async Task DiffSaves(string fileUpdated, string filePrevious)
    {
        if (!SaveUtil.TryGetSaveFile(fileUpdated, out var s1) || s1 is not SAV9ZA updated)
        {
            await AppDialogs.Error(this, EventWorkDiffCompatibility.FileMissing1.GetMessage());
            return;
        }
        if (!SaveUtil.TryGetSaveFile(filePrevious, out var s2) || s2 is not SAV9ZA previous)
        {
            await AppDialogs.Error(this, EventWorkDiffCompatibility.FileMissing2.GetMessage());
            return;
        }

        List<string> result = [];
        AppendDiff<EventWorkFlagStorage, bool>(result, updated.Blocks.Flags, previous.Blocks.Flags);
        AppendDiff<EventWorkFlagStorage, bool>(result, updated.Blocks.Event, previous.Blocks.Event);
        AppendDiff<EventWorkValueStorage, ulong>(result, updated.Blocks.Work, previous.Blocks.Work);
        AppendDiff<EventWorkValueStorage, ulong>(result, updated.Blocks.Quest, previous.Blocks.Quest);
        AppendDiff<EventWorkValueStorage, ulong>(result, updated.Blocks.WorkMable, previous.Blocks.WorkMable);
        AppendDiff<EventWorkValueStorage, ulong>(result, updated.Blocks.CountMable, previous.Blocks.CountMable);
        AppendDiff<EventWorkValueStorage, ulong>(result, updated.Blocks.CountTitle, previous.Blocks.CountTitle);
        AppendDiff<EventWorkValueStorage, ulong>(result, updated.Blocks.WorkSpawn, previous.Blocks.WorkSpawn);
        AppendDiff<EventWorkFlagStorage, bool>(result, updated.Blocks.FieldItems, previous.Blocks.FieldItems);

        if (result.Count == 0)
            result.Add("No differences found.");
        RTB_Diff.Text = string.Join(Environment.NewLine, result);
    }

    private void AppendDiff<T1, T2>(List<string> result, T1 update, T1 previous, [CallerArgumentExpression(nameof(update))] string title = null!)
        where T1 : IEventValueStorage<T2> where T2 : unmanaged, IEquatable<T2>
    {
        var diff = DiffBlocks<T1, T2>(update, previous);
        if (diff.Count == 0)
            return;
        result.Add("=====");
        result.Add($"{title}:");
        result.Add("=====");
        result.AddRange(diff);
        result.Add(string.Empty);
    }

    private List<string> DiffBlocks<T1, T2>(T1 update, T1 previous) where T1 : IEventValueStorage<T2> where T2 : unmanaged, IEquatable<T2>
    {
        List<string> result = [];
        HashSet<ulong> hashes = [];
        var count = update.Count;
        for (int i = 0; i < count; i++)
        {
            var hash = update.GetKey(i);
            if (hash == FnvHash.HashEmpty)
                break;
            var u = update.GetValue(i);

            var name = Names.GetName(hash);
            if (!previous.TryGetValue(hash, out var p))
                result.Add($"{name} added with value {u}");
            else if (!p.Equals(u))
                result.Add($"{name} changed from {p} to {u}");

            hashes.Add(hash);
        }

        count = previous.Count;
        for (var i = 0; i < count; i++)
        {
            var hash = update.GetKey(i);
            if (hash == FnvHash.HashEmpty)
                break;
            if (hashes.Contains(hash))
                continue;
            var p = previous.GetValue(i);
            result.Add($"{Names.GetName(hash)} @ {i:X} removed, was {p}");
        }
        return result;
    }

    protected override void OnSave()
    {
        foreach (var grid in Grids)
            grid.Save();
        Close();
    }
}
