using Avalonia.Controls;
using PKHeX.Avalonia.Localization;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// About dialog showing the shortcuts and changelog (port of the WinForms <c>About</c> form).
/// </summary>
public sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        Icon = AppIcon.Get();
    }

    public AboutWindow(AboutPage index) : this()
    {
        var lang = MainWindow.CurrentLanguage;
        Translator.TranslateInterface(this, lang);
        this.FindControl<TextBox>("RTB_Changelog")!.Text = AppResources.GetText("changelog") ?? string.Empty;
        this.FindControl<TextBox>("RTB_Shortcuts")!.Text = GetShortcutsText(lang);
        this.FindControl<TabControl>("TC_About")!.SelectedIndex = (int)index;
    }

    private static string GetShortcutsText(string lang)
    {
        var localized = AppResources.GetText($"shortcuts_{lang}");
        return localized ?? AppResources.GetText("shortcuts") ?? string.Empty;
    }
}

public enum AboutPage
{
    Shortcuts,
    Changelog,
}
