using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;
using static PKHeX.Core.PokedexResearchTaskType8a;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen8;

/// <summary>
/// Research task editor for Legends: Arceus (port of the WinForms <c>SAV_PokedexResearchEditorLA</c>).
/// </summary>
/// <remarks>
/// The Pokédex editor only shows the tasks a species actually has; this editor exposes every task slot the save
/// format stores, so progress can be written for tasks the species never receives.
/// </remarks>
public sealed class PokedexResearchEditorLAWindow : SaveEditorWindow
{
    private readonly SAV8LA Origin;
    private readonly SAV8LA SAV;
    private readonly PokedexSave8a Dex;

    private readonly ushort Species;
    private readonly bool WasEmpty;

    private readonly NumericUpDown[] TaskNUPs;
    private readonly TextBlock[] TaskLabels;
    private readonly int[] TaskParameters;

    private readonly TextBlock L_Species = UiFactory.Label("L_Species", "{Species}");

    private static ReadOnlySpan<PokedexResearchTaskType8a> TaskTypes =>
    [
        Catch,
        CatchAlpha,
        CatchLarge,
        CatchSmall,
        CatchHeavy,
        CatchLight,
        CatchAtTime,
        CatchSleeping,
        CatchInAir,
        CatchNotSpotted,

        UseMove,
        UseMove,
        UseMove,
        UseMove,
        DefeatWithMoveType,
        DefeatWithMoveType,
        DefeatWithMoveType,
        Defeat,
        UseStrongStyleMove,
        UseAgileStyleMove,

        Evolve,
        GiveFood,
        StunWithItems,
        ScareWithScatterBang,
        LureWithPokeshiDoll,

        LeapFromTrees,
        LeapFromLeaves,
        LeapFromSnow,
        LeapFromOre,
        LeapFromTussocks,
    ];

    private static ReadOnlySpan<sbyte> TaskIndexes =>
    [
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        0, 1, 2, 3, 0, 1, 2, -1, -1, -1,
        -1, -1, -1, -1, -1,
        -1, -1, -1, -1, -1,
    ];

    /// <summary>Control name suffixes, in the same order as <see cref="TaskTypes"/> (WinForms <c>L_*</c> / <c>NUP_*</c>).</summary>
    private static readonly string[] TaskNames =
    [
        "Catch", "CatchAlpha", "CatchLarge", "CatchSmall", "CatchHeavy",
        "CatchLight", "CatchAtTime", "CatchSleeping", "CatchInAir", "CatchNotSpotted",

        "UseMove0", "UseMove1", "UseMove2", "UseMove3", "DefeatWithMove0",
        "DefeatWithMove1", "DefeatWithMove2", "Defeat", "StrongStyle", "AgileStyle",

        "Evolve", "GiveFood", "Stun", "Scare", "Lure",

        "LeapTrees", "LeapLeaves", "LeapSnow", "LeapOre", "LeapTussocks",
    ];

    public PokedexResearchEditorLAWindow(SAV8LA sav, ushort species, int dexIdx, IReadOnlyList<string> tasks, IReadOnlyList<string> timeTasks)
        : base("SAV_PokedexResearchEditorLA", "Pokédex Research Editor")
    {
        SAV = (SAV8LA)(Origin = sav).Clone();
        Dex = SAV.Blocks.PokedexSave;

        Species = species;

        var count = TaskNames.Length;
        TaskLabels = new TextBlock[count];
        TaskNUPs = new NumericUpDown[count];
        for (int i = 0; i < count; i++)
        {
            var name = TaskNames[i];
            TaskLabels[i] = UiFactory.Label($"L_{name}", "Task Description:");
            TaskNUPs[i] = UiFactory.NumericUpDown($"NUP_{name}", 0, 60000, 90);
        }

        TaskParameters = new int[count];
        InitializeTaskParameters(dexIdx);

        BuildLayout(); // translates the window; the task labels below are filled afterwards, like WinForms

        L_Species.Text = GameInfo.Strings.Species[species];

        // Initialize labels/values
        for (int i = 0; i < count; i++)
        {
            TaskLabels[i].Text = PokedexResearchTask8aExtensions.GetGenericTaskLabelString(TaskTypes[i], TaskIndexes[i], TaskParameters[i], tasks, timeTasks);

            Dex.GetResearchTaskProgressByForce(Species, TaskTypes[i], TaskIndexes[i], out var curValue);
            TaskNUPs[i].Value = curValue;
        }

        // Detect empty
        WasEmpty = IsEmpty();
    }

