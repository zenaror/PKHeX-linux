using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Avalonia.Views.SaveEditors;
using PKHeX.Avalonia.Views.SaveEditors.Gen1;
using PKHeX.Avalonia.Views.SaveEditors.Gen2;
using PKHeX.Avalonia.Views.SaveEditors.Gen3;
using PKHeX.Avalonia.Views.SaveEditors.Gen4;
using PKHeX.Avalonia.Views.SaveEditors.Gen6;
using PKHeX.Avalonia.Views.SaveEditors.Gen7;
using PKHeX.Avalonia.Views.SaveEditors.Gen8;
using PKHeX.Avalonia.Views.SaveEditors.Gen9;
using PKHeX.Avalonia.Views.SaveEditors.Gen9.Donuts;
using PKHeX.Avalonia.Views.SaveEditors.DexEditor;
using PKHeX.Avalonia.Views.SaveEditors.Gen5;
using PKHeX.Core;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// SAV tab: save-wide tools and buttons that open the save sub-editors (port of the WinForms <c>Tab_SAV</c>).
/// </summary>
public sealed partial class SAVEditorView
{
    public readonly TabItem Tab_SAV = new() { Name = "Tab_SAV", Header = "SAV" };

    private readonly WrapPanel FLP_SAVToolsMisc = new() { Name = "FLP_SAVToolsMisc", Orientation = Orientation.Horizontal };
    private readonly Button B_SaveBoxBin = UiFactory.Button("B_SaveBoxBin", "Save Box Data++");
    private readonly Button B_VerifyCHK = UiFactory.Button("B_VerifyCHK", "Verify Checksums");
    private readonly Button B_VerifySaveEntities = UiFactory.Button("B_VerifySaveEntities", "Verify All PKMs");
    private readonly Button Menu_ExportBAK = UiFactory.Button("Menu_ExportBAK", "Export Backup");
    private readonly Button B_JPEG = UiFactory.Button("B_JPEG", "Save PGL .JPEG");
    private readonly Button B_ConvertKorean = UiFactory.Button("B_ConvertKorean", "Korean Save Conversion");

    private readonly TextBlock L_SaveSlot = UiFactory.Label("L_SaveSlot", "Save Slot:");
    private readonly ComboBox CB_SaveSlot = UiFactory.Combo("CB_SaveSlot", 160);

    private readonly WrapPanel FLP_SAVtools = new() { Name = "FLP_SAVtools", Orientation = Orientation.Horizontal };
    private readonly Button B_OpenTrainerInfo = UiFactory.Button("B_OpenTrainerInfo", "Trainer Info");
    private readonly Button B_OpenItemPouch = UiFactory.Button("B_OpenItemPouch", "Items");
    private readonly Button B_OpenBoxLayout = UiFactory.Button("B_OpenBoxLayout", "Box Layout");
    private readonly Button B_OpenWondercards = UiFactory.Button("B_OpenWondercards", "Wondercard");
    private readonly Button B_OpenOPowers = UiFactory.Button("B_OpenOPowers", "O-Powers");
    private readonly Button B_OpenEventFlags = UiFactory.Button("B_OpenEventFlags", "Event Flags");
    private readonly Button B_OpenPokedex = UiFactory.Button("B_OpenPokedex", "Pokédex");
    private readonly Button B_OpenLinkInfo = UiFactory.Button("B_OpenLinkInfo", "Link Data");
    private readonly Button B_OpenBerryField = UiFactory.Button("B_OpenBerryField", "Berry Field");
    private readonly Button B_OpenPokeblocks = UiFactory.Button("B_OpenPokeblocks", "Pokéblocks");
    private readonly Button B_OpenSecretBase = UiFactory.Button("B_OpenSecretBase", "Secret Base");
    private readonly Button B_OpenPokepuffs = UiFactory.Button("B_OpenPokepuffs", "Poké Puffs");
    private readonly Button B_OpenSuperTraining = UiFactory.Button("B_OpenSuperTraining", "Super Train");
    private readonly Button B_OpenHallofFame = UiFactory.Button("B_OpenHallofFame", "Hall of Fame");
    private readonly Button B_OUTPasserby = UiFactory.Button("B_OUTPasserby", "Passerby");
    private readonly Button B_DLC = UiFactory.Button("B_DLC", "DLC I/O");
    private readonly Button B_Donuts = UiFactory.Button("B_Donuts", "Donuts");
    private readonly Button B_OpenPokeBeans = UiFactory.Button("B_OpenPokeBeans", "Poké Beans");
    private readonly Button B_CellsStickers = UiFactory.Button("B_CellsStickers", "Cells/Stickers");
    private readonly Button B_OpenMiscEditor = UiFactory.Button("B_OpenMiscEditor", "Misc Edits");
    private readonly Button B_OpenHoneyTreeEditor = UiFactory.Button("B_OpenHoneyTreeEditor", "Honey Tree");
    private readonly Button B_OpenFriendSafari = UiFactory.Button("B_OpenFriendSafari", "Friend Safari");
    private readonly Button B_OpenRTCEditor = UiFactory.Button("B_OpenRTCEditor", "Clock (RTC)");
    private readonly Button B_OpenUGSEditor = UiFactory.Button("B_OpenUGSEditor", "Underground");
    private readonly Button B_OpenGeonetEditor = UiFactory.Button("B_OpenGeonetEditor", "Geonet");
    private readonly Button B_OpenUnityTowerEditor = UiFactory.Button("B_OpenUnityTowerEditor", "Unity Tower");
    private readonly Button B_OpenJoinAvenueEditor = UiFactory.Button("B_OpenJoinAvenueEditor", "Join Avenue");
    private readonly Button B_OpenPokeathlon = UiFactory.Button("B_OpenPokeathlon", "Pokéathlon");
    private readonly Button B_OpenMedalsEditor = UiFactory.Button("B_OpenMedalsEditor", "Medals");
    private readonly Button B_OpenChatterEditor = UiFactory.Button("B_OpenChatterEditor", "Chatter");
    private readonly Button B_Roamer = UiFactory.Button("B_Roamer", "Roamer");
    private readonly Button B_FestivalPlaza = UiFactory.Button("B_FestivalPlaza", "Festival Plaza");
    private readonly Button B_MailBox = UiFactory.Button("B_MailBox", "Mail Box");
    private readonly Button B_OpenApricorn = UiFactory.Button("B_OpenApricorn", "Apricorns");
    private readonly Button B_Raids = UiFactory.Button("B_Raids", "Raids");
    private readonly Button B_RaidsDLC1 = UiFactory.Button("B_RaidsDLC1", "Raids (DLC 1)");
    private readonly Button B_RaidsDLC2 = UiFactory.Button("B_RaidsDLC2", "Raids (DLC 2)");
    private readonly Button B_Blocks = UiFactory.Button("B_Blocks", "Block Data");
    private readonly Button B_OtherSlots = UiFactory.Button("B_OtherSlots", "Other Slots");
    private readonly Button B_OpenSealStickers = UiFactory.Button("B_OpenSealStickers", "Seal Stickers");
    private readonly Button B_Poffins = UiFactory.Button("B_Poffins", "Poffins");
    private readonly Button B_RaidsSevenStar = UiFactory.Button("B_RaidsSevenStar", "Raids (7 Star)");
    private readonly Button B_OpenBattlePass = UiFactory.Button("B_OpenBattlePass", "Battle Passes");
    private readonly Button B_OpenGear = UiFactory.Button("B_OpenGear", "Gear");
    private readonly Button B_OpenFashion = UiFactory.Button("B_OpenFashion", "Fashion");
    private readonly Button B_OpenGlobalLink = UiFactory.Button("B_OpenGlobalLink", "Pokémon Global Link");


