using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PKHeX.Avalonia.Localization;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Clickable gender indicator (port of the WinForms <c>GenderToggle</c>).
/// </summary>
public sealed class GenderToggleView : Border
{
    public bool AllowClick { get; set; } = true;

    private int Value = -1; // Initial load will trigger gender to appear (-1 => 0)
    private readonly Image Picture = new() { Stretch = Stretch.None, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };

    public event EventHandler? Click;

    public byte Gender
    {
        get => (byte)Value;
        set => Value = SetGender(value);
    }

    public GenderToggleView()
    {
        Width = 24;
        Height = 24;
        Background = Brushes.Transparent;
        Focusable = true;
        Child = Picture;
        Cursor = new Cursor(StandardCursorType.Hand);
        Gender = 0;
        AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;
            Focus();
            e.Handled = true;
            if (!AllowClick)
                return;
            Click?.Invoke(this, EventArgs.Empty);
        }, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
        KeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Enter or Key.Space))
                return;
            e.Handled = true;
            if (AllowClick)
                Click?.Invoke(this, EventArgs.Empty);
        };
    }

    private int SetGender(int value)
    {
        if ((uint)value > 2)
            value = 2;
        if (Value == value)
            return value;
        Picture.Source = AppResources.GetImage($"gender_{value}");
        return value;
    }

    public (bool CanToggle, int Value) ToggleGender()
    {
        if (CanToggle())
            return (true, Gender ^= 1);
        return (false, Gender);
    }

    public bool CanToggle() => (uint)Gender < 2;
}
