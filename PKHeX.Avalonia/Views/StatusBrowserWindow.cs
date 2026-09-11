using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Status condition picker (port of the WinForms <c>StatusBrowser</c>).
/// </summary>
public sealed class StatusBrowserWindow : Window
{
    public bool WasChosen { get; private set; }
    public StatusCondition Choice { get; private set; }

    private readonly NumericUpDown NUD_Sleep;
    private readonly StackPanel flp = new() { Orientation = Orientation.Vertical, Spacing = 2, Margin = new global::Avalonia.Thickness(8) };

    public StatusBrowserWindow(int generation)
    {
        Icon = AppIcon.Get();
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = flp;

        NUD_Sleep = UiFactory.NumericUpDown("NUD_Sleep", 1, 7, 80);

        Add(GetImage(StatusCondition.None, "None"));
        var sleep = GetImage(StatusCondition.Sleep1, "Sleep");
        if (generation <= 4)
            Add(UiFactory.Row(sleep, NUD_Sleep));
        else
            Add(sleep);
        Add(GetImage(StatusCondition.Poison, "Poison"));
        Add(GetImage(StatusCondition.Burn, "Burn"));
        Add(GetImage(StatusCondition.Paralysis, "Paralysis"));
        Add(GetImage(StatusCondition.Freeze, "Freeze"));
        if (generation is 3 or 4)
            Add(GetImage(StatusCondition.PoisonBad, "Toxic"));
    }

    private void Add(Control c) => flp.Children.Add(c);

    public void LoadList(PKM pk)
    {
        StatusType type;
        if (pk.Format <= 4)
        {
            var condition = (StatusCondition)pk.Status_Condition;
            NUD_Sleep.Value = Math.Max(1, (int)condition & 7);
            type = condition.GetStatusType();
        }
        else
        {
            type = (StatusType)(pk.Status_Condition & 7);
        }

        Title = Translator.TranslateEnum(type, MainWindow.CurrentLanguage);
    }

    private Control GetImage(StatusCondition value, string name)
    {
        var img = value == 0
            ? PKHeX.Drawing.PokeSprite.Properties.Resources.ResourceManager.GetObject("sickfaint")
            : value.GetStatusSprite();
        var pb = new Image { Stretch = Stretch.None, Source = img?.ToAvaloniaBitmapAndDispose() };
        var border = new Border
        {
            Name = name,
            Child = pb,
            Background = Brushes.Transparent,
            Padding = new global::Avalonia.Thickness(4),
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
        };
        ToolTip.SetTip(border, name);
        border.PointerEntered += (_, _) => Title = value is StatusCondition.PoisonBad ? "Toxic" : Translator.TranslateEnum(value.GetStatusType(), MainWindow.CurrentLanguage);
        border.AttachClickHandled(_ =>
        {
            if (value is StatusCondition.Sleep1)
                value = (StatusCondition)(int)(NUD_Sleep.Value ?? 1);
            SelectValue(value);
        });
        border.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                SelectValue(value);
        };
        return border;
    }

    private void SelectValue(StatusCondition value)
    {
        Choice = value;
        WasChosen = true;
        Close();
    }
}