    /// <summary>Raised when the user double-clicks the SAV tab (WinForms: reload the detected save file).</summary>
    public event EventHandler? RequestReloadSave;

    private const string NotPortedTip = "Not yet available in the Linux version.";

    private void BuildSavTab()
    {
        foreach (var b in new[] { B_SaveBoxBin, B_VerifyCHK, B_VerifySaveEntities, Menu_ExportBAK, B_JPEG, B_ConvertKorean })
            AddToolButton(FLP_SAVToolsMisc, b);

        Button[] tools =
        [
            B_OpenTrainerInfo, B_OpenItemPouch, B_OpenBoxLayout, B_OpenWondercards, B_OpenOPowers, B_OpenEventFlags,
            B_OpenPokedex, B_OpenLinkInfo, B_OpenBerryField, B_OpenPokeblocks, B_OpenSecretBase, B_OpenPokepuffs,
            B_OpenSuperTraining, B_OpenHallofFame, B_OUTPasserby, B_DLC, B_Donuts, B_OpenPokeBeans, B_CellsStickers,
            B_OpenMiscEditor, B_OpenHoneyTreeEditor, B_OpenFriendSafari, B_OpenRTCEditor, B_OpenUGSEditor,
            B_OpenGeonetEditor, B_OpenUnityTowerEditor, B_OpenJoinAvenueEditor, B_OpenPokeathlon, B_OpenMedalsEditor,
            B_OpenChatterEditor, B_Roamer, B_FestivalPlaza, B_MailBox, B_OpenApricorn, B_Raids, B_RaidsDLC1, B_RaidsDLC2,
            B_Blocks, B_OtherSlots, B_OpenSealStickers, B_Poffins, B_RaidsSevenStar, B_OpenBattlePass, B_OpenGear,
            B_OpenFashion, B_OpenGlobalLink,
        ];
        foreach (var b in tools)
            AddToolButton(FLP_SAVtools, b);

        // Sub-editors not yet ported: keep the button (for parity/visibility rules) but disable it.
        Button[] ported = [B_OpenTrainerInfo, B_OpenItemPouch, B_OpenBoxLayout, B_OpenWondercards, B_MailBox, B_OpenBerryField, B_OpenLinkInfo, B_OpenSuperTraining, B_OpenMedalsEditor, B_OpenUnityTowerEditor, B_OpenGlobalLink, B_OpenChatterEditor, B_OpenPokedex, B_OpenEventFlags, B_OpenMiscEditor, B_OpenRTCEditor, B_Roamer, B_OUTPasserby, B_OpenHoneyTreeEditor, B_OpenApricorn, B_OpenOPowers, B_OpenPokeblocks, B_OpenHallofFame, B_OpenGeonetEditor, B_OpenPokepuffs, B_OpenPokeBeans, B_CellsStickers, B_OpenSealStickers, B_Poffins, B_OpenUGSEditor, B_Blocks, B_Raids, B_RaidsDLC1, B_RaidsDLC2, B_RaidsSevenStar, B_OpenSecretBase, B_OpenPokeathlon, B_OpenJoinAvenueEditor, B_OpenGear, B_OpenBattlePass, B_OpenFriendSafari, B_Donuts, B_DLC, B_OpenFashion, B_FestivalPlaza, B_OtherSlots];
        foreach (var b in tools.Except(ported))
        {
            b.IsEnabled = false;
            ToolTip.SetTip(b, NotPortedTip);
        }

        var slotRow = UiFactory.Row(L_SaveSlot, CB_SaveSlot);
        slotRow.Margin = new Thickness(4, 2);

        var scroll = new ScrollViewer { Content = FLP_SAVtools, VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var layout = new DockPanel();
        DockPanel.SetDock(FLP_SAVToolsMisc, Dock.Top);
        DockPanel.SetDock(slotRow, Dock.Top);
        layout.Children.Add(FLP_SAVToolsMisc);
        layout.Children.Add(slotRow);
        layout.Children.Add(scroll);
        Tab_SAV.Content = layout;

        B_SaveBoxBin.Click += async (_, _) => await ClickSaveBoxBin();
        B_VerifyCHK.Click += async (_, _) => await ClickVerifyCHK();
        B_VerifySaveEntities.Click += async (_, _) => await ClickVerifyStoredEntities();
        Menu_ExportBAK.Click += async (_, _) => await ExportBackup();
        B_JPEG.Click += async (_, _) => await ClickJPEG();
        B_ConvertKorean.Click += async (_, _) => await ClickConvertKorean();
        CB_SaveSlot.SelectionChanged += (_, _) => UpdateSaveSlot();

        B_OpenTrainerInfo.Click += async (_, _) => await ClickTrainerInfo();
        B_OpenItemPouch.Click += async (_, _) => await OpenDialog(() => new InventoryWindow(SAV));
        B_OpenBoxLayout.Click += async (_, _) => await ClickBoxLayout();
        B_OpenWondercards.Click += async (_, _) => await OpenDialog(() => new WondercardWindow(SAV));
        B_MailBox.Click += async (_, _) => await OpenDialog(() => new MailBoxWindow(SAV));
        B_OpenBerryField.Click += async (_, _) => await OpenDialog(() => new BerryFieldXYWindow((SAV6XY)SAV));
        B_OpenLinkInfo.Click += async (_, _) => await OpenDialog(() => new Link6Window(SAV));
        B_OpenSuperTraining.Click += async (_, _) => await OpenDialog(() => new SuperTrainWindow((SAV6)SAV));
        B_OpenUnityTowerEditor.Click += async (_, _) => await OpenDialog(() => new UnityTowerWindow((SAV5)SAV));
        B_OpenGlobalLink.Click += async (_, _) => await OpenDialog(() => new GlobalLink5Window((SAV5)SAV));
        B_OpenMedalsEditor.Click += async (_, _) => await OpenDialog(() => new Medals5Window((SAV5B2W2)SAV));
        B_OpenChatterEditor.Click += async (_, _) => await OpenDialog(() => new ChatterWindow(SAV));
        B_OpenPokedex.Click += async (_, _) => await ClickPokedex();
        B_OpenEventFlags.Click += async (_, _) => await ClickEventFlags();
        B_OpenMiscEditor.Click += async (_, _) => await ClickMiscEditor();
        B_OpenRTCEditor.Click += async (_, _) => await ClickRTCEditor();
        B_Roamer.Click += async (_, _) => await ClickRoamer();
        B_OUTPasserby.Click += async (_, _) => await ClickPasserby();
        B_OpenHoneyTreeEditor.Click += async (_, _) => await OpenDialog(() => new HoneyTreeWindow((SAV4Sinnoh)SAV));
        B_OpenApricorn.Click += async (_, _) => await OpenDialog(() => new ApricornWindow((SAV4HGSS)SAV));
        B_OpenOPowers.Click += async (_, _) => await OpenDialog(() => new OPowerWindow((ISaveBlock6Main)SAV));
        B_OpenPokeblocks.Click += async (_, _) => await OpenDialog(() => new PokeBlockORASWindow((SAV6AO)SAV));
        B_OpenHallofFame.Click += async (_, _) => await ClickHallOfFame();
        B_OpenSecretBase.Click += async (_, _) => await ClickSecretBase();
        B_OpenPokeathlon.Click += async (_, _) => await OpenDialog(() => new Pokeathlon4Window((SAV4HGSS)SAV));
        B_OpenJoinAvenueEditor.Click += async (_, _) => await OpenDialog(() => new JoinAvenueWindow((SAV5B2W2)SAV));
        B_OpenGear.Click += async (_, _) => await OpenDialog(() => new Gear4BRWindow((SAV4BR)SAV));
        B_OpenFriendSafari.Click += async (_, _) => await ClickFriendSafari();
        B_OpenBattlePass.Click += async (_, _) => await OpenDialog(() => new BattlePass4BRWindow(this, (SAV4BR)SAV));
        B_Donuts.Click += async (_, _) => await OpenDialog(() => new Donut9aWindow((SAV9ZA)SAV));
        B_DLC.Click += async (_, _) => await ClickDLC();
        B_OpenFashion.Click += async (_, _) => await OpenDialog(() => new Fashion9Window(SAV));
        B_FestivalPlaza.Click += async (_, _) => await OpenDialog(() => new FestivalPlazaWindow((SAV7)SAV));
        B_OtherSlots.Click += (_, _) => ClickOtherSlots();
        B_OpenGeonetEditor.Click += async (_, _) => await OpenDialog(() => new Geonet4Window((SAV4)SAV));
        B_OpenPokepuffs.Click += async (_, _) => await OpenDialog(() => new PokepuffWindow((ISaveBlock6Main)SAV));
        B_OpenPokeBeans.Click += async (_, _) => await OpenDialog(() => new PokebeanWindow((SAV7)SAV));
        B_CellsStickers.Click += async (_, _) => await OpenDialog(() => new ZygardeCellWindow((SAV7)SAV));
        B_OpenSealStickers.Click += async (_, _) => await OpenDialog(() => new SealStickers8bWindow((SAV8BS)SAV));
        B_Poffins.Click += async (_, _) => await OpenDialog(() => new Poffin8bWindow((SAV8BS)SAV));
        B_OpenUGSEditor.Click += async (_, _) => await ClickUnderground();
        B_Blocks.Click += async (_, _) => await ClickBlocks();
        B_Raids.Click += async (_, _) => await ClickRaids(B_Raids);
        B_RaidsDLC1.Click += async (_, _) => await ClickRaids(B_RaidsDLC1);
        B_RaidsDLC2.Click += async (_, _) => await ClickRaids(B_RaidsDLC2);
        B_RaidsSevenStar.Click += async (_, _) => await ClickRaids(B_RaidsSevenStar);

        tabBoxMulti.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.ClickCount != 2 || !e.GetCurrentPoint(tabBoxMulti).Properties.IsLeftButtonPressed)
                return;
            if (e.Source is not Visual v || tabBoxMulti.SelectedItem != Tab_SAV)
                return;
            // Only when the tab strip (not the content) is double-clicked.
            if (v.FindAncestorOfType<TabItem>() != Tab_SAV || v.FindAncestorOfType<Button>() is not null)
                return;
            RequestReloadSave?.Invoke(this, EventArgs.Empty);
        }, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private static void AddToolButton(Panel panel, Button b)
    {
        b.Margin = new Thickness(3);
        b.Padding = new Thickness(8, 4);
        b.MinWidth = 100;
        panel.Children.Add(b);
    }

