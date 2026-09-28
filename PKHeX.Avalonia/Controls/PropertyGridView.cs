using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
/// Property names are translated with the <c>PropertyGrid.&lt;name&gt;</c> keys, categories with
/// <c>PropertyGrid.Category.&lt;name&gt;</c> and enum values with <c>&lt;EnumType&gt;.&lt;Value&gt;</c>, matching
/// <c>PropertyGridLocalization</c>. Rows are grouped by category and sorted alphabetically inside it, like the WinForms
/// grid's default <c>CategorizedAlphabetical</c> sort. Values are written back to the object as soon as they are edited
/// (WinForms behavior).
/// </remarks>
public sealed class PropertyGridView : ScrollViewer
{
    private readonly StackPanel Root = new() { Orientation = Orientation.Vertical, Spacing = 2, Margin = new Thickness(4) };

    /// <summary>
    /// Objects on the path from the displayed object down to the row being built.
    /// </summary>
    /// <remarks>
    /// The WinForms grid expands a nested object only when the user clicks it, so a cyclic object graph is harmless
    /// there. Expanding inline has to stop by itself: a save file reaches itself again through its blocks, which
    /// recursed until the stack overflowed (Block Data on a Generation 1 save).
    /// </remarks>
    private readonly List<object> Ancestors = [];

    /// <summary>How many levels of nested objects are expanded inline.</summary>
    private const int MaxDepth = 3;

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
    /// Raised when the row under the pointer (or holding the focus) changes; the payload feeds a description pane like
    /// the one the WinForms <c>PropertyGrid</c> draws at its bottom.
    /// </summary>
    public event Action<PropertyHelp?>? HelpChanged;

    /// <summary>Name and description of the property a description pane should show.</summary>
    public sealed record PropertyHelp(string Name, string Description);

    /// <summary>
    /// Displays the editable properties of <paramref name="obj"/>.
    /// </summary>
    public void SetObject(object? obj)
    {
        SelectedObject = obj;
        Root.Children.Clear();
        HelpChanged?.Invoke(null);
        Ancestors.Clear();
        if (obj is null)
            return;
        Ancestors.Add(obj);
        AddProperties(Root, obj, 0, null);
    }

    /// <param name="onChanged">Invoked after a child value is written; used to store an edited boxed struct back into its owner.</param>
    private void AddProperties(Panel panel, object obj, int depth, Action? onChanged)
    {
        var rows = new List<(string Category, string Label, Control Editor, string Description)>();
        var type = obj.GetType();
        foreach (var pi in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!pi.CanRead || pi.GetIndexParameters().Length != 0)
                continue;
            if (pi.GetCustomAttribute<BrowsableAttribute>() is { Browsable: false })
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

            var editor = CreateEditor(obj, pi, value, depth, onChanged);
            if (editor is null)
                continue;
            var label = Translate($"PropertyGrid.{pi.Name}", pi.Name);
            var category = pi.GetCustomAttribute<CategoryAttribute>()?.Category ?? CategoryAttribute.Default.Category ?? "Misc";
            var description = pi.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
            rows.Add((Translate($"PropertyGrid.Category.{category}", category), label, editor, description));
        }

