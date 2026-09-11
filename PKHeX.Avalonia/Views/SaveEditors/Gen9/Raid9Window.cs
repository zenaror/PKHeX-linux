using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen9;

/// <summary>
/// Tera Raid editor for Scarlet/Violet (port of the WinForms <c>SAV_Raid9</c>).
/// </summary>
public sealed class Raid9Window : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV9SV SAV;
    private readonly RaidSpawnList9 Raids;

    private readonly ComboBox CB_Raid = UiFactory.StringCombo("CB_Raid", 160);
    private readonly PropertyGridView PG_Raid = new() { MinHeight = 340 };
    private readonly TextBlock L_SeedCurrent = UiFactory.Label("L_SeedCurrent", "Current Seed:");
    private readonly NumericTextBox TB_SeedToday = UiFactory.Numeric("TB_SeedToday", 16, 160, hex: true);
    private readonly TextBlock L_SeedTomorrow = UiFactory.Label("L_SeedTomorrow", "Tomorrow:");
    private readonly NumericTextBox TB_SeedTomorrow = UiFactory.Numeric("TB_SeedTomorrow", 16, 160, hex: true);
    private readonly Button B_CopyToOthers = UiFactory.Button("B_CopyToOthers", "Copy to Other Raids");

    public Raid9Window(SAV9SV sav, TeraRaidOrigin raidOrigin) : base("SAV_Raid9", "Raid Editor")
    {
        SAV = (SAV9SV)(Origin = sav).Clone();
        Raids = raidOrigin switch
        {
            TeraRaidOrigin.Paldea => SAV.RaidPaldea,
            TeraRaidOrigin.Kitakami => SAV.RaidKitakami,
            TeraRaidOrigin.BlueberryAcademy => SAV.RaidBlueberry,
            _ => throw new ArgumentOutOfRangeException(nameof(raidOrigin), $"Raid Origin {raidOrigin} is not valid for Scarlet and Violet"),
        };
        SizeToContent = SizeToContent.Manual;
        CanResize = true;
        Width = 600;
        Height = 560;

        foreach (var raid in Enumerable.Range(1, Raids.CountUsed).Select(z => $"Raid {z:000}"))
            CB_Raid.Items.Add(raid);

        var seeds = UiFactory.FormGrid(2);
        UiFactory.AddFormRow(seeds, 0, L_SeedCurrent, TB_SeedToday);
        UiFactory.AddFormRow(seeds, 1, L_SeedTomorrow, TB_SeedTomorrow);
        SetBody(UiFactory.Column(UiFactory.Row(CB_Raid, B_CopyToOthers), seeds, PG_Raid));

        CB_Raid.SelectionChanged += (_, _) => LoadRaid(CB_Raid.SelectedIndex);
        B_CopyToOthers.AttachClick(mods => Raids.Propagate(CB_Raid.SelectedIndex, seedToo: mods == KeyModifiers.Shift));
        TB_SeedToday.LostFocus += async (_, _) => await UpdateStringSeed(TB_SeedToday);
        TB_SeedTomorrow.LostFocus += async (_, _) => await UpdateStringSeed(TB_SeedTomorrow);

        CB_Raid.SelectedIndex = 0;
        LoadSeeds();
    }

    private void LoadSeeds()
    {
        if (Raids.HasSeeds)
        {
            TB_SeedToday.Text = Raids.CurrentSeed.ToString("X16");
            TB_SeedTomorrow.Text = Raids.TomorrowSeed.ToString("X16");
        }
        else
        {
            L_SeedCurrent.IsVisible = false;
            L_SeedTomorrow.IsVisible = false;
            TB_SeedToday.IsVisible = false;
            TB_SeedTomorrow.IsVisible = false;
        }
    }

    private void LoadRaid(int index)
    {
        if (index >= 0)
            PG_Raid.SetObject(Raids.GetRaid(index));
    }

    private async System.Threading.Tasks.Task UpdateStringSeed(NumericTextBox tb)
    {
        var text = tb.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return;

        string filterText = Util.GetOnlyHex(text);
        if (string.IsNullOrWhiteSpace(filterText) || filterText.Length != text.Length)
        {
            await AppDialogs.Alert(this, MsgProgramErrorExpectedHex, text);
            return;
        }

        // Write final value back to the save
        var value = ulong.Parse(text, NumberStyles.HexNumber);
        if (tb == TB_SeedToday)
            Raids.CurrentSeed = value;
        else if (tb == TB_SeedTomorrow)
            Raids.TomorrowSeed = value;
    }

    protected override void OnSave()
    {
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
