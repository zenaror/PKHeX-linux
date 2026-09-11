using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4;

/// <summary>
/// Underground editor for Diamond/Pearl/Platinum (port of the WinForms <c>SAV_Underground</c>).
/// </summary>
/// <remarks>
/// Four 40-slot pouches (goods, spheres, traps, treasures) plus the Underground score counters. The
/// pouches are stored compacted, so saving skips empty slots and rewrites the list from the top.
/// The WinForms form has no label for the "helped others" counter; one is added here.
/// </remarks>
public sealed class Underground4Window : SaveEditorWindow
{
    private const int MAX_SIZE = SAV4Sinnoh.UG_POUCH_SIZE;

    private readonly SaveFile Origin;
    private readonly SAV4Sinnoh SAV;

    private readonly string[] ugGoods, ugSpheres, ugTraps, ugTreasures;
    private readonly string[] ugGoodsSorted, ugTrapsSorted, ugTreasuresSorted;

    private readonly ObservableCollection<NameRow> GoodsRows = [];
    private readonly ObservableCollection<CountedRow> SphereRows = [];
    private readonly ObservableCollection<NameRow> TrapRows = [];
    private readonly ObservableCollection<NameRow> TreasureRows = [];

    private readonly NumericUpDown NUD_PlayersMet = Score("NUD_PlayersMet");
    private readonly NumericUpDown NUD_GiftsGiven = Score("NUD_GiftsGiven");
    private readonly NumericUpDown NUD_GiftsReceived = Score("NUD_GiftsReceived");
    private readonly NumericUpDown NUD_Spheres = Score("NUD_Spheres");
    private readonly NumericUpDown NUD_Fossils = Score("NUD_Fossils");
    private readonly NumericUpDown NUD_TrapPlayers = Score("NUD_TrapPlayers");
    private readonly NumericUpDown NUD_TrapSelf = Score("NUD_TrapSelf");
    private readonly NumericUpDown NUD_MyBaseMoved = Score("NUD_MyBaseMoved");
    private readonly NumericUpDown NUD_FlagsObtained = Score("NUD_FlagsObtained");
    private readonly NumericUpDown NUD_MyFlagTaken = Score("NUD_MyFlagTaken");
    private readonly NumericUpDown NUD_MyFlagRecovered = Score("NUD_MyFlagRecovered");
    private readonly NumericUpDown NUD_FlagsCaptured = Score("NUD_FlagsCaptured");
    private readonly NumericUpDown NUD_HelpedOthers = Score("NUD_HelpedOthers");

    private static NumericUpDown Score(string name) => UiFactory.NumericUpDown(name, 0, SAV4Sinnoh.UG_MAX, 120);

    public Underground4Window(SAV4Sinnoh sav) : base("SAV_Underground", "Underground Editor")
    {
        SAV = (SAV4Sinnoh)(Origin = sav).Clone();

        ugGoods = GameInfo.Strings.uggoods;
        ugSpheres = GameInfo.Strings.ugspheres;
        ugTraps = GameInfo.Strings.ugtraps;
        ugTreasures = GameInfo.Strings.ugtreasures;

        ugGoodsSorted = SanitizeList(ugGoods);
        ugTrapsSorted = SanitizeList(ugTraps);
        ugTreasuresSorted = SanitizeList(ugTreasures);

        BuildLayout();
        GetUGScores();
        ReadUGData();
    }

    private static string[] SanitizeList(string[] input)
    {
        var sorted = Array.FindAll(input, x => !string.IsNullOrEmpty(x));
        Array.Sort(sorted);
        return sorted;
    }

    #region Rows