    /// <summary>
    /// Opens a sub-editor, surfacing construction/runtime errors instead of losing them on a fire-and-forget task.
    /// </summary>
    private GroupViewerWindow? GroupViewer;

    /// <summary>Opens the registered-team viewer for a Stadium save (port of <c>B_OtherSlots_Click</c>).</summary>
    private void ClickOtherSlots()
    {
        if (SAV is not SAV_STADIUM stadium || Owner is null)
            return;
        if (GroupViewer is null || !GroupViewer.IsVisible)
        {
            GroupViewer = new GroupViewerWindow(stadium, EditEnv.PKMEditor, stadium.GetRegisteredTeams());
            GroupViewer.Show(Owner);
            return;
        }
        GroupViewer.Activate();
    }

    /// <summary>Opens the DLC editor for the loaded save (port of <c>B_DLC_Click</c>).</summary>
    private async Task ClickDLC()
    {
        if (SAV is SAV5 s5)
            await OpenDialog(() => new DLC5Window(s5));
        else if (SAV is SAV4 s4)
            await OpenDialog(() => new DLC4Window(s4));
    }

    private async Task OpenDialog(Func<Window> create)
    {
        if (Owner is null)
            return;
        try
        {
            var w = create();
            await w.ShowDialog(Owner);
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(Owner, ex.Message, ex);
        }
    }

