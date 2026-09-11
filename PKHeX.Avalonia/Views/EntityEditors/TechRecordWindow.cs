using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Views.SaveEditors;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.EntityEditors;

/// <summary>
/// Technical Record flag editor (port of the WinForms <c>TechRecordEditor</c>).
/// </summary>
public sealed class TechRecordWindow : SaveEditorWindow
{
    private readonly ITechRecord Record;
    private readonly PKM Entity;
    private readonly LegalityAnalysis Legality;
    private readonly FlagRowList dgv = new("Has");
    private readonly Button B_None = UiFactory.Button("B_None", "Remove All");
    private readonly Button B_All = UiFactory.Button("B_All", "Give All");

    public TechRecordWindow(ITechRecord techRecord, PKM pk) : base("TechRecordEditor", "TR Relearn Editor")
    {
        Record = techRecord;
        Entity = pk;
        Legality = new LegalityAnalysis(pk);
        SizeToContent = SizeToContent.Manual;
        CanResize = true;
        Width = 360;
        Height = 480;

        ButtonBar.Children.Insert(0, B_None);
        ButtonBar.Children.Insert(1, B_All);
        B_All.Click += (_, _) => B_All_Click();
        B_None.Click += (_, _) => { Record.ClearRecordFlags(); Close(); };

        Span<ushort> currentMoves = stackalloc ushort[4];
        pk.GetMoves(currentMoves);
        PopulateRecords(pk.Context, currentMoves);
        SetBody(dgv);
    }

    private void PopulateRecords(EntityContext context, ReadOnlySpan<ushort> currentMoves)
    {
        var names = GameInfo.Strings.Move;
        var indexes = Record.Permit.RecordPermitIndexes;
        var baseRecordIndex = context == EntityContext.Gen9a ? 1 : 0; // TM001 in Legends: Z-A but is 0-index bits.
        var evos = Legality.Info.EvoChainsAllGens.Get(context);
        for (int i = 0; i < indexes.Length; i++)
        {
            var move = indexes[i];
            var type = MoveInfo.GetType(move, context);
            bool isValid = Record.Permit.IsRecordPermitted(i);
            IBrush flag = isValid ? ColorUtilAvalonia.ColorValid.ToBrush()
                : Record.IsRecordPermitted(evos, i) ? ColorUtilAvalonia.ColorHint.ToBrush()
                : ColorUtilAvalonia.ColorSuspect.ToBrush();
            var index = i + baseRecordIndex;
            dgv.Rows.Add(new FlagRow
            {
                RecordIndex = index,
                IndexText = index.ToString("000"),
                Type = type,
                TypeIcon = FlagRowList.GetTypeIcon(type),
                Name = names[move],
                SortKey = type.ToString("00") + (isValid ? 0 : 1) + names[move], // type -> valid -> name sorting
                FlagBrush = flag,
                NameBrush = currentMoves.Contains(move) ? ColorUtilAvalonia.ColorAccept.ToBrush() : Brushes.Transparent,
                HasFlag = Record.GetMoveRecordFlag(index),
            });
        }
    }

    protected override void OnSave()
    {
        Save();
        Close();
    }

    private void Save()
    {
        foreach (var row in dgv.Rows)
            Record.SetMoveRecordFlag(row.RecordIndex, row.HasFlag);
    }

    private void B_All_Click()
    {
        Save();
        var option = MainWindow.CurrentModifiers switch
        {
            KeyModifiers.Alt => TechnicalRecordApplicatorOption.None,
            KeyModifiers.Shift => TechnicalRecordApplicatorOption.ForceAll,
            KeyModifiers.Control => TechnicalRecordApplicatorOption.LegalCurrent,
            _ => TechnicalRecordApplicatorOption.LegalAll,
        };
        Record.SetRecordFlags(Entity, option);
        Close();
    }
}
