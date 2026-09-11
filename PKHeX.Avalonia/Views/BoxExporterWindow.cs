using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Box dump window (port of the WinForms <c>BoxExporter</c>).
/// </summary>
public sealed class BoxExporterWindow : Window
{
    private readonly SaveFile SAV;
    private readonly IFileNamer<PKM>[] Namers = [.. EntityFileNamer.AvailableNamers];
    private BoxExportSettings Settings;

    private readonly TextBlock L_Namer = UiFactory.Label("L_Namer", "Namer:");
    private readonly ComboBox CB_Namer = UiFactory.StringCombo("CB_Namer", 220);
    private readonly PropertyGridView PG_Settings = new() { Name = "PG_Settings", MinHeight = 260 };
    private readonly Button B_Export = UiFactory.Button("B_Export", "Export");

    public BoxExporterWindow(SaveFile sav, ExportOverride eo = ExportOverride.None)
    {
        Name = "BoxExporter";
        Title = "Box Export";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 520;
        Height = 420;

        SAV = sav;
        var obj = MainWindow.Settings.SlotExport;
        var settings = obj.BoxExport;
        if (eo != 0)
            settings = settings with { Scope = eo == ExportOverride.All ? BoxExportScope.All : BoxExportScope.Current };
        Settings = settings;

        var top = UiFactory.Row(L_Namer, CB_Namer);
        B_Export.HorizontalAlignment = HorizontalAlignment.Right;
        B_Export.Padding = new Thickness(12, 4);
        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(B_Export, Dock.Bottom);
        root.Children.Add(top);
        root.Children.Add(B_Export);
        root.Children.Add(PG_Settings);
        Content = root;

        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
        PG_Settings.SetObject(Settings);

        int index = 0;
        for (var i = 0; i < Namers.Length; i++)
        {
            var name = Namers[i].Name;
            CB_Namer.Items.Add(name);
            if (name == obj.DefaultBoxExportNamer)
                index = i;
        }
        CB_Namer.SelectedIndex = index;

        B_Export.Click += async (_, _) => await B_Export_Click();
        Closing += (_, _) =>
        {
            var settings = MainWindow.Settings.SlotExport;
            settings.DefaultBoxExportNamer = GetSelectedNamer().Name;
            settings.BoxExport = Settings;
        };
    }

    private async Task B_Export_Click()
    {
        var folder = await FileDialogs.PickFolder(this, "Select a folder to export the boxes to.");
        if (folder is null)
            return;

        var namer = GetSelectedNamer();
        var settings = Settings;
        int ctr = BoxExport.Export(SAV, folder, namer, settings);
        if (settings.Notify == BoxExportNofify.Silent)
            return;

        if (ctr < 0)
        {
            await AppDialogs.Error(this, MessageStrings.MsgSaveBoxExportInvalid);
            return;
        }
        var result = string.Format(MessageStrings.MsgSaveBoxExportPathCount, ctr) + Environment.NewLine + folder;
        await AppDialogs.Alert(this, result);
    }

    private IFileNamer<PKM> GetSelectedNamer() => Namers[Math.Max(0, CB_Namer.SelectedIndex)];

    public enum ExportOverride
    {
        None = 0,
        All = 1,
        Current = 2,
    }
}
