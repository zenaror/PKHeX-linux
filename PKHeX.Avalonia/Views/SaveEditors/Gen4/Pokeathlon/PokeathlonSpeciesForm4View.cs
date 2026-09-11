using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4.Pokeathlon;

/// <summary>
/// Species + form picker with a live sprite (port of the WinForms <c>PokeathlonSpeciesForm4Editor</c>).
/// </summary>
public sealed class PokeathlonSpeciesForm4View : StackPanel
{
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 170);
    private readonly ComboBox CB_Form = UiFactory.Combo("CB_Form", 130);
    private readonly Image PB_Sprite = UiFactory.Picture("PB_Sprite", 56);

    private bool IsLoading;
    private byte SpriteGender;
    private bool SpriteShiny;

    public event EventHandler? ValueChanged;

    public ushort Species => (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
    public byte Form => (byte)Math.Max(CB_Form.SelectedIndex, 0);

    public byte DisplayGender
    {
        get => SpriteGender;
        set
        {
            SpriteGender = value > 2 ? (byte)2 : value;
            RefreshSprite();
        }
    }

    public bool DisplayShiny
    {
        get => SpriteShiny;
        set
        {
            SpriteShiny = value;
            RefreshSprite();
        }
    }

    public PokeathlonSpeciesForm4View()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 4;
        VerticalAlignment = VerticalAlignment.Center;
        Children.Add(PB_Sprite);
        Children.Add(CB_Species);
        Children.Add(CB_Form);

        CB_Species.SetItems(GameInfo.FilteredSources.Species.ToList());
        CB_Species.SelectionChanged += (_, _) => { LoadForms(); OnValueChanged(); };
        CB_Form.SelectionChanged += (_, _) => { RefreshSprite(); OnValueChanged(); };
        LoadValues(0, 0);
    }

    public void LoadValues(ushort species, byte form)
    {
        IsLoading = true;
        CB_Species.SetValue(species);
        LoadForms(form);
        RefreshSprite();
        IsLoading = false;
    }

    private void LoadForms(byte selectedForm = 0)
    {
        var forms = FormConverter.GetFormList(Species, GameInfo.Strings.types, GameInfo.Strings.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen4);
        var source = GetFormSource(forms);
        CB_Form.SetItems(source);
        CB_Form.SelectedIndex = Math.Clamp(selectedForm, 0, source.Count - 1);
        RefreshSprite();
    }

    private void RefreshSprite()
    {
        var shiny = SpriteShiny ? Shiny.Always : Shiny.Never;
        var sprite = SpriteUtil.GetSprite(Species, Form, SpriteGender, 0, 0, false, shiny, EntityContext.Gen4);
        PB_Sprite.Source = sprite.ToAvaloniaBitmapAndDispose();
    }

    private void OnValueChanged()
    {
        if (!IsLoading)
            ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private static List<ComboItem> GetFormSource(IReadOnlyList<string> forms)
    {
        var result = new List<ComboItem>(forms.Count);
        for (int i = 0; i < forms.Count; i++)
            result.Add(new ComboItem(forms[i], i));
        return result;
    }
}
