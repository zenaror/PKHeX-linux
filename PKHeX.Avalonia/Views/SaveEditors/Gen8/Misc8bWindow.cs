using Avalonia.Controls;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen8;

/// <summary>
/// Miscellaneous editor for Brilliant Diamond / Shining Pearl (port of the WinForms <c>SAV_Misc8b</c>).
/// </summary>
/// <remarks>
/// Every action flips the event flags needed to make a one-off encounter available again. A button is only
/// enabled while its unlock is actually applicable, so the save is never pushed into an inconsistent state.
/// </remarks>
public sealed class Misc8bWindow : SaveEditorWindow
{
    private readonly SAV8BS Origin;
    private readonly SAV8BS SAV;
    private readonly EventUnlocker8b Unlocker;

    private readonly Button B_Spiritomb = UiFactory.Button("B_Spiritomb", "Greet all Underground NPCs (Spiritomb)");
    private readonly Button B_Shaymin = UiFactory.Button("B_Shaymin", "Unlock Shaymin Event");
    private readonly Button B_Darkrai = UiFactory.Button("B_Darkrai", "Unlock Darkrai Event");
    private readonly Button B_Arceus = UiFactory.Button("B_Arceus", "Unlock Arceus Event");
    private readonly Button B_DialgaPalkia = UiFactory.Button("B_DialgaPalkia", "Reset Dialga/Palkia Encounter");
    private readonly Button B_Roamer = UiFactory.Button("B_Roamer", "Reset Roamers");
    private readonly Button B_Zones = UiFactory.Button("B_Zones", "Unlock All Zones");
    private readonly Button B_Fashion = UiFactory.Button("B_Fashion", "Unlock All Fashion");
    private readonly Button B_DefeatEyecatch = UiFactory.Button("B_DefeatEyecatch", "Defeat all Eyecatch Trainers");
    private readonly Button B_RebattleEyecatch = UiFactory.Button("B_RebattleEyecatch", "Rebattle all Eyecatch Trainers");

    public Misc8bWindow(SAV8BS sav) : base("SAV_Misc8b", "Misc Editor")
    {
        SAV = (SAV8BS)(Origin = sav).Clone();
        Unlocker = new EventUnlocker8b(SAV);

        var buttons = UiFactory.Column(
            B_Spiritomb, B_Shaymin, B_Darkrai, B_Arceus, B_DialgaPalkia,
            B_Roamer, B_Zones, B_Fashion, B_DefeatEyecatch, B_RebattleEyecatch);
        foreach (var b in new[] { B_Spiritomb, B_Shaymin, B_Darkrai, B_Arceus, B_DialgaPalkia, B_Roamer, B_Zones, B_Fashion, B_DefeatEyecatch, B_RebattleEyecatch })
            b.MinWidth = 300;

        SetBody(new TabControl { Items = { new TabItem { Name = "TAB_Main", Header = "Main", Content = buttons } } });

        ReadMain();

        B_Spiritomb.Click += (_, _) => { Unlocker.UnlockSpiritomb(); B_Spiritomb.IsEnabled = Unlocker.UnlockReadySpiritomb; };
        B_Shaymin.Click += (_, _) => { Unlocker.UnlockShaymin(); B_Shaymin.IsEnabled = Unlocker.UnlockReadyShaymin; };
        B_Darkrai.Click += (_, _) => { Unlocker.UnlockDarkrai(); B_Darkrai.IsEnabled = Unlocker.UnlockReadyDarkrai; };
        B_Arceus.Click += (_, _) => { Unlocker.UnlockArceus(); B_Arceus.IsEnabled = Unlocker.UnlockReadyArceus; };
        B_DialgaPalkia.Click += (_, _) => { Unlocker.UnlockBoxLegend(); B_DialgaPalkia.IsEnabled = Unlocker.UnlockReadyBoxLegend; };
        B_Roamer.Click += (_, _) =>
        {
            Unlocker.RespawnRoamer();
            B_Roamer.IsEnabled = Unlocker.ResetReadyRoamerMesprit || Unlocker.ResetReadyRoamerCresselia;
        };
        B_Zones.Click += (_, _) => { Unlocker.UnlockZones(); B_Zones.IsEnabled = false; };
        B_Fashion.Click += (_, _) => { Unlocker.UnlockFashion(); B_Fashion.IsEnabled = false; };
        B_DefeatEyecatch.Click += (_, _) =>
        {
            SAV.BattleTrainer.DefeatAll();
            B_DefeatEyecatch.IsEnabled = false;
            B_RebattleEyecatch.IsEnabled = true;
        };
        B_RebattleEyecatch.Click += (_, _) =>
        {
            SAV.BattleTrainer.RebattleAll();
            B_RebattleEyecatch.IsEnabled = false;
            B_DefeatEyecatch.IsEnabled = true;
        };
    }

    private void ReadMain()
    {
        B_Spiritomb.IsEnabled = Unlocker.UnlockReadySpiritomb;
        B_Darkrai.IsEnabled = Unlocker.UnlockReadyDarkrai;
        B_Shaymin.IsEnabled = Unlocker.UnlockReadyShaymin;
        B_Arceus.IsEnabled = Unlocker.UnlockReadyArceus;
        B_DialgaPalkia.IsEnabled = Unlocker.UnlockReadyBoxLegend;
        B_Roamer.IsEnabled = Unlocker.ResetReadyRoamerMesprit || Unlocker.ResetReadyRoamerCresselia;
        B_RebattleEyecatch.IsEnabled = SAV.BattleTrainer.AnyDefeated;
        B_DefeatEyecatch.IsEnabled = SAV.BattleTrainer.AnyUndefeated;
    }

    protected override void OnSave()
    {
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
