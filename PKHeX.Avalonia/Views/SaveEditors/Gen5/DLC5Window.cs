using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using PKHeX.Drawing;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen5;

/// <summary>
/// Downloadable-content editor for Generation 5: C-Gear skin, Pokédex skin, Battle Videos, Musicals, Memory Link,
/// PWT and Pokéstar Studios (port of the WinForms <c>SAV_DLC5</c>).
/// </summary>
public sealed class DLC5Window : SaveEditorWindow
{
    private readonly SAV5 Origin;
    private readonly SAV5 SAV;

    private readonly CGearBackground bg;
    private readonly PokeDexSkin5 dex;

    private const string CGearFilterBW = $"PokeStock C-Gear Skin Background|*.{CGearBackgroundBW.Extension}";
    private const string CGearFilterB2W2 = $"C-Gear Background|*.{CGearBackgroundB2W2.Extension}";

    private const string PWTFileName = "PWT";
    private const string MusicalShowFileName = "Musical Show";
    private const string BattleVideoFileName = "Generation 5 Battle Video";
    private const string PokeStarMovieFileName = "Pokestar Studio Movie";
    private const string MemoryLinkFileName = "Memory Link";
    private const string PokeDexFileName = "Pokédex Skin";
    private const string BattleTestFileName = "Battle Test";

    private const string MemoryLinkExtension = "ml5";

    private readonly TabControl TC_Tabs = new() { Name = "TC_Tabs" };

