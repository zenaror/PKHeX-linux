using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen5.JoinAvenue;

/// <summary>Assistant-only fields (port of <c>JoinAvenueAssistantSpecificEditor</c>).</summary>
public sealed class JoinAvenueAssistantSpecificView : StackPanel, IJoinAvenueSpecificView<JoinAvenueAssistant5>
{
    private readonly NumericUpDown NUD_Position0 = Byte("NUD_Position0");
    private readonly NumericUpDown NUD_Position1 = Byte("NUD_Position1");
    private readonly NumericUpDown NUD_Position2 = Byte("NUD_Position2");
    private readonly NumericUpDown NUD_PositionUnused = Byte("NUD_PositionUnused");
    private readonly CheckBox CHK_InteractedToday = UiFactory.Check("CHK_InteractedToday", "Interacted Today");

    private static NumericUpDown Byte(string name) => UiFactory.NumericUpDown(name, 0, byte.MaxValue, 110);

    public JoinAvenueAssistantSpecificView()
    {
        Orientation = Orientation.Vertical;
        Spacing = 2;

        var grid = UiFactory.FormGrid(5);
        UiFactory.AddFormRow(grid, 0, UiFactory.Label("L_Position0", "Position 0:"), NUD_Position0);
        UiFactory.AddFormRow(grid, 1, UiFactory.Label("L_Position1", "Position 1:"), NUD_Position1);
        UiFactory.AddFormRow(grid, 2, UiFactory.Label("L_Position2", "Position 2:"), NUD_Position2);
        UiFactory.AddFormRow(grid, 3, UiFactory.Label("L_PositionUnused", "Position (unused):"), NUD_PositionUnused);
        UiFactory.AddFormRow(grid, 4, null, CHK_InteractedToday);
        Children.Add(grid);
    }

    public void LoadObject(JoinAvenueAssistant5 entity)
    {
        NUD_Position0.SetValueClamped(entity.Position0);
        NUD_Position1.SetValueClamped(entity.Position1);
        NUD_Position2.SetValueClamped(entity.Position2);
        NUD_PositionUnused.SetValueClamped(entity.PositionUnused);
        CHK_InteractedToday.IsChecked = entity.IsInteractedToday;
    }

    public void SaveObject(JoinAvenueAssistant5 entity)
    {
        entity.Position0 = (byte)(NUD_Position0.Value ?? 0);
        entity.Position1 = (byte)(NUD_Position1.Value ?? 0);
        entity.Position2 = (byte)(NUD_Position2.Value ?? 0);
        entity.PositionUnused = (byte)(NUD_PositionUnused.Value ?? 0);
        entity.IsInteractedToday = CHK_InteractedToday.IsChecked == true;
    }
}
