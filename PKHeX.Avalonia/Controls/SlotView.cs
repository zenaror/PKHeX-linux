using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Displays a single entity slot: a background (touch-type indicator) image layered under the sprite image.
/// </summary>
/// <remarks>Equivalent of the WinForms <c>SelectablePictureBox</c> slot with <c>BackgroundImage</c> + <c>Image</c>.</remarks>
public sealed class SlotView : Border
{
    private static readonly IBrush FrameBrush = new SolidColorBrush(Color.FromRgb(0x64, 0x64, 0x64));

    private readonly Image BackgroundImage = new() { Stretch = Stretch.None, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
    private readonly Image SpriteImage = new() { Stretch = Stretch.None, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };

    private Bitmap? _background;
    private Bitmap? _sprite;

    public const int BorderSize = 1; // between

    public SlotView(int spriteWidth, int spriteHeight)
    {
        Width = spriteWidth + (2 * BorderSize);
        Height = spriteHeight + (2 * BorderSize);
        BorderThickness = new Thickness(BorderSize);
        BorderBrush = FrameBrush;
        Background = Brushes.Transparent; // ensure hit-testing over the whole slot
        Focusable = true;

        var panel = new Panel();
        panel.Children.Add(BackgroundImage);
        panel.Children.Add(SpriteImage);
        Child = panel;
    }

    /// <summary>
    /// Image drawn behind the sprite (slot interaction indicator). Ownership is not transferred (shared images).
    /// </summary>
    public Bitmap? BackgroundBitmap
    {
        get => _background;
        set
        {
            _background = value;
            BackgroundImage.Source = value;
        }
    }

    /// <summary>
    /// Sprite image. Ownership is transferred; the previous image is disposed.
    /// </summary>
    public Bitmap? Sprite
    {
        get => _sprite;
        set
        {
            if (ReferenceEquals(_sprite, value))
                return;
            var old = _sprite;
            _sprite = value;
            SpriteImage.Source = value;
            old?.Dispose();
        }
    }

    /// <summary>
    /// Background color of the slot (transparent shows the box wallpaper).
    /// </summary>
    public void SetBackColor(System.Drawing.Color color)
    {
        Background = color.A == 0
            ? Brushes.Transparent
            : new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
    }

    /// <summary>
    /// Text describing the slot content (shown as tooltip).
    /// </summary>
    public string? Description
    {
        get => ToolTip.GetTip(this) as string;
        set => ToolTip.SetTip(this, string.IsNullOrEmpty(value) ? null : value);
    }
}
