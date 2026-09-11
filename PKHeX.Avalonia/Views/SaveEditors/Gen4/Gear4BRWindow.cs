using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4;

/// <summary>
/// Gear unlock editor for Battle Revolution (port of the WinForms <c>SAV_Gear</c>).
/// </summary>
/// <remarks>
/// Every gear piece has one unlock flag per character style, except the badges, whose flags are shared across
/// all styles; those rows are labelled accordingly.
/// </remarks>
public sealed class Gear4BRWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV4BR SAV;

    private readonly string[] CharacterStyles = Translator.GetEnumTranslation<ModelBR>(MainWindow.CurrentLanguage);
    private readonly string[] GearCategories = Translator.GetEnumTranslation<GearCategory>(MainWindow.CurrentLanguage);
    private readonly string[] GearNames = GameLanguage.GetStrings("gear", MainWindow.CurrentLanguage);

    private readonly ObservableCollection<GearRow> Rows = [];
    private readonly CheckBox CHK_Groudon = UiFactory.Check("CHK_Groudon", "Groudon");
    private readonly CheckBox CHK_Lucario = UiFactory.Check("CHK_Lucario", "Lucario");
    private readonly CheckBox CHK_Electivire = UiFactory.Check("CHK_Electivire", "Electivire");
    private readonly CheckBox CHK_Kyogre = UiFactory.Check("CHK_Kyogre", "Kyogre");
    private readonly CheckBox CHK_Roserade = UiFactory.Check("CHK_Roserade", "Roserade");
    private readonly CheckBox CHK_Pachirisu = UiFactory.Check("CHK_Pachirisu", "Pachirisu");

    private sealed class GearRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private bool _obtained;

        public required int Index { get; init; }
        public required string CharacterStyle { get; init; }
        public required string Category { get; init; }
        public required string Gear { get; init; }

        public bool Obtained
        {
            get => _obtained;
            set
            {
                if (_obtained == value)
                    return;
                _obtained = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Obtained)));
            }
        }
    }

    public Gear4BRWindow(SAV4BR sav) : base("SAV_Gear", "Gear Editor")
    {
        SAV = (SAV4BR)(Origin = sav).Clone();

        BuildLayout();
        InitializeRows();

        CHK_Groudon.IsChecked = SAV.GearShinyGroudonOutfit;
        CHK_Lucario.IsChecked = SAV.GearShinyLucarioOutfit;
        CHK_Electivire.IsChecked = SAV.GearShinyElectivireOutfit;
        CHK_Kyogre.IsChecked = SAV.GearShinyKyogreOutfit;
        CHK_Roserade.IsChecked = SAV.GearShinyRoseradeOutfit;
        CHK_Pachirisu.IsChecked = SAV.GearShinyPachirisuOutfit;
    }

    private void BuildLayout()
    {
        var dgv = new DataGrid
        {
            Name = "DGV_Gear",
            ItemsSource = Rows,
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            CanUserReorderColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Width = 700,
            MaxHeight = 520,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        dgv.Columns.Add(new DataGridTextColumn { Header = "Index", Binding = new Binding(nameof(GearRow.Index)), IsReadOnly = true, Width = new DataGridLength(85) });
        dgv.Columns.Add(new DataGridTextColumn { Header = "Character Style", Binding = new Binding(nameof(GearRow.CharacterStyle)), IsReadOnly = true, Width = new DataGridLength(160) });
        dgv.Columns.Add(new DataGridTextColumn { Header = "Category", Binding = new Binding(nameof(GearRow.Category)), IsReadOnly = true, Width = new DataGridLength(120) });
        dgv.Columns.Add(new DataGridTextColumn { Header = "Gear", Binding = new Binding(nameof(GearRow.Gear)), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        dgv.Columns.Add(DataGridUtil.CheckColumn("Obtained", nameof(GearRow.Obtained), 100));

        var unlockAll = UiFactory.Button("B_UnlockAll", "Unlock All Gear");
        var clear = UiFactory.Button("B_Clear", "Reset Gear to Default");
        unlockAll.Click += (_, _) => { SAV.GearUnlock.UnlockAll(); RefreshRows(); };
        clear.Click += (_, _) => { SAV.GearUnlock.Clear(); RefreshRows(); };

        var outfits = new GroupBoxView("GB_ShinyOutfits", "Shiny Outfits", UiFactory.Column(
            CHK_Groudon, CHK_Lucario, CHK_Electivire, CHK_Kyogre, CHK_Roserade, CHK_Pachirisu));

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(UiFactory.Column(dgv, UiFactory.Row(unlockAll, clear)));
        body.Children.Add(outfits);
        SetBody(body);
    }

    private void InitializeRows()
    {
        for (ModelBR model = ModelBR.YoungBoy; model <= ModelBR.LittleGirl; model++)
        {
            for (GearCategory category = 0; (int)category < GearUnlock.CategoryCount; category++)
            {
                var (offset, count) = GearUnlock.GetOffsetCount(model, category);
                for (int i = 0; i < count; i++)
                {
                    // The unlock flags for badges are shared, so those rows say so instead of naming one style.
                    bool shared = category is GearCategory.Badges && i != 0;
                    var index = offset + i;
                    Rows.Add(new GearRow
                    {
                        Index = index,
                        CharacterStyle = shared ? MessageStrings.MsgGearAllCharacterStyles : CharacterStyles[(int)model],
                        Category = GearCategories[(int)category],
                        Gear = GearNames[index],
                        Obtained = SAV.GearUnlock.Get(index),
                    });
                }
            }
        }
    }

    private void RefreshRows()
    {
        foreach (var row in Rows)
            row.Obtained = SAV.GearUnlock.Get(row.Index);
    }

    protected override void OnSave()
    {
        foreach (var row in Rows)
            SAV.GearUnlock.Set(row.Index, row.Obtained);

        SAV.GearShinyGroudonOutfit = CHK_Groudon.IsChecked == true;
        SAV.GearShinyLucarioOutfit = CHK_Lucario.IsChecked == true;
        SAV.GearShinyElectivireOutfit = CHK_Electivire.IsChecked == true;
        SAV.GearShinyKyogreOutfit = CHK_Kyogre.IsChecked == true;
        SAV.GearShinyRoseradeOutfit = CHK_Roserade.IsChecked == true;
        SAV.GearShinyPachirisuOutfit = CHK_Pachirisu.IsChecked == true;

        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
