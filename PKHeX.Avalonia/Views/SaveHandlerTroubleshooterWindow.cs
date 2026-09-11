using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Forces a save file through a chosen handler/type/version (port of the WinForms <c>SaveHandlerTroubleshooter</c>).
/// </summary>
public sealed class SaveHandlerTroubleshooterWindow : Window
{
    private readonly MainWindow Main;

    private readonly ComboBox CB_Type = UiFactory.Combo("CB_Type", 220);
    private readonly TextBlock L_SubVersion = UiFactory.Label("L_SubVersion", "Version:");
    private readonly ComboBox CB_SubVersion = UiFactory.Combo("CB_SubVersion", 220);
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 220);
    private readonly ComboBox CB_Handler = UiFactory.Combo("CB_Handler", 220);
    private readonly TextBox TB_Path = UiFactory.Text("TB_Path", 4096, 380);
    private readonly TextBlock L_FileName = UiFactory.Label("L_FileName", string.Empty);
    private readonly Button B_Browse = UiFactory.Button("B_Browse", "Browse...");
    private readonly Button B_Continue = UiFactory.Button("B_Continue", "Continue");
    private readonly Button B_Cancel = UiFactory.Button("B_Cancel", "Cancel");

    private readonly List<ISaveHandler> Handlers = [];

    public SaveHandlerTroubleshooterWindow(MainWindow main)
    {
        Main = main;
        Name = "SaveHandlerTroubleshooter";
        Title = "Force Load Save File";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;

        InitializeBindings();

        var grid = UiFactory.FormGrid(5);
        UiFactory.AddFormRow(grid, 0, UiFactory.Label("L_Type", "Save Type:"), CB_Type);
        UiFactory.AddFormRow(grid, 1, L_SubVersion, CB_SubVersion);
        UiFactory.AddFormRow(grid, 2, UiFactory.Label("L_Language", "Language:"), CB_Language);
        UiFactory.AddFormRow(grid, 3, UiFactory.Label("L_Handler", "Handler:"), CB_Handler);
        UiFactory.AddFormRow(grid, 4, UiFactory.Label("L_Path", "File:"), UiFactory.Row(TB_Path, B_Browse));

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new global::Avalonia.Thickness(0, 10, 0, 0),
        };
        B_Continue.MinWidth = B_Cancel.MinWidth = 90;
        B_Continue.Padding = B_Cancel.Padding = new global::Avalonia.Thickness(8, 4);
        buttons.Children.Add(B_Cancel);
        buttons.Children.Add(B_Continue);

        Content = new StackPanel
        {
            Margin = new global::Avalonia.Thickness(10),
            Spacing = 4,
            Children = { grid, L_FileName, buttons },
        };

        CB_Type.SelectionChanged += (_, _) => UpdateSubVersionChoices();
        B_Browse.Click += async (_, _) => await ClickBrowse();
        B_Continue.Click += async (_, _) => await ClickContinue();
        B_Cancel.Click += (_, _) => Close();
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
                return;
            e.Handled = true;
            Close();
        };

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.DataTransfer.TryGetFiles() is { Length: not 0 } ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            e.Handled = true;
            if (e.DataTransfer.TryGetFiles() is not { Length: not 0 } files)
                return;
            if (files[0].TryGetLocalPath() is not { } path)
                return;
            TB_Path.Text = path;
            L_FileName.Text = Path.GetFileName(path);
        });

        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
    }

    private void InitializeBindings()
    {
        foreach (var type in Enum.GetValues<SaveFileType>())
        {
            if (type is SaveFileType.None)
                continue;
            CB_Type.Items.Add(type.ToString());
        }
        CB_Type.SelectedIndex = 0;

        foreach (var language in Enum.GetValues<LanguageID>())
            CB_Language.Items.Add(language.ToString());
        CB_Language.SelectedIndex = 0;

        Handlers.Add(new SaveHandlerDefault());
        Handlers.AddRange(SaveUtil.Handlers);
        foreach (var handler in Handlers)
            CB_Handler.Items.Add(GetHandlerDisplayName(handler));
        CB_Handler.SelectedIndex = 0;

        UpdateSubVersionChoices();
    }

    private GameVersion[] SubVersions = [];

    private void UpdateSubVersionChoices()
    {
        var type = GetSelectedType();
        SubVersions = GameUtil.GameVersions.Where(z => z.SaveFileType == type).Distinct().ToArray();

        CB_SubVersion.Items.Clear();
        foreach (var version in SubVersions)
            CB_SubVersion.Items.Add(GetGameVersionDisplayName(version));

        if (SubVersions.Length == 0)
        {
            L_SubVersion.IsVisible = CB_SubVersion.IsVisible = false;
        }
        else
        {
            CB_SubVersion.SelectedIndex = 0;
            L_SubVersion.IsVisible = CB_SubVersion.IsVisible = true;
        }
    }

    private SaveFileType GetSelectedType()
    {
        var index = CB_Type.SelectedIndex;
        var all = Enum.GetValues<SaveFileType>().Where(z => z is not SaveFileType.None).ToArray();
        return (uint)index < all.Length ? all[index] : SaveFileType.None;
    }

    private GameVersion GetSelectedVersion()
    {
        var index = CB_SubVersion.SelectedIndex;
        return (uint)index < SubVersions.Length ? SubVersions[index] : GameVersion.Any;
    }

    private LanguageID GetSelectedLanguage()
    {
        var all = Enum.GetValues<LanguageID>();
        var index = CB_Language.SelectedIndex;
        return (uint)index < all.Length ? all[index] : LanguageID.None;
    }

    private async Task ClickBrowse()
    {
        var path = await FileDialogs.OpenSingleFile(this, "All Files|*.*");
        if (path is null)
            return;
        TB_Path.Text = path;
        L_FileName.Text = Path.GetFileName(path);
    }

    private async Task ClickContinue()
    {
        var path = (TB_Path.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            await AppDialogs.Error(this, MsgFileLoadSelectFileSave);
            return;
        }

        if (!File.Exists(path))
        {
            await AppDialogs.Error(this, MsgFileLoadFail, path);
            return;
        }

        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(this, $"{MsgFileInUse}{Environment.NewLine}{path}", ex);
            return;
        }

        var index = CB_Handler.SelectedIndex;
        if ((uint)index >= Handlers.Count)
        {
            await AppDialogs.Error(this, MsgFileLoadFail);
            return;
        }
        var handler = Handlers[index];
        var typeInfo = new SaveTypeInfo(GetSelectedType(), GetSelectedVersion(), GetSelectedLanguage());

        SaveFile? sav;
        try
        {
            if (!SaveUtil.TryGetSaveFileHandler(data, out sav, path, handler, typeInfo))
            {
                await AppDialogs.Error(this, MsgFileLoadSaveFail, path);
                return;
            }
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(this, MsgFileLoadSaveLoadFail, ex);
            return;
        }

        try
        {
            await Main.OpenSAV(sav, path, forceOpen: true);
            Close();
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(this, MsgFileLoadSaveLoadFail, ex);
        }
    }

    private static string GetGameVersionDisplayName(GameVersion version)
    {
        var text = GameInfo.GetVersionName(version);
        return string.IsNullOrWhiteSpace(text) ? version.ToString() : text;
    }

    private static string GetHandlerDisplayName(ISaveHandler handler)
    {
        const string prefix = "SaveHandler";
        var name = handler.GetType().Name;
        return name.StartsWith(prefix, StringComparison.Ordinal) ? name[prefix.Length..] : name;
    }
}