    private readonly Image PB_CGearBackground = new() { Name = "PB_CGearBackground", Width = 256, Height = 192, Stretch = Stretch.None };
    private readonly Border CGearFrame = new() { Width = 256, Height = 192, Background = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Button B_ImportPNG = UiFactory.Button("B_ImportPNG", "Load Image");
    private readonly Button B_ExportPNG = UiFactory.Button("B_ExportPNG", "Save Image");
    private readonly Button B_ImportCGB = UiFactory.Button("B_ImportCGB", "Import");
    private readonly Button B_ExportCGB = UiFactory.Button("B_ExportCGB", "Export");

    private readonly Button B_PokeDexSkinLoad = UiFactory.Button("B_PokeDexSkinLoad", "Import");
    private readonly Button B_PokeDexSkinSave = UiFactory.Button("B_PokeDexSkinSave", "Export");

    private readonly ObservableCollection<string> BattleVideos = [];
    private readonly ListBox LB_BattleVideo = new() { Name = "LB_BattleVideo", Width = 260, Height = 120 };
    private readonly Button B_BattleVideoImport = UiFactory.Button("B_BattleVideoImport", "Import");
    private readonly Button B_BattleVideoExport = UiFactory.Button("B_BattleVideoExport", "Export");
    private readonly Button B_BattleVideoExportDecrypted = UiFactory.Button("B_BattleVideoExportDecrypted", "Export Decrypted");

    private readonly Button B_MusicalImport = UiFactory.Button("B_MusicalImport", "Import");
    private readonly Button B_MusicalExport = UiFactory.Button("B_MusicalExport", "Export");

    private readonly Button B_Memory1Import = UiFactory.Button("B_Memory1Import", "Import Slot 1");
    private readonly Button B_Memory1Export = UiFactory.Button("B_Memory1Export", "Export Slot 1");
    private readonly Button B_Memory2Import = UiFactory.Button("B_Memory2Import", "Import Slot 2");
    private readonly Button B_Memory2Export = UiFactory.Button("B_Memory2Export", "Export Slot 2");

    private readonly Button B_BattleTestImport = UiFactory.Button("B_BattleTestImport", "Import");
    private readonly Button B_BattleTestExport = UiFactory.Button("B_BattleTestExport", "Export");

    private readonly ObservableCollection<string> PWTs = [];
    private readonly ListBox LB_PWT = new() { Name = "LB_PWT", Width = 260, Height = 90 };
    private readonly Button B_PWTImport = UiFactory.Button("B_PWTImport", "Import");
    private readonly Button B_PWTExport = UiFactory.Button("B_PWTExport", "Export");

    private readonly ObservableCollection<string> Pokestars = [];
    private readonly ListBox LB_Pokestar = new() { Name = "LB_Pokestar", Width = 260, Height = 180 };
    private readonly Button B_PokestarImport = UiFactory.Button("B_PokestarImport", "Import");
    private readonly Button B_PokestarExport = UiFactory.Button("B_PokestarExport", "Export");

    private string? LastImportedFile;

    public DLC5Window(SAV5 sav) : base("SAV_DLC5", "Downloadable Content")
    {
        SAV = (SAV5)(Origin = sav).Clone();

        var data = SAV.CGearSkinData;
        bg = SAV is SAV5BW ? new CGearBackgroundBW(data) : new CGearBackgroundB2W2(data);
        dex = new PokeDexSkin5(SAV.PokedexSkinData);

        LB_BattleVideo.ItemsSource = BattleVideos;
        LB_PWT.ItemsSource = PWTs;
        LB_Pokestar.ItemsSource = Pokestars;

        BuildTabs();
        SetBody(TC_Tabs);

        LoadCGear();
        LoadPokedexSkin();
        LoadBattleVideos();
        AttachEvents();
    }

    #region Layout

    private void BuildTabs()
    {
        if (SAV is SAV5B2W2 b2w2)
        {
            LoadPWT(b2w2);
            TC_Tabs.Items.Add(new TabItem { Name = "Tab_PWT", Header = "PWT", Content = BuildList(LB_PWT, B_PWTImport, B_PWTExport) });
        }

        TC_Tabs.Items.Add(new TabItem { Name = "Tab_CGear", Header = "C-Gear Skin", Content = BuildCGear() });
        TC_Tabs.Items.Add(new TabItem { Name = "Tab_PokeDex", Header = "PokéDex Skin", Content = BuildPokeDex() });
        TC_Tabs.Items.Add(new TabItem { Name = "Tab_BattleVideo", Header = "Battle Videos", Content = BuildList(LB_BattleVideo, B_BattleVideoImport, B_BattleVideoExport, B_BattleVideoExportDecrypted) });
        TC_Tabs.Items.Add(new TabItem { Name = "Tab_Musical", Header = "Musical", Content = BuildButtons(B_MusicalImport, B_MusicalExport) });

        if (SAV is SAV5B2W2 b2 )
        {
            LoadPokestar(b2);
            TC_Tabs.Items.Add(new TabItem { Name = "Tab_Pokestar", Header = "Pokéstar Studios", Content = BuildList(LB_Pokestar, B_PokestarImport, B_PokestarExport) });
        }

        TC_Tabs.Items.Add(new TabItem { Name = "Tab_MemoryLink", Header = "Memory Link", Content = BuildButtons(B_Memory1Import, B_Memory1Export, B_Memory2Import, B_Memory2Export) });
        TC_Tabs.Items.Add(new TabItem { Name = "Tab_BattleTest", Header = "Battle Test", Content = BuildBattleTest() });
        TC_Tabs.SelectedIndex = 0;
    }

    private Control BuildCGear()
    {
        CGearFrame.Child = PB_CGearBackground;
        var buttons = UiFactory.Row(B_ImportPNG, B_ExportPNG, B_ImportCGB, B_ExportCGB);
        buttons.Spacing = 6;
        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new global::Avalonia.Thickness(8),
            Spacing = 8,
            Children = { CGearFrame, buttons },
        };
    }

    private Control BuildPokeDex() => new StackPanel
    {
        Orientation = Orientation.Vertical,
        HorizontalAlignment = HorizontalAlignment.Left,
        Margin = new global::Avalonia.Thickness(8),
        Spacing = 8,
        Children =
        {
            UiFactory.Label("L_PokeDexSkin", "The Pokédex skin is imported and exported as raw data; the tile layout is not decoded."),
            UiFactory.Row(B_PokeDexSkinLoad, B_PokeDexSkinSave),
        },
    };

