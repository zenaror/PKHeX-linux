using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Entity summary of a save file, one row per stored Pokémon (port of the WinForms <c>ReportGrid</c>).
/// </summary>
public sealed class ReportGridWindow : Window
{
    public IPropertyProvider<PKM> PropertyProvider { get; init; } =
        new BatchPropertyProvider<EntityBatchEditor, PKM>(EntityBatchEditor.Instance);

    private readonly DataGrid dgData = new()
    {
        Name = "dgData",
        AutoGenerateColumns = false,
        IsReadOnly = true,
        CanUserSortColumns = true,
        CanUserReorderColumns = true,
        CanUserResizeColumns = true,
        RowHeight = 34,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        SelectionMode = DataGridSelectionMode.Extended,
    };

    private readonly ObservableCollection<EntitySummaryRow> Rows = [];
    private readonly List<global::Avalonia.Media.Imaging.Bitmap> Sprites = [];

    public ReportGridWindow()
    {
        Name = "ReportGrid";
        Title = "Box Data Report";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 1000;
        Height = 600;
        Content = dgData;

        var menu = new ContextMenu();
        var mnuHide = new MenuItem { Name = "mnuHide", Header = MsgReportColumnHide };
        mnuHide.Click += async (_, _) => await HideSelectedColumn();
        var mnuRestore = new MenuItem { Name = "mnuRestore", Header = MsgReportColumnRestore };
        mnuRestore.Click += async (_, _) => await RestoreColumns();
        menu.Items.Add(mnuHide);
        menu.Items.Add(mnuRestore);
        dgData.ContextMenu = menu;

        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.C && e.KeyModifiers == KeyModifiers.Control)
                _ = CopyToClipboard();
        }, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);

        Closing += (_, e) => { if (!closing) { e.Cancel = true; _ = PromptSaveCSV(); } };
        Closed += (_, _) =>
        {
            foreach (var s in Sprites)
                s.Dispose();
            Sprites.Clear();
        };
    }

    private bool closing;

    public void PopulateData(IReadOnlyList<SlotCache> data) => PopulateData(data, [], []);

    public void PopulateData(IReadOnlyList<SlotCache> data, ReadOnlySpan<string> extra, ReadOnlySpan<string> hide)
    {
        var strings = GameInfo.Strings;
        foreach (var entry in data)
        {
            var pk = entry.Entity;
            if (pk.Species - 1u >= pk.MaxSpeciesID)
                continue;
            pk.Stat_Level = pk.CurrentLevel; // recalc Level
            var sprite = pk.Sprite().ToAvaloniaBitmapAndDispose();
            Sprites.Add(sprite);
            Rows.Add(new EntitySummaryRow(pk, strings, entry.Identify(), sprite));
        }

        CreateColumns(hide);
        if (extra.Length != 0)
            AddExtraColumns(extra);
        dgData.ItemsSource = Rows;
    }

    private void CreateColumns(ReadOnlySpan<string> hide)
    {
        var sprite = new DataGridTemplateColumn
        {
            Header = "Sprite",
            CellTemplate = new FuncDataTemplate<EntitySummaryRow>((_, _) =>
            {
                var img = new Image { Width = 34, Height = 30, Stretch = Stretch.Uniform };
                img.Bind(Image.SourceProperty, new Binding(nameof(EntitySummaryRow.Sprite)));
                return img;
            }),
            CanUserSort = false,
        };
        dgData.Columns.Add(sprite);

        foreach (var pi in GetSummaryProperties())
        {
            var col = new DataGridTextColumn
            {
                Header = pi.Name,
                Binding = new Binding(pi.Name),
                IsVisible = !Contains(hide, pi.Name),
            };
            dgData.Columns.Add(col);
        }
    }

    private static bool Contains(ReadOnlySpan<string> list, string value)
    {
        foreach (var x in list)
        {
            if (x == value)
                return true;
        }
        return false;
    }

    private static IEnumerable<PropertyInfo> GetSummaryProperties() => typeof(EntitySummaryRow)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(z => z.CanRead && z.PropertyType == typeof(string));

    private void AddExtraColumns(ReadOnlySpan<string> extra)
    {
        var rent = ArrayPool<string>.Shared.Rent(Rows.Count);
        var span = rent.AsSpan(0, Rows.Count);
        foreach (var prop in extra)
        {
            if (prop.Length == 0)
                continue;
            span.Clear();
            bool any = false;
            for (int i = 0; i < Rows.Count; i++)
            {
                var pk = Rows[i].Entity;
                if (!TryGetCustomCell(pk, prop, out var str))
                    continue;
                span[i] = str;
                any = true;
            }

            if (!any)
                continue;

            for (int i = 0; i < Rows.Count; i++)
                Rows[i].Extra[prop] = span[i] ?? string.Empty;

            var col = new DataGridTextColumn
            {
                Header = prop,
                Binding = new Binding($"Extra[{prop}]"),
            };
            dgData.Columns.Add(col);
        }
        ArrayPool<string>.Shared.Return(rent, true);
    }

    private bool TryGetCustomCell(PKM pk, string prop, [NotNullWhen(true)] out string? result)
        => PropertyProvider.TryGetProperty(pk, prop, out result);

    private async Task HideSelectedColumn()
    {
        var col = dgData.CurrentColumn;
        if (col is null)
        {
            await AppDialogs.Alert(this, MsgReportColumnHideFail);
            return;
        }
        col.IsVisible = false;
    }

    private async Task RestoreColumns()
    {
        foreach (var col in dgData.Columns)
            col.IsVisible = true;
        await AppDialogs.Alert(this, MsgReportColumnRestoreSuccess);
    }

    private async Task PromptSaveCSV()
    {
        if (MainWindow.CurrentModifiers.HasFlag(KeyModifiers.Shift))
        {
            CloseForced();
            return;
        }
        if (await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgReportExportCSV) != DialogResult.Yes)
        {
            CloseForced();
            return;
        }

        var path = await FileDialogs.SaveFileDialog(this, "Spreadsheet|*.csv", "Box Data Dump.csv");
        if (path is not null)
            await Export_CSV(path);
        CloseForced();
    }

    private void CloseForced()
    {
        closing = true;
        Close();
    }

    private async Task Export_CSV(string path)
    {
        await using var fs = new FileStream(path, FileMode.Create);
        await using var s = new StreamWriter(fs, new UTF8Encoding(false));

        var columns = dgData.Columns.Skip(1).ToArray(); // skip the sprite column
        await s.WriteLineAsync(string.Join(",", columns.Select(z => $"\"{z.Header}\"")));

        foreach (var row in GetOrderedRows())
            await s.WriteLineAsync(string.Join(",", columns.Select(z => $"\"{GetCell(row, z)}\"")));
    }

    /// <summary>Rows in the order currently displayed (the grid's sort is applied to its collection view).</summary>
    private IEnumerable<EntitySummaryRow> GetOrderedRows()
    {
        if (dgData.CollectionView is { } view)
            return view.Cast<EntitySummaryRow>();
        return Rows;
    }

    private static string GetCell(EntitySummaryRow row, DataGridColumn column)
    {
        var name = column.Header as string ?? string.Empty;
        if (row.Extra.TryGetValue(name, out var extra))
            return extra;
        var pi = typeof(EntitySummaryRow).GetProperty(name);
        return pi?.GetValue(row)?.ToString() ?? string.Empty;
    }

    private async Task CopyToClipboard()
    {
        var columns = dgData.Columns.Skip(1).Where(z => z.IsVisible).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join('\t', columns.Select(z => z.Header)));
        foreach (var row in GetOrderedRows())
            sb.AppendLine(string.Join('\t', columns.Select(z => GetCell(row, z))));

        var data = sb.ToString();
        var dr = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgReportExportTable);
        if (dr != DialogResult.Yes)
        {
            await ClipboardService.SetText(this, data);
            return;
        }

        // Reformat datagrid clipboard content
        string[] lines = data.Split(Environment.NewLine);
        string[] newlines = ConvertTabbedToRedditTable(lines);
        await ClipboardService.SetText(this, string.Join(Environment.NewLine, newlines));
    }

    private static string[] ConvertTabbedToRedditTable(ReadOnlySpan<string> lines)
    {
        string[] newlines = new string[lines.Length + 1];
        int tabcount = lines[0].Count('\t');

        newlines[0] = lines[0].Replace('\t', '|');
        newlines[1] = string.Join(":--:", Enumerable.Repeat('|', tabcount + 2)); // 2 pipes for each end
        for (int i = 1; i < lines.Length; i++)
            newlines[i + 1] = lines[i].Replace('\t', '|');
        return newlines;
    }
}

/// <summary>
/// Report row: entity summary plus the Avalonia sprite and any extra property columns.
/// </summary>
public sealed class EntitySummaryRow(PKM pk, GameStrings strings, string position, global::Avalonia.Media.Imaging.Bitmap? sprite)
    : EntitySummary(pk, strings)
{
    public global::Avalonia.Media.Imaging.Bitmap? Sprite { get; } = sprite;
    public override string Position { get; } = position;

    /// <summary>Values for the user-configured extra property columns.</summary>
    public Dictionary<string, string> Extra { get; } = [];
}
