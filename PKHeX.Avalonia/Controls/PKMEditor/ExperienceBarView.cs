using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Experience progress bar that can be dragged/scrolled to edit EXP (port of the WinForms <c>ExperienceBar</c>).
/// </summary>
public sealed class ExperienceBarView : Border
{
    public event EventHandler? ValueChanged;
    private bool IsDragging { get; set; }
    private byte Growth { get; set; }
    private byte Level { get; set; }
    public uint EXP { get; private set; }

    private readonly Rectangle PAN_ExpPercent = new() { Fill = new SolidColorBrush(Color.FromRgb(0x1E, 0x90, 0xFF)), HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left, Width = 0 };

    public ExperienceBarView()
    {
        Height = 10;
        BorderThickness = new Thickness(1);
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
        Background = Brushes.Transparent;
        Child = PAN_ExpPercent;
        Cursor = new Cursor(StandardCursorType.Hand);

        PointerPressed += HandleMouseDown;
        PointerMoved += HandleMouseMove;
        PointerReleased += HandleMouseUp;
        PointerExited += (_, _) => { if (!IsDragging) HideHover(); };
        PointerWheelChanged += OnScroll;
        SizeChanged += (_, _) => SetSizePercentFull();
    }

    private double CurrentPercent => Experience.GetEXPToLevelUpPercentage(Level, EXP, Growth);
    private void NotifyUpdate() => ValueChanged?.Invoke(this, EventArgs.Empty);
    private int RealWidth => Math.Max(0, (int)(Bounds.Width - BorderThickness.Left - BorderThickness.Right));
    private int Border_ => (int)BorderThickness.Left;

    private uint GetEXPEdgeHigh()
    {
        var next = Experience.GetEXPToLevelUp(Level, Growth);
        if (next == 0)
            return EXP;
        return Experience.GetEXP(Level, Growth) + next - 1;
    }

    private uint GetEXPAtWidth(int width)
    {
        var start = Experience.GetEXP(Level, Growth);
        var range = Experience.GetEXPToLevelUp(Level, Growth);
        var maxWidth = RealWidth;
        if (range == 0 || maxWidth <= 0)
            return start;

        var progress = (uint)((width * range) / maxWidth);
        if (progress >= range)
            progress = range - 1;
        return start + progress;
    }

    private uint GetHoverEXP(int x)
    {
        if (Level >= Experience.MaxLevel)
            return EXP;

        var maxWidth = RealWidth;
        if (maxWidth <= 0)
            return Experience.GetEXP(Level, Growth);

        var width = Math.Clamp(x - Border_, 0, maxWidth);
        if (width == 0)
            return Experience.GetEXP(Level, Growth);
        if (width == maxWidth)
            return GetEXPEdgeHigh();
        return GetEXPAtWidth(width);
    }

    private string GetHoverText(int x, KeyModifiers mods)
    {
        var start = Experience.GetEXP(Level, Growth);
        var current = GetHoverEXP(x);
        if (mods.HasFlag(KeyModifiers.Control))
            current = EXP;

        var gained = current - start;
        var range = Experience.GetEXPToLevelUp(Level, Growth);
        var remain = range - gained;
        if (range == 0)
            return $"{current}";
        return $"{gained}/{range} (-{remain})" + Environment.NewLine + $"{current} {((float)gained*100)/range:F0}%";
    }

    private void ShowHover(int x, KeyModifiers mods)
    {
        ToolTip.SetTip(this, GetHoverText(x, mods));
        ToolTip.SetIsOpen(this, true);
    }

    private void HideHover() => ToolTip.SetIsOpen(this, false);

    private void HandleMouseDown(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;
        e.Handled = true;

        if (TryAction(e.KeyModifiers))
        {
            HideHover();
            return;
        }

        IsDragging = true;
        e.Pointer.Capture(this);

        var x = (int)point.Position.X;
        SetBoundedPixelPercent(x);
        ShowHover(x, e.KeyModifiers);
    }

    private int lastMouseMoveX = int.MinValue;

    private void HandleMouseMove(object? sender, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var x = (int)point.Position.X;
        if (x == lastMouseMoveX)
            return;
        if (IsDragging && point.Properties.IsLeftButtonPressed)
            SetBoundedPixelPercent(x);

        ShowHover(x, e.KeyModifiers);
        lastMouseMoveX = x;
    }

    private void HandleMouseUp(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;

        IsDragging = false;
        e.Pointer.Capture(null);
        ShowHover((int)e.GetPosition(this).X, e.KeyModifiers);
    }

    private bool TrySetEXPWithinLevel(int newWidth)
    {
        if (Level >= Experience.MaxLevel)
            return false;

        var maxWidth = RealWidth;
        if (maxWidth <= 0)
            return false;

        var width = Math.Clamp(newWidth, 0, maxWidth);
        var original = EXP;
        if (width == 0)
            EdgeLow();
        else if (width == maxWidth)
            EdgeHigh();
        else
            EXP = GetEXPAtWidth(width);

        return EXP != original;
    }

