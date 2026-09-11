using System;
using PKHeX.Core;
using PKHeX.Drawing;
using SkiaSharp;

namespace PKHeX.Avalonia.Drawing;

/// <summary>
/// Converts images to C-Gear Backgrounds and back (port of the WinForms <c>CGearImage</c>).
/// </summary>
/// <remarks>
/// The WinForms helper works on GDI+ <c>Format32bppArgb</c> bitmaps. The Skia bitmaps this port uses are
/// <see cref="SKColorType.Bgra8888"/> with <see cref="SKAlphaType.Unpremul"/> alpha, which is the same memory layout,
/// so the tile/palette conversion in Core is fed the same bytes.
/// </remarks>
public static class CGearImage
{
    private const int Width = CGearBackground.Width;
    private const int Height = CGearBackground.Height;

    /// <summary>
    /// Gets the visual image of a <see cref="CGearBackground"/>.
    /// </summary>
    public static SKBitmap GetBitmap(CGearBackground bg)
    {
        var data = bg.GetImageData();
        return ImageUtil.GetBitmap(data, Width, Height);
    }

    /// <summary>
    /// Checks that a decoded image can be used as a C-Gear background.
    /// </summary>
    public static bool IsInputCorrect(SKBitmap img, out string? msg)
    {
        if (img.Width != Width || img.Height != Height)
        {
            msg = $"Incorrect image dimensions. Expected {Width}x{Height}";
            return false;
        }
        if (img.ColorType != SKColorType.Bgra8888)
        {
            msg = $"Incorrect image format. Expected {SKColorType.Bgra8888}";
            return false;
        }
        msg = null;
        return true;
    }

    /// <summary>
    /// Converts an image to a <see cref="CGearBackground"/>.
    /// </summary>
    public static TiledImageStat GetCGearBackground(SKBitmap img, CGearBackground bg)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(img.Width, Width);
        ArgumentOutOfRangeException.ThrowIfNotEqual(img.Height, Height);

        // get raw bytes of image
        var data = img.GetBitmapSpan();
        const int bpp = 4;
        if (data.Length != Width * Height * bpp)
            throw new ArgumentException($"Unexpected pixel buffer length {data.Length}.", nameof(img));

        return bg.SetImageData(data);
    }

    /// <summary>Encodes a bitmap as PNG.</summary>
    public static byte[] EncodePng(SKBitmap img)
    {
        using var image = SKImage.FromBitmap(img);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
