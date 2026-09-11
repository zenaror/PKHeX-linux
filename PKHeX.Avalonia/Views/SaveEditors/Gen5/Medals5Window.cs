using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen5;

/// <summary>
/// Medal and Habitat List editor for Black 2 / White 2 (port of the WinForms <c>SAV_Medals5</c>).
/// </summary>
/// <remarks>
/// Each medal has a state, an unread flag and, for the ones that record it, the date it was earned.
/// The Habitat tab tracks which encounter types were completed per habitat entry.
/// </remarks>
public sealed class Medals5Window : SaveEditorWindow
{
    private const string MedalListFilter = "Medal List 5|*.ml5";
    private const string DateFormat = "yyyy-MM-dd";

    private readonly SAV5B2W2 Origin;
    private readonly SAV5B2W2 SAV;
    private readonly MedalList5 Medals;
    private readonly HabitatList5 Habitat;

    private readonly string[] MedalNames;
    private readonly string[] MedalTypeNames;
    private readonly string[] MedalStateNames;
    private readonly string[] MedalRankNames;
    private readonly string[] HabitatCompletionNames;
    private readonly string[] HabitatEncounterTypeNames;

    private readonly ObservableCollection<MedalRow> MedalRows = [];
    private readonly ObservableCollection<HabitatRow> HabitatRows = [];
    private readonly DataGrid DGV_Medals = new() { Name = "DGV_Medals", AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column, CanUserSortColumns = false, Height = 400, Width = 700 };
    private readonly DataGrid DGV_Habitat = new() { Name = "DGV_Habitat", AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column, CanUserSortColumns = false, Height = 360, Width = 700 };

    private readonly ComboBox CB_PinnedMedal = UiFactory.Combo("CB_PinnedMedal", 240);
    private readonly ComboBox CB_Rank = UiFactory.Combo("CB_Rank", 160);
    private readonly TextBlock L_Rank = UiFactory.Label("L_Rank", "Rank:", clickable: true);
    private readonly CheckBox CHK_TutorialComplete = UiFactory.Check("CHK_TutorialComplete", "Tutorial Complete");
    private readonly Button B_GiveAll = UiFactory.Button("B_GiveAll", "Give All");
    private readonly Button B_ImportAll = UiFactory.Button("B_ImportAll", "Import All");
    private readonly Button B_ExportAll = UiFactory.Button("B_ExportAll", "Export All");

