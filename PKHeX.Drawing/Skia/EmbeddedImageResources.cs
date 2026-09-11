using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SkiaSharp;

namespace PKHeX.Drawing;

/// <summary>
/// Cross-platform replacement for the WinForms <c>.resx</c> image resource lookup.
/// Images are embedded directly as PNG manifest resources (logical name = file name without extension).
/// </summary>
/// <remarks>
/// Mirrors the behavior of <c>ResourceManager.GetObject</c> for bitmaps: every lookup returns a new bitmap instance,
/// so callers may freely mutate the result.
/// The resx resource names replace '-' with '_' (e.g. <c>a_1007-1.png</c> is exposed as <c>a_1007_1</c>); the same normalization is applied here.
/// </remarks>
public sealed class EmbeddedImageResources
{
    private readonly Assembly Assembly;
    private readonly Dictionary<string, string> Names;

    public EmbeddedImageResources(Assembly assembly)
    {
        Assembly = assembly;
        var names = assembly.GetManifestResourceNames();
        Names = new Dictionary<string, string>(names.Length, StringComparer.Ordinal);
        foreach (var name in names)
            Names[NormalizeName(name)] = name;
    }

    private static string NormalizeName(string name) => name.Replace('-', '_');

    /// <summary>
    /// Checks if a resource with the requested name exists.
    /// </summary>
    public bool Contains(string name) => Names.ContainsKey(NormalizeName(name));

    /// <summary>
    /// Gets a new decoded bitmap for the requested resource name, or null if it does not exist.
    /// </summary>
    public SKBitmap? GetObject(string name)
    {
        if (!Names.TryGetValue(NormalizeName(name), out var resourceName))
            return null;
        using var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;
        return Decode(stream);
    }

    /// <summary>
    /// Gets a new decoded bitmap for the requested resource name.
    /// </summary>
    /// <exception cref="ArgumentException">Resource does not exist.</exception>
    public SKBitmap GetRequired(string name) => GetObject(name) ?? throw new ArgumentException($"Image resource not found: {name}", nameof(name));

    private static SKBitmap? Decode(Stream stream)
    {
        var length = checked((int)stream.Length);
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            stream.ReadExactly(buffer, 0, length);
            return ImageUtil.Decode(buffer.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
