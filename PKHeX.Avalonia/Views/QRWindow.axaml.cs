using System;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Core;
using PKHeX.Drawing.Misc;
using SkiaSharp;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Displays a QR code for the current entity (port of the WinForms <c>QR</c> form; click the image to copy it).
/// </summary>
public sealed partial class QRWindow : Window
{
    private readonly global::Avalonia.Media.Imaging.Bitmap? Image;

    public QRWindow()
    {
        InitializeComponent();
        Icon = AppIcon.Get();
    }

    /// <summary>
    /// Displays a QR code for a Mystery Gift (WinForms <c>QR(qr, icon, lines)</c>).
    /// </summary>
    public QRWindow(SKBitmap qr, SKBitmap sprite, string[] lines, string footer) : this()
    {
        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);

        const int width = 365;
        const int height = 365 + 50;
        using var font = new SKFont(SKTypeface.Default, 9);
        using var qrImage = QRImageUtil.GetQRImageExtended(font, qr, sprite, width, height, lines, footer);
        Image = qrImage.ToAvaloniaBitmap();

        var pb = this.FindControl<Image>("PB_QR")!;
        pb.Source = Image;
        pb.PointerPressed += async (_, _) => await CopyImage();
        this.FindControl<TextBlock>("L_Info")!.Text = string.Join("\n", [.. lines, footer]);
        Closed += (_, _) => Image?.Dispose();
    }

    public QRWindow(SKBitmap qr, SKBitmap sprite, PKM pk, string line1, string line2, string line3, string line4) : this()
    {
        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);

        const int width = 365;
        const int height = 365 + 50;
        using var font = new SKFont(SKTypeface.Default, 9);
        using var qrImage = QRImageUtil.GetQRImageExtended(font, qr, sprite, width, height, [line1, line2, line3], line4);
        Image = qrImage.ToAvaloniaBitmap();

        var pb = this.FindControl<Image>("PB_QR")!;
        pb.Source = Image;
        pb.PointerPressed += async (_, _) => await CopyImage();
        this.FindControl<TextBlock>("L_Info")!.Text = $"{line1}\n{line2}\n{line3}\n{line4}";
        Closed += (_, _) => Image?.Dispose();
    }

    private async System.Threading.Tasks.Task CopyImage()
    {
        try
        {
            if (Clipboard is { } clipboard && Image is not null)
                await clipboard.SetBitmapAsync(Image);
        }
        catch (Exception ex)
        {
            await Services.AppDialogs.Error(this, MessageStrings.MsgClipboardFailWrite, ex);
        }
    }
}
