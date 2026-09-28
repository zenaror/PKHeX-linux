using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Controls.Hover;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4;

/// <summary>
/// Battle Pass editor for Battle Revolution (port of the WinForms <c>SAV_BattlePass</c>).
/// </summary>
/// <remarks>
/// Each pass holds a trainer profile, an appearance built from the gear lists, six party references, six
/// catchphrases and the battle records. Party slots are viewed, set and deleted with Ctrl, Shift and Alt
/// clicks, routed to the main entity editor exactly as the box slots are.
/// </remarks>
public sealed class BattlePass4BRWindow : SaveEditorWindow
{
    private const ushort TitleOffset1 = 11057;
    private const ushort TitleOffset2 = 18015;
    private const string NPC = "NPC";

    private readonly SAV4BR Origin;
    private readonly SAV4BR SAV;
    private readonly SAVEditorView Host;

    private BattlePass CurrentPass;
    private int CurrentPassIndex;
    private bool loading;

    private readonly string[] CharacterStyles = Translator.GetEnumTranslation<ModelBR>(MainWindow.CurrentLanguage);
    private readonly string[] Gear = GameLanguage.GetStrings("gear", MainWindow.CurrentLanguage);
    private readonly string[] Skin = Translator.GetEnumTranslation<SkinColorBR>(MainWindow.CurrentLanguage);
    private readonly string[] PictureTypes = Translator.GetEnumTranslation<PictureTypeBR>(MainWindow.CurrentLanguage);
    private readonly string[] PassDesigns = GameLanguage.GetStrings("pass_design", MainWindow.CurrentLanguage);
    private readonly string[] TrainerTitles1 = GameLanguage.GetStrings("trainer_title", MainWindow.CurrentLanguage);
    private readonly string[] TrainerTitles2 = GameLanguage.GetStrings("trainer_title_npc", MainWindow.CurrentLanguage);
    private readonly IReadOnlyList<ComboItem> Languages = GameInfo.LanguageDataSource(3, EntityContext.Gen3);
    private static readonly IReadOnlyList<ComboItem> EmptyCBList = [new(string.Empty, 0)];
    private string None => CharacterStyles[0];

    private readonly ListBox LB_Passes = new() { Name = "LB_Passes", Width = 250, Height = 520 };
    private readonly ObservableCollection<string> PassItems = [];
    private readonly PokeGrid Box = new();
    private readonly ContextMenu SlotMenu = new();
    private readonly SummaryPreviewer Preview = new();
    private int MenuIndex = -1;
    private int GroupSelected = -1;
    private int SlotSelected = -1;
    private static global::Avalonia.Media.Imaging.Bitmap? ViewOverlay => SlotUtil.GetTouchTypeBackground(SlotTouchType.Get);

    private readonly TextBox TB_Name = UiFactory.Text("TB_Name", 11, 160);
    private readonly ComboBox CB_TrainerTitle = UiFactory.Combo("CB_TrainerTitle", 250);
    private readonly TextBox MT_TID = UiFactory.Text("MT_TID", 5, 90);
    private readonly TextBox MT_SID = UiFactory.Text("MT_SID", 5, 90);
    private readonly ComboBox CB_Model = UiFactory.Combo("CB_Model", 180);
    private readonly ComboBox CB_SkinColor = UiFactory.Combo("CB_SkinColor", 180);
    private readonly ComboBox CB_PictureType = UiFactory.Combo("CB_PictureType", 180);
    private readonly ComboBox CB_PassDesign = UiFactory.Combo("CB_PassDesign", 180);
    private readonly ComboBox CB_Head = UiFactory.Combo("CB_Head", 200);
    private readonly ComboBox CB_Hair = UiFactory.Combo("CB_Hair", 200);
    private readonly ComboBox CB_Face = UiFactory.Combo("CB_Face", 200);
    private readonly ComboBox CB_Glasses = UiFactory.Combo("CB_Glasses", 200);
    private readonly ComboBox CB_Top = UiFactory.Combo("CB_Top", 200);
    private readonly ComboBox CB_Hands = UiFactory.Combo("CB_Hands", 200);
    private readonly ComboBox CB_Bottom = UiFactory.Combo("CB_Bottom", 200);
    private readonly ComboBox CB_Shoes = UiFactory.Combo("CB_Shoes", 200);
    private readonly ComboBox CB_Badge = UiFactory.Combo("CB_Badge", 200);
    private readonly ComboBox CB_Bag = UiFactory.Combo("CB_Bag", 200);
    private readonly CheckBox CHK_Available = UiFactory.Check("CHK_Available", "Pass Available");
    private readonly CheckBox CHK_Issued = UiFactory.Check("CHK_Issued", "Pass Issued");
    private readonly CheckBox CHK_Rental = UiFactory.Check("CHK_Rental", "Rental Pass");
    private readonly CheckBox CHK_Friend = UiFactory.Check("CHK_Friend", "Friend Pass");

    private readonly NumericUpDown[] PartyBox = new NumericUpDown[BattlePass.Count];
    private readonly NumericUpDown[] PartySlot = new NumericUpDown[BattlePass.Count];
    private readonly NumericUpDown[] PartyFlags = new NumericUpDown[BattlePass.Count];

