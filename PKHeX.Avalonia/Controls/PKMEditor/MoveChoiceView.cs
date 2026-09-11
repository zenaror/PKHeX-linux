using System;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Drawing;
using PKHeX.Core;
using PKHeX.Drawing.Misc;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Single move entry: type icon, move selector, PP and PP Ups (port of the WinForms <c>MoveChoice</c>).
/// </summary>
public sealed class MoveChoiceView : StackPanel
{
    private EntityContext Context;

    public readonly Image PB_Type = UiFactory.Picture("PB_Type", 16);
    public readonly ComboBox CB_Move = UiFactory.Combo("CB_Move", 150);
    public readonly NumericTextBox TB_PP = UiFactory.Numeric("TB_PP", 3, 40);
    public readonly ComboBox CB_PPUps = UiFactory.StringCombo("CB_PPUps", 52, "0", "1", "2", "3");
    public readonly Image PB_Triangle = UiFactory.Picture("PB_Triangle", 16);

    private global::Avalonia.Media.Imaging.Bitmap? TypeImage;

    public MoveChoiceView()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 4;
        PB_Type.Width = 20;
        Children.Add(PB_Type);
        Children.Add(CB_Move);
        Children.Add(TB_PP);
        Children.Add(CB_PPUps);
        Children.Add(PB_Triangle);
        CB_PPUps.SelectedIndex = 0;
        CB_Move.SelectionChanged += (_, _) => UpdateTypeSprite(CB_Move.GetValue());
    }

    public ushort SelectedMove { get => (ushort)CB_Move.GetValue(); set => CB_Move.SetValue(value); }
    public int PP { get => SelectedMove == 0 ? 0 : TB_PP.IntValue; set => TB_PP.Text = value.ToString(); }
    public int PPUps { get => SelectedMove == 0 ? 0 : CB_PPUps.SelectedIndex; set => CB_PPUps.SetIndexClamped(value); }
    public bool HideLegality { private get; set; }
    public void SetContext(EntityContext context) => Context = context;

    private void UpdateTypeSprite(int value)
    {
        var old = TypeImage;
        if (value <= 0)
        {
            TypeImage = null;
            PB_Type.Source = null;
        }
        else
        {
            var type = MoveInfo.GetType((ushort)value, Context);
            using var img = TypeSpriteUtil.GetTypeSpriteIconSmall(type);
            TypeImage = img?.ToAvaloniaBitmap();
            PB_Type.Source = TypeImage;
        }
        old?.Dispose();
    }

    public void UpdateLegality(MoveResult move, PKM entity, int i)
    {
        if (HideLegality)
        {
            PB_Triangle.IsVisible = false;
            return;
        }
        PB_Triangle.IsVisible = true;
        PB_Triangle.Source = MoveDisplayState.GetMoveImage(!move.Valid, entity, i);
    }

    public void HealPP(PKM pk)
    {
        var move = SelectedMove;
        var up = PPUps;
        if (move == 0)
            PPUps = up = 0;
        PP = pk.GetMovePP(move, up);
    }
}

/// <summary>
/// Move legality indicator images (port of the WinForms <c>MoveDisplayState</c>).
/// </summary>
public static class MoveDisplayState
{
    private static global::Avalonia.Media.Imaging.Bitmap? Warn, Hint;

    public static global::Avalonia.Media.Imaging.Bitmap? GetMoveImage(bool isIllegal, PKM pk, int index)
    {
        if (isIllegal)
            return Warn ??= PKHeX.Drawing.PokeSprite.Properties.Resources.warn.ToAvaloniaBitmapAndDispose();

        if (MoveInfo.IsDummiedMove(pk, index))
            return Hint ??= PKHeX.Drawing.PokeSprite.Properties.Resources.hint.ToAvaloniaBitmapAndDispose();

        return null;
    }
}
