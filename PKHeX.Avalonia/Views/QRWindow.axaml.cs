using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Core;
using PKHeX.Drawing.Misc;
using SkiaSharp;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Displays a QR code for the current entity (port of the WinForms <c>QR</c> form; click the image to copy it).
/// </summary>
/// <remarks>
/// For a <see cref="PK7"/> the QR encodes an injection payload, so the box / slot / copies row is shown and
/// <c>Refresh</c> re-encodes it, exactly as the WinForms form does with <c>splitContainer1.Panel1</c>.
/// </remarks>
public sealed partial class QRWindow : Window
{
    private readonly PKM? Entity;
    private readonly SKBitmap Sprite = null!;
    private readonly string[] Lines = [];

    /// <summary>QR code currently rendered; either the one handed in or the last one generated for a PK7.</summary>
    private SKBitmap Source = null!;

    /// <summary>QR code generated here (PK7 only); owned by this window, unlike the one the caller passes in.</summary>
    private SKBitmap? Generated;

    private string ExtraText = string.Empty;

    /// <summary>
    /// Every composed image handed to the <see cref="Image"/> control; disposed when the window closes.
    /// </summary>
    /// <remarks>
    /// X11 clipboards are lazy: the owner keeps the data and only encodes it when another application asks for it.
    /// Disposing the bitmap that was copied would make that later request throw, so it is kept alive.
    /// </remarks>
    private readonly List<AvaloniaBitmap> Composed = [];

    private AvaloniaBitmap? CopiedToClipboard;

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
        Sprite = sprite;
        Source = qr;
        Lines = [.. lines, footer];
        Initialize();
    }

    /// <summary>
    /// Displays a QR code for an entity (WinForms <c>QR(qr, icon, pk, lines)</c>).
    /// </summary>
    public QRWindow(SKBitmap qr, SKBitmap sprite, PKM pk, string line1, string line2, string line3, string line4) : this()
    {
        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
        Sprite = sprite;
        Source = qr;
        Entity = pk;
        Lines = [line1, line2, line3, line4];
        Initialize();
    }

    private void Initialize()
    {
        var pb = this.FindControl<Image>("PB_QR")!;
        pb.PointerPressed += async (_, _) => await CopyImage();

        if (Entity is PK7)
        {
            this.FindControl<StackPanel>("P_QR7")!.IsVisible = true;
            this.FindControl<Button>("B_Refresh")!.Click += (_, _) => UpdateBoxSlotCopies();
            ReloadQRData();
        }

        RefreshImage();
        Closed += (_, _) => ReleaseImages();
    }

    private void UpdateBoxSlotCopies()
    {
        ReloadQRData();
        RefreshImage();
    }

    private void ReloadQRData()
    {
        var pk7 = (PK7)Entity!;
        var box = (int)(this.FindControl<NumericUpDown>("NUD_Box")!.Value ?? 1) - 1;
        var slot = (int)(this.FindControl<NumericUpDown>("NUD_Slot")!.Value ?? 1) - 1;
        var copies = (int)(this.FindControl<NumericUpDown>("NUD_Copies")!.Value ?? 1);
        ExtraText = $" (Box {box + 1}, Slot {slot + 1}, {copies} cop{(copies > 1 ? "ies" : "y")})";

        Generated?.Dispose();
        Source = Generated = QREncode.GenerateQRCode7(pk7, box, slot, copies);
    }

    private void RefreshImage()
    {
        var width = Math.Max(Source.Width, 370); // WinForms QR.RefreshImage
        var height = Source.Height + 50;
        using var font = new SKFont(SKTypeface.Default, 9);
        using var qrImage = QRImageUtil.GetQRImageExtended(font, Source, Sprite, width, height, Lines, ExtraText);

        var composed = qrImage.ToAvaloniaBitmap();
        Composed.Add(composed);
        this.FindControl<Image>("PB_QR")!.Source = composed;
        this.FindControl<TextBlock>("L_Info")!.Text = string.Join("\n", Lines) + ExtraText;
    }

    private void ReleaseImages()
    {
        foreach (var image in Composed)
        {
            if (!ReferenceEquals(image, CopiedToClipboard))
                image.Dispose();
        }
        Composed.Clear();
        Generated?.Dispose();
        Generated = null;
    }

    private async Task CopyImage()
    {
        try
        {
            if (Clipboard is { } clipboard && this.FindControl<Image>("PB_QR")!.Source is AvaloniaBitmap image)
            {
                CopiedToClipboard = image;
                await clipboard.SetBitmapAsync(image);
            }
        }
        catch (Exception ex)
        {
            await Services.AppDialogs.Error(this, MessageStrings.MsgClipboardFailWrite, ex);
        }
    }
}
