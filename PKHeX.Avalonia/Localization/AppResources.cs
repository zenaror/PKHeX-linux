using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Avalonia.Media.Imaging;
using PKHeX.Drawing;

namespace PKHeX.Avalonia.Localization;

/// <summary>
/// Access to the embedded frontend resources (program icons, translation files, changelog, shortcuts).
/// </summary>
/// <remarks>
/// The files are linked from <c>PKHeX.WinForms/Resources</c> at build time (see the project file) so that the
/// Linux frontend reuses the upstream assets without duplicating them in the repository.
/// Logical resource names equal the file name without extension.
/// </remarks>
public static class AppResources
{
    private static readonly Assembly Assembly = typeof(AppResources).Assembly;
    private static readonly Dictionary<string, Bitmap?> ImageCache = [];

    private static Stream? Open(string name) => Assembly.GetManifestResourceStream(name);

    /// <summary>
    /// Gets an embedded UTF-8 text resource, or null if it does not exist.
    /// </summary>
    public static string? GetText(string name)
    {
        using var stream = Open(name);
        if (stream is null)
            return null;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Gets an embedded (cached) image resource, or null if it does not exist.
    /// </summary>
    public static Bitmap? GetImage(string name)
    {
        lock (ImageCache)
        {
            if (ImageCache.TryGetValue(name, out var cached))
                return cached;
            using var stream = Open(name);
            var result = stream is null ? null : new Bitmap(stream);
            ImageCache[name] = result;
            return result;
        }
    }

    /// <summary>
    /// Gets an embedded image resource.
    /// </summary>
    /// <exception cref="FileNotFoundException">Resource does not exist.</exception>
    public static Bitmap GetRequiredImage(string name) => GetImage(name) ?? throw new FileNotFoundException($"Embedded image resource not found: {name}");

    /// <summary>
    /// Gets an embedded (cached) image resource with all opaque pixels recolored to white (dark theme icons), or null if it does not exist.
    /// </summary>
    public static Bitmap? GetImageBlackToWhite(string name)
    {
        var key = name + "\0white";
        lock (ImageCache)
        {
            if (ImageCache.TryGetValue(key, out var cached))
                return cached;
            using var stream = Open(name);
            Bitmap? result = null;
            if (stream is not null)
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                using var bmp = ImageUtil.Decode(ms.GetBuffer().AsSpan(0, (int)ms.Length));
                if (bmp is not null)
                {
                    bmp.ChangeAllColorTo(System.Drawing.Color.White);
                    result = Drawing.SkiaBitmapExtensions.ToAvaloniaBitmap(bmp);
                }
            }
            ImageCache[key] = result;
            return result;
        }
    }

    /// <summary>
    /// Decodes an embedded image resource into a new SkiaSharp bitmap (for pixel manipulation), or null if it does not exist.
    /// </summary>
    public static SkiaSharp.SKBitmap? GetSkBitmap(string name)
    {
        using var stream = Open(name);
        if (stream is null)
            return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ImageUtil.Decode(ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    /// <summary>
    /// Gets the program icon as a raw stream.
    /// </summary>
    /// <summary>
    /// Reads an embedded binary payload (fashion unlock blobs and similar raw resources).
    /// </summary>
    public static byte[]? GetBytes(string name)
    {
        using var stream = Open(name);
        if (stream is null)
            return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public static Stream? OpenProgramIcon() => Open("icon");
}
