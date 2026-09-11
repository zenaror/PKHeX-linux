using PKHeX.Drawing;
using SkiaSharp;

namespace PKHeX.Drawing.PokeSprite.Properties;

/// <summary>
/// Cross-platform (net10.0) image resource accessor.
/// </summary>
/// <remarks>
/// Replaces the generated <c>Resources.Designer.cs</c> (GDI+ / resx) for the SkiaSharp target.
/// Only the members referenced by the shared sprite logic are exposed by name; any other resource can be retrieved via <see cref="ResourceManager"/>.
/// Every access returns a new bitmap instance (same semantics as <c>ResourceManager.GetObject</c>).
/// </remarks>
public static class Resources
{
    public static EmbeddedImageResources ResourceManager { get; } = new(typeof(Resources).Assembly);

    private static SKBitmap Get(string name) => ResourceManager.GetRequired(name);

    public static SKBitmap _ball4 => Get("_ball4");

    public static SKBitmap b_0 => Get("b_0");
    public static SKBitmap b_unknown => Get("b_unknown");
    public static SKBitmap b_egg => Get("b_egg");
    public static SKBitmap b_490_e => Get("b_490_e");
    public static SKBitmap b_249x => Get("b_249x");
    public static SKBitmap a_egg => Get("a_egg");
    public static SKBitmap a_490_e => Get("a_490_e");

    public static SKBitmap bitem_unk => Get("bitem_unk");
    public static SKBitmap bitem_tm => Get("bitem_tm");
    public static SKBitmap bitem_tr => Get("bitem_tr");
    public static SKBitmap aitem_tm => Get("aitem_tm");

    public static SKBitmap slotHover68 => Get("slotHover68");
    public static SKBitmap slotView68 => Get("slotView68");
    public static SKBitmap slotSet68 => Get("slotSet68");
    public static SKBitmap slotDel68 => Get("slotDel68");
    public static SKBitmap slotTrans68 => Get("slotTrans68");
    public static SKBitmap slotDrag68 => Get("slotDrag68");

    public static SKBitmap rare_icon_alt => Get("rare_icon_alt");
    public static SKBitmap rare_icon_alt_2 => Get("rare_icon_alt_2");
    public static SKBitmap dyna => Get("dyna");
    public static SKBitmap alpha_alt => Get("alpha_alt");

    public static SKBitmap warn => Get("warn");
    public static SKBitmap hint => Get("hint");
    public static SKBitmap valid => Get("valid");
    public static SKBitmap team => Get("team");
    public static SKBitmap locked => Get("locked");
    public static SKBitmap starter => Get("starter");

    public static SKBitmap party1 => Get("party1");
    public static SKBitmap party2 => Get("party2");
    public static SKBitmap party3 => Get("party3");
    public static SKBitmap party4 => Get("party4");
    public static SKBitmap party5 => Get("party5");
    public static SKBitmap party6 => Get("party6");

    public static SKBitmap sicksleep => Get("sicksleep");
    public static SKBitmap sicktoxic => Get("sicktoxic");
    public static SKBitmap sickpoison => Get("sickpoison");
    public static SKBitmap sickburn => Get("sickburn");
    public static SKBitmap sickparalyze => Get("sickparalyze");
    public static SKBitmap sickfrostbite => Get("sickfrostbite");
}
