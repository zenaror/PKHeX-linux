using System;
using Avalonia.Controls;
using Avalonia.Data;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4;

/// <summary>
/// Apricorn count editor for HeartGold/SoulSilver (port of the WinForms <c>SAV_Apricorn</c>).
/// </summary>
public sealed class ApricornWindow : SaveEditorWindow
{
    private const int Count = 7;
    private const int ItemNameBase = 485; // Red Apricorn

    private static ReadOnlySpan<byte> ItemNameOffset =>
    [
        0, // 485: Red
        2, // 487: Yellow - out of order
        1, // 486: Blue - out of order
        3, // 488: Green
        4, // 489: Pink
        5, // 490: White
        6, // 491: Black
    ];

    private readonly SAV4HGSS Origin;
    private readonly SAV4HGSS SAV;
    private readonly NumericUpDown[] Counts = new NumericUpDown[Count];
    private readonly Button B_All = UiFactory.Button("B_All", "All");
    private readonly Button B_None = UiFactory.Button("B_None", "None");

    public ApricornWindow(SAV4HGSS sav) : base("SAV_Apricorn", "Apricorn Editor")
    {
        SAV = (SAV4HGSS)(Origin = sav).Clone();

        var grid = UiFactory.FormGrid(Count);
        var itemNames = GameInfo.Strings.itemlist;
        for (int i = 0; i < Count; i++)
        {
            var itemId = ItemNameBase + ItemNameOffset[i];
            Counts[i] = UiFactory.NumericUpDown($"NUD_Apricorn{i}", 0, byte.MaxValue, 90);
            UiFactory.AddFormRow(grid, i, UiFactory.Label($"L_Apricorn{i}", itemNames[itemId]), Counts[i]);
        }
        SetBody(UiFactory.Column(grid, UiFactory.Row(B_All, B_None)));

        B_All.Click += (_, _) => SetAll(99);
        B_None.Click += (_, _) => SetAll(0);
        LoadCount();
    }

    private void SetAll(int value)
    {
        foreach (var nud in Counts)
            nud.Value = value;
    }

    private void LoadCount()
    {
        for (int i = 0; i < Count; i++)
            Counts[i].Value = SAV.GetApricornCount(i);
    }

    protected override void OnSave()
    {
        for (int i = 0; i < Count; i++)
            SAV.SetApricornCount(i, Math.Min(byte.MaxValue, (int)(Counts[i].Value ?? 0)));
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
