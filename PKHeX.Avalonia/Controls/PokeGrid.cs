using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Fixed-layout grid of <see cref="SlotView"/> entries with an optional wallpaper background.
/// </summary>
/// <remarks>Port of the WinForms <c>PokeGrid</c>; the layout constants match so that box wallpapers align.</remarks>
public sealed class PokeGrid : Canvas
{
    public readonly List<SlotView> Entries = [];
    public int Slots { get; private set; }

    private int sizeW = 68;
    private int sizeH = 56;

    private Bitmap? _wallpaper;

    public bool InitializeGrid(int width, int height, SpriteBuilder info)
    {
        var newCount = width * height;
        if (Slots == newCount)
        {
            if (info.Width == sizeW && info.Height == sizeH)
                return false;
        }

        sizeW = info.Width;
        sizeH = info.Height;
        Generate(width, height);
        Slots = newCount;

        return true;
    }

    private const int padEdge = 1; // edges
    private const int border = 1; // between

    public static (int Width, int Height) GetGridSize(int width, int height, int spriteWidth, int spriteHeight)
    {
        int w = (2 * padEdge) + border + (width * (spriteWidth + border));
        int h = (2 * padEdge) + border + (height * (spriteHeight + border));
        return (w, h);
    }

    /// <summary>Rows that fit in <paramref name="availableHeight"/> pixels (port of the WinForms <c>PokeGrid.GetMaxRowCount</c>).</summary>
    public static int GetMaxRowCount(int availableHeight, int spriteHeight)
    {
        const int heightOffset = (2 * padEdge) + border;
        var rowHeight = spriteHeight + border;
        if (rowHeight <= 0)
            return 1;
        return System.Math.Max(1, (availableHeight - heightOffset) / rowHeight);
    }

    /// <summary>
    /// Row count for a database sprite grid: the requested count clamped to [5, 20] and then capped to what fits on
    /// the window's screen, as the WinForms database forms do in their <c>GetGridHeight</c>.
    /// </summary>
    /// <param name="window">Window the grid lives in; its screen bounds the grid.</param>
    /// <param name="requestedRows">Row count from the settings.</param>
    /// <param name="nonGridHeight">Vertical space the window needs for everything except the grid.</param>
    public static int GetDatabaseRowCount(Window window, int requestedRows, int nonGridHeight)
    {
        requestedRows = System.Math.Clamp(requestedRows, 5, 20);
        var screens = window.Screens;
        var screen = screens.Primary ?? (screens.All.Count != 0 ? screens.All[0] : null);
        if (screen is null)
            return requestedRows; // no screen info (headless); trust the setting

        var available = (int)(screen.WorkingArea.Height / screen.Scaling) - nonGridHeight;
        var maxRows = GetMaxRowCount(available, SpriteUtil.Spriter.Height);
        return System.Math.Max(1, System.Math.Min(requestedRows, maxRows));
    }

    private void Generate(int width, int height)
    {
        Children.Clear();
        foreach (var c in Entries)
            c.Sprite = null;
        Entries.Clear();

        int colWidth = sizeW;
        int rowHeight = sizeH;

        for (int row = 0; row < height; row++)
        {
            var y = padEdge + (row * (rowHeight + border));
            for (int column = 0; column < width; column++)
            {
                var x = padEdge + (column * (colWidth + border));
                var pb = new SlotView(sizeW, sizeH) { Name = $"Pokémon Grid Row {row:00} Column {column:00}" };
                SetLeft(pb, x);
                SetTop(pb, y);
                Entries.Add(pb);
                Children.Add(pb);
            }
        }

        var (w, h) = GetGridSize(width, height, colWidth, rowHeight);
        Width = w;
        Height = h;
    }

    /// <summary>
    /// Sets the wallpaper image; ownership is transferred.
    /// </summary>
    public void SetBackground(SkiaSharp.SKBitmap img)
    {
        if (App.IsDarkModeEnabled)
        {
            var faded = PKHeX.Drawing.ImageUtil.CopyChangeOpacity(img, 0.5);
            img.Dispose();
            img = faded;
        }
        var bmp = Drawing.SkiaBitmapExtensions.ToAvaloniaBitmapAndDispose(img);
        var old = _wallpaper;
        _wallpaper = bmp;
        // The WinForms grids set BackgroundImageLayout = Stretch, so the wallpaper covers the whole grid once.
        Background = new ImageBrush(bmp)
        {
            TileMode = TileMode.None,
            Stretch = Stretch.Fill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center,
        };
        old?.Dispose();
    }

    public void ClearBackground()
    {
        Background = null;
        _wallpaper?.Dispose();
        _wallpaper = null;
    }
}
