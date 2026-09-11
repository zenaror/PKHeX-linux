using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using PKHeX.Avalonia.Localization;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Height / Weight / Scale / CP editor (port of the WinForms <c>SizeCP</c>).
/// </summary>
public sealed class SizeCPView : Grid
{
    private IScaledSize? ss;
    private IScaledSize3? scale;
    private IScaledSizeValue? sv;
    private ICombatPower? pk;
    private bool Loading;
    private bool IsScaleDetailed;

    private static string[] SizeClass = Enum.GetNames<PokeSize>();
    private static string[] SizeClassDetailed = Enum.GetNames<PokeSizeDetailed>();

    private readonly TextBlock L_Height = UiFactory.Label("L_Height", "Height:");
    private readonly TextBlock L_Weight = UiFactory.Label("L_Weight", "Weight:");
    private readonly TextBlock L_Scale = UiFactory.Label("L_Scale", "Scale:");
    private readonly TextBlock L_CP = UiFactory.Label("L_CP", "CP:");
    private readonly NumericUpDown NUD_HeightScalar = UiFactory.NumericUpDown("NUD_HeightScalar", 0, 255, 80);
    private readonly NumericUpDown NUD_WeightScalar = UiFactory.NumericUpDown("NUD_WeightScalar", 0, 255, 80);
    private readonly NumericUpDown NUD_Scale = UiFactory.NumericUpDown("NUD_Scale", 0, 255, 80);
    private readonly TextBox TB_HeightAbs = UiFactory.Text("TB_HeightAbs", 12, 80);
    private readonly TextBox TB_WeightAbs = UiFactory.Text("TB_WeightAbs", 12, 80);
    private readonly TextBlock L_SizeH = UiFactory.Label("L_SizeH", "XL");
    private readonly TextBlock L_SizeW = UiFactory.Label("L_SizeW", "XS");
    private readonly TextBlock L_SizeS = UiFactory.Label("L_SizeS", "XXXS");
    private readonly NumericTextBox MT_CP = UiFactory.Numeric("MT_CP", 5, 60);
    private readonly CheckBox CHK_Auto = UiFactory.Check("CHK_Auto", "Auto");
    private readonly StackPanel FLP_Scale3;
    private readonly StackPanel FLP_CP;

    private readonly bool IsSetUp;

    public SizeCPView()
    {
        ColumnSpacing = 6;
        RowSpacing = 3;
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (int i = 0; i < 4; i++)
            RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var height = UiFactory.Row(NUD_HeightScalar, TB_HeightAbs, L_SizeH);
        var weight = UiFactory.Row(NUD_WeightScalar, TB_WeightAbs, L_SizeW);
        FLP_Scale3 = UiFactory.Row(NUD_Scale, L_SizeS);
        FLP_CP = UiFactory.Row(MT_CP, CHK_Auto);
        CHK_Auto.IsChecked = true;

        UiFactory.AddFormRow(this, 0, L_Height, height);
        UiFactory.AddFormRow(this, 1, L_Weight, weight);
        UiFactory.AddFormRow(this, 2, L_Scale, FLP_Scale3);
        UiFactory.AddFormRow(this, 3, L_CP, FLP_CP);

        CHK_Auto.IsCheckedChanged += (_, _) => UpdateFlagState();
        MT_CP.OnTextChanged(_ => MT_CP_TextChanged());
        NUD_HeightScalar.ValueChanged += (_, _) => NUD_HeightScalar_ValueChanged();
        NUD_WeightScalar.ValueChanged += (_, _) => NUD_WeightScalar_ValueChanged();
        NUD_Scale.ValueChanged += (_, _) => NUD_Scale_ValueChanged();
        TB_HeightAbs.OnTextChanged(_ => TB_HeightAbs_TextChanged());
        TB_WeightAbs.OnTextChanged(_ => TB_WeightAbs_TextChanged());
        foreach (var nud in new[] { NUD_HeightScalar, NUD_WeightScalar, NUD_Scale })
            nud.AttachClick(mods => ClickScalarEntry(nud, mods));
        IsSetUp = true;
    }

    public static void ResetSizeLocalizations(string language)
    {
        SizeClass = Translator.GetEnumTranslation<PokeSize>(language);
        SizeClassDetailed = Translator.GetEnumTranslation<PokeSizeDetailed>(language);
    }

    public void LoadPKM(PKM entity)
    {
        pk = entity as ICombatPower;
        ss = entity as IScaledSize;
        sv = entity as IScaledSizeValue;
        scale = entity as IScaledSize3;
        IsScaleDetailed = entity is PK9; // not PA9
        if (ss is null)
            return;
        TryResetStats();
    }

    public void TryResetStats()
    {
        if (!IsSetUp)
            return;

        if (CHK_Auto.IsChecked == true)
            ResetCalculatedStats();
        LoadStoredValues();
    }

    private void ResetCalculatedStats()
    {
        sv?.ResetHeight();
        sv?.ResetWeight();
        pk?.ResetCP();
    }