    private void BuildLayout()
    {
        var tabs = new TabControl { Name = "TC_Research", Width = 500 };
        tabs.Items.Add(CreateTab("GB_Catch", "Catch", 0, 10));
        tabs.Items.Add(CreateTab("GB_Battle", "Battle", 10, 10));
        tabs.Items.Add(CreateTab("GB_Interact", "Interact", 20, 5));
        tabs.Items.Add(CreateTab("GB_Observe", "Observe", 25, 5));

        L_Species.HorizontalAlignment = HorizontalAlignment.Center;
        var side = UiFactory.Column(L_Species);
        side.Width = 130;
        side.VerticalAlignment = VerticalAlignment.Top;
        side.Margin = new global::Avalonia.Thickness(0, 12, 0, 0);

        var body = UiFactory.Row(tabs, side);
        body.Spacing = 8;
        SetBody(body);
    }

    private TabItem CreateTab(string name, string header, int start, int length)
    {
        var grid = new Grid { ColumnSpacing = 6, RowSpacing = 3, Margin = new global::Avalonia.Thickness(4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (int i = 0; i < length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var label = TaskLabels[start + i];
            label.HorizontalAlignment = HorizontalAlignment.Left;
            label.TextWrapping = global::Avalonia.Media.TextWrapping.NoWrap;
            UiFactory.SetRowCol(label, i, 0);
            grid.Children.Add(label);

            var nud = TaskNUPs[start + i];
            UiFactory.SetRowCol(nud, i, 1);
            grid.Children.Add(nud);
        }
        return new TabItem { Name = name, Header = header, Content = grid };
    }

    private void InitializeTaskParameters(int idx)
    {
        if (idx < 0)
        {
            for (int i = 0; i < TaskParameters.Length; i++)
                TaskParameters[i] = TaskTypes[i] == CatchAtTime ? 0 : -1;
            return;
        }

        var tasks = PokedexConstants8a.ResearchTasks[idx];
        for (int i = 0; i < TaskParameters.Length; i++)
        {
            TaskParameters[i] = -1;

            switch (TaskTypes[i])
            {
                case UseMove:
                    foreach (var task in tasks)
                    {
                        if (task.Task != UseMove || task.Index != TaskIndexes[i])
                            continue;
                        TaskParameters[i] = task.Move;
                        break;
                    }
                    break;
                case DefeatWithMoveType:
                    foreach (var task in tasks)
                    {
                        if (task.Task != DefeatWithMoveType || task.Index != TaskIndexes[i])
                            continue;
                        TaskParameters[i] = (int)task.Type;
                        break;
                    }
                    break;
                case CatchAtTime:
                    TaskParameters[i] = 0;
                    foreach (var task in tasks)
                    {
                        if (task.Task != CatchAtTime)
                            continue;
                        TaskParameters[i] = (int)task.TimeOfDay;
                        break;
                    }
                    break;
            }
        }
    }

    private bool IsEmpty()
    {
        foreach (var nup in TaskNUPs)
        {
            if (nup.Value != 0)
                return false;
        }
        return true;
    }

    protected override void OnSave()
    {
        // If we should, set values.
        if (!WasEmpty || !IsEmpty())
        {
            for (int i = 0; i < TaskNUPs.Length; i++)
                Dex.SetResearchTaskProgressByForce(Species, TaskTypes[i], (int)(TaskNUPs[i].Value ?? 0), TaskIndexes[i]);
        }

        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
