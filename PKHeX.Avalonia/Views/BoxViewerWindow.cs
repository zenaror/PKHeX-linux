using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Second box viewer window (port of the WinForms <c>SAV_BoxViewer</c>).
/// </summary>
public sealed class BoxViewerWindow : Window
{
    private readonly SAVEditorView Editor;
    private readonly BoxView Box = new() { Name = "Box" };
    private readonly Button B_BoxSwap = UiFactory.Button("B_BoxSwap", "⇄");

    public BoxViewerWindow(SAVEditorView parent, int box)
    {
        Name = "SAV_BoxViewer";
        Title = "Box Viewer";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;

        Editor = parent;
        Box.Host = parent;
        ToolTip.SetTip(B_BoxSwap, "Swap with the main box view");

        var root = new DockPanel { Margin = new Thickness(6) };
        DockPanel.SetDock(B_BoxSwap, Dock.Top);
        B_BoxSwap.HorizontalAlignment = HorizontalAlignment.Right;
        root.Children.Add(B_BoxSwap);
        root.Children.Add(Box);
        Content = root;

        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);

        Box.InitializeFromSAV(parent.SAV);
        Box.CanSetCurrentBox = false; // the main view owns the save's current box
        Box.ControlsVisible = true;
        Box.ControlsEnabled = parent.SAV.BoxCount > 1;
        Box.ResetBoxNames(box);
        Box.ResetSlots();

        B_BoxSwap.Click += (_, _) => Box.CurrentBox = Editor.SwapBoxesViewer(Box.CurrentBox);

        parent.Publisher.Subscribe(Box);
        Closed += (_, _) => parent.Publisher.Unsubscribe(Box);
    }

    /// <summary>Refreshes the viewer when the save data changed elsewhere.</summary>
    public void ReloadSlots()
    {
        Box.FlagIllegal = Editor.FlagIllegal;
        Box.ResetSlots();
    }

    /// <summary>Box currently displayed.</summary>
    public int CurrentBox => Box.CurrentBox;
}
