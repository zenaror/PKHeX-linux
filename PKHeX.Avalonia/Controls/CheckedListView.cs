using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Styling;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Scrollable list of labelled check boxes; replacement for the WinForms <c>CheckedListBox</c>.
/// </summary>
public sealed class CheckedListView : ListBox
{
    // Derived controls have no theme of their own; reuse the ListBox theme.
    protected override System.Type StyleKeyOverride => typeof(ListBox);

    private readonly ObservableCollection<CheckedItem> Entries = [];

    public CheckedListView()
    {
        ItemsSource = Entries;
        SelectionMode = SelectionMode.Single;
        ItemTemplate = new FuncDataTemplate<CheckedItem>((_, _) =>
        {
            var chk = new CheckBox { MinHeight = 0, Padding = new Thickness(6, 0, 0, 0) };
            chk.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(CheckedItem.IsChecked)) { Mode = BindingMode.TwoWay });
            chk.Bind(ContentControl.ContentProperty, new Binding(nameof(CheckedItem.Text)));
            return chk;
        });
        Styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(2, 0)), new Setter(MinHeightProperty, 0d) },
        });
    }

    /// <summary>
    /// Raised after an entry's checked state changes, with its index and new value
    /// (equivalent of the WinForms <c>ItemCheck</c> event, which fires before the change).
    /// </summary>
    public event System.Action<int, bool>? ItemCheckChanged;

    /// <summary>Number of entries in the list.</summary>
    public int Count => Entries.Count;

    /// <summary>Appends an entry.</summary>
    public void Add(string text, bool isChecked = false)
    {
        var item = new CheckedItem { Text = text, IsChecked = isChecked };
        item.PropertyChanged += (s, _) => ItemCheckChanged?.Invoke(Entries.IndexOf((CheckedItem)s!), ((CheckedItem)s!).IsChecked);
        Entries.Add(item);
    }

    /// <summary>Removes every entry.</summary>
    public void ClearItems() => Entries.Clear();

    /// <summary>Replaces the label of an entry, keeping its checked state.</summary>
    public void SetItemText(int index, string text)
    {
        if ((uint)index >= Entries.Count)
            return;
        var old = Entries[index];
        var replacement = new CheckedItem { Text = text, IsChecked = old.IsChecked };
        replacement.PropertyChanged += (s, _) => ItemCheckChanged?.Invoke(Entries.IndexOf((CheckedItem)s!), ((CheckedItem)s!).IsChecked);
        Entries[index] = replacement;
    }

    /// <summary>Reads the checked state of an entry.</summary>
    public bool GetItemChecked(int index) => (uint)index < Entries.Count && Entries[index].IsChecked;

    /// <summary>Writes the checked state of an entry.</summary>
    public void SetItemChecked(int index, bool value)
    {
        if ((uint)index < Entries.Count)
            Entries[index].IsChecked = value;
    }

    /// <summary>Sets every entry to the given state.</summary>
    public void SetAllChecked(bool value)
    {
        foreach (var item in Entries)
            item.IsChecked = value;
    }

    private sealed class CheckedItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private bool isChecked;

        public required string Text { get; init; }

        public bool IsChecked
        {
            get => isChecked;
            set
            {
                if (isChecked == value)
                    return;
                isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }
    }
}