    private sealed class NameRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }
    }

    private sealed class CountedRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private string _name = string.Empty;
        private string _count = "0";

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public string Count
        {
            get => _count;
            set
            {
                if (_count == value)
                    return;
                _count = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            }
        }
    }

    #endregion

    private void BuildLayout()
    {
        var scores = UiFactory.FormGrid(13);
        AddScore(scores, 0, "L_PeopleMet", "People Met:", NUD_PlayersMet);
        AddScore(scores, 1, "L_Gifts", "Gifts Given:", NUD_GiftsGiven);
        AddScore(scores, 2, "L_GiftsReceived", "Gifts Received:", NUD_GiftsReceived);
        AddScore(scores, 3, "L_Spheres", "Spheres Dug:", NUD_Spheres);
        AddScore(scores, 4, "L_Fossils", "Fossils Dug:", NUD_Fossils);
        AddScore(scores, 5, "L_TrapOthers", "Trap Hits (Players):", NUD_TrapPlayers);
        AddScore(scores, 6, "L_TrapSelf", "Trap Hits (Self):", NUD_TrapSelf);
        AddScore(scores, 7, "L_MyBaseMoved", "Moved My Base:", NUD_MyBaseMoved);
        AddScore(scores, 8, "L_FlagsObtained", "Flags Obtained:", NUD_FlagsObtained);
        AddScore(scores, 9, "L_MyFlagTaken", "My Flag Taken:", NUD_MyFlagTaken);
        AddScore(scores, 10, "L_MyFlagRecovered", "Recovered Flags:", NUD_MyFlagRecovered);
        AddScore(scores, 11, "L_FlagsCaptured", "Captured Flags:", NUD_FlagsCaptured);
        AddScore(scores, 12, "L_HelpedOthers", "Helped Others:", NUD_HelpedOthers);
        var gbScores = new GroupBoxView("GB_UScores", "Scores", scores);

        var tabs = new TabControl { Name = "TC_Underground" };
        tabs.Items.Add(new TabItem { Name = "TB_UGGoods", Header = "Goods", Content = NameGrid("DGV_UGGoods", GoodsRows, ugGoodsSorted) });
        tabs.Items.Add(new TabItem { Name = "TB_UGSpheres", Header = "Spheres", Content = SphereGrid() });
        tabs.Items.Add(new TabItem { Name = "TB_UGTraps", Header = "Traps", Content = NameGrid("DGV_UGTraps", TrapRows, ugTrapsSorted) });
        tabs.Items.Add(new TabItem { Name = "TB_UGTreasures", Header = "Treasures", Content = NameGrid("DGV_UGTreasures", TreasureRows, ugTreasuresSorted) });

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(gbScores);
        body.Children.Add(tabs);
        SetBody(new ScrollViewer { Content = body, MaxHeight = 640 });
        return;

        static void AddScore(Grid g, int row, string name, string text, NumericUpDown nud)
            => UiFactory.AddFormRow(g, row, UiFactory.Label(name, text), nud);
    }

    private static DataGrid MakeGrid(string name, System.Collections.IEnumerable rows, double width)
        => new()
        {
            Name = name,
            ItemsSource = rows,
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            CanUserReorderColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Width = width,
            Height = 420,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

    private static DataGrid NameGrid(string name, ObservableCollection<NameRow> rows, string[] options)
    {
        var grid = MakeGrid(name, rows, 260);
        grid.Columns.Add(DataGridUtil.StringComboColumn("Item", options, nameof(NameRow.Name), 230));
        return grid;
    }

    private DataGrid SphereGrid()
    {
        var grid = MakeGrid("DGV_UGSpheres", SphereRows, 330);
        grid.Columns.Add(DataGridUtil.StringComboColumn("Sphere", ugSpheres, nameof(CountedRow.Name), 220));
        grid.Columns.Add(new DataGridTextColumn { Header = "Size", Binding = new Binding(nameof(CountedRow.Count)) { Mode = BindingMode.TwoWay }, Width = new DataGridLength(80) });
        return grid;
    }

    #region Pouch data

    private void ReadUGData()
    {
        var goodsList = SAV.GetUGI_Goods();
        var spheresList = SAV.GetUGI_Spheres();
        var trapsList = SAV.GetUGI_Traps();
        var treasuresList = SAV.GetUGI_Treasures();

        var sphereCount = spheresList[MAX_SIZE..];
        spheresList = spheresList[..MAX_SIZE];

        for (int i = 0; i < goodsList.Length; i++)
        {
            var goods = goodsList[i];
            if (goods >= ugGoods.Length)
                goods = 0;
            GoodsRows.Add(new NameRow { Name = ugGoods[goods] });
        }

        for (int i = 0; i < spheresList.Length; i++)
        {
            var sphere = spheresList[i];
            var count = sphereCount[i];
            if (sphere >= ugSpheres.Length)
                sphere = count = 0;
            SphereRows.Add(new CountedRow { Name = ugSpheres[sphere], Count = count.ToString() });
        }

        for (int i = 0; i < trapsList.Length; i++)
        {
            var trap = trapsList[i];
            if (trap >= ugTraps.Length)
                trap = 0;
            TrapRows.Add(new NameRow { Name = ugTraps[trap] });
        }

        for (int i = 0; i < treasuresList.Length; i++)
        {
            var treasure = treasuresList[i];
            if (treasure >= ugTreasures.Length)
                treasure = 0;
            TreasureRows.Add(new NameRow { Name = ugTreasures[treasure] });
        }
    }

    private void SaveUGData()
    {
        var goodsList = SAV.GetUGI_Goods();
        var spheresList = SAV.GetUGI_Spheres();
        var trapsList = SAV.GetUGI_Traps();
        var treasuresList = SAV.GetUGI_Treasures();

        goodsList.Clear();
        spheresList.Clear();
        trapsList.Clear();
        treasuresList.Clear();

        WriteNames(GoodsRows, ugGoods, goodsList);
        WriteNames(TrapRows, ugTraps, trapsList);
        WriteNames(TreasureRows, ugTreasures, treasuresList);

        int ctr = 0;
        foreach (var row in SphereRows)
        {
            var itemIndex = ugSpheres.IndexOf(row.Name);
            bool success = int.TryParse(row.Count, out var itemCount);
            if (!success || itemIndex <= 0)
                continue; // ignore empty slot or non-numeric values

            spheresList[ctr] = (byte)itemIndex;
            spheresList[ctr + MAX_SIZE] = (byte)itemCount;
            ctr++;
        }
        return;

        static void WriteNames(ObservableCollection<NameRow> rows, string[] names, Span<byte> dest)
        {
            int ctr = 0;
            foreach (var row in rows)
            {
                var itemIndex = names.IndexOf(row.Name);
                if (itemIndex <= 0)
                    continue; // ignore empty slot
                dest[ctr++] = (byte)itemIndex;
            }
        }
    }

    #endregion

    #region Scores

    private void GetUGScores()
    {
        Load(NUD_PlayersMet, SAV.UG_PeopleMet);
        Load(NUD_GiftsGiven, SAV.UG_GiftsGiven);
        Load(NUD_GiftsReceived, SAV.UG_GiftsReceived);
        Load(NUD_Spheres, SAV.UG_Spheres);
        Load(NUD_Fossils, SAV.UG_Fossils);
        Load(NUD_TrapPlayers, SAV.UG_TrapPlayers);
        Load(NUD_TrapSelf, SAV.UG_TrapSelf);
        Load(NUD_MyBaseMoved, SAV.UG_MyBaseMoved);
        Load(NUD_FlagsObtained, SAV.UG_FlagsTaken);
        Load(NUD_MyFlagTaken, SAV.UG_FlagsFromMe);
        Load(NUD_MyFlagRecovered, SAV.UG_FlagsRecovered);
        Load(NUD_FlagsCaptured, SAV.UG_FlagsCaptured);
        Load(NUD_HelpedOthers, SAV.UG_HelpedOthers);

        static void Load(NumericUpDown box, uint value) => box.SetValueClamped(Math.Clamp(value, 0, SAV4Sinnoh.UG_MAX));
    }

    private void SetUGScores()
    {
        SAV.UG_PeopleMet = Get(NUD_PlayersMet);
        SAV.UG_GiftsGiven = Get(NUD_GiftsGiven);
        SAV.UG_GiftsReceived = Get(NUD_GiftsReceived);
        SAV.UG_Spheres = Get(NUD_Spheres);
        SAV.UG_Fossils = Get(NUD_Fossils);
        SAV.UG_TrapPlayers = Get(NUD_TrapPlayers);
        SAV.UG_TrapSelf = Get(NUD_TrapSelf);
        SAV.UG_MyBaseMoved = Get(NUD_MyBaseMoved);
        SAV.UG_FlagsTaken = Get(NUD_FlagsObtained);
        SAV.UG_FlagsFromMe = Get(NUD_MyFlagTaken);
        SAV.UG_FlagsRecovered = Get(NUD_MyFlagRecovered);
        SAV.UG_FlagsCaptured = Get(NUD_FlagsCaptured);
        SAV.UG_HelpedOthers = Get(NUD_HelpedOthers);

        static uint Get(NumericUpDown box) => (uint)(box.Value ?? 0);
    }

    #endregion

    protected override void OnSave()
    {
        SetUGScores();
        SaveUGData();
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
