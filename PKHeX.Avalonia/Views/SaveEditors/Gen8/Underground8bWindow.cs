using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Data;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen8;

/// <summary>
/// Underground item editor for Brilliant Diamond / Shining Pearl (port of the WinForms <c>SAV_Underground8b</c>).
/// </summary>
/// <remarks>
/// Only slots with a known item type are listed. Each row carries how many are held plus the "new" and
/// "favorite" markers the game shows in the bag.
/// </remarks>
public sealed class Underground8bWindow : SaveEditorWindow
{
    private readonly SAV8BS SAV;
    private readonly IReadOnlyList<UndergroundItem8b> AllItems;
    private readonly string[] ItemNames;
    private readonly ObservableCollection<UgRow> Rows = [];

    private readonly DataGrid dgv = new()
    {
        Name = "dgv",
        AutoGenerateColumns = false,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        CanUserSortColumns = true,
        Height = 460,
        Width = 640,
    };
    private readonly Button B_All = UiFactory.Button("B_All", "All");
    private readonly Button B_None = UiFactory.Button("B_None", "None");

    public Underground8bWindow(SAV8BS sav) : base("SAV_Underground8b", "Underground Items")
    {
        SAV = sav;
        ItemNames = Util.GetStringList("ug_item", MainWindow.CurrentLanguage);
        AllItems = SAV.Underground.ReadItems();

        dgv.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding(nameof(UgRow.Id)), IsReadOnly = true, Width = new DataGridLength(60) });
        dgv.Columns.Add(new DataGridTextColumn { Header = "Type", Binding = new Binding(nameof(UgRow.Type)), IsReadOnly = true, Width = new DataGridLength(110) });
        dgv.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(UgRow.Name)), IsReadOnly = true, Width = new DataGridLength(190) });
        dgv.Columns.Add(new DataGridTextColumn { Header = "Count", Binding = new Binding(nameof(UgRow.Count)) { Mode = BindingMode.TwoWay }, Width = new DataGridLength(80) });
        dgv.Columns.Add(DataGridUtil.CheckColumn("New", nameof(UgRow.IsNew), 80));
        dgv.Columns.Add(DataGridUtil.CheckColumn("Favorite", nameof(UgRow.IsFavorite), 100));
        dgv.ItemsSource = Rows;

        SetBody(UiFactory.Column(UiFactory.Row(B_All, B_None), dgv));

        LoadItems();

        B_All.Click += (_, _) =>
        {
            foreach (var row in Rows)
                row.Count = AllItems[row.Index].MaxValue.ToString();
            Rebind();
        };
        B_None.Click += (_, _) =>
        {
            foreach (var row in Rows)
            {
                row.Count = "0";
                row.IsNew = true;
                row.IsFavorite = false;
            }
            Rebind();
        };
    }

    private void LoadItems()
    {
        Rows.Clear();
        foreach (var item in AllItems)
        {
            if (item.Type == UgItemType.None)
                continue;
            Rows.Add(new UgRow
            {
                Index = item.Index,
                Id = item.Index.ToString("000"),
                Type = item.Type.ToString(),
                Name = (uint)item.Index < ItemNames.Length ? ItemNames[item.Index] : $"#{item.Index}",
                Count = item.Count.ToString(),
                IsNew = !item.HideNewFlag,
                IsFavorite = item.IsFavoriteFlag,
            });
        }
    }

    private void Rebind()
    {
        dgv.ItemsSource = null;
        dgv.ItemsSource = Rows;
    }

    protected override void OnSave()
    {
        foreach (var row in Rows)
        {
            var item = AllItems[row.Index];
            item.Count = int.TryParse(row.Count, out var count) ? count : 0;
            item.HideNewFlag = !row.IsNew;
            item.IsFavoriteFlag = row.IsFavorite;
        }
        SAV.Underground.WriteItems(AllItems);
        Close();
    }

    private sealed class UgRow
    {
        public required int Index { get; init; }
        public required string Id { get; init; }
        public required string Type { get; init; }
        public required string Name { get; init; }
        public string Count { get; set; } = "0";
        public bool IsNew { get; set; }
        public bool IsFavorite { get; set; }
    }
}
