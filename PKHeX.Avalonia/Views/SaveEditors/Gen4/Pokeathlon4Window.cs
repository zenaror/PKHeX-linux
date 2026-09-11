using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Views.SaveEditors.Gen4.Pokeathlon;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4;

/// <summary>
/// Pokéathlon editor for HeartGold / SoulSilver (port of the WinForms <c>SAV_Pokeathlon4</c>).
/// </summary>
/// <remarks>
/// Seven pages: the points and unlock flags, the per-species medal grid, the global counters, the best
/// score per event, the five course records with their entrants, and the single-player and multiplayer
/// record sets for each event.
/// </remarks>
public sealed class Pokeathlon4Window : SaveEditorWindow
{
    private const int MaxSpeciesGen4 = 493;

    private readonly SAV4HGSS Origin;
    private readonly SAV4HGSS SAV;
    private readonly Pokeathlon4 Pokeathlon;

    private readonly List<CounterBinding> CounterEditors = [];
    private readonly List<(PokeathlonEvent4 Event, NumericUpDown Editor)> BestScoreEditors = [];
    private readonly CheckBox[] DailyShopEditors = new CheckBox[12];
    private readonly NumericUpDown[] CourseScoreEditors;
    private readonly PokeathlonParticipant4View[] CourseParticipantEditors;

    private bool IsLoading;
    private int CurrentCourseIndex = -1;
    private int CurrentSelfEventIndex = -1;
    private int CurrentConnectionIndex = -1;

    private readonly NumericUpDown NUD_Points = UiFactory.NumericUpDown("NUD_Points", 0, Pokeathlon4.MaxPoints, 130);
    private readonly CheckedListView CLB_DataCards = new() { Name = "CLB_DataCards", Width = 300, Height = 300 };
    private readonly ObservableCollection<MedalRow> MedalRows = [];
    private readonly ComboBox CB_CourseIndex = UiFactory.Combo("CB_CourseIndex", 200);
    private readonly ComboBox CB_SelfEventIndex = UiFactory.Combo("CB_SelfEventIndex", 200);
    private readonly ComboBox CB_ConnectionIndex = UiFactory.Combo("CB_ConnectionIndex", 200);
    private readonly PokeathlonEventData4View UC_SelfEventData = new();
    private readonly PokeathlonConnection4View UC_Connection = new();
    private Panel CountersHost = null!;
    private Panel BestHost = null!;

    private static string Localize<T>(T value) where T : Enum => Translator.TranslateEnum(value, MainWindow.CurrentLanguage);
    private static string GetCourseDisplayName(PokeathlonStat4 stat) => $"{(int)stat + 1} - {Localize(stat)}";
    private static string GetEventDisplayName(PokeathlonEvent4 value) => $"{(int)value + 1} - {Localize(value)}";

    public Pokeathlon4Window(SAV4HGSS sav) : base("SAV_Pokeathlon4", "Pokéathlon Editor")
    {
        Origin = sav;
        SAV = (SAV4HGSS)sav.Clone();
        Pokeathlon = SAV.Pokeathlon;

        for (int i = 0; i < DailyShopEditors.Length; i++)
            DailyShopEditors[i] = UiFactory.Check($"CHK_DailyShop{i}", (i + 1).ToString());
        CourseScoreEditors = [
            UiFactory.NumericUpDown("NUD_CourseScore0", 0, ushort.MaxValue, 120),
            UiFactory.NumericUpDown("NUD_CourseScore1", 0, ushort.MaxValue, 120),
            UiFactory.NumericUpDown("NUD_CourseScore2", 0, ushort.MaxValue, 120),
            UiFactory.NumericUpDown("NUD_CourseScoreMax", 0, ushort.MaxValue, 120),
        ];
        CourseParticipantEditors = [
            new PokeathlonParticipant4View("Participant 1:"),
            new PokeathlonParticipant4View("Participant 2:"),
            new PokeathlonParticipant4View("Participant 3:"),
        ];

        BuildLayout();
        InitializeGeneral();
        InitializeMedals();
        InitializeCounters();
        InitializeBestScores();
        InitializeIndexes();
        LoadData();
    }

