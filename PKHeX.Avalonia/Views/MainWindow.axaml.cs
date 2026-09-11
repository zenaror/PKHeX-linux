using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Plugins;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Settings;
using PKHeX.Core;
using PKHeX.Drawing;
using PKHeX.Drawing.Misc;
using PKHeX.Drawing.PokeSprite;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Main application window (port of the WinForms <c>Main</c> form).
/// </summary>
public sealed partial class MainWindow : Window
{
    #region Important Variables
    public static string CurrentLanguage
    {
        get => GameInfo.CurrentLanguage;
        private set => GameInfo.CurrentLanguage = value;
    }

    public static bool Unicode
    {
        get;
        private set
        {
            field = value;
            GenderSymbols = value ? GameInfo.GenderSymbolUnicode : GameInfo.GenderSymbolASCII;
        }
    }

    public static IReadOnlyList<string> GenderSymbols { get; private set; } = GameInfo.GenderSymbolUnicode;
    public static bool HaX => Program.HaX;
    public static PKHeXSettings Settings => Program.Settings;
    public static DrawConfig Draw { get; private set; } = new();
    #endregion

    #region Path Variables
    public static string DatabasePath => Settings.LocalResources.GetDatabasePath();
    public static string MGDatabasePath => Settings.LocalResources.GetMGDatabasePath();
    public static string BackupPath => Settings.LocalResources.GetBackupPath();

    /// <summary>Folder the hover cries are loaded from.</summary>
    public static string CryPath => Settings.LocalResources.GetCryPath();
    private static string TrainerPath => Settings.LocalResources.GetTrainerPath();
    private static string PluginPath => Settings.LocalResources.GetPluginPath();

    /// <summary>Plugins loaded from the plugin folder.</summary>
    private static List<IPlugin> Plugins { get; } = [];

    // Keep the load contexts alive so plugins can resolve their own dependencies.
    private static PluginLoadResult? PluginLoadResult { get; set; }
    private static string TemplatePath => Settings.LocalResources.GetTemplatePath();
    private const string ThreadPath = "https://projectpokemon.org/pkhex/";
    #endregion

    /// <summary>
    /// Modifier keys currently held (tracked from keyboard/pointer events; equivalent of WinForms <c>Control.ModifierKeys</c>).
    /// </summary>
    public KeyModifiers ModifierKeys
    {
        get;
        private set => CurrentModifiers = field = value;
    }

    /// <summary>
    /// Modifier keys currently held, for code that has no access to the input event (WinForms <c>Control.ModifierKeys</c>).
    /// </summary>
    public static KeyModifiers CurrentModifiers { get; private set; }

    private global::Avalonia.Media.Imaging.Bitmap? dragoutImage;
    private global::Avalonia.Media.Imaging.Bitmap? spriterSet, spriterDelete, spriterTransparent;
    private bool closeConfirmed;
    private bool startupDone;

    public MainWindow()
    {
        InitializeComponent();
        Icon = AppIcon.Get();

        C_SAV.Owner = this;
        C_SAV.SetEditEnvironment(new SaveDataEditor<SlotView>(FakeSaveFile.Default, PKME_Tabs));
        SetMenuIcons();
        FormLoadAddEvents();
        FormInitializeSecond();
        Title = GetProgramTitle();

        Opened += Main_Opened;
        Closing += Main_Closing;
    }

    #region //// MAIN MENU FUNCTIONS ////

    private void SetMenuIcons()
    {
        Func<string, global::Avalonia.Media.Imaging.Bitmap?> get = App.IsDarkModeEnabled ? AppResources.GetImageBlackToWhite : AppResources.GetImage;
        (string Name, string Icon)[] icons =
        [
            ("Menu_Open", "open"), ("Menu_Save", "savePKM"), ("Menu_ExportSAV", "saveSAV"), ("Menu_Exit", "exit"),
            ("Menu_Showdown", "showdown"), ("Menu_ShowdownImportPKM", "import"), ("Menu_ShowdownExportPKM", "export"),
            ("Menu_ShowdownExportParty", "export"), ("Menu_ShowdownExportCurrentBox", "export"),
            ("Menu_Data", "data"), ("Menu_LoadBoxes", "load"), ("Menu_DumpBoxes", "dump"), ("Menu_DumpBox", "dump"),
            ("Menu_Report", "report"), ("Menu_Database", "database"), ("Menu_MGDatabase", "gift"), ("Menu_EncDatabase", "users"),
            ("Menu_BatchEditor", "settings"), ("Menu_Folder", "folder"),
            ("Menu_Language", "language"), ("Menu_Undo", "bak"), ("Menu_Redo", "redo"), ("Menu_Settings", "settings"), ("Menu_About", "about"),
        ];
        foreach (var (name, icon) in icons)
        {
            var item = this.FindControl<MenuItem>(name);
            var bmp = get(icon);
            if (item is null || bmp is null)
                continue;
            item.Icon = new Image { Source = bmp, Width = 16, Height = 16 };
        }
    }

    /// <summary>
    /// Loads plugins and hands each one the save editor, the entity editor, the Tools menu and the program version.
    /// </summary>
    private void AttachPlugins()
    {
        if (Plugins.Count != 0)
            return; // already loaded
        var folder = PluginPath;
        if (!Directory.Exists(folder))
            return;

        try
        {
            PluginLoadResult = PluginLoader.LoadPlugins(folder, Plugins);
        }
        catch (Exception ex)
        {
            _ = AppDialogs.Error(this, MsgPluginFailLoad, ex);
            return;
        }

        var menu = this.FindControl<MenuItem>("Menu_Tools");
        foreach (var plugin in Plugins.OrderBy(z => z.Priority).ToArray())
        {
            try
            {
                plugin.Initialize(C_SAV, PKME_Tabs, menu!, Program.CurrentVersion);
            }
            catch (Exception ex)
            {
                _ = AppDialogs.Error(this, MsgPluginFailLoad, ex);
                Plugins.Remove(plugin);
            }
        }
    }

    /// <summary>Notifies plugins that a different save file is loaded.</summary>
    private static void NotifyPluginsSaveLoaded()
    {
        foreach (var plugin in Plugins)
        {
            try
            {
                plugin.NotifySaveLoaded();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Plugin {plugin.Name} failed to handle the save file: {ex.Message}");
            }
        }
    }

