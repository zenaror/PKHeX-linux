using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Shows every box of the save at once (port of the WinForms <c>SAV_BoxList</c>).
/// </summary>
public sealed class BoxListWindow : Window
{
    private readonly SAVEditorView Editor;
    private readonly List<BoxView> Boxes = [];

    public BoxListWindow(SAVEditorView parent)
    {
        Name = "SAV_BoxList";
        Title = "Box List";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 1200;
        Height = 800;
        Editor = parent;

        var sav = parent.SAV;
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < sav.BoxCount; i++)
        {
            var box = new BoxView { Name = $"BE_Box{i:00}", Margin = new Thickness(2), Host = parent };
            box.InitializeFromSAV(sav);
            box.CanSetCurrentBox = false;
            box.ControlsVisible = true;
            box.ControlsEnabled = false;
            box.ResetBoxNames(i);
            box.ResetSlots();
            Boxes.Add(box);
            panel.Children.Add(box);
            parent.Publisher.Subscribe(box);
        }
        Content = new ScrollViewer { Content = panel };
        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);

        Closed += (_, _) =>
        {
            foreach (var b in Boxes)
                parent.Publisher.Unsubscribe(b);
        };
    }

    /// <summary>Refreshes every box when the save data changed elsewhere.</summary>
    public void ReloadSlots()
    {
        foreach (var b in Boxes)
        {
            b.FlagIllegal = Editor.FlagIllegal;
            b.ResetSlots();
        }
    }
}
