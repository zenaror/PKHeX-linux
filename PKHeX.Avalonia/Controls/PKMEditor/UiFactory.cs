using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Small factory helpers to build editor layouts in code; public so plugins can build matching editors.
/// </summary>
public static class UiFactory
{
    public static TextBlock Label(string name, string text, bool clickable = false) => new()
    {
        Name = name,
        Text = text,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 6, 0),
        Cursor = clickable ? new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand) : null,
    };

    public static TextBlock Header(string name, string text) => new()
    {
        Name = name,
        Text = text,
        FontWeight = FontWeight.Bold,
        Margin = new Thickness(0, 6, 0, 2),
        Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand),
    };

    public static StackPanel Row(params Control[] children)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        foreach (var c in children)
            sp.Children.Add(c);
        return sp;
    }

    public static StackPanel Column(params Control[] children)
    {
        var sp = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
        foreach (var c in children)
            sp.Children.Add(c);
        return sp;
    }

    public static ComboBox Combo(string name, double minWidth = 140)
    {
        var cb = new ComboBox { Name = name, MinWidth = minWidth, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 2) };
        cb.InitializeBinding();
        return cb;
    }

    public static ComboBox StringCombo(string name, double minWidth = 60, params string[] items)
    {
        var cb = new ComboBox { Name = name, MinWidth = minWidth, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 2) };
        foreach (var item in items)
            cb.Items.Add(item);
        return cb;
    }

    public static NumericTextBox Numeric(string name, int maxLength, double width = 48, bool hex = false) => new()
    {
        Name = name,
        MaxLength = maxLength,
        Width = width,
        IsHex = hex,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static TextBox Text(string name, int maxLength, double width = 120) => new()
    {
        Name = name,
        MaxLength = maxLength,
        Width = width,
        VerticalAlignment = VerticalAlignment.Center,
        Padding = new Thickness(4, 2),
    };

    public static CheckBox Check(string name, string text) => new()
    {
        Name = name,
        Content = text,
        VerticalAlignment = VerticalAlignment.Center,
        MinHeight = 0,
        Padding = new Thickness(4, 0, 0, 0),
    };

    public static Button Button(string name, string text) => new()
    {
        Name = name,
        Content = text,
        VerticalAlignment = VerticalAlignment.Center,
        Padding = new Thickness(8, 2),
        MinHeight = 0,
    };

    public static NumericUpDown NumericUpDown(string name, decimal min, decimal max, double width = 90) => new()
    {
        Name = name,
        Minimum = min,
        Maximum = max,
        Value = min < 0 ? 0 : min,
        Increment = 1,
        FormatString = "0",
        Width = width,
        MinHeight = 0,
        VerticalAlignment = VerticalAlignment.Center,
        Padding = new Thickness(4, 0),
    };

    public static Image Picture(string name, double size = 16) => new()
    {
        Name = name,
        Width = size,
        Height = size,
        Stretch = Stretch.None,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>
    /// Converts a date to the offset value an Avalonia <see cref="DatePicker"/> expects (kind-agnostic, zero offset).
    /// </summary>
    public static System.DateTimeOffset ToOffset(System.DateTime value)
        => new(System.DateTime.SpecifyKind(value.Date, System.DateTimeKind.Unspecified), System.TimeSpan.Zero);

    public static void SetRowCol(Control c, int row, int col, int colSpan = 1)
    {
        Grid.SetRow(c, row);
        Grid.SetColumn(c, col);
        if (colSpan > 1)
            Grid.SetColumnSpan(c, colSpan);
    }

    /// <summary>
    /// Two-column form grid (label | field) with the requested number of rows.
    /// </summary>
    public static Grid FormGrid(int rows)
    {
        var g = new Grid { ColumnSpacing = 6, RowSpacing = 3 };
        g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int i = 0; i < rows; i++)
            g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        return g;
    }

    public static void AddFormRow(Grid g, int row, Control? label, Control field, HorizontalAlignment labelAlign = HorizontalAlignment.Right)
    {
        if (label is not null)
        {
            label.HorizontalAlignment = labelAlign;
            SetRowCol(label, row, 0);
            g.Children.Add(label);
        }
        SetRowCol(field, row, 1);
        g.Children.Add(field);
    }
}
