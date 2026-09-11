using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Views;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Reflection driven property editor; replacement for the WinForms <c>PropertyGrid</c> used by the settings editor.
/// </summary>
/// <remarks>
/// Property names are translated with the <c>PropertyGrid.&lt;name&gt;</c> keys and enum values with
/// <c>&lt;EnumType&gt;.&lt;Value&gt;</c>, matching <c>PropertyGridLocalization</c>.
/// Values are written back to the object as soon as they are edited (WinForms behavior).
/// </remarks>
public sealed class PropertyGridView : ScrollViewer
{
    private readonly StackPanel Root = new() { Orientation = Orientation.Vertical, Spacing = 2, Margin = new Thickness(4) };

    public PropertyGridView()
    {
        Content = Root;
        HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
    }

    /// <summary>
    /// The object being displayed. A value type is held boxed, so edits made in the grid are visible here
    /// (the WinForms property grid behaves the same through its <c>SelectedObject</c>).
    /// </summary>
    public object? SelectedObject { get; private set; }

    /// <summary>Raised after an edit is written back to the displayed object.</summary>
    public event Action? PropertyValueChanged;

    /// <summary>
    /// Displays the editable properties of <paramref name="obj"/>.
    /// </summary>
    public void SetObject(object? obj)
    {
        SelectedObject = obj;
        Root.Children.Clear();
        if (obj is null)
            return;
        AddProperties(Root, obj, 0, null);
    }

