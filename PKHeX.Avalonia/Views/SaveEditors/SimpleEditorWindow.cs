using Avalonia.Controls;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Views;

namespace PKHeX.Avalonia.Views.SaveEditors;

/// <summary>
/// Plain property grid over an object (port of the WinForms <c>SAVEditor.GetPropertyForm</c> fallback).
/// </summary>
public sealed class SimpleEditorWindow : Window
{
    public SimpleEditorWindow(object target)
    {
        Name = "SimpleEditor";
        Title = "Simple Editor";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MinWidth = 350;
        MinHeight = 380;

        var grid = new PropertyGridView();
        grid.SetObject(target);
        Content = grid;

        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
    }
}