    private void ToggleViewSubEditors(SaveFile sav)
    {
        if (!sav.State.Exportable || sav is BulkStorage)
        {
            FLP_SAVtools.IsVisible = false;
            B_JPEG.IsVisible = false;
            B_ConvertKorean.IsVisible = false;
            return;
        }

        B_ConvertKorean.IsVisible = sav is SAV4;
        B_OpenPokeblocks.IsVisible = sav is SAV6AO;
        B_OpenSecretBase.IsVisible = sav is SAV6AO or SAV3 { LargeBlock: ISaveBlock3LargeHoenn };
        B_OpenSecretBase.IsEnabled = sav is SAV6AO or SAV3 { LargeBlock: ISaveBlock3LargeHoenn };
        B_OpenPokepuffs.IsVisible = sav is ISaveBlock6Main;
        B_JPEG.IsVisible = B_OpenLinkInfo.IsVisible = B_OpenSuperTraining.IsVisible = B_OUTPasserby.IsVisible = sav is ISaveBlock6Main;
        B_OpenBoxLayout.IsVisible = sav is IBoxDetailName;
        B_OpenWondercards.IsVisible = sav is IMysteryGiftStorageProvider;
        B_OpenWondercards.IsEnabled = sav is IMysteryGiftStorageProvider && sav.Generation is 4 or 5 or 6 or 7; // the album layout only covers Gen 4-7
        if (!B_OpenWondercards.IsEnabled)
            ToolTip.SetTip(B_OpenWondercards, NotPortedTip);
        B_OpenHallofFame.IsVisible = sav is ISaveBlock6Main or SAV7 or SAV3 { IsMisconfiguredSize: false } or SAV1;
        B_OpenHallofFame.IsEnabled = sav is SAV7 or SAV1 or SAV6 or SAV3 { IsMisconfiguredSize: false };
        if (!B_OpenHallofFame.IsEnabled)
            ToolTip.SetTip(B_OpenHallofFame, NotPortedTip);
        B_OpenOPowers.IsVisible = sav is ISaveBlock6Main;
        B_OpenPokedex.IsVisible = sav.HasPokeDex;
        B_OpenPokedex.IsEnabled = sav is SAV1 or SAV2 or SAV3 or SAV4 or SAV5 or SAV6XY or SAV6AO or SAV7 or SAV7b or SAV8BS or SAV8SWSH or SAV8LA or SAV9SV or SAV9ZA;
        if (!B_OpenPokedex.IsEnabled)
            ToolTip.SetTip(B_OpenPokedex, NotPortedTip);
        B_OpenBerryField.IsVisible = sav is SAV6XY; // OR/AS undocumented
        B_OpenFriendSafari.IsVisible = B_OpenFriendSafari.IsEnabled = sav is SAV6XY;
        B_OpenEventFlags.IsVisible = sav is IEventFlag37 or IEventFlagProvider37 or SAV1 or SAV2 or SAV8BS or SAV7b or SAV9ZA;
        B_OpenEventFlags.IsEnabled = sav is IEventFlag37 or IEventFlagProvider37 or SAV1 or SAV2 or SAV9ZA or SAV8BS or SAV7b;
        if (!B_OpenEventFlags.IsEnabled)
            ToolTip.SetTip(B_OpenEventFlags, NotPortedTip);
        B_DLC.IsVisible = sav is SAV5 or SAV4HGSS or SAV4Pt;
        B_OpenPokeBeans.IsVisible = B_CellsStickers.IsVisible = B_FestivalPlaza.IsVisible = sav is SAV7;

        B_OtherSlots.IsVisible = sav is SAV1StadiumJ or SAV1Stadium or SAV2Stadium;
        B_OpenTrainerInfo.IsVisible = sav.HasParty || SAV is SAV7b; // Box RS
        B_OpenItemPouch.IsVisible = (sav.HasParty && SAV is not SAV4BR) || SAV is SAV7b; // Box RS & Battle Revolution
        B_OpenMiscEditor.IsVisible = sav is SAV2 { Version: GameVersion.C } or SAV3 or SAV4 or SAV5 or SAV8BS;
        B_OpenMiscEditor.IsEnabled = sav is SAV2 or SAV3 or SAV4 or SAV5 or SAV8BS;
        if (!B_OpenMiscEditor.IsEnabled)
            ToolTip.SetTip(B_OpenMiscEditor, NotPortedTip);
        B_Roamer.IsVisible = sav is SAV3 or SAV6XY;

        B_OpenHoneyTreeEditor.IsVisible = sav is SAV4Sinnoh;
        B_OpenUGSEditor.IsVisible = sav is SAV4Sinnoh or SAV8BS;
        B_OpenUGSEditor.IsEnabled = sav is SAV8BS or SAV4Sinnoh;
        B_OpenGeonetEditor.IsVisible = sav is SAV4;
        B_OpenGlobalLink.IsVisible = B_OpenUnityTowerEditor.IsVisible = sav is SAV5;
        B_OpenJoinAvenueEditor.IsVisible = B_OpenJoinAvenueEditor.IsEnabled = B_OpenMedalsEditor.IsVisible = sav is SAV5B2W2;
        B_OpenChatterEditor.IsVisible = sav is SAV4 or SAV5;
        B_OpenBattlePass.IsVisible = B_OpenGear.IsVisible = sav is SAV4BR;
        B_OpenGear.IsEnabled = B_OpenBattlePass.IsEnabled = sav is SAV4BR;
        B_OpenSealStickers.IsVisible = B_Poffins.IsVisible = sav is SAV8BS;
        B_OpenApricorn.IsVisible = sav is SAV4HGSS;
        B_OpenPokeathlon.IsVisible = B_OpenPokeathlon.IsEnabled = sav is SAV4HGSS;
        B_OpenRTCEditor.IsVisible = (sav.Generation == 2 && sav is not SAV2Stadium) || sav is SAV3 { SmallBlock: ISaveBlock3SmallHoenn };
        B_MailBox.IsVisible = sav is SAV2 or SAV2Stadium or SAV3 or SAV4 or SAV5;

        B_Raids.IsVisible = sav is SAV8SWSH or SAV9SV;
        B_RaidsSevenStar.IsVisible = sav is SAV9SV;
        B_RaidsDLC1.IsVisible = sav is SAV8SWSH { SaveRevision: >= 1 } or SAV9SV { SaveRevision: >= 1 };
        B_RaidsDLC2.IsVisible = sav is SAV8SWSH { SaveRevision: >= 2 } or SAV9SV { SaveRevision: >= 2 };
        FLP_SAVtools.IsVisible = B_Blocks.IsVisible = true;
        B_Blocks.IsEnabled = sav is ISCBlockArray; // the per-generation block accessor viewer is not ported
        if (!B_Blocks.IsEnabled)
            ToolTip.SetTip(B_Blocks, NotPortedTip);

        B_OpenFashion.IsVisible = sav is SAV9SV or SAV9ZA;
        B_Donuts.IsVisible = sav is SAV9ZA { SaveRevision: >= 1 };

        var list = FLP_SAVtools.Children.OfType<Button>().OrderBy(z => z.Content as string, StringComparer.CurrentCulture).ToArray();
        FLP_SAVtools.Children.Clear();
        FLP_SAVtools.Children.AddRange(list);
    }

