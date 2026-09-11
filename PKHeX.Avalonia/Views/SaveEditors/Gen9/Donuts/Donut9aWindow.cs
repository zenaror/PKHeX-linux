using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using Avalonia.Platform.Storage;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen9.Donuts;

/// <summary>
/// Donut pocket editor for Legends: Z-A (port of the WinForms <c>SAV_Donut9a</c>).
/// </summary>
public sealed class Donut9aWindow : SaveEditorWindow
{
    private const string DonutFilter = "Donut File|*.donut|All Files|*.*";

    private readonly SAV9ZA Origin;
    private readonly SAV9ZA SAV;
    private readonly DonutPocket9a Donuts;

    private readonly ObservableCollection<DonutEntry> Entries = [];
    private readonly ListBox LB_Donut = new() { Name = "LB_Donut", Width = 200, Height = 460 };
    private readonly DonutEditor9aView DonutEditor = new() { Name = "donutEditor" };
    private readonly DonutFlavorProfile9aView DonutFlavorProfile = new() { Name = "DonutFlavorProfile" };
    private readonly Button B_ModifyAll = UiFactory.Button("B_ModifyAll", "Modify All");
    private readonly Button B_Reset = UiFactory.Button("B_Reset", "Reset");
    private readonly Button B_Import = UiFactory.Button("B_Import", "Import");
    private readonly Button B_Export = UiFactory.Button("B_Export", "Export");

    private int lastIndex;
    private bool Loading;

