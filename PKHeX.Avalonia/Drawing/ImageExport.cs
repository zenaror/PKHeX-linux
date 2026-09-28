using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using PKHeX.Avalonia.Services;
using SkiaSharp;

namespace PKHeX.Avalonia.Drawing;

/// <summary>
/// Saves a decoded save-file picture to disk (WinForms <c>SAV_Trainer9.IMG_Save</c>).
/// </summary>
/// <remarks>
/// WinForms picks the encoder from the chosen extension (png / bmp / jpg) through <c>ImageFormat</c>.
/// Skia has no BMP encoder, so only the formats it can write are offered; see PORTING.md.
/// </remarks>
public static class ImageExport
{
    private const string Filter = "Images|*.png;*.jpg";

    /// <summary>Prompts for a path and writes <paramref name="image"/> there, encoded from the chosen extension.</summary>
    public static async Task SaveDialog(Window owner, SKBitmap image, string suggestedName)
    {
        var path = await FileDialogs.SaveFileDialog(owner, Filter, $"{suggestedName}.png");
        if (path is null)
            return;

        var format = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            _ => SKEncodedImageFormat.Png,
        };

        using var snapshot = SKImage.FromBitmap(image);
        using var data = snapshot.Encode(format, 100);
        if (data is null)
        {
            await AppDialogs.Error(owner, $"Unable to encode the image as {format}.");
            return;
        }

        try
        {
            await using var stream = File.Create(path);
            data.SaveTo(stream);
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(owner, $"Failed to save the image:{Environment.NewLine}{ex.Message}");
        }
    }
}