    private static Control BuildList(ListBox list, params Button[] buttons)
    {
        var row = UiFactory.Row(buttons);
        row.Spacing = 6;
        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new global::Avalonia.Thickness(8),
            Spacing = 8,
            Children = { list, row },
        };
    }

    private static Control BuildButtons(params Button[] buttons)
    {
        var row = UiFactory.Row(buttons);
        row.Spacing = 6;
        row.Margin = new global::Avalonia.Thickness(8);
        row.HorizontalAlignment = HorizontalAlignment.Left;
        return row;
    }

    private Control BuildBattleTest()
    {
        var warn = UiFactory.Label("L_BattleTest", "Not working. Needs research.");
        warn.Foreground = ColorUtilAvalonia.ColorWarn.ToBrush();
        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new global::Avalonia.Thickness(8),
            Spacing = 8,
            Children = { warn, UiFactory.Row(B_BattleTestImport, B_BattleTestExport) },
        };
    }

    #endregion

    #region Load

    private void LoadCGear()
    {
        if (bg.IsUninitialized)
        {
            B_ExportCGB.IsEnabled = B_ExportPNG.IsEnabled = false;
            PB_CGearBackground.Source = null;
            return;
        }
        PB_CGearBackground.Source = CGearImage.GetBitmap(bg).ToAvaloniaBitmapAndDispose();
    }

    private void LoadPokedexSkin()
    {
        if (dex.IsUninitialized)
            B_PokeDexSkinSave.IsEnabled = false;
    }

    private void LoadBattleVideos()
    {
        for (int i = 0; i < 4; i++)
        {
            var data = SAV.GetBattleVideo(i);
            var bvid = new BattleVideo5(data);
            var name = bvid.IsUninitialized ? "Empty" : bvid.GetTrainerNames();
            BattleVideos.Add($"{i:00} - {name}");
        }
        LB_BattleVideo.SelectedIndex = 0;
    }

    private void LoadPokestar(SAV5B2W2 sav)
    {
        for (int i = 0; i < SAV5B2W2.PokestarCount; i++)
        {
            var movie = new PokestarMovie5(sav.GetPokestarMovie(i));
            Pokestars.Add($"{i + 1:00} - {movie.Name}");
        }
        LB_Pokestar.SelectedIndex = 0;
    }

    private void LoadPWT(SAV5B2W2 sav)
    {
        for (int i = 0; i < SAV5B2W2.PWTCount; i++)
        {
            var pwt = new WorldTournament5(sav.GetPWT(i));
            var name = pwt.Name;
            if (string.IsNullOrWhiteSpace(name))
                name = "Empty";
            PWTs.Add($"{i + 1:00} - {name}");
        }
        LB_PWT.SelectedIndex = 0;
    }

    #endregion

    #region File helpers

    private async Task<byte[]?> ImportFile(string extension, string name, int expectSize, string? initialName = null, int otherSize = -1)
    {
        var path = await FileDialogs.OpenSingleFile(this, $"{name}|*.{extension}");
        if (path is null)
            return null;

        var length = new FileInfo(path).Length;
        if (length != expectSize && length != otherSize)
        {
            await AppDialogs.Error(this, $"Invalid file size. Expected {expectSize} bytes, got {length} bytes.");
            return null;
        }

        var data = File.ReadAllBytes(path);
        if (data.Length == otherSize)
            Array.Resize(ref data, expectSize);

        LastImportedFile = path;
        _ = initialName;
        return data;
    }

    private async Task ExportFile(string extension, string name, byte[] data, string? initialName = null)
    {
        if (string.IsNullOrWhiteSpace(initialName))
            initialName = string.IsNullOrWhiteSpace(name) ? "Empty" : name;
        var path = await FileDialogs.SaveFileDialog(this, $"{name}|*.{extension}", $"{PathUtil.CleanFileName(initialName)}.{extension}");
        if (path is null)
            return;
        File.WriteAllBytes(path, data);
    }

    private static string GetImportedMusicalName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).Trim();

        var split = name.LastIndexOf(" - ", StringComparison.Ordinal);
        if (split >= 0 && split + 3 < name.Length)
            name = name.AsSpan()[(split + 3)..].Trim().ToString();

        var suffix = name.LastIndexOf(" (", StringComparison.Ordinal);
        if (suffix > 0 && name[^1] == ')' && IsLikelyLanguageTag(name.AsSpan()[(suffix + 2)..^1]))
            name = name.AsSpan()[..suffix].TrimEnd().ToString();

        if (name.Length > Musical5.MusicalNameMaxLength)
            name = name.AsSpan()[..Musical5.MusicalNameMaxLength].TrimEnd().ToString();

        return name;
    }

    private static bool IsLikelyLanguageTag(ReadOnlySpan<char> value)
    {
        if (value.Length is < 2 or > 5)
            return false;

        foreach (var c in value)
        {
            if ((uint)(c - 'A') > 'Z' - 'A')
                return false;
        }

        return true;
    }

    #endregion

    #region Events

    private void AttachEvents()
    {
        B_ImportPNG.Click += async (_, _) => await ClickImportPNG();
        B_ExportPNG.Click += async (_, _) => await ClickExportPNG();
        B_ImportCGB.Click += async (_, _) => await ClickImportCGB();
        B_ExportCGB.Click += async (_, _) => await ExportFile(SAV is SAV5BW ? CGearBackgroundBW.Extension : CGearBackgroundB2W2.Extension, "C-Gear Background", bg.Data.ToArray());

        B_PokeDexSkinSave.Click += async (_, _) => await ExportFile(PokeDexSkin5.Extension, PokeDexFileName, SAV.PokedexSkinData.ToArray());
        B_PokeDexSkinLoad.Click += async (_, _) => await ClickImportPokeDexSkin();

        B_BattleVideoImport.Click += async (_, _) => await ClickImportBattleVideo();
        B_BattleVideoExport.Click += async (_, _) => await ClickExportBattleVideo(decrypt: false);
        B_BattleVideoExportDecrypted.Click += async (_, _) => await ClickExportBattleVideo(decrypt: true);

        B_MusicalImport.Click += async (_, _) => await ClickImportMusical();
        B_MusicalExport.Click += async (_, _) => await ExportFile(MusicalShow5.Extension, SAV.Musical.MusicalName, SAV.MusicalDownloadData.ToArray());

        B_Memory1Import.Click += async (_, _) => await ClickImportMemory(1);
        B_Memory2Import.Click += async (_, _) => await ClickImportMemory(2);
        B_Memory1Export.Click += async (_, _) => await ExportFile(MemoryLinkExtension, MemoryLinkFileName, SAV.Link1Data.ToArray());
        B_Memory2Export.Click += async (_, _) => await ExportFile(MemoryLinkExtension, MemoryLinkFileName, SAV.Link2Data.ToArray());

        B_BattleTestImport.Click += async (_, _) => await ClickImportBattleTest();
        B_BattleTestExport.Click += async (_, _) => await ExportFile(BattleTest5.Extension, BattleTestFileName, SAV.BattleTest.ToArray());

        B_PWTImport.Click += async (_, _) => await ClickImportPWT();
        B_PWTExport.Click += async (_, _) => await ClickExportPWT();
        B_PokestarImport.Click += async (_, _) => await ClickImportPokestar();
        B_PokestarExport.Click += async (_, _) => await ClickExportPokestar();
    }

    private async Task ClickImportPNG()
    {
        var path = await FileDialogs.OpenSingleFile(this, "PNG File|*.png");
        if (path is null)
            return;

        try
        {
            using var img = ImageUtil.Decode(File.ReadAllBytes(path));
            if (img is null)
            {
                await AppDialogs.Error(this, "Unable to decode the image.");
                return;
            }
            if (!CGearImage.IsInputCorrect(img, out var msg))
            {
                await AppDialogs.Alert(this, msg);
                return;
            }
            var result = CGearImage.GetCGearBackground(img, bg);
            SAV.SetCGearSkin(bg.Data);
            PB_CGearBackground.Source = CGearImage.GetBitmap(bg).ToAvaloniaBitmapAndDispose(); // regenerate rather than reuse input
            B_ExportCGB.IsEnabled = B_ExportPNG.IsEnabled = true;
            if (!CheckResult<CGearBackground>(result, out msg))
                await AppDialogs.Alert(this, msg);
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(this, ex.Message, ex);
        }
    }

    private static bool CheckResult<T>(TiledImageStat result, out string? msg) where T : ITiledImage
    {
        var tooManyColors = result.ColorCount > T.ColorCount;
        var tooManyTiles = result.TileCount > T.TilePoolCount;
        if (!tooManyColors && !tooManyTiles)
        {
            msg = null;
            return true; // Success
        }

        msg = "";
        if (tooManyColors)
            msg += $"Too many colors. Expected: {T.ColorCount}, received {result.ColorCount}";
        if (tooManyTiles)
            msg += (msg.Length != 0 ? Environment.NewLine : "") + $"Too many tiles. Expected {T.TilePoolCount}, received {result.TileCount}";
        return false;
    }

    private async Task ClickExportPNG()
    {
        if (bg.IsUninitialized)
            return;
        var path = await FileDialogs.SaveFileDialog(this, "PNG File|*.png", "Background.png");
        if (path is null)
            return;
        using var img = CGearImage.GetBitmap(bg);
        File.WriteAllBytes(path, CGearImage.EncodePng(img));
    }

    private async Task ClickImportCGB()
    {
        var isBW = SAV is SAV5BW;
        var filter = isBW ? $"{CGearFilterBW}|{CGearFilterB2W2}" : $"{CGearFilterB2W2}|{CGearFilterBW}";
        var path = await FileDialogs.OpenSingleFile(this, filter);
        if (path is null)
            return;

        var len = new FileInfo(path).Length;
        if (len != CGearBackground.SIZE)
        {
            await AppDialogs.Error(this, $"Incorrect size, got {len} bytes, expected {CGearBackground.SIZE} bytes.");
            return;
        }

        var data = File.ReadAllBytes(path);

        // Load the data and adjust it to the correct game format if not matching.
        CGearBackground temp = isBW ? new CGearBackgroundBW(data) : new CGearBackgroundB2W2(data);
        var isPSK = PaletteTileSelection.IsPaletteShiftFormat(temp.Arrange);

        try
        {
            if (isBW && !isPSK)
                PaletteTileSelection.ConvertToShiftFormat<CGearBackgroundBW>(temp.Arrange);
            else if (!isBW && isPSK)
                PaletteTileSelection.ConvertFromShiftFormat(temp.Arrange);
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(this, ex.Message, ex);
            return;
        }

        SAV.SetCGearSkin(temp.Data);
        PB_CGearBackground.Source = CGearImage.GetBitmap(bg).ToAvaloniaBitmapAndDispose();
        B_ExportCGB.IsEnabled = B_ExportPNG.IsEnabled = true;
    }

    private async Task ClickImportPokeDexSkin()
    {
        const int pporg = 0x6200; // missing 4 bytes, flag
        var data = await ImportFile(PokeDexSkin5.Extension, PokeDexFileName, SAV.PokedexSkinData.Length, otherSize: pporg);
        if (data is null)
            return;
        SAV.SetPokeDexSkin(data);
        B_PokeDexSkinSave.IsEnabled = true;
    }

    private async Task ClickImportBattleVideo()
    {
        var data = await ImportFile(BattleVideo5.Extension, BattleVideoFileName, BattleVideo5.SIZE);
        if (data is null)
            return;

        var decrypted = BattleVideo5.GetIsDecrypted(data);
        var bvid = new BattleVideo5(data) { IsDecrypted = decrypted };
        bvid.Encrypt();
        if (!bvid.IsUninitialized)
            bvid.RefreshChecksums();

        var index = LB_BattleVideo.SelectedIndex;
        if (index < 0)
            return;
        SAV.SetBattleVideo(index, data);
        var name = bvid.IsUninitialized ? "Empty" : bvid.GetTrainerNames();
        BattleVideos[index] = $"{index:00} - {name}";
        LB_BattleVideo.SelectedIndex = index;
    }

    private async Task ClickExportBattleVideo(bool decrypt)
    {
        var index = LB_BattleVideo.SelectedIndex;
        if (index < 0)
            return;
        var data = SAV.GetBattleVideo(index);
        if (!decrypt)
        {
            await ExportFile(BattleVideo5.Extension, BattleVideoFileName, data.ToArray());
            return;
        }

        var bvid = new BattleVideo5(data);
        var actual = !bvid.IsUninitialized;
        if (actual)
            bvid.Decrypt();
        var copy = data.ToArray(); // snapshot the decrypted bytes before restoring the save's copy
        if (actual)
            bvid.Encrypt();
        await ExportFile(BattleVideo5.Extension, BattleVideoFileName, copy);
    }

    private async Task ClickImportMusical()
    {
        var size = SAV is SAV5B2W2 ? MusicalShow5.SIZE_B2W2 : MusicalShow5.SIZE_BW;
        const int pporg = 0x17D78;
        var data = await ImportFile(MusicalShow5.Extension, MusicalShowFileName, size, otherSize: pporg);
        if (data is null)
            return;

        var musical = new MusicalShow5(data);
        SAV.SetMusical(data);
        if (LastImportedFile is { } name)
            SAV.Musical.MusicalName = musical.IsUninitialized ? "" : GetImportedMusicalName(name);
    }

    private async Task ClickImportMemory(int slot)
    {
        var length = slot == 1 ? SAV.Link1Data.Length : SAV.Link2Data.Length;
        var data = await ImportFile(MemoryLinkExtension, MemoryLinkFileName, length);
        if (data is null)
            return;
        if (slot == 1)
            SAV.SetLink1Data(data);
        else
            SAV.SetLink2Data(data);
    }

    private async Task ClickImportBattleTest()
    {
        var data = await ImportFile(BattleTest5.Extension, BattleTestFileName, BattleTest5.SIZE);
        if (data is null)
            return;

        // Ensure checksums are valid for user-fuzzed data.
        var test = new BattleTest5(data);
        if (!test.IsUninitialized)
        {
            test.Magic = BattleTest5.Sentinel;
            test.RefreshChecksums();
        }
        SAV.SetBattleTest(data);
    }

    private async Task ClickImportPWT()
    {
        const int pporg = 0x1314;
        var data = await ImportFile(WorldTournament5.Extension, PWTFileName, WorldTournament5.SIZE, otherSize: pporg);
        if (data is null)
            return;
        var index = LB_PWT.SelectedIndex;
        if (index < 0)
            return;
        var b2w2 = (SAV5B2W2)SAV;
        b2w2.SetPWT(index, data);
        var pwt = new WorldTournament5(data);
        var name = pwt.Name;
        if (string.IsNullOrWhiteSpace(name))
            name = "Empty";
        PWTs[index] = $"{index + 1:00} - {name}";
        LB_PWT.SelectedIndex = index;
    }

    private async Task ClickExportPWT()
    {
        var index = LB_PWT.SelectedIndex;
        if (index < 0)
            return;
        var b2w2 = (SAV5B2W2)SAV;
        var data = b2w2.GetPWT(index);
        var pwt = new WorldTournament5(data);
        var name = pwt.Name;
        if (string.IsNullOrWhiteSpace(name))
            name = "Empty";
        await ExportFile(WorldTournament5.Extension, PWTFileName, data.ToArray(), name);
    }

    private async Task ClickImportPokestar()
    {
        var data = await ImportFile(PokestarMovie5.Extension, PokeStarMovieFileName, PokestarMovie5.SIZE);
        if (data is null)
            return;
        var index = LB_Pokestar.SelectedIndex;
        if (index < 0)
            return;
        var b2w2 = (SAV5B2W2)SAV;
        b2w2.SetPokestarMovie(index, data);
        var movie = new PokestarMovie5(data);
        Pokestars[index] = $"{index + 1:00} - {movie.Name}";
        LB_Pokestar.SelectedIndex = index;
    }

    private async Task ClickExportPokestar()
    {
        var index = LB_Pokestar.SelectedIndex;
        if (index < 0)
            return;
        var b2w2 = (SAV5B2W2)SAV;
        await ExportFile(PokestarMovie5.Extension, PokeStarMovieFileName, b2w2.GetPokestarMovie(index).ToArray());
    }

    #endregion

    protected override void OnSave()
    {
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
