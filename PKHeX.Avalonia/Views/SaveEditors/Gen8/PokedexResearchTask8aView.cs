using System;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;
using PKHeX.Drawing;
using Color = System.Drawing.Color;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen8;

/// <summary>
/// One research task row of the Legends: Arceus Pokédex (port of the WinForms <c>PokedexResearchTask8aPanel</c>).
/// </summary>
/// <remarks>
/// Shows the task name, the current progress and its five level thresholds. Each threshold is tinted to say
/// whether it is already met and whether that level was reported to Professor Laventon.
/// </remarks>
public sealed class PokedexResearchTask8aView : StackPanel
{
    private const int ThresholdCount = 5;

    private readonly TextBlock Label_Task = UiFactory.Label("Label_Task", string.Empty);
    private readonly NumericUpDown NUP_CurrentValue = UiFactory.NumericUpDown("NUP_CurrentValue", 0, ushort.MaxValue, 110);
    private readonly TextBlock PB_Bonus = UiFactory.Label("PB_Bonus", string.Empty);
    private readonly TextBox[] ThresholdBoxes = new TextBox[ThresholdCount];
    private readonly StackPanel FLP_T1Right = new() { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };

    private string[] TaskDescriptions = [];
    private string[] SpeciesQuests = [];
    private string[] TimeTaskDescriptions = [];

    public ushort Species { get; private set; }
    public int ReportedCount { get; private set; }
    public PokedexResearchTask8a Task { get; private set; } = new();

    public PokedexResearchTask8aView()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        VerticalAlignment = VerticalAlignment.Center;

        for (int i = 0; i < ThresholdBoxes.Length; i++)
        {
            ThresholdBoxes[i] = UiFactory.Text($"MTB_Threshold{i + 1}", 5, 56);
            ThresholdBoxes[i].IsReadOnly = true;
        }

        Label_Task.MinWidth = 320;
        Children.Add(Label_Task);
        Children.Add(NUP_CurrentValue);
        Children.Add(PB_Bonus);
        Children.Add(FLP_T1Right);

        NUP_CurrentValue.ValueChanged += (_, _) => ShadeBoxes();
    }

    public int CurrentValue
    {
        get => (int)(NUP_CurrentValue.Value ?? 0);
        set => NUP_CurrentValue.SetValueClamped(value);
    }

    public int PointsPerLevel => Task.PointsSingle + Task.PointsBonus;
    public bool CanSetCurrentValue => Task.Task.CanSetCurrentValue();

    public void SetStrings(string[] tasks, string[] speciesQuests, string[] timeTasks)
    {
        TaskDescriptions = tasks;
        SpeciesQuests = speciesQuests;
        TimeTaskDescriptions = timeTasks;
    }

    public void SetTask(ushort species, PokedexResearchTask8a task, int reportedLevel)
    {
        Species = species;
        Task = task;
        ReportedCount = reportedLevel - 1;

        PB_Bonus.Text = task.PointsBonus != 0 ? "★" : string.Empty;
        Label_Task.Text = $"{Task.GetTaskLabelString(TaskDescriptions, TimeTaskDescriptions, SpeciesQuests)}:";
        NUP_CurrentValue.IsEnabled = CanSetCurrentValue;

        FLP_T1Right.Children.Clear();
        for (var t = 0; t < task.TaskThresholds.Length && t < ThresholdBoxes.Length; t++)
            ThresholdBoxes[t].Text = task.TaskThresholds[t].ToString();

        // The WinForms panel lists the thresholds right to left.
        for (var t = 0; t < task.TaskThresholds.Length && t < ThresholdBoxes.Length; t++)
            FLP_T1Right.Children.Add(ThresholdBoxes[task.TaskThresholds.Length - 1 - t]);

        ShadeBoxes();
    }

    public void ShadeBoxes()
    {
        var currentValue = CurrentValue;
        for (var i = 0; i < Task.TaskThresholds.Length && i < ThresholdBoxes.Length; i++)
            ThresholdBoxes[i].SetBackColor(GetTaskColor(currentValue, i));
    }

    private Color GetTaskColor(int currentValue, int thresholdIndex)
    {
        bool belowReported = thresholdIndex < ReportedCount;
        var window = Color.White;
        if (currentValue >= Task.TaskThresholds[thresholdIndex])
            return ColorUtil.Blend(belowReported ? Color.Green : Color.YellowGreen, window, 0.4);
        return belowReported ? ColorUtil.Blend(Color.Red, window, 0.4) : window;
    }
}