    private void SetBoundedPixelPercent(int x)
    {
        if (TrySetEXPWithinLevel(x - Border_))
            NotifyUpdate();
    }

    /// <summary>
    /// Returns true if an action was taken, false if the caller should handle it otherwise.
    /// </summary>
    public bool TryAction(KeyModifiers mods)
    {
        if (mods.HasFlag(KeyModifiers.Alt))
        {
            if (mods.HasFlag(KeyModifiers.Control))
                DownlevelNoEXP();
            else if (EXP != Experience.GetEXP(Level, Growth))
                EdgeLow();
            else
                Underflow();
            NotifyUpdate();
            return true;
        }

        if (Level >= Experience.MaxLevel)
            return true;

        if (mods.HasFlag(KeyModifiers.Shift))
        {
            if (!mods.HasFlag(KeyModifiers.Control) && EXP != GetEXPEdgeHigh())
                EdgeHigh();
            else
                Overflow();
            NotifyUpdate();
            return true;
        }

        return false;
    }

    private void OnScroll(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;
        var delta = e.Delta.Y;
        if ((Level >= Experience.MaxLevel && delta > 0) || (Level <= Experience.MinLevel && delta < 0 && PAN_ExpPercent.Width == 0))
            return;

        int value = 0;
        const int increment = 1;
        if (delta > 0)
            value += increment;
        else if (delta < 0)
            value -= increment;
        else
            return;

        SetNewPixelPercent((int)PAN_ExpPercent.Width + value, true);
        ShowHover((int)e.GetPosition(this).X, e.KeyModifiers);
    }

    private void SetNewPixelPercent(int newWidth, bool scroll)
    {
        var currentWidth = (int)PAN_ExpPercent.Width;
        if (newWidth == currentWidth)
            return; // unchanged, so do nothing

        var maxWidth = RealWidth;
        if (newWidth < 0)
        {
            Underflow();
        }
        else if (newWidth >= maxWidth)
        {
            Overflow();
        }
        else if (newWidth == 0)
        {
            EdgeLow();
        }
        else
        {
            var range = Experience.GetEXPToLevelUp(Level, Growth);
            if (range == 0)
                return;
            var pixelsPerEXP = (double)maxWidth / range;

            double delta = newWidth - currentWidth;
            // If there aren't enough pixels to represent 1 EXP, round up to ensure at least 1 EXP is gained/lost per scroll increment.
            if (pixelsPerEXP > 1 && Math.Abs(delta) < pixelsPerEXP)
                delta = Math.Sign(delta) * pixelsPerEXP;

            var adjust = (uint)(int)(delta / pixelsPerEXP);
            var newEXP = unchecked(EXP + adjust);

            // don't allow clicking to change levels, in the event the user is trying to manually edge via clicking.
            // allow scrolling to change levels over/underflow.
            if (!scroll && Experience.GetLevel(newEXP, Growth) != Level)
                return;
            EXP = newEXP;
        }
        NotifyUpdate();
    }

    public void DownlevelNoEXP()
    {
        if (Level <= Experience.MinLevel)
            return;
        EXP = Experience.GetEXP((byte)(Level - 1), Growth);
    }

    public void Overflow()
    {
        if (Level >= Experience.MaxLevel)
            return;
        EXP = Experience.GetEXP((byte)(Level + 1), Growth);
    }

    public void Underflow()
    {
        if (Level <= Experience.MinLevel)
            return;
        EXP = Experience.GetEXP(Level, Growth) - 1;
    }

    public void EdgeLow()
    {
        EXP = Experience.GetEXP(Level, Growth);
    }

    public void EdgeHigh()
    {
        if (Level >= Experience.MaxLevel)
            return;
        EXP = GetEXPEdgeHigh();
    }

    public void Update(uint exp, byte growth) => Update(exp, growth, Experience.GetLevel(exp, growth));

    public void Update(uint exp, byte growth, byte level)
    {
        EXP = exp;
        Growth = growth;
        Level = level;

        if (level >= Experience.MaxLevel)
        {
            PAN_ExpPercent.Width = 0;
            return;
        }

        SetSizePercentFull();
    }

    private void SetSizePercentFull()
    {
        if (Level >= Experience.MaxLevel || Growth == 0 && Level == 0)
        {
            PAN_ExpPercent.Width = 0;
            return;
        }
        // If progress to next level is not entirely empty, round up to at least 1 pixel to show progress.
        var gainedPercent = CurrentPercent;
        var newWidth = (int)(gainedPercent * RealWidth);
        if (newWidth == 0 && gainedPercent != 0)
            newWidth = 1;
        PAN_ExpPercent.Width = newWidth;
    }
}
