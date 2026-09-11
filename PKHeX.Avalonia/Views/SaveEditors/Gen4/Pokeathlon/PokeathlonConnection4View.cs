using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4.Pokeathlon;

/// <summary>
/// The multiplayer half of an event: the same five records plus the opponents that took part
/// (port of the WinForms <c>PokeathlonConnection4Editor</c>).
/// </summary>
public sealed class PokeathlonConnection4View : StackPanel
{
    private readonly NumericUpDown NUD_Attempts = UiFactory.NumericUpDown("NUD_Attempts", 0, PokeathlonEventData4.MaxAttempts, 130);
    private readonly PokeathlonEventRecord4View[] EventRecords;
    private readonly PokeathlonEventTrainer4View[] Trainers;

    public PokeathlonConnection4View()
    {
        Orientation = Orientation.Vertical;
        Spacing = 6;

        int count = (int)PokeathlonEventData4.MaxRecord;
        EventRecords = new PokeathlonEventRecord4View[count];
        Trainers = new PokeathlonEventTrainer4View[count];

        Children.Add(UiFactory.Row(UiFactory.Label("L_Attempts", "Attempts:"), NUD_Attempts));
        for (int i = 0; i < count; i++)
        {
            EventRecords[i] = new PokeathlonEventRecord4View($"#{i + 1}");
            Trainers[i] = new PokeathlonEventTrainer4View("Trainer:");
            Children.Add(new GroupBoxView($"GB_Connection{i}", $"Record {i + 1}", UiFactory.Column(EventRecords[i], Trainers[i])));
        }
    }

    public void LoadObject(PokeathlonConnection4 entity)
    {
        var inner = entity.Inner;
        NUD_Attempts.SetValueClamped(inner.Attempts);
        for (int i = 0; i < EventRecords.Length; i++)
        {
            EventRecords[i].LoadObject(inner.GetRecord(i));
            Trainers[i].LoadObject(entity.GetTrainer(i));
        }
    }

    public void SaveObject(PokeathlonConnection4 entity)
    {
        var inner = entity.Inner;
        inner.Attempts = (uint)(NUD_Attempts.Value ?? 0);
        for (int i = 0; i < EventRecords.Length; i++)
        {
            EventRecords[i].SaveObject(inner.GetRecord(i));
            Trainers[i].SaveObject(entity.GetTrainer(i));
        }
    }
}