    private readonly CheckBox CHK_HabitatTutorialViewed = UiFactory.Check("CHK_HabitatTutorialViewed", "Tutorial Viewed");
    private readonly CheckBox CHK_HabitatTutorialCompleteCapture = UiFactory.Check("CHK_HabitatTutorialCompleteCapture", "Tutorial Capture Done");
    private readonly NumericUpDown NUD_Unknown90 = UiFactory.NumericUpDown("NUD_Unknown90", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_Unknown92 = UiFactory.NumericUpDown("NUD_Unknown92", 0, byte.MaxValue, 110);
    private readonly ComboBox CB_LastEncounterType = UiFactory.StringCombo("CB_LastEncounterType", 160);
    private readonly Button B_HabitatSetComplete = UiFactory.Button("B_HabitatSetComplete", "Set Complete");
    private readonly Button B_HabitatClear = UiFactory.Button("B_HabitatClear", "Clear");

    public Medals5Window(SAV5B2W2 sav) : base("SAV_Medals5", "Medals")
    {
        Origin = sav;
        SAV = (SAV5B2W2)sav.Clone();
        Medals = SAV.Medals;
        Habitat = Medals.HabitatList;

        var lang = MainWindow.CurrentLanguage;
        MedalNames = Util.GetStringList("medals", lang);
        MedalTypeNames = Util.GetStringList("medal_types", lang);
        MedalStateNames = Translator.GetEnumTranslation<MedalState5>(lang);
        MedalRankNames = Translator.GetEnumTranslation<MedalRank5>(lang);
        HabitatCompletionNames = Translator.GetEnumTranslation<HabitatCompletion5>(lang);
        HabitatEncounterTypeNames = Translator.GetEnumTranslation<HabitatEncounterType5>(lang);

        BuildLayout();
        LoadMedalData();
        LoadHabitatData();
        LoadHabitatSettings();
    }

    private void BuildLayout()
    {
        DGV_Medals.Columns.Add(new DataGridTextColumn { Header = "#", Binding = new Binding(nameof(MedalRow.Index)), IsReadOnly = true, Width = new DataGridLength(50) });
        DGV_Medals.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(MedalRow.Name)), IsReadOnly = true, Width = new DataGridLength(220) });
        DGV_Medals.Columns.Add(new DataGridTextColumn { Header = "Type", Binding = new Binding(nameof(MedalRow.Type)), IsReadOnly = true, Width = new DataGridLength(110) });
        DGV_Medals.Columns.Add(DataGridUtil.StringComboColumn("State", MedalStateNames, nameof(MedalRow.State), 130));
        DGV_Medals.Columns.Add(DataGridUtil.CheckColumn("Unread", nameof(MedalRow.Unread), 90));
        DGV_Medals.Columns.Add(new DataGridTextColumn { Header = "Date", Binding = new Binding(nameof(MedalRow.Date)) { Mode = BindingMode.TwoWay }, Width = new DataGridLength(110) });
        DGV_Medals.ItemsSource = MedalRows;

        DGV_Habitat.Columns.Add(new DataGridTextColumn { Header = "#", Binding = new Binding(nameof(HabitatRow.Index)), IsReadOnly = true, Width = new DataGridLength(50) });
        DGV_Habitat.Columns.Add(DataGridUtil.CheckColumn("Complete", nameof(HabitatRow.Complete), 90));
        DGV_Habitat.Columns.Add(DataGridUtil.StringComboColumn("Grass", HabitatCompletionNames, nameof(HabitatRow.Grass), 170));
        DGV_Habitat.Columns.Add(DataGridUtil.StringComboColumn("Surf", HabitatCompletionNames, nameof(HabitatRow.Surf), 170));
        DGV_Habitat.Columns.Add(DataGridUtil.StringComboColumn("Fish", HabitatCompletionNames, nameof(HabitatRow.Fish), 170));
        DGV_Habitat.ItemsSource = HabitatRows;

        var medalItems = MedalNames.Select((z, i) => new ComboItem(z, i)).ToList();
        medalItems.Insert(0, new ComboItem(GameInfo.Strings.specieslist[0], MedalList5.PinnedMedalNone));
        CB_PinnedMedal.SetItems(medalItems);

        var rankValues = Enum.GetValues<MedalRank5>();
        var rankItems = new ComboItem[rankValues.Length];
        for (int i = 0; i < rankItems.Length; i++)
            rankItems[i] = new ComboItem(MedalRankNames[i], (int)rankValues[i]);
        CB_Rank.SetItems(rankItems);

        CB_LastEncounterType.Items.Clear();
        foreach (var name in HabitatEncounterTypeNames)
            CB_LastEncounterType.Items.Add(name);

        var medalTop = UiFactory.Row(
            UiFactory.Label("L_PinnedMedal", "Pinned Medal:"), CB_PinnedMedal,
            L_Rank, CB_Rank,
            CHK_TutorialComplete);
        var medalTab = new TabItem
        {
            Name = "Tab_Medals",
            Header = "Medals",
            Content = UiFactory.Column(medalTop, UiFactory.Row(B_GiveAll, B_ImportAll, B_ExportAll), DGV_Medals),
        };

        var habitatBottom = UiFactory.Column(
            UiFactory.Row(CHK_HabitatTutorialViewed, CHK_HabitatTutorialCompleteCapture),
            UiFactory.Row(UiFactory.Label("L_Unknown90", "Unknown90:"), NUD_Unknown90, UiFactory.Label("L_Unknown92", "Unknown92:"), NUD_Unknown92),
            UiFactory.Row(UiFactory.Label("L_LastEncounterType", "Last Encounter Type:"), CB_LastEncounterType),
            UiFactory.Row(B_HabitatSetComplete, B_HabitatClear));
        var habitatTab = new TabItem
        {
            Name = "Tab_Habitat",
            Header = "Habitat",
            Content = UiFactory.Column(DGV_Habitat, habitatBottom),
        };

        var tabs = new TabControl();
        tabs.Items.Add(medalTab);
        tabs.Items.Add(habitatTab);
        SetBody(tabs);

        // Clicking the rank label recalculates the rank from the medals, as in WinForms.
        L_Rank.AttachClick(_ => CB_Rank.SetValue((int)Medals.CalculateRank()));
        B_GiveAll.Click += (_, _) =>
        {
            Medals.GiveAll(EncounterDate.GetDateNDS(), unread: true);
            LoadMedalData();
        };
        B_ImportAll.Click += async (_, _) => await ImportAll();
        B_ExportAll.Click += async (_, _) => await ExportAll();
        B_HabitatSetComplete.Click += (_, _) => SetHabitatComplete();
        B_HabitatClear.Click += (_, _) => ClearHabitat();
    }

    #region Load / save

    private void LoadMedalData()
    {
        MedalRows.Clear();
        for (int i = 0; i < MedalNames.Length; i++)
        {
            var medal = Medals[i];
            MedalRows.Add(new MedalRow(this, i)
            {
                Index = i.ToString(),
                Name = MedalNames[i],
                Type = MedalTypeNames[(int)MedalList5.GetMedalType(i)],
                StateValue = MedalStateNames[(int)medal.State],
                UnreadValue = medal.IsUnread,
                DateValue = GetDisplayedDate(medal),
            });
        }

        CB_PinnedMedal.SetValue(Medals.PinnedMedal);
        CB_Rank.SetValue((int)Medals.Rank);
        CHK_TutorialComplete.IsChecked = Medals.IsTutorialComplete;
    }

    private void LoadHabitatData()
    {
        HabitatRows.Clear();
        for (int i = 0; i < HabitatList5.HabitatCount; i++)
        {
            var habitat = Habitat.GetHabitat(i);
            HabitatRows.Add(new HabitatRow(this, i)
            {
                Index = i.ToString(),
                CompleteValue = habitat.IsComplete,
                GrassValue = HabitatCompletionNames[(int)habitat.GetStatus(HabitatEncounterType5.Grass)],
                SurfValue = HabitatCompletionNames[(int)habitat.GetStatus(HabitatEncounterType5.Surf)],
                FishValue = HabitatCompletionNames[(int)habitat.GetStatus(HabitatEncounterType5.Fish)],
            });
        }
    }

    private void LoadHabitatSettings()
    {
        CHK_HabitatTutorialViewed.IsChecked = Habitat.IsTutorialViewed;
        CHK_HabitatTutorialCompleteCapture.IsChecked = Habitat.IsTutorialCompleteCapture;
        NUD_Unknown90.Value = Habitat.Unknown90;
        NUD_Unknown92.Value = Habitat.Unknown92;
        CB_LastEncounterType.SelectedIndex = (int)Habitat.LastEncounterType;
    }

    private void SaveHabitatSettings()
    {
        Habitat.IsTutorialViewed = CHK_HabitatTutorialViewed.IsChecked == true;
        Habitat.IsTutorialCompleteCapture = CHK_HabitatTutorialCompleteCapture.IsChecked == true;
        Habitat.Unknown90 = (ushort)(NUD_Unknown90.Value ?? 0);
        Habitat.Unknown92 = (byte)(NUD_Unknown92.Value ?? 0);
        if (CB_LastEncounterType.SelectedIndex >= 0)
            Habitat.LastEncounterType = (HabitatEncounterType5)CB_LastEncounterType.SelectedIndex;
    }

    #endregion

    #region Commands

    private void SetHabitatComplete()
    {
        var selected = DGV_Habitat.SelectedItems.OfType<HabitatRow>().ToArray();
        if (selected.Length == 0)
            Habitat.CompleteAll();
        else
            foreach (var row in selected)
                Habitat.GetHabitat(row.Number).SetComplete();
        LoadHabitatData();
    }

    private void ClearHabitat()
    {
        foreach (var row in DGV_Habitat.SelectedItems.OfType<HabitatRow>().ToArray())
            Habitat.GetHabitat(row.Number).Clear();
        LoadHabitatData();
    }

    private async Task ImportAll()
    {
        var path = await FileDialogs.OpenSingleFile(this, MedalListFilter);
        if (path is null)
            return;
        var fi = new FileInfo(path);
        if (fi.Length != MedalList5.LengthAllMedals)
        {
            await AppDialogs.Alert(this, string.Format(MessageStrings.MsgFileSizeIncorrect, fi.Length, MedalList5.LengthAllMedals));
            return;
        }
        var data = await File.ReadAllBytesAsync(path);
        data.AsSpan().CopyTo(Medals.AllMedals);
        LoadMedalData();
    }

    private async Task ExportAll()
    {
        var path = await FileDialogs.SaveFileDialog(this, MedalListFilter, GetDefaultFileName());
        if (path is null)
            return;
        await File.WriteAllBytesAsync(path, Medals.AllMedals.ToArray());
    }

    private string GetDefaultFileName() => PathUtil.CleanFileName($"{SAV.OT} {SAV.Version}.ml5");

    #endregion

    private static string GetDisplayedDate(Medal5 medal) => medal is { CanHaveDate: true, HasDate: true }
        ? medal.Date.ToString(DateFormat, CultureInfo.InvariantCulture)
        : string.Empty;

    /// <summary>Accepts the culture's own date format as well as the fixed one the grid displays.</summary>
    private static bool TryParseDate(string text, out DateOnly date)
    {
        text = text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            date = default;
            return false;
        }
        if (!DateOnly.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out date) &&
            !DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return false;
        }
        return EncounterDate.IsValidDateNDS(date);
    }

    protected override void OnSave()
    {
        Medals.PinnedMedal = (byte)(CB_PinnedMedal.GetSelectedItem()?.Value ?? MedalList5.PinnedMedalNone);
        Medals.Rank = (MedalRank5)(CB_Rank.GetSelectedItem()?.Value ?? 0);
        Medals.IsTutorialComplete = CHK_TutorialComplete.IsChecked == true;

        SaveHabitatSettings();
        Origin.CopyChangesFrom(SAV);
        Close();
    }

    /// <summary>One medal row; every edit writes straight into the save block.</summary>
    private sealed class MedalRow(Medals5Window owner, int number) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Number => number;
        public required string Index { get; init; }
        public required string Name { get; init; }
        public required string Type { get; init; }

        public required string StateValue { private get; init; }
        public required bool UnreadValue { private get; init; }
        public required string DateValue { private get; init; }

        private string? state;
        private bool? unread;
        private string? date;

        public string State
        {
            get => state ?? StateValue;
            set
            {
                if (State == value)
                    return;
                state = value;
                var medal = owner.Medals[number];
                var index = Array.IndexOf(owner.MedalStateNames, value);
                if (index >= 0)
                    medal.State = (MedalState5)index;
                if (medal is { CanHaveDate: true, HasDate: false })
                    medal.Date = EncounterDate.GetDateNDS();
                date = GetDisplayedDate(medal);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Date)));
            }
        }

        public bool Unread
        {
            get => unread ?? UnreadValue;
            set
            {
                unread = value;
                var medal = owner.Medals[number]; // struct over the save buffer; writes go through
                medal.IsUnread = value;
            }
        }

        public string Date
        {
            get => date ?? DateValue;
            set
            {
                var medal = owner.Medals[number];
                if (!medal.CanHaveDate || !TryParseDate(value, out var parsed))
                    return; // keep the previous value, like the WinForms validation
                medal.Date = parsed;
                date = parsed.ToString(DateFormat, CultureInfo.InvariantCulture);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Date)));
            }
        }
    }

    /// <summary>One habitat row; every edit writes straight into the save block.</summary>
    private sealed class HabitatRow(Medals5Window owner, int number) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Number => number;
        public required string Index { get; init; }
        public required bool CompleteValue { private get; init; }
        public required string GrassValue { private get; init; }
        public required string SurfValue { private get; init; }
        public required string FishValue { private get; init; }

        private bool? complete;
        private string? grass, surf, fish;

        public bool Complete
        {
            get => complete ?? CompleteValue;
            set
            {
                complete = value;
                var habitat = owner.Habitat.GetHabitat(number); // struct over the save buffer; writes go through
                habitat.IsComplete = value;
            }
        }

        public string Grass
        {
            get => grass ?? GrassValue;
            set { grass = value; SetStatus(HabitatEncounterType5.Grass, value); }
        }

        public string Surf
        {
            get => surf ?? SurfValue;
            set { surf = value; SetStatus(HabitatEncounterType5.Surf, value); }
        }

        public string Fish
        {
            get => fish ?? FishValue;
            set { fish = value; SetStatus(HabitatEncounterType5.Fish, value); }
        }

        private void SetStatus(HabitatEncounterType5 type, string text)
        {
            var index = Array.IndexOf(owner.HabitatCompletionNames, text);
            if (index >= 0)
                owner.Habitat.GetHabitat(number).SetStatus(type, (HabitatCompletion5)index);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Complete)));
        }
    }
}
