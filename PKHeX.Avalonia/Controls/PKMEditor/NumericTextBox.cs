using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Text box that only accepts digits (or hexadecimal digits); replacement for the WinForms numeric <c>MaskedTextBox</c>.
/// </summary>
public class NumericTextBox : TextBox
{
    protected override Type StyleKeyOverride => typeof(TextBox);

    /// <summary>Accept hexadecimal characters.</summary>
    public bool IsHex { get; set; }

    public NumericTextBox()
    {
        MinWidth = 40;
        HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        Padding = new global::Avalonia.Thickness(4, 2);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (e.Text is { } text)
        {
            foreach (var c in text)
            {
                if (!IsAllowed(c))
                {
                    e.Handled = true;
                    return;
                }
            }
        }
        base.OnTextInput(e);
    }

    private bool IsAllowed(char c) => char.IsAsciiDigit(c) || (IsHex && char.IsAsciiHexDigit(c));

    /// <summary>
    /// Integer value of the text (0 if empty/invalid).
    /// </summary>
    public int IntValue
    {
        get => IsHex ? (int)Util.GetHexValue(Text ?? string.Empty) : Util.ToInt32(Text ?? string.Empty);
        set => Text = value.ToString();
    }

    public uint UIntValue => IsHex ? Util.GetHexValue(Text ?? string.Empty) : Util.ToUInt32(Text ?? string.Empty);
}

/// <summary>
/// Mouse wheel increment helpers (port of <c>WinFormsUtil.MouseWheelIncrement1/4</c>).
/// </summary>
public static class TextBoxUtil
{
    /// <summary>
    /// Invokes <paramref name="handler"/> synchronously whenever the text changes (Avalonia's <c>TextChanged</c> event is raised
    /// asynchronously, which breaks the WinForms ordering assumptions of the editor logic).
    /// </summary>
    public static void OnTextChanged(this TextBox tb, Action<TextBox> handler)
    {
        tb.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                handler(tb);
        };
    }

    public static void MouseWheelIncrement(this TextBox tb, uint increment)
    {
        tb.PointerWheelChanged += (_, e) =>
        {
            var text = tb.Text ?? string.Empty;
            var value = Util.ToUInt32(text);
            if (e.Delta.Y > 0)
                value += increment;
            else if (value >= increment)
                value -= increment;
            tb.Text = value.ToString();
            e.Handled = true;
        };
    }

    /// <summary>
    /// Attaches a click handler (fires on left pointer press, before the control handles it) that receives the key modifiers.
    /// </summary>
    public static void AttachClick(this Control c, Action<KeyModifiers> onClick)
    {
        c.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(c).Properties.IsLeftButtonPressed)
                return;
            onClick(e.KeyModifiers);
        }, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Attaches a click handler that fires on left pointer press and marks the event handled.
    /// </summary>
    public static void AttachClickHandled(this Control c, Action<KeyModifiers> onClick)
    {
        c.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(c).Properties.IsLeftButtonPressed)
                return;
            e.Handled = true;
            onClick(e.KeyModifiers);
        }, RoutingStrategies.Tunnel);
    }
}