    /// <param name="onChanged">Invoked after a child value is written; used to store an edited boxed struct back into its owner.</param>
    private void AddProperties(Panel panel, object obj, int depth, Action? onChanged)
    {
        var type = obj.GetType();
        foreach (var pi in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!pi.CanRead || pi.GetIndexParameters().Length != 0)
                continue;
            if (pi.GetCustomAttribute<System.ComponentModel.BrowsableAttribute>() is { Browsable: false })
                continue; // hidden from the WinForms property grid too
            object? value;
            try
            {
                value = pi.GetValue(obj);
            }
            catch
            {
                continue; // property threw; not editable
            }
            if (value is null)
                continue;

            var label = Translate($"PropertyGrid.{pi.Name}", pi.Name);
            var editor = CreateEditor(obj, pi, value, depth, onChanged);
            if (editor is null)
                continue;
            panel.Children.Add(CreateRow(label, editor, depth));
        }
    }

    private static string Translate(string key, string fallback) => Translator.TranslateText(key, fallback, MainWindow.CurrentLanguage);

    private static Control CreateRow(string label, Control editor, int depth)
    {
        var grid = new Grid { ColumnSpacing = 8, Margin = new Thickness(depth * 12, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(3, GridUnitType.Star)));
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        grid.Children.Add(text);
        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);
        return grid;
    }

    private Control? CreateEditor(object owner, PropertyInfo pi, object value, int depth, Action? onChanged)
    {
        var type = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
        bool writable = pi.CanWrite && pi.SetMethod?.IsPublic == true;

        if (type == typeof(bool))
        {
            var chk = new CheckBox { IsChecked = (bool)value, MinHeight = 0, IsEnabled = writable, Padding = new Thickness(4, 0, 0, 0) };
            chk.IsCheckedChanged += (_, _) => Set(owner, pi, chk.IsChecked == true, onChanged);
            return chk;
        }
        if (type.IsEnum)
            return CreateEnumEditor(owner, pi, type, value, writable, onChanged);
        if (type == typeof(string))
        {
            var tb = new TextBox { Text = (string)value, MinHeight = 0, IsEnabled = writable, Padding = new Thickness(4, 2) };
            tb.OnTextChanged(_ => Set(owner, pi, tb.Text ?? string.Empty, onChanged));
            return tb;
        }
        if (type == typeof(System.Drawing.Color))
        {
            var color = (System.Drawing.Color)value;
            var tb = new TextBox { Text = ToHex(color), MinHeight = 0, IsEnabled = writable, Padding = new Thickness(4, 2) };
            var swatch = new Border { Width = 20, Height = 20, Background = color.ToBrush(), BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray };
            tb.OnTextChanged(_ =>
            {
                if (!TryParseColor(tb.Text, out var parsed))
                    return;
                swatch.Background = parsed.ToBrush();
                Set(owner, pi, parsed, onChanged);
            });
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            row.Children.Add(swatch);
            tb.Width = 100;
            row.Children.Add(tb);
            return row;
        }
        if (IsNumeric(type))
        {
            var nud = new NumericUpDown
            {
                Value = Convert.ToDecimal(value, CultureInfo.InvariantCulture),
                Minimum = GetMin(type),
                Maximum = GetMax(type),
                Increment = 1,
                FormatString = "0",
                MinHeight = 0,
                IsEnabled = writable,
                Padding = new Thickness(4, 0),
            };
            nud.ValueChanged += (_, _) =>
            {
                if (nud.Value is not { } d)
                    return;
                Set(owner, pi, Convert.ChangeType(d, type, CultureInfo.InvariantCulture), onChanged);
            };
            return nud;
        }
        if (value is ICollection)
            return new TextBlock { Text = Translate("PropertyGrid.Value.Collection", "(Collection)"), VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray };

        // Nested settings object: expand inline (the WinForms grid expands all items).
        if (type.Namespace?.StartsWith("PKHeX.", StringComparison.Ordinal) == true && (type.IsClass || type.IsValueType))
        {
            // Value types are edited through their box; write the box back to the owner after each change.
            var box = value;
            var inner = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
            Action? notify = type.IsValueType ? () => { Set(owner, pi, box, onChanged); } : onChanged;
            AddProperties(inner, box, depth + 1, notify);
            if (inner.Children.Count == 0)
                return null;
            return new Expander { Header = string.Empty, Content = inner, IsExpanded = true, Padding = new Thickness(4, 2) };
        }
        return null;
    }

    private Control CreateEnumEditor(object owner, PropertyInfo pi, Type type, object value, bool writable, Action? onChanged)
    {
        bool isFlags = type.GetCustomAttribute<FlagsAttribute>() is not null;
        if (isFlags)
        {
            // Flag combinations are edited as text (comma separated), matching the WinForms grid's string round-trip.
            var tb = new TextBox { Text = value.ToString(), MinHeight = 0, IsEnabled = writable, Padding = new Thickness(4, 2) };
            tb.OnTextChanged(_ =>
            {
                if (Enum.TryParse(type, tb.Text, true, out var parsed) && parsed is not null)
                    Set(owner, pi, parsed, onChanged);
            });
            return tb;
        }

        var cb = new ComboBox { MinHeight = 0, IsEnabled = writable, Padding = new Thickness(6, 2), HorizontalAlignment = HorizontalAlignment.Stretch };
        var values = Enum.GetValues(type).Cast<object>().ToArray();
        var display = values.Select(z => TranslateEnum(type, z)).ToArray();
        cb.ItemsSource = display;
        cb.SelectedIndex = Math.Max(0, Array.IndexOf(values, value));
        cb.SelectionChanged += (_, _) =>
        {
            var index = cb.SelectedIndex;
            if ((uint)index < values.Length)
                Set(owner, pi, values[index], onChanged);
        };
        return cb;
    }

    private static string TranslateEnum(Type type, object value)
    {
        var name = value.ToString() ?? string.Empty;
        return Translate($"{type.Name}.{name}", name);
    }

    private void Set(object owner, PropertyInfo pi, object value, Action? onChanged)
    {
        if (!pi.CanWrite)
            return;
        try
        {
            pi.SetValue(owner, value);
            onChanged?.Invoke();
            PropertyValueChanged?.Invoke();
        }
        catch (Exception ex) when (ex is ArgumentException or TargetInvocationException)
        {
            // Invalid input for the property; keep the previous value (WinForms grid rejects it too).
        }
    }

    private static string ToHex(System.Drawing.Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    private static bool TryParseColor(string? text, out System.Drawing.Color color)
    {
        color = System.Drawing.Color.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var span = text.AsSpan().Trim().TrimStart('#');
        if (span.Length != 8 || !uint.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
            return false;
        color = System.Drawing.Color.FromArgb((int)argb);
        return true;
    }

    private static bool IsNumeric(Type t) => Type.GetTypeCode(t) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
        or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;

    private static decimal GetMin(Type t) => Type.GetTypeCode(t) switch
    {
        TypeCode.Byte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 => 0,
        TypeCode.SByte => sbyte.MinValue,
        TypeCode.Int16 => short.MinValue,
        _ => int.MinValue,
    };

    private static decimal GetMax(Type t) => Type.GetTypeCode(t) switch
    {
        TypeCode.Byte => byte.MaxValue,
        TypeCode.SByte => sbyte.MaxValue,
        TypeCode.Int16 => short.MaxValue,
        TypeCode.UInt16 => ushort.MaxValue,
        _ => int.MaxValue,
    };
}
