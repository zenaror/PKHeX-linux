using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Settings;
using PKHeX.Avalonia.Startup;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Program settings editor (port of the WinForms <c>SettingsEditor</c>).
/// </summary>
public sealed class SettingsWindow : Window
{
    /// <summary>True if the user picked a different blank save file version.</summary>
    public bool BlankChanged { get; private set; }

    // Remember the last settings tab for the remainder of the session.
    private static string? _last;

    private readonly List<SettingItem> _settingsPages = [];
    private readonly ListBox LB_Tabs = new() { Name = "LB_Tabs", Width = 190 };
    private readonly PropertyGridView PG_Editor = new() { Name = "PG_Editor" };
    private readonly TextBlock L_Blank = UiFactory.Label("L_Blank", "Blank Save Version:");
    private readonly ComboBox CB_Blank = UiFactory.Combo("CB_Blank", 200);
    private readonly StackPanel FLP_Blank;
    private readonly Button B_Reset = UiFactory.Button("B_Reset", "Reset All");

    public SettingsWindow(object obj)
    {
        Name = "SettingsEditor";
        Title = "Settings";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 780;
        Height = 560;

        FLP_Blank = UiFactory.Row(L_Blank, CB_Blank);
        var bottom = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(B_Reset, Dock.Right);
        bottom.Children.Add(B_Reset);
        bottom.Children.Add(FLP_Blank);

        var body = new Grid { ColumnSpacing = 8 };
        body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetColumn(PG_Editor, 1);
        body.Children.Add(LB_Tabs);
        body.Children.Add(PG_Editor);

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(body);
        Content = root;

        LoadSettings(obj);
        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);

        if (obj is PKHeXSettings s)
        {
            static bool IsInvalidSaveFileVersion(GameVersion value) => value is 0 or GameVersion.GO or GameVersion.CP;
            var versions = GameInfo.Sources.VersionDataSource.Where(z => !IsInvalidSaveFileVersion((GameVersion)z.Value)).ToList();
            CB_Blank.SetItems(versions);
            CB_Blank.SetValue((int)s.Startup.DefaultSaveVersion);
            CB_Blank.SelectionChanged += (_, _) =>
            {
                var version = (GameVersion)CB_Blank.GetValue();
                if (IsInvalidSaveFileVersion(version))
                    return;
                s.Startup.DefaultSaveVersion = version;
                BlankChanged = true;
            };
            B_Reset.Click += async (_, _) => await DeleteSettings();
        }
        else
        {
            FLP_Blank.IsVisible = false;
            B_Reset.IsVisible = false;
        }

        LB_Tabs.ItemsSource = _settingsPages.Select(z => z.Name).ToArray();
        LB_Tabs.SelectionChanged += (_, _) => ChangeTab();
        var index = _last is null ? 0 : Math.Max(0, _settingsPages.FindIndex(z => z.Name == _last));
        LB_Tabs.SelectedIndex = Math.Min(index, _settingsPages.Count - 1);

        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.W && e.KeyModifiers == KeyModifiers.Control)
                Close();
        }, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void LoadSettings(object obj)
    {
        var type = obj.GetType();
        var props = ReflectUtil.GetPropertiesCanWritePublicDeclared(type);
        foreach (var p in props)
        {
            var state = ReflectUtil.GetValue(obj, p);
            if (state is null)
                continue;

            var key = Translator.GetKey("SettingsEditor", p); // keep the WinForms form name for translations
            var text = Translator.TranslateText(key, p, MainWindow.CurrentLanguage);
            _settingsPages.Add(new SettingItem { Name = text, Item = state });
        }

        _settingsPages.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
    }

    private void ChangeTab()
    {
        var index = LB_Tabs.SelectedIndex;
        if ((uint)index >= _settingsPages.Count)
        {
            _last = null;
            PG_Editor.SetObject(null);
            return;
        }
        var item = _settingsPages[index];
        PG_Editor.SetObject(item.Item);
        _last = item.Name;
    }

    private async Task DeleteSettings()
    {
        try
        {
            var dr = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, "Resetting settings requires the program to exit.", MessageStrings.MsgContinue);
            if (dr != DialogResult.Yes)
                return;
            var path = AppPaths.ConfigFilePath;
            if (File.Exists(path))
                File.Delete(path);
            var exe = Environment.ProcessPath;
            if (exe is not null)
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false });
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(this, "Failed to delete settings.", ex.Message);
        }
    }

    private sealed class SettingItem
    {
        public required string Name { get; init; }
        public required object Item { get; init; }
    }
}
