using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace PKHeX.Drawing;

/// <summary>
/// SkiaSharp (<see cref="SKBitmap"/>) specific members of <see cref="ImageUtil"/>.
/// </summary>
/// <remarks>
/// All bitmaps created by this utility are <see cref="SKColorType.Bgra8888"/> with <see cref="SKAlphaType.Unpremul"/> alpha,
/// which has the same memory layout as GDI+ <c>Format32bppArgb</c>; the shared pixel routines operate on that layout.
/// </remarks>
public static partial class ImageUtil
{
    /// <summary>
    /// Pixel layout used for all sprite bitmaps.
    /// </summary>
    public static SKImageInfo GetImageInfo(int width, int height) => new(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);

    /// <summary>
    /// Creates a new empty (transparent) bitmap with the expected pixel layout.
    /// </summary>
    public static SKBitmap CreateBitmap(int width, int height) => new(GetImageInfo(width, height));

    /// <summary>
    /// Decodes an encoded image (PNG, etc.) into a bitmap with the expected pixel layout.
    /// </summary>
    public static SKBitmap? Decode(ReadOnlySpan<byte> encoded)
    {
        using var data = SKData.CreateCopy(encoded);
        using var codec = SKCodec.Create(data);
        if (codec is null)
            return null;
        var info = codec.Info;
        var bmp = CreateBitmap(info.Width, info.Height);
        var result = codec.GetPixels(bmp.Info, bmp.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bmp.Dispose();
            return null;
        }
        return bmp;
    }

    extension(SKBitmap bmp)
    {
        /// <summary>
        /// Gets a span over the bitmap's raw pixel data (32bpp BGRA).
        /// </summary>
        public Span<byte> GetBitmapSpan()
        {
            EnsureExpectedFormat(bmp);
            return GetSpan(bmp.GetPixels(), bmp.ByteCount);
        }

        public void GetBitmapData(Span<byte> data) => bmp.GetBitmapSpan().CopyTo(data);

        public void GetBitmapData(Span<int> data) => MemoryMarshal.Cast<byte, int>(bmp.GetBitmapSpan()).CopyTo(data);

        public void SetBitmapData(ReadOnlySpan<byte> data) => data.CopyTo(bmp.GetBitmapSpan());

        public void SetBitmapData(Span<int> data) => data.CopyTo(MemoryMarshal.Cast<byte, int>(bmp.GetBitmapSpan()));

        public byte[] GetBitmapData() => [..bmp.GetBitmapSpan()];

        public void ToGrayscale(float intensity)
        {
            if (intensity is <= 0.01f or > 1f)
                return; // don't care
            SetAllColorToGrayScale(bmp.GetBitmapSpan(), intensity);
        }

        public void ChangeOpacity(double trans)
        {
            if (trans is <= 0.01f or > 1f)
                return; // don't care
            SetAllTransparencyTo(bmp.GetBitmapSpan(), trans);
        }

        public void BlendTransparentTo(Color c, byte trans, int start = 0, int end = -1)
        {
            var data = bmp.GetBitmapSpan();
            if (end == -1)
                end = data.Length;
            BlendAllTransparencyTo(data[start..end], c, trans);
        }

        public void ChangeAllColorTo(Color c) => ChangeAllColorTo(bmp.GetBitmapSpan(), c);

        public void ChangeTransparentTo(Color c, byte trans, int start = 0, int end = -1)
        {
            var data = bmp.GetBitmapSpan();
            if (end == -1)
                end = data.Length;
            SetAllTransparencyTo(data[start..end], c, trans);
        }

        public void WritePixels(Color c, int start, int end) => ChangeAllTo(bmp.GetBitmapSpan(), c, start, end);

        public int GetAverageColor() => GetAverageColor(bmp.GetBitmapSpan());

        /// <summary>
        /// Creates an independent copy of the bitmap with the expected pixel layout.
        /// </summary>
        public SKBitmap CloneBitmap()
        {
            var result = CreateBitmap(bmp.Width, bmp.Height);
            bmp.GetBitmapSpan().CopyTo(result.GetBitmapSpan());
            return result;
        }
    }

    private static void EnsureExpectedFormat(SKBitmap bmp)
    {
        if (bmp.ColorType != SKColorType.Bgra8888 || bmp.AlphaType != SKAlphaType.Unpremul)
            throw new InvalidOperationException($"Unexpected bitmap format: {bmp.ColorType}/{bmp.AlphaType}. Expected Bgra8888/Unpremul.");
        if (bmp.RowBytes != bmp.Width * 4)
            throw new InvalidOperationException("Unexpected bitmap row stride; expected tightly packed rows.");
    }

