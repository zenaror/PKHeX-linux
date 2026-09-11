using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen5;

/// <summary>
/// Unity Tower / Geonet editor (port of the WinForms <c>SAV_UnityTower</c>).
/// </summary>
public sealed class UnityTowerWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV5 SAV;
    private readonly UnityTower5 UnityTower;

    private readonly List<ComboItem> countryList;
    private readonly List<ComboItem> subregionListDefault;
    private readonly List<ComboItem> pointList;

    private readonly ObservableCollection<GeonetRow> GeonetRows = [];
    private readonly ObservableCollection<FloorRow> FloorRows = [];
    private readonly DataGrid DGV_Geonet = new() { AutoGenerateColumns = false, CanUserSortColumns = true, HeadersVisibility = DataGridHeadersVisibility.Column, MinWidth = 420 };
    private readonly DataGrid DGV_UnityTower = new() { AutoGenerateColumns = false, CanUserSortColumns = true, HeadersVisibility = DataGridHeadersVisibility.Column, MinWidth = 260 };
    private readonly CheckBox CHK_GlobalFlag = UiFactory.Check("CHK_GlobalFlag", "Whole Globe Visible");
    private readonly CheckBox CHK_UnityTowerFlag = UiFactory.Check("CHK_UnityTowerFlag", "Unity Tower Unlocked");
    private readonly Button B_SetAllLocations = UiFactory.Button("B_SetAllLocations", "Set All Locations");
    private readonly Button B_SetAllLegalLocations = UiFactory.Button("B_SetAllLegalLocations", "Set All Legal Locations");
    private readonly Button B_ClearLocations = UiFactory.Button("B_ClearLocations", "Clear Locations");

    public UnityTowerWindow(SAV5 sav) : base("SAV_UnityTower", "Unity Tower Editor")
    {
        SAV = (SAV5)(Origin = sav).Clone();
        UnityTower = SAV.UnityTower;
        SizeToContent = SizeToContent.Manual;
        CanResize = true;
        Width = 760;
        Height = 560;

        countryList = Util.GetCountryRegionList("gen5_countries", MainWindow.CurrentLanguage);
        subregionListDefault = Util.GetCountryRegionList("gen5_sr_default", MainWindow.CurrentLanguage);
        pointList = Util.GetGeonetPointList();

        DGV_Geonet.Columns.Add(new DataGridTextColumn { Header = "Country", Binding = new Binding(nameof(GeonetRow.CountryName)), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        DGV_Geonet.Columns.Add(new DataGridTextColumn { Header = "Subregion", Binding = new Binding(nameof(GeonetRow.SubregionName)), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        DGV_Geonet.Columns.Add(DataGridUtil.ComboColumn("Point", pointList, nameof(GeonetRow.Point), 130));
        DGV_Geonet.ItemsSource = GeonetRows;

        DGV_UnityTower.Columns.Add(DataGridUtil.CheckColumn("Floor", nameof(FloorRow.Unlocked), 60));
        DGV_UnityTower.Columns.Add(new DataGridTextColumn { Header = "Country", Binding = new Binding(nameof(FloorRow.CountryName)), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        DGV_UnityTower.ItemsSource = FloorRows;
        DGV_UnityTower.IsReadOnly = false;

        var buttons = UiFactory.Row(B_SetAllLocations, B_SetAllLegalLocations, B_ClearLocations);
        var flags = UiFactory.Row(CHK_GlobalFlag, CHK_UnityTowerFlag);
        var grids = new Grid { ColumnSpacing = 8 };
        grids.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
        grids.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        Grid.SetColumn(DGV_UnityTower, 1);
        grids.Children.Add(DGV_Geonet);
        grids.Children.Add(DGV_UnityTower);

        var body = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Top);
        DockPanel.SetDock(flags, Dock.Bottom);
        body.Children.Add(buttons);
        body.Children.Add(flags);
        body.Children.Add(grids);
        SetBody(body);

        B_SetAllLocations.Click += (_, _) => { UnityTower.SetAll(); Reload(); };
        B_SetAllLegalLocations.Click += (_, _) => { UnityTower.SetAllLegal(); Reload(); };
        B_ClearLocations.Click += (_, _) => { UnityTower.ClearAll(); Reload(); };

        InitializeDGVGeonet();
        InitializeDGVUnityTower();
        CHK_GlobalFlag.IsChecked = UnityTower.GlobalFlag;
        CHK_UnityTowerFlag.IsChecked = UnityTower.UnityTowerFlag;
    }

    private void Reload()
    {
        InitializeDGVGeonet();
        InitializeDGVUnityTower();
        CHK_GlobalFlag.IsChecked = UnityTower.GlobalFlag;
        CHK_UnityTowerFlag.IsChecked = UnityTower.UnityTowerFlag;
    }

    private void InitializeDGVGeonet()
    {
        GeonetRows.Clear();

        for (int i = 1; i <= LocaleNDS5.CountryCount; i++)
        {
            var country = countryList[i].Value;
            var countryName = countryList[i].Text;
            var subregionCount = UnityTower5.GetSubregionCount((byte)country);
            var subregionList = subregionCount == 0 ? subregionListDefault : Util.GetCountryRegionList($"gen5_sr_{country:000}", MainWindow.CurrentLanguage);
            if (subregionCount == 0)
            {
                var subregion = subregionList[0].Value;
                var subregionName = subregionList[0].Text;
                AddCountrySubregionRow(country, subregion, countryName, subregionName);
            }
            for (int j = 1; j <= subregionCount; j++)
            {
                var subregion = subregionList[j].Value;
                var subregionName = subregionList[j].Text;
                AddCountrySubregionRow(country, subregion, countryName, subregionName);
            }
        }
    }

    private void AddCountrySubregionRow(int country, int subregion, string countryName, string subregionName)
    {
        var point = UnityTower.GetCountrySubregion((byte)country, (byte)subregion);
        GeonetRows.Add(new GeonetRow
        {
            Country = country,
            CountryName = countryName,
            Subregion = subregion,
            SubregionName = subregionName,
            Point = (int)point,
        });
    }

    private void InitializeDGVUnityTower()
    {
        FloorRows.Clear();
        for (int i = 0; i < LocaleNDS5.CountryCount; i++)
        {
            var country = countryList[i + 1].Value;
            var countryName = countryList[i + 1].Text;
            FloorRows.Add(new FloorRow
            {
                Unlocked = UnityTower.GetUnityTowerFloor((byte)country),
                Country = country,
                CountryName = countryName,
            });
        }
    }

    protected override void OnSave()
    {
        UnityTower.ClearAll();
        foreach (var row in GeonetRows)
        {
            if (row.Country > 0)
                UnityTower.SetCountrySubregion((byte)row.Country, (byte)row.Subregion, (GeonetPoint)row.Point);
        }
        foreach (var row in FloorRows)
            UnityTower.SetUnityTowerFloor((byte)row.Country, row.Unlocked);
        UnityTower.SetSAVCountry();

        UnityTower.GlobalFlag = CHK_GlobalFlag.IsChecked == true;
        UnityTower.UnityTowerFlag = CHK_UnityTowerFlag.IsChecked == true;
        Origin.CopyChangesFrom(SAV);
        Close();
    }

    private sealed class GeonetRow
    {
        public int Country { get; init; }
        public string CountryName { get; init; } = string.Empty;
        public int Subregion { get; init; }
        public string SubregionName { get; init; } = string.Empty;
        public int Point { get; set; }
    }

    private sealed class FloorRow
    {
        public bool Unlocked { get; set; }
        public int Country { get; init; }
        public string CountryName { get; init; } = string.Empty;
    }
}
