using System;
using Color = System.Drawing.Color;
using Avalonia.Media.Imaging;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Utility logic for drawing individual Slot views that represent underlying <see cref="PKM"/> data.
/// </summary>
/// <remarks>Port of the WinForms <c>SlotUtil</c>.</remarks>
public static class SlotUtil
{
    private static Bitmap? View, Set, Delete;
    private static SpriteBuilder? CachedFor;

    private static void EnsureBackgrounds()
    {
        var spriter = SpriteUtil.Spriter;
        if (ReferenceEquals(CachedFor, spriter))
            return;
        CachedFor = spriter;
        View = spriter.View.ToAvaloniaBitmap();
        Set = spriter.Set.ToAvaloniaBitmap();
        Delete = spriter.Delete.ToAvaloniaBitmap();
    }

    /// <summary>
    /// Gets the background image for a slot based on the provided <see cref="type"/>.
    /// </summary>
    public static Bitmap? GetTouchTypeBackground(SlotTouchType type)
    {
        EnsureBackgrounds();
        return type switch
        {
            SlotTouchType.None => null,
            SlotTouchType.Get => View,
            SlotTouchType.Set => Set,
            SlotTouchType.Delete => Delete,
            SlotTouchType.Swap => Set,
            SlotTouchType.Undo => Delete,
            SlotTouchType.Redo => Set,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    public static readonly Color GoodDataColor = Color.Transparent;
    public static Color BadDataColor => App.IsDarkModeEnabled ? Color.OrangeRed : Color.Red;

    /// <summary>
    /// Refreshes a <see cref="SlotView"/> with the appropriate display content.
    /// </summary>
    public static void UpdateSlot(SlotView pb, ISlotInfo info, PKM pk, SaveFile sav, SlotVisibilityType flags, SlotTouchType t = SlotTouchType.None)
    {
        pb.BackgroundBitmap = GetTouchTypeBackground(t);
        if (pk.Species == 0) // Nothing in slot
        {
            pb.Sprite = null;
            pb.SetBackColor(GoodDataColor);
            pb.Description = null;
            return;
        }
        if (!pk.Valid) // Invalid
        {
            // Bad Egg present in slot.
            pb.Sprite = null;
            pb.SetBackColor(BadDataColor);
            pb.Description = null;
            return;
        }

        pb.Sprite = GetImage(info, pk, sav, flags).ToAvaloniaBitmapAndDispose();
        pb.SetBackColor(GoodDataColor);

        // Get an accessible description for the slot (also used as hover text)
        var x = MainWindow.Settings;
        if (!x.Hover.HoverSlotShowText)
        {
            pb.Description = null;
            return;
        }
        var programLanguage = Language.GetLanguageValue(x.Startup.Language);
        var cfg = x.BattleTemplate;
        var settings = cfg.Hover.GetSettings(programLanguage, pk.Context);
        pb.Description = ShowdownParsing.GetLocalizedPreviewText(pk, settings);
    }

    private static SkiaSharp.SKBitmap GetImage(ISlotInfo info, PKM pk, SaveFile sav, SlotVisibilityType flags) => info switch
    {
        SlotInfoBox b => pk.Sprite(sav, b.Box, b.Slot, flags, b.Type),
        SlotInfoParty ps => pk.Sprite(sav, -1, ps.Slot, flags, ps.Type),
        _ => pk.Sprite(sav, -1, -1, flags, info.Type),
    };
}
