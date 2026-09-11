using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Simple bordered group with a bold header (WinForms <c>GroupBox</c> replacement).
/// </summary>
/// <remarks>The header text block carries the group name so <c>Form.GB_Name</c> translation keys apply.</remarks>
public sealed class GroupBoxView : Border
{
    private readonly TextBlock HeaderBlock;
    private readonly StackPanel Panel = new() { Orientation = global::Avalonia.Layout.Orientation.Vertical };

    public GroupBoxView(string name, string header, Control content)
    {
        HeaderBlock = UiFactory.Header(name, header);
        HeaderBlock.Cursor = null;
        Panel.Children.Add(HeaderBlock);
        Panel.Children.Add(content);
        BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x80, 0x80, 0x80));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(3);
        Padding = new Thickness(8, 2, 8, 8);
        Child = Panel;
    }

    /// <summary>Header text (WinForms <c>GroupBox.Text</c>).</summary>
    public string Header
    {
        get => HeaderBlock.Text ?? string.Empty;
        set => HeaderBlock.Text = value;
    }

    /// <summary>Attaches a click handler to the header (WinForms group box click).</summary>
    public void AttachHeaderClick(System.Action onClick)
    {
        HeaderBlock.Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand);
        HeaderBlock.AttachClick(_ => onClick());
    }
}
