using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4;

/// <summary>
/// Geonet map editor for Gen 4 (port of the WinForms <c>SAV_Geonet4</c>).
/// </summary>
public sealed class Geonet4Window : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV4 SAV;
    private readonly Geonet4 Geonet;

    private readonly List<ComboItem> countryList;
    private readonly List<ComboItem> subregionListDefault;
    private readonly List<ComboItem> pointList;

    private readonly ObservableCollection<GeonetRow> Rows = [];
    private readonly DataGrid DGV_Geonet = new() { AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column, CanUserSortColumns = true, IsReadOnly = false };
    private readonly CheckBox CHK_GlobalFlag = UiFactory.Check("CHK_GlobalFlag", "Whole Globe Visible");
    private readonly Button B_SetAllLocations = UiFactory.Button("B_SetAllLocations", "Set All Locations");
    private readonly Button B_SetAllLegalLocations = UiFactory.Button("B_SetAllLegalLocations", "Set All Legal Locations");
    private readonly Button B_ClearLocations = UiFactory.Button("B_ClearLocations", "Clear Locations");

    public Geonet4Window(SAV4 sav) : base("SAV_Geonet4", "Geonet Editor")
    {
        SAV = (SAV4)(Origin = sav).Clone();
        Geonet = new Geonet4(SAV);
        SizeToContent = SizeToContent.Manual;
        CanResize = true;
        Width = 620;
        Height = 560;

        countryList = Util.GetCountryRegionList("gen4_countries", MainWindow.CurrentLanguage);
        subregionListDefault = Util.GetCountryRegionList("gen4_sr_default", MainWindow.CurrentLanguage);
        pointList = Util.GetGeonetPointList();

        DGV_Geonet.Columns.Add(new DataGridTextColumn { Header = "Country", Binding = new Binding(nameof(GeonetRow.CountryName)), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        DGV_Geonet.Columns.Add(new DataGridTextColumn { Header = "Subregion", Binding = new Binding(nameof(GeonetRow.SubregionName)), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        DGV_Geonet.Columns.Add(DataGridUtil.ComboColumn("Point", pointList, nameof(GeonetRow.Point), 130));
        DGV_Geonet.ItemsSource = Rows;

        var body = new DockPanel();
        var buttons = UiFactory.Row(B_SetAllLocations, B_SetAllLegalLocations, B_ClearLocations);
        DockPanel.SetDock(buttons, Dock.Top);
        DockPanel.SetDock(CHK_GlobalFlag, Dock.Bottom);
        body.Children.Add(buttons);
        body.Children.Add(CHK_GlobalFlag);
        body.Children.Add(DGV_Geonet);
        SetBody(body);

        B_SetAllLocations.Click += (_, _) => { Geonet.SetAll(); Reload(); };
        B_SetAllLegalLocations.Click += (_, _) => { Geonet.SetAllLegal(); Reload(); };
        B_ClearLocations.Click += (_, _) => { Geonet.ClearAll(); Reload(); };

        InitializeGeonet();
        CHK_GlobalFlag.IsChecked = Geonet.GlobalFlag;
    }

    private void Reload()
    {
        InitializeGeonet();
        CHK_GlobalFlag.IsChecked = Geonet.GlobalFlag;
    }

    private void InitializeGeonet()
    {
        Rows.Clear();
        for (int i = 1; i <= LocaleNDS4.CountryCount; i++)
        {
            var country = countryList[i].Value;
            var countryName = countryList[i].Text;
            var subregionCount = LocaleNDS4.GetSubregionCount((byte)country);
            var subregionList = subregionCount == 0 ? subregionListDefault : Util.GetCountryRegionList($"gen4_sr_{country:000}", MainWindow.CurrentLanguage);
            if (subregionCount == 0)
            {
                AddRow(country, subregionList[0].Value, countryName, subregionList[0].Text);
            }
            for (int j = 1; j <= subregionCount; j++)
                AddRow(country, subregionList[j].Value, countryName, subregionList[j].Text);
        }
    }

    private void AddRow(int country, int subregion, string countryName, string subregionName)
    {
        var point = Geonet.GetCountrySubregion((byte)country, (byte)subregion);
        Rows.Add(new GeonetRow
        {
            Country = country,
            CountryName = countryName,
            Subregion = subregion,
            SubregionName = subregionName,
            Point = (int)point,
        });
    }

    protected override void OnSave()
    {
        Geonet.ClearAll();
        foreach (var row in Rows)
        {
            if (row.Country > 0)
                Geonet.SetCountrySubregion((byte)row.Country, (byte)row.Subregion, (GeonetPoint)row.Point);
        }
        Geonet.SetSAVCountry();
        Geonet.Save();

        Geonet.GlobalFlag = CHK_GlobalFlag.IsChecked == true;
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
}
