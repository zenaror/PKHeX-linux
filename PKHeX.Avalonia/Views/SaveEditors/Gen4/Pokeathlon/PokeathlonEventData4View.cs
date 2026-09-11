using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4.Pokeathlon;

/// <summary>
/// The five best records of one event, plus the attempt counter (port of <c>PokeathlonEventData4Editor</c>).
/// </summary>
public sealed class PokeathlonEventData4View : StackPanel
{
    private readonly NumericUpDown NUD_Attempts = UiFactory.NumericUpDown("NUD_Attempts", 0, PokeathlonEventData4.MaxAttempts, 130);
    private readonly PokeathlonEventRecord4View[] EventRecords;

    public PokeathlonEventData4View()
    {
        Orientation = Orientation.Vertical;
        Spacing = 6;

        EventRecords = new PokeathlonEventRecord4View[PokeathlonEventData4.MaxRecord];
        for (int i = 0; i < EventRecords.Length; i++)
            EventRecords[i] = new PokeathlonEventRecord4View($"#{i + 1}");

        Children.Add(UiFactory.Row(UiFactory.Label("L_Attempts", "Attempts:"), NUD_Attempts));
        foreach (var r in EventRecords)
            Children.Add(new GroupBoxView($"GB_Record{System.Array.IndexOf(EventRecords, r)}", $"Record {System.Array.IndexOf(EventRecords, r) + 1}", r));
    }

    public void LoadObject(PokeathlonEventData4 entity)
    {
        NUD_Attempts.SetValueClamped(entity.Attempts);
        for (int i = 0; i < EventRecords.Length; i++)
            EventRecords[i].LoadObject(entity.GetRecord(i));
    }

    public void SaveObject(PokeathlonEventData4 entity)
    {
        entity.Attempts = (uint)(NUD_Attempts.Value ?? 0);
        for (int i = 0; i < EventRecords.Length; i++)
            EventRecords[i].SaveObject(entity.GetRecord(i));
    }
}
