using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen5.JoinAvenue;

/// <summary>
/// A list of Join Avenue entities with a shared general editor and a per-kind editor
/// (port of the WinForms <c>JoinAvenueListEditor&lt;T, TSpecific&gt;</c>).
/// </summary>
internal sealed class JoinAvenueListView<T, TSpecific> : StackPanel
    where T : class, IJoinAvenueEntity5
    where TSpecific : Control, IJoinAvenueSpecificView<T>
{
    private const string ImportFilter = "Join Avenue Entity (*.jav5;*.jaa5;*.jah5)|*.jav5;*.jaa5;*.jah5|All Files|*.*";

    private readonly JoinAvenueEntityGeneralView GeneralEditor = new();
    private readonly TSpecific SpecificEditor;
    private readonly Func<int, T> Getter;
    private readonly int Count;

    private readonly ListBox LB_Entries = new() { Name = "LB_Entries", Width = 190, Height = 460 };
    private readonly ObservableCollection<string> Items = [];
    private readonly Button B_Import = UiFactory.Button("B_Import", "Import");
    private readonly Button B_Export = UiFactory.Button("B_Export", "Export");

    private int CurrentIndex = -1;
    private bool Loading;

    public JoinAvenueListView(int count, Func<int, T> getter, TSpecific specificEditor)
    {
        Count = count;
        Getter = getter;
        SpecificEditor = specificEditor;

        Orientation = Orientation.Horizontal;
        Spacing = 10;
        LB_Entries.ItemsSource = Items;

        var tabs = new TabControl { Name = "TC_Entity" };
        tabs.Items.Add(new TabItem { Name = "Tab_General", Header = "General", Content = new ScrollViewer { Content = GeneralEditor, MaxHeight = 520 } });
        tabs.Items.Add(new TabItem { Name = "Tab_Specific", Header = "Specific", Content = new ScrollViewer { Content = SpecificEditor, MaxHeight = 520 } });

        Children.Add(UiFactory.Column(LB_Entries, UiFactory.Row(B_Import, B_Export)));
        Children.Add(tabs);

        LB_Entries.SelectionChanged += (_, _) => ChangeIndex();
        B_Import.Click += async (_, _) => await ClickImport();
        B_Export.Click += async (_, _) => await ClickExport();
    }

    public void LoadAll()
    {
        Loading = true;
        Items.Clear();
        for (int i = 0; i < Count; i++)
            Items.Add(GetLabel(i, Getter(i)));
        Loading = false;
        if (Items.Count != 0)
            LB_Entries.SelectedIndex = 0;
    }

    public void SaveAll()
    {
        SaveCurrent();
        RefreshLabels();
    }

    private void ChangeIndex()
    {
        if (Loading)
            return;

        SaveCurrent();
        CurrentIndex = LB_Entries.SelectedIndex;
        if (CurrentIndex < 0)
            return;

        Loading = true;
        var entity = Getter(CurrentIndex);
        GeneralEditor.LoadObject(entity);
        SpecificEditor.LoadObject(entity);
        Loading = false;
    }

    private void SaveCurrent()
    {
        if (Loading || CurrentIndex < 0)
            return;

        var entity = Getter(CurrentIndex);
        GeneralEditor.SaveObject(entity);
        SpecificEditor.SaveObject(entity);
        SetLabel(CurrentIndex, entity);
    }

    private void RefreshLabels()
    {
        for (int i = 0; i < Count && i < Items.Count; i++)
            SetLabel(i, Getter(i));
    }

    private void SetLabel(int index, T entity)
    {
        if ((uint)index >= Items.Count)
            return;
        Loading = true;
        var selected = LB_Entries.SelectedIndex;
        Items[index] = GetLabel(index, entity);
        LB_Entries.SelectedIndex = selected; // replacing the item drops the selection
        Loading = false;
    }

    private static string GetLabel(int index, T entity)
    {
        var label = entity.Name.Trim();
        if (string.IsNullOrWhiteSpace(label))
            label = GameInfo.Strings.specieslist[0];
        return $"{index + 1:00} - {label}";
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    private async Task ClickExport()
    {
        SaveCurrent();
        if (CurrentIndex < 0 || Owner is not { } owner)
            return;

        var entity = Getter(CurrentIndex);
        var filter = $"Join Avenue Entity (*.{entity.FileExtension})|*.{entity.FileExtension}|All Files|*.*";
        var suggested = PathUtil.CleanFileName($"{CurrentIndex + 1:00}_{entity.Name}") + $".{entity.FileExtension}";
        var path = await FileDialogs.SaveFileDialog(owner, filter, suggested);
        if (path is null)
            return;
        await File.WriteAllBytesAsync(path, entity.Write().ToArray());
    }

    private async Task ClickImport()
    {
        if (CurrentIndex < 0 || Owner is not { } owner)
            return;

        var path = await FileDialogs.OpenSingleFile(owner, ImportFilter);
        if (path is null)
            return;

        var data = await File.ReadAllBytesAsync(path);
        if (!TryCreateImportedEntity(data, out var imported))
        {
            await AppDialogs.Error(owner, "Unable to import Join Avenue entity.");
            return;
        }

        var entity = Getter(CurrentIndex);
        entity.CopyFrom(imported);
        Loading = true;
        GeneralEditor.LoadObject(entity);
        SpecificEditor.LoadObject(entity);
        Loading = false;
        SetLabel(CurrentIndex, entity);
    }

    private static bool TryCreateImportedEntity(Memory<byte> data, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IJoinAvenueEntity5? entity)
    {
        switch (data.Length)
        {
            case JoinAvenueVisitor5.SIZE:
                entity = new JoinAvenueVisitor5(data);
                return true;
            case JoinAvenueFan5.SIZE:
                entity = new JoinAvenueFan5(data);
                return true;
            case JoinAvenueAssistant5.SIZE:
                entity = new JoinAvenueAssistant5(data);
                return true;
            default:
                entity = null;
                return false;
        }
    }
}
