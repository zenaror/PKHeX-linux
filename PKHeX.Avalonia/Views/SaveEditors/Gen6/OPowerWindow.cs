using System;
using Avalonia.Controls;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen6;

/// <summary>
/// O-Power editor (port of the WinForms <c>SAV_OPower</c>).
/// </summary>
public sealed class OPowerWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SaveFile SAV;
    private readonly OPower6 Block;

    private readonly CheckedListView CLB_Unlock = new() { Name = "CLB_Unlock", Width = 240, Height = 300 };
    private readonly NumericUpDown[] NUDField_A;
    private readonly NumericUpDown[] NUDField_B;
    private readonly NumericUpDown[] NUDBattle_A;
    private readonly NumericUpDown[] NUDBattle_B;
    private readonly TextBlock L_Points = UiFactory.Label("L_Points", "Points:");
    private readonly NumericUpDown NUD_Points = UiFactory.NumericUpDown("NUD_Points", 0, byte.MaxValue, 100);
    private readonly Button B_ClearAll = UiFactory.Button("B_ClearAll", "Clear All");
    private readonly Button B_GiveAll = UiFactory.Button("B_GiveAll", "Give All");

    public OPowerWindow(ISaveBlock6Main sav) : base("SAV_OPower", "O-Power Editor")
    {
        Origin = (SaveFile)sav;
        SAV = Origin.Clone();
        Block = ((ISaveBlock6Main)SAV).OPower;

        var lang = MainWindow.CurrentLanguage;
        // get names, without the "Count" enum value at the end.
        var nameIndex = Translator.GetEnumTranslation<OPower6Index>(lang).AsSpan()[..^1];
        var nameField = Translator.GetEnumTranslation<OPower6FieldType>(lang).AsSpan()[..^1];
        var nameBattle = Translator.GetEnumTranslation<OPower6BattleType>(lang).AsSpan()[..^1];

        foreach (var index in nameIndex)
            CLB_Unlock.Add(index);

        NUDField_A = new NumericUpDown[nameField.Length];
        NUDField_B = new NumericUpDown[nameField.Length];
        var field = UiFactory.FormGrid(nameField.Length);
        for (int i = 0; i < nameField.Length; i++)
        {
            NUDField_A[i] = UiFactory.NumericUpDown($"NUD_F{i}A", 0, byte.MaxValue, 80);
            NUDField_B[i] = UiFactory.NumericUpDown($"NUD_F{i}B", 0, byte.MaxValue, 80);
            UiFactory.AddFormRow(field, i, UiFactory.Label($"L_F{i}", nameField[i]), UiFactory.Row(NUDField_A[i], NUDField_B[i]));
        }

        NUDBattle_A = new NumericUpDown[nameBattle.Length];
        NUDBattle_B = new NumericUpDown[nameBattle.Length];
        var battle = UiFactory.FormGrid(nameBattle.Length);
        for (int i = 0; i < nameBattle.Length; i++)
        {
            NUDBattle_A[i] = UiFactory.NumericUpDown($"NUD_B{i}A", 0, byte.MaxValue, 80);
            NUDBattle_B[i] = UiFactory.NumericUpDown($"NUD_B{i}B", 0, byte.MaxValue, 80);
            UiFactory.AddFormRow(battle, i, UiFactory.Label($"L_B{i}", nameBattle[i]), UiFactory.Row(NUDBattle_A[i], NUDBattle_B[i]));
        }

        var right = UiFactory.Column(
            new GroupBoxView("GB_Field", "Field", field),
            new GroupBoxView("GB_Battle", "Battle", battle),
            UiFactory.Row(L_Points, NUD_Points));
        var left = UiFactory.Column(CLB_Unlock, UiFactory.Row(B_GiveAll, B_ClearAll));
        var body = UiFactory.Row(left, new ScrollViewer { Content = right, MaxHeight = 520 });
        body.Spacing = 12;
        SetBody(body);

        B_ClearAll.Click += (_, _) => { Block.ClearAll(); LoadCurrent(); };
        B_GiveAll.Click += (_, _) => { Block.UnlockAll(); LoadCurrent(); };

        LoadCurrent();
    }

    private void LoadCurrent()
    {
        for (int i = 0; i < NUDField_A.Length; i++)
        {
            NUDField_A[i].Value = Block.GetLevel1((OPower6FieldType)i);
            NUDField_B[i].Value = Block.GetLevel2((OPower6FieldType)i);
        }
        for (int i = 0; i < NUDBattle_A.Length; i++)
        {
            NUDBattle_A[i].Value = Block.GetLevel1((OPower6BattleType)i);
            NUDBattle_B[i].Value = Block.GetLevel2((OPower6BattleType)i);
        }
        for (int i = 0; i < CLB_Unlock.Count; i++)
            CLB_Unlock.SetItemChecked(i, Block.GetState((OPower6Index)i) == OPowerFlagState.Unlocked);
        NUD_Points.Value = Block.Points;
    }

    protected override void OnSave()
    {
        for (int i = 0; i < CLB_Unlock.Count; i++)
            Block.SetState((OPower6Index)i, CLB_Unlock.GetItemChecked(i) ? OPowerFlagState.Unlocked : OPowerFlagState.Locked);
        for (int i = 0; i < NUDField_A.Length; i++)
        {
            Block.SetLevel1((OPower6FieldType)i, (byte)(NUDField_A[i].Value ?? 0));
            Block.SetLevel2((OPower6FieldType)i, (byte)(NUDField_B[i].Value ?? 0));
        }
        for (int i = 0; i < NUDBattle_A.Length; i++)
        {
            Block.SetLevel1((OPower6BattleType)i, (byte)(NUDBattle_A[i].Value ?? 0));
            Block.SetLevel2((OPower6BattleType)i, (byte)(NUDBattle_B[i].Value ?? 0));
        }
        Block.Points = (byte)(NUD_Points.Value ?? 0);
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
