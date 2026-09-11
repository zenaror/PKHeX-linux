using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;

namespace PKHeX.Avalonia.Views.SaveEditors;

/// <summary>
/// Base window for save sub-editors: content area plus Save / Cancel buttons, translated with the WinForms form name.
/// </summary>
public abstract class SaveEditorWindow : Window
{
    protected readonly Button B_Save = UiFactory.Button("B_Save", "Save");
    protected readonly Button B_Cancel = UiFactory.Button("B_Cancel", "Cancel");
    /// <summary>Bottom button bar; derived editors may insert extra controls before the Save/Cancel buttons.</summary>
    protected readonly StackPanel ButtonBar = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };
    private readonly DockPanel Root = new() { Margin = new Thickness(10) };

    protected SaveEditorWindow(string formName, string title)
    {
        Name = formName;
        Title = title;
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;

        B_Save.MinWidth = B_Cancel.MinWidth = 80;
        B_Save.Padding = B_Cancel.Padding = new Thickness(8, 4);
        ButtonBar.Children.Add(B_Cancel);
        ButtonBar.Children.Add(B_Save);
        DockPanel.SetDock(ButtonBar, Dock.Bottom);
        Root.Children.Add(ButtonBar);
        Content = Root;

        B_Cancel.Click += (_, _) => Close();
        B_Save.Click += (_, _) => OnSave();
        // WinForms closes these forms with Escape through the form's CancelButton.
        KeyDown += (_, e) =>
        {
            if (e.Key != global::Avalonia.Input.Key.Escape)
                return;
            e.Handled = true;
            Close();
        };
    }

    /// <summary>
    /// Sets the editor body and applies the current language.
    /// </summary>
    /// <summary>The editor body assigned by <see cref="SetBody"/>.</summary>
    protected Control? Body { get; private set; }

    protected void SetBody(Control body)
    {
        Body = body;
        Root.Children.Add(body); // last child fills the remaining space
        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
    }

    protected abstract void OnSave();
}
