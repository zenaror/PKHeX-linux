using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Localization;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Shiny leaf / crown flags (port of the WinForms <c>ShinyLeaf</c>).
/// </summary>
public sealed class ShinyLeafView : StackPanel
{
    private readonly ToggleButton[] Flags;
    private readonly Image[] Images;

    public ShinyLeafView()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 2;
        Flags = new ToggleButton[6];
        Images = new Image[6];
        for (int i = 0; i < 6; i++)
        {
            var img = new Image { Stretch = Stretch.None, Source = AppResources.GetImage(i == 5 ? "crown" : "leaf") };
            var tb = new ToggleButton { Name = i == 5 ? "CHK_C" : $"CHK_{i + 1}", Content = img, Padding = new global::Avalonia.Thickness(2), MinHeight = 0, MinWidth = 0 };
            Images[i] = img;
            Flags[i] = tb;
            Children.Add(tb);
            tb.IsCheckedChanged += (s, _) => UpdateFlagState(s);
        }
        foreach (var tb in Flags)
            UpdateFlagState(tb);
    }

    private const byte CrownAndFiveLeafs = 0b00_1_11111;
    public void CheckAll(bool all = true) => SetValue(all ? CrownAndFiveLeafs : 0);

    public int GetValue()
    {
        int value = 0;
        for (int i = 0; i < Flags.Length; i++)
        {
            if (Flags[i].IsChecked == true)
                value |= 1 << i;
        }
        return value;
    }

    public void SetValue(int value)
    {
        for (int i = 0; i < Flags.Length; i++)
            Flags[i].IsChecked = ((value >> i) & 1) == 1;
    }

    private void UpdateFlagState(object? sender)
    {
        if (sender is not ToggleButton c)
            return;
        var index = System.Array.IndexOf(Flags, c);
        var crown = Flags[5];
        if (c != crown)
        {
            if (c.IsChecked != true)
                crown.IsChecked = crown.IsEnabled = false;
            else if (HasAllFiveLeafs())
                crown.IsEnabled = true;
        }
        Images[index].Opacity = c.IsChecked == true ? 1.0 : 0.4;
    }

    private bool HasAllFiveLeafs()
    {
        for (int i = 0; i < 5; i++)
        {
            if (Flags[i].IsChecked != true)
                return false;
        }
        return true;
    }
}
