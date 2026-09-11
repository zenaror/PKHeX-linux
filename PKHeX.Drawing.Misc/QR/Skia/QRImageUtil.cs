using System;
using SkiaSharp;

namespace PKHeX.Drawing.Misc;

/// <summary>
/// Provides utility methods for composing and extending QR code images with overlays and text.
/// </summary>
/// <remarks>Cross-platform (SkiaSharp) implementation; the GDI+ implementation is in <c>QR/QRImageUtil.cs</c>.</remarks>
public static class QRImageUtil
{
    private static readonly SKColor Background = SKColors.White;
    private static readonly SKColor Foreground = SKColors.Black;

    /// <summary>
    /// Creates a new QR image with a preview image layered in the center.
    /// </summary>
    /// <param name="qr">The base QR code image.</param>
    /// <param name="preview">The preview image to overlay.</param>
    /// <returns>A new bitmap with the preview image centered on the QR code.</returns>
    public static SKBitmap GetQRImage(SKBitmap qr, SKBitmap preview)
    {
        // create a small area with the pk sprite, with a white background
        using var foreground = ImageUtil.CreateBitmap(preview.Width + 4, preview.Height + 4);
        foreground.GetBitmapSpan().Fill(0xFF); // opaque white
        {
            int x = (foreground.Width / 2) - (preview.Width / 2);
            int y = (foreground.Height / 2) - (preview.Height / 2);
            using var layered = ImageUtil.LayerImage(foreground, preview, x, y);
            // Layer on Preview Image
            int qx = (qr.Width / 2) - (foreground.Width / 2);
            int qy = (qr.Height / 2) - (foreground.Height / 2);
            return ImageUtil.LayerImage(qr, layered, qx, qy);
        }
    }

    /// <summary>
    /// Creates an extended QR image with additional text and formatting.
    /// </summary>
    /// <param name="font">The font to use for text.</param>
    /// <param name="qr">The base QR code image.</param>
    /// <param name="pk">The preview image to overlay.</param>
    /// <param name="width">The width of the final image.</param>
    /// <param name="height">The height of the final image.</param>
    /// <param name="lines">The lines of text to display.</param>
    /// <param name="extraText">Additional text to display.</param>
    /// <returns>A new bitmap with the preview image and extended text.</returns>
    public static SKBitmap GetQRImageExtended(SKFont font, SKBitmap qr, SKBitmap pk, int width, int height, ReadOnlySpan<string> lines, string extraText)
    {
        using var pic = GetQRImage(qr, pk);
        return ExtendImage(font, qr, width, height, pic, lines, extraText);
    }

    /// <summary>
    /// Extends an image with additional lines of text and formatting.
    /// </summary>
    private static SKBitmap ExtendImage(SKFont font, SKBitmap qr, int width, int height, SKBitmap pic, ReadOnlySpan<string> lines, string extraText)
    {
        // Text is rendered on an opaque premultiplied surface, then converted to the utility's pixel layout.
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        var g = surface.Canvas;
        g.Clear(Background);
        using (var image = SKImage.FromBitmap(pic))
            g.DrawImage(image, 0, 0);

        using var paint = new SKPaint { Color = Foreground, IsAntialias = true };
        const int indent = 18;
        // GDI+ DrawString positions the top of the text box; Skia positions the baseline.
        var baseline = -font.Metrics.Ascent;
        g.DrawText(GetLine(lines, 0), indent, qr.Height - 5 + baseline, font, paint);
        g.DrawText(GetLine(lines, 1), indent, qr.Height + 8 + baseline, font, paint);
        g.DrawText(GetLine2(lines)  , indent, qr.Height + 20 + baseline, font, paint);
        g.DrawText(GetLine(lines, 3) + extraText, indent, qr.Height + 32 + baseline, font, paint);
        g.Flush();

        var result = ImageUtil.CreateBitmap(width, height);
        if (!surface.ReadPixels(result.Info, result.GetPixels(), result.RowBytes, 0, 0))
            throw new InvalidOperationException("Failed to read rendered QR image pixels.");
        return result;
    }

    /// <summary>
    /// Gets and formats the second line of text for display.
    /// </summary>
    private static string GetLine2(ReadOnlySpan<string> lines) => GetLine(lines, 2)
        .Replace(Environment.NewLine, "/")
        .Replace("//", "   ")
        .Replace(":/", ": ");

    /// <summary>
    /// Gets a specific line of text or an empty string if the line does not exist.
    /// </summary>
    private static string GetLine(ReadOnlySpan<string> lines, int line) => lines.Length <= line ? string.Empty : lines[line];
}
