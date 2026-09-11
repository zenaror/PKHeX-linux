using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen6;

/// <summary>
/// Poké Puff editor (port of the WinForms <c>SAV_Pokepuff</c>).
/// </summary>
public sealed class PokepuffWindow : SaveEditorWindow
{
    private readonly ISaveBlock6Main SAV;
    private readonly string[] pfa = GameInfo.Strings.puffs;
    private readonly ObservableCollection<PuffRow> Rows = [];
    private readonly DataGrid dgv = new() { AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column, IsReadOnly = false, CanUserSortColumns = false, Height = 420 };
    private readonly Button B_All = UiFactory.Button("B_All", "All");
    private readonly Button B_None = UiFactory.Button("B_None", "None");
    private readonly Button B_Sort = UiFactory.Button("B_Sort", "Sort");

    public PokepuffWindow(ISaveBlock6Main sav) : base("SAV_Pokepuff", "Poké Puff Editor")
    {
        SAV = sav;

        dgv.Columns.Add(new DataGridTextColumn { Header = "Slot", Binding = new Binding(nameof(PuffRow.Slot)), IsReadOnly = true, Width = new DataGridLength(60) });
        dgv.Columns.Add(DataGridUtil.StringComboColumn("Puff", pfa, nameof(PuffRow.Name), 220));
        dgv.ItemsSource = Rows;
        SetBody(UiFactory.Column(dgv, UiFactory.Row(B_All, B_None, B_Sort)));

        ToolTip.SetTip(B_Sort, "Hold CTRL to reverse sort.");
        ToolTip.SetTip(B_All, "Hold CTRL to best instead of varied.");

        B_All.AttachClick(mods => { SAV.Puff.MaxCheat(mods == KeyModifiers.Control); LoadPuffs(SAV.Puff.GetPuffs()); });
        B_None.Click += (_, _) => { SAV.Puff.Reset(); LoadPuffs(SAV.Puff.GetPuffs()); };
        B_Sort.AttachClick(mods => { SAV.Puff.Sort(mods == KeyModifiers.Control); LoadPuffs(SAV.Puff.GetPuffs()); });

        LoadPuffs(SAV.Puff.GetPuffs());
    }

    private void LoadPuffs(ReadOnlySpan<byte> puffs)
    {
        Rows.Clear();
        for (int i = 0; i < puffs.Length; i++)
        {
            int value = puffs[i];
            if (value >= pfa.Length)
                value = 0;
            Rows.Add(new PuffRow { Slot = (i + 1).ToString(), Name = pfa[value] });
        }
    }

    protected override void OnSave()
    {
        var puffs = new byte[Rows.Count];
        for (int i = 0; i < Rows.Count; i++)
            puffs[i] = (byte)Math.Max(0, Array.IndexOf(pfa, Rows[i].Name));
        SAV.Puff.SetPuffs(puffs);
        SAV.Puff.PuffCount = puffs.Length;
        Close();
    }

    private sealed class PuffRow
    {
        public required string Slot { get; init; }
        public required string Name { get; set; }
    }
}
