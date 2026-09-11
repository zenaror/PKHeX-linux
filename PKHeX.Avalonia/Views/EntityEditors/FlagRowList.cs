using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Core;
using PKHeX.Drawing.Misc;

namespace PKHeX.Avalonia.Views.EntityEditors;

/// <summary>
/// Row of a move-flag grid (index, type icon, move name and up to two flags).
/// </summary>
public sealed class FlagRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private bool hasFlag, hasFlag2;

    /// <summary>Record index used by the flag getter/setter.</summary>
    public int RecordIndex { get; init; }
    public string IndexText { get; init; } = string.Empty;
    public byte Type { get; init; }
    public string Name { get; init; } = string.Empty;
    /// <summary>type -> valid -> name sort key (WinForms hidden column).</summary>
    public string SortKey { get; init; } = string.Empty;
    public IBrush FlagBrush { get; init; } = Brushes.Transparent;
    public IBrush NameBrush { get; init; } = Brushes.Transparent;
    public global::Avalonia.Media.Imaging.Bitmap? TypeIcon { get; init; }

    public bool HasFlag { get => hasFlag; set => Set(ref hasFlag, value); }
    public bool HasFlag2 { get => hasFlag2; set => Set(ref hasFlag2, value); }

    private void Set(ref bool field, bool value, [CallerMemberName] string? name = null)
    {
        if (field == value)
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>
/// Header + virtualized list of <see cref="FlagRow"/> entries (replacement for the WinForms record <c>DataGridView</c>s).
/// </summary>
public sealed class FlagRowList : DockPanel
{
    public readonly ObservableCollection<FlagRow> Rows = [];
    private readonly ListBox List;
    private readonly bool FlagsFirst;
    private readonly string? Flag2Header;
    private bool sortDescending;

    private const double WidthCheck = 44, WidthIndex = 44, WidthType = 36, WidthName = 150, WidthFlag = 80;

    /// <param name="flagHeader">Header of the first flag column.</param>
    /// <param name="flag2Header">Header of the optional second flag column.</param>
    /// <param name="flagsFirst">Place the flag column before the index (TR/Plus editors) instead of after the name (Move Shop).</param>
    public FlagRowList(string flagHeader, string? flag2Header = null, bool flagsFirst = true)
    {
        FlagsFirst = flagsFirst;
        Flag2Header = flag2Header;
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 4, 0, 2) };
        if (flagsFirst)
            header.Children.Add(HeaderCell(flagHeader, WidthCheck, () => SortBy(z => z.HasFlag ? 0 : 1)));
        header.Children.Add(HeaderCell("Index", WidthIndex, () => SortBy(z => z.RecordIndex)));
        header.Children.Add(HeaderCell("Type", WidthType, () => SortBy(z => z.SortKey)));
        header.Children.Add(HeaderCell("Move", WidthName, () => SortBy(z => z.Name)));
        if (!flagsFirst)
        {
            header.Children.Add(HeaderCell(flagHeader, WidthFlag, () => SortBy(z => z.HasFlag ? 0 : 1)));
            if (flag2Header is not null)
                header.Children.Add(HeaderCell(flag2Header, WidthFlag, () => SortBy(z => z.HasFlag2 ? 0 : 1)));
        }
        SetDock(header, Dock.Top);
        Children.Add(header);

        List = new ListBox
        {
            ItemsSource = Rows,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new FuncDataTemplate<FlagRow>((_, _) => BuildRow()),
        };
        List.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(4, 1)), new Setter(MinHeightProperty, 0d) },
        });
        // Space toggles the first flag of the selected row (WinForms PressKeyCell).
        List.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key != Key.Space || List.SelectedItem is not FlagRow row)
                return;
            row.HasFlag = !row.HasFlag;
            e.Handled = true;
        }, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Children.Add(List);
    }

    private void SortBy<T>(Func<FlagRow, T> key) where T : IComparable
    {
        var sorted = (sortDescending ? Rows.OrderByDescending(key) : Rows.OrderBy(key)).ToArray();
        sortDescending = !sortDescending;
        Rows.Clear();
        foreach (var r in sorted)
            Rows.Add(r);
    }

    private static TextBlock HeaderCell(string text, double width, Action onClick)
    {
        var tb = new TextBlock { Text = text, Width = width, FontWeight = FontWeight.Bold, TextAlignment = TextAlignment.Center, Cursor = new Cursor(StandardCursorType.Hand) };
        tb.AttachClick(_ => onClick());
        return tb;
    }

    private Control BuildRow()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        if (FlagsFirst)
            panel.Children.Add(Check(nameof(FlagRow.HasFlag), WidthCheck, true));

        var index = new TextBlock { Width = WidthIndex, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        index.Bind(TextBlock.TextProperty, new Binding(nameof(FlagRow.IndexText)));
        panel.Children.Add(index);

        var icon = new Image { Width = WidthType, Height = 16, Stretch = Stretch.None };
        icon.Bind(Image.SourceProperty, new Binding(nameof(FlagRow.TypeIcon)));
        panel.Children.Add(icon);

        var name = new TextBlock { Width = WidthName, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 2) };
        name.Bind(TextBlock.TextProperty, new Binding(nameof(FlagRow.Name)));
        var nameBorder = new Border { Child = name };
        nameBorder.Bind(Border.BackgroundProperty, new Binding(nameof(FlagRow.NameBrush)));
        panel.Children.Add(nameBorder);

        if (!FlagsFirst)
        {
            panel.Children.Add(Check(nameof(FlagRow.HasFlag), WidthFlag, true));
            if (Flag2Header is not null)
                panel.Children.Add(Check(nameof(FlagRow.HasFlag2), WidthFlag, false));
        }
        return panel;
    }

    private static Control Check(string property, double width, bool colored)
    {
        var chk = new CheckBox { MinHeight = 0, HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(12, 0, 0, 0) };
        chk.Bind(ToggleButton.IsCheckedProperty, new Binding(property) { Mode = BindingMode.TwoWay });
        var border = new Border { Child = chk, Width = width };
        if (colored)
            border.Bind(Border.BackgroundProperty, new Binding(nameof(FlagRow.FlagBrush)));
        return border;
    }

    private static readonly Dictionary<byte, global::Avalonia.Media.Imaging.Bitmap?> TypeIconCache = [];

    /// <summary>Cached small type icon.</summary>
    public static global::Avalonia.Media.Imaging.Bitmap? GetTypeIcon(byte type)
    {
        lock (TypeIconCache)
        {
            if (TypeIconCache.TryGetValue(type, out var cached))
                return cached;
            using var img = TypeSpriteUtil.GetTypeSpriteIconSmall(type);
            return TypeIconCache[type] = img?.ToAvaloniaBitmap();
        }
    }
}
