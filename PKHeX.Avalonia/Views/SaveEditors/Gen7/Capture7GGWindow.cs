using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen7;

/// <summary>
/// Capture record editor for Let's Go Pikachu / Eevee (port of the WinForms <c>SAV_Capture7GG</c>).
/// </summary>
public sealed class Capture7GGWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV7b SAV;
    private readonly Zukan7b Dex;
    private readonly CaptureRecords Captured;

    private ushort Index;
    private bool Loading;

    private readonly ListBox LB_Species = new() { Name = "LB_Species", Width = 220, Height = 380 };
    private readonly ObservableCollection<string> SpeciesItems = [];
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);

    private readonly NumericUpDown NUD_SpeciesCaptured = UiFactory.NumericUpDown("NUD_SpeciesCaptured", 0, 9999, 110);
    private readonly NumericUpDown NUD_SpeciesTransferred = UiFactory.NumericUpDown("NUD_SpeciesTransferred", 0, 999999999, 110);
    private readonly NumericUpDown NUD_TotalCaptured = UiFactory.NumericUpDown("NUD_TotalCaptured", 0, 999999999, 110);
    private readonly NumericUpDown NUD_TotalTransferred = UiFactory.NumericUpDown("NUD_TotalTransferred", 0, 999999999, 110);

    private readonly TextBlock L_SpeciesCaptured = UiFactory.Label("L_SpeciesCaptured", "Captured", true);
    private readonly TextBlock L_SpeciesTransferred = UiFactory.Label("L_SpeciesTransferred", "Transferred", true);
    private readonly TextBlock L_TotalCaptured = UiFactory.Label("L_TotalCaptured", "Captured", true);
    private readonly TextBlock L_TotalTransferred = UiFactory.Label("L_TotalTransferred", "Transferred", true);

    private readonly Button B_Modify = UiFactory.Button("B_Modify", "Set All");
    private readonly Button B_SumTotal = UiFactory.Button("B_SumTotal", "Σ");

    public Capture7GGWindow(SAV7b sav) : base("SAV_Capture7GG", "Capture Record Editor")
    {
        SAV = (SAV7b)(Origin = sav).Clone();
        Dex = SAV.Blocks.Zukan;
        Captured = SAV.Blocks.Captured;

        Loading = true;
        LB_Species.ItemsSource = SpeciesItems;

        var species = GameInfo.FilteredSources.Species.Where(z => IsLegalSpecies(z.Value)).ToList();
        CB_Species.SetItems(species);
        CB_Species.SelectedIndex = 0; // the WinForms binding shows the first entry; the list handler is suppressed while loading
        foreach (var (text, value) in species.OrderBy(z => z.Value))
            SpeciesItems.Add($"{value:000}: {text}");

        BuildLayout();

        GetTotals();
        LB_Species.SelectedIndex = Index = 0;
        GetEntry();
        Loading = false;
    }

    private static bool IsLegalSpecies(int species) => species is >= 1 and (<= 151 or 808 or 809);

    private void BuildLayout()
    {
        var speciesGrid = UiFactory.FormGrid(2);
        UiFactory.AddFormRow(speciesGrid, 0, L_SpeciesCaptured, NUD_SpeciesCaptured);
        UiFactory.AddFormRow(speciesGrid, 1, L_SpeciesTransferred, NUD_SpeciesTransferred);
        var gbSpecies = new GroupBoxView("GB_Species", "Species Info", UiFactory.Column(speciesGrid, B_Modify));

        var totalGrid = UiFactory.FormGrid(2);
        UiFactory.AddFormRow(totalGrid, 0, L_TotalCaptured, NUD_TotalCaptured);
        UiFactory.AddFormRow(totalGrid, 1, L_TotalTransferred, NUD_TotalTransferred);
        var gbTotal = new GroupBoxView("GB_Total", "Totals", UiFactory.Column(totalGrid, B_SumTotal));

        var right = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_goto", "goto:"), CB_Species),
            gbSpecies,
            gbTotal);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(LB_Species);
        body.Children.Add(right);
        SetBody(body);

        LB_Species.SelectionChanged += (_, _) => ChangeLBSpecies();
        CB_Species.SelectionChanged += (_, _) => ChangeCBSpecies();
        B_Modify.Click += (_, _) => ClickModify();
        B_SumTotal.Click += (_, _) => ClickSumTotal();
        L_SpeciesCaptured.AttachClick(_ => ToggleMax(NUD_SpeciesCaptured));
        L_SpeciesTransferred.AttachClick(_ => ToggleMax(NUD_SpeciesTransferred));
        L_TotalCaptured.AttachClick(_ => ToggleMax(NUD_TotalCaptured));
        L_TotalTransferred.AttachClick(_ => ToggleMax(NUD_TotalTransferred));
    }

    private void ChangeCBSpecies()
    {
        if (Loading || CB_Species.GetSelectedItem() is not { } item)
            return;
        SetEntry();

        Index = CaptureRecords.GetSpeciesIndex((ushort)item.Value);
        Loading = true;
        LB_Species.SelectedIndex = Index;
        LB_Species.ScrollIntoView(Index);
        GetEntry();
        Loading = false;
    }

    private void ChangeLBSpecies()
    {
        if (Loading || LB_Species.SelectedIndex < 0)
            return;
        SetEntry();

        Index = (ushort)LB_Species.SelectedIndex;
        Loading = true;
        CB_Species.SetValue(CaptureRecords.GetIndexSpecies(Index));
        GetEntry();
        Loading = false;
    }

    private void GetEntry()
    {
        var index = Index;
        if (index > CaptureRecords.MaxIndex)
            return;
        NUD_SpeciesCaptured.SetValueClamped(Captured.GetCapturedCountIndex(index));
        NUD_SpeciesTransferred.SetValueClamped(Captured.GetTransferredCountIndex(index));
    }

    private void SetEntry()
    {
        var index = Index;
        if (index > CaptureRecords.MaxIndex)
            return;
        Captured.SetCapturedCountIndex(index, (uint)(NUD_SpeciesCaptured.Value ?? 0));
        Captured.SetTransferredCountIndex(index, (uint)(NUD_SpeciesTransferred.Value ?? 0));
    }

    private void GetTotals()
    {
        NUD_TotalCaptured.SetValueClamped(Captured.TotalCaptured);
        NUD_TotalTransferred.SetValueClamped(Captured.TotalTransferred);
    }

    private void SetTotals()
    {
        Captured.TotalCaptured = (uint)(NUD_TotalCaptured.Value ?? 0);
        Captured.TotalTransferred = (uint)(NUD_TotalTransferred.Value ?? 0);
    }

    private void ClickModify()
    {
        SetTotals();
        Captured.SetAllCaptured((uint)(NUD_SpeciesCaptured.Value ?? 0), Dex);
        Captured.SetAllTransferred((uint)(NUD_SpeciesTransferred.Value ?? 0), Dex);
        GetEntry();
    }

    private void ClickSumTotal()
    {
        SetEntry();
        NUD_TotalCaptured.SetValueClamped(Captured.CalculateTotalCaptured());
        NUD_TotalTransferred.SetValueClamped(Captured.CalculateTotalTransferred());
    }

    private static void ToggleMax(NumericUpDown nud) => nud.Value = nud.Value != nud.Maximum ? nud.Maximum : 0;

    protected override void OnSave()
    {
        SetEntry();
        SetTotals();
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