    private void ToggleViewMisc(SaveFile sav)
    {
        // Generational Interface
        B_VerifyCHK.IsVisible = SAV.State.Exportable;
        Menu_ExportBAK.IsVisible = SAV.State.Exportable && SAV.Metadata.FilePath is not null;
        B_SaveBoxBin.IsEnabled = sav.HasBox;

        if (sav is SAV4BR br)
        {
            L_SaveSlot.IsVisible = CB_SaveSlot.IsVisible = true;
            var current = br.CurrentSlot;
            var list = br.SaveNames.Select((z, i) => new ComboItem(z, i)).ToList();
            CB_SaveSlot.SetItems(list);
            CB_SaveSlot.SetValue(current);
        }
        else
        {
            L_SaveSlot.IsVisible = CB_SaveSlot.IsVisible = false;
        }
    }

    private void UpdateSaveSlot()
    {
        var index = CB_SaveSlot.GetValue();
        if (SAV is not SAV4BR br || br.CurrentSlot == index)
            return;

        br.CurrentSlot = index;
        Box.ResetBoxNames(); // fix box names
        SetPKMBoxes();
        UpdateBoxViewers(true);
    }

    private async Task ClickSaveBoxBin()
    {
        if (!SAV.HasBox)
        { await AppDialogs.Alert(Owner, MsgSaveBoxFailNone); return; }
        await SaveBoxBinary();
    }

