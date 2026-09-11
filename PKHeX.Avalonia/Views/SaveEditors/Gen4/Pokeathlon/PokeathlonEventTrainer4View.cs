using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4.Pokeathlon;

/// <summary>
/// A recorded opponent trainer (port of the WinForms <c>PokeathlonEventTrainer4Editor</c>).
/// </summary>
public sealed class PokeathlonEventTrainer4View : StackPanel
{
    private readonly TextBox TB_OT = UiFactory.Text("TB_OT", 7, 130);
    private readonly TextBox TB_TID16 = UiFactory.Text("TB_TID16", 5, 80);
    private readonly TextBox TB_SID16 = UiFactory.Text("TB_SID16", 5, 80);
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 150);

    public PokeathlonEventTrainer4View(string caption)
    {
        Orientation = Orientation.Horizontal;
        Spacing = 4;
        VerticalAlignment = VerticalAlignment.Center;

        Children.Add(UiFactory.Label("L_Trainer", caption));
        Children.Add(TB_OT);
        Children.Add(TB_TID16);
        Children.Add(TB_SID16);
        Children.Add(CB_Language);

        // The "no trainer" entry uses species index 0's blank name, as in WinForms.
        var available = GameInfo.LanguageDataSource(4, EntityContext.Gen4);
        var languages = new List<ComboItem>(available.Count + 1) { new(GameInfo.Strings.specieslist[0], 0) };
        languages.AddRange(available);
        CB_Language.SetItems(languages);
    }

    public void LoadObject(PokeathlonEventTrainer4 trainer)
    {
        TB_OT.Text = trainer.OriginalTrainerName;
        TB_TID16.Text = trainer.TID16.ToString("00000");
        TB_SID16.Text = trainer.SID16.ToString("00000");
        CB_Language.SetValue(trainer.Language);
    }

    public void SaveObject(PokeathlonEventTrainer4 trainer)
    {
        trainer.OriginalTrainerName = TB_OT.Text ?? string.Empty;
        trainer.TID16 = (ushort)Math.Min(ushort.MaxValue, Util.ToUInt32(TB_TID16.Text));
        trainer.SID16 = (ushort)Math.Min(ushort.MaxValue, Util.ToUInt32(TB_SID16.Text));
        trainer.Language = (byte)(CB_Language.GetSelectedItem()?.Value ?? 0);
    }
}
