using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen5.JoinAvenue;

/// <summary>Fan-only fields (port of <c>JoinAvenueFanSpecificEditor</c>).</summary>
public sealed class JoinAvenueFanSpecificView : StackPanel, IJoinAvenueSpecificView<JoinAvenueFan5>
{
    private readonly NumericUpDown NUD_Unknown4C = Byte("NUD_Unknown4C");
    private readonly NumericUpDown NUD_Unknown4D = Byte("NUD_Unknown4D");
    private readonly NumericUpDown NUD_Unknown4E = Byte("NUD_Unknown4E");
    private readonly CheckBox CHK_InteractedToday = UiFactory.Check("CHK_InteractedToday", "Interacted Today");
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);
    private readonly NumericUpDown NUD_Unknown52 = UiFactory.NumericUpDown("NUD_Unknown52", 0, ushort.MaxValue, 120);
    private readonly NumericUpDown NUD_Unknown54 = Byte("NUD_Unknown54");
    private readonly NumericUpDown NUD_BubbleTarget = Byte("NUD_BubbleTarget");
    private readonly NumericUpDown NUD_Unknown56 = Byte("NUD_Unknown56");
    private readonly NumericUpDown NUD_Unknown5A = UiFactory.NumericUpDown("NUD_Unknown5A", 0, ushort.MaxValue, 120);

    private static NumericUpDown Byte(string name) => UiFactory.NumericUpDown(name, 0, byte.MaxValue, 110);

    public JoinAvenueFanSpecificView()
    {
        Orientation = Orientation.Vertical;
        Spacing = 2;
        CB_Species.SetItems([.. GameInfo.FilteredSources.Species]);

        var grid = UiFactory.FormGrid(10);
        int r = 0;
        Row(grid, r++, "L_Unknown4C", "0x4C:", NUD_Unknown4C);
        Row(grid, r++, "L_Unknown4D", "0x4D:", NUD_Unknown4D);
        Row(grid, r++, "L_Unknown4E", "0x4F:", NUD_Unknown4E);
        Row(grid, r++, "L_InteractedToday", string.Empty, CHK_InteractedToday);
        Row(grid, r++, "L_Species", "Species:", CB_Species);
        Row(grid, r++, "L_Unknown52", "0x52:", NUD_Unknown52);
        Row(grid, r++, "L_Unknown54", "0x54:", NUD_Unknown54);
        Row(grid, r++, "L_BubbleTarget", "Bubble Target:", NUD_BubbleTarget);
        Row(grid, r++, "L_Unknown56", "0x56:", NUD_Unknown56);
        Row(grid, r, "L_Unknown5A", "0x5A:", NUD_Unknown5A);
        Children.Add(grid);
        return;

        static void Row(Grid g, int row, string name, string text, Control editor)
            => UiFactory.AddFormRow(g, row, UiFactory.Label(name, text), editor);
    }

    public void LoadObject(JoinAvenueFan5 entity)
    {
        NUD_Unknown4C.SetValueClamped(entity.Unknown4C);
        NUD_Unknown4D.SetValueClamped(entity.Unknown4D);
        NUD_Unknown4E.SetValueClamped(entity.Unknown4F);
        CHK_InteractedToday.IsChecked = entity.IsInteractedToday;
        CB_Species.SetValue(entity.Species);
        NUD_Unknown52.SetValueClamped(entity.Unknown52);
        NUD_Unknown54.SetValueClamped(entity.Unknown54);
        NUD_BubbleTarget.SetValueClamped(entity.BubbleTarget);
        NUD_Unknown56.SetValueClamped(entity.Unknown56);
        NUD_Unknown5A.SetValueClamped(entity.Unknown5A);
    }

    public void SaveObject(JoinAvenueFan5 entity)
    {
        entity.Unknown4C = (byte)(NUD_Unknown4C.Value ?? 0);
        entity.Unknown4D = (byte)(NUD_Unknown4D.Value ?? 0);
        entity.Unknown4F = (byte)(NUD_Unknown4E.Value ?? 0);
        entity.IsInteractedToday = CHK_InteractedToday.IsChecked == true;
        entity.Species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
        entity.Unknown52 = (ushort)(NUD_Unknown52.Value ?? 0);
        entity.Unknown54 = (byte)(NUD_Unknown54.Value ?? 0);
        entity.BubbleTarget = (byte)(NUD_BubbleTarget.Value ?? 0);
        entity.Unknown56 = (byte)(NUD_Unknown56.Value ?? 0);
        entity.Unknown5A = (ushort)(NUD_Unknown5A.Value ?? 0);
    }
}
