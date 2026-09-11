using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Data;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen8;

/// <summary>
/// Poffin case editor for Brilliant Diamond / Shining Pearl (port of the WinForms <c>SAV_Poffin8b</c>).
/// </summary>
/// <remarks>
/// Each slot stores the poffin type, its level and smoothness, and the five flavour values.
/// The type is stored as the string index minus one, so the empty entry maps to <c>0xFF</c>.
/// </remarks>
public sealed class Poffin8bWindow : SaveEditorWindow
{
    private readonly SAV8BS SAV;
    private readonly IReadOnlyList<Poffin8b> AllItems;
    private readonly string[] ItemNames;
    private readonly ObservableCollection<PoffinRow> Rows = [];

    private readonly DataGrid dgv = new()
    {
        Name = "dgv",
        AutoGenerateColumns = false,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        CanUserSortColumns = true,
        Height = 460,
        Width = 820,
    };
    private readonly Button B_All = UiFactory.Button("B_All", "All");
    private readonly Button B_None = UiFactory.Button("B_None", "None");

    public Poffin8bWindow(SAV8BS sav) : base("SAV_Poffin8b", "Poffins")
    {
        SAV = sav;
        ItemNames = Util.GetStringList("poffin8b", MainWindow.CurrentLanguage);
        ItemNames[0] = GameInfo.Strings.Item[0];
        AllItems = SAV.Poffins.GetPoffins();

        dgv.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding(nameof(PoffinRow.Id)), IsReadOnly = true, Width = new DataGridLength(55) });
        dgv.Columns.Add(DataGridUtil.StringComboColumn("Type", ItemNames, nameof(PoffinRow.Type), 150));
        dgv.Columns.Add(Text("Level", nameof(PoffinRow.Level)));
        dgv.Columns.Add(Text("Smooth", nameof(PoffinRow.Taste)));
        dgv.Columns.Add(new DataGridCheckBoxColumn { Header = "New", Binding = new Binding(nameof(PoffinRow.IsNew)) { Mode = BindingMode.TwoWay }, Width = new DataGridLength(55) });
        dgv.Columns.Add(Text("Spicy", nameof(PoffinRow.Spicy)));
        dgv.Columns.Add(Text("Dry", nameof(PoffinRow.Dry)));
        dgv.Columns.Add(Text("Sweet", nameof(PoffinRow.Sweet)));
        dgv.Columns.Add(Text("Bitter", nameof(PoffinRow.Bitter)));
        dgv.Columns.Add(Text("Sour", nameof(PoffinRow.Sour)));
        dgv.ItemsSource = Rows;

        SetBody(UiFactory.Column(UiFactory.Row(B_All, B_None), dgv));

        LoadItems();

        B_All.Click += (_, _) =>
        {
            foreach (var poffin in AllItems)
            {
                poffin.MstID = 0x1C;
                poffin.Level = 60;
                poffin.Taste = 0xFF;
                poffin.FlavorSpicy = poffin.FlavorBitter = poffin.FlavorDry = poffin.FlavorSour = poffin.FlavorSweet = 0xFF;
            }
            LoadItems();
        };
        B_None.Click += (_, _) =>
        {
            foreach (var poffin in AllItems)
                poffin.ToNull();
            LoadItems();
        };
    }

    private static DataGridTextColumn Text(string header, string path) => new()
    {
        Header = header,
        Binding = new Binding(path) { Mode = BindingMode.TwoWay },
        Width = new DataGridLength(85),
    };

    private void LoadItems()
    {
        Rows.Clear();
        for (var i = 0; i < AllItems.Count; i++)
        {
            var item = AllItems[i];
            Rows.Add(new PoffinRow
            {
                Index = i,
                Id = i.ToString("000"),
                Type = GetPoffinName(item.MstID),
                Level = item.Level.ToString(),
                Taste = item.Taste.ToString(),
                IsNew = item.IsNew,
                Spicy = item.FlavorSpicy.ToString(),
                Dry = item.FlavorDry.ToString(),
                Sweet = item.FlavorSweet.ToString(),
                Bitter = item.FlavorBitter.ToString(),
                Sour = item.FlavorSour.ToString(),
            });
        }
        dgv.ItemsSource = null;
        dgv.ItemsSource = Rows;
    }

    private string GetPoffinName(byte itemMstId)
    {
        var index = (int)(byte)(itemMstId + 1);
        if ((uint)index >= ItemNames.Length)
            index = 0;
        return ItemNames[index];
    }

    private byte SetPoffinName(string value)
    {
        var index = Array.IndexOf(ItemNames, value);
        return index != -1 ? (byte)(index - 1) : unchecked((byte)-1);
    }

    protected override void OnSave()
    {
        foreach (var row in Rows)
        {
            var item = AllItems[row.Index];
            item.MstID = SetPoffinName(row.Type);
            item.Level = Parse(row.Level);
            item.Taste = Parse(row.Taste);
            item.IsNew = row.IsNew;
            item.FlavorSpicy = Parse(row.Spicy);
            item.FlavorDry = Parse(row.Dry);
            item.FlavorSweet = Parse(row.Sweet);
            item.FlavorBitter = Parse(row.Bitter);
            item.FlavorSour = Parse(row.Sour);
        }
        SAV.Poffins.SetPoffins(AllItems);
        Close();

        static byte Parse(string text) => byte.TryParse(text, out var p) ? p : (byte)0;
    }

    private sealed class PoffinRow
    {
        public required int Index { get; init; }
        public required string Id { get; init; }
        public string Type { get; set; } = string.Empty;
        public string Level { get; set; } = "0";
        public string Taste { get; set; } = "0";
        public bool IsNew { get; set; }
        public string Spicy { get; set; } = "0";
        public string Dry { get; set; } = "0";
        public string Sweet { get; set; } = "0";
        public string Bitter { get; set; } = "0";
        public string Sour { get; set; } = "0";
    }
}
