using System.Linq;
using Avalonia.Controls;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen9;

/// <summary>
/// Seven Star raid editor for Scarlet/Violet (port of the WinForms <c>SAV_RaidSevenStar9</c>).
/// </summary>
public sealed class RaidSevenStar9Window : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV9SV SAV;
    private readonly RaidSevenStar9 Raids;
    private readonly ComboBox CB_Raid = UiFactory.StringCombo("CB_Raid", 160);
    private readonly PropertyGridView PG_Raid = new() { MinHeight = 360 };

    public RaidSevenStar9Window(SAV9SV sav) : base("SAV_RaidSevenStar9", "Raid Editor")
    {
        SAV = (SAV9SV)(Origin = sav).Clone();
        Raids = SAV.RaidSevenStar;
        SizeToContent = SizeToContent.Manual;
        CanResize = true;
        Width = 560;
        Height = 520;

        foreach (var raid in Enumerable.Range(1, Raids.CountAll).Select(z => $"Raid {z:0000}"))
            CB_Raid.Items.Add(raid);
        SetBody(UiFactory.Column(CB_Raid, PG_Raid));

        CB_Raid.SelectionChanged += (_, _) => LoadRaid(CB_Raid.SelectedIndex);
        CB_Raid.SelectedIndex = 0;
    }

    private void LoadRaid(int index)
    {
        if (index >= 0)
            PG_Raid.SetObject(Raids.GetRaid(index));
    }

    protected override void OnSave()
    {
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
