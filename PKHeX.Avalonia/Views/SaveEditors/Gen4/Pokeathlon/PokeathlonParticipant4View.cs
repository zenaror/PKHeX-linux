using System;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4.Pokeathlon;

/// <summary>
/// One entrant of a course record (port of the WinForms <c>PokeathlonParticipant4Editor</c>).
/// </summary>
public sealed class PokeathlonParticipant4View : StackPanel
{
    private readonly PokeathlonSpeciesForm4View UC_SpeciesForm = new();
    private readonly GenderToggleView GT_Gender = new() { Name = "GT_Gender" };
    private readonly CheckBox CHK_IsShiny = UiFactory.Check("CHK_IsShiny", "☆");
    private readonly TextBox TB_PID = UiFactory.Text("TB_PID", 8, 100);
    private readonly TextBox TB_TID16 = UiFactory.Text("TB_TID16", 5, 80);
    private readonly TextBox TB_SID16 = UiFactory.Text("TB_SID16", 5, 80);

    private bool IsLoading;

    public PokeathlonParticipant4View(string caption)
    {
        Orientation = Orientation.Horizontal;
        Spacing = 4;
        VerticalAlignment = VerticalAlignment.Center;

        Children.Add(UiFactory.Label("L_Participant", caption));
        Children.Add(UC_SpeciesForm);
        Children.Add(GT_Gender);
        Children.Add(CHK_IsShiny);
        Children.Add(TB_PID);
        Children.Add(TB_TID16);
        Children.Add(TB_SID16);

        UC_SpeciesForm.ValueChanged += (_, _) => WriteBack();
        GT_Gender.Click += (_, _) => WriteBack();
        CHK_IsShiny.IsCheckedChanged += (_, _) => ShinyChanged();
    }

    public void LoadObject(PokeathlonParticipant4 entity)
    {
        IsLoading = true;
        UC_SpeciesForm.DisplayGender = entity.Gender;
        UC_SpeciesForm.DisplayShiny = entity.IsShiny;
        UC_SpeciesForm.LoadValues(entity.Species, entity.Form);
        GT_Gender.Gender = entity.Gender;
        CHK_IsShiny.IsChecked = entity.IsShiny;
        TB_PID.Text = entity.EncryptionConstant.ToString("X8");
        TB_TID16.Text = entity.TID16.ToString("00000");
        TB_SID16.Text = entity.SID16.ToString("00000");
        IsLoading = false;
    }

    public void SaveObject(PokeathlonParticipant4 entity)
    {
        entity.Species = UC_SpeciesForm.Species;
        entity.Form = UC_SpeciesForm.Form;
        entity.Gender = GT_Gender.Gender;
        entity.IsShiny = CHK_IsShiny.IsChecked == true;
        entity.EncryptionConstant = Util.GetHexValue(TB_PID.Text);
        entity.TID16 = (ushort)Math.Min(ushort.MaxValue, Util.ToUInt32(TB_TID16.Text));
        entity.SID16 = (ushort)Math.Min(ushort.MaxValue, Util.ToUInt32(TB_SID16.Text));
    }

    private void ShinyChanged()
    {
        if (IsLoading)
            return;
        UC_SpeciesForm.DisplayShiny = CHK_IsShiny.IsChecked == true;
        WriteBack();
    }

    /// <summary>Keeps the sprite in step with the gender and shiny toggles.</summary>
    private void WriteBack()
    {
        if (IsLoading)
            return;
        UC_SpeciesForm.DisplayGender = GT_Gender.Gender;
        UC_SpeciesForm.DisplayShiny = CHK_IsShiny.IsChecked == true;
    }
}
