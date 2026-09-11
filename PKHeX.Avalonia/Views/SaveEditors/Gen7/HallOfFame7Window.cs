using Avalonia.Controls;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen7;

/// <summary>
/// Hall of Fame editor for Sun/Moon and Ultra Sun/Ultra Moon (port of the WinForms <c>SAV_HallOfFame7</c>).
/// </summary>
public sealed class HallOfFame7Window : SaveEditorWindow
{
    private readonly SAV7 SAV;
    private readonly ComboBox[] entries = new ComboBox[12];
    private readonly TextBlock L_EC = UiFactory.Label("L_EC", "Starter EC:");
    private readonly NumericTextBox TB_EC = UiFactory.Numeric("TB_EC", 8, 90, hex: true);

    public HallOfFame7Window(SAV7 sav) : base("SAV_HallOfFame7", "Hall of Fame Editor")
    {
        SAV = sav;

        var block = SAV.EventWork.Fame;
        var specList = GameInfo.FilteredSources.Species;

        var first = UiFactory.FormGrid(6);
        var current = UiFactory.FormGrid(6);
        for (int i = 0; i < entries.Length; i++)
        {
            bool isFirst = i < 6;
            var index = (i % 6) + 1;
            var cb = UiFactory.Combo(isFirst ? $"CB_F{index}" : $"CB_C{index}", 180);
            cb.SetItems(specList);
            cb.SetValue(block.GetEntry(i));
            entries[i] = cb;
            var label = UiFactory.Label(isFirst ? $"L_F{index}" : $"L_C{index}", $"PKM {index}:");
            UiFactory.AddFormRow(isFirst ? first : current, index - 1, label, cb);
        }

        var body = UiFactory.Column(
            UiFactory.Row(new GroupBoxView("L_First", "First", first), new GroupBoxView("L_Current", "Current", current)),
            UiFactory.Row(L_EC, TB_EC));
        SetBody(body);

        if (SAV is SAV7USUM uu)
            TB_EC.Text = uu.Misc.StarterEncryptionConstant.ToString("X8");
        else
            TB_EC.IsVisible = L_EC.IsVisible = false;
    }

    protected override void OnSave()
    {
        var block = SAV.EventWork.Fame;
        for (int i = 0; i < entries.Length; i++)
            block.SetEntry(i, (ushort)entries[i].GetValue());

        if (SAV is SAV7USUM uu)
            uu.Misc.StarterEncryptionConstant = TB_EC.UIntValue;

        Close();
    }
}