    private static string GetString(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private void LoadStoredValues()
    {
        Loading = true;
        if (ss is not null)
        {
            NUD_HeightScalar.Value = ss.HeightScalar;
            NUD_WeightScalar.Value = ss.WeightScalar;
        }
        if (sv is not null)
        {
            TB_HeightAbs.Text = GetString(sv.HeightAbsolute);
            TB_WeightAbs.Text = GetString(sv.WeightAbsolute);
        }
        if (scale is not null)
        {
            NUD_Scale.Value = scale.Scale;
        }
        if (pk is not null)
        {
            MT_CP.Text = Math.Min(65535, pk.Stat_CP).ToString();
        }
        Loading = false;
    }

    private void UpdateFlagState()
    {
        if (CHK_Auto.IsChecked != true)
            return;

        ResetCalculatedStats();
        LoadStoredValues();
    }

    private void MT_CP_TextChanged()
    {
        if (pk is not null && int.TryParse(MT_CP.Text, out var cp))
            pk.Stat_CP = Math.Min(65535, cp);
    }

    private void NUD_HeightScalar_ValueChanged()
    {
        if (ss is not null)
        {
            if (!Loading)
            {
                ss.HeightScalar = (byte)(NUD_HeightScalar.Value ?? 0);
                if (ss is PA8) // Height copied to Scale
                    NUD_Scale.Value = ss.HeightScalar;
            }
            var label = L_SizeH;
            var value = ss.HeightScalar;
            label.Text = SizeClass[(int)PokeSizeUtil.GetSizeRating(value)];
            SetLabelColorHeightWeight(label);
        }

        if (CHK_Auto.IsChecked != true || Loading || sv is null)
            return;
        sv.ResetHeight();
        sv.ResetWeight();
        TB_HeightAbs.Text = GetString(sv.HeightAbsolute);
        TB_WeightAbs.Text = GetString(sv.WeightAbsolute);
    }

    private void NUD_WeightScalar_ValueChanged()
    {
        if (ss is not null)
        {
            if (!Loading)
                ss.WeightScalar = (byte)(NUD_WeightScalar.Value ?? 0);
            var label = L_SizeW;
            var value = ss.WeightScalar;
            label.Text = SizeClass[(int)PokeSizeUtil.GetSizeRating(value)];
            SetLabelColorHeightWeight(label);
        }

        if (CHK_Auto.IsChecked != true || Loading || sv is null)
            return;
        sv.ResetWeight();
        TB_WeightAbs.Text = GetString(sv.WeightAbsolute);
    }

    private void NUD_Scale_ValueChanged()
    {
        if (scale is null)
            return;
        if (!Loading)
        {
            scale.Scale = (byte)(NUD_Scale.Value ?? 0);
            if (scale is PA8) // Height copied to Scale
                NUD_HeightScalar.Value = scale.Scale;
        }

        var label = L_SizeS;
        var value = scale.Scale;
        label.Text = IsScaleDetailed
            ? SizeClassDetailed[(int)PokeSizeDetailedUtil.GetSizeRating(value)]
            : SizeClass[(int)PokeSizeUtil.GetSizeRating(value)];

        if (value is 0 or 255) // Tiny or Jumbo Mark possible.
            label.SetForeColor(ColorUtilAvalonia.ColorWarn);
        else
            label.ResetForeColor();
    }

    private void SetLabelColorHeightWeight(TextBlock label)
    {
        if (scale is not null)
            label.SetForeColor(System.Drawing.Color.Gray); // not indicative of actual size
        else
            label.ResetForeColor();
    }

    private void TB_HeightAbs_TextChanged()
    {
        if (sv is null || Loading)
            return;
        if (CHK_Auto.IsChecked == true)
            sv.ResetHeight();
        else if (float.TryParse(TB_HeightAbs.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            sv.HeightAbsolute = result;
    }

    private void TB_WeightAbs_TextChanged()
    {
        if (sv is null || Loading)
            return;
        if (CHK_Auto.IsChecked == true)
            sv.ResetWeight();
        else if (float.TryParse(TB_WeightAbs.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            sv.WeightAbsolute = result;
    }

    public void ToggleVisibility(PKM entity)
    {
        bool isCP = entity is ICombatPower;
        bool isAbsolute = entity is IScaledSizeValue;
        bool isScale3 = entity is IScaledSize3;
        MT_CP.IsVisible = L_CP.IsVisible = isCP;
        TB_HeightAbs.IsVisible = TB_WeightAbs.IsVisible = isAbsolute;
        L_Scale.IsVisible = FLP_Scale3.IsVisible = isScale3;
        FLP_CP.IsVisible = isCP || isAbsolute; // Auto checkbox
    }

    private static void ClickScalarEntry(NumericUpDown nud, KeyModifiers mods)
    {
        if (mods != KeyModifiers.Control)
            return;
        nud.Value = PokeSizeUtil.GetRandomScalar();
    }
}
