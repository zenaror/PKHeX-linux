using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PKHeX.Avalonia.Controls;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen9.EventWork;

/// <summary>
/// Shared plumbing for the flag/work grids: a search box over a grid (port of <c>EventWorkGridBase</c>).
/// </summary>
/// <remarks>
/// Avalonia's <see cref="DataGrid"/> has no per-row visibility, so the filter swaps the bound collection
/// instead of hiding rows. The full row list is kept separately so saving still walks every entry.
/// </remarks>
public abstract class EventWorkGridBase<TRow> : IEventWorkGrid where TRow : class
{
    private static readonly FontFamily Mono = new("Courier New, monospace");

    private readonly DispatcherTimer _searchDebounce = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly TextBox Search;
    protected readonly DataGrid Grid;
    protected readonly List<TRow> Rows = [];

    protected EventWorkGridBase(ContentControl host)
    {
        Search = new TextBox { Name = "TB_Search", PlaceholderText = "Search...", Margin = new global::Avalonia.Thickness(0, 0, 0, 4) };
        Grid = new DataGrid
        {
            Name = "DGV_Entries",
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            CanUserReorderColumns = true,
            CanUserResizeColumns = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            FontFamily = Mono,
            FontSize = 12,
            // Without a cap the grid sizes to all of its rows and pushes the button bar off the screen.
            MaxHeight = 560,
        };

        var panel = new DockPanel();
        DockPanel.SetDock(Search, Dock.Top);
        panel.Children.Add(Search);
        panel.Children.Add(Grid);
        host.Content = panel;

        _searchDebounce.Tick += (_, _) => { _searchDebounce.Stop(); ApplyFilter(Search.Text ?? string.Empty); };
        Search.OnTextChanged(_ => { _searchDebounce.Stop(); _searchDebounce.Start(); });
    }

    /// <summary>Rebinds the grid to the rows whose text matches, or to everything when the box is empty.</summary>
    private void ApplyFilter(string text)
    {
        if (text.Length == 0)
        {
            Grid.ItemsSource = Rows;
            return;
        }
        Grid.ItemsSource = Rows.Where(r => Matches(r, text)).ToList();
    }

    protected void Rebind() => Grid.ItemsSource = Rows.ToList();

    protected abstract bool Matches(TRow row, string text);

    protected static DataGridTextColumn IndexColumn()
        => new() { Header = "Index", Binding = new global::Avalonia.Data.Binding("Index"), IsReadOnly = true, Width = new DataGridLength(85) };

    /// <summary>Pass a width of 0 to let the column take the remaining space, as the WinForms Fill mode does.</summary>
    protected static DataGridTextColumn TextColumn(string header, string path, double width, bool readOnly = false)
        => new()
        {
            Header = header,
            Binding = new global::Avalonia.Data.Binding(path) { Mode = readOnly ? global::Avalonia.Data.BindingMode.OneWay : global::Avalonia.Data.BindingMode.TwoWay },
            IsReadOnly = readOnly,
            Width = width > 0 ? new DataGridLength(width) : new DataGridLength(1, DataGridLengthUnitType.Star),
        };

    protected static DataGridTemplateColumn BoolColumn(string header, string path, double width)
        => DataGridUtil.CheckColumn(header, path, width);

    public abstract void Load();
    public abstract void Save();
}

/// <summary>Base row with an index and property change notification.</summary>
public abstract class EventWorkRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public required int Index { get; init; }

    protected void Set<T>(ref T field, T value, string name)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
