using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Styling;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views.EntityEditors;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;
using Color = System.Drawing.Color;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen3;

/// <summary>
/// Miscellaneous editor for Generation 3 saves (port of the WinForms <c>SAV_Misc3</c>).
/// </summary>
/// <remarks>
/// Keeps the same tab set as the original and hides the tabs a given game does not have: the Hoenn-only
/// Pokéblock case, decorations and contest paintings, the Emerald-only ferry and Battle Frontier pages,
/// and the FR/LG-only rival name and trainer card icons.
/// </remarks>
public sealed class Misc3Window : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV3 SAV;

    private readonly TabControl TC_Misc = new() { Name = "TC_Misc" };
    private readonly List<TabItem> Tabs = [];

    #region Main tab

    private readonly NumericUpDown NUD_Coins = UiFactory.NumericUpDown("NUD_Coins", 0, 9999, 110);
    private readonly NumericUpDown NUD_BP = UiFactory.NumericUpDown("NUD_BP", 0, 9999, 110);
    private readonly NumericUpDown NUD_BPEarned = UiFactory.NumericUpDown("NUD_BPEarned", 0, ushort.MaxValue, 110);
    private readonly TextBlock L_Coins = UiFactory.Label("L_Coins", "Coins:");
    private readonly TextBlock L_BP = UiFactory.Label("L_BP", "Current BP:");
    private readonly TextBlock L_BPEarned = UiFactory.Label("L_BPEarned", "Earned BP:");
    private readonly TextBox TB_RivalName = UiFactory.Text("TB_RivalName", 7, 140);
    private readonly TextBlock L_TrainerName = UiFactory.Label("L_TrainerName", "Rival Name:");
    private readonly ComboBox[] CB_TCM;
    private GroupBoxView GB_TCM = null!;

    #endregion

    #region Joyful tab

    private readonly TextBox TB_J1 = UiFactory.Text("TB_J1", 5, 70);
    private readonly TextBox TB_J2 = UiFactory.Text("TB_J2", 5, 70);
    private readonly TextBox TB_J3 = UiFactory.Text("TB_J3", 5, 70);
    private readonly TextBox TB_J4 = UiFactory.Text("TB_J4", 5, 70);
    private readonly TextBox TB_B1 = UiFactory.Text("TB_B1", 5, 70);
    private readonly TextBox TB_B2 = UiFactory.Text("TB_B2", 5, 70);
    private readonly TextBox TB_B3 = UiFactory.Text("TB_B3", 5, 70);
    private readonly TextBox TB_BerryPowder = UiFactory.Text("TB_BerryPowder", 5, 70);

    #endregion

    #region Ferry tab

    private readonly CheckBox CHK_Catchable = UiFactory.Check("CHK_Catchable", "Can get ride");
    private readonly CheckBox CHK_ReachSouthern = UiFactory.Check("CHK_ReachSouthern", "Southern Island");
    private readonly CheckBox CHK_ReachBirth = UiFactory.Check("CHK_ReachBirth", "Birth Island");
    private readonly CheckBox CHK_ReachFaraway = UiFactory.Check("CHK_ReachFaraway", "Faraway Island");
    private readonly CheckBox CHK_ReachNavel = UiFactory.Check("CHK_ReachNavel", "Navel Rock");
    private readonly CheckBox CHK_ReachBF = UiFactory.Check("CHK_ReachBF", "Battle Frontier");
    private readonly CheckBox CHK_InitialSouthern = UiFactory.Check("CHK_InitialSouthern", "Southern Island");
    private readonly CheckBox CHK_InitialBirth = UiFactory.Check("CHK_InitialBirth", "Birth Island");
    private readonly CheckBox CHK_InitialFaraway = UiFactory.Check("CHK_InitialFaraway", "Faraway Island");
    private readonly CheckBox CHK_InitialNavel = UiFactory.Check("CHK_InitialNavel", "Navel Rock");
    private readonly Button B_GetTickets = UiFactory.Button("B_GetTickets", "Get Tickets");

    #endregion

    #region Battle Frontier tab

    private readonly CheckBox CHK_ActivatePass = UiFactory.Check("CHK_ActivatePass", "Activated");
    private readonly Button[] SymbolButtonA;
    private readonly Color[] SymbolColors = new Color[7];
    private readonly ComboBox CB_Stats1 = UiFactory.Combo("CB_Stats1", 160);
    private readonly ComboBox CB_Stats2 = UiFactory.StringCombo("CB_Stats2", 140);
    private readonly TextBlock L_Facility = UiFactory.Label("L_Facility", "Battle Facility:");
    private readonly TextBlock L_Mode = UiFactory.Label("L_Mode", "Battle Mode:");
    private readonly RadioButton RB_Stats3_01 = new() { Name = "RB_Stats3_01", Content = "Lv. 50", GroupName = "BFRecord" };
    private readonly RadioButton RB_Stats3_02 = new() { Name = "RB_Stats3_02", Content = "Open", GroupName = "BFRecord" };
    private readonly NumericUpDown[] StatNUDA;
    private readonly TextBlock[] StatLabelA;
    private readonly CheckBox CHK_Continue = UiFactory.Check("CHK_Continue", "Continue");

    private bool editingcont;
    private bool editingval;
    private bool loading;

    #endregion

    #region Records tab

    private readonly ComboBox CB_Record = UiFactory.Combo("CB_Record", 260);
    private readonly NumericUpDown NUD_RecordValue = UiFactory.NumericUpDown("NUD_RecordValue", 0, uint.MaxValue, 140);
    private readonly NumericUpDown NUD_FameH = UiFactory.NumericUpDown("NUD_FameH", 0, 9999, 100);
    private readonly NumericUpDown NUD_FameM = UiFactory.NumericUpDown("NUD_FameM", 0, 59, 90);
    private readonly NumericUpDown NUD_FameS = UiFactory.NumericUpDown("NUD_FameS", 0, 59, 90);

    #endregion

    #region Pokéblocks tab

    private PokeBlock3Case Case = null!;
    private readonly ListBox LB_Pokeblocks = new() { Name = "LB_Pokeblocks", Width = 220, Height = 320 };
    private readonly ObservableCollection<string> BlockItems = [];
    private readonly PropertyGridView PG_Pokeblocks = new() { Name = "PG_Pokeblocks", Width = 320, Height = 320 };
    private readonly Button B_PokeblockAll = UiFactory.Button("B_PokeblockAll", "Give All");
    private readonly Button B_PokeblockDel = UiFactory.Button("B_PokeblockDel", "Delete All");
    private readonly string[] BlockNames = Util.GetStringList("pokeblock3", MainWindow.CurrentLanguage);
    private int CurrentBlockIndex = -1;
    private bool UpdatingBlocks;

    #endregion

    #region Decorations tab

    private readonly DataGrid[] DecoGrids = new DataGrid[8];
    private readonly ObservableCollection<DecoRow>[] DecoRows = new ObservableCollection<DecoRow>[8];

    #endregion

    #region Paintings tab

    private readonly NumericUpDown NUD_Painting = UiFactory.NumericUpDown("NUD_Painting", 0, 4, 100);
    private readonly CheckBox CHK_EnablePaint = UiFactory.Check("CHK_EnablePaint", "Enabled");
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);
    private readonly NumericUpDown NUD_Caption = UiFactory.NumericUpDown("NUD_Caption", 0, 2, 100);
    private readonly TextBox TB_TID = UiFactory.Text("TB_TID", 5, 70);
    private readonly TextBox TB_SID = UiFactory.Text("TB_SID", 5, 70);
    private readonly TextBox TB_PID = UiFactory.Text("TB_PID", 8, 90);
    private readonly TextBox TB_Nickname = UiFactory.Text("TB_Nickname", 10, 140);
    private readonly TextBox TB_OT = UiFactory.Text("TB_OT", 7, 140);
    private readonly CheckBox CHK_Shiny = UiFactory.Check("CHK_Shiny", "Shiny");
    private GroupBoxView GB_Painting = null!;
    private int PaintingIndex = -1;
    private bool loadingPainting;

    #endregion

    private readonly Button B_ForceMirageIsland = UiFactory.Button("B_ForceMirageIsland", "Mirage Island Appear: Match First Party Member");

    public Misc3Window(SAV3 sav) : base("SAV_Misc3", "Misc Edits")
    {
        SAV = (SAV3)(Origin = sav).Clone();

        CB_TCM = [
            UiFactory.Combo("CB_TCM1", 160), UiFactory.Combo("CB_TCM2", 160), UiFactory.Combo("CB_TCM3", 160),
            UiFactory.Combo("CB_TCM4", 160), UiFactory.Combo("CB_TCM5", 160), UiFactory.Combo("CB_TCM6", 160),
        ];
        SymbolButtonA = [
            MakeSymbol("BTN_SymbolA", "A"), MakeSymbol("BTN_SymbolT", "T"), MakeSymbol("BTN_SymbolS", "S"),
            MakeSymbol("BTN_SymbolG", "G"), MakeSymbol("BTN_SymbolK", "K"), MakeSymbol("BTN_SymbolL", "L"),
            MakeSymbol("BTN_SymbolB", "B"),
        ];
        StatNUDA = [
            UiFactory.NumericUpDown("NUD_Stat0", 0, 9999, 90), UiFactory.NumericUpDown("NUD_Stat1", 0, 9999, 90),
            UiFactory.NumericUpDown("NUD_Stat2", 0, 9999, 90), UiFactory.NumericUpDown("NUD_Stat3", 0, 9999, 90),
        ];
        StatLabelA = [
            UiFactory.Label("L_Stat0", "Current"), UiFactory.Label("L_Stat1", "Trade"),
            UiFactory.Label("L_Stat2", "Record"), UiFactory.Label("L_Stat3", "Trade"),
        ];

        BuildLayout();
        LoadRecords();

        if (SAV.LargeBlock is ISaveBlock3LargeHoenn h)
        {
            InitializeBlocks(h);
            ReadDecorations(h);

            CB_Species.SetItems(GameInfo.FilteredSources.Species.ToList());
            LoadPaintings();
        }
        else
        {
            RemoveTab("Tab_Pokeblocks");
            RemoveTab("Tab_Decorations");
            RemoveTab("Tab_Paintings");
            B_ForceMirageIsland.IsVisible = false;
        }

        if (SAV.SmallBlock is ISaveBlock3SmallExpansion j)
            ReadJoyful(j);
        else
            RemoveTab("TAB_Joyful");

        if (SAV is SAV3E)
        {
            ReadFerry();
            ReadBattleFrontier();
        }
        else
        {
            RemoveTab("TAB_Ferry");
            RemoveTab("TAB_BF");
        }

        if (!B_ForceMirageIsland.IsVisible)
            RemoveTab("Tab_Other");

        if (SAV is SAV3FRLG frlg)
        {
            TB_RivalName.Text = frlg.RivalName;
            TB_RivalName.AttachClick(async _ => await TrashEditorWindow.ShowAsync(this, TB_RivalName, frlg, frlg.LargeBlock.RivalNameTrash.ToArray()));

            var legal = GameInfo.FilteredSources.Species.ToList();
            for (int i = 0; i < CB_TCM.Length; i++)
            {
                CB_TCM[i].SetItems(legal);
                var g3Species = SAV.GetWork(0x43 + i);
                CB_TCM[i].SetValue(SpeciesConverter.GetNational3(g3Species));
            }
        }
        else
        {
            TB_RivalName.IsVisible = L_TrainerName.IsVisible = GB_TCM.IsVisible = false;
        }

        NUD_Coins.SetValueClamped(SAV.Coin);
    }

    private static Button MakeSymbol(string name, string text) => new()
    {
        Name = name,
        Content = text,
        Width = 36,
        Height = 36,
        // The symbol faces are painted transparent/silver/gold, so keep a border to still read as a button.
        BorderThickness = new global::Avalonia.Thickness(1),
        BorderBrush = global::Avalonia.Media.Brushes.Gray,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        Classes = { "symbol" },
    };

    private void RemoveTab(string name)
    {
        var tab = Tabs.FirstOrDefault(z => z.Name == name);
        if (tab is null)
            return;
        Tabs.Remove(tab);
        TC_Misc.Items.Remove(tab);
    }

    private bool HasTab(string name) => Tabs.Any(z => z.Name == name);

    private void AddTab(string name, string header, Control content)
    {
        var tab = new TabItem { Name = name, Header = header, Content = content };
        Tabs.Add(tab);
        TC_Misc.Items.Add(tab);
    }

    #region Layout

    private void BuildLayout()
    {
        AddTab("TAB_Main", "Main", BuildMain());
        AddTab("TAB_Joyful", "Joyful", BuildJoyful());
        AddTab("TAB_Ferry", "Ferry", BuildFerry());
        AddTab("TAB_BF", "Battle Frontier", BuildBattleFrontier());
        AddTab("Tab_Records", "Records", BuildRecords());
        AddTab("Tab_Pokeblocks", "Pokéblocks", BuildPokeblocks());
        AddTab("Tab_Decorations", "Decorations", BuildDecorations());
        AddTab("Tab_Paintings", "Paintings", BuildPaintings());
        AddTab("Tab_Other", "Other", UiFactory.Column(B_ForceMirageIsland));

        SetBody(TC_Misc);
    }

    private Control BuildMain()
    {
        var tcm = UiFactory.FormGrid(6);
        for (int i = 0; i < CB_TCM.Length; i++)
            UiFactory.AddFormRow(tcm, i, UiFactory.Label($"L_TCM{i + 1}", $"{i + 1}."), CB_TCM[i]);
        GB_TCM = new GroupBoxView("GB_TCM", "Trainer Card Pokémon Icons", tcm);

        var grid = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(grid, 0, L_Coins, NUD_Coins);
        UiFactory.AddFormRow(grid, 1, L_BP, NUD_BP);
        UiFactory.AddFormRow(grid, 2, L_BPEarned, NUD_BPEarned);
        UiFactory.AddFormRow(grid, 3, L_TrainerName, TB_RivalName);

        return UiFactory.Column(grid, GB_TCM);
    }

    private Control BuildJoyful()
    {
        var jump = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(jump, 0, UiFactory.Label("L_JRow", "In a Row:"), TB_J1);
        UiFactory.AddFormRow(jump, 1, UiFactory.Label("L_JHigh", "High Score:"), TB_J2);
        UiFactory.AddFormRow(jump, 2, UiFactory.Label("L_J5Score", "5 In a Row:"), TB_J3);
        UiFactory.AddFormRow(jump, 3, UiFactory.Label("L_JMaxPlayers", "Max Players:"), TB_J4);

        var berry = UiFactory.FormGrid(3);
        UiFactory.AddFormRow(berry, 0, UiFactory.Label("L_BCaught", "Caught:"), TB_B1);
        UiFactory.AddFormRow(berry, 1, UiFactory.Label("L_BHigh", "High Score:"), TB_B2);
        UiFactory.AddFormRow(berry, 2, UiFactory.Label("L_B5Score", "5 In a Row:"), TB_B3);

        var powder = UiFactory.FormGrid(1);
        UiFactory.AddFormRow(powder, 0, UiFactory.Label("L_BerryPowder", "Berry Powder:"), TB_BerryPowder);

        return UiFactory.Column(
            new GroupBoxView("label4", "Pokémon Jump", jump),
            new GroupBoxView("label5", "Berry Picking", berry),
            powder);
    }

    private Control BuildFerry()
    {
        var reachable = new GroupBoxView("GB_Reachable", "Reachable", UiFactory.Column(
            CHK_ReachSouthern, CHK_ReachBirth, CHK_ReachFaraway, CHK_ReachNavel, CHK_ReachBF));
        var initial = new GroupBoxView("GB_InitialEvent", "Initial Event", UiFactory.Column(
            CHK_InitialSouthern, CHK_InitialBirth, CHK_InitialFaraway, CHK_InitialNavel));

        B_GetTickets.Click += async (_, _) => await ClickGetTickets();
        return UiFactory.Column(CHK_Catchable, UiFactory.Row(reachable, initial), B_GetTickets);
    }

    private Control BuildBattleFrontier()
    {
        var icons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        // Fluent paints its own hover/press background over the symbol colour; keep the assigned colour instead.
        foreach (var state in (string[])[":pointerover", ":pressed"])
        {
            icons.Styles.Add(new global::Avalonia.Styling.Style(x => x.OfType<Button>().Class("symbol").Class(state).Template().OfType<ContentPresenter>())
            {
                Setters =
                {
                    new global::Avalonia.Styling.Setter(ContentPresenter.BackgroundProperty,
                        new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) }),
                },
            });
        }
        foreach (var b in SymbolButtonA)
        {
            b.Click += (s, _) => ClickSymbol((Button)s!);
            icons.Children.Add(b);
        }

        var pass = new GroupBoxView("GB_FrontierPass", "Frontier Pass", UiFactory.Column(
            CHK_ActivatePass, new GroupBoxView("GB_Icons", "Symbol Icons", icons)));

        var statGrid = UiFactory.FormGrid(4);
        for (int i = 0; i < StatNUDA.Length; i++)
            UiFactory.AddFormRow(statGrid, i, StatLabelA[i], StatNUDA[i]);

        var stats = new GroupBoxView("GB_Stats", "Stats", UiFactory.Column(
            UiFactory.Row(L_Facility, CB_Stats1),
            UiFactory.Row(L_Mode, CB_Stats2),
            UiFactory.Row(RB_Stats3_01, RB_Stats3_02),
            statGrid,
            CHK_Continue));

        CB_Stats1.SelectionChanged += (_, _) => ChangeStat1();
        CB_Stats2.SelectionChanged += (_, _) => ChangeStat();
        RB_Stats3_01.IsCheckedChanged += (_, _) => ChangeStat();
        RB_Stats3_02.IsCheckedChanged += (_, _) => ChangeStat();
        foreach (var nud in StatNUDA)
            nud.ValueChanged += (s, _) => ChangeStatVal((NumericUpDown)s!);
        CHK_Continue.IsCheckedChanged += (_, _) => { if (!editingval) SaveContinueFlag(); };

        return UiFactory.Column(pass, stats);
    }

    private Control BuildRecords()
    {
        var fame = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        fame.Children.Add(NUD_FameH);
        fame.Children.Add(NUD_FameM);
        fame.Children.Add(NUD_FameS);

        return UiFactory.Column(CB_Record, NUD_RecordValue, fame);
    }

    private Control BuildPokeblocks()
    {
        LB_Pokeblocks.ItemsSource = BlockItems;
        LB_Pokeblocks.SelectionChanged += (_, _) => ChangeBlockIndex();
        B_PokeblockAll.Click += (_, _) => { Case.MaximizeAll(true); RefreshBlockView(); };
        B_PokeblockDel.Click += (_, _) => { Case.DeleteAll(); RefreshBlockView(); };
        PG_Pokeblocks.PropertyValueChanged += () => SaveBlockIndex(LB_Pokeblocks.SelectedIndex);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(UiFactory.Column(LB_Pokeblocks, UiFactory.Row(B_PokeblockAll, B_PokeblockDel)));
        body.Children.Add(PG_Pokeblocks);
        return body;
    }

    private Control BuildDecorations()
    {
        string[] headers = ["Desk", "Chair", "Plant", "Ornament", "Mat", "Poster", "Doll", "Cushion"];
        var decorations = Util.GetStringList("decoration3", MainWindow.CurrentLanguage);
        var all = Util.GetCBList(decorations);

        // Each category only offers its own decorations, plus the shared empty slot.
        var perCategory = new List<ComboItem>[8];
        for (int i = 0; i < perCategory.Length; i++)
            perCategory[i] = [];
        foreach (var cb in all)
        {
            if (cb.Value == (int)Decoration3.NONE)
            {
                foreach (var list in perCategory)
                    list.Add(cb);
                continue;
            }
            var cat = (int)((Decoration3)cb.Value).GetCategory();
            perCategory[cat].Add(cb);
        }

        var tabs = new TabControl { Name = "TC_Decorations" };
        for (int i = 0; i < DecoGrids.Length; i++)
        {
            DecoRows[i] = [];
            var grid = new DataGrid
            {
                Name = $"DGV_{headers[i]}",
                ItemsSource = DecoRows[i],
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                CanUserReorderColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                Width = 280,
                Height = 320,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            grid.Columns.Add(DataGridUtil.ComboColumn(headers[i], perCategory[i], nameof(DecoRow.Value), 250));
            DecoGrids[i] = grid;
            tabs.Items.Add(new TabItem { Name = $"TB_{headers[i]}", Header = headers[i], Content = grid });
        }
        return tabs;
    }

    private Control BuildPaintings()
    {
        var grid = UiFactory.FormGrid(7);
        UiFactory.AddFormRow(grid, 0, UiFactory.Label("L_Species", "Species:"), CB_Species);
        UiFactory.AddFormRow(grid, 1, UiFactory.Label("L_Caption", "Caption:"), NUD_Caption);
        UiFactory.AddFormRow(grid, 2, UiFactory.Label("L_TID", "TID:"), TB_TID);
        UiFactory.AddFormRow(grid, 3, UiFactory.Label("L_SID", "SID:"), TB_SID);
        UiFactory.AddFormRow(grid, 4, UiFactory.Label("L_PID", "PID:"), UiFactory.Row(TB_PID, CHK_Shiny));
        UiFactory.AddFormRow(grid, 5, UiFactory.Label("L_Nickname", "Nickname:"), TB_Nickname);
        UiFactory.AddFormRow(grid, 6, UiFactory.Label("L_OT", "OT:"), TB_OT);
        GB_Painting = new GroupBoxView("GB_Painting", "Details", grid);

        CHK_Shiny.IsEnabled = false;
        NUD_Painting.ValueChanged += (_, _) => ChangePainting();
        CHK_EnablePaint.IsCheckedChanged += (_, _) => GB_Painting.IsVisible = CHK_EnablePaint.IsChecked == true;
        foreach (var tb in new[] { TB_PID, TB_TID, TB_SID })
            tb.LostFocus += (_, _) => PaintingIDChanged();

        return UiFactory.Column(UiFactory.Row(NUD_Painting, CHK_EnablePaint), GB_Painting);
    }

    #endregion

    #region Joyful

    private void ReadJoyful(ISaveBlock3SmallExpansion j)
    {
        TB_J1.Text = Math.Min((ushort)9999, j.JoyfulJumpInRow).ToString();
        TB_J2.Text = Math.Min(99990, j.JoyfulJumpScore).ToString();
        TB_J3.Text = Math.Min((ushort)9999, j.JoyfulJump5InRow).ToString();
        TB_J4.Text = Math.Min((ushort)9999, j.JoyfulJumpGamesMaxPlayers).ToString();
        TB_B1.Text = Math.Min((ushort)9999, j.JoyfulBerriesInRow).ToString();
        TB_B2.Text = Math.Min(99990, j.JoyfulBerriesScore).ToString();
        TB_B3.Text = Math.Min((ushort)9999, j.JoyfulBerries5InRow).ToString();
        TB_BerryPowder.Text = Math.Min(99999u, j.BerryPowder).ToString();
    }

    private void SaveJoyful(ISaveBlock3SmallExpansion j)
    {
        j.JoyfulJumpInRow = (ushort)Util.ToUInt32(TB_J1.Text);
        j.JoyfulJumpScore = (ushort)Util.ToUInt32(TB_J2.Text);
        j.JoyfulJump5InRow = (ushort)Util.ToUInt32(TB_J3.Text);
        j.JoyfulJumpGamesMaxPlayers = (ushort)Util.ToUInt32(TB_J4.Text);
        j.JoyfulBerriesInRow = (ushort)Util.ToUInt32(TB_B1.Text);
        j.JoyfulBerriesScore = (ushort)Util.ToUInt32(TB_B2.Text);
        j.JoyfulBerries5InRow = (ushort)Util.ToUInt32(TB_B3.Text);
        j.BerryPowder = Util.ToUInt32(TB_BerryPowder.Text);
    }

    #endregion

    #region Ferry

    private const ushort ItemIDOldSeaMap = 0x178;
    private static ReadOnlySpan<ushort> TicketItemIDs => [0x109, 0x113, 0x172, 0x173, ItemIDOldSeaMap];

    private async Task ClickGetTickets()
    {
        var bag = SAV.Inventory;
        var itemlist = GameInfo.Strings.GetItemStrings(SAV.Context, SAV.Version);

        var tickets = TicketItemIDs.ToArray();
        var p = bag.GetPouch(InventoryType.KeyItems);
        bool hasOldSea = p.HasItem(ItemIDOldSeaMap);
        if (!hasOldSea && !SAV.Japanese)
        {
            var ask = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, $"Non Japanese save file. Add {itemlist[ItemIDOldSeaMap]} (unreleased)?");
            if (ask != DialogResult.Yes)
                tickets = tickets[..^1]; // remove old sea map
        }

        var have = new List<ushort>();
        var missing = new List<ushort>();
        foreach (var item in tickets)
            (p.HasItem(item) ? have : missing).Add(item);

        if (missing.Count == 0)
        {
            await AppDialogs.Alert(this, "Already have all tickets.");
            B_GetTickets.IsEnabled = false;
            return;
        }

        int end = p.FindIndexFirstEmptySlot();
        if (end == -1 || end + missing.Count >= p.Items.Length)
        {
            await AppDialogs.Alert(this, "Not enough space in pouch.", "Please use the InventoryEditor.");
            B_GetTickets.IsEnabled = false;
            return;
        }

        var added = Format(missing, itemlist);
        var addmsg = $"Add the following items?{Environment.NewLine}{added}";
        if (have.Count != 0)
            addmsg += $"{Environment.NewLine}{Environment.NewLine}Already have:{Environment.NewLine}{Format(have, itemlist)}";
        if (await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, addmsg) != DialogResult.Yes)
            return;

        for (int i = 0; i < missing.Count; i++)
        {
            var item = p.Items[end + i];
            item.Index = missing[i];
            item.Count = 1;
        }

        await AppDialogs.Alert(this, $"Inserted the following items to the Key Items Pouch:{Environment.NewLine}{added}");
        bag.CopyTo(SAV);
        B_GetTickets.IsEnabled = false;
        return;

        static string Format(List<ushort> items, ReadOnlySpan<string> names)
        {
            var sb = new StringBuilder();
            foreach (var item in items)
            {
                if (sb.Length != 0)
                    sb.Append(", ");
                sb.Append(names[item]);
            }
            return sb.ToString();
        }
    }

    private void ReadFerry()
    {
        CHK_Catchable.IsChecked = SAV.GetEventFlag(0x864);
        CHK_ReachSouthern.IsChecked = SAV.GetEventFlag(0x8B3);
        CHK_ReachBirth.IsChecked = SAV.GetEventFlag(0x8D5);
        CHK_ReachFaraway.IsChecked = SAV.GetEventFlag(0x8D6);
        CHK_ReachNavel.IsChecked = SAV.GetEventFlag(0x8E0);
        CHK_ReachBF.IsChecked = SAV.GetEventFlag(0x1D0);
        CHK_InitialSouthern.IsChecked = SAV.GetEventFlag(0x1AE);
        CHK_InitialBirth.IsChecked = SAV.GetEventFlag(0x1AF);
        CHK_InitialFaraway.IsChecked = SAV.GetEventFlag(0x1B0);
        CHK_InitialNavel.IsChecked = SAV.GetEventFlag(0x1DB);
    }

    private void SaveFerry()
    {
        SAV.SetEventFlag(0x864, CHK_Catchable.IsChecked == true);
        SAV.SetEventFlag(0x8B3, CHK_ReachSouthern.IsChecked == true);
        SAV.SetEventFlag(0x8D5, CHK_ReachBirth.IsChecked == true);
        SAV.SetEventFlag(0x8D6, CHK_ReachFaraway.IsChecked == true);
        SAV.SetEventFlag(0x8E0, CHK_ReachNavel.IsChecked == true);
        SAV.SetEventFlag(0x1D0, CHK_ReachBF.IsChecked == true);
        SAV.SetEventFlag(0x1AE, CHK_InitialSouthern.IsChecked == true);
        SAV.SetEventFlag(0x1AF, CHK_InitialBirth.IsChecked == true);
        SAV.SetEventFlag(0x1B0, CHK_InitialFaraway.IsChecked == true);
        SAV.SetEventFlag(0x1DB, CHK_InitialNavel.IsChecked == true);
    }

    #endregion

    #region Battle Frontier

    private void ReadBattleFrontier()
    {
        loading = true;
        CHK_ActivatePass.IsChecked = SAV.GetEventFlag(BattleFrontier3.FrontierPassFlagIndex);
        for (int i = 0; i < SymbolButtonA.Length; i++)
        {
            var facility = (BattleFrontierFacility3)i;
            var silver = SAV.GetEventFlag(BattleFrontier3.GetSymbolSilverFlagIndex(facility));
            var gold = SAV.GetEventFlag(BattleFrontier3.GetSymbolGoldFlagIndex(facility));
            SetSymbolColor(i, silver ? gold ? Color.Gold : Color.Silver : Color.Transparent);
        }

        CB_Stats1.SetItems([.. Enum.GetValues<BattleFrontierFacility3>().Select(z => new ComboItem(z.ToString(), (int)z))]);

        loading = false;
        CB_Stats1.SelectedIndex = 0;
        ChangeStat1();
    }

    private void SetSymbolColor(int index, Color color)
    {
        SymbolColors[index] = color;
        SymbolButtonA[index].SetBackColor(color);
    }

    private void ClickSymbol(Button sender)
    {
        var index = Array.IndexOf(SymbolButtonA, sender);
        if (index < 0)
            return;
        var color = SymbolColors[index];
        color = color == Color.Transparent ? Color.Silver : color == Color.Silver ? Color.Gold : Color.Transparent;
        SetSymbolColor(index, color);
    }

    private void SaveBattleFrontier()
    {
        for (int i = 0; i < SymbolButtonA.Length; i++)
        {
            var facility = (BattleFrontierFacility3)i;
            var color = SymbolColors[i];
            SAV.SetEventFlag(BattleFrontier3.GetSymbolSilverFlagIndex(facility), color != Color.Transparent);
            SAV.SetEventFlag(BattleFrontier3.GetSymbolGoldFlagIndex(facility), color == Color.Gold);
        }
        SAV.SetEventFlag(BattleFrontier3.FrontierPassFlagIndex, CHK_ActivatePass.IsChecked == true);
    }

    private bool TryGetFacility(out BattleFrontierFacility3 facility)
    {
        facility = default;
        if (loading || CB_Stats1.GetSelectedItem() is not { } item)
            return false;
        facility = (BattleFrontierFacility3)item.Value;
        return true;
    }

    private void ChangeStat1()
    {
        if (!TryGetFacility(out var facility))
            return;

        editingcont = true;
        CB_Stats2.ItemsSource = null;
        RB_Stats3_01.IsChecked = RB_Stats3_02.IsChecked = false;

        int modeCount = BattleFrontier3.GetModeCount(facility);
        if (modeCount == 1)
        {
            CB_Stats2.IsVisible = L_Mode.IsVisible = false;
        }
        else
        {
            CB_Stats2.IsVisible = L_Mode.IsVisible = true;
            CB_Stats2.ItemsSource = Enumerable.Range(0, modeCount).Select(i => ((BattleFrontierBattleMode3)i).ToString()).ToList();
            CB_Stats2.SelectedIndex = 0;
        }

        var validStats = BattleFrontier3.GetValidStats(facility);
        var context = Translator.GetDictionary(MainWindow.CurrentLanguage);
        for (int i = 0; i < StatLabelA.Length; i++)
        {
            bool isValid = i < validStats.Length;
            StatNUDA[i].IsVisible = StatNUDA[i].IsEnabled = isValid;
            StatLabelA[i].IsVisible = isValid;
            if (!isValid)
                continue;

            var key = GetTranslationKey(facility, validStats[i]);
            StatLabelA[i].Text = context.TryGetValue(key, out var text) ? text : key.Split('_')[^1];
        }

        editingcont = false;
        RB_Stats3_01.IsChecked = true;
    }

    private static string GetTranslationKey(BattleFrontierFacility3 facility, BattleFrontierStatType3 stat) => (facility, stat) switch
    {
        (BattleFrontierFacility3.Factory, BattleFrontierStatType3.CurrentSwapped) => "SAV_Misc3.L_CurrentSwapped",
        (BattleFrontierFacility3.Factory, BattleFrontierStatType3.RecordSwapped) => "SAV_Misc3.L_RecordSwapped",
        (BattleFrontierFacility3.Dome, BattleFrontierStatType3.Championships) => "SAV_Misc3.L_Championships",
        (BattleFrontierFacility3.Pike, BattleFrontierStatType3.RecordCleared) => "SAV_Misc3.L_RecordCleared",
        (_, BattleFrontierStatType3.CurrentStreak) => "SAV_Misc3.L_CurrentStreak",
        (_, BattleFrontierStatType3.RecordStreak) => "SAV_Misc3.L_RecordStreak",
        _ => "",
    };

    private void ChangeStat()
    {
        if (editingcont)
            return;
        LoadStatsFromSave();
    }

    private bool TryGetSelection(out BattleFrontierFacility3 facility, out BattleFrontierBattleMode3 mode, out BattleFrontierRecordType3 record)
    {
        mode = default;
        record = default;
        if (!TryGetFacility(out facility))
            return false;

        int modeIndex = CB_Stats2.IsVisible ? CB_Stats2.SelectedIndex : 0;
        if (modeIndex < 0)
            return false;
        mode = (BattleFrontierBattleMode3)modeIndex;

        if (RB_Stats3_01.IsChecked == true)
            record = BattleFrontierRecordType3.Level50;
        else if (RB_Stats3_02.IsChecked == true)
            record = BattleFrontierRecordType3.OpenLevel;
        else
            return false;
        return true;
    }

    private void LoadStatsFromSave()
    {
        if (!TryGetSelection(out var facility, out var mode, out var record))
            return;

        var bf = ((SAV3E)SAV).SmallBlock.BattleFrontier;
        editingval = true;

        var validStats = BattleFrontier3.GetValidStats(facility);
        for (int i = 0; i < validStats.Length; i++)
            StatNUDA[i].SetValueClamped(Math.Min((ushort)9999, bf.GetStat(facility, mode, record, validStats[i])));

        CHK_Continue.IsChecked = bf.GetContinueFlag(facility, mode, record);
        editingval = false;
    }

    private void ChangeStatVal(NumericUpDown nud)
    {
        if (editingval)
            return;
        var statIndex = Array.IndexOf(StatNUDA, nud);
        if (statIndex < 0)
            return;
        if (!TryGetSelection(out var facility, out var mode, out var record))
            return;

        var validStats = BattleFrontier3.GetValidStats(facility);
        if (statIndex >= validStats.Length)
            return;

        var bf = ((SAV3E)SAV).SmallBlock.BattleFrontier;
        bf.SetStat(facility, mode, record, validStats[statIndex], (ushort)Math.Min(9999, nud.Value ?? 0));
    }

    private void SaveContinueFlag()
    {
        if (!TryGetSelection(out var facility, out var mode, out var record))
            return;
        var bf = ((SAV3E)SAV).SmallBlock.BattleFrontier;
        bf.SetContinueFlag(facility, mode, record, CHK_Continue.IsChecked == true);
    }

    #endregion

    #region Records

    private void LoadRecords()
    {
        CB_Record.SetItems([.. Record3.GetItems(SAV)]);
        CB_Record.SelectionChanged += (_, _) =>
        {
            if (CB_Record.GetSelectedItem() is not { } item)
                return;
            NUD_RecordValue.SetValueClamped(SAV.GetRecord(item.Value));
            NUD_FameH.IsVisible = NUD_FameS.IsVisible = NUD_FameM.IsVisible = item.Value == 1;
        };
        CB_Record.SelectedIndex = 0;
        NUD_RecordValue.SetValueClamped(SAV.GetRecord(CB_Record.GetSelectedItem()?.Value ?? 0));

        NUD_RecordValue.ValueChanged += (_, _) =>
        {
            if (CB_Record.GetSelectedItem() is not { } item)
                return;
            var value = (uint)(NUD_RecordValue.Value ?? 0);
            SAV.SetRecord(item.Value, value);
            if (item.Value == 1)
                SetFameTime(value);
        };

        if (SAV is SAV3E em)
        {
            var small = em.SmallBlock;
            NUD_BP.SetValueClamped(small.BP);
            NUD_BPEarned.SetValueClamped(small.BPEarned);
            NUD_BPEarned.ValueChanged += (_, _) => small.BPEarned = (ushort)(NUD_BPEarned.Value ?? 0);
        }
        else
        {
            NUD_BP.IsVisible = L_BP.IsVisible = false;
            NUD_BPEarned.IsVisible = L_BPEarned.IsVisible = false;
        }

        NUD_FameH.ValueChanged += (_, _) => ChangeFame();
        NUD_FameM.ValueChanged += (_, _) => ChangeFame();
        NUD_FameS.ValueChanged += (_, _) => ChangeFame();
    }

    private void ChangeFame()
    {
        var value = GetFameTime();
        NUD_RecordValue.SetValueClamped(value);
        SAV.SetRecord(1, value);
    }

    private uint GetFameTime()
    {
        var hrs = Math.Min(9999u, (uint)(NUD_FameH.Value ?? 0));
        var min = Math.Min(59u, (uint)(NUD_FameM.Value ?? 0));
        var sec = Math.Min(59u, (uint)(NUD_FameS.Value ?? 0));
        return (hrs << 16) | (min << 8) | sec;
    }

    private void SetFameTime(uint time)
    {
        NUD_FameH.SetValueClamped(time >> 16);
        NUD_FameM.SetValueClamped((byte)(time >> 8));
        NUD_FameS.SetValueClamped((byte)time);
    }

    #endregion

    #region Pokéblocks

    private void InitializeBlocks(ISaveBlock3LargeHoenn sav)
    {
        Case = sav.PokeBlocks;
        for (int i = 0; i < Case.Blocks.Length; i++)
            BlockItems.Add(GetPokeblockText(i));
        LB_Pokeblocks.SelectedIndex = 0;
    }

    private string GetPokeblockName(PokeBlock3Color color)
    {
        var index = (uint)color;
        if (index >= BlockNames.Length)
            index = 0;
        return BlockNames[index];
    }

    private string GetPokeblockText(int index) => $"{index + 1:00} - {GetPokeblockName(Case.Blocks[index].Color)}";

    private void RefreshBlockView()
    {
        PG_Pokeblocks.SetObject(PG_Pokeblocks.SelectedObject);
        UpdatingBlocks = true;
        var selected = LB_Pokeblocks.SelectedIndex;
        for (int i = 0; i < Case.Blocks.Length; i++)
            BlockItems[i] = GetPokeblockText(i);
        LB_Pokeblocks.SelectedIndex = selected; // replacing the item drops the selection
        UpdatingBlocks = false;
    }

    private void SaveBlockIndex(int index)
    {
        if (index < 0)
            return;
        UpdatingBlocks = true;
        var selected = LB_Pokeblocks.SelectedIndex;
        BlockItems[index] = GetPokeblockText(index);
        LB_Pokeblocks.SelectedIndex = selected; // replacing the item drops the selection
        UpdatingBlocks = false;
    }

    private void ChangeBlockIndex()
    {
        if (UpdatingBlocks)
            return;
        SaveBlockIndex(CurrentBlockIndex);
        CurrentBlockIndex = LB_Pokeblocks.SelectedIndex;
        if (CurrentBlockIndex < 0)
        {
            LB_Pokeblocks.SelectedIndex = 0;
            return;
        }
        PG_Pokeblocks.SetObject(Case.Blocks[CurrentBlockIndex]);
    }

    #endregion

    #region Decorations

    private sealed class DecoRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private int _value;

        public int Value
        {
            get => _value;
            set
            {
                if (_value == value)
                    return;
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    private void ReadDecorations(ISaveBlock3LargeHoenn h)
    {
        var d = h.Decorations;
        Read(0, d.Desk); Read(1, d.Chair); Read(2, d.Plant); Read(3, d.Ornament);
        Read(4, d.Mat); Read(5, d.Poster); Read(6, d.Doll); Read(7, d.Cushion);

        void Read(int category, ReadOnlySpan<Decoration3> data)
        {
            var rows = DecoRows[category];
            rows.Clear();
            foreach (var deco in data)
                rows.Add(new DecoRow { Value = (int)deco });
        }
    }

    private void SaveDecorations(ISaveBlock3LargeHoenn h)
    {
        var d = h.Decorations;
        Save(0, d.Desk); Save(1, d.Chair); Save(2, d.Plant); Save(3, d.Ornament);
        Save(4, d.Mat); Save(5, d.Poster); Save(6, d.Doll); Save(7, d.Cushion);

        void Save(int category, Span<Decoration3> data)
        {
            var rows = DecoRows[category];
            int ctr = 0;
            for (int i = 0; i < data.Length && i < rows.Count; i++)
            {
                var deco = (Decoration3)rows[i].Value;
                if (deco == Decoration3.NONE) // Compression of Empty Slots
                    continue;
                data[ctr++] = deco;
            }
            for (int i = ctr; i < data.Length; i++)
                data[i] = Decoration3.NONE; // Empty Slots at the end
        }
    }

    #endregion

    #region Paintings

    private void LoadPaintings() => LoadPainting((int)(NUD_Painting.Value ?? 0));
    private void SavePaintings() => SavePainting((int)(NUD_Painting.Value ?? 0));

    private void ChangePainting()
    {
        var index = (int)(NUD_Painting.Value ?? 0);
        if (PaintingIndex == index)
            return;
        SavePainting(PaintingIndex);
        LoadPainting(index);
    }

    private void LoadPainting(int index)
    {
        if ((uint)index >= 5)
            return;
        if (SAV.LargeBlock is not ISaveBlock3LargeHoenn gallery)
            return;
        var painting = gallery.GetPainting(index, SAV.Japanese);

        loadingPainting = true;
        GB_Painting.IsVisible = SAV.GetEventFlag(Paintings3.GetFlagIndexContestStat(index));
        CHK_EnablePaint.IsChecked = GB_Painting.IsVisible;

        CB_Species.SetValue(painting.Species);
        NUD_Caption.SetValueClamped(painting.GetCaptionRelative(index));
        TB_TID.Text = painting.TID.ToString();
        TB_SID.Text = painting.SID.ToString();
        TB_PID.Text = painting.PID.ToString("X8");
        TB_Nickname.Text = painting.Nickname;
        TB_OT.Text = painting.OT;
        loadingPainting = false;

        PaintingIndex = index;
        NUD_Painting.SetBackColor(ContestColor.GetColor(index));
        PaintingIDChanged();
    }

    private void SavePainting(int index)
    {
        if ((uint)index >= 5)
            return;
        if (SAV.LargeBlock is not ISaveBlock3LargeHoenn gallery)
            return;
        var painting = gallery.GetPainting(index, SAV.Japanese);

        var enabled = CHK_EnablePaint.IsChecked == true;
        SAV.SetEventFlag(Paintings3.GetFlagIndexContestStat(index), enabled);
        if (!enabled)
        {
            painting.Clear();
            gallery.SetPainting(index, painting);
            return;
        }

        painting.Species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
        painting.SetCaptionRelative(index, (byte)(NUD_Caption.Value ?? 0));
        painting.TID = (ushort)Util.ToUInt32(TB_TID.Text);
        painting.SID = (ushort)Util.ToUInt32(TB_SID.Text);
        painting.PID = Util.GetHexValue(TB_PID.Text);
        painting.Nickname = TB_Nickname.Text ?? string.Empty;
        painting.OT = TB_OT.Text ?? string.Empty;

        gallery.SetPainting(index, painting);
    }

    private void PaintingIDChanged()
    {
        if (loadingPainting)
            return;
        ValidatePaintingIDs();

        var pid = Util.GetHexValue(TB_PID.Text);
        var tid = Util.ToUInt32(TB_TID.Text);
        var sid = Util.ToUInt32(TB_SID.Text);
        CHK_Shiny.IsChecked = ShinyUtil.GetIsShiny3((sid << 16) | tid, pid);
    }

    private void ValidatePaintingIDs()
    {
        var pid = Util.GetHexValue(TB_PID.Text);
        if (pid.ToString("X") != TB_PID.Text && pid.ToString("X8") != TB_PID.Text)
            TB_PID.Text = pid.ToString();

        var tid = Math.Min(ushort.MaxValue, Util.ToUInt32(TB_TID.Text));
        if (tid.ToString() != TB_TID.Text)
            TB_TID.Text = tid.ToString();

        var sid = Math.Min(ushort.MaxValue, Util.ToUInt32(TB_SID.Text));
        if (sid.ToString() != TB_SID.Text)
            TB_SID.Text = sid.ToString();
    }

    #endregion

    private void ClickForceMirageIsland()
    {
        if (SAV.SmallBlock is not ISaveBlock3SmallHoenn) // Only run for R/S/E
            return;

        // Mirage island appears when work value 0x24 matches the low PID half of the first party member.
        var party1 = SAV.LargeBlock.PartyBuffer;
        var pidLow = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(party1);
        SAV.SetWork(0x24, pidLow);
        B_ForceMirageIsland.IsEnabled = false; // disable to indicate the cheat was activated.
    }

    protected override void OnSave()
    {
        if (SAV.LargeBlock is ISaveBlock3LargeHoenn h)
        {
            SaveBlockIndex(CurrentBlockIndex);
            h.PokeBlocks = Case;
            SaveDecorations(h);
            SavePaintings();
        }
        if (HasTab("TAB_Joyful") && SAV.SmallBlock is ISaveBlock3SmallExpansion j)
            SaveJoyful(j);
        if (HasTab("TAB_Ferry"))
            SaveFerry();
        if (HasTab("TAB_BF"))
            SaveBattleFrontier();
        if (SAV is SAV3FRLG frlg)
        {
            if (frlg.RivalName != TB_RivalName.Text)
                frlg.RivalName = TB_RivalName.Text ?? string.Empty; // preserve trash
            for (int i = 0; i < CB_TCM.Length; i++)
            {
                var species = (ushort)(CB_TCM[i].GetSelectedItem()?.Value ?? 0);
                SAV.SetWork(0x43 + i, SpeciesConverter.GetInternal3(species));
            }
        }

        if (SAV is SAV3E se)
            se.SmallBlock.BP = (ushort)(NUD_BP.Value ?? 0);
        SAV.Coin = (uint)(NUD_Coins.Value ?? 0);

        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
