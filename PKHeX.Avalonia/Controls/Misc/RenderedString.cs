using System;
using Avalonia.Controls;
using Avalonia.Media;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Text box that renders its content with the in-game glyph font (port of the WinForms <c>RenderedString</c>).
/// </summary>
/// <remarks>
/// The font carries the private-use characters the games store in names (gender symbols, the PK/MN pair, ...),
/// which no system font maps.
/// </remarks>
public class RenderedString : TextBox
{
    protected override Type StyleKeyOverride => typeof(TextBox);

    private bool _disableInGameFont;
    private EntityContext _displayContext = FontUtil.DefaultContext;

    public RenderedString() => ApplyFont();

    /// <summary>
    /// Renders with the regular interface font instead of the in-game one.
    /// </summary>
    public bool DisableInGameFont
    {
        get => _disableInGameFont;
        set
        {
            if (value == _disableInGameFont)
                return;
            _disableInGameFont = value;
            ApplyFont();
        }
    }

    /// <summary>
    /// Context the string is rendered in.
    /// </summary>
    public EntityContext DisplayContext
    {
        get => _displayContext;
        set
        {
            if (value == _displayContext)
                return;
            _displayContext = value;
            if (!_disableInGameFont)
                ApplyFont();
        }
    }

    private void ApplyFont()
    {
        if (_disableInGameFont)
        {
            ClearValue(FontFamilyProperty);
            ClearValue(FontSizeProperty);
            return;
        }

        var (family, size) = FontUtil.GetFont(_displayContext);
        if (family is null)
            return; // font resource missing: keep the inherited font rather than failing to render
        FontFamily = family;
        FontSize = size;
    }
}

/// <summary>
/// In-game font lookup (port of the WinForms <c>FontUtil</c>).
/// </summary>
/// <remarks>
/// Avalonia resolves fonts through URIs rather than a private font collection, so the font file ships as an
/// Avalonia resource and is named here; the family name inside the file is what follows the '#'.
/// </remarks>
public static class FontUtil
{
    public const EntityContext DefaultContext = EntityContext.Gen6;

    private static FontFamily? Cached;
    private static bool Failed;

    /// <summary>
    /// Gets the in-game font for a context, or <see langword="null"/> when the resource cannot be loaded.
    /// </summary>
    public static (FontFamily? Family, double Size) GetFont(EntityContext context = DefaultContext)
    {
        var (uri, size) = GetFontUri(context);
        if (Failed)
            return (null, size);
        if (Cached is not null)
            return (Cached, size);

        try
        {
            return (Cached = new FontFamily(uri), size);
        }
        catch (Exception ex)
        {
            // A missing font must not break the editor; the field keeps the interface font.
            System.Diagnostics.Debug.WriteLine($"Unable to load the in-game font: {ex.Message}");
            Failed = true;
            return (null, size);
        }
    }

    // Declare fonts here; any not present will fall back to Gen6.
    // The size follows WinForms' 13pt at 96 DPI, expressed in the device-independent pixels Avalonia uses.
    private static (string Uri, double Size) GetFontUri(EntityContext context) => context switch
    {
        _ => ("avares://PKHeX.Avalonia/Assets/Fonts/PGLDings-NormalRegular.ttf#PGLDings", 13d * 96d / 72d), // Gen6
    };
}
