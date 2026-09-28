using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Editable <see cref="ComboBox"/> that completes the typed text from its own item list.
/// </summary>
/// <remarks>
/// Replaces the WinForms combo boxes configured with <c>AutoCompleteMode.SuggestAppend</c> and
/// <c>AutoCompleteSource.ListItems</c> (Species, Nature, Ability, Held Item, locations, moves, ...): typing selects the
/// first item that starts with the typed text and appends the rest as a selected suggestion, so the next keystroke
/// replaces it.
/// <br/>
/// Three behaviours differ from WinForms, because Avalonia's editable <see cref="ComboBox"/> cannot hold a text that
/// does not belong to the selected item (its own text handler always maps the text back onto the selection):
/// <list type="bullet">
/// <item>text that matches no item is undone instead of being shown in red, so the control never reports an empty
/// selection to the editor and never clears the value the user had;</item>
/// <item>Backspace/Delete shorten the typed prefix and re-complete it instead of leaving a partial text;</item>
/// <item>clicking into the field selects the whole entry, since the middle of it cannot be edited.</item>
/// </list>
/// Everything else (the drop-down arrow, picking from the list, keyboard navigation) is the stock <see cref="ComboBox"/>.
/// </remarks>
public class AutoCompleteComboBox : ComboBox
{
    protected override Type StyleKeyOverride => typeof(ComboBox);

    private TextBox? _editor;
    /// <summary>Set while the control writes the text itself, so those writes are not treated as typing.</summary>
    private bool _applying;
    /// <summary>Set between a keystroke and the completion that follows it; hides the transient empty selection.</summary>
    private bool _suppressSelection;
    /// <summary>A completion is queued on the dispatcher.</summary>
    private bool _pending;
    /// <summary>Selection to fall back to when the typed text matches no item.</summary>
    private object? _restore;
    /// <summary>Caret position before the keystroke, restored when the keystroke is undone.</summary>
    private int _anchor;

    public AutoCompleteComboBox()
    {
        IsEditable = true;
        // Registered before any consumer subscribes, so marking the event handled hides the transient states above.
        AddHandler(SelectionChangedEvent, GateSelectionChanged, RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, PreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void GateSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection)
            e.Handled = true;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _editor = e.NameScope.Find<TextBox>("PART_EditableTextBox");
        _editor?.AddHandler(PointerReleasedEvent, SelectAllOnClick, RoutingStrategies.Bubble);
    }

    /// <summary>Clicking into the field selects everything, so the next keystroke starts a new search.</summary>
    private void SelectAllOnClick(object? sender, PointerReleasedEventArgs e)
    {
        if (_editor is { } editor && editor.SelectionStart == editor.SelectionEnd)
            editor.SelectAll();
    }

    private void PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_editor is null)
            return;
        _anchor = _editor.SelectionStart;
        if (e.Key is not (Key.Back or Key.Delete))
            return;
        // Shorten the typed prefix ourselves: letting the text box delete would leave a text no item can hold.
        e.Handled = true;
        var start = Math.Min(_editor.SelectionStart, _editor.SelectionEnd);
        ShortenPrefix(start == 0 ? 0 : start - 1);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == SelectedItemProperty)
        {
            // An editable ComboBox renders its Text (not SelectionBoxItemTemplate), and Avalonia derives that Text from
            // DisplayMemberBinding. The move combos clear that binding to own the drop-down ItemTemplate, which would
            // leave the raw record ToString() in the field, so write the item's own text here instead.
            if (!_applying && !_pending && change.GetNewValue<object?>() is { } selected)
                SetCurrentValue(TextProperty, GetItemText(selected));
            base.OnPropertyChanged(change);
            return;
        }

        if (_applying || change.Property != TextProperty || _editor is null || !_editor.IsFocused
            || IsSelectionText(change.GetNewValue<string?>() ?? string.Empty))
        {
            base.OnPropertyChanged(change);
            return;
        }

        if (!_pending)
        {
            // The text box is mid-edit, and its two-way binding still has writes queued; complete once it has settled.
            _pending = true;
            _restore = SelectedItem;
            _suppressSelection = true;
            Dispatcher.UIThread.Post(Complete, DispatcherPriority.Input);
        }
        base.OnPropertyChanged(change);
    }

    private void Complete()
    {
        _pending = false;
        if (_editor is null)
        {
            _suppressSelection = false;
            return;
        }
        var typed = _editor.Text ?? string.Empty;
        if (typed.Length != 0 && FindPrefix(typed) is { } match)
            Select(match, typed.Length);
        else if (_restore is not null)
            Select(_restore, _anchor);
        else
            _suppressSelection = false;
    }

    /// <summary>Re-completes from the first <paramref name="length"/> characters of the current text.</summary>
    private void ShortenPrefix(int length)
    {
        var text = Text ?? string.Empty;
        if (length <= 0 || length >= text.Length)
        {
            SelectEditorText(text, Math.Max(0, length));
            return;
        }
        if (FindPrefix(text[..length]) is { } match)
            Select(match, length);
    }

    /// <summary>Selects <paramref name="item"/> and shows its text with everything past the prefix selected.</summary>
    private void Select(object item, int prefixLength)
    {
        var text = GetItemText(item);
        // Avalonia's own text handler nulls SelectedItem on every keystroke and that null is hidden from the consumer,
        // so putting the same item back has to be hidden too: the editor sees one SelectionChanged per real change,
        // never one for a keystroke that resolved to the item it already had.
        _suppressSelection = SelectedItem is null && Equals(item, _restore);
        SelectedItem = item;
        _suppressSelection = false;
        _applying = true;
        try
        {
            SetCurrentValue(TextProperty, text);
            if (_editor is not null)
                _editor.Text = text;
        }
        finally { _applying = false; }
        SelectEditorText(text, prefixLength);
    }

    private void SelectEditorText(string text, int start)
    {
        if (_editor is null)
            return;
        _editor.SelectionStart = Math.Clamp(start, 0, text.Length);
        _editor.SelectionEnd = text.Length;
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (_pending)
            Complete(); // flush before anything reads the value
    }

    private bool IsSelectionText(string text)
        => SelectedItem is { } s && string.Equals(GetItemText(s), text, StringComparison.Ordinal);

    private object? FindPrefix(string typed)
    {
        foreach (var item in Items)
        {
            if (item is not null && GetItemText(item).StartsWith(typed, StringComparison.CurrentCultureIgnoreCase))
                return item;
        }
        return null;
    }

    private static string GetItemText(object item) => item is ComboItem c ? c.Text : item.ToString() ?? string.Empty;
}