        foreach (var group in rows.GroupBy(z => z.Category).OrderBy(z => z.Key, StringComparer.CurrentCulture))
        {
            var body = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
            foreach (var row in group.OrderBy(z => z.Label, StringComparer.CurrentCulture))
                body.Children.Add(CreateRow(row.Label, row.Editor, row.Description, depth));
            panel.Children.Add(new Expander
            {
                Header = group.Key,
                Content = body,
                IsExpanded = true,
                Padding = new Thickness(4, 2),
                Margin = new Thickness(0, 2, 0, 2),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            });
        }
    }

    /// <summary>True when the object is already being expanded further up the current path.</summary>
    private bool IsAncestor(object value)
    {
        foreach (var ancestor in Ancestors)
        {
            if (ReferenceEquals(ancestor, value))
                return true;
        }
        return false;
    }

    private static string Translate(string key, string fallback) => Translator.TranslateText(key, fallback, MainWindow.CurrentLanguage);

    private Control CreateRow(string label, Control editor, string description, int depth)
    {
        var grid = new Grid { ColumnSpacing = 8, Margin = new Thickness(depth * 12, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(3, GridUnitType.Star)));
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        grid.Children.Add(text);
        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);
        if (description.Length == 0)
            return grid;

        var help = new PropertyHelp(label, description);
        grid.PointerEntered += (_, _) => HelpChanged?.Invoke(help);
        grid.AddHandler(GotFocusEvent, (_, _) => HelpChanged?.Invoke(help), RoutingStrategies.Bubble);
        ToolTip.SetTip(text, description);
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
        if (type == typeof(System.Drawing.Point))
            return CreatePointEditor(owner, pi, (System.Drawing.Point)value, writable, onChanged);
        if (IsNumeric(type))
        {
            bool fractional = IsFractional(type);
            var nud = new NumericUpDown
            {
                Value = Convert.ToDecimal(value, CultureInfo.InvariantCulture),
                Minimum = GetMin(type),
                Maximum = GetMax(type),
                // The float settings (sprite filter opacity/greyscale) are fractions; an integer step would round them away.
                Increment = fractional ? 0.05m : 1m,
                FormatString = fractional ? "0.####" : "0",
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
        if (value is ICollection collection)
            return CreateCollectionEditor(owner, pi, collection, writable, onChanged);

        // Nested settings object: expand inline (the WinForms grid expands all items).
        if (type.Namespace?.StartsWith("PKHeX.", StringComparison.Ordinal) == true && (type.IsClass || type.IsValueType))
        {
            // Value types are edited through their box; write the box back to the owner after each change.
            var box = value;
            if (depth >= MaxDepth || IsAncestor(box))
                return null; // already on this path, or too deep: stop instead of recursing forever
            var inner = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
            Action? notify = type.IsValueType ? () => { Set(owner, pi, box, onChanged); } : onChanged;
            Ancestors.Add(box);
            try
            {
                AddProperties(inner, box, depth + 1, notify);
            }
            finally
            {
                Ancestors.RemoveAt(Ancestors.Count - 1);
            }
            if (inner.Children.Count == 0)
                return null;
            return inner;
        }
        return null;
    }

    /// <summary>
    /// Editor for <see cref="System.Drawing.Point"/> settings (<c>Hover.PreviewCursorShift</c>), shown as "X, Y" like
    /// the WinForms grid.
    /// </summary>
    private Control CreatePointEditor(object owner, PropertyInfo pi, System.Drawing.Point value, bool writable, Action? onChanged)
    {
        var tb = new TextBox
        {
            Text = $"{value.X}, {value.Y}",
            MinHeight = 0,
            IsEnabled = writable,
            Padding = new Thickness(4, 2),
        };
        tb.OnTextChanged(_ =>
        {
            var parts = (tb.Text ?? string.Empty).Split(',');
            if (parts.Length != 2)
                return;
            if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var x))
                return;
            if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var y))
                return;
            Set(owner, pi, new System.Drawing.Point(x, y), onChanged);
        });
        return tb;
    }

    /// <summary>
    /// Editor for the list settings (recent files, backup paths, report properties, battle template token order).
    /// The WinForms grid opens a modal collection editor; here the entries are edited in place, one per line.
    /// </summary>
    private Control CreateCollectionEditor(object owner, PropertyInfo pi, ICollection value, bool writable, Action? onChanged)
    {
        var element = GetElementType(pi.PropertyType);
        if (element is null || !(element == typeof(string) || element.IsEnum) || !writable)
            return new TextBlock { Text = Translate("PropertyGrid.Value.Collection", "(Collection)"), VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray };

        var entries = value.Cast<object?>().Select(z => z?.ToString() ?? string.Empty).ToArray();
        var tb = new TextBox
        {
            Text = string.Join(Environment.NewLine, entries),
            AcceptsReturn = true,
            MinHeight = 56,
            MaxHeight = 140,
            Padding = new Thickness(4, 2),
            TextWrapping = TextWrapping.NoWrap,
        };
        ToolTip.SetTip(tb, Translate("PropertyGrid.Value.CollectionHint", "One entry per line."));
        tb.OnTextChanged(_ =>
        {
            if (!TryBuildCollection(pi.PropertyType, element, tb.Text, out var result))
                return;
            Set(owner, pi, result, onChanged);
        });
        return tb;
    }

    private static Type? GetElementType(Type type)
    {
        if (type.IsArray)
            return type.GetElementType();
        foreach (var i in type.GetInterfaces())
        {
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>))
                return i.GetGenericArguments()[0];
        }
        return null;
    }

    private static bool TryBuildCollection(Type type, Type element, string? text, out object result)
    {
        result = null!;
        var lines = (text ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new List<object>(lines.Length);
        foreach (var line in lines)
        {
            if (element == typeof(string))
            {
                values.Add(line);
            }
            else if (Enum.TryParse(element, line, true, out var parsed) && parsed is not null)
            {
                values.Add(parsed);
            }
            else
            {
                return false; // unfinished input: keep the previous value
            }
        }

        if (type.IsArray)
        {
            var array = Array.CreateInstance(element, values.Count);
            for (int i = 0; i < values.Count; i++)
                array.SetValue(values[i], i);
            result = array;
            return true;
        }

        var listType = typeof(List<>).MakeGenericType(element);
        if (!type.IsAssignableFrom(listType))
            return false;
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var value in values)
            list.Add(value);
        result = list;
        return true;
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

    private static bool IsFractional(Type t) => Type.GetTypeCode(t) is TypeCode.Single or TypeCode.Double or TypeCode.Decimal;

    private static decimal GetMin(Type t) => Type.GetTypeCode(t) switch
    {
        TypeCode.Byte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 => 0,
        TypeCode.SByte => sbyte.MinValue,
        TypeCode.Int16 => short.MinValue,
        TypeCode.Single or TypeCode.Double or TypeCode.Decimal => decimal.MinValue,
        _ => int.MinValue,
    };

    private static decimal GetMax(Type t) => Type.GetTypeCode(t) switch
    {
        TypeCode.Byte => byte.MaxValue,
        TypeCode.SByte => sbyte.MaxValue,
        TypeCode.Int16 => short.MaxValue,
        TypeCode.UInt16 => ushort.MaxValue,
        TypeCode.Single or TypeCode.Double or TypeCode.Decimal => decimal.MaxValue,
        _ => int.MaxValue,
    };
}
