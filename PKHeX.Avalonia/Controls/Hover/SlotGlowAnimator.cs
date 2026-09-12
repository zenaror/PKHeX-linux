using System;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Drawing;
using PKHeX.Drawing.PokeSprite;
using SkiaSharp;
using Color = System.Drawing.Color;

namespace PKHeX.Avalonia.Controls.Hover;

/// <summary>
/// Pulses a glow around the hovered slot's sprite (port of the WinForms <c>BitmapAnimator</c>).
/// </summary>
/// <remarks>
/// The glow is the sprite's edges recolored, so one set of pixels is built per hover and each animation frame only
/// changes its color. Frames are cached for the duration of the hover, as upstream does, and the slot draws them in
/// its own layer so the touch-type background underneath is left alone.
/// </remarks>
public sealed class SlotGlowAnimator : IDisposable
{
    /// <summary>Frames between the two glow colors; the counter walks up and back down again.</summary>
    private const int Frames = 16; // WinForms: 1000ms / 60fps

    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(1000d / 60d);

    private readonly DispatcherTimer Timer;
    private readonly Bitmap?[] Cache = new Bitmap[Frames + 1];

    private byte[]? GlowData;
    private int Width;
    private int Height;
    private SlotView? Slot;
    private int Counter;

    private static Color GlowFrom => MainWindow.Settings.Draw.GlowInitial;
    private static Color GlowTo => MainWindow.Settings.Draw.GlowFinal;

    public SlotGlowAnimator() => Timer = new DispatcherTimer(Interval, DispatcherPriority.Background, Tick);

    /// <summary>
    /// Starts glowing the slot showing <paramref name="pk"/>.
    /// </summary>
    public void Start(SlotView slot, PKM pk)
    {
        Stop();
        if (pk.Species == 0)
            return;

        var glow = GlowFrom;
        SKBitmap? baseSprite = null;
        try
        {
            SpriteUtil.GetSpriteGlow(pk, glow.B, glow.G, glow.R, out var pixels, out baseSprite);
            if (pixels.Length == 0)
                return;
            GlowData = pixels;
            Width = baseSprite.Width;
            Height = baseSprite.Height;
        }
        catch (Exception ex)
        {
            // A hover must never take the editor down; fall back to no glow.
            System.Diagnostics.Debug.WriteLine($"Slot glow failed: {ex.Message}");
            return;
        }
        finally
        {
            baseSprite.Release();
        }

        Slot = slot;
        Counter = 0;
        slot.GlowBitmap = GetFrame(0);
        Timer.Start();
    }

    /// <summary>
    /// Stops the animation and clears the glow from the slot it was drawn on.
    /// </summary>
    public void Stop()
    {
        Timer.Stop();
        if (Slot is not null)
        {
            Slot.GlowBitmap = null;
            Slot = null;
        }

        for (int i = 0; i < Cache.Length; i++)
        {
            Cache[i]?.Dispose();
            Cache[i] = null;
        }
        GlowData = null;
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (Slot is null)
            return;

        Counter = (Counter + 1) % (Frames * 2); // loop backwards
        var index = Counter >= Frames ? (Frames * 2) - Counter : Counter;
        Slot.GlowBitmap = GetFrame(index);
    }

    private Bitmap? GetFrame(int index)
    {
        if (Cache[index] is { } cached)
            return cached;
        if (GlowData is not { Length: not 0 } data)
            return null;

        var frameColor = ColorUtil.Blend(GlowTo, GlowFrom, (double)index / Frames);
        var frameData = GC.AllocateUninitializedArray<byte>(data.Length);
        data.CopyTo(frameData, 0);
        ImageUtil.ChangeAllColorTo(frameData, frameColor);

        using var glow = ImageUtil.GetBitmap(frameData, Width, Height);
        using var frame = ImageUtil.LayerImage(glow, SpriteUtil.Spriter.Hover, 0, 0);
        return Cache[index] = frame.ToAvaloniaBitmap();
    }

    public void Dispose()
    {
        Stop();
        Timer.Stop();
    }
}