    public Donut9aWindow(SAV9ZA sav) : base("SAV_Donut9a", "Donut Editor")
    {
        SAV = (SAV9ZA)(Origin = sav).Clone();
        Donuts = SAV.Donuts;

        LB_Donut.ItemsSource = Entries;
        LB_Donut.DisplayMemberBinding = new Binding(nameof(DonutEntry.Text));

        var strings = GameInfo.Strings;
        DonutEditor.InitializeLists(strings.donutFlavor, strings.itemlist, strings.donutName);
        DonutEditor.ValueChanged += (_, _) => Editor_ValueChanged();

        foreach (var b in new[] { B_ModifyAll, B_Reset, B_Import, B_Export })
        {
            b.MinWidth = 128;
            b.Padding = new global::Avalonia.Thickness(8, 4);
        }
        B_ModifyAll.Flyout = BuildModifyMenu();

        var actions = UiFactory.Column(UiFactory.Row(B_Import, B_Export), UiFactory.Row(B_ModifyAll, B_Reset));
        actions.VerticalAlignment = VerticalAlignment.Bottom;
        var lower = UiFactory.Row(actions, DonutFlavorProfile);
        lower.VerticalAlignment = VerticalAlignment.Bottom;
        lower.Spacing = 16;
        var right = UiFactory.Column(DonutEditor, lower);
        right.Spacing = 10;
        SetBody(UiFactory.Row(LB_Donut, right));

        Loading = true;
        LoadDonutNames();
        LB_Donut.SelectedIndex = 0;
        Loading = false;

        lastIndex = 0;
        GetEntry(0);

        LB_Donut.SelectionChanged += (_, _) => ChangeIndex();
        B_Reset.Click += (_, _) => { DonutEditor.Reset(); Refresh(Donuts.GetDonut(lastIndex)); };
        B_Import.AttachClickHandled(async mods => await ClickImport(mods));
        B_Export.AttachClickHandled(async mods => await ClickExport(mods));

        // Dropping a .donut file anywhere on the form overwrites the selected entry, as in WinForms.
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = DragDropEffects.Copy; e.Handled = true; });
        AddHandler(DragDrop.DropEvent, async (_, e) => await WindowDrop(e));
    }

    private MenuFlyout BuildModifyMenu()
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.Bottom };
        AddItem(flyout, "mnuRandomizeMax", "Randomize Max Level", RandomizeAll);
        AddItem(flyout, "mnuCloneCurrent", "Clone Current to All", CloneCurrent);
        AddItem(flyout, "mnuShinyAssortment", "Shiny Assortment", ShinyAssortment);
        AddItem(flyout, "mnuGenerateRandom", "Generate Random...", () => _ = GenerateRandom());
        return flyout;
    }

    private static void AddItem(MenuFlyout flyout, string name, string header, Action action)
    {
        var item = new MenuItem { Name = name, Header = header };
        item.Click += (_, _) => action();
        flyout.Items.Add(item);
    }

    private void LoadDonutNames()
    {
        for (int i = 0; i < DonutPocket9a.MaxCount; i++)
            Entries.Add(new DonutEntry { Text = GetDonutName(i) });
    }

    private void ReloadDonutNames()
    {
        for (int i = 0; i < DonutPocket9a.MaxCount; i++)
            Entries[i].Text = GetDonutName(i);
    }

    private string GetDonutName(int i) => GetDonutName(Donuts.GetDonut(i), i);

    private static string GetDonutName(Donut9a donut, int i)
    {
        var flavorCount = donut.FlavorCount;
        var flavorString = new string('*', flavorCount);
        return $"#{i + 1:000} {donut.Stars}⭐ @ {donut.Calories:0000} cal {flavorString}";
    }

    private void Editor_ValueChanged()
    {
        if (Loading)
            return;

        Loading = true;
        var index = lastIndex;
        // The editor writes to its own copy of the fields; flush them so the summary line and the chart agree.
        DonutEditor.SaveDonut();
        var donut = Donuts.GetDonut(index);
        // Only refresh the name in the list if it has changed.
        var currentName = GetDonutName(donut, index);
        if (Entries[index].Text != currentName)
            Entries[index].Text = currentName;

        // Update profile if applicable
        DonutFlavorProfile.LoadFromDonut(donut);
        Loading = false;
    }

    private void ChangeIndex()
    {
        if (Loading || LB_Donut.SelectedIndex < 0)
            return;

        SetEntry(lastIndex);
        lastIndex = LB_Donut.SelectedIndex;
        GetEntry(lastIndex);
    }

    private void GetEntry(int index)
    {
        if (Loading || index < 0)
            return;

        Loading = true;
        var donut = Donuts.GetDonut(index);
        DonutEditor.LoadDonut(donut);
        DonutFlavorProfile.LoadFromDonut(donut);
        Loading = false;
    }

    private void SetEntry(int index)
    {
        if (Loading || index < 0)
            return;

        DonutEditor.SaveDonut();
    }

    protected override void OnSave()
    {
        SetEntry(lastIndex);
        Origin.CopyChangesFrom(SAV);
        Close();
    }

    #region Modify All

    private void RandomizeAll()
    {
        Donuts.SetAllRandomLv3();
        ReloadDonutNames();
        GetEntry(lastIndex);
    }

    private void CloneCurrent()
    {
        SetEntry(lastIndex);
        Donuts.CloneAllFromIndex(lastIndex);
        ReloadDonutNames();
    }

    private void ShinyAssortment()
    {
        Donuts.SetAllAsShinyTemplate();
        ReloadDonutNames();
        GetEntry(lastIndex);
    }

    private async Task GenerateRandom()
    {
        var form = new DonutGenerator9aWindow(GenerateRandomDonuts);
        await form.ShowDialog(this);
    }

    private void GenerateRandomDonuts(ulong[] flavorOptions, int start, int end)
    {
        SetEntry(lastIndex);
        Donuts.SetRandomShinyTemplateRange(flavorOptions, start, end);
        ReloadDonutNames();
        GetEntry(lastIndex);
    }

    #endregion

    #region Import / Export

    private async Task ClickImport(KeyModifiers mods)
    {
        var current = Donuts.GetDonut(lastIndex);
        var data = current.Data;

        if (mods == KeyModifiers.Control)
        {
            if (!await TryLoadDonutClipboard(current))
                return;
        }
        else
        {
            var path = await FileDialogs.OpenSingleFile(this, DonutFilter);
            if (path is null)
                return;
            if (!await ImportDonutFromPath(current, path))
                return;
        }

        Refresh(current);
    }

    private async Task<bool> TryLoadDonutClipboard(Donut9a current)
    {
        // Import from clipboard as hex string
        try
        {
            var hex = (await ClipboardService.GetText(this) ?? string.Empty).Trim();
            Util.GetBytesFromHexString(hex.Replace(" ", ""), current.Data);
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(this, $"Failed to import donut from clipboard:\n{ex.Message}", ex);
            return false;
        }
        return true;
    }

    private async Task<bool> ImportDonutFromPath(Donut9a current, string path)
    {
        try
        {
            var fileData = File.ReadAllBytes(path);
            if (fileData.Length != Donut9a.Size)
                throw new Exception($"Invalid donut size: expected {Donut9a.Size} bytes, got {fileData.Length} bytes.");
            fileData.CopyTo(current.Data);
            return true;
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(this, $"Failed to import donut from file:\n{ex.Message}", ex);
            return false;
        }
    }

    private void Refresh(Donut9a current)
    {
        DonutEditor.LoadDonut(current);
        DonutFlavorProfile.LoadFromDonut(current);
        Entries[lastIndex].Text = GetDonutName(current, lastIndex);
    }

    private async Task ClickExport(KeyModifiers mods)
    {
        SetEntry(lastIndex);
        var current = Donuts.GetDonut(lastIndex);

        if (mods == KeyModifiers.Control)
        {
            // Copy to clipboard as hex string
            var sb = new StringBuilder(Donut9a.Size * 3);
            foreach (var b in current.Data)
                sb.Append($"{b:X2} ");
            await ClipboardService.SetText(this, sb.ToString().TrimEnd());
            return;
        }

        var suggestion = $"{lastIndex + 1:000}_{DonutEditor.GetDonutName()}.donut";
        var path = await FileDialogs.SaveFileDialog(this, DonutFilter, suggestion);
        if (path is null)
            return;
        File.WriteAllBytes(path, current.Data.ToArray());
    }

    private async Task WindowDrop(DragEventArgs e)
    {
        e.Handled = true;
        if (e.DataTransfer.TryGetFiles() is not { Length: not 0 } files)
            return;
        if (files[0].TryGetLocalPath() is not { } path)
            return;

        var current = Donuts.GetDonut(lastIndex);
        if (!await ImportDonutFromPath(current, path))
            return;
        Refresh(current);
    }

    #endregion

    /// <summary>List row; the instance is kept so the selection survives a label refresh.</summary>
    private sealed class DonutEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private string text = string.Empty;

        public required string Text
        {
            get => text;
            set
            {
                if (text == value)
                    return;
                text = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            }
        }
    }
}
