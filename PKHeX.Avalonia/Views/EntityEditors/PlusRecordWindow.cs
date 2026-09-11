using System;
using System.Buffers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Views.SaveEditors;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.EntityEditors;

/// <summary>
/// Plus move flag editor (port of the WinForms <c>PlusRecordEditor</c>).
/// </summary>
public sealed class PlusRecordWindow : SaveEditorWindow
{
    private readonly IPlusRecord Plus;
    private readonly IPermitPlus Permit;
    private readonly PKM Entity;
    private readonly LegalityAnalysis Legality;
    private readonly FlagRowList dgv = new("Has");
    private readonly Button B_None = UiFactory.Button("B_None", "Remove All");
    private readonly Button B_All = UiFactory.Button("B_All", "Give All");

    public PlusRecordWindow(IPlusRecord plus, IPermitPlus permit, PKM pk) : base("PlusRecordEditor", "Plus Move Editor")
    {
        Plus = plus;
        Permit = permit;
        Entity = pk;
        Legality = new LegalityAnalysis(pk);
        SizeToContent = SizeToContent.Manual;
        CanResize = true;
        Width = 360;
        Height = 480;

        ButtonBar.Children.Insert(0, B_None);
        ButtonBar.Children.Insert(1, B_All);
        B_All.Click += (_, _) => B_All_Click();
        B_None.Click += (_, _) => { Plus.ClearPlusFlags(Permit.PlusCountTotal); Close(); };

        Span<ushort> currentMoves = stackalloc ushort[4];
        pk.GetMoves(currentMoves);
        PopulateRecords(pk.Context, currentMoves);
        SetBody(dgv);
    }

    private void PopulateRecords(EntityContext context, ReadOnlySpan<ushort> currentMoves)
    {
        var names = GameInfo.Strings.Move;
        var indexes = Permit.PlusMoveIndexes;

        var count = Entity.MaxMoveID + 1;
        var rent = ArrayPool<bool>.Shared.Rent(count);
        var span = rent.AsSpan(0, count);
        span.Clear();
        LearnPossible.Get(Entity, Legality.EncounterMatch, Legality.Info.EvoChainsAllGens, span);

        for (int i = 0; i < indexes.Length; i++)
        {
            var move = indexes[i];
            var type = MoveInfo.GetType(move, context);
            bool isValid = true;
            System.Drawing.Color color;
            if (currentMoves.Contains(move))
                color = ColorUtilAvalonia.ColorAccept;
            else if (span[move])
                color = ColorUtilAvalonia.ColorValid;
            else
            {
                color = ColorUtilAvalonia.ColorSuspect;
                isValid = false;
            }

            dgv.Rows.Add(new FlagRow
            {
                RecordIndex = i,
                IndexText = i.ToString("000"),
                Type = type,
                TypeIcon = FlagRowList.GetTypeIcon(type),
                Name = names[move],
                SortKey = type.ToString("00") + (isValid ? 0 : 1) + names[move], // type -> valid -> name sorting
                FlagBrush = color.ToBrush(),
                HasFlag = Plus.GetMovePlusFlag(i),
            });
        }

        ArrayPool<bool>.Shared.Return(rent);
    }

    protected override void OnSave()
    {
        Save();
        Close();
    }

    private void Save()
    {
        foreach (var row in dgv.Rows)
            Plus.SetMovePlusFlag(row.RecordIndex, row.HasFlag);
    }

    private void B_All_Click()
    {
        Save();
        var option = MainWindow.CurrentModifiers switch
        {
            KeyModifiers.Alt => PlusRecordApplicatorOption.None,
            KeyModifiers.Shift => PlusRecordApplicatorOption.LegalSeedTM,
            KeyModifiers.Control => PlusRecordApplicatorOption.LegalCurrentTM,
            _ => PlusRecordApplicatorOption.LegalCurrent,
        };
        Plus.SetPlusFlags(Entity, Permit, option);
        Close();
    }
}
