using Avalonia.Controls;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen6;

/// <summary>
/// Roaming legendary editor for X/Y (port of the WinForms <c>SAV_Roamer6</c>).
/// </summary>
/// <remarks>
/// Only one of the legendary birds roams, chosen by the starter the player picked. Until the league is beaten
/// the species is not written yet, so it is derived from the starter choice event work value.
/// </remarks>
public sealed class Roamer6Window : SaveEditorWindow
{
    private const int SpeciesOffset = 144;
    private const int StarterChoiceIndex = 48;

    private readonly SAV6XY Origin;
    private readonly SAV6XY SAV;
    private readonly Roamer6 roamer;

    private readonly ComboBox CB_Species = UiFactory.StringCombo("CB_Species", 160);
    private readonly ComboBox CB_RoamState = UiFactory.StringCombo("CB_RoamState", 160);
    private readonly NumericUpDown NUD_TimesEncountered = UiFactory.NumericUpDown("NUD_TimesEncountered", 0, uint.MaxValue, 130);

    public Roamer6Window(SAV6XY sav) : base("SAV_Roamer6", "Roamer Editor")
    {
        SAV = (SAV6XY)(Origin = sav).Clone();
        roamer = SAV.Encount.Roamer;

        var species = GameInfo.Strings.specieslist;
        foreach (var s in new[] { species[(int)Species.Articuno], species[(int)Species.Zapdos], species[(int)Species.Moltres] })
            CB_Species.Items.Add(s);
        foreach (var s in new[] { "Inactive", "Roaming", "Stationary", "Defeated", "Captured" })
            CB_RoamState.Items.Add(s);

        var grid = UiFactory.FormGrid(3);
        UiFactory.AddFormRow(grid, 0, UiFactory.Label("L_Species", "Species:"), CB_Species);
        UiFactory.AddFormRow(grid, 1, UiFactory.Label("L_RoamState", "State:"), CB_RoamState);
        UiFactory.AddFormRow(grid, 2, UiFactory.Label("L_TimesEncountered", "Times Encountered:"), NUD_TimesEncountered);
        SetBody(grid);

        CB_Species.SelectedIndex = GetInitialIndex(sav);
        NUD_TimesEncountered.Value = roamer.TimesEncountered;
        CB_RoamState.SelectedIndex = (int)roamer.RoamStatus;

        CB_Species.SelectionChanged += (_, _) => roamer.Species = (ushort)(SpeciesOffset + CB_Species.SelectedIndex);
        NUD_TimesEncountered.ValueChanged += (_, _) => roamer.TimesEncountered = (uint)(NUD_TimesEncountered.Value ?? 0);
        CB_RoamState.SelectionChanged += (_, _) => roamer.RoamStatus = (Roamer6State)CB_RoamState.SelectedIndex;
    }

    private int GetInitialIndex(SAV6XY sav)
    {
        if (roamer.Species != 0)
            return roamer.Species - SpeciesOffset;
        return sav.EventWork.GetWork(StarterChoiceIndex);
    }

    protected override void OnSave()
    {
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
