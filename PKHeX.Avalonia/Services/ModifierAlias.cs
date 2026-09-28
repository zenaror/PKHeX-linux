using Avalonia.Input;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Lets Ctrl+Shift stand in for Alt on a click, because a window manager may never let Alt+click through.
/// </summary>
/// <remarks>
/// <para>
/// Linux port addition. On X11 a window manager takes a passive grab on its window-drag chord, and the press then
/// never reaches the application at all. Cinnamon - the desktop this port targets first - uses
/// <c>org.cinnamon.desktop.wm.preferences mouse-button-modifier</c> = <c>&lt;Alt&gt;</c> by default, so every Alt+click
/// shortcut upstream offers is unreachable there. Measured on Linux Mint: an Alt+click on a box slot changes nothing
/// on screen, while Ctrl+Alt+click (clone a box full) works, because the grab is on that exact chord and not on
/// supersets of it.
/// </para>
/// <para>
/// Ctrl+Shift is offered <em>alongside</em> Alt, never instead of it: Alt+click keeps working wherever the desktop
/// leaves it alone - GNOME and KDE bind Super by default, and Cinnamon can be set to Super with
/// <c>gsettings set org.cinnamon.desktop.wm.preferences mouse-button-modifier '&lt;Super&gt;'</c>. Ctrl+Shift is free
/// in every place this is used, and the same actions remain on the slot context menu, which needs no modifier.
/// </para>
/// </remarks>
public static class ModifierAlias
{
    /// <summary>The chord that stands in for <see cref="KeyModifiers.Alt"/>.</summary>
    private const KeyModifiers AltAlias = KeyModifiers.Control | KeyModifiers.Shift;

    /// <summary>
    /// Reads <paramref name="modifiers"/> as Alt when they are the stand-in chord, and returns them unchanged
    /// otherwise, so a caller can keep comparing against <see cref="KeyModifiers.Alt"/>.
    /// </summary>
    public static KeyModifiers Normalize(this KeyModifiers modifiers) => modifiers == AltAlias ? KeyModifiers.Alt : modifiers;

    /// <summary>Whether the gesture means Alt: the key itself, or the stand-in chord.</summary>
    public static bool IsAltGesture(this KeyModifiers modifiers) => modifiers.Normalize().HasFlag(KeyModifiers.Alt);
}