    private void FormLoadAddEvents()
    {
        C_SAV.Menu_Redo = Menu_Redo;
        C_SAV.Menu_Undo = Menu_Undo;
        C_SAV.RequestEditorLegality = la => _ = DisplayLegalityReport(la);
        C_SAV.RequestReloadSave += (_, _) => _ = ClickSaveFileName();

        PKME_Tabs.UpdatePreviewSprite += PKME_Tabs_UpdatePreviewSprite;
        PKME_Tabs.LegalityChanged += PKME_Tabs_LegalityChanged;
        PKME_Tabs.RequestShowdownExport += (s, e) => ClickShowdownExportPKM(s, new RoutedEventArgs());
        PKME_Tabs.RequestShowdownImport += (s, e) => ClickShowdownImportPKM(s, new RoutedEventArgs());
        PKME_Tabs.SaveFileRequested = () => C_SAV.SAV;

        // Track modifier keys (WinForms Control.ModifierKeys equivalent)
        AddHandler(KeyDownEvent, (_, e) => ModifierKeys = e.KeyModifiers, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) => ModifierKeys = GetModifiersAfterKeyUp(e), RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, e) => ModifierKeys = e.KeyModifiers, RoutingStrategies.Tunnel);
        // Menu items live in their own popup window, so the window-level handlers above never see the modifier that
        // was held while clicking one. Capture it on the item itself (WinForms reads Control.ModifierKeys directly).
        Menu_Database.AddHandler(PointerPressedEvent, (_, e) => ModifierKeys = e.KeyModifiers, RoutingStrategies.Tunnel);

        // Drag & Drop of files onto the window
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, Main_DragOver);
        AddHandler(DragDrop.DropEvent, Main_DragDrop);

        // Drag-out of the current entity as a file
        dragoutBorder.PointerPressed += Dragout_PointerPressed;
        dragoutBorder.PointerEntered += DragoutEnter;
        dragoutBorder.PointerExited += DragoutLeave;
        ToolTip.SetTip(dragoutBorder, "Drag to Save");

        // Context menu for the current entity
        var mnu = new ContextMenu();
        var mnuLegality = new MenuItem { Name = "mnuLLegality", Header = "_Legality" };
        var mnuQR = new MenuItem { Name = "mnuLQR", Header = "_QR!" };
        var mnuSaveAs = new MenuItem { Name = "mnuLSave", Header = "_Save as..." };
        mnuLegality.Click += (_, _) => _ = ClickLegality();
        mnuQR.Click += (_, _) => _ = ClickQR();
        mnuSaveAs.Click += (_, _) => _ = MainMenuSaveAsync();
        mnu.Items.Add(mnuLegality);
        mnu.Items.Add(mnuQR);
        mnu.Items.Add(mnuSaveAs);
        dragoutBorder.ContextMenu = mnu;

        L_UpdateAvailable.PointerPressed += (_, _) => Process.Start(new ProcessStartInfo(ThreadPath) { UseShellExecute = true });
    }

    private KeyModifiers GetModifiersAfterKeyUp(KeyEventArgs e)
    {
        var mods = e.KeyModifiers;
        mods &= e.Key switch
        {
            Key.LeftCtrl or Key.RightCtrl => ~KeyModifiers.Control,
            Key.LeftShift or Key.RightShift => ~KeyModifiers.Shift,
            Key.LeftAlt or Key.RightAlt => ~KeyModifiers.Alt,
            _ => ~KeyModifiers.None,
        };
        return mods;
    }

    private async void Main_Opened(object? sender, EventArgs e)
    {
        if (startupDone)
            return;
        startupDone = true;
        try
        {
            AttachPlugins();
            await LoadInitialFiles(Program.StartupArgs);

            // Follow-up: display popups if needed.
            var init = Program.Init;
            if (init.HaX)
                await WarnBehavior();
            else if (init.ShowChangelog)
                await ShowAboutDialog(AboutPage.Changelog);
            else if (init.BackupPrompt)
                await PromptBackup(Settings.LocalResources.GetBackupPath());

            await CheckForUpdates();
        }
        catch (Exception ex)
        {
            await ErrorWindow.ShowErrorDialog(this, MsgFileLoadFailAuto, ex, true);
        }
    }

    public async Task LoadInitialFiles(StartupArguments args)
    {
        var sav = args.SAV!;
        var path = sav.Metadata.FilePath ?? string.Empty;
        await OpenSAV(sav, path);

        var pk = args.Entity!;
        OpenPKM(pk);

        if (args.Error is { } ex)
            await ErrorWindow.ShowErrorDialog(this, MsgFileLoadFailAuto, ex, true);
    }

    private async Task LoadBlankSaveFile(GameVersion version)
    {
        if (!version.IsValidSavedVersion())
            version = Latest.Version;
        var current = C_SAV.SAV;
        var sav = BlankSaveFile.Get(version, current);
        await OpenSAV(sav, string.Empty);
        C_SAV.SAV.State.Edited = false; // Prevents form close warning from showing until changes are made
    }

    public async Task CheckForUpdates()
    {
        Version? latestVersion;
        // User might not be connected to the internet or with a flaky connection.
        try { latestVersion = await Task.Run(UpdateUtil.GetLatestPKHeXVersion); }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception while checking for latest version: {ex}");
            return;
        }
        if (latestVersion is null || latestVersion <= Program.CurrentVersion)
            return;

        NotifyNewVersionAvailable(latestVersion);
    }

    private void NotifyNewVersionAvailable(Version version)
    {
        var date = $"{2000 + version.Major:00}{version.Minor:00}{version.Build:00}";
        var lbl = L_UpdateAvailable;
        lbl.Text = $"{MsgProgramUpdateAvailable} {date}";
        lbl.IsVisible = true;
    }

    private void FormInitializeSecond()
    {
        var settings = Settings;
        Draw = PKME_Tabs.Draw = settings.Draw;
        ReloadProgramSettings(settings, true);
        PB_Legal.IsVisible = !HaX;
        C_SAV.HaX = PKME_Tabs.HaX = HaX;

        // Select Language
        AddLanguageMenuItems();
        ApplyMainLanguage(GameLanguage.GetLanguageIndex(settings.Startup.Language));
    }

    // Main Menu Strip UI Functions
    private async void MainMenuOpen(object? sender, RoutedEventArgs e)
    {
        try
        {
            var path = await FileDialogs.OpenSAVPKMDialog(this, C_SAV.SAV.PKMExtensions);
            if (path is not null)
                await OpenQuick(path);
        }
        catch (Exception ex) { await AppDialogs.Error(this, MsgFileLoadFail, ex); }
    }

    private void MainMenuSave(object? sender, RoutedEventArgs e) => _ = MainMenuSaveAsync();

    private async Task MainMenuSaveAsync()
    {
        if (!PKME_Tabs.EditsComplete)
            return;
        PKM pk = PreparePKM();
        try { await FileDialogs.SavePKMDialog(this, pk); }
        catch (Exception ex) { await AppDialogs.Error(this, MsgFileWriteFail, ex); }
    }

    private async void MainMenuExit(object? sender, RoutedEventArgs e)
    {
        if (ModifierKeys == KeyModifiers.Control) // triggered via hotkey
        {
            if (DialogResult.Yes != await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgConfirmQuitProgram))
                return;
        }

        Close();
    }

    private void MainMenuAbout(object? sender, RoutedEventArgs e) => _ = ShowAboutDialog(AboutPage.Shortcuts);

    public async Task ShowAboutDialog(AboutPage index)
    {
        var form = new AboutWindow(index);
        await form.ShowDialog(this);
    }

    // Sub Menu Options (features not yet ported are disabled in FormInitializeSecond)
    private void MainMenuBoxReport(object? sender, RoutedEventArgs e)
    {
        var report = new ReportGridWindow();
        var list = new List<SlotCache>();
        SlotInfoLoader.AddFromSaveFile(C_SAV.SAV, list);

        var settings = Settings.Report;
        var extra = CollectionsMarshal.AsSpan(settings.ExtraProperties);
        var hide = CollectionsMarshal.AsSpan(settings.HiddenProperties);
        report.PopulateData(list, extra, hide);
        report.Show(this);
    }
    private void MainMenuDatabase(object? sender, RoutedEventArgs e) => _ = MainMenuDatabaseAsync();

    private async Task MainMenuDatabaseAsync()
    {
        if (ModifierKeys == KeyModifiers.Shift) // WinForms: Shift opens the personal-table chart instead.
        {
            new KChartWindow(C_SAV.SAV).Show(this);
            return;
        }

        if (!Directory.Exists(DatabasePath))
        {
            await AppDialogs.Alert(this, MsgDatabase, string.Format(MsgDatabaseAdvice, DatabasePath));
            return;
        }

        new DatabaseWindow(PKME_Tabs, C_SAV).Show(this);
    }
    private void Menu_EncDatabase_Click(object? sender, RoutedEventArgs e)
    {
        var db = new TrainerDatabase();
        var sav = C_SAV.SAV;
        _ = Task.Run(() =>
        {
            var dir = TrainerPath;
            if (!Directory.Exists(dir))
                return;
            var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories);
            var pk = BoxUtil.GetPKMsFromPaths(files, sav.Context);
            foreach (var f in pk)
                db.RegisterCopy(f);
        });
        new EncounterDatabaseWindow(PKME_Tabs, db).Show(this);
    }
    private void MainMenuMysteryDB(object? sender, RoutedEventArgs e)
        => new MysteryGiftDatabaseWindow(PKME_Tabs, C_SAV).Show(this);
    private void MainMenuSettings(object? sender, RoutedEventArgs e) => _ = MainMenuSettingsAsync();

    private async Task MainMenuSettingsAsync()
    {
        var settings = Settings;
        var form = new SettingsWindow(settings);
        await form.ShowDialog(this);

        // Reload text (if OT details hidden)
        Title = GetProgramTitle(C_SAV.SAV);
        // Update final settings
        ReloadProgramSettings(Settings);

        if (form.BlankChanged) // changed by user
        {
            await LoadBlankSaveFile(Settings.Startup.DefaultSaveVersion);
            return;
        }

        PKME_Tabs_UpdatePreviewSprite(this, EventArgs.Empty);
        if (C_SAV.SAV.HasBox)
            C_SAV.ReloadSlots();
    }
    private void MainMenuBoxDump(object? sender, RoutedEventArgs e) => _ = MainMenuBoxDumpAsync();

    private async Task MainMenuBoxDumpAsync()
    {
        var ld = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgDatabaseExport);
        if (ld == DialogResult.Yes)
        {
            BoxExport.Export(C_SAV.SAV, DatabasePath, BoxExportSettings.Default);
            return;
        }
        if (ld != DialogResult.No)
            return;

        var dumper = new BoxExporterWindow(C_SAV.SAV, BoxExporterWindow.ExportOverride.All);
        await dumper.ShowDialog(this);
    }

    private void MainMenuBoxDumpSingle(object? sender, RoutedEventArgs e) => _ = MainMenuBoxDumpSingleAsync();

    private async Task MainMenuBoxDumpSingleAsync()
    {
        C_SAV.SAV.CurrentBox = C_SAV.CurrentBox; // double check
        var dumper = new BoxExporterWindow(C_SAV.SAV, BoxExporterWindow.ExportOverride.Current);
        await dumper.ShowDialog(this);
    }
    private void MainMenuBatchEditor(object? sender, RoutedEventArgs e) => _ = MainMenuBatchEditorAsync();

    private async Task MainMenuBatchEditorAsync()
    {
        var form = new BatchEditorWindow(PKME_Tabs.PreparePKM(), C_SAV.SAV, C_SAV.EditEnv.Slots.Changelog);
        await form.ShowDialog(this);
        if (!form.Accepted)
            return;

        foreach (var slot in form.GetModifiedSlots())
            C_SAV.EditEnv.Slots.UpdateSlot(slot);
        C_SAV.UpdateUndoRedo();
    }
    private void MainMenuFolder(object? sender, RoutedEventArgs e)
    {
        var form = new FolderListWindow(s => _ = OpenSAV(s.Clone(), s.Metadata.FilePath!));
        form.Show(this);
    }

    #region Troubleshooting menu

    /// <summary>Opens the save handler troubleshooter (port of <c>Troubleshooting.OpenSaveHandlerTroubleshooter</c>).</summary>
    private async void MainMenuForceLoadSAV(object? sender, RoutedEventArgs e)
    {
        try
        {
            var form = new SaveHandlerTroubleshooterWindow(this);
            await form.ShowDialog(this);
        }
        catch (Exception ex) { await AppDialogs.Error(this, ex.Message, ex); }
    }

    /// <summary>Loads a file pasted into the clipboard as a hex string (port of <c>OpenFileFromClipboardHex</c>).</summary>
    private async void MainMenuHexImporter(object? sender, RoutedEventArgs e)
    {
        try
        {
            var hex = (await ClipboardService.GetText(this) ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(hex))
            {
                await AppDialogs.Alert(this, MessageStrings.MsgTroubleshootingClipboardEmpty);
                return;
            }

            byte[] data;
            try
            {
                data = Convert.FromHexString(hex.Replace(" ", string.Empty));
            }
            catch (FormatException)
            {
                await AppDialogs.Alert(this, MessageStrings.MsgTroubleshootingClipboardInvalidHex);
                return;
            }
            await OpenFile(data, string.Empty, string.Empty);
        }
        catch (Exception ex) { await AppDialogs.Error(this, ex.Message, ex); }
    }

    /// <summary>Lists the loaded plugins (port of <c>DisplayPluginList</c>).</summary>
    private async void MainMenuPluginInfo(object? sender, RoutedEventArgs e)
    {
        var text = new System.Text.StringBuilder();
        text.AppendFormat(MessageStrings.MsgTroubleshootingPluginListHeader, Plugins.Count).AppendLine();
        if (Plugins.Count == 0)
        {
            text.AppendLine(MessageStrings.MsgTroubleshootingPluginListEmpty);
            await AppDialogs.Alert(this, text.ToString());
            return;
        }

        List<(IPlugin Plugin, string Group)> loaded = [];
        foreach (var plugin in Plugins)
        {
            var fullName = plugin.GetType().Assembly.FullName;
            if (fullName is not null)
            {
                var culture = fullName.IndexOf("Culture", StringComparison.Ordinal);
                if (culture != -1)
                    fullName = fullName[..(culture - 2)];
                if (fullName.EndsWith(".0", StringComparison.Ordinal))
                    fullName = fullName[..^2];
            }
            loaded.Add(new(plugin, fullName ?? "Unknown"));
        }

        foreach (var group in loaded.GroupBy(z => z.Group).OrderBy(z => z.Key, StringComparer.Ordinal))
        {
            text.AppendLine(group.Key);
            foreach (var plugin in group.OrderBy(z => z.Plugin.Name, StringComparer.Ordinal))
                text.AppendLine($"- {plugin.Plugin.Name}");
        }

        await AppDialogs.Alert(this, text.ToString());
    }

    #endregion

    private void ReloadProgramSettings(PKHeXSettings settings, bool skipCore = false)
    {
        if (!skipCore)
            StartupUtil.ReloadSettings(settings);

        PKME_Tabs.Unicode = Unicode = settings.Display.Unicode;
        PKME_Tabs.UpdateUnicode(GenderSymbols);
        SpriteName.AllowShinySprite = settings.Sprite.ShinySprites;
        SpriteBuilderUtil.SpriterPreference = settings.Sprite.SpritePreference;

        C_SAV.ModifyPKM = PKME_Tabs.ModifyPKM = settings.SlotWrite.SetUpdatePKM;
        C_SAV.FlagIllegal = settings.Display.FlagIllegal;
        PKME_Tabs.HideSecretValues = settings.Privacy.HideSecretDetails;
        FileDialogs.DetectSaveFileOnFileOpen = settings.Startup.TryDetectRecentSave;

        if (HaX)
        {
            EntityConverter.AllowIncompatibleConversion = EntityCompatibilitySetting.AllowIncompatibleAll;
        }
        SpriteBuilder.LoadSettings(settings.Sprite);
        FileDialogs.AddSaveFileExtensions(settings.Backup.OtherSaveFileExtensions);
        AppDialogs.Quiet = !settings.Sounds.PlaySoundOther;
    }

    private async void MainMenuBoxLoad(object? sender, RoutedEventArgs e)
    {
        try
        {
            string? path = null;
            if (Directory.Exists(DatabasePath))
            {
                var dr = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgDatabaseLoad);
                if (dr == DialogResult.Yes)
                    path = DatabasePath;
            }
            var (loaded, result) = await C_SAV.LoadBoxes(path);
            if (loaded)
                await AppDialogs.Alert(this, result);
        }
        catch (Exception ex) { await AppDialogs.Error(this, MsgFileLoadFail, ex); }
    }

    // Misc Options
    private async void ClickShowdownImportPKM(object? sender, RoutedEventArgs e)
    {
        try
        {
            var text = await ClipboardService.GetText(this);
            if (string.IsNullOrWhiteSpace(text))
            { await AppDialogs.Alert(this, MsgClipboardFailRead); return; }

            // Get Simulator Data
            var sets = BattleTemplateTeams.TryGetSets(text);
            var set = sets.FirstOrDefault() ?? new(string.Empty); // take only first set

            if (set.Species == 0)
            { await AppDialogs.Alert(this, MsgSimulatorFailClipboard); return; }

            var programLanguage = Language.GetLanguageValue(Settings.Startup.Language);
            var settings = Settings.BattleTemplate.Export.GetSettings(programLanguage, set.Context);
            var reformatted = set.GetText(settings);
            if (DialogResult.Yes != await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgSimulatorLoad, reformatted))
                return;

            var invalid = set.InvalidLines;
            if (invalid.Count != 0)
            {
                var localization = BattleTemplateParseErrorLocalization.Get(CurrentLanguage);
                var sb = new System.Text.StringBuilder();
                foreach (var line in invalid)
                {
                    var error = line.Humanize(localization);
                    sb.AppendLine(error);
                }
                await AppDialogs.Alert(this, MsgSimulatorInvalid, sb.ToString());
            }
            PKME_Tabs.LoadShowdownSet(set);
        }
        catch (Exception ex) { await AppDialogs.Error(this, MsgClipboardFailRead, ex); }
    }

    private async void ClickShowdownExportPKM(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!PKME_Tabs.EditsComplete)
            {
                await AppDialogs.Alert(this, MsgSimulatorExportBadFields);
                return;
            }

            var pk = PreparePKM();
            var programLanguage = Language.GetLanguageValue(Settings.Startup.Language);
            var settings = Settings.BattleTemplate.Export.GetSettings(programLanguage, pk.Context);
            var text = ShowdownParsing.GetShowdownText(pk, settings);
            bool success = await ClipboardService.SetText(this, text);
            var verify = success ? await ClipboardService.GetText(this) : null;
            if (!success || verify != text)
                await AppDialogs.Alert(this, MsgClipboardFailWrite, MsgSimulatorExportFail);
            else
                await AppDialogs.Alert(this, MsgSimulatorExportSuccess, text);
        }
        catch (Exception ex) { await AppDialogs.Error(this, MsgClipboardFailWrite, ex); }
    }

    private void ClickShowdownExportParty(object? sender, RoutedEventArgs e) => _ = C_SAV.ClickShowdownExportParty();
    private void ClickShowdownExportCurrentBox(object? sender, RoutedEventArgs e) => _ = C_SAV.ClickShowdownExportCurrentBox();

    // Main Menu Subfunctions
    private Task OpenQuick(string path) => OpenFromPath(path);

    private async Task OpenFromPath(string path)
    {
        // detect if it is a folder (load into boxes or not)
        if (Directory.Exists(path))
        { await C_SAV.LoadBoxes(path); return; }

        var fi = new FileInfo(path);
        if (!fi.Exists)
            return;

        if (FileUtil.IsFileTooBig(fi.Length))
        {
            await AppDialogs.Error(this, MsgFileSizeLarge + Environment.NewLine + string.Format(MsgFileSize, fi.Length), path);
            return;
        }
        if (FileUtil.IsFileTooSmall(fi.Length))
        {
            await AppDialogs.Error(this, MsgFileSizeSmall + Environment.NewLine + string.Format(MsgFileSize, fi.Length), path);
            return;
        }
        byte[] input; try { input = File.ReadAllBytes(path); }
        catch (Exception e) { await AppDialogs.Error(this, MsgFileInUse + path, e); return; }

        string ext = fi.Extension;
#if DEBUG
        await OpenFile(input, path, ext);
#else
        try { await OpenFile(input, path, ext); }
        catch (Exception e) { await AppDialogs.Error(this, MsgFileLoadFail + "\nPath: " + path, e); }
#endif
    }

    internal async Task OpenFile(Memory<byte> input, string path, string ext)
    {
        var obj = FileUtil.GetSupportedFile(input, ext, C_SAV.SAV);
        if (obj is not null && await LoadFile(obj, path))
            return;

        await AppDialogs.Error(this, GetHintInvalidFile(input.Span, path),
            $"{MsgFileLoad}{Environment.NewLine}{path}",
            $"{string.Format(MsgFileSize, input.Length)}{Environment.NewLine}{input.Length} bytes (0x{input.Length:X4})");
    }

    private static string GetHintInvalidFile(ReadOnlySpan<byte> input, string path)
    {
        bool isSAV = FileDialogs.IsFileExtensionSAV(path);
        if (!isSAV)
            return MsgPKMUnsupported;

        // Include a hint for the user to check if the file is all 00 or all FF
        bool allZero = !input.ContainsAnyExcept<byte>(0x00);
        if (allZero)
            return MsgFileLoadAllZero;
        bool allFF = !input.ContainsAnyExcept<byte>(0xFF);
        if (allFF)
            return MsgFileLoadAllFFFF;

        return MsgFileUnsupported;
    }

    /// <summary>
    /// Detects the most recent save file on the system and offers to load it (WinForms: double-click on the SAV tab).
    /// </summary>
    private async Task ClickSaveFileName()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!SaveFinder.TryDetectSaveFile(cts.Token, out var sav))
                return;

            var path = sav.Metadata.FilePath!;
            var time = new FileInfo(path).CreationTime;
            var timeStamp = time.ToString(CultureInfo.CurrentCulture);
            if (await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgFileLoadSaveDetectReload, path, timeStamp) == DialogResult.Yes)
                await LoadFile(sav, path); // load save
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(this, ex.Message); // `path` contains the error message
        }
    }

    private async Task<bool> LoadFile(object? input, string path)
    {
        if (input is null)
            return false;

        switch (input)
        {
            case PKM pk: return OpenPKM(pk);
            case SaveFile s: return await OpenSAV(s, path);
            case IPokeGroup b: return await OpenGroup(b);
            case MysteryGift g: return await OpenMysteryGift(g, path);
            case ConcatenatedEntitySet pkms: return await OpenPCBoxBin(pkms);
            case IEncounterConvertible enc: return OpenPKM(enc.ConvertToPKM(C_SAV.SAV));

            case SAV3GCMemoryCard gc:
                if (!await CheckGCMemoryCard(gc, path))
                    return true;
                if (!SaveUtil.TryGetSaveFile(gc, out var mcsav))
                    return false;
                mcsav.Metadata.SetExtraInfo(path);
                return await OpenSAV(mcsav, path);
        }
        return false;
    }

    private bool OpenPKM(PKM pk)
    {
        var sav = C_SAV.SAV;
        var destType = sav.PKMType;
        var tmp = EntityConverter.ConvertToType(pk, destType, out var c);
        Debug.WriteLine(c.GetDisplayString(pk, destType));
        if (tmp is null)
            return false;

        var unconverted = ReferenceEquals(pk, tmp);
        if (unconverted && sav is { State.Exportable: true })
            sav.AdaptToSaveFile(tmp);
        PKME_Tabs.PopulateFields(tmp);
        return true;
    }

    private async Task<bool> OpenGroup(IPokeGroup b)
    {
        var (result, msg) = await C_SAV.OpenGroup(b);
        if (!string.IsNullOrWhiteSpace(msg))
            await AppDialogs.Alert(this, msg);
        Debug.WriteLine(msg);
        return result;
    }

    private async Task<bool> OpenMysteryGift(MysteryGift tg, string path)
    {
        if (!tg.IsEntity)
        {
            await AppDialogs.Alert(this, MsgPKMMysteryGiftFail, path);
            return true;
        }

        var temp = tg.ConvertToPKM(C_SAV.SAV);
        var destType = C_SAV.SAV.PKMType;
        var pk = EntityConverter.ConvertToType(temp, destType, out var c);

        if (pk is null)
        {
            await AppDialogs.Alert(this, c.GetDisplayString(temp, destType));
            return true;
        }

        C_SAV.SAV.AdaptToSaveFile(pk);
        PKME_Tabs.PopulateFields(pk);
        Debug.WriteLine(c);
        return true;
    }

    private async Task<bool> OpenPCBoxBin(ConcatenatedEntitySet pkms)
    {
        if (!C_SAV.OpenPCBoxBin(pkms.Data.Span, out var msg))
        {
            await AppDialogs.Alert(this, MsgFileLoadIncompatible, msg);
            return true;
        }

        await AppDialogs.Alert(this, msg);
        return true;
    }

    private async Task<SaveFileType> SelectMemoryCardSaveGame(SAV3GCMemoryCard memCard)
    {
        if (memCard.SaveGameCount == 1)
            return memCard.SelectedGameVersion;

        string[] games =
        [
            MsgGameColosseum,
            MsgGameXD,
            MsgGameRSBOX,
        ];

        var index = await AppDialogs.TrySelectIndex(this, MsgFileLoadSaveMultiple, MsgFileLoadSaveSelectGame, games);
        return index switch
        {
            0 => SaveFileType.Colosseum,
            1 => SaveFileType.XD,
            2 => SaveFileType.RSBox,
            _ => SaveFileType.None,
        };
    }

    private async Task<bool> CheckGCMemoryCard(SAV3GCMemoryCard memCard, string path)
    {
        var state = memCard.GetMemoryCardState();
        switch (state)
        {
            case MemoryCardSaveStatus.NoPkmSaveGame:
                await AppDialogs.Error(this, MsgFileGameCubeNoGames, path);
                return false;

            case MemoryCardSaveStatus.DuplicateCOLO:
            case MemoryCardSaveStatus.DuplicateXD:
            case MemoryCardSaveStatus.DuplicateRSBOX:
                await AppDialogs.Error(this, MsgFileGameCubeDuplicate, path);
                return false;

            case MemoryCardSaveStatus.MultipleSaveGame:
                var game = await SelectMemoryCardSaveGame(memCard);
                if (game == 0) // Cancel
                    return false;
                memCard.SelectSaveGame(game);
                break;

            case MemoryCardSaveStatus.SaveGameCOLO: memCard.SelectSaveGame(SaveFileType.Colosseum); break;
            case MemoryCardSaveStatus.SaveGameXD: memCard.SelectSaveGame(SaveFileType.XD); break;
            case MemoryCardSaveStatus.SaveGameRSBOX: memCard.SelectSaveGame(SaveFileType.RSBox); break;

            default:
                await AppDialogs.Error(this, !SAV3GCMemoryCard.IsMemoryCardSize(memCard.Data.Length) ? MsgFileGameCubeBad : GetHintInvalidFile(memCard.Data, path), path);
                return false;
        }
        return true;
    }

    private static void StoreLegalSaveGameData(SaveFile sav)
    {
        if (sav is SAV3 sav3)
            EReaderBerrySettings.LoadFrom(sav3);
    }

    internal async Task<bool> OpenSAV(SaveFile sav, string path, bool forceOpen = false)
    {
        if (ModifierKeys == KeyModifiers.Alt)
        {
            SaveTypeInfo other = default;
            if (SaveUtil.TryOverride(sav, other, out var replace))
                sav = replace;
        }
        if (!sav.IsVersionValid() && !forceOpen)
        {
            await AppDialogs.Error(this, MsgFileLoadSaveLoadFail, path);
            return true;
        }

        sav.Metadata.SetExtraInfo(path);
        var sanity = await SanityCheckSAV(sav);
        if (sanity is null)
            return true;
        sav = sanity;

        if (C_SAV.SAV.State.Edited && Settings.SlotWrite.ModifyUnset)
        {
            var prompt = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgProgramCloseUnsaved, MsgProgramSaveFileConfirm);
            if (prompt != DialogResult.Yes)
                return true;
        }

        StoreLegalSaveGameData(sav);
        ParseSettings.InitFromSaveFileData(sav); // physical GB, no longer used in logic
        RecentTrainerCache.SetRecentTrainer(sav);
        SpriteUtil.Initialize(sav); // refresh sprite generator
        RefreshSpriterImages();
        dragoutBorder.Width = SpriteUtil.Spriter.Width + 4;
        dragoutBorder.Height = SpriteUtil.Spriter.Height;

        // clean fields
        Menu_ExportSAV.IsEnabled = sav.State.Exportable;

        // No changes made yet
        Menu_Undo.IsEnabled = false;
        Menu_Redo.IsEnabled = false;

        GameInfo.FilteredSources = new FilteredGameDataSource(sav, GameInfo.Sources, HaX);
        ResetSAVPKMEditors(sav);

        Title = GetProgramTitle(sav);
        await TryBackupExportCheck(sav, path);
        await CheckLoadPath(path);

        Menu_ShowdownExportParty.IsVisible = sav.HasParty;
        Menu_ShowdownExportCurrentBox.IsVisible = sav.HasBox;

        Settings.Startup.LoadSaveFile(path);
        return true;
    }

    private void RefreshSpriterImages()
    {
        var spriter = SpriteUtil.Spriter;
        spriterSet?.Dispose();
        spriterDelete?.Dispose();
        spriterTransparent?.Dispose();
        spriterSet = spriter.Set.ToAvaloniaBitmap();
        spriterDelete = spriter.Delete.ToAvaloniaBitmap();
        spriterTransparent = spriter.Transparent.ToAvaloniaBitmap();
        dragoutBackground.Source = spriterTransparent;
    }

    private void ResetSAVPKMEditors(SaveFile sav)
    {
        C_SAV.SetEditEnvironment(new SaveDataEditor<SlotView>(sav, PKME_Tabs));

        var pk = sav.LoadTemplate(TemplatePath);
        PKME_Tabs.CurrentPKM = pk;

        bool init = PKME_Tabs.IsEditorInitialized;
        if (!init)
        {
            PKME_Tabs.InitializeBinding();
            PKME_Tabs.SetPKMFormatMode(pk);
            PKME_Tabs.ChangeLanguage(sav);
        }
        else
        {
            PKME_Tabs.SetPKMFormatMode(pk);
        }
        PKME_Tabs.PopulateFields(pk);

        // Initialize Overall Info
        Menu_LoadBoxes.IsEnabled = C_SAV.SAV.HasBox;

        // Initialize Subviews
        bool WindowTranslationRequired = false;
        WindowTranslationRequired |= PKME_Tabs.ToggleInterface(sav, pk);
        WindowTranslationRequired |= C_SAV.ToggleInterface();
        if (WindowTranslationRequired) // force update -- re-added controls may be untranslated
            Translator.TranslateInterface(this, CurrentLanguage);

        PKME_Tabs.PopulateFields(pk);

        sav.State.Edited = false;
        NotifyPluginsSaveLoaded();
    }

    private static string GetProgramTitle()
    {
#if DEBUG
        // Get the file path that started this exe.
        var path = Environment.ProcessPath;
        var date = path is null ? DateTime.Now : File.GetLastWriteTime(path);
        string version = $"d-{date:yyyyMMdd}";
#else
        var v = Program.CurrentVersion;
        string version = $"{2000+v.Major:00}{v.Minor:00}{v.Build:00}";
#endif
        return $"PKH{(HaX ? "a" : "e")}X ({version})";
    }

    private static string GetProgramTitle(SaveFile sav)
    {
        var type = sav.GetType().Name;
        if (sav is ISaveFileRevision rev)
            type += rev.SaveRevisionString;

        var title = GetProgramTitle() + $" - {type}: ";
        var version = GameInfo.GetVersionName(sav.Version);
        if (Settings.Privacy.HideSAVDetails)
            return title + $"[{version}]";
        if (!sav.State.Exportable) // Blank save file
            return title + $"{sav.Metadata.FileName} [{sav.OT} ({version})]";
        return title + Path.GetFileNameWithoutExtension(PathUtil.CleanFileName(sav.Metadata.BAKName)); // more descriptive
    }

    private async Task<bool> TryBackupExportCheck(SaveFile sav, string path)
    {
        // If backup folder exists, save a backup.
        if (string.IsNullOrWhiteSpace(path))
            return false; // not actual save
        if (!Settings.Backup.BAKEnabled)
            return false;
        if (!sav.State.Exportable)
            return false; // not actual save
        var dir = BackupPath;
        if (!Directory.Exists(dir))
            return false;

        var meta = sav.Metadata;
        var backupName = meta.GetBackupFileName(dir);
        if (File.Exists(backupName))
            return false; // Already backed up.

        // Ensure the file we are copying exists.
        var src = meta.FilePath;
        if (src is null || !File.Exists(src))
            return false;

        try
        {
            // Don't need to force overwrite, but on the off-chance it was written externally, we force ours.
            File.Copy(src, backupName, true);
            return true;
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(this, MsgBackupUnable, ex);
            return false;
        }
    }

    private async Task<bool> CheckLoadPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false; // not actual save
        if (!FileUtil.IsFileLocked(path))
            return true;

        await AppDialogs.Alert(this, MsgFileWriteProtected + Environment.NewLine + path, MsgFileWriteProtectedAdvice);
        return false;
    }

    /// <returns>The (possibly replaced) save file, or null if the user cancelled.</returns>
    private async Task<SaveFile?> SanityCheckSAV(SaveFile sav)
    {
        if (sav.Generation <= 3)
            SaveLanguage.TryRevise(sav);

        if (sav.State.Exportable && sav is SAV3 s3)
        {
            if (ModifierKeys == KeyModifiers.Control || s3.IsCorruptPokedexFF())
            {
                GameVersion[] choices = [GameVersion.R, GameVersion.S, GameVersion.E, GameVersion.FR, GameVersion.LG];
                var options = new string[choices.Length];
                for (int i = 0; i < options.Length; i++)
                    options[i] = GameInfo.Strings.gamelist[(int)choices[i]];

                var msg = string.Format(MsgFileLoadVersionDetect, $"3 ({s3.Version})");
                var text = MsgFileLoadSaveSelectVersion;
                if (sav.Metadata.FileName is { } fn)
                    text += Environment.NewLine + fn;
                var index = await AppDialogs.TrySelectIndex(this, msg, text, options, Array.IndexOf(choices, s3.Version));
                if (index < 0)
                    return null;

                var game = choices[index];
                var s = s3.ForceLoad(game);
                if (s is SAV3FRLG frlg)
                {
                    // Try to give the correct Deoxys form stats (different in R/S, E, FR and LG)
                    bool result = frlg.ResetPersonal(game);
                    if (!result)
                        return null;
                }
                var origin = sav.Metadata.FilePath;
                if (origin is not null)
                    s.Metadata.SetExtraInfo(origin);
                return s;
            }
            if (s3 is SAV3FRLG frlg2 && !frlg2.Version.IsValidSavedVersion()) // IndeterminateSubVersion
            {
                GameVersion[] choices = [GameVersion.FR, GameVersion.LG];
                var options = new string[choices.Length];
                for (int i = 0; i < options.Length; i++)
                    options[i] = GameInfo.Strings.gamelist[(int)choices[i]];

                string dual = "{1}/{2} " + MsgFileLoadVersionDetect;
                var msg = string.Format(dual, "3", options[0], options[1]);
                var index = await AppDialogs.TrySelectIndex(this, msg, MsgFileLoadSaveSelectVersion, options);
                if (index < 0)
                    return null;

                var game = choices[index];
                bool result = frlg2.ResetPersonal(game);
                if (!result)
                    return null;
            }
        }

        return sav;
    }

    // Language Translation
    private void AddLanguageMenuItems()
    {
        Menu_Language.Items.Clear();
        var names = Enum.GetNames<ProgramLanguage>();
        for (int i = 0; i < names.Length; i++)
        {
            var item = new MenuItem
            {
                Header = names[i],
                Tag = i,
                ToggleType = MenuItemToggleType.CheckBox,
            };
            item.Click += ChangeMainLanguage;
            Menu_Language.Items.Add(item);
        }
        UpdateLanguageMenuChecks(GameLanguage.GetLanguageIndex(CurrentLanguage));
    }

    private void ChangeMainLanguage(object? sender, RoutedEventArgs e)
    {
        var index = sender is MenuItem { Tag: int menuIndex }
            ? menuIndex
            : GameLanguage.GetLanguageIndex(CurrentLanguage);
        ApplyMainLanguage(index);
    }

    private void ApplyMainLanguage(int index)
    {
        if ((uint)index < GameLanguage.LanguageCount)
            CurrentLanguage = GameLanguage.LanguageCode(index);

        var lang = CurrentLanguage;
        UpdateLanguageMenuChecks(index);

        Settings.Startup.Language = lang;
        CultureUtil.SetCultureLanguage(lang);

        Menu_Options.Close();

        var sav = C_SAV.SAV;
        LocalizeUtil.InitializeStrings(lang, sav, HaX);
        Translator.TranslateInterface(this, lang); // Translate the UI to language.
        LocalizedDescriptionAttribute.Localizer = Translator.GetDictionary(lang);

        SizeCPView.ResetSizeLocalizations(lang);
        PKME_Tabs.TryResetSizeStats();

        if (sav is not FakeSaveFile)
        {
            var pk = PKME_Tabs.CurrentPKM.Clone();

            PKME_Tabs.ChangeLanguage(sav);
            PKME_Tabs.PopulateFields(pk); // put data back in form
            Title = GetProgramTitle(sav);
            C_SAV.ReloadSlots(); // hover text language
        }
    }

    private void UpdateLanguageMenuChecks(int index)
    {
        foreach (var item in Menu_Language.Items.OfType<MenuItem>())
            item.IsChecked = item.Tag is int itemIndex && itemIndex == index;
    }
    #endregion

    #region //// PKX WINDOW FUNCTIONS ////
    private bool QR6Notified;

    private async Task ClickQR()
    {
        if (ModifierKeys == KeyModifiers.Alt)
        {
            string? url = await ClipboardService.GetText(this);
            if (!string.IsNullOrWhiteSpace(url))
            {
                if (url.StartsWith("http") && !url.Contains('\n')) // qr payload
                    await ImportQRToTabs(url);
                return;
            }
        }
        await ExportQRFromTabs();
    }

    private async Task ImportQRToTabs(string url)
    {
        var msg = await Task.Run(() => QRDecode.GetQRData(url, out var data) is var r ? (Result: r, Data: data) : default);
        if (msg.Result != 0)
        {
            await AppDialogs.Alert(this, msg.Result.ConvertMsg());
            return;
        }

        var input = msg.Data;
        if (input.Length == 0)
            return;

        var sav = C_SAV.SAV;
        if (FileUtil.TryGetPKM(input, out var pk, sav.Generation.ToString(), sav))
        {
            OpenPKM(pk);
            return;
        }
        if (FileUtil.TryGetMysteryGift(input, out var mg, url))
        {
            await OpenMysteryGift(mg, url);
            return;
        }

        await AppDialogs.Alert(this, MsgQRDecodeFail, string.Format(MsgQRDecodeSize, input.Length));
    }

    private async Task ExportQRFromTabs()
    {
        if (!PKME_Tabs.EditsComplete)
            return;

        PKM pk = PreparePKM();
        if (pk.Format == 6 && !QR6Notified) // hint that the user should not be using QR6 injection
        {
            await AppDialogs.Alert(this, MsgQRDeprecated, MsgQRAlternative);
            QR6Notified = true;
        }

        using var qr = QREncode.GenerateQRCode(pk);
        var sprite = pk.Sprite(C_SAV.SAV); // may be a shared instance (empty entity); release instead of dispose
        var la = new LegalityAnalysis(pk, C_SAV.SAV.Personal);
        using var indicator = la.Parsed && pk.Species != 0
            ? ImageUtil.LayerImage(sprite, SpriteUtil.GetLegalIndicator(la.Valid), sprite.Width - SpriteUtil.GetLegalIndicator(la.Valid).Width, 0)
            : sprite.CloneBitmap();
        sprite.Release();

        string[] r = pk.GetQRLines();
        string refer = GetProgramTitle();
        var form = new QRWindow(qr, indicator, pk, r[0], r[1], r[2], $"{refer} ({pk.GetType().Name})");
        await form.ShowDialog(this);
    }

    private async Task ClickLegality()
    {
        if (!PKME_Tabs.EditsComplete)
            return;

        var pk = PreparePKM();

        if (pk.Species == 0 || !pk.ChecksumValid)
            return;

        var la = new LegalityAnalysis(pk, C_SAV.SAV.Personal);
        PKME_Tabs.UpdateLegality(la);
        await DisplayLegalityReport(la);
    }

    private async Task DisplayLegalityReport(LegalityAnalysis la)
    {
        if (Settings.Display.IgnoreLegalPopup && la.Valid)
            return;

        var verbose = (ModifierKeys == KeyModifiers.Control) ^ Settings.Display.ExportLegalityAlwaysVerbose;
        var (text, clipboardText) = GetLegalityReportText(la, verbose);

        if (Settings.Display.ExportLegalityNeverClipboard)
        {
            await AppDialogs.Alert(this, MsgLegalityPopupCaption, text);
            return;
        }

        var options = new[] { "OK", MsgLegalityPopupCopyClipboard };
        var index = await AppDialogs.TrySelectIndex(this, MsgLegalityPopupCaption, text, options, 0);
        if (index == 1)
            await ClipboardService.SetText(this, clipboardText);
    }

    private static (string Text, string Clipboard) GetLegalityReportText(LegalityAnalysis la, bool verbose)
    {
        var localizer = LegalityLocalizationContext.Create(la, CurrentLanguage);
        var report = localizer.Report(verbose);
        var settings = localizer.Settings;
        var heading = la.Valid ? settings.Lines.Legal : settings.Lines.SInvalid;
        var text = heading + Environment.NewLine + Environment.NewLine + report;

        var verboseReport = localizer.Report(true);
        var enc = la.EncounterOriginal.GetTextLines(Settings.Display.ExportLegalityVerboseProperties);
        var clipboard = verboseReport + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, enc);
        return (text, clipboard);
    }

    private void GetPreview(PKM? pk = null)
    {
        pk ??= PreparePKM(false); // don't perform control loss click

        dragoutBorder.ContextMenu?.IsEnabled = pk.Species != 0 || HaX; // Species

        var img = pk.Sprite(C_SAV.SAV);
        if (App.IsDarkModeEnabled)
        {
            var avg = img.GetAverageColor();
            var c = System.Drawing.Color.FromArgb(avg);
            SpriteUtil.GetSpriteGlow(img, c.B, c.G, c.R, out var pixels, true);
            using var layer = ImageUtil.GetBitmap(pixels, img.Width, img.Height);
            var layered = ImageUtil.LayerImage(img, layer, 0, 0);
            img.Dispose();
            img = layered;
        }
        var old = dragoutImage;
        dragoutImage = img.ToAvaloniaBitmapAndDispose();
        dragout.Source = dragoutImage;
        old?.Dispose();
    }

    private void PKME_Tabs_UpdatePreviewSprite(object? sender, EventArgs e) => GetPreview();

    private void PKME_Tabs_LegalityChanged(bool isValid)
    {
        if (HaX)
        {
            PB_Legal.IsVisible = false;
            return;
        }

        PB_Legal.IsVisible = true;
        PB_Legal.Source = AppResources.GetImage(isValid ? "valid" : "warn");
        ToolTip.SetTip(PB_Legal, isValid ? MsgLegalityHoverValid : MsgLegalityHoverInvalid);
    }

    private PKM PreparePKM(bool click = true) => PKME_Tabs.PreparePKM(click);

    // Drag & Drop Events
    private static void Main_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void Main_DragDrop(object? sender, DragEventArgs e)
    {
        try
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files is not { Length: not 0 })
                return;
            var path = files[0].TryGetLocalPath();
            if (path is null)
                return;
            e.DragEffects = DragDropEffects.Copy;
            await OpenQuick(path);
        }
        catch (Exception ex) { await AppDialogs.Error(this, MsgFileLoadFail, ex); }
    }

    private async void Dragout_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            var props = e.GetCurrentPoint(dragoutBorder).Properties;
            if (!props.IsLeftButtonPressed)
                return;

            if (ModifierKeys is KeyModifiers.Alt or KeyModifiers.Shift)
            {
                await ClickQR();
                return;
            }

            if (!PKME_Tabs.EditsComplete)
                return;

            // Gather data
            var pk = PreparePKM();
            var preModify = pk.Clone();
            var encrypt = ModifierKeys == KeyModifiers.Control;
            var data = new byte[pk.SIZE_PARTY];
            if (!encrypt)
                pk.WriteDecryptedDataParty(data);
            else
                pk.WriteEncryptedDataParty(data);

            // Create Temp File to Drag
            var newFile = FileUtil.GetPKMTempFileName(pk, encrypt);
            try
            {
                await File.WriteAllBytesAsync(newFile, data);

                var file = await StorageProvider.TryGetFileFromPathAsync(newFile);
                if (file is null)
                    return;
                var transfer = new DataTransfer();
                transfer.Add(DataTransferItem.CreateFile(file));
                await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Copy);
            }
            // Tons of things can happen with drag & drop; don't try to handle things, just indicate failure.
            catch (Exception x)
            { await AppDialogs.Error(this, "Drag && Drop Error", x); }
            finally
            {
                _ = DeleteAsync(newFile, 20_000);
            }
            PKME_Tabs.NotifyWasExported(preModify); // restore pre-modify state, in case the user drags into the same program window
        }
        catch
        {
            // Ignore.
        }
    }

    private static async Task DeleteAsync(string path, int delay)
    {
        await Task.Delay(delay).ConfigureAwait(false);
        if (!File.Exists(path))
            return;

        try { File.Delete(path); }
        catch (Exception ex) { Debug.WriteLine(ex.Message); }
    }

    private void DragoutEnter(object? sender, PointerEventArgs e)
    {
        dragoutBackground.Source = PKME_Tabs.Entity.Species > 0 ? spriterSet : spriterDelete;
    }

    private void DragoutLeave(object? sender, PointerEventArgs e)
    {
        dragoutBackground.Source = spriterTransparent;
    }

    private async void Main_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (closeConfirmed)
            return;
        e.Cancel = true; // confirm asynchronously, then close for real
        try
        {
            if (C_SAV.SAV.State.Edited || PKME_Tabs.PKMIsUnsaved)
            {
                var prompt = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgProgramCloseUnsaved, MsgProgramCloseConfirm);
                if (prompt != DialogResult.Yes)
                    return;
            }

            await PKHeXSettings.SaveSettings(Program.PathConfig, Settings);
        }
        catch
        {
            // Ignore; program is shutting down.
        }
        closeConfirmed = true;
        Close();
    }

    #endregion

    #region //// SAVE FILE FUNCTIONS ////

    private async void ClickExportSAV(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!Menu_ExportSAV.IsEnabled)
                return; // hot-keys can't cheat the system!

            if (Settings.Advanced.SaveExportCheckUnsavedEntity && PKME_Tabs.PKMIsUnsaved)
            {
                var prompt = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgProgramSaveUnsaved, MsgContinue);
                if (prompt != DialogResult.Yes)
                    return;
            }

            await C_SAV.ExportSaveFile();
            Title = GetProgramTitle(C_SAV.SAV);
        }
        catch (Exception ex) { await AppDialogs.Error(this, MsgFileWriteFail, ex); }
    }

    public async Task PromptBackup(string folder)
    {
        if (Directory.Exists(folder))
            return;
        if (DialogResult.Yes != await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, string.Format(MsgBackupCreateLocation, folder), MsgBackupCreateQuestion))
            return;

        try
        {
            Directory.CreateDirectory(folder);
            await AppDialogs.Alert(this, MsgBackupSuccess, string.Format(MsgBackupDelete, folder));
        }
        catch (Exception ex)
        // Maybe they put their exe in a folder that we can't create files/folders to.
        { await AppDialogs.Error(this, $"{MsgBackupUnable} @ {folder}", ex); }
    }

    private void ClickUndo(object? sender, RoutedEventArgs e) => C_SAV.ClickUndo();
    private void ClickRedo(object? sender, RoutedEventArgs e) => C_SAV.ClickRedo();
    #endregion

    public Task WarnBehavior() => AppDialogs.Alert(this, MsgProgramIllegalModeActive, MsgProgramIllegalModeBehave);
}
