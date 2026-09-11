using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Settings;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Drawing.Misc;
using PKHeX.Drawing.PokeSprite;
using Bitmap = global::Avalonia.Media.Imaging.Bitmap;

namespace PKHeX.Avalonia.Controls.Hover;

/// <summary>
/// Rich hover card for a slot: ball + name + gender header, the Showdown paste, the move list with type icons,
/// the first legality hint and the encounter summary (port of the WinForms <c>PokePreview</c>).
/// </summary>
/// <remarks>
/// The WinForms form is a custom-painted, non-activating top-most window positioned with <c>SetWindowPos</c>.
/// Avalonia's tooltip already shows without taking focus and follows the pointer, so this is the tooltip's content
/// instead of a window; the section order, paddings and colours follow the WinForms painter.
/// </remarks>
public sealed class SlotPreviewView : Border
{
    private const int IconSize = 24;

    private static System.Drawing.Color IllegalTextColor => ColorUtilAvalonia.ColorWarn;

    private readonly StackPanel Body = new() { Orientation = Orientation.Vertical, Margin = new Thickness(4, 2) };

    public SlotPreviewView()
    {
        BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x80, 0x80, 0x80));
        BorderThickness = new Thickness(1);
        Padding = new Thickness(0);
        MaxWidth = 640;
    }

    /// <summary>
    /// Builds the card for the given entity.
    /// </summary>
    public void Populate(PKM pk, in BattleTemplateExportSettings settings, in LegalityLocalizationContext ctx, HoverSettings hover)
    {
        var root = new StackPanel { Orientation = Orientation.Vertical };
        root.Children.Add(BuildHeader(pk, settings, hover));
        root.Children.Add(Separator());

        Body.Children.Clear();
        var (before, mid, after) = GetBeforeAndAfter(pk, ctx, settings, hover);

        AppendTextSection(Body, before, hover.PreviewShowPaste, null);
        AppendMoves(Body, pk, ctx.Analysis, settings);
        AppendTextSection(Body, mid, hover.HoverSlotShowLegalityHint, IllegalTextColor.ToBrush());
        root.Children.Add(Body);

        if (hover.HoverSlotShowEncounter && after.Length != 0)
        {
            root.Children.Add(Separator());
            var encounter = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4, 2) };
            AppendTextSection(encounter, after, true, null);
            root.Children.Add(encounter);
        }

        Child = root;
    }

    private static Control Separator() => new Border
    {
        Height = 1,
        Background = new SolidColorBrush(Color.FromArgb(0x60, 0x80, 0x80, 0x80)),
    };

    private static Control BuildHeader(PKM pk, in BattleTemplateExportSettings settings, HoverSettings hover)
    {
        var panel = new DockPanel { Margin = new Thickness(4), LastChildFill = true };

        if (pk.Format > 2 && GetBallImage(pk) is { } ball)
        {
            var img = new Image { Source = ball, Width = IconSize, Height = IconSize, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 2, 0) };
            DockPanel.SetDock(img, Dock.Left);
            panel.Children.Add(img);
        }

        if ((pk.Format != 1 || MainWindow.Settings.EntityEditor.ShowGenderGen1) && GetGenderImage(pk) is { } gender)
        {
            var img = new Image { Source = gender, Width = IconSize, Height = IconSize, Stretch = Stretch.Uniform, Margin = new Thickness(2, 0, 0, 0) };
            DockPanel.SetDock(img, Dock.Right);
            panel.Children.Add(img);
        }

        panel.Children.Add(new TextBlock
        {
            Text = GetNameTitle(pk, settings),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = FontWeight.SemiBold,
        });
        return panel;
    }

    private static void AppendMoves(Panel host, PKM pk, LegalityAnalysis la, in BattleTemplateExportSettings settings)
    {
        if (pk.MoveCount == 0)
            return;

        var context = pk.Context;
        var strings = settings.Localization.Strings;
        var names = strings.movelist;
        var checks = la.Info.Moves;

        var moves = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 4, 0, 8) };
        AppendMoveLine(moves, pk, strings, names, context, pk.Move1, checks[0].Valid);
        AppendMoveLine(moves, pk, strings, names, context, pk.Move2, checks[1].Valid);
        AppendMoveLine(moves, pk, strings, names, context, pk.Move3, checks[2].Valid);
        AppendMoveLine(moves, pk, strings, names, context, pk.Move4, checks[3].Valid);
        if (moves.Children.Count != 0)
            host.Children.Add(moves);
    }

    private static void AppendMoveLine(Panel host, PKM pk, GameStrings strings, ReadOnlySpan<string> names, EntityContext context, ushort move, bool valid)
    {
        if (move == 0 || move >= names.Length)
            return;

        byte type = MoveInfo.GetType(move, context);
        var name = names[move];
        if (move == (int)Move.HiddenPower && pk.Context is not EntityContext.Gen8a)
        {
            if (HiddenPower.TryGetTypeIndex(pk.HPType, out type))
                name = $"{name} ({strings.types[type]}) [{pk.HPPower}]";
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Height = IconSize };
        if (TypeSpriteUtil.GetTypeSpriteIconSmall(type) is { } icon)
            row.Children.Add(new Image { Source = icon.ToAvaloniaBitmapAndDispose(), Height = IconSize, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(Line(name, valid ? null : IllegalTextColor.ToBrush(), VerticalAlignment.Center));
        host.Children.Add(row);
    }

    private static void AppendTextSection(Panel host, ReadOnlySpan<char> text, bool visible, IBrush? color)
    {
        if (!visible || text.Length == 0)
            return;

        var block = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 2) };
        foreach (var line in text.EnumerateLines())
            block.Children.Add(Line(line.ToString(), color));
        host.Children.Add(block);
    }

    /// <remarks>
    /// Assigning <c>null</c> to <see cref="TextBlock.Foreground"/> is a local value of "no brush", which draws
    /// nothing; the property has to be left untouched to inherit the tooltip's colour.
    /// </remarks>
    private static TextBlock Line(string text, IBrush? color, VerticalAlignment align = VerticalAlignment.Top)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.NoWrap, VerticalAlignment = align };
        if (color is not null)
            block.Foreground = color;
        return block;
    }

    private static string GetNameTitle(PKM pk, in BattleTemplateExportSettings settings)
    {
        var nick = pk.Nickname;
        var strings = settings.Localization.Strings;
        var all = strings.Species;
        var species = pk.Species;
        if (species >= all.Count)
            return nick;
        var expect = all[species];
        if (settings.IsTokenInExport(BattleTemplateToken.Nickname))
            return expect;

        if (nick.Equals(expect, StringComparison.OrdinalIgnoreCase))
            return nick;
        return $"{nick} ({expect})";
    }

    private static readonly Dictionary<int, Bitmap?> BallCache = [];
    private static readonly Dictionary<int, Bitmap?> GenderCache = [];

    private static Bitmap? GetBallImage(PKM pk)
    {
        var ball = (byte)Ball.Poke;
        if (pk.Format >= 3)
            ball = pk.Ball;
        if (BallCache.TryGetValue(ball, out var cached))
            return cached;
        return BallCache[ball] = SpriteUtil.GetBallSprite(ball)?.ToAvaloniaBitmapAndDispose();
    }

    private static Bitmap? GetGenderImage(PKM pk)
    {
        if (pk.Format == 1)
            return null;
        int gender = pk.Gender;
        if (gender >= 3)
            gender = 2;
        if (GenderCache.TryGetValue(gender, out var cached))
            return cached;
        var sk = PKHeX.Drawing.Misc.Properties.Resources.ResourceManager.GetObject($"gender_{gender}");
        return GenderCache[gender] = sk?.ToAvaloniaBitmapAndDispose();
    }

    private static (string Before, string Middle, string After) GetBeforeAndAfter(PKM pk, in LegalityLocalizationContext la, in BattleTemplateExportSettings settings, HoverSettings hover)
    {
        var order = settings.Order;
        // Bifurcate the order into two sections; split via Moves.
        var moveIndex = settings.GetTokenIndex(BattleTemplateToken.Moves);
        var before = moveIndex == -1 ? order : order[..moveIndex];
        var after = moveIndex == -1 ? default : order[(moveIndex + 1)..];
        if (before.Length > 0 && before[0] == BattleTemplateToken.FirstLine)
            before = before[1..]; // remove first line token; trust that users don't randomly move it lower in the list.

        var start = SummaryPreviewer.GetPreviewText(pk, settings with { Order = before });
        var end = SummaryPreviewer.GetPreviewText(pk, settings with { Order = after });
        if (settings.IsTokenInExport(BattleTemplateToken.IVs, before))
            TryAppendOtherStats(pk, ref start, settings);
        else if (settings.IsTokenInExport(BattleTemplateToken.IVs, after))
            TryAppendOtherStats(pk, ref end, settings);

        var mid = "";
        if (hover.HoverSlotShowLegalityHint)
            mid = SummaryPreviewer.AppendLegalityHint(la, mid);

        if (hover.HoverSlotShowEncounter)
            end = SummaryPreviewer.AppendEncounterInfo(la, end);

        return (start, mid, end);
    }

    public static void TryAppendOtherStats(PKM pk, ref string line, in BattleTemplateExportSettings settings)
    {
        if (pk is IGanbaru g)
            AppendGanbaru(g, ref line, settings);
        if (pk is IAwakened a)
            AppendAwakening(a, ref line, settings);
        if (!settings.IsTokenInExport(BattleTemplateToken.SPs) && settings.IncludeChampionsStatPoints)
            AppendChampionsSP(pk, ref line, settings);
    }

    private static void AppendChampionsSP(PKM pk, ref string line, in BattleTemplateExportSettings settings)
    {
        Span<int> EVs = stackalloc int[6];
        pk.GetEVs(EVs);
        EffortValues.ConvertToChampions(EVs, EVs);
        TryAppend(EVs, ref line, settings, BattleTemplateToken.SPs, settings.StatsSPs);
    }

    private static void AppendGanbaru(IGanbaru g, ref string line, in BattleTemplateExportSettings settings)
    {
        Span<byte> stats = stackalloc byte[6];
        g.GetGVs(stats);
        TryAppend(stats, ref line, settings, BattleTemplateToken.GVs, settings.StatsOther);
    }

    private static void AppendAwakening(IAwakened a, ref string line, in BattleTemplateExportSettings settings)
    {
        Span<byte> stats = stackalloc byte[6];
        a.GetAVs(stats);
        TryAppend(stats, ref line, settings, BattleTemplateToken.AVs, settings.StatsOther);
    }

    private static void TryAppend<T>(ReadOnlySpan<T> stats, ref string line, in BattleTemplateExportSettings settings, BattleTemplateToken token, StatDisplayStyle style) where T : unmanaged, IEquatable<T>
    {
        var localization = settings.Localization;
        var statNames = localization.Config.GetStatDisplay(style);
        var value = ShowdownSet.GetStringStats(stats, default, statNames);
        if (value.Length == 0)
            return;
        var result = localization.Config.Push(token, value);
        line += Environment.NewLine + result;
    }
}
