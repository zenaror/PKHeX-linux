using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Picker for the trainer record list plus the value of the selected record
/// (port of the WinForms <c>TrainerStat</c> user control shared by the Gen 6+ trainer editors).
/// </summary>
public sealed class TrainerStatView : StackPanel
{
    private readonly ComboBox CB_Stats = UiFactory.StringCombo("CB_Stats", 260);
    private readonly NumericUpDown NUD_Stat = UiFactory.NumericUpDown("NUD_Stat", 0, int.MaxValue, 150);
    private readonly TextBlock L_Offset = UiFactory.Label("L_Offset", "Offset: 0x000");
    private readonly ObservableCollection<string> Names = [];

    private bool editing;
    private ITrainerStatRecord SAV = null!;
    private Dictionary<int, string> RecordList = null!;

    /// <summary>Optional per-record description; falls back to the record list text.</summary>
    public Func<int, string?>? GetToolTipText { private get; set; }

    public TrainerStatView()
    {
        Name = "TrainerStats";
        Orientation = Orientation.Vertical;
        Spacing = 4;
        CB_Stats.ItemsSource = Names;
        Children.Add(CB_Stats);
        Children.Add(NUD_Stat);
        Children.Add(L_Offset);

        CB_Stats.SelectionChanged += (_, _) => ChangeStat();
        NUD_Stat.ValueChanged += (_, _) => ChangeStatValue();
    }

    public void LoadRecords(ITrainerStatRecord sav, Dictionary<int, string> records)
    {
        SAV = sav;
        RecordList = records;
        Names.Clear();
        for (int i = 0; i < sav.RecordCount; i++)
            Names.Add(records.TryGetValue(i, out var name) ? name : $"{i:D3}");
        CB_Stats.SelectedIndex = records.First().Key;
    }

    private void ChangeStat()
    {
        int index = CB_Stats.SelectedIndex;
        if (index < 0 || SAV is null)
            return;

        editing = true;
        int val = SAV.GetRecord(index);
        NUD_Stat.Maximum = Math.Max(val, SAV.GetRecordMax(index));
        NUD_Stat.Value = val;
        L_Offset.Text = $"Offset: 0x{SAV.GetRecordOffset(index):X3}";
        UpdateTip(index, true);
        editing = false;
    }

    private void ChangeStatValue()
    {
        if (editing || SAV is null)
            return;
        int index = CB_Stats.SelectedIndex;
        if (index < 0)
            return;
        SAV.SetRecord(index, (int)(NUD_Stat.Value ?? 0));
        UpdateTip(index, false);
    }

    private void UpdateTip(int index, bool updateStats)
    {
        var special = GetToolTipText?.Invoke(index);
        if (special is not null)
        {
            ToolTip.SetTip(NUD_Stat, special);
            return;
        }
        if (updateStats && RecordList.TryGetValue(index, out var tip))
            ToolTip.SetTip(CB_Stats, tip);
        else
            ToolTip.SetTip(CB_Stats, null);
    }
}