    #region Layout

    private void BuildLayout()
    {
        var tabs = new TabControl { Name = "TC_Pokeathlon" };
        tabs.Items.Add(new TabItem { Name = "Tab_General", Header = "General", Content = BuildGeneral() });
        tabs.Items.Add(new TabItem { Name = "Tab_Medals", Header = "Medals", Content = BuildMedals() });
        tabs.Items.Add(new TabItem { Name = "Tab_Counters", Header = "Counters", Content = BuildCounters() });
        tabs.Items.Add(new TabItem { Name = "Tab_Best", Header = "Best", Content = BuildBest() });
        tabs.Items.Add(new TabItem { Name = "Tab_Courses", Header = "Courses", Content = BuildCourses() });
        tabs.Items.Add(new TabItem { Name = "Tab_SelfEvent", Header = "Self Event", Content = BuildSelfEvent() });
        tabs.Items.Add(new TabItem { Name = "Tab_Connection", Header = "Connection", Content = BuildConnection() });
        SetBody(tabs);

        CB_CourseIndex.SelectionChanged += (_, _) => ChangeCourseIndex();
        CB_SelfEventIndex.SelectionChanged += (_, _) => ChangeSelfEventIndex();
        CB_ConnectionIndex.SelectionChanged += (_, _) => ChangeConnectionIndex();
    }

    private Control BuildGeneral()
    {
        var shop = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var c in DailyShopEditors)
            shop.Children.Add(c);

