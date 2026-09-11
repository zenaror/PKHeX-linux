using System;
using System.Linq;
using Avalonia.Controls;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen8;

/// <summary>
/// Max Raid den editor for Sword/Shield (port of the WinForms <c>SAV_Raid8</c>).
/// </summary>
public sealed class Raid8Window : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV8SWSH SAV;
    private readonly RaidSpawnList8 Raids;
    private readonly ComboBox CB_Den = UiFactory.StringCombo("CB_Den", 160);
    private readonly PropertyGridView PG_Den = new() { MinHeight = 360 };

    public Raid8Window(SAV8SWSH sav, MaxRaidOrigin raidOrigin) : base("SAV_Raid8", "Raid Editor")
    {
        SAV = (SAV8SWSH)(Origin = sav).Clone();
        Raids = raidOrigin switch
        {
            MaxRaidOrigin.Galar => SAV.RaidGalar,
            MaxRaidOrigin.IsleOfArmor => SAV.RaidArmor,
            MaxRaidOrigin.CrownTundra => SAV.RaidCrown,
            _ => throw new ArgumentOutOfRangeException(nameof(raidOrigin), $"Raid Origin {raidOrigin} is not valid for Sword and Shield"),
        };
        SizeToContent = SizeToContent.Manual;
        CanResize = true;
        Width = 560;
        Height = 520;

        foreach (var den in Enumerable.Range(1, Raids.CountUsed).Select(z => $"Den {z:000}"))
            CB_Den.Items.Add(den);
        SetBody(UiFactory.Column(CB_Den, PG_Den));

        CB_Den.SelectionChanged += (_, _) => LoadDen(CB_Den.SelectedIndex);
        CB_Den.SelectedIndex = 0;
    }

    private void LoadDen(int index)
    {
        if (index >= 0)
            PG_Den.SetObject(Raids.GetRaid(index));
    }

    protected override void OnSave()
    {
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
