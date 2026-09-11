using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Save file browser for recent files, backups and known folders (port of the WinForms <c>SAV_FolderList</c>).
/// </summary>
public sealed class FolderListWindow : Window
{
    private readonly Action<SaveFile> OpenSaveFile;
    private readonly List<INamedFolderPath> Paths;
    private readonly ObservableCollection<SavePreview> Recent = [];
    private readonly ObservableCollection<SavePreview> Backup = [];
    private readonly CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));

    private readonly TabControl Tabs = new();
    private readonly TabItem Tab_Recent = new() { Name = "Tab_Recent", Header = "Recent" };
    private readonly TabItem Tab_Backup = new() { Name = "Tab_Backup", Header = "Backups" };
    private readonly TabItem Tab_Folders = new() { Name = "Tab_Folders", Header = "Folders" };
    private readonly DataGrid dgDataRecent = CreateGrid();
    private readonly DataGrid dgDataBackup = CreateGrid();
    private readonly WrapPanel FLP_Buttons = new() { Orientation = Orientation.Horizontal };
    private readonly ComboBox CB_FilterColumn = UiFactory.StringCombo("CB_FilterColumn", 140);
    private readonly TextBox TB_FilterTextContains = new() { Name = "TB_FilterTextContains", MinWidth = 200, MinHeight = 0, Padding = new Thickness(4, 2), IsEnabled = false };

    private static readonly string[] Columns = ["OT", "G", "Game", "Played", "FileTime", "TID", "SID", "Folder", "Name"];

    public FolderListWindow(Action<SaveFile> openSaveFile)
    {
        Name = "SAV_FolderList";
        Title = "Folder List";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 1000;
        Height = 560;
        OpenSaveFile = openSaveFile;

        var backups = MainWindow.BackupPath;
        var drives = Environment.GetLogicalDrives();
        Paths = GetPathList(drives, backups, cts.Token);

        Tab_Recent.Content = dgDataRecent;
        Tab_Backup.Content = dgDataBackup;
        Tab_Folders.Content = new ScrollViewer { Content = FLP_Buttons };
        Tabs.Items.Add(Tab_Recent);
        Tabs.Items.Add(Tab_Backup);
        Tabs.Items.Add(Tab_Folders);

        var filterRow = UiFactory.Row(CB_FilterColumn, TB_FilterTextContains);
        filterRow.Margin = new Thickness(6, 4);
        var root = new DockPanel();
        DockPanel.SetDock(filterRow, Dock.Top);
        root.Children.Add(filterRow);
        root.Children.Add(Tabs);
        Content = root;

        foreach (var grid in new[] { dgDataRecent, dgDataBackup })
        {
            foreach (var name in Columns)
                grid.Columns.Add(new DataGridTextColumn { Header = name, Binding = new Binding(name) });
            grid.ContextMenu = GetContextMenu(grid);
        }
        dgDataRecent.ItemsSource = Recent;
        dgDataBackup.ItemsSource = Backup;

        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
        TranslateColumns();

        CB_FilterColumn.Items.Add(MsgAny);
        foreach (var col in dgDataRecent.Columns)
            CB_FilterColumn.Items.Add(col.Header as string ?? string.Empty);
        CB_FilterColumn.SelectedIndex = 0;
        CB_FilterColumn.SelectionChanged += (_, _) => ChangeFilterIndex();
        TB_FilterTextContains.OnTextChanged(_ => ChangeFilterText());

        // Pre-programmed folders
        foreach (var loc in Paths)
            AddButton(loc.DisplayText, loc.Path);

        AddHandler(KeyDownEvent, (_, e) =>
        {
            // Quick close with Ctrl+W
            if (e.Key == Key.W && e.KeyModifiers == KeyModifiers.Control)
                Close();
        }, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Closing += (_, _) => cts.Cancel();

        var token = cts.Token;
        var extra = Paths.Select(z => z.Path).Where(z => z != backups).Distinct().ToArray();
        _ = Task.Run(() => LoadSaves(drives, backups, extra, token), token);
    }

    private static DataGrid CreateGrid() => new()
    {
        AutoGenerateColumns = false,
        IsReadOnly = true,
        CanUserSortColumns = true,
        SelectionMode = DataGridSelectionMode.Single,
        HeadersVisibility = DataGridHeadersVisibility.Column,
    };

    private void TranslateColumns()
    {
        var lang = MainWindow.CurrentLanguage;
        foreach (var grid in new[] { dgDataRecent, dgDataBackup })
        {
            foreach (var col in grid.Columns)
            {
                var name = col.Header as string ?? string.Empty;
                col.Header = Translator.TranslateText(Translator.GetKey("SAV_FolderList", $"DGV_{name}"), name, lang);
            }
        }
    }

    private void LoadSaves(string[] drives, string backups, string[] extra, CancellationToken token)
    {
        var backup = SaveFinder.GetSaveFiles(drives, false, [backups], false, token);
        var recent = SaveFinder.GetSaveFiles(drives, false, extra, true, token).ToList();
        var loaded = MainWindow.Settings.Startup.RecentlyLoaded
            .Where(z => recent.All(x => x.Metadata.FilePath != z))
            .Where(File.Exists).Select(SaveUtil.GetSaveFile).OfType<SaveFile>();

        Populate(Recent, loaded.Concat(recent), token);
        Populate(Backup, backup, token);
    }

    private void Populate(ObservableCollection<SavePreview> list, IEnumerable<SaveFile> saves, CancellationToken token)
    {
        foreach (var sav in saves)
        {
            if (token.IsCancellationRequested)
                return;
            var preview = new SavePreview(sav, Paths);
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => list.Add(preview));
        }
    }

    private static List<INamedFolderPath> GetPathList(IReadOnlyList<string> drives, string backupPath, CancellationToken token)
    {
        List<INamedFolderPath> locs =
        [
            new CustomFolderPath(backupPath, DisplayText: "PKHeX Backups"),
            .. GetUserPaths(), .. GetPaths3DS(drives, token), .. GetPathsSwitch(drives, token),
        ];
        var filtered = locs
            .DistinctBy(z => z.Path)
            .OrderByDescending(z => Directory.Exists(z.Path));
        return [.. filtered];
    }

    private void AddButton(string name, string path)
    {
        var button = new Button
        {
            Name = $"B_{name}",
            Content = name,
            Width = 150,
            Height = 44,
            Margin = new Thickness(3),
            IsEnabled = Directory.Exists(path),
        };
        button.Click += async (_, _) =>
        {
            if (!Directory.Exists(path))
            {
                await AppDialogs.Alert(this, MsgFolderNotFound, path);
                return;
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            Close();
        };
        ToolTip.SetTip(button, path);
        FLP_Buttons.Children.Add(button);
    }

    private static IEnumerable<CustomFolderPath> GetUserPaths()
    {
        var paths = MainWindow.Settings.Backup.OtherBackupPaths;
        return paths.Select(x => new CustomFolderPath(x, FolderPathGroup.Custom));
    }

    private static IEnumerable<CustomFolderPath> GetPaths3DS(IEnumerable<string> drives, CancellationToken token)
    {
        var path3DS = SaveFinder.Get3DSLocation(drives, true, token);
        if (path3DS is null)
            return [];

        var root = Path.GetPathRoot(path3DS);
        if (root is null)
            return [];

        var paths = SaveFinder.Get3DSBackupPaths(root);
        return paths.Select(z => new CustomFolderPath(z, FolderPathGroup.Nintendo3DS));
    }

    private static IEnumerable<CustomFolderPath> GetPathsSwitch(IEnumerable<string> drives, CancellationToken token)
    {
        var pathNX = SaveFinder.GetSwitchLocation(drives, true, token);
        if (pathNX is null)
            return [];

        var root = Path.GetPathRoot(pathNX);
        if (root is null)
            return [];

        var paths = SaveFinder.GetSwitchBackupPaths(root);
        return paths.Select(z => new CustomFolderPath(z, FolderPathGroup.NintendoSwitch));
    }

    private sealed record CustomFolderPath(string Path, string DisplayText, FolderPathGroup Group = 0) : INamedFolderPath
    {
        public CustomFolderPath(string path, FolderPathGroup group = 0)
            : this(path, ResolveFolderName(path), group) { }

        private static string ResolveFolderName(string path)
        {
            var di = new DirectoryInfo(path);
            var root = di.Root.Name;
            var display = di.Parent?.Name ?? di.Name;
            return root == display ? di.Name : display;
        }
    }

    private ContextMenu GetContextMenu(DataGrid dgv)
    {
        var mnuOpen = new MenuItem { Name = "mnuOpen", Header = "_Open" };
        mnuOpen.Click += async (_, _) => await ClickOpenFile(dgv);
        var mnuBrowseAt = new MenuItem { Name = "mnuBrowseAt", Header = "_Browse..." };
        mnuBrowseAt.Click += async (_, _) => await ClickOpenFolder(dgv);
        var mnuDelete = new MenuItem { Name = "mnuDelete", Header = "_Delete" };
        mnuDelete.Click += async (_, _) => await ClickDeleteFile(dgv);

        var mnu = new ContextMenu();
        mnu.Items.Add(mnuOpen);
        mnu.Items.Add(mnuBrowseAt);
        mnu.Items.Add(mnuDelete);
        return mnu;
    }

    private async Task ClickOpenFile(DataGrid dgv)
    {
        var sav = GetSaveFile(dgv);
        if (sav is null || !File.Exists(sav.FilePath))
        {
            await AppDialogs.Alert(this, MsgFileLoadFail);
            return;
        }

        OpenSaveFile(sav.Save);
        Close();
    }

    private async Task ClickDeleteFile(DataGrid dgv)
    {
        var preview = GetSaveFile(dgv);
        if (preview is null || !File.Exists(preview.FilePath))
        {
            await AppDialogs.Alert(this, MsgFileLoadFail);
            return;
        }

        var path = preview.FilePath;
        var result = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgFileDelete, preview.FilePath);
        if (result != DialogResult.Yes)
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
            DeleteSaveFile(dgv, preview);
        }
        catch (Exception ex)
        {
            await AppDialogs.Alert(this, MsgFileDeleteFail, path, ex.Message);
        }
    }

    private async Task ClickOpenFolder(DataGrid dgv)
    {
        var sav = GetSaveFile(dgv);
        if (sav is null || !File.Exists(sav.FilePath))
        {
            await AppDialogs.Alert(this, MsgFileLoadFail);
            return;
        }

        var path = Path.GetDirectoryName(sav.FilePath);
        if (path is not null)
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private static SavePreview? GetSaveFile(DataGrid dgData) => dgData.SelectedItem as SavePreview;

    private void DeleteSaveFile(DataGrid dgData, SavePreview preview)
    {
        var parent = ReferenceEquals(dgData, dgDataRecent) ? Recent : Backup;
        parent.Remove(preview);

        // Scan other list to remove if it exists there too (e.g. backup and recent)
        var other = ReferenceEquals(dgData, dgDataRecent) ? Backup : Recent;
        var match = other.FirstOrDefault(z => z.FilePath == preview.FilePath);
        if (match is not null)
            other.Remove(match);
    }

    private void ChangeFilterIndex()
    {
        TB_FilterTextContains.IsEnabled = CB_FilterColumn.SelectedIndex != 0;
        SetRowFilter();
    }

    private void ChangeFilterText()
    {
        if (CB_FilterColumn.SelectedIndex != 0)
            SetRowFilter();
    }

    private void SetRowFilter()
    {
        int column = CB_FilterColumn.SelectedIndex - 1;
        var text = TB_FilterTextContains.Text ?? string.Empty;
        ApplyFilter(dgDataRecent, Recent, column, text);
        ApplyFilter(dgDataBackup, Backup, column, text);
    }

    private static void ApplyFilter(DataGrid grid, ObservableCollection<SavePreview> source, int column, string text)
    {
        if (column < 0 || text.Length == 0)
        {
            grid.ItemsSource = source;
            return;
        }
        var name = Columns[Math.Min(column, Columns.Length - 1)];
        var pi = typeof(SavePreview).GetProperty(name);
        if (pi is null)
        {
            grid.ItemsSource = source;
            return;
        }
        var filtered = source.Where(z => pi.GetValue(z)?.ToString()?.Contains(text, StringComparison.CurrentCultureIgnoreCase) == true).ToList();
        grid.ItemsSource = filtered;
    }

    /// <summary>
    /// Moves backup files that do not match their expected name (port of <c>SAV_FolderList.CleanBackups</c>).
    /// </summary>
    public static void CleanBackups(string path, bool deleteNotSaves)
    {
        var files = Directory.GetFiles(path);
        foreach (var file in files)
        {
            var fi = new FileInfo(file);
            if (!SaveUtil.IsSizeValid(fi.Length) || !SaveUtil.TryGetSaveFile(file, out var sav))
            {
                if (deleteNotSaves)
                    File.Delete(file);
                continue;
            }

            var self = sav.Metadata.FilePath;
            if (self is null)
                continue; // shouldn't hit
            var index = self.IndexOf(" [", StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                continue;
            var original = self[..index];
            sav.Metadata.SetExtraInfo(original);

            string backupName = sav.Metadata.GetBackupFileName(MainWindow.BackupPath);
            if (self == backupName)
                continue;

            if (File.Exists(backupName))
                File.Delete(self);
            else
                File.Move(self, backupName);
        }
    }
}
