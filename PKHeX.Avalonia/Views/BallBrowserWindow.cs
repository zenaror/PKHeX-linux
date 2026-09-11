using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Ball picker (port of the WinForms <c>BallBrowser</c>).
/// </summary>
public sealed class BallBrowserWindow : Window
{
    public bool WasBallChosen { get; private set; }
    public byte BallChoice { get; private set; }

    private readonly WrapPanel flp = new() { Orientation = Orientation.Horizontal, Margin = new global::Avalonia.Thickness(8), MaxWidth = 5 * 36 };
    private readonly global::Avalonia.Media.Imaging.Bitmap SetBackground = SpriteUtil.Spriter.Set.ToAvaloniaBitmap();
    private readonly global::Avalonia.Media.Imaging.Bitmap DeleteBackground = SpriteUtil.Spriter.Delete.ToAvaloniaBitmap();

    public BallBrowserWindow()
    {
        Icon = AppIcon.Get();
        Title = "Ball";
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = flp;
        Closed += (_, _) => { SetBackground.Dispose(); DeleteBackground.Dispose(); };
    }

    public void LoadBalls(PKM pk)
    {
        Span<Ball> valid = stackalloc Ball[BallApplicator.MaxBallSpanAlloc];
        var legal = BallApplicator.GetLegalBalls(valid, pk);
        LoadBalls(valid[..legal], pk.MaxBallID);
    }

    private void LoadBalls(ReadOnlySpan<Ball> legal, int max)
    {
        Span<bool> flags = stackalloc bool[BallApplicator.MaxBallSpanAlloc];
        foreach (var ball in legal)
            flags[(int)ball] = true;

        int countLegal = 0;
        List<Control> controls = [];
        var names = GameInfo.Sources.BallDataSource;
        for (byte ballID = 1; ballID <= max; ballID++)
        {
            var name = GetBallName(ballID, names);
            var pb = GetBallView(ballID, name, flags[ballID]);
            if (MainWindow.Settings.EntityEditor.ShowLegalBallsFirst && flags[ballID])
                controls.Insert(countLegal++, pb);
            else
                controls.Add(pb);
        }

        foreach (var pb in controls)
            flp.Children.Add(pb);
    }

    private static string GetBallName(byte ballID, IEnumerable<ComboItem> names)
    {
        foreach (var x in names)
        {
            if (x.Value == ballID)
                return x.Text;
        }
        throw new ArgumentOutOfRangeException(nameof(ballID));
    }

    private Control GetBallView(byte ballID, string name, bool valid)
    {
        var img = new Image { Stretch = Stretch.None, Source = SpriteUtil.GetBallSprite(ballID).ToAvaloniaBitmapAndDispose() };
        var pb = new Border
        {
            Name = name,
            Width = 36,
            Height = 36,
            Child = img,
            Focusable = true,
            Cursor = new Cursor(StandardCursorType.Hand),
            Background = new ImageBrush(valid ? SetBackground : DeleteBackground) { TileMode = TileMode.Tile, Stretch = Stretch.None, DestinationRect = new global::Avalonia.RelativeRect(0, 0, 68, 56, global::Avalonia.RelativeUnit.Absolute) },
        };
        ToolTip.SetTip(pb, name);
        pb.PointerEntered += (_, _) => Title = name;
        pb.AttachClickHandled(_ => SelectBall(ballID));
        pb.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                SelectBall(ballID);
        };
        return pb;
    }

    private void SelectBall(byte b)
    {
        BallChoice = b;
        WasBallChosen = true;
        Close();
    }
}
