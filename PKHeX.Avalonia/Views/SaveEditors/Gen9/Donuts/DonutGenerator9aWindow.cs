using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen9.Donuts;

/// <summary>
/// Range/flavor picker for the bulk donut generator (port of the WinForms <c>SAV_DonutGenerator9a</c>).
/// </summary>
public sealed class DonutGenerator9aWindow : Window
{
    private readonly CheckedListView CLB_Flavors = new() { Name = "CLB_Flavors", Width = 320, Height = 160 };
    private readonly NumericUpDown NUD_Start = UiFactory.NumericUpDown("NUD_Start", 0, DonutPocket9a.MaxCount - 1, 112);
    private readonly NumericUpDown NUD_End = UiFactory.NumericUpDown("NUD_End", 0, DonutPocket9a.MaxCount, 112);
    private readonly Button B_Generate = UiFactory.Button("B_Generate", "Generate");
    private readonly Button B_Cancel = UiFactory.Button("B_Cancel", "Cancel");

    private readonly List<ulong> Hashes = [];
    private readonly Action<ulong[], int, int> Generate;

    public DonutGenerator9aWindow(Action<ulong[], int, int> generate)
    {
        Generate = generate;
        Name = "SAV_DonutGenerator9a";
        Title = "Donut Generator";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;

        NUD_End.Value = DonutPocket9a.MaxCount;
        InitializeFlavorChoices();

        var range = UiFactory.Row(NUD_Start, UiFactory.Label("L_RangeSeparator", "to"), NUD_End);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new global::Avalonia.Thickness(0, 10, 0, 0),
        };
        B_Generate.MinWidth = B_Cancel.MinWidth = 80;
        B_Generate.Padding = B_Cancel.Padding = new global::Avalonia.Thickness(8, 4);
        buttons.Children.Add(B_Cancel);
        buttons.Children.Add(B_Generate);

        Content = new StackPanel
        {
            Margin = new global::Avalonia.Thickness(10),
            Spacing = 4,
            Children =
            {
                UiFactory.Label("L_FlavorPool", "Flavor Pool"),
                CLB_Flavors,
                UiFactory.Label("L_Range", "Range [start, end)"),
                range,
                buttons,
            },
        };

        B_Cancel.Click += (_, _) => Close();
        B_Generate.Click += async (_, _) => await ClickGenerate();
        KeyDown += (_, e) =>
        {
            if (e.Key != global::Avalonia.Input.Key.Escape)
                return;
            e.Handled = true;
            Close();
        };

        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
    }

    private void InitializeFlavorChoices()
    {
        var localized = GameInfo.Strings.donutFlavor;
        var all = DonutInfo.Flavors;
        for (int i = 0; i < all.Count; i++)
        {
            var (hash, name) = all[i];
            if (!TryGetTypeIndex(name, out _))
                continue;
            Hashes.Add(hash);
            CLB_Flavors.Add(localized[i], true);
        }
    }

    private static bool TryGetTypeIndex(string name, out int typeIndex)
    {
        typeIndex = -1;
        if (name.Length < 8 || !int.TryParse(name.AsSpan(6, 2), out var value) || value is < 3 or > 21)
            return false;

        typeIndex = value - 3;
        return true;
    }

    private async Task ClickGenerate()
    {
        var start = (int)(NUD_Start.Value ?? 0);
        var end = (int)(NUD_End.Value ?? 0);
        if (start >= end)
        {
            await AppDialogs.Alert(this, "Choose a valid donut range.");
            return;
        }

        List<ulong> flavors = [];
        for (int i = 0; i < Hashes.Count; i++)
        {
            if (CLB_Flavors.GetItemChecked(i))
                flavors.Add(Hashes[i]);
        }
        if (flavors.Count == 0)
        {
            await AppDialogs.Alert(this, "Select at least one flavor type.");
            return;
        }

        Generate([.. flavors], start, end);
        Close();
    }
}