    private async Task<bool> SaveBoxBinary()
    {
        if (Owner is null)
            return false;
        var dr = await AppDialogs.Prompt(Owner, MessageBoxButtons.YesNoCancel,
            MsgSaveBoxExportYes + Environment.NewLine +
            string.Format(MsgSaveBoxExportNo, Box.CurrentBoxName, CurrentBox + 1) + Environment.NewLine +
            MsgSaveBoxExportCancel);

        if (dr == DialogResult.Yes)
        {
            var path = await FileDialogs.SaveFileDialog(Owner, "Box Data|*.bin", "pcdata.bin");
            if (path is null)
                return false;
            File.WriteAllBytes(path, SAV.GetPCBinary());
            return true;
        }
        if (dr == DialogResult.No)
        {
            var path = await FileDialogs.SaveFileDialog(Owner, "Box Data|*.bin", $"boxdata {Box.CurrentBoxName}.bin");
            if (path is null)
                return false;
            File.WriteAllBytes(path, SAV.GetBoxBinary(CurrentBox));
            return true;
        }
        return false;
    }

    private async Task ClickVerifyCHK()
    {
        if (SAV.State.Edited)
        {
            await AppDialogs.Alert(Owner, MsgSaveChecksumFailEdited);
            return;
        }
        if (SAV.ChecksumsValid)
        {
            await AppDialogs.Alert(Owner, MsgSaveChecksumValid);
            return;
        }

        if (DialogResult.Yes == await AppDialogs.Prompt(Owner, MessageBoxButtons.YesNo, MsgSaveChecksumFailExport))
            await ClipboardService.SetText(Owner, SAV.ChecksumInfo);
    }

    private async Task ClickVerifyStoredEntities()
    {
        var bulk = new Core.Bulk.BulkAnalysis(SAV, MainWindow.Settings.Legality.Bulk);
        if (bulk.Valid)
        {
            await AppDialogs.Alert(Owner, "Clean!");
            return;
        }

        if (await AppDialogs.Prompt(Owner, MessageBoxButtons.YesNo, MsgClipboardLegalityExport) != DialogResult.Yes)
            return;

        var localization = LegalityLocalizationSet.GetLocalization(MainWindow.CurrentLanguage);
        var msg = bulk.Report(localization);
        await ClipboardService.SetText(Owner, msg);
    }

    public async Task<bool> ExportBackup()
    {
        if (Owner is null || !SAV.State.Exportable || SAV.Metadata.FilePath is not { } file)
            return false;

        if (!File.Exists(file))
        {
            await AppDialogs.Error(Owner, MsgSaveBackupNotFound, file);
            return false;
        }

        var suggestion = PathUtil.CleanFileName(SAV.Metadata.BAKName);
        var path = await FileDialogs.SaveFileDialog(Owner, null, suggestion);
        if (path is null)
            return false;

        if (!File.Exists(file)) // did they move it again?
        {
            await AppDialogs.Error(Owner, MsgSaveBackupNotFound, file);
            return false;
        }
        File.Copy(file, path, true);
        await AppDialogs.Alert(Owner, MsgSaveBackup, path);
        return true;
    }

    private async Task ClickJPEG()
    {
        if (Owner is null || SAV is not SAV6 s6)
            return;
        var jpeg = s6.GetJPEGData();
        if (jpeg.Length == 0)
        {
            await AppDialogs.Alert(Owner, MsgSaveJPEGExportFail);
            return;
        }
        var data = jpeg.ToArray(); // span cannot cross the await
        string filename = $"{s6.JPEGTitle}'s picture";
        var path = await FileDialogs.SaveFileDialog(Owner, "JPEG|*.jpeg", filename);
        if (path is null)
            return;
        File.WriteAllBytes(path, data);
    }

    private async Task ClickConvertKorean()
    {
        if (SAV is not SAV4 s4)
            return;
        var isKorean = s4.Magic == SAV4.MAGIC_KOREAN;
        var msg = isKorean ? MsgSaveGen4ConvertInternational : MsgSaveGen4ConvertKorean;
        if (DialogResult.Yes != await AppDialogs.Prompt(Owner, MessageBoxButtons.YesNo, msg))
            return;
        s4.Magic = isKorean ? SAV4.MAGIC_JAPAN_INTL : SAV4.MAGIC_KOREAN;
        SAV.State.Edited = true;
    }

