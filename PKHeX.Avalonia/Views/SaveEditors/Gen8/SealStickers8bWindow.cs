using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen8;

/// <summary>
/// Seal sticker editor for Brilliant Diamond / Shining Pearl (port of the WinForms <c>SAV_SealStickers8b</c>).
/// </summary>
/// <remarks>
/// Each sticker tracks how many are held, how many were ever obtained, and whether the entry is unlocked.
/// Stickers with no name in the string table are hidden, matching the WinForms form.
/// </remarks>
public sealed class SealStickers8bWindow : SaveEditorWindow
{
    private readonly SAV8BS SAV;
    private readonly IReadOnlyList<SealSticker8b> AllItems;
    private readonly string[] ItemNames;
    private readonly ObservableCollection<StickerRow> Rows = [];

    private readonly DataGrid dgv = new()
    {
        Name = "dgv",
        AutoGenerateColumns = false,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        CanUserSortColumns = true,
        Height = 460,
        Width = 560,
    };
    private readonly Button B_All = UiFactory.Button("B_All", "All");
    private readonly Button B_None = UiFactory.Button("B_None", "None");

    public SealStickers8bWindow(SAV8BS sav) : base("SAV_SealStickers8b", "Seal Stickers")
    {
        SAV = sav;
        ItemNames = Util.GetStringList("stickers", MainWindow.CurrentLanguage);
        AllItems = SAV.SealList.ReadItems();

        dgv.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding(nameof(StickerRow.Id)), IsReadOnly = true, Width = new DataGridLength(60) });
        dgv.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(StickerRow.Name)), IsReadOnly = true, Width = new DataGridLength(180) });
        dgv.Columns.Add(new DataGridTextColumn { Header = "Count", Binding = new Binding(nameof(StickerRow.Count)) { Mode = BindingMode.TwoWay }, Width = new DataGridLength(90) });
        dgv.Columns.Add(new DataGridTextColumn { Header = "Total", Binding = new Binding(nameof(StickerRow.Total)) { Mode = BindingMode.TwoWay }, Width = new DataGridLength(90) });
        dgv.Columns.Add(DataGridUtil.CheckColumn("Obtained", nameof(StickerRow.IsGet), 110));
        dgv.ItemsSource = Rows;

        SetBody(UiFactory.Column(UiFactory.Row(B_All, B_None), dgv));

        LoadItems();

        B_All.Click += (_, _) => SetAll(true);
        B_None.Click += (_, _) => SetAll(false);
    }

    private void LoadItems()
    {
        Rows.Clear();
        foreach (var item in AllItems)
        {
            var index = item.Index;
            if ((uint)index >= ItemNames.Length || ItemNames[index].Length == 0)
                continue;
            Rows.Add(new StickerRow
            {
                Index = index,
                Id = index.ToString("000"),
                Name = ItemNames[index],
                Count = item.Count.ToString(),
                Total = item.TotalCount.ToString(),
                IsGet = item.IsGet,
            });
        }
    }

    private void SetAll(bool obtained)
    {
        foreach (var row in Rows)
        {
            if (!obtained)
            {
                row.Count = row.Total = "0";
                row.IsGet = false;
                continue;
            }

            const int max = SealSticker8b.MaxValue;
            var count = int.TryParse(row.Count, out var c) ? c : 0;
            var total = int.TryParse(row.Total, out var t) ? t : 0;
            count += max - total;
            row.Count = count.ToString();
            row.Total = max.ToString();
            row.IsGet = true;
        }
        dgv.ItemsSource = null;
        dgv.ItemsSource = Rows;
    }

    protected override void OnSave()
    {
        foreach (var row in Rows)
        {
            var item = AllItems[row.Index];
            item.Count = int.TryParse(row.Count, out var count) ? count : 0;
            item.TotalCount = int.TryParse(row.Total, out var total) ? total : 0;
            item.IsGet = row.IsGet;
        }
        SAV.SealList.WriteItems(AllItems);
        Close();
    }

    private sealed class StickerRow
    {
        public required int Index { get; init; }
        public required string Id { get; init; }
        public required string Name { get; init; }
        public string Count { get; set; } = "0";
        public string Total { get; set; } = "0";
        public bool IsGet { get; set; }
    }
}
