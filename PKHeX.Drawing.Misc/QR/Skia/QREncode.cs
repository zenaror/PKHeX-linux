using System;
using PKHeX.Core;
using QRCoder;
using SkiaSharp;

namespace PKHeX.Drawing.Misc;

/// <summary>
/// Provides methods for generating QR codes from various PKHeX data types.
/// </summary>
/// <remarks>Cross-platform (SkiaSharp) implementation; the GDI+ implementation is in <c>QR/QREncode.cs</c>.</remarks>
public static class QREncode
{
    /// <summary>
    /// Generates a QR code bitmap from a <see cref="DataMysteryGift"/> object.
    /// </summary>
    /// <param name="mg">The mystery gift data to encode.</param>
    /// <returns>A bitmap containing the QR code.</returns>
    public static SKBitmap GenerateQRCode(DataMysteryGift mg) => GenerateQRCode(QRMessageUtil.GetMessage(mg));

    /// <summary>
    /// Generates a QR code bitmap from a <see cref="PKM"/> object.
    /// </summary>
    /// <param name="pk">The PKM data to encode.</param>
    /// <returns>A bitmap containing the QR code.</returns>
    public static SKBitmap GenerateQRCode(PKM pk) => GenerateQRCode(QRMessageUtil.GetMessage(pk));

    /// <summary>
    /// Generates a QR code bitmap for a Generation 7 PKM with additional options.
    /// </summary>
    /// <param name="pk7">The Generation 7 PKM data to encode.</param>
    /// <param name="box">The box number for the PKM.</param>
    /// <param name="slot">The slot number in the box.</param>
    /// <param name="copies">The number of copies to encode.</param>
    /// <returns>A bitmap containing the QR code.</returns>
    public static SKBitmap GenerateQRCode7(PK7 pk7, int box = 0, int slot = 0, int copies = 1)
        => GenerateQRCode(QRMessageUtil.GetMessage(pk7, box, slot, copies), ppm: 4);

    /// <summary>
    /// Generates a QR code bitmap from a message string.
    /// </summary>
    /// <param name="msg">The message to encode in the QR code.</param>
    /// <param name="ppm">Pixels per module for the QR code graphic.</param>
    /// <returns>A bitmap containing the QR code.</returns>
    private static SKBitmap GenerateQRCode(string msg, int ppm = 4)
    {
        using var data = QRCodeGenerator.GenerateQrCode(msg, QRCodeGenerator.ECCLevel.Q);
        using var code = new PngByteQRCode(data);
        var png = code.GetGraphic(ppm, [0x00, 0x00, 0x00, 0xFF], [0xFF, 0xFF, 0xFF, 0xFF], drawQuietZones: true);
        return ImageUtil.Decode(png) ?? throw new InvalidOperationException("Failed to decode generated QR code image.");
    }
}
