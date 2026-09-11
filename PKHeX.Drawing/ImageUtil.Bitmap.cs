using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PKHeX.Drawing;

/// <summary>
/// GDI+ (<see cref="Bitmap"/>) specific members of <see cref="ImageUtil"/>.
/// </summary>
public static partial class ImageUtil
{
    extension(Bitmap bmp)
    {
        /// <summary>
        /// Locks the bitmap and returns a span containing its raw pixel data in the specified pixel format.
        /// </summary>
        /// <remarks>
        /// The returned span provides direct access to the bitmap's memory. Modifying the span will update the bitmap.
        /// The caller is responsible for unlocking the bitmap using <see cref="Bitmap.UnlockBits"/> after processing. This method is not thread-safe.
        /// </remarks>
        /// <param name="bmpData">
        /// When this method returns, contains a BitmapData object representing the locked bitmap area.
        /// The caller must unlock the bitmap after processing the data.
        /// </param>
        /// <param name="format">
        /// The pixel format to use when locking the bitmap.
        /// Defaults to <see cref="PixelFormat.Format32bppArgb"/> to ensure the usages within this utility class process pixels in the expected way.
        /// </param>
        /// <returns>A span of bytes representing the bitmap's pixel data. The span covers the entire bitmap in the specified pixel format.</returns>
        public Span<byte> GetBitmapData(out BitmapData bmpData, PixelFormat format = PixelFormat.Format32bppArgb)
        {
            bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, format);
            return GetSpan(bmpData.Scan0, Math.Abs(bmpData.Stride) * bmpData.Height);
        }

        public void GetBitmapData(Span<byte> data, PixelFormat format = PixelFormat.Format32bppArgb)
        {
            var span = bmp.GetBitmapData(out var bmpData, format);
            span.CopyTo(data);
            bmp.UnlockBits(bmpData);
        }

        public void GetBitmapData(Span<int> data)
        {
            var span = bmp.GetBitmapData(out var bmpData);
            var src = MemoryMarshal.Cast<byte, int>(span);
            src.CopyTo(data);
            bmp.UnlockBits(bmpData);
        }

        public void SetBitmapData(ReadOnlySpan<byte> data, PixelFormat format = PixelFormat.Format32bppArgb)
        {
            var span = bmp.GetBitmapData(out var bmpData, format);
            data.CopyTo(span);
            bmp.UnlockBits(bmpData);
        }

        public void SetBitmapData(Span<int> data)
        {
            var span = bmp.GetBitmapData(out var bmpData);
            var dest = MemoryMarshal.Cast<byte, int>(span);
            data.CopyTo(dest);
            bmp.UnlockBits(bmpData);
        }

        public byte[] GetBitmapData()
        {
            var format = bmp.PixelFormat;
            var span = bmp.GetBitmapData(out var bmpData, format);
            try { return [..span]; }
            finally { bmp.UnlockBits(bmpData); }
        }

        public void ToGrayscale(float intensity)
        {
            if (intensity is <= 0.01f or > 1f)
                return; // don't care

            var data = bmp.GetBitmapData(out var bmpData);
            try { SetAllColorToGrayScale(data, intensity); }
            finally { bmp.UnlockBits(bmpData); }
        }

        public void ChangeOpacity(double trans)
        {
            if (trans is <= 0.01f or > 1f)
                return; // don't care

            var data = bmp.GetBitmapData(out var bmpData);
            try { SetAllTransparencyTo(data, trans); }
            finally { bmp.UnlockBits(bmpData); }
        }

        public void BlendTransparentTo(Color c, byte trans, int start = 0, int end = -1)
        {
            var data = bmp.GetBitmapData(out var bmpData);
            if (end == -1)
                end = data.Length;
            try { BlendAllTransparencyTo(data[start..end], c, trans); }
            finally { bmp.UnlockBits(bmpData); }
        }

        public void ChangeAllColorTo(Color c)
        {
            var data = bmp.GetBitmapData(out var bmpData);
            try { ChangeAllColorTo(data, c); }
            finally { bmp.UnlockBits(bmpData); }
        }

        public void ChangeTransparentTo(Color c, byte trans, int start = 0, int end = -1)
        {
            var data = bmp.GetBitmapData(out var bmpData);
            if (end == -1)
                end = data.Length;
            try { SetAllTransparencyTo(data[start..end], c, trans); }
            finally { bmp.UnlockBits(bmpData); }
        }

        public void WritePixels(Color c, int start, int end)
        {
            var data = bmp.GetBitmapData(out var bmpData);
            try { ChangeAllTo(data, c, start, end); }
            finally { bmp.UnlockBits(bmpData); }
        }

        public int GetAverageColor()
        {
            var data = bmp.GetBitmapData(out var bmpData);
            try { return GetAverageColor(data); }
            finally { bmp.UnlockBits(bmpData); }
        }
    }

    private static Span<byte> GetSpan(nint ptr, int length)
        => MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), ptr), length);

    public static Bitmap LayerImage(Bitmap baseLayer, Bitmap overLayer, int x, int y, double transparency)
    {
        overLayer = CopyChangeOpacity(overLayer, transparency);
        return LayerImage(baseLayer, overLayer, x, y);
    }

    public static Bitmap LayerImage(Bitmap baseLayer, Image overLayer, int x, int y)
    {
        var bmp = new Bitmap(baseLayer);
        using var gr = Graphics.FromImage(bmp);
        gr.DrawImage(overLayer, x, y, overLayer.Width, overLayer.Height);
        return bmp;
    }

    public static Bitmap CopyChangeOpacity(Bitmap img, double trans)
    {
        var bmp = (Bitmap)img.Clone();
        bmp.ChangeOpacity(trans);
        return bmp;
    }

    public static Bitmap CopyChangeAllColorTo(Bitmap img, Color c)
    {
        var bmp = (Bitmap)img.Clone();
        bmp.ChangeAllColorTo(c);
        return bmp;
    }

    public static Bitmap CopyChangeTransparentTo(Bitmap img, Color c, byte trans, int start = 0, int end = -1)
    {
        var bmp = (Bitmap)img.Clone();
        bmp.ChangeTransparentTo(c, trans, start, end);
        return bmp;
    }

    public static Bitmap CopyWritePixels(Bitmap img, Color c, int start, int end)
    {
        var bmp = (Bitmap)img.Clone();
        bmp.WritePixels(c, start, end);
        return bmp;
    }

    public static Bitmap GetBitmap(ReadOnlySpan<byte> data, int width, int height, int length, PixelFormat format = PixelFormat.Format32bppArgb)
    {
        var bmp = new Bitmap(width, height, format);
        var span = bmp.GetBitmapData(out var bmpData);
        data[..length].CopyTo(span);
        bmp.UnlockBits(bmpData);
        return bmp;
    }

    public static Bitmap GetBitmap(ReadOnlySpan<byte> data, int width, int height, PixelFormat format = PixelFormat.Format32bppArgb)
    {
        return GetBitmap(data, width, height, data.Length, format);
    }
}
