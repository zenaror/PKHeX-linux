using PKHeX.Drawing;
using SkiaSharp;

namespace PKHeX.Drawing.Misc.Properties;

/// <summary>
/// Cross-platform (net10.0) image resource accessor.
/// </summary>
/// <remarks>
/// Replaces the generated <c>Resources.Designer.cs</c> (GDI+ / resx) for the SkiaSharp target.
/// Only the members referenced by the shared logic are exposed by name; any other resource can be retrieved via <see cref="ResourceManager"/>.
/// Every access returns a new bitmap instance (same semantics as <c>ResourceManager.GetObject</c>).
/// </remarks>
public static class Resources
{
    public static EmbeddedImageResources ResourceManager { get; } = new(typeof(Resources).Assembly);

    private static SKBitmap Get(string name) => ResourceManager.GetRequired(name);

    public static SKBitmap Bag_Key => Get("Bag_Key");
    public static SKBitmap tr_00 => Get("tr_00");

    public static SKBitmap box_wp16xy => Get("box_wp16xy");
    public static SKBitmap box_wp01bdsp => Get("box_wp01bdsp");
    public static SKBitmap box_wp02bdsp => Get("box_wp02bdsp");

    public static SKBitmap donut_uni382 => Get("donut_uni382");
    public static SKBitmap donut_uni383 => Get("donut_uni383");
    public static SKBitmap donut_uni384 => Get("donut_uni384");
    public static SKBitmap donut_uni491 => Get("donut_uni491");
    public static SKBitmap donut_uni807 => Get("donut_uni807");
}