    private static Span<byte> GetSpan(nint ptr, int length)
        => MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), ptr), length);

    public static SKBitmap LayerImage(SKBitmap baseLayer, SKBitmap overLayer, int x, int y, double transparency)
    {
        using var faded = CopyChangeOpacity(overLayer, transparency);
        return LayerImage(baseLayer, faded, x, y);
    }

    /// <summary>
    /// Draws <paramref name="overLayer"/> on top of a copy of <paramref name="baseLayer"/> at the requested offset (source-over blending).
    /// </summary>
    public static SKBitmap LayerImage(SKBitmap baseLayer, SKBitmap overLayer, int x, int y)
    {
        var bmp = baseLayer.CloneBitmap();
        DrawOver(bmp, overLayer, x, y);
        return bmp;
    }

    private static void DrawOver(SKBitmap dest, SKBitmap src, int x, int y)
    {
        var d = MemoryMarshal.Cast<byte, uint>(dest.GetBitmapSpan());
        var s = MemoryMarshal.Cast<byte, uint>(src.GetBitmapSpan());
        int dw = dest.Width, dh = dest.Height;
        int sw = src.Width, sh = src.Height;

        int left = Math.Max(0, x), top = Math.Max(0, y);
        int right = Math.Min(dw, x + sw), bottom = Math.Min(dh, y + sh);
        for (int dy = top; dy < bottom; dy++)
        {
            int sy = dy - y;
            var srcRow = s.Slice(sy * sw, sw);
            var dstRow = d.Slice(dy * dw, dw);
            for (int dx = left; dx < right; dx++)
            {
                var sp = srcRow[dx - x];
                var sa = sp >> 24;
                if (sa == 0)
                    continue;
                ref var dp = ref dstRow[dx];
                if (sa == 0xFF || (dp >> 24) == 0)
                {
                    dp = sp;
                    continue;
                }
                dp = SourceOver(sp, dp);
            }
        }
    }

    /// <summary>
    /// Source-over compositing of two non-premultiplied BGRA pixels.
    /// </summary>
    private static uint SourceOver(uint src, uint dst)
    {
        uint sa = src >> 24, da = dst >> 24;
        uint outA = sa + ((da * (255 - sa) + 127) / 255);
        if (outA == 0)
            return 0;

        uint r = Mix((src >> 16) & 0xFF, (dst >> 16) & 0xFF, sa, da, outA);
        uint g = Mix((src >> 8) & 0xFF, (dst >> 8) & 0xFF, sa, da, outA);
        uint b = Mix(src & 0xFF, dst & 0xFF, sa, da, outA);
        return (outA << 24) | (r << 16) | (g << 8) | b;

        static uint Mix(uint sc, uint dc, uint sa, uint da, uint outA)
        {
            // (sc*sa + dc*da*(1-sa)) / outA, all in 8-bit fixed point
            uint dcWeight = (da * (255 - sa) + 127) / 255;
            return ((sc * sa) + (dc * dcWeight) + (outA / 2)) / outA;
        }
    }

    public static SKBitmap CopyChangeOpacity(SKBitmap img, double trans)
    {
        var bmp = img.CloneBitmap();
        bmp.ChangeOpacity(trans);
        return bmp;
    }

    public static SKBitmap CopyChangeAllColorTo(SKBitmap img, Color c)
    {
        var bmp = img.CloneBitmap();
        bmp.ChangeAllColorTo(c);
        return bmp;
    }

    public static SKBitmap CopyChangeTransparentTo(SKBitmap img, Color c, byte trans, int start = 0, int end = -1)
    {
        var bmp = img.CloneBitmap();
        bmp.ChangeTransparentTo(c, trans, start, end);
        return bmp;
    }

    public static SKBitmap CopyWritePixels(SKBitmap img, Color c, int start, int end)
    {
        var bmp = img.CloneBitmap();
        bmp.WritePixels(c, start, end);
        return bmp;
    }

    public static SKBitmap GetBitmap(ReadOnlySpan<byte> data, int width, int height, int length)
    {
        var bmp = CreateBitmap(width, height);
        data[..length].CopyTo(bmp.GetBitmapSpan());
        return bmp;
    }

    public static SKBitmap GetBitmap(ReadOnlySpan<byte> data, int width, int height)
    {
        return GetBitmap(data, width, height, data.Length);
    }
}
