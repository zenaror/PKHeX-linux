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
/// Scrollable list of three-state check boxes; replacement for a WinForms <c>CheckedListBox</c> used with
/// <c>CheckState.Indeterminate</c>.
/// </summary>
public sealed class TriCheckedListView : ListBox
{
    // Derived controls have no theme of their own; reuse the ListBox theme.
    protected override System.Type StyleKeyOverride => typeof(ListBox);

    private readonly ObservableCollection<TriItem> Entries = [];

    public TriCheckedListView()
    {
        ItemsSource = Entries;
        SelectionMode = SelectionMode.Single;
        ItemTemplate = new FuncDataTemplate<TriItem>((_, _) =>
        {
            var chk = new CheckBox { MinHeight = 0, Padding = new Thickness(6, 0, 0, 0), IsThreeState = true };
            chk.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(TriItem.State)) { Mode = BindingMode.TwoWay });
            chk.Bind(ContentControl.ContentProperty, new Binding(nameof(TriItem.Text)));
            return chk;
        });
        Styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(2, 0)), new Setter(MinHeightProperty, 0d) },
        });
    }

    /// <summary>Number of entries in the list.</summary>
    public int Count => Entries.Count;

    /// <summary>Appends an entry. <c>null</c> is the indeterminate state.</summary>
    public void Add(string text, bool? state) => Entries.Add(new TriItem { Text = text, State = state });

    /// <summary>Reads the state of an entry.</summary>
    public bool? GetItemCheckState(int index) => (uint)index < Entries.Count ? Entries[index].State : false;

    /// <summary>Writes the state of an entry.</summary>
    public void SetItemCheckState(int index, bool? state)
    {
        if ((uint)index < Entries.Count)
            Entries[index].State = state;
    }

    /// <summary>Sets every entry to the given state.</summary>
    public void SetAllCheckState(bool? state)
    {
        foreach (var item in Entries)
            item.State = state;
    }

    private sealed class TriItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private bool? state;

        public required string Text { get; init; }

        public bool? State
        {
            get => state;
            set
            {
                if (state == value)
                    return;
                state = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
            }
        }
    }
}