    /// <summary>
    /// Opens the Pokédex editor for the loaded save (port of <c>B_OpenPokedex_Click</c>).
    /// </summary>
    /// <remarks>Only the simple (Gen 1-3) editor is ported; the per-game editors are not available yet.</remarks>
    /// <summary>Trainer editor, per generation (port of <c>B_OpenTrainerInfo_Click</c>).</summary>
    private async Task ClickTrainerInfo()
    {
        if (SAV is SAV6 sav6)
        {
            await OpenDialog(() => new Trainer6Window(sav6));
            return;
        }
        if (SAV is SAV7b sav7b)
        {
            await OpenDialog(() => new Trainer7GGWindow(sav7b));
            return;
        }
        if (SAV is SAV7 sav7)
        {
            await OpenDialog(() => new Trainer7Window(sav7));
            return;
        }
        if (SAV is SAV8BS sav8bs)
        {
            await OpenDialog(() => new Trainer8bWindow(sav8bs));
            return;
        }
        if (SAV is SAV8SWSH sav8swsh)
        {
            await OpenDialog(() => new Trainer8Window(sav8swsh));
            return;
        }
        if (SAV is SAV9SV sav9sv)
        {
            await OpenDialog(() => new Trainer9Window(sav9sv));
            return;
        }
        if (SAV is SAV8LA sav8la)
        {
            await OpenDialog(() => new Trainer8aWindow(sav8la));
            return;
        }
        if (SAV is SAV9ZA sav9za)
        {
            await OpenDialog(() => new Trainer9aWindow(sav9za));
            return;
        }
        await OpenDialog(() => new SimpleTrainerWindow(SAV));
    }

    private async Task ClickPokedex()
    {
        switch (SAV)
        {
            case SAV5 sav5dex:
                await OpenDialog(() => new Pokedex5Window(sav5dex));
                return;
            case SAV6XY sav6xy:
                await OpenDialog(() => new PokedexXYWindow(sav6xy));
                return;
            case SAV6AO sav6ao:
                await OpenDialog(() => new PokedexORASWindow(sav6ao));
                return;
            case SAV7b sav7b:
                await OpenDialog(() => new PokedexGGWindow(sav7b));
                return;
            case SAV7 sav7 when SAV is not SAV7b:
                await OpenDialog(() => new PokedexSMWindow(sav7));
                return;
            case SAV4 sav4:
                await OpenDialog(() => new Pokedex4Window(sav4));
                return;
            case SAV8BS sav8bsDex:
                await OpenDialog(() => new PokedexBDSPWindow(sav8bsDex));
                return;
            case SAV8SWSH sav8swshDex:
                await OpenDialog(() => new PokedexSWSHWindow(sav8swshDex));
                return;
            case SAV8LA sav8laDex:
                await OpenDialog(() => new PokedexLAWindow(sav8laDex));
                return;
            case SAV9ZA sav9zaDex:
                await OpenDialog(() => new Pokedex9aWindow(sav9zaDex));
                return;
            case SAV9SV { SaveRevision: 0 } sav9svDex:
                await OpenDialog(() => new PokedexSVWindow(sav9svDex));
                return;
            case SAV9SV sav9svDlcDex:
                await OpenDialog(() => new PokedexSVKitakamiWindow(sav9svDlcDex));
                return;
        }
        if (SAV is SAV1 or SAV2 or SAV3)
        {
            await OpenDialog(() => new SimplePokedexWindow(SAV));
            return;
        }
        await AppDialogs.Alert(Owner, NotPortedTip);
    }

    /// <summary>
    /// Opens the event flag editor for the loaded save (port of <c>B_OpenEventFlags_Click</c>).
    /// </summary>
    /// <remarks>Gen 1/2, Let's Go, BDSP and Legends: Z-A use their own editors, which are not ported yet.</remarks>
    private async Task ClickEventFlags()
    {
        switch (SAV)
        {
            case IEventFlag37 g37:
                await OpenDialog(() => new EventFlagsWindow(g37, SAV.Version));
                return;
            case IEventFlagProvider37 p:
                await OpenDialog(() => new EventFlagsWindow(p.EventWork, SAV.Version));
                return;
            case SAV9ZA sav9za:
                await OpenDialog(() => new FlagWork9aWindow(sav9za));
                return;
            case SAV8BS sav8bs:
                await OpenDialog(() => new FlagWork8bWindow(sav8bs));
                return;
            case SAV7b sav7bFlags:
                await OpenDialog(() => new EventWork7bWindow(sav7bFlags));
                return;
            case SAV2 sav2:
                await OpenDialog(() => new EventFlags2Window(sav2));
                return;
            case SAV1 sav1:
                await OpenDialog(() => new EventReset1Window(sav1));
                return;
            default:
                await AppDialogs.Alert(Owner, NotPortedTip);
                return;
        }
    }

    /// <summary>Raw block browser; available for the block-based saves from Sword/Shield onwards.</summary>
    private async Task ClickBlocks()
    {
        if (SAV is ISCBlockArray blocks)
            await OpenDialog(() => new BlockDump8Window(blocks));
        else
            await AppDialogs.Alert(Owner, NotPortedTip);
    }

    /// <summary>Underground editor; only the Brilliant Diamond / Shining Pearl variant is ported.</summary>
    private async Task ClickUnderground()
    {
        if (SAV is SAV8BS bs)
            await OpenDialog(() => new Underground8bWindow(bs));
        else if (SAV is SAV4Sinnoh s4)
            await OpenDialog(() => new Underground4Window(s4));
        else
            await AppDialogs.Alert(Owner, NotPortedTip);
    }

    /// <summary>Miscellaneous per-game edits (port of <c>B_OpenMiscEditor_Click</c>).</summary>
    private async Task ClickMiscEditor()
    {
        if (SAV is SAV5 sav5misc)
        {
            await OpenDialog(() => new Misc5Window(sav5misc));
            return;
        }
        if (SAV is SAV8BS sav8bsMisc)
        {
            await OpenDialog(() => new Misc8bWindow(sav8bsMisc));
            return;
        }
        if (SAV is SAV3 sav3misc)
        {
            await OpenDialog(() => new Misc3Window(sav3misc));
            return;
        }
        if (SAV is SAV4 sav4misc)
        {
            await OpenDialog(() => new Misc4Window(sav4misc));
            return;
        }
        if (SAV is SAV2 sav2)
            await OpenDialog(() => new Misc2Window(sav2));
        else
            await AppDialogs.Alert(Owner, NotPortedTip);
    }

