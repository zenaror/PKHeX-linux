using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4.Pokeathlon;

/// <summary>
/// One Pokéathlon record row: the score and the three entrants (port of <c>PokeathlonEventRecord4Editor</c>).
/// </summary>
public sealed class PokeathlonEventRecord4View : StackPanel
{
    private readonly NumericUpDown NUD_Record = UiFactory.NumericUpDown("NUD_Record", 0, ushort.MaxValue, 135);
    private readonly PokeathlonSpeciesForm4View[] Entries = [new(), new(), new()];

    public PokeathlonEventRecord4View()
    {
        Orientation = Orientation.Vertical;
        Spacing = 2;

        var header = UiFactory.Row(UiFactory.Label("L_Record", "Record:"), NUD_Record);
        Children.Add(header);
        foreach (var e in Entries)
            Children.Add(e);
    }

    public void LoadObject(PokeathlonEventRecord4 entity)
    {
        NUD_Record.SetValueClamped(entity.Record);
        Entries[0].LoadValues(entity.Entry0.Species, entity.Entry0.Form);
        Entries[1].LoadValues(entity.Entry1.Species, entity.Entry1.Form);
        Entries[2].LoadValues(entity.Entry2.Species, entity.Entry2.Form);
    }

    /// <summary>Caption for the box around record <paramref name="index"/>, built from the translated "Record:" label.</summary>
    public static string GetGroupCaption(int index)
    {
        var text = Translator.TranslateText("SAV_Pokeathlon4.L_Record", "Record:", MainWindow.CurrentLanguage);
        return $"{text.TrimEnd(':', ' ')} {index + 1}";
    }

    public void SaveObject(PokeathlonEventRecord4 entity)
    {
        entity.Record = (ushort)(NUD_Record.Value ?? 0);
        entity.Entry0 = new SpeciesForm10 { Species = Entries[0].Species, Form = Entries[0].Form };
        entity.Entry1 = new SpeciesForm10 { Species = Entries[1].Species, Form = Entries[1].Form };
        entity.Entry2 = new SpeciesForm10 { Species = Entries[2].Species, Form = Entries[2].Form };
    }
}
