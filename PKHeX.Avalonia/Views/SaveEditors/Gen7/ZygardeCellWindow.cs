using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Data;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen7;

/// <summary>
/// Zygarde cell / sticker editor for Generation 7 saves (port of the WinForms <c>SAV_ZygardeCell</c>).
/// </summary>
/// <remarks>
/// Each cell has three states: not present in the world, waiting to be picked up, or already collected.
/// The counters above the grid are stored separately from the per-cell states, so both are edited here.
/// </remarks>
public sealed class ZygardeCellWindow : SaveEditorWindow
{
    private static readonly string[] States = ["None", "Available", "Received"];

    private readonly SaveFile Origin;
    private readonly SAV7 SAV;
    private readonly ObservableCollection<CellRow> Rows = [];

    private readonly NumericUpDown NUD_CellsTotal = UiFactory.NumericUpDown("NUD_CellsTotal", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_CellsCollected = UiFactory.NumericUpDown("NUD_CellsCollected", 0, ushort.MaxValue, 110);
    private readonly Button B_GiveAll = UiFactory.Button("B_GiveAll", "Collect All");
    private readonly DataGrid dgv = new()
    {
        Name = "dgv",
        AutoGenerateColumns = false,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        CanUserSortColumns = false,
        Height = 460,
        Width = 520,
    };

    public ZygardeCellWindow(SAV7 sav) : base("SAV_ZygardeCell", "Zygarde Cells")
    {
        SAV = (SAV7)(Origin = sav).Clone();
        var ew = SAV.EventWork;

        dgv.Columns.Add(new DataGridTextColumn { Header = "#", Binding = new Binding(nameof(CellRow.Index)), IsReadOnly = true, Width = new DataGridLength(50) });
        dgv.Columns.Add(new DataGridTextColumn { Header = "Location", Binding = new Binding(nameof(CellRow.Location)), IsReadOnly = true, Width = new DataGridLength(320) });
        dgv.Columns.Add(DataGridUtil.StringComboColumn("State", States, nameof(CellRow.State), 130));
        dgv.ItemsSource = Rows;

        var top = UiFactory.FormGrid(2);
        UiFactory.AddFormRow(top, 0, UiFactory.Label("L_CellsTotal", "Total:"), NUD_CellsTotal);
        UiFactory.AddFormRow(top, 1, UiFactory.Label("L_CellsCollected", "Collected:"), NUD_CellsCollected);
        SetBody(UiFactory.Column(top, B_GiveAll, dgv));

        NUD_CellsTotal.Value = ew.ZygardeCellTotal;
        NUD_CellsCollected.Value = ew.ZygardeCellCount;

        var locations = SAV is SAV7SM ? ZygardeCellLocations.SM : ZygardeCellLocations.USUM;
        for (int i = 0; i < ew.TotalZygardeCellCount; i++)
        {
            var cell = ew.GetZygardeCell(i);
            if (cell > 2)
                throw new IndexOutOfRangeException("Unable to find cell index.");
            Rows.Add(new CellRow
            {
                Index = (i + 1).ToString(),
                Location = i < locations.Length ? locations[i] : $"#{i + 1}",
                State = States[cell],
            });
        }

        B_GiveAll.Click += (_, _) => ClickGiveAll();
    }

    private void ClickGiveAll()
    {
        int added = 0;
        foreach (var row in Rows)
        {
            if (row.State != States[2])
                added++;
            row.State = States[2];
        }
        // Rebind so the grid picks up the new values (the rows are plain objects).
        dgv.ItemsSource = null;
        dgv.ItemsSource = Rows;

        NUD_CellsCollected.Value += added;
        if (SAV is not SAV7USUM)
            NUD_CellsTotal.Value += added;
    }

    protected override void OnSave()
    {
        var ew = SAV.EventWork;
        for (int i = 0; i < Rows.Count; i++)
        {
            var val = Array.IndexOf(States, Rows[i].State);
            if (val < 0)
                throw new IndexOutOfRangeException("Unable to find cell index.");
            ew.SetZygardeCell(i, (ushort)val);
        }

        ew.ZygardeCellTotal = (ushort)(NUD_CellsTotal.Value ?? 0);
        ew.ZygardeCellCount = (ushort)(NUD_CellsCollected.Value ?? 0);
        if (SAV is SAV7USUM)
            SAV.SetRecord(72, (int)(NUD_CellsCollected.Value ?? 0));

        Origin.CopyChangesFrom(SAV);
        Close();
    }

    private sealed class CellRow
    {
        public required string Index { get; init; }
        public required string Location { get; init; }
        public string State { get; set; } = string.Empty;
    }
}