        var body = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_Points", "Points:"), NUD_Points),
            UiFactory.Label("L_DailyShopFlags", "Daily Shop:"),
            shop,
            UiFactory.Label("L_DataCards", "Data Cards:"),
            CLB_DataCards);
        return new ScrollViewer { Content = body, MaxHeight = 620 };
    }

    private Control BuildMedals()
    {
        var grid = new DataGrid
        {
            Name = "DGV_Medals",
            ItemsSource = MedalRows,
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            CanUserReorderColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Width = 640,
            Height = 520,
            RowHeight = 40,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Species", Binding = new Binding(nameof(MedalRow.Species)), IsReadOnly = true, Width = new DataGridLength(80) });
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Sprite",
            Width = new DataGridLength(56),
            IsReadOnly = true,
            CellTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<MedalRow>((_, _) =>
            {
                var img = new Image { Width = 40, Height = 32, Stretch = global::Avalonia.Media.Stretch.Uniform };
                img.Bind(Image.SourceProperty, new Binding(nameof(MedalRow.Sprite)));
                return img;
            }),
        });

        var names = Enum.GetNames<PokeathlonStat4>().Take((int)PokeathlonStat4.Count).ToArray();
        for (int i = 0; i < names.Length; i++)
        {
            grid.Columns.Add(DataGridUtil.CheckColumn(names[i], $"M{i}", 80));
        }

        var giveAll = UiFactory.Button("B_MedalsGiveAll", "Give All");
        var clearAll = UiFactory.Button("B_MedalsClearAll", "Clear All");
        giveAll.Click += (_, _) => { var m = Pokeathlon.Medals; m.SetAllMedals(); LoadMedals(); };
        clearAll.Click += (_, _) => { var m = Pokeathlon.Medals; m.Clear(); LoadMedals(); };

        return UiFactory.Column(grid, UiFactory.Row(giveAll, clearAll));
    }

    private Control BuildCounters()
    {
        CountersHost = new Panel();
        return new ScrollViewer { Content = CountersHost, MaxHeight = 620 };
    }

    private Control BuildBest()
    {
        BestHost = new Panel();
        return new ScrollViewer { Content = BestHost, MaxHeight = 620 };
    }

    /// <summary>Builds the label/editor rows as one grid so the labels line up, as the WinForms table layout does.</summary>
    private static void FillRows(Panel host, List<(string Name, string Text, Control Editor)> rows)
    {
        var grid = UiFactory.FormGrid(rows.Count);
        for (int i = 0; i < rows.Count; i++)
            UiFactory.AddFormRow(grid, i, UiFactory.Label(rows[i].Name, rows[i].Text), rows[i].Editor);
        host.Children.Add(grid);
    }

    private Control BuildCourses()
    {
        var scores = UiFactory.FormGrid(4);
        string[] labels = ["Score0:", "Score1:", "Score2:", "ScoreMax:"];
        for (int i = 0; i < CourseScoreEditors.Length; i++)
            UiFactory.AddFormRow(scores, i, UiFactory.Label($"L_CourseScore{i}", labels[i]), CourseScoreEditors[i]);

        var body = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_CourseIndex", "Index:"), CB_CourseIndex),
            scores);
        foreach (var p in CourseParticipantEditors)
            body.Children.Add(p);
        return new ScrollViewer { Content = body, MaxHeight = 620 };
    }

    private Control BuildSelfEvent()
    {
        var body = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_SelfEventIndex", "Index:"), CB_SelfEventIndex),
            UC_SelfEventData);
        return new ScrollViewer { Content = body, MaxHeight = 620 };
    }

    private Control BuildConnection()
    {
        var body = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_ConnectionIndex", "Index:"), CB_ConnectionIndex),
            UC_Connection);
        return new ScrollViewer { Content = body, MaxHeight = 620 };
    }

    #endregion

    #region Rows

    /// <summary>
    /// One species row of the medal grid. The five medal bits are separate properties rather than an
    /// indexer, because an Avalonia grid column bound to <c>Medals[i]</c> is not told when the backing
    /// array changes and would not refresh on "Give All".
    /// </summary>
    private sealed class MedalRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public required ushort Species { get; init; }
        public Bitmap? Sprite { get; init; }

        private bool _m0, _m1, _m2, _m3, _m4;

        public bool M0 { get => _m0; set => Set(ref _m0, value, nameof(M0)); }
        public bool M1 { get => _m1; set => Set(ref _m1, value, nameof(M1)); }
        public bool M2 { get => _m2; set => Set(ref _m2, value, nameof(M2)); }
        public bool M3 { get => _m3; set => Set(ref _m3, value, nameof(M3)); }
        public bool M4 { get => _m4; set => Set(ref _m4, value, nameof(M4)); }

        private void Set(ref bool field, bool value, string name)
        {
            if (field == value)
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public byte ToBits()
        {
            byte bits = 0;
            if (_m0) bits |= 1 << 0;
            if (_m1) bits |= 1 << 1;
            if (_m2) bits |= 1 << 2;
            if (_m3) bits |= 1 << 3;
            if (_m4) bits |= 1 << 4;
            return bits;
        }

        public void FromBits(byte bits)
        {
            M0 = ((bits >> 0) & 1) != 0;
            M1 = ((bits >> 1) & 1) != 0;
            M2 = ((bits >> 2) & 1) != 0;
            M3 = ((bits >> 3) & 1) != 0;
            M4 = ((bits >> 4) & 1) != 0;
        }
    }

    private sealed record CounterBinding(string Name, NumericUpDown Editor, Func<uint> Getter, Action<uint>? Setter);

    #endregion

    #region Initialization

    private void InitializeGeneral()
    {
        var items = GameInfo.Strings.GetItemStrings(EntityContext.Gen4);
        for (int i = 0; i < (int)DataCard4.Count; i++)
            CLB_DataCards.Add($"[{i}] {items[505 + i]}"); // Data Card 01...
    }

    private void InitializeMedals()
    {
        for (ushort species = 1; species <= MaxSpeciesGen4; species++)
        {
            var sprite = SpriteUtil.GetSprite(species, 0, 0, 0, 0, false, Shiny.Never, EntityContext.Gen4);
            MedalRows.Add(new MedalRow { Species = species, Sprite = sprite.ToAvaloniaBitmapAndDispose() });
        }
    }

    private void InitializeCounters()
    {
        List<(string Name, string Text, Control Editor)> rows = [];
        Add("TimeSpent", PokeathlonGlobalCounters4.MaxPlay, () => Pokeathlon.GlobalCounters.TimeSpent, v => { var c = Pokeathlon.GlobalCounters; c.TimeSpent = v; });
        Add("SessionsJoined", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.SessionsJoined, v => { var c = Pokeathlon.GlobalCounters; c.SessionsJoined = v; });
        Add("PlacedFirst", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.PlacedFirst, v => { var c = Pokeathlon.GlobalCounters; c.PlacedFirst = v; });
        Add("PlacedLast", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.PlacedLast, v => { var c = Pokeathlon.GlobalCounters; c.PlacedLast = v; });
        Add("BonusesEarned", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.BonusesEarned, v => { var c = Pokeathlon.GlobalCounters; c.BonusesEarned = v; });
        Add("Instructions", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.Instructions, v => { var c = Pokeathlon.GlobalCounters; c.Instructions = v; });
        Add("Failed", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.Failed, v => { var c = Pokeathlon.GlobalCounters; c.Failed = v; });
        Add("Jumped", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.Jumped, v => { var c = Pokeathlon.GlobalCounters; c.Jumped = v; });
        Add("Acquired", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.Acquired, v => { var c = Pokeathlon.GlobalCounters; c.Acquired = v; });
        Add("Tackled", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.Tackled, v => { var c = Pokeathlon.GlobalCounters; c.Tackled = v; });
        Add("FellDown", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.FellDown, v => { var c = Pokeathlon.GlobalCounters; c.FellDown = v; });
        Add("Dashed", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.Dashed, v => { var c = Pokeathlon.GlobalCounters; c.Dashed = v; });
        Add("Switched", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.Switched, v => { var c = Pokeathlon.GlobalCounters; c.Switched = v; });
        Add("SelfImpeded", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.SelfImpeded, v => { var c = Pokeathlon.GlobalCounters; c.SelfImpeded = v; });
        Add("ConnectionJoined", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.ConnectionJoined, v => { var c = Pokeathlon.GlobalCounters; c.ConnectionJoined = v; });
        Add("ConnectionFirst", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.ConnectionFirst, v => { var c = Pokeathlon.GlobalCounters; c.ConnectionFirst = v; });
        Add("ConnectionLast", PokeathlonGlobalCounters4.MaxStat, () => Pokeathlon.GlobalCounters.ConnectionLast, v => { var c = Pokeathlon.GlobalCounters; c.ConnectionLast = v; });

        for (int i = 0; i < (int)PokeathlonEvent4.Count; i++)
        {
            var eventIndex = (PokeathlonEvent4)i;
            Add($"{Localize(eventIndex)}First", PokeathlonGlobalCounters4.MaxStat,
                () => { var c = Pokeathlon.GlobalCounters; return c[eventIndex]; },
                v => { var c = Pokeathlon.GlobalCounters; c[eventIndex] = v; });
        }

        // Derived total: read-only, as in WinForms.
        Add("TotalEventFirst", PokeathlonGlobalCounters4.MaxStat * (decimal)PokeathlonEvent4.Count,
            () => { var c = Pokeathlon.GlobalCounters; return c.TotalEventFirst; }, null);
        Add("TotalEventLast", PokeathlonGlobalCounters4.MaxStat,
            () => Pokeathlon.GlobalCounters.TotalEventLast, v => { var c = Pokeathlon.GlobalCounters; c.TotalEventLast = v; });
        Add("Fame", PokeathlonGlobalCounters4.MaxFame,
            () => Pokeathlon.GlobalCounters.Fame, v => { var c = Pokeathlon.GlobalCounters; c.Fame = v; });
        FillRows(CountersHost, rows);
        return;

        void Add(string name, decimal maximum, Func<uint> getter, Action<uint>? setter)
        {
            var editor = UiFactory.NumericUpDown($"NUD_{name}", 0, maximum, 140);
            editor.IsEnabled = setter is not null;
            rows.Add(($"L_{name}", name, editor));
            CounterEditors.Add(new CounterBinding(name, editor, getter, setter));
        }
    }

    private void InitializeBestScores()
    {
        List<(string Name, string Text, Control Editor)> rows = [];
        for (int i = 0; i < (int)PokeathlonEvent4.Count; i++)
        {
            var ev = (PokeathlonEvent4)i;
            var editor = UiFactory.NumericUpDown($"NUD_Best{i}", 0, ushort.MaxValue, 140);
            rows.Add(($"L_Best{i}", GetEventDisplayName(ev) + ':', editor));
            BestScoreEditors.Add((ev, editor));
        }
        FillRows(BestHost, rows);
    }

    private void InitializeIndexes()
    {
        CB_CourseIndex.SetItems([.. Enum.GetValues<PokeathlonStat4>().Take((int)PokeathlonStat4.Count).Select(z => new ComboItem(GetCourseDisplayName(z), (int)z))]);
        List<ComboItem> events = [.. Enum.GetValues<PokeathlonEvent4>().Take((int)PokeathlonEvent4.Count).Select(z => new ComboItem(GetEventDisplayName(z), (int)z))];
        CB_SelfEventIndex.SetItems(events);
        CB_ConnectionIndex.SetItems(events);
    }

    private void LoadData()
    {
        IsLoading = true;
        LoadGeneral();
        LoadMedals();
        LoadCounters();
        LoadBestScores();
        CB_CourseIndex.SelectedIndex = 0;
        CB_SelfEventIndex.SelectedIndex = 0;
        CB_ConnectionIndex.SelectedIndex = 0;
        IsLoading = false;

        LoadCourse(0);
        LoadSelfEvent(0);
        LoadConnection(0);
    }

    #endregion

    #region General / medals / counters / best

    private void LoadGeneral()
    {
        NUD_Points.SetValueClamped(Pokeathlon.Points);

        var dailyFlags = Pokeathlon.FlagsDailyShop;
        for (int i = 0; i < DailyShopEditors.Length; i++)
            DailyShopEditors[i].IsChecked = ((dailyFlags >> i) & 1) != 0;

        var dataCardFlags = Pokeathlon.FlagsDataCard;
        for (int i = 0; i < CLB_DataCards.Count; i++)
            CLB_DataCards.SetItemChecked(i, ((dataCardFlags >> i) & 1) != 0);
    }

    private void SaveGeneral()
    {
        Pokeathlon.Points = (uint)(NUD_Points.Value ?? 0);

        ushort dailyFlags = 0;
        for (int i = 0; i < DailyShopEditors.Length; i++)
        {
            if (DailyShopEditors[i].IsChecked == true)
                dailyFlags |= (ushort)(1 << i);
        }
        Pokeathlon.FlagsDailyShop = dailyFlags;

        uint dataCardFlags = 0;
        for (int i = 0; i < CLB_DataCards.Count; i++)
        {
            if (CLB_DataCards.GetItemChecked(i))
                dataCardFlags |= 1u << i;
        }
        Pokeathlon.FlagsDataCard = dataCardFlags;
    }

    private void LoadMedals()
    {
        var medals = Pokeathlon.Medals;
        foreach (var row in MedalRows)
            row.FromBits(medals.GetMedal(row.Species));
    }

    private void SaveMedals()
    {
        var medals = Pokeathlon.Medals;
        foreach (var row in MedalRows)
            medals.SetMedal(row.Species, row.ToBits());
    }

    private void LoadCounters()
    {
        foreach (var binding in CounterEditors)
            binding.Editor.SetValueClamped(binding.Getter());
    }

    private void SaveCounters()
    {
        foreach (var binding in CounterEditors)
            binding.Setter?.Invoke((uint)(binding.Editor.Value ?? 0));
    }

    private void LoadBestScores()
    {
        foreach (var (ev, editor) in BestScoreEditors)
            editor.SetValueClamped(Pokeathlon.GetBestScore(ev));
    }

    private void SaveBestScores()
    {
        foreach (var (ev, editor) in BestScoreEditors)
            Pokeathlon.SetBestScore(ev, (ushort)(editor.Value ?? 0));
    }

    #endregion

    #region Index pages

    private void ChangeCourseIndex()
    {
        if (IsLoading || CB_CourseIndex.GetSelectedItem() is not { } item)
            return;
        if (CurrentCourseIndex >= 0)
            SaveCourse(CurrentCourseIndex);
        LoadCourse(item.Value);
    }

    private void ChangeSelfEventIndex()
    {
        if (IsLoading || CB_SelfEventIndex.GetSelectedItem() is not { } item)
            return;
        if (CurrentSelfEventIndex >= 0)
            SaveSelfEvent(CurrentSelfEventIndex);
        LoadSelfEvent(item.Value);
    }

    private void ChangeConnectionIndex()
    {
        if (IsLoading || CB_ConnectionIndex.GetSelectedItem() is not { } item)
            return;
        if (CurrentConnectionIndex >= 0)
            SaveConnection(CurrentConnectionIndex);
        LoadConnection(item.Value);
    }

    private void LoadCourse(int index)
    {
        IsLoading = true;
        var course = Pokeathlon.GetCourseRecord((PokeathlonStat4)index);
        CourseScoreEditors[0].SetValueClamped(course.Score0);
        CourseScoreEditors[1].SetValueClamped(course.Score1);
        CourseScoreEditors[2].SetValueClamped(course.Score2);
        CourseScoreEditors[3].SetValueClamped(course.ScoreMax);
        for (int i = 0; i < CourseParticipantEditors.Length; i++)
            CourseParticipantEditors[i].LoadObject(course.GetParticipant(i));
        CurrentCourseIndex = index;
        IsLoading = false;
    }

    private void SaveCourse(int index)
    {
        var course = Pokeathlon.GetCourseRecord((PokeathlonStat4)index);
        course.Score0 = (ushort)(CourseScoreEditors[0].Value ?? 0);
        course.Score1 = (ushort)(CourseScoreEditors[1].Value ?? 0);
        course.Score2 = (ushort)(CourseScoreEditors[2].Value ?? 0);
        course.ScoreMax = (ushort)(CourseScoreEditors[3].Value ?? 0);
        for (int i = 0; i < CourseParticipantEditors.Length; i++)
            CourseParticipantEditors[i].SaveObject(course.GetParticipant(i));
    }

    private void LoadSelfEvent(int index)
    {
        IsLoading = true;
        UC_SelfEventData.LoadObject(Pokeathlon.GetEventSelf((PokeathlonEvent4)index));
        CurrentSelfEventIndex = index;
        IsLoading = false;
    }

    private void SaveSelfEvent(int index) => UC_SelfEventData.SaveObject(Pokeathlon.GetEventSelf((PokeathlonEvent4)index));

    private void LoadConnection(int index)
    {
        IsLoading = true;
        UC_Connection.LoadObject(Pokeathlon.GetEventConnection((PokeathlonEvent4)index));
        CurrentConnectionIndex = index;
        IsLoading = false;
    }

    private void SaveConnection(int index) => UC_Connection.SaveObject(Pokeathlon.GetEventConnection((PokeathlonEvent4)index));

    private void SaveCurrentViews()
    {
        if (CurrentCourseIndex >= 0)
            SaveCourse(CurrentCourseIndex);
        if (CurrentSelfEventIndex >= 0)
            SaveSelfEvent(CurrentSelfEventIndex);
        if (CurrentConnectionIndex >= 0)
            SaveConnection(CurrentConnectionIndex);
    }

    #endregion

    protected override void OnSave()
    {
        SaveCurrentViews();
        SaveGeneral();
        SaveMedals();
        SaveCounters();
        SaveBestScores();
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
