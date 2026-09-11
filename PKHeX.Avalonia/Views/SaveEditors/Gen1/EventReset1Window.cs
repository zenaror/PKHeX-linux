using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen1;

/// <summary>
/// Resets the overworld spawn flags of static encounters in Generation 1 (port of the WinForms <c>SAV_EventReset1</c>).
/// </summary>
/// <remarks>
/// Each button clears one "already caught / already fought" flag so the encounter reappears.
/// A button is enabled only while its spawn is hidden; the changes are written back when the window closes.
/// </remarks>
public sealed class EventReset1Window : Window
{
    private readonly G1OverworldSpawner Overworld;

    public EventReset1Window(SAV1 sav)
    {
        Name = "SAV_EventReset1";
        Title = "Event Reset";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.Height;
        Width = 400;
        CanResize = false;

        Overworld = new G1OverworldSpawner(sav);

        var list = new WrapPanel { Name = "FLP_List", Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        foreach (var pair in Overworld.GetFlagPairs().OrderBy(z => z.Name))
            list.Children.Add(CreateButton(pair));

        Content = new ScrollViewer { Content = list, MaxHeight = 560 };
        Localization.Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
        KeyDown += (_, e) => { if (e.Key == global::Avalonia.Input.Key.Escape) Close(); };
        Closing += (_, _) => Overworld.Save();
    }

    private Button CreateButton(FlagPairG1Detail pair)
    {
        var b = UiFactory.Button($"B_{pair.Name}", GetDisplayName(pair.Name));
        b.IsEnabled = pair.IsHidden;
        b.Width = 170;
        b.Margin = new Thickness(2);
        b.Click += async (_, _) =>
        {
            pair.Reset();
            b.IsEnabled = false;
            await AppDialogs.Alert(this, "Reset!");
        };
        return b;
    }

    /// <summary>
    /// Converts the flag property name into the species name for the current language, keeping any suffix.
    /// </summary>
    private static string GetDisplayName(string propertyName)
    {
        var name = propertyName.AsSpan(G1OverworldSpawner.FlagPropertyPrefix.Length);
        var index = name.IndexOf('_');
        var specName = index == -1 ? name : name[..index];

        SpeciesName.TryGetSpecies(specName, (int)LanguageID.English, out var species);
        var localized = GameInfo.Strings.specieslist[species];
        if (index != -1)
            localized += $" {name[(index + 1)..]}";
        return localized;
    }
}
