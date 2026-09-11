using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PKHeX.Avalonia.Drawing;
using PKHeX.Core;
using PKHeX.Drawing.Misc;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen9.Donuts;

/// <summary>
/// Radar chart of the five flavor values of a donut (port of the WinForms <c>DonutFlavorProfile9a</c>).
/// </summary>
/// <remarks>
/// The WinForms control paints the polygon with <c>Graphics.FillPolygon</c>; here the same geometry is handed to the
/// Avalonia renderer. The design size and the label placement match the WinForms designer so the artwork lines up.
/// </remarks>
public sealed class DonutFlavorProfile9aView : Canvas
{
    private const int MaxStatValue = 760;
    private const int PentagonWidth = 110;
    private const int PentagonHeight = 110;

    // WinForms control size; the paint code scales against 276x240 (the designer's reference size).
    private const double DesignWidth = 277;
    private const double DesignHeight = 237;
    private const double ReferenceWidth = 276;
    private const double ReferenceHeight = 240;

    private readonly int[] FlavorProfileStats = new int[5];
    private readonly PentagonView Pentagon = new();

    private readonly TextBlock L_StatSpicy = Stat("L_StatSpicy", 117, 36);
    private readonly TextBlock L_StatSour = Stat("L_StatSour", 195, 88);
    private readonly TextBlock L_StatFresh = Stat("L_StatFresh", 181, 160);
    private readonly TextBlock L_StatBitter = Stat("L_StatBitter", 49, 160);
    private readonly TextBlock L_StatSweet = Stat("L_StatSweet", 40, 88);

    public DonutFlavorProfile9aView()
    {
        Width = DesignWidth;
        Height = DesignHeight;

        if (DonutSpriteUtil.GetFlavorProfileImage() is { } background)
            Background = new ImageBrush(background.ToAvaloniaBitmapAndDispose()) { Stretch = Stretch.Fill };

        Pentagon.Width = DesignWidth;
        Pentagon.Height = DesignHeight;
        SetLeft(Pentagon, 0);
        SetTop(Pentagon, 0);
        Children.Add(Pentagon);

        // Labels sit above the polygon, as the WinForms child controls do.
        Add(NameLabel("L_NameSpicy", "Spicy", 0xFF, 0xC0, 0xC0), 117, 18);
        Add(NameLabel("L_NameSour", "Sour", 0xFF, 0xE0, 0xC0), 197, 71);
        Add(NameLabel("L_NameFresh", "Fresh", 0xC0, 0xFF, 0xC0), 181, 143);
        Add(NameLabel("L_NameBitter", "Bitter", 0xC0, 0xC0, 0xFF), 47, 143);
        Add(NameLabel("L_NameSweet", "Sweet", 0xFF, 0xC0, 0xFF), 37, 71);

        foreach (var label in new[] { L_StatSpicy, L_StatSour, L_StatFresh, L_StatBitter, L_StatSweet })
            Children.Add(label);
    }

    private void Add(TextBlock label, double x, double y)
    {
        SetLeft(label, x);
        SetTop(label, y);
        Children.Add(label);
    }

    private static TextBlock NameLabel(string name, string text, byte r, byte g, byte b) => new()
    {
        Name = name,
        Text = text,
        FontSize = 14.4, // 10.8pt
        FontWeight = FontWeight.Bold,
        Foreground = new SolidColorBrush(Color.FromRgb(r, g, b)),
    };

    private static TextBlock Stat(string name, double x, double y)
    {
        var label = new TextBlock
        {
            Name = name,
            Text = "0",
            FontSize = 18.4, // 13.8pt
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
        };
        SetLeft(label, x);
        SetTop(label, y);
        return label;
    }

    /// <summary>
    /// Recomputes and displays the flavor values of the supplied donut.
    /// </summary>
    public void LoadFromDonut(Donut9a donut)
    {
        Span<int> flavorStats = stackalloc int[5];
        donut.RecalculateDonutFlavors(flavorStats);

        FlavorProfileStats[0] = flavorStats[0]; // Spicy - top
        FlavorProfileStats[1] = flavorStats[4]; // Sour - top-right
        FlavorProfileStats[2] = flavorStats[1]; // Fresh - bottom-right
        FlavorProfileStats[3] = flavorStats[3]; // Bitter - bottom-left
        FlavorProfileStats[4] = flavorStats[2]; // Sweet - top-left

        UpdateStatLabels(flavorStats);

        FlavorProfileStats.CopyTo(Pentagon.Stats, 0);
        Pentagon.InvalidateVisual();
    }

    private void UpdateStatLabels(ReadOnlySpan<int> flavorStats)
    {
        L_StatSpicy.Text = flavorStats[0].ToString();
        L_StatFresh.Text = flavorStats[1].ToString();
        L_StatSweet.Text = flavorStats[2].ToString();
        L_StatBitter.Text = flavorStats[3].ToString();
        L_StatSour.Text = flavorStats[4].ToString();
    }

    /// <summary>Draws the filled stat polygon over the chart artwork.</summary>
    private sealed class PentagonView : Control
    {
        public int[] Stats { get; } = new int[5];

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
            var scaleX = Bounds.Width / ReferenceWidth;
            var scaleY = Bounds.Height / ReferenceHeight;
            var radiusX = (PentagonWidth / 2d) * scaleX;
            var radiusY = (PentagonHeight / 2d) * scaleY;

            Span<Point> calculated = stackalloc Point[5];
            for (int i = 0; i < calculated.Length; i++)
                calculated[i] = center + GetStatCoordinate(radiusX, radiusY, Stats[i], i);

            ReadOnlySpan<int> counterClockwiseOrder = [0, 4, 3, 2, 1];
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(calculated[counterClockwiseOrder[0]], isFilled: true);
                for (int i = 1; i < counterClockwiseOrder.Length; i++)
                    ctx.LineTo(calculated[counterClockwiseOrder[i]]);
                ctx.EndFigure(true);
            }
            context.DrawGeometry(Brushes.Yellow, null, geometry);
        }

        private static Vector GetStatCoordinate(double radiusX, double radiusY, int statValue, int i)
        {
            int statMax = statValue switch
            {
                <= 350 => statValue + 200,
                <= 700 => ((statValue + 99) / 100) * 100,
                _ => MaxStatValue,
            };

            var scale = statMax > 0 ? Math.Min((double)statValue / statMax, 1.0) : 0d;

            // Use baseline scale (10%) if stat is 0, otherwise use calculated scale
            const double baselineScale = 0.10;
            if (scale == 0d)
                scale = baselineScale;

            const double angleStep = 2 * Math.PI / 5;
            var angle = (-Math.PI / 2) + (i * angleStep);
            return new Vector(radiusX * scale * Math.Cos(angle), radiusY * scale * Math.Sin(angle));
        }
    }
}
