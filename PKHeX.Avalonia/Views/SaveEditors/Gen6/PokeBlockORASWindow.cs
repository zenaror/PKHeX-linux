using Avalonia.Controls;
using Avalonia.Input;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen6;

/// <summary>
/// Pokéblock editor for Omega Ruby / Alpha Sapphire (port of the WinForms <c>SAV_PokeBlockORAS</c>).
/// </summary>
public sealed class PokeBlockORASWindow : SaveEditorWindow
{
    private const int Count = 12;
    private readonly SaveFile Origin;
    private readonly SAV6AO SAV;
    private readonly NumericUpDown[] nup_spec = new NumericUpDown[Count];
    private readonly Button B_RandomizeBerries = UiFactory.Button("B_RandomizeBerries", "Randomize Berries");
    private readonly Button B_GiveAllBlocks = UiFactory.Button("B_GiveAllBlocks", "Give All Blocks");

    private static readonly string[] Names =
    [
        "L_Red", "L_Blue", "L_Pink", "L_Green", "L_Yellow", "L_Rainbow",
        "L_RedPlus", "L_BluePlus", "L_PinkPlus", "L_GreenPlus", "L_YellowPlus", "L_RainbowPlus",
    ];

    public PokeBlockORASWindow(SAV6AO sav) : base("SAV_PokeBlockORAS", "Pokéblock Editor")
    {
        SAV = (SAV6AO)(Origin = sav).Clone();

        var contest = SAV.Contest;
        var grid = UiFactory.FormGrid(Count);
        for (int i = 0; i < Count; i++)
        {
            nup_spec[i] = UiFactory.NumericUpDown($"NUP_{Names[i][2..]}", 0, 999, 110);
            nup_spec[i].Value = contest.GetBlockCount(i);
            var label = UiFactory.Label(Names[i], $"{GameInfo.Strings.pokeblocks[94 + i]}:");
            UiFactory.AddFormRow(grid, i, label, nup_spec[i]);
        }
        SetBody(UiFactory.Column(grid, UiFactory.Row(B_GiveAllBlocks, B_RandomizeBerries)));

        B_GiveAllBlocks.AttachClick(mods =>
        {
            var value = mods == KeyModifiers.Control ? 0 : 999;
            foreach (var n in nup_spec)
                n.Value = value;
        });
        B_RandomizeBerries.Click += async (_, _) =>
        {
            if (DialogResult.Yes != await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, "Repopulate all berry plots with random berries?"))
                return;

            // Randomize the trees.
            SAV.BerryField.ResetAndRandomize(Util.Rand, ItemStorage6XY.Berry);
        };
    }

    protected override void OnSave()
    {
        var contest = SAV.Contest;
        for (int i = 0; i < nup_spec.Length; i++)
            contest.SetBlockCount(i, (uint)(nup_spec[i].Value ?? 0));
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
