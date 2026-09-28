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
    /// <summary>
    /// Body above, button bar below. A grid rather than a <see cref="DockPanel"/>, because the bar has to be the
    /// <em>last</em> child for the tab order (Avalonia walks siblings in tree order) while still being laid out at the
    /// bottom, and <see cref="DockPanel.LastChildFillProperty"/> would give that last child the remaining space.
    /// </summary>
    private readonly Grid Root = new()
    {
        Margin = new Thickness(10),
        RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)],
    };

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
        Grid.SetRow(ButtonBar, 1);
        Root.Children.Add(ButtonBar); // SetBody inserts the body before it, so the fields come first in the tab order
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
        Grid.SetRow(body, 0);
        // Inserted before the button bar: Avalonia tabs through siblings in tree order, and WinForms reaches
        // Cancel/Save after the editor fields.
        Root.Children.Insert(0, body);
        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
    }

    protected abstract void OnSave();
}
