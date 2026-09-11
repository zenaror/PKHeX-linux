using System;
using PKHeX.Core;
using SkiaSharp;

namespace PKHeX.Avalonia.Drawing;

/// <summary>
/// Pokétch dot-art conversion for the Generation 4 Sinnoh games (port of the WinForms <c>PoketchDotMatrix</c>).
/// </summary>
/// <remarks>
/// The dot matrix is a 24x20 grid of two-bit pixels; the two bits index a four-entry grey ramp. Importing an
/// image quantises it to those four levels by brightness, trying every level permutation and keeping the one
/// with the least total error, exactly as the original does.
/// </remarks>
public static class PoketchDotMatrix
{
    public const int DotMatrixHeight = 20;
    public const int DotMatrixWidth = 24;
    public const int DotMatrixPixelCount = DotMatrixHeight * DotMatrixWidth;
    public const int DotMatrixUpscaleFactor = 4;

    public static ReadOnlySpan<byte> ColorTable => [248, 168, 88, 8];

    public static bool TryBuild(string fileName, Span<byte> result)
    {
        if (FileUtil.GetFileSize(fileName) > 0x80A)
            return false;

        using var bmp = SKBitmap.Decode(fileName);
        if (bmp is null || bmp.Width != DotMatrixWidth || bmp.Height != DotMatrixHeight)
            return false;

        Span<byte> brightMap = stackalloc byte[DotMatrixPixelCount];
        Span<byte> brightCount = stackalloc byte[0x100];
        if (!TryBuildBrightMap(bmp, brightMap, brightCount, out int colorCount))
            return false;

        Build(colorCount, brightCount, brightMap, result);
        return true;
    }

    private static bool TryBuildBrightMap(SKBitmap bmp, Span<byte> brightMap, Span<byte> brightCount, out int colorCount)
    {
        for (int iy = 0; iy < DotMatrixHeight; iy++)
        {
            for (int ix = 0; ix < DotMatrixWidth; ix++)
            {
                var ig = GetBrightness(bmp.GetPixel(ix, iy));
                brightMap[ix + (DotMatrixWidth * iy)] = ig;
                brightCount[ig]++;
            }
        }
        colorCount = brightCount.Length - brightCount.Count<byte>(0);
        return (colorCount - 1) <= 3; // 1-4
    }

    /// <summary>Matches <c>System.Drawing.Color.GetBrightness</c>: the HSL lightness, scaled to a byte.</summary>
    private static byte GetBrightness(SKColor c)
    {
        int max = Math.Max(c.Red, Math.Max(c.Green, c.Blue));
        int min = Math.Min(c.Red, Math.Min(c.Green, c.Blue));
        return (byte)(0xFF * ((max + min) / 2f / 255f));
    }

    private static void Build(int colorCount, Span<byte> brightCount, Span<byte> brightMap, Span<byte> result)
    {
        int errorsMin = int.MaxValue;
        Span<byte> LCT = stackalloc byte[4];
        for (int i = 0; i < LCT.Length; i++)
            LCT[i] = (byte)(colorCount < i + 1 ? 4 : colorCount - i - 1);

        Span<byte> mLCT = stackalloc byte[4];
        Span<byte> iBrightCount = stackalloc byte[0x100];
        int ee = 0;
        while (++ee < 1000)
        {
            brightCount.CopyTo(iBrightCount);
            for (int i = 0, j = 0; i < iBrightCount.Length; i++)
            {
                if (iBrightCount[i] > 0)
                    iBrightCount[i] = LCT[j++];
            }

            var errorsTotal = 0;
            for (int i = 0; i < DotMatrixPixelCount; i++)
                errorsTotal += Math.Abs(brightMap[i] - ColorTable[iBrightCount[brightMap[i]]]);
            if (errorsMin > errorsTotal)
            {
                errorsMin = errorsTotal;
                LCT.CopyTo(mLCT);
            }
            GetNextLCT(LCT);
            if (LCT[0] >= 4)
                break;
        }
        for (int i = 0, j = 0; i < brightCount.Length; i++)
        {
            if (brightCount[i] > 0)
                brightCount[i] = mLCT[j++];
        }

        for (int i = 0; i < brightMap.Length; i++)
            brightMap[i] = brightCount[brightMap[i]];

        result.Clear();
        for (int i = 0; i < brightMap.Length; i++)
            result[i >> 2] |= (byte)((brightMap[i] & 3) << ((i % 4) << 1));
    }

    private static void GetNextLCT(Span<byte> inp)
    {
        while (true)
        {
            if (++inp[0] < 4)
                continue;

            inp[0] = 0;
            if (++inp[1] < 4)
                continue;

            inp[1] = 0;
            if (++inp[2] < 4)
                continue;

            inp[2] = 0;
            if (++inp[3] < 4)
                continue;

            inp[0] = 4;
            return;
        }
    }

    /// <summary>Renders the two-bit dot art upscaled 4x, as the WinForms picture box shows it.</summary>
    public static SKBitmap GetDotArt(ReadOnlySpan<byte> inp)
    {
        const int width = DotMatrixWidth * DotMatrixUpscaleFactor;
        const int height = DotMatrixHeight * DotMatrixUpscaleFactor;
        var bmp = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        for (int iy = 0; iy < DotMatrixHeight; iy++)
        {
            for (int ix = 0; ix < DotMatrixWidth; ix++)
            {
                var ib = ix + (DotMatrixWidth * iy);
                var ict = ColorTable[(inp[ib >> 2] >> ((ib % 4) << 1)) & 3];
                var color = new SKColor(ict, ict, ict);
                for (int izy = 0; izy < DotMatrixUpscaleFactor; izy++)
                {
                    for (int izx = 0; izx < DotMatrixUpscaleFactor; izx++)
                        bmp.SetPixel((ix * DotMatrixUpscaleFactor) + izx, (iy * DotMatrixUpscaleFactor) + izy, color);
                }
            }
        }
        return bmp;
    }
}
