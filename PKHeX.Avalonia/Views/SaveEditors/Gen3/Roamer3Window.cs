using System;
using Avalonia.Controls;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen3;

/// <summary>
/// Roaming Pokémon editor for Gen 3 (port of the WinForms <c>SAV_Roamer3</c>).
/// </summary>
public sealed class Roamer3Window : SaveEditorWindow
{
    private readonly Roamer3 Reader;
    private readonly SAV3 SAV;

    private readonly TextBlock Label_Species = UiFactory.Label("Label_Species", "Species:");
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 170);
    private readonly TextBlock Label_PID = UiFactory.Label("Label_PID", "PID:");
    private readonly NumericTextBox TB_PID = UiFactory.Numeric("TB_PID", 8, 90, hex: true);
    private readonly CheckBox CHK_Shiny = UiFactory.Check("CHK_Shiny", "Shiny?");
    private readonly CheckBox CHK_Active = UiFactory.Check("CHK_Active", "Roaming (Active)");
    private readonly TextBlock L_Level = UiFactory.Label("L_Level", "Level:");
    private readonly NumericUpDown NUD_Level = UiFactory.NumericUpDown("NUD_Level", 0, 100, 90);
    private readonly TextBlock L_HP = UiFactory.Label("L_HP", "HP:");
    private readonly NumericUpDown NUD_HP = UiFactory.NumericUpDown("NUD_HP", 0, ushort.MaxValue, 110);

    private readonly TextBlock Label_HP = UiFactory.Label("Label_HP", "HP:");
    private readonly NumericTextBox TB_HPIV = UiFactory.Numeric("TB_HPIV", 2, 50);
    private readonly TextBlock Label_ATK = UiFactory.Label("Label_ATK", "Atk:");
    private readonly NumericTextBox TB_ATKIV = UiFactory.Numeric("TB_ATKIV", 2, 50);
    private readonly TextBlock Label_DEF = UiFactory.Label("Label_DEF", "Def:");
    private readonly NumericTextBox TB_DEFIV = UiFactory.Numeric("TB_DEFIV", 2, 50);
    private readonly TextBlock Label_SPA = UiFactory.Label("Label_SPA", "SpA:");
    private readonly NumericTextBox TB_SPAIV = UiFactory.Numeric("TB_SPAIV", 2, 50);
    private readonly TextBlock Label_SPD = UiFactory.Label("Label_SPD", "SpD:");
    private readonly NumericTextBox TB_SPDIV = UiFactory.Numeric("TB_SPDIV", 2, 50);
    private readonly TextBlock Label_SPE = UiFactory.Label("Label_SPE", "Spe:");
    private readonly NumericTextBox TB_SPEIV = UiFactory.Numeric("TB_SPEIV", 2, 50);

    public Roamer3Window(SAV3 sav) : base("SAV_Roamer3", "Roamer Editor")
    {
        Reader = new Roamer3(sav.LargeBlock.RoamerData, sav is not SAV3E);
        SAV = sav;
        CHK_Shiny.IsEnabled = false; // display only, derived from the PID

        CB_Species.SetItems(GameInfo.FilteredSources.Species);

        var top = UiFactory.FormGrid(5);
        UiFactory.AddFormRow(top, 0, Label_Species, CB_Species);
        UiFactory.AddFormRow(top, 1, Label_PID, UiFactory.Row(TB_PID, CHK_Shiny));
        UiFactory.AddFormRow(top, 2, null, CHK_Active);
        UiFactory.AddFormRow(top, 3, L_Level, NUD_Level);
        UiFactory.AddFormRow(top, 4, L_HP, NUD_HP);

        var ivs = UiFactory.FormGrid(6);
        UiFactory.AddFormRow(ivs, 0, Label_HP, TB_HPIV);
        UiFactory.AddFormRow(ivs, 1, Label_ATK, TB_ATKIV);
        UiFactory.AddFormRow(ivs, 2, Label_DEF, TB_DEFIV);
        UiFactory.AddFormRow(ivs, 3, Label_SPA, TB_SPAIV);
        UiFactory.AddFormRow(ivs, 4, Label_SPD, TB_SPDIV);
        UiFactory.AddFormRow(ivs, 5, Label_SPE, TB_SPEIV);

        SetBody(UiFactory.Row(top, new GroupBoxView("GB_IVs", "IVs", ivs)));

        TB_PID.OnTextChanged(_ => CHK_Shiny.IsChecked = Roamer3.IsShiny(TB_PID.UIntValue, SAV));
        LoadData();
    }

    private void LoadData()
    {
        TB_PID.Text = Reader.PID.ToString("X8");
        CHK_Shiny.IsChecked = Roamer3.IsShiny(Reader.PID, SAV);
        CB_Species.SetValue(Reader.Species);

        TB_HPIV.Text = Reader.IV_HP.ToString();
        TB_ATKIV.Text = Reader.IV_ATK.ToString();
        TB_DEFIV.Text = Reader.IV_DEF.ToString();
        TB_SPEIV.Text = Reader.IV_SPE.ToString();
        TB_SPAIV.Text = Reader.IV_SPA.ToString();
        TB_SPDIV.Text = Reader.IV_SPD.ToString();

        CHK_Active.IsChecked = Reader.IsActive;
        NUD_Level.Value = Math.Min(Reader.CurrentLevel, NUD_Level.Maximum);
        NUD_HP.Value = Math.Min(Reader.HP_Current, NUD_HP.Maximum);
    }

    protected override void OnSave()
    {
        Reader.PID = TB_PID.UIntValue;
        Reader.Species = (ushort)CB_Species.GetValue();
        Reader.SetIVs(
        [
            TB_HPIV.IntValue,
            TB_ATKIV.IntValue,
            TB_DEFIV.IntValue,
            TB_SPEIV.IntValue,
            TB_SPAIV.IntValue,
            TB_SPDIV.IntValue,
        ]);
        Reader.IsActive = CHK_Active.IsChecked == true;
        Reader.CurrentLevel = (byte)(NUD_Level.Value ?? 0);
        Reader.HP_Current = (ushort)(NUD_HP.Value ?? 0);
        Close();
    }
}