    /// <summary>Clock editor (port of <c>B_OpenRTCEditor_Click</c>).</summary>
    private async Task ClickRTCEditor()
    {
        switch (SAV.Generation)
        {
            case 2:
            {
                var sav2 = (SAV2)SAV;
                var msg = MsgSaveGen2RTCResetBitflag;
                if (!sav2.Japanese) // show Reset Key for non-Japanese saves
                    msg = string.Format(MsgSaveGen2RTCResetPassword, sav2.ResetKey) + Environment.NewLine + Environment.NewLine + msg;
                if (await AppDialogs.Prompt(Owner, MessageBoxButtons.YesNo, msg) == DialogResult.Yes)
                    sav2.ResetRTC();
                break;
            }
            case 3:
                await OpenDialog(() => new RTC3Window(SAV));
                break;
        }
    }

    /// <summary>Roaming Pokémon editor (port of <c>B_Roamer_Click</c>).</summary>
    private async Task ClickRoamer()
    {
        if (SAV is SAV6XY sav6xy)
        {
            await OpenDialog(() => new Roamer6Window(sav6xy));
            return;
        }
        if (SAV is SAV3 s3)
            await OpenDialog(() => new Roamer3Window(s3));
        else
            await AppDialogs.Alert(Owner, NotPortedTip);
    }

    /// <summary>Copies the Gen 6 passerby list to the clipboard (port of <c>B_OUTPasserby_Click</c>).</summary>
    private async Task ClickPasserby()
    {
        if (SAV.Generation != 6)
            return;
        if (DialogResult.Yes != await AppDialogs.Prompt(Owner, MessageBoxButtons.YesNo, MsgSaveGen6Passerby))
            return;
        var result = PSS6.GetPSSParse((SAV6)SAV);
        await ClipboardService.SetText(Owner, string.Join(Environment.NewLine, result));
    }

    /// <summary>Hall of Fame editor (port of <c>B_HallofFame_Click</c>).</summary>
    /// <remarks>Only the Gen 7 editor is ported; Gen 1/3/6 use their own forms.</remarks>
    /// <summary>Unlocks every Friend Safari slot (port of <c>B_OpenFriendSafari_Click</c>).</summary>
    private async Task ClickFriendSafari()
    {
        if (SAV is not SAV6XY xy)
            return;
        var dr = await AppDialogs.Prompt(Owner, MessageBoxButtons.YesNo, MsgSaveGen6FriendSafari, MsgSaveGen6FriendSafariCheatDesc);
        if (dr == DialogResult.Yes)
            xy.UnlockAllFriendSafariSlots();
    }

    /// <summary>Secret Base editor (port of <c>B_OpenSecretBase_Click</c>).</summary>
    private async Task ClickSecretBase()
    {
        if (SAV is SAV3 { LargeBlock: ISaveBlock3LargeHoenn } s3)
            await OpenDialog(() => new SecretBase3Window(s3));
        else if (SAV is SAV6AO s6ao)
            await OpenDialog(() => new SecretBase6Window(s6ao));
        else
            await AppDialogs.Alert(Owner, NotPortedTip);
    }

    private async Task ClickHallOfFame()
    {
        switch (SAV)
        {
            case SAV7 s7:
                await OpenDialog(() => new HallOfFame7Window(s7));
                return;
            case SAV1 s1:
                await OpenDialog(() => new HallOfFame1Window(s1));
                return;
            case SAV6 s6:
                await OpenDialog(() => new HallOfFame6Window(s6));
                return;
            case SAV3 s3:
                await OpenDialog(() => new HallOfFame3Window(s3));
                return;
            default:
                await AppDialogs.Alert(Owner, NotPortedTip);
                return;
        }
    }

    /// <summary>Raid den editors (port of <c>B_OpenRaids_Click</c>).</summary>
    private async Task ClickRaids(Button sender)
    {
        if (SAV is SAV9SV sv)
        {
            if (sender == B_Raids)
                await OpenDialog(() => new Raid9Window(sv, TeraRaidOrigin.Paldea));
            else if (sender == B_RaidsDLC1)
                await OpenDialog(() => new Raid9Window(sv, TeraRaidOrigin.Kitakami));
            else if (sender == B_RaidsDLC2)
                await OpenDialog(() => new Raid9Window(sv, TeraRaidOrigin.BlueberryAcademy));
            else if (sender == B_RaidsSevenStar)
                await OpenDialog(() => new RaidSevenStar9Window(sv));
        }
        else if (SAV is SAV8SWSH swsh)
        {
            if (sender == B_Raids)
                await OpenDialog(() => new Raid8Window(swsh, MaxRaidOrigin.Galar));
            else if (sender == B_RaidsDLC1)
                await OpenDialog(() => new Raid8Window(swsh, MaxRaidOrigin.IsleOfArmor));
            else if (sender == B_RaidsDLC2)
                await OpenDialog(() => new Raid8Window(swsh, MaxRaidOrigin.CrownTundra));
        }
    }

    private async Task ClickBoxLayout()
    {
        await OpenDialog(() => new BoxLayoutWindow(SAV, Box.CurrentBox));
        Box.ResetBoxNames(); // fix box names
        Box.ResetSlots(); // refresh box background
        UpdateBoxViewers(all: true); // update subviewers
    }
}
