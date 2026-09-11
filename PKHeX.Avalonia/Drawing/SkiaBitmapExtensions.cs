using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace PKHeX.Avalonia.Drawing;

/// <summary>
/// Converts sprites produced by the drawing layer (<see cref="SKBitmap"/>) into Avalonia images.
/// </summary>
public static class SkiaBitmapExtensions
{
    private static readonly Vector Dpi96 = new(96, 96);

    /// <summary>
    /// Copies the pixel data into a new Avalonia <see cref="Bitmap"/> (single copy, no re-encoding).
    /// </summary>
    public static Bitmap ToAvaloniaBitmap(this SKBitmap bmp)
    {
        var alpha = bmp.AlphaType switch
        {
            SKAlphaType.Premul => AlphaFormat.Premul,
            SKAlphaType.Opaque => AlphaFormat.Opaque,
            _ => AlphaFormat.Unpremul,
        };
        var format = bmp.ColorType switch
        {
            SKColorType.Bgra8888 => PixelFormat.Bgra8888,
            SKColorType.Rgba8888 => PixelFormat.Rgba8888,
            _ => throw new NotSupportedException($"Unsupported color type {bmp.ColorType}"),
        };
        return new Bitmap(format, alpha, bmp.GetPixels(), new PixelSize(bmp.Width, bmp.Height), Dpi96, bmp.RowBytes);
    }

    /// <summary>
    /// Converts the source bitmap and releases it, unless it is one of the drawing layer's shared instances.
    /// </summary>
    /// <remarks>
    /// The sprite builders hand out cached singletons for a few results (an empty slot returns <c>None</c>,
    /// for instance). Disposing one of those frees native memory that every later caller still points at,
    /// which crashes the process, so those instances are recognised and left alone.
    /// </remarks>
    public static Bitmap ToAvaloniaBitmapAndDispose(this SKBitmap bmp)
    {
        try { return bmp.ToAvaloniaBitmap(); }
        finally
        {
            bmp.Release();
        }
    }

    /// <summary>
    /// Disposes a sprite unless the drawing layer owns it (see <see cref="ToAvaloniaBitmapAndDispose"/>).
    /// </summary>
    public static void Release(this SKBitmap? bmp)
    {
        if (bmp is not null && !SharedSprites.IsShared(bmp))
            bmp.Dispose();
    }
}

/// <summary>
/// Tracks the sprite bitmaps that the drawing layer caches and reuses, which must never be disposed by a caller.
/// </summary>
public static class SharedSprites
{
    private static readonly HashSet<SKBitmap> Instances = new(ReferenceEqualityComparer.Instance as IEqualityComparer<SKBitmap> ?? EqualityComparer<SKBitmap>.Default);
    private static object? CachedFor;

    /// <summary>
    /// True when the drawing layer owns this instance and hands it to every caller.
    /// </summary>
    public static bool IsShared(SKBitmap bmp)
    {
        Refresh();
        lock (Instances)
            return Instances.Contains(bmp);
    }

    private static void Refresh()
    {
        var spriter = PKHeX.Drawing.PokeSprite.SpriteUtil.Spriter;
        lock (Instances)
        {
            if (ReferenceEquals(CachedFor, spriter))
                return;
            CachedFor = spriter;
            Instances.Clear();
            foreach (var b in new[] { spriter.Hover, spriter.View, spriter.Set, spriter.Delete, spriter.Transparent, spriter.Drag, spriter.UnknownItem, spriter.None, spriter.ItemTM, spriter.ItemTR })
                Instances.Add(b);
        }
    }
}
