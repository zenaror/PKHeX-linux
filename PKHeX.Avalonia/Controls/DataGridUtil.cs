using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Column helpers for <see cref="DataGrid"/>; Avalonia's grid has no combo box column.
/// </summary>
public static class DataGridUtil
{
    /// <summary>
    /// Creates a column whose cells are a <see cref="ComboBox"/> of <see cref="ComboItem"/> values,
    /// two-way bound to the integer property <paramref name="valuePath"/> of the row.
    /// </summary>
    public static DataGridTemplateColumn ComboColumn(string header, IReadOnlyList<ComboItem> items, string valuePath, double width = 130)
    {
        var template = new FuncDataTemplate<object>((_, _) =>
        {
            var cb = new ComboBox { MinHeight = 0, Padding = new Thickness(4, 0), HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch };
            cb.InitializeBinding();
            cb.SetItems(items);
            cb.Bind(SelectingItemsControl.SelectedValueProperty, new Binding(valuePath) { Mode = BindingMode.TwoWay });
            cb.SelectedValueBinding = new Binding(nameof(ComboItem.Value));
            return cb;
        });
        return new DataGridTemplateColumn
        {
            Header = header,
            CellTemplate = template,
            CellEditingTemplate = template,
            Width = new DataGridLength(width),
        };
    }

    /// <summary>
    /// Creates a column whose cells are a <see cref="ComboBox"/> of plain strings, two-way bound to
    /// the string property <paramref name="valuePath"/> of the row.
    /// </summary>
    public static DataGridTemplateColumn StringComboColumn(string header, IReadOnlyList<string> items, string valuePath, double width = 160)
    {
        var template = new FuncDataTemplate<object>((_, _) =>
        {
            var cb = new ComboBox { MinHeight = 0, Padding = new Thickness(4, 0), ItemsSource = items, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch };
            cb.Bind(SelectingItemsControl.SelectedItemProperty, new Binding(valuePath) { Mode = BindingMode.TwoWay });
            return cb;
        });
        return new DataGridTemplateColumn
        {
            Header = header,
            CellTemplate = template,
            CellEditingTemplate = template,
            Width = new DataGridLength(width),
        };
    }
}