    private readonly CheckBox[] PresetChecks = new CheckBox[6];
    private readonly NumericUpDown[] PresetIndexes = new NumericUpDown[6];
    private readonly TextBox TB_Greeting = UiFactory.Text("TB_Greeting", 25, 340);
    private readonly TextBox TB_SentOut = Multi("TB_SentOut", 27);
    private readonly TextBox TB_Shift1 = UiFactory.Text("TB_Shift1", 25, 340);
    private readonly TextBox TB_Shift2 = UiFactory.Text("TB_Shift2", 25, 340);
    private readonly TextBox TB_Win = Multi("TB_Win", 51);
    private readonly TextBox TB_Lose = Multi("TB_Lose", 51);

    private readonly TextBox TB_CreatorName = UiFactory.Text("TB_CreatorName", 11, 160);
    private readonly TextBox TB_BirthMonth = UiFactory.Text("TB_BirthMonth", 4, 80);
    private readonly TextBox TB_BirthDay = UiFactory.Text("TB_BirthDay", 4, 80);
    private readonly ComboBox CB_Country = UiFactory.Combo("CB_Country", 200);
    private readonly ComboBox CB_Region = UiFactory.Combo("CB_Region", 200);
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 170);
    private readonly TextBox TB_SelfIntroduction = Multi("TB_SelfIntroduction", 53);
    private readonly TextBox TB_RegionCode = UiFactory.Text("TB_RegionCode", 4, 140);
    private readonly TextBox MT_PlayerID = UiFactory.Text("MT_PlayerID", 16, 180);

    private readonly NumericUpDown NUD_Battles = Record("NUD_Battles");
    private readonly NumericUpDown[] Records = new NumericUpDown[13];
    private static readonly string[] RecordLabels =
    [
        "Colosseum Battles", "Free Battles", "Wi-Fi Battles",
        "Gateway Colosseum", "Main Street Colosseum", "Waterfall Colosseum", "Neon Colosseum",
        "Crystal Colosseum", "Sunny Park Colosseum", "Magma Colosseum", "Courtyard Colosseum",
        "Sunset Colosseum", "Stargazer Colosseum",
    ];

    private readonly Button B_FDelete = UiFactory.Button("B_FDelete", "X");
    private static TextBox Multi(string name, int maxLength) => new() { Name = name, AcceptsReturn = true, MaxLength = maxLength, Width = 340, Height = 70, TextWrapping = global::Avalonia.Media.TextWrapping.NoWrap };
    private static NumericUpDown Record(string name) => UiFactory.NumericUpDown(name, 0, int.MaxValue, 140);

    public BattlePass4BRWindow(SAVEditorView parent, SAV4BR sav, int index = 0) : base("SAV_BattlePass", "Battle Pass Editor")
    {
        Host = parent;
        SAV = (SAV4BR)(Origin = sav).Clone();

        for (int i = 0; i < BattlePass.Count; i++)
        {
            PartyBox[i] = UiFactory.NumericUpDown($"NUD_PKM{i + 1}Box", 0, byte.MaxValue, 90);
            PartySlot[i] = UiFactory.NumericUpDown($"NUD_PKM{i + 1}Slot", 0, byte.MaxValue, 90);
            PartyFlags[i] = UiFactory.NumericUpDown($"NUD_PKM{i + 1}Flags", 0, 0x7FFF, 110);
        }
        string[] presetNames = ["Greeting", "SentOut", "Shift1", "Shift2", "Win", "Lose"];
        for (int i = 0; i < PresetChecks.Length; i++)
        {
            PresetChecks[i] = UiFactory.Check($"CHK_Preset{presetNames[i]}", "Preset");
            PresetIndexes[i] = UiFactory.NumericUpDown($"NUD_Preset{presetNames[i]}Index", 0, ushort.MaxValue, 110);
        }
        for (int i = 0; i < Records.Length; i++)
            Records[i] = Record($"NUD_Record{i}");

        BuildLayout();
        SetupComboBoxes();
        LoadBattlePassList();

        loading = true;
        CurrentPassIndex = index;
        LB_Passes.SelectedIndex = index;
        CurrentPass = GetBattlePassReference(index);
        LoadCurrent(CurrentPass);
        loading = false;
    }

    #region Layout

    private void BuildLayout()
    {
        Box.InitializeGrid(3, 2, SpriteUtil.Spriter);
        BuildSlotMenu();
        foreach (var slot in Box.Entries)
        {
            slot.PointerPressed += (s, e) => OmniClick((SlotView)s!, e);
            slot.PointerEntered += (s, _) => HoverSlot((SlotView)s!);
            slot.PointerExited += (_, _) => Preview.Clear();
            slot.Cursor = new Cursor(StandardCursorType.Hand);
        }
        Closed += (_, _) => Preview.Clear();

        var tabs = new TabControl { Name = "Tab_Base" };
        tabs.Items.Add(new TabItem { Name = "f_MAIN", Header = "Main", Content = new ScrollViewer { Content = BuildMain(), MaxHeight = 560 } });
        tabs.Items.Add(new TabItem { Name = "f_PKM", Header = "Pokémon", Content = new ScrollViewer { Content = BuildParty(), MaxHeight = 560 } });
        tabs.Items.Add(new TabItem { Name = "f_CATCHPHRASES", Header = "Catchphrases", Content = new ScrollViewer { Content = BuildPhrases(), MaxHeight = 560 } });
        tabs.Items.Add(new TabItem { Name = "f_CREATOR", Header = "Creator", Content = new ScrollViewer { Content = BuildCreator(), MaxHeight = 560 } });

        var up = UiFactory.Button("B_Up", "^");
        var down = UiFactory.Button("B_Down", "v");
        var import = UiFactory.Button("B_Import", "Import");
        var export = UiFactory.Button("B_Export", "Export");
        var unlockCustom = UiFactory.Button("B_UnlockCustom", "Unlock All Custom Passes");
        var unlockRental = UiFactory.Button("B_UnlockRental", "Unlock All Rental Passes");

        up.Click += (_, _) => SwapSlots(false);
        down.Click += (_, _) => SwapSlots(true);
        B_FDelete.Click += (_, _) => ClickDeletePass();
        import.Click += async (_, _) => await ClickImport();
        export.Click += async (_, _) => await ClickExport();
        unlockCustom.Click += (_, _) => { SaveCurrent(CurrentPass); SAV.BattlePasses.UnlockAllCustomPasses(); LoadCurrent(CurrentPass); };
        unlockRental.Click += (_, _) => { SaveCurrent(CurrentPass); SAV.BattlePasses.UnlockAllRentalPasses(); LoadCurrent(CurrentPass); };

        LB_Passes.ItemsSource = PassItems;
        LB_Passes.SelectionChanged += (_, _) => ChangeIndexPass();

        var left = UiFactory.Column(
            UiFactory.Label("L_BattlePasses", "Battle Passes:"),
            LB_Passes,
            UiFactory.Row(up, down, B_FDelete),
            UiFactory.Row(import, export),
            unlockCustom,
            unlockRental);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(left);
        body.Children.Add(tabs);
        SetBody(body);
    }

    private Control BuildMain()
    {
        var trainer = UiFactory.FormGrid(4);
        int r = 0;
        Row(trainer, r++, "L_Name", "Name:", TB_Name);
        Row(trainer, r++, "L_TrainerTitle", "Trainer Title:", CB_TrainerTitle);
        Row(trainer, r++, "L_TID", "TID:", MT_TID);
        Row(trainer, r, "L_SID", "SID:", MT_SID);

        var gear = UiFactory.FormGrid(12);
        Row(gear, 0, "L_Model", "Character:", CB_Model);
        Row(gear, 1, "L_SkinColor", "Skin Color:", CB_SkinColor);
        string[] gearNames = ["Head", "Hair", "Face", "Glasses", "Top", "Hands", "Bottom", "Shoes", "Badge", "Bag"];
        ComboBox[] gearBoxes = [CB_Head, CB_Hair, CB_Face, CB_Glasses, CB_Top, CB_Hands, CB_Bottom, CB_Shoes, CB_Badge, CB_Bag];
        for (int i = 0; i < gearBoxes.Length; i++)
            Row(gear, i + 2, $"L_{gearNames[i]}", $"{gearNames[i]}:", gearBoxes[i]);

        var pass = UiFactory.FormGrid(2);
        Row(pass, 0, "L_PictureType", "Picture Type:", CB_PictureType);
        Row(pass, 1, "L_PassDesign", "Pass Design:", CB_PassDesign);

        var flags = UiFactory.Row(CHK_Available, CHK_Issued, CHK_Rental, CHK_Friend);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(UiFactory.Column(new GroupBoxView("GB_Trainer", "Trainer", trainer), pass, flags));
        row.Children.Add(new GroupBoxView("GB_Appearance", "Appearance", gear));
        return row;
    }

    private Control BuildParty()
    {
        // WinForms shows the six sprites above six "Pokémon N" boxes laid out three per row, each with its
        // Box / Slot / Flags fields.
        var slots = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)],
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)],
            ColumnSpacing = 8,
            RowSpacing = 8,
        };
        for (int i = 0; i < BattlePass.Count; i++)
        {
            var grid = UiFactory.FormGrid(3);
            Row(grid, 0, $"L_PKM{i + 1}Box", "Box:", PartyBox[i]);
            Row(grid, 1, $"L_PKM{i + 1}Slot", "Slot:", PartySlot[i]);
            Row(grid, 2, $"L_PKM{i + 1}Flags", "Flags:", PartyFlags[i]);
            var box = new GroupBoxView($"GB_PKM{i + 1}", $"Pokémon {i + 1}", grid);
            UiFactory.SetRowCol(box, i / 3, i % 3);
            slots.Children.Add(box);
        }
        return UiFactory.Column(Box, slots);
    }

    private Control BuildPhrases()
    {
        TextBox[] boxes = [TB_Greeting, TB_SentOut, TB_Shift1, TB_Shift2, TB_Win, TB_Lose];
        string[] names = ["L_Greeting", "L_SentOut", "L_Shift1", "L_Shift2", "L_Win", "L_Lose"];
        string[] labels = ["Greeting:", "Pokémon Sent Out:", "Pokémon Shift 1:", "Pokémon Shift 2:", "Win:", "Lose:"];
        var grid = UiFactory.FormGrid(boxes.Length);
        for (int i = 0; i < boxes.Length; i++)
            UiFactory.AddFormRow(grid, i, UiFactory.Label(names[i], labels[i]), UiFactory.Row(boxes[i], PresetChecks[i], PresetIndexes[i]));
        return grid;
    }

    private Control BuildCreator()
    {
        var grid = UiFactory.FormGrid(9);
        int r = 0;
        Row(grid, r++, "L_CreatorName", "Created By:", TB_CreatorName);
        Row(grid, r++, "L_BirthMonth", "Birth Month:", TB_BirthMonth);
        Row(grid, r++, "L_BirthDay", "Birth Day:", TB_BirthDay);
        Row(grid, r++, "L_Country", "Country:", CB_Country);
        Row(grid, r++, "L_Region", "Sub Region:", CB_Region);
        Row(grid, r++, "L_Language", "Language:", CB_Language);
        Row(grid, r++, "L_SelfIntroduction", "Self-Introduction:", TB_SelfIntroduction);
        Row(grid, r++, "L_RegionCode", "Region Code:", TB_RegionCode);
        Row(grid, r, "L_PlayerID", "Player ID:", MT_PlayerID);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(new GroupBoxView("GB_Creator", "Creator", grid));
        row.Children.Add(BuildRecords());
        return row;
    }

    private Control BuildRecords()
    {
        // Label names follow the WinForms record labels, so the translations apply.
        string[] names =
        [
            "L_RecordColosseumBattles", "L_RecordFreeBattles", "L_RecordWiFiBattles",
            "L_RecordGatewayColosseumClears", "L_RecordMainStreetColosseumClears", "L_RecordWaterfallColosseumClears",
            "L_RecordNeonColosseumClears", "L_RecordCrystalColosseumClears", "L_RecordSunnyParkColosseumClears",
            "L_RecordMagmaColosseumClears", "L_RecordCourtyardColosseumClears", "L_RecordSunsetColosseumClears",
            "L_RecordStargazerColosseumClears",
        ];
        var grid = UiFactory.FormGrid(Records.Length + 1);
        Row(grid, 0, "L_Battles", "Battles:", NUD_Battles);
        for (int i = 0; i < Records.Length; i++)
            Row(grid, i + 1, names[i], RecordLabels[i] + ':', Records[i]);
        return new GroupBoxView("GB_Records", "Records", grid);
    }

    private static void Row(Grid g, int row, string name, string text, Control editor)
        => UiFactory.AddFormRow(g, row, UiFactory.Label(name, text), editor);

    #endregion

    #region Combo boxes

    private void SetupComboBoxes()
    {
        CB_TrainerTitle.SetItems(GetTrainerTitles());
        CB_Model.SetItems(Util.GetCBList(CharacterStyles));
        CB_SkinColor.SetItems(Util.GetCBList(Skin));
        CB_PictureType.SetItems(Util.GetCBList(PictureTypes));
        CB_PassDesign.SetItems(Util.GetCBList(PassDesigns));
        CB_Country.SetCountrySubRegion("gen4_countries");
        CB_Language.SetItems(Languages);

        CB_Model.SelectionChanged += (_, _) => ChangeModel();
        CB_Country.SelectionChanged += (_, _) => UpdateCountry();
        CB_Language.SelectionChanged += (_, _) => UpdateLanguage();
        MT_PlayerID.LostFocus += (_, _) => MT_PlayerID.Text = Util.GetHexValue64(MT_PlayerID.Text ?? string.Empty).ToString("X16");
        foreach (var tb in new[] { TB_Greeting, TB_SentOut, TB_Shift1, TB_Shift2, TB_Win, TB_Lose, TB_SelfIntroduction })
            tb.LostFocus += (s, _) => ValidateCatchphrase((TextBox)s!);
    }

    /// <summary>Builds the title list, disambiguating duplicates by character style and then by number.</summary>
    private List<ComboItem> GetTrainerTitles()
    {
        var titles1 = (string[])TrainerTitles1.Clone();
        var titles2 = (string[])TrainerTitles2.Clone();

        var counts = new Dictionary<string, int>();
        foreach (var s in titles1)
            counts[s] = counts.GetValueOrDefault(s) + 1;
        foreach (var s in titles2)
            counts[s] = counts.GetValueOrDefault(s) + 1;

        for (var i = 0; i < titles1.Length; i++)
        {
            if (counts[titles1[i]] > 1)
                titles1[i] += $" ({CharacterStyles[(i / (titles1.Length / 6)) + 1]})";
        }
        for (var i = 0; i < titles2.Length; i++)
        {
            if (counts[titles2[i]] > 1)
                titles2[i] += $" ({NPC})";
        }

        counts.Clear();
        foreach (var s in titles1)
            counts[s] = counts.GetValueOrDefault(s) + 1;
        foreach (var s in titles2)
            counts[s] = counts.GetValueOrDefault(s) + 1;

        var maxCounts = new Dictionary<string, int>(counts);
        for (var i = titles2.Length - 1; i >= 0; i--)
        {
            var s = titles2[i];
            var count = counts[s]--;
            if (maxCounts[s] != 1)
                titles2[i] += $" ({count})";
        }
        for (var i = titles1.Length - 1; i >= 0; i--)
        {
            var s = titles1[i];
            var count = counts[s]--;
            if (maxCounts[s] != 1)
                titles1[i] += $" ({count})";
        }

        List<ComboItem> cbList = [new(None, 0)];
        Util.AddCBWithOffset(cbList, titles1, TitleOffset1);
        Util.AddCBWithOffset(cbList, titles2, TitleOffset2);
        return cbList;
    }

    private void SetupComboBoxesAppearance(ModelBR model)
    {
        SetupGearCategory(CB_Head, model, GearCategory.Head);
        SetupGearCategory(CB_Hair, model, GearCategory.Hair);
        SetupGearCategory(CB_Face, model, GearCategory.Face);
        SetupGearCategory(CB_Glasses, model, GearCategory.Glasses);
        SetupGearCategory(CB_Top, model, GearCategory.Top);
        SetupGearCategory(CB_Hands, model, GearCategory.Hands);
        SetupGearCategory(CB_Bottom, model, GearCategory.Bottom);
        SetupGearCategory(CB_Shoes, model, GearCategory.Shoes);
        SetupGearCategory(CB_Badge, model, GearCategory.Badges);
        SetupGearCategory(CB_Bag, model, GearCategory.Bags);
    }

    private void SetupGearCategory(ComboBox cb, ModelBR model, GearCategory category)
    {
        if (model is >= ModelBR.YoungBoy and <= ModelBR.LittleGirl)
        {
            if (category == GearCategory.Badges)
                model = ModelBR.YoungBoy;
            var (offset, count) = GearUnlock.GetOffsetCount(model, category);
            cb.SetItems(Util.GetCBList(Gear.AsSpan(offset, count)));
            cb.SetValue(GearUnlock.GetDefault(model, category));
            cb.IsEnabled = true;
        }
        else
        {
            cb.SetItems(EmptyCBList);
            cb.SetValue(0);
            cb.IsEnabled = false;
        }
    }

    private void ChangeModel()
    {
        if (CB_Model.GetSelectedItem() is not { } item)
            return;
        SetupComboBoxesAppearance((ModelBR)item.Value);
        if (loading)
            return;
        SaveCurrent(CurrentPass);
        CurrentPass.ResetPresetIndexes();
        LoadCurrent(CurrentPass);
    }

    private void UpdateCountry()
    {
        if (CB_Country.GetSelectedItem() is not { } item)
            return;
        CB_Region.SetCountrySubRegion($"gen4_sr_{item.Value:000}");
        if (CB_Region.ItemCount == 0)
            CB_Region.SetCountrySubRegion("gen4_sr_default");
    }

    /// <summary>Japanese fits two more characters in the self-introduction (port of <c>CB_Language_SelectedIndexChanged</c>).</summary>
    private void UpdateLanguage()
    {
        if (CB_Language.GetSelectedItem() is not { } item)
            return;
        TB_SelfIntroduction.MaxLength = item.Value != (int)LanguageID.Japanese ? 51 : 53;
        ValidateCatchphrase(TB_SelfIntroduction);
    }

    /// <summary>
    /// Truncates a phrase to the in-game character budget (port of <c>ValidateCatchphrase</c>).
    /// </summary>
    /// <remarks>Line breaks and the special glyphs cost two in-game characters each.</remarks>
    private static void ValidateCatchphrase(TextBox tb)
    {
        var text = tb.Text ?? string.Empty;
        int max = tb.MaxLength;
        int length = 0;
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r')
                continue; // half of a CRLF pair; the '\n' accounts for the break
            if (c == '\n')
            {
                length += 2;
                continue;
            }
            length += c switch
            {
                StringConverter4GC.LineBreak or
                    StringConverter4GC.Proportional or
                    StringConverter4GC.PokemonName => 2,
                _ => 1,
            };
            if (length > max)
            {
                tb.Text = text[..i];
                return;
            }
        }
    }

    #endregion

    #region Party slots

    private void OmniClick(SlotView view, PointerPressedEventArgs e)
    {
        var index = Box.Entries.IndexOf(view);
        if (index < 0)
            return;
        if (e.GetCurrentPoint(view).Properties.IsRightButtonPressed)
        {
            // WinForms attaches the View/Set/Delete menu to every slot picture box.
            MenuIndex = index;
            SlotMenu.Open(view);
            return;
        }
        switch (e.KeyModifiers)
        {
            case KeyModifiers.Control: ClickView(index); break;
            case KeyModifiers.Shift: _ = ClickSet(index); break;
            case KeyModifiers.Alt: ClickDelete(index); break;
        }
    }

    /// <summary>Builds the slot context menu (port of the WinForms <c>mnu</c> strip).</summary>
    private void BuildSlotMenu()
    {
        var view = new MenuItem { Name = "mnuView", Header = "View" };
        var set = new MenuItem { Name = "mnuSet", Header = "Set" };
        var delete = new MenuItem { Name = "mnuDelete", Header = "Delete" };
        view.Click += (_, _) => { if (MenuIndex >= 0) ClickView(MenuIndex); };
        set.Click += async (_, _) => { if (MenuIndex >= 0) await ClickSet(MenuIndex); };
        delete.Click += (_, _) => { if (MenuIndex >= 0) ClickDelete(MenuIndex); };
        SlotMenu.Items.Add(view);
        SlotMenu.Items.Add(set);
        SlotMenu.Items.Add(delete);
        // A ContextMenu is outside its target's logical tree, so it is translated on its own.
        Translator.TranslateControls(SlotMenu, "SAV_BattlePass", MainWindow.CurrentLanguage);
    }

    private void HoverSlot(SlotView view)
    {
        var index = Box.Entries.IndexOf(view);
        if (index < 0)
            return;
        Preview.Show(view, CurrentPass.GetPartySlotAtIndex(index));
    }

    private void ClickView(int index)
    {
        Host.EditEnv.PKMEditor.PopulateFields(CurrentPass.GetPartySlotAtIndex(index), false);

        // WinForms marks the slot that was loaded into the editor with the "View" overlay.
        if (SlotSelected != index && (uint)SlotSelected < Box.Entries.Count)
            Box.Entries[SlotSelected].BackgroundBitmap = null;
        GroupSelected = CurrentPassIndex;
        SlotSelected = index;
        Box.Entries[index].BackgroundBitmap = ViewOverlay;
    }

    private async Task ClickSet(int index)
    {
        var editor = Host.EditEnv.PKMEditor;
        if (!editor.EditsComplete)
            return;
        var pk = editor.PreparePKM();
        if (pk.Species == 0)
            return;

        var errata = SAV.EvaluateCompatibility(pk);
        if (errata.Count != 0)
        {
            var msg = string.Join(Environment.NewLine, errata);
            if (await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, msg, MessageStrings.MsgContinue) != DialogResult.Yes)
                return;
        }

        SaveCurrent(CurrentPass);
        CurrentPass.SetPartySlotAtIndex(pk, index);
        switch (SAV.BattlePasses.GetPassType(CurrentPassIndex))
        {
            case BattlePassType.Custom:
                var (box, slot) = Origin.FindSlot(pk);
                CurrentPass.SetPartySlotBoxSlot(index, box, slot);
                break;
            case BattlePassType.Rental:
                CurrentPass.SetPartySlotBoxSlot(index, 255, 0);
                break;
        }
        LoadCurrent(CurrentPass);
    }

    private void ClickDelete(int index)
    {
        if (CurrentPass.GetPartySlotAtIndex(index).Species == 0)
            return;
        SaveCurrent(CurrentPass);
        CurrentPass.DeletePartySlot(index);
        LoadCurrent(CurrentPass);
    }

    #endregion

    #region Pass list

    private BattlePass GetBattlePassReference(int index) => SAV.BattlePasses[index];

    private string GetPassLabel(int i)
    {
        var type = Translator.TranslateEnum(SAV.BattlePasses.GetPassType(i), MainWindow.CurrentLanguage);
        var pass = GetBattlePassReference(i);
        var name = pass.Name;
        if (pass is { Rental: false, Issued: false } || string.IsNullOrWhiteSpace(name))
            name = None;
        return $"{i + 1:00} {type}/{name}";
    }

    private void LoadBattlePassList()
    {
        loading = true;
        PassItems.Clear();
        for (int i = 0; i < BattlePassAccessor.PASS_COUNT; i++)
            PassItems.Add(GetPassLabel(i));
        loading = false;
    }

    private void ReloadBattlePassList()
    {
        loading = true;
        var selected = LB_Passes.SelectedIndex;
        for (int i = 0; i < BattlePassAccessor.PASS_COUNT && i < PassItems.Count; i++)
            PassItems[i] = GetPassLabel(i);
        LB_Passes.SelectedIndex = selected; // replacing the item drops the selection
        loading = false;
    }

    private void ChangeIndexPass()
    {
        int index = LB_Passes.SelectedIndex;
        if (index < 0 || loading)
            return;

        SaveCurrent(CurrentPass);
        B_FDelete.IsEnabled = SAV.BattlePasses.GetPassType(index) != BattlePassType.Rental;
        CurrentPassIndex = index;
        LoadCurrent(CurrentPass = GetBattlePassReference(index));
    }

    #endregion

    #region Load / save a pass

    private void LoadCurrent(BattlePass p)
    {
        loading = true;

        TB_Name.Text = p.Name;
        CB_TrainerTitle.SetValue(p.TrainerTitle);
        MT_TID.Text = p.TID.ToString("00000");
        MT_SID.Text = p.SID.ToString("00000");

        CB_Model.SetValue(p.Model);
        SetupComboBoxesAppearance((ModelBR)p.Model);
        CB_SkinColor.SetValue(p.Skin);
        CB_Head.SetValue(p.Head);
        CB_Hair.SetValue(p.Hair);
        CB_Face.SetValue(p.Face);
        CB_Glasses.SetValue(p.Glasses);
        CB_Top.SetValue(p.Top);
        CB_Hands.SetValue(p.Hands);
        CB_Bottom.SetValue(p.Bottom);
        CB_Shoes.SetValue(p.Shoes);
        CB_Badge.SetValue(p.Badge);
        CB_Bag.SetValue(p.Bag);
        CB_PictureType.SetValue(p.PictureType);
        CB_PassDesign.SetValue(p.PassDesign);

        CHK_Available.IsChecked = p.Available;
        CHK_Issued.IsChecked = p.Issued;
        CHK_Rental.IsChecked = p.Rental;
        CHK_Friend.IsChecked = p.Friend;

        for (int i = 0; i < BattlePass.Count; i++)
        {
            var (box, slot) = p.GetPartySlotBoxSlot(i);
            PartyBox[i].SetValueClamped(box);
            PartySlot[i].SetValueClamped(slot);
            PartyFlags[i].SetValueClamped(p.GetPartySlotFlags(i));
        }

        ReadOnlySpan<bool> presets = [p.PresetGreeting, p.PresetSentOut, p.PresetShift1, p.PresetShift2, p.PresetWin, p.PresetLose];
        ReadOnlySpan<ushort> presetIdx = [p.PresetGreetingIndex, p.PresetSentOutIndex, p.PresetShift1Index, p.PresetShift2Index, p.PresetWinIndex, p.PresetLoseIndex];
        for (int i = 0; i < PresetChecks.Length; i++)
        {
            PresetChecks[i].IsChecked = presets[i];
            PresetIndexes[i].SetValueClamped(presetIdx[i]);
        }
        TB_Greeting.Text = p.Greeting;
        TB_SentOut.Text = JoinLines(p.SentOut);
        TB_Shift1.Text = p.Shift1;
        TB_Shift2.Text = p.Shift2;
        TB_Win.Text = JoinLines(p.Win);
        TB_Lose.Text = JoinLines(p.Lose);

        TB_CreatorName.Text = p.CreatorName;
        TB_BirthMonth.Text = p.BirthMonth;
        TB_BirthDay.Text = p.BirthDay;
        CB_Country.SetValue(p.Country);
        UpdateCountry();
        CB_Region.SetValue(p.Region);
        CB_Language.SetValue((int)p.Language.ToLanguageID());
        TB_SelfIntroduction.Text = JoinLines(p.SelfIntroduction.TrimStart(StringConverter4GC.Proportional));
        TB_RegionCode.Text = p.RegionCode;
        MT_PlayerID.Text = p.PlayerID.ToString("X16");

        NUD_Battles.SetValueClamped(p.Battles);
        ReadOnlySpan<int> records =
        [
            p.RecordColosseumBattles, p.RecordFreeBattles, p.RecordWiFiBattles,
            p.RecordGatewayColosseumClears, p.RecordMainStreetColosseumClears, p.RecordWaterfallColosseumClears,
            p.RecordNeonColosseumClears, p.RecordCrystalColosseumClears, p.RecordSunnyParkColosseumClears,
            p.RecordMagmaColosseumClears, p.RecordCourtyardColosseumClears, p.RecordSunsetColosseumClears,
            p.RecordStargazerColosseumClears,
        ];
        for (int i = 0; i < Records.Length; i++)
            Records[i].SetValueClamped(records[i]);

        for (int i = 0; i < BattlePass.Count; i++)
        {
            var view = Box.Entries[i];
            if (!p.GetPartySlotPresent(i))
            {
                view.Sprite = SpriteUtil.Spriter.None.ToAvaloniaBitmap();
                continue;
            }
            var pk = p.GetPartySlotAtIndex(i);
            view.Sprite = pk.Sprite(SAV, visibility: SlotVisibilityType.CheckLegalityIndicate).ToAvaloniaBitmapAndDispose();
        }

        if (SlotSelected != -1 && (uint)SlotSelected < Box.Entries.Count)
            Box.Entries[SlotSelected].BackgroundBitmap = GroupSelected != CurrentPassIndex ? null : ViewOverlay;

        loading = false;
    }

    private static string JoinLines(string value) => value.Replace(StringConverter4GC.LineBreak, '\n');
    private static string SplitLines(string? value) => (value ?? string.Empty).Replace("\r\n", "\n").Replace('\n', StringConverter4GC.LineBreak);

    private void SaveCurrent(BattlePass p)
    {
        p.Name = TB_Name.Text ?? string.Empty;
        p.TrainerTitle = (short)(CB_TrainerTitle.GetSelectedItem()?.Value ?? 0);
        p.TID = (ushort)Util.ToUInt32(MT_TID.Text);
        p.SID = (ushort)Util.ToUInt32(MT_SID.Text);

        p.Model = CB_Model.GetSelectedItem()?.Value ?? 0;
        p.Skin = CB_SkinColor.GetSelectedItem()?.Value ?? 0;
        p.Head = CB_Head.GetSelectedItem()?.Value ?? 0;
        p.Hair = CB_Hair.GetSelectedItem()?.Value ?? 0;
        p.Face = CB_Face.GetSelectedItem()?.Value ?? 0;
        p.Glasses = CB_Glasses.GetSelectedItem()?.Value ?? 0;
        p.Top = CB_Top.GetSelectedItem()?.Value ?? 0;
        p.Hands = CB_Hands.GetSelectedItem()?.Value ?? 0;
        p.Bottom = CB_Bottom.GetSelectedItem()?.Value ?? 0;
        p.Shoes = CB_Shoes.GetSelectedItem()?.Value ?? 0;
        p.Badge = CB_Badge.GetSelectedItem()?.Value ?? 0;
        p.Bag = CB_Bag.GetSelectedItem()?.Value ?? 0;
        p.PictureType = CB_PictureType.GetSelectedItem()?.Value ?? 0;
        p.PassDesign = CB_PassDesign.GetSelectedItem()?.Value ?? 0;

        p.Available = CHK_Available.IsChecked == true;
        p.Issued = CHK_Issued.IsChecked == true;
        p.Rental = CHK_Rental.IsChecked == true;
        p.Friend = CHK_Friend.IsChecked == true;

        for (int i = 0; i < BattlePass.Count; i++)
        {
            p.SetPartySlotBoxSlot(i, (byte)(PartyBox[i].Value ?? 0), (byte)(PartySlot[i].Value ?? 0));
            p.SetPartySlotFlags(i, (ushort)(PartyFlags[i].Value ?? 0));
        }

        p.PresetGreeting = PresetChecks[0].IsChecked == true;
        p.PresetSentOut = PresetChecks[1].IsChecked == true;
        p.PresetShift1 = PresetChecks[2].IsChecked == true;
        p.PresetShift2 = PresetChecks[3].IsChecked == true;
        p.PresetWin = PresetChecks[4].IsChecked == true;
        p.PresetLose = PresetChecks[5].IsChecked == true;
        p.Greeting = TB_Greeting.Text ?? string.Empty;
        p.SentOut = SplitLines(TB_SentOut.Text);
        p.Shift1 = TB_Shift1.Text ?? string.Empty;
        p.Shift2 = TB_Shift2.Text ?? string.Empty;
        p.Win = SplitLines(TB_Win.Text);
        p.Lose = SplitLines(TB_Lose.Text);
        p.PresetGreetingIndex = (ushort)(PresetIndexes[0].Value ?? 0);
        p.PresetSentOutIndex = (ushort)(PresetIndexes[1].Value ?? 0);
        p.PresetShift1Index = (ushort)(PresetIndexes[2].Value ?? 0);
        p.PresetShift2Index = (ushort)(PresetIndexes[3].Value ?? 0);
        p.PresetWinIndex = (ushort)(PresetIndexes[4].Value ?? 0);
        p.PresetLoseIndex = (ushort)(PresetIndexes[5].Value ?? 0);

        p.CreatorName = TB_CreatorName.Text ?? string.Empty;
        p.BirthMonth = TB_BirthMonth.Text ?? string.Empty;
        p.BirthDay = TB_BirthDay.Text ?? string.Empty;
        p.Country = CB_Country.GetSelectedItem()?.Value ?? 0;
        p.Region = CB_Region.GetSelectedItem()?.Value ?? 0;
        p.Language = ((LanguageID)(CB_Language.GetSelectedItem()?.Value ?? 0)).ToBattlePassLanguage();
        var prefix = p.Language == BattlePassLanguage.Japanese ? string.Empty : StringConverter4GC.Proportional.ToString();
        p.SelfIntroduction = prefix + SplitLines(TB_SelfIntroduction.Text);
        p.RegionCode = TB_RegionCode.Text ?? string.Empty;
        p.PlayerID = Util.GetHexValue64(MT_PlayerID.Text);

        p.Battles = (int)(NUD_Battles.Value ?? 0);
        p.RecordColosseumBattles = Rec(0);
        p.RecordFreeBattles = Rec(1);
        p.RecordWiFiBattles = Rec(2);
        p.RecordGatewayColosseumClears = Rec(3);
        p.RecordMainStreetColosseumClears = Rec(4);
        p.RecordWaterfallColosseumClears = Rec(5);
        p.RecordNeonColosseumClears = Rec(6);
        p.RecordCrystalColosseumClears = Rec(7);
        p.RecordSunnyParkColosseumClears = Rec(8);
        p.RecordMagmaColosseumClears = Rec(9);
        p.RecordCourtyardColosseumClears = Rec(10);
        p.RecordSunsetColosseumClears = Rec(11);
        p.RecordStargazerColosseumClears = Rec(12);

        ReloadBattlePassList();
        return;

        int Rec(int i) => (int)(Records[i].Value ?? 0);
    }

    #endregion

    #region Meta buttons

    private void SwapSlots(bool down)
    {
        var index = LB_Passes.SelectedIndex;
        if (index < 0)
            return;
        var other = index + (down ? 1 : -1);
        if ((uint)other >= PassItems.Count)
            return;

        SaveCurrent(CurrentPass);
        SAV.BattlePasses.Swap(index, other);
        ReloadBattlePassList();

        loading = true;
        CurrentPassIndex = other;
        LB_Passes.SelectedIndex = other;
        CurrentPass = GetBattlePassReference(other);
        LoadCurrent(CurrentPass);
        B_FDelete.IsEnabled = SAV.BattlePasses.GetPassType(other) != BattlePassType.Rental;
        loading = false;
    }

    private void ClickDeletePass()
    {
        var index = CurrentPassIndex;
        SaveCurrent(CurrentPass);
        SAV.BattlePasses.Delete(index);
        ReloadBattlePassList();

        loading = true;
        CurrentPassIndex = index;
        CurrentPass = GetBattlePassReference(index);
        LoadCurrent(CurrentPass);
        loading = false;
    }

    private async Task ClickImport()
    {
        var path = await FileDialogs.OpenSingleFile(this, "Battle Pass Data|*.bin");
        if (path is null)
            return;
        if (new FileInfo(path).Length != BattlePass.Size)
            return;

        var data = await File.ReadAllBytesAsync(path);
        data.CopyTo(CurrentPass.Data);
        LoadCurrent(CurrentPass);
    }

    private async Task ClickExport()
    {
        SaveCurrent(CurrentPass);
        var name = CurrentPass.Name;
        if (string.IsNullOrWhiteSpace(name))
            name = "Trainer";
        var path = await FileDialogs.SaveFileDialog(this, "Battle Pass Data|*.bin", $"{PathUtil.CleanFileName(name)}.bin");
        if (path is null)
            return;
        await File.WriteAllBytesAsync(path, CurrentPass.Data.ToArray());
    }

    #endregion

    protected override void OnSave()
    {
        SaveCurrent(CurrentPass);
        // Other changes may have been made in the main window, so only the Battle Passes are copied back.
        Origin.BattlePasses.CopyChangesFrom(SAV.BattlePasses);
        Close();
    }
}
