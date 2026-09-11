using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Drawing;
using PKHeX.Drawing.Misc;
using PKHeX.Drawing.PokeSprite;
using Color = System.Drawing.Color;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Stat editor (port of the WinForms <c>StatEditor</c>).
/// </summary>
public sealed class StatEditorView : StackPanel
{
    private static Color EVsInvalid => Color.Red;
    private static Color EVsMaxed => Color.LightGreen;
    private static Color EVsFishy => Color.Yellow;
    private static Color StatIncreased => ColorUtilAvalonia.ColorPlus;
    private static Color StatDecreased => ColorUtilAvalonia.ColorMinus;
    private static Color StatHyperTrained => Color.LightGreen;

    public IMainEditor MainEditor { private get; set; } = null!;
    public bool HaX { get => CHK_HackedStats.IsEnabled; set => CHK_HackedStats.IsEnabled = CHK_HackedStats.IsVisible = value; }

    private StatEditorStatOrder StatOrder;

    // Grid
    private readonly Grid TLP_StatGrid = new() { ColumnSpacing = 4, RowSpacing = 2 };
    public readonly CheckBox CHK_HackedStats = UiFactory.Check("CHK_HackedStats", "Hacked");
    private readonly TextBlock Label_Base = UiFactory.Label("Label_Base", "Base");
    private readonly TextBlock Label_IVs = UiFactory.Label("Label_IVs", "IVs");
    private readonly TextBlock Label_EVs = UiFactory.Label("Label_EVs", "EVs");
    private readonly TextBlock Label_AVs = UiFactory.Label("Label_AVs", "AVs");
    private readonly TextBlock Label_GVs = UiFactory.Label("Label_GVs", "GVs");
    private readonly TextBlock Label_Stats = UiFactory.Label("Label_Stats", "Stats");
    private readonly TextBlock Label_HP = UiFactory.Label("Label_HP", "HP:", true);
    private readonly TextBlock Label_ATK = UiFactory.Label("Label_ATK", "Atk:", true);
    private readonly TextBlock Label_DEF = UiFactory.Label("Label_DEF", "Def:", true);
    private readonly TextBlock Label_SPE = UiFactory.Label("Label_SPE", "Spe:", true);
    private readonly TextBlock Label_SPA = UiFactory.Label("Label_SPA", "SpA:", true);
    private readonly TextBlock Label_SPC = UiFactory.Label("Label_SPC", "SpC:", true);
    private readonly TextBlock Label_SPD = UiFactory.Label("Label_SPD", "SpD:", true);
    private readonly TextBlock Label_Total = UiFactory.Label("Label_Total", "Total:");
    private readonly StackPanel FLP_SPA;
    private readonly TextBlock[] L_Stats;
    private readonly NumericTextBox[] MT_EVs, MT_IVs, MT_AVs, MT_GVs, MT_Stats;
    private readonly TextBox[] MT_Base;
    private readonly TextBox TB_BST = ReadOnlyBox("TB_BST", 3);
    private readonly TextBox TB_IVTotal = ReadOnlyBox("TB_IVTotal", 3);
    private readonly TextBox TB_EVTotal = ReadOnlyBox("TB_EVTotal", 4);
    private readonly TextBox TB_AVTotal = ReadOnlyBox("TB_AVTotal", 4);
    private readonly TextBlock L_Potential = UiFactory.Label("L_Potential", "1234");

    // Extras
    private readonly StackPanel FLP_HPType;
    private readonly TextBlock Label_HiddenPowerPrefix = UiFactory.Label("Label_HiddenPowerPrefix", "Hidden Power Type:");
    public readonly ComboBox CB_HPType = UiFactory.Combo("CB_HPType", 110);
    private readonly TextBlock Label_HiddenPowerPower = UiFactory.Label("Label_HiddenPowerPower", "60");
    private readonly StackPanel FLP_Characteristic;
    private readonly TextBlock Label_CharacteristicPrefix = UiFactory.Label("Label_CharacteristicPrefix", "Characteristic:");
    private readonly TextBlock L_Characteristic = UiFactory.Label("L_Characteristic", "(char)");
    private readonly StackPanel FLP_TeraType;
    private readonly TypeBox PB_TeraType1 = new() { Name = "PB_TeraType1" };
    private readonly TypeBox PB_TeraType2 = new() { Name = "PB_TeraType2" };
    private readonly TextBlock L_TeraTypeOriginal = UiFactory.Label("L_TeraTypeOriginal", "Original Tera Type:", true);
    public readonly ComboBox CB_TeraTypeOriginal = UiFactory.Combo("CB_TeraTypeOriginal", 100);
    private readonly TextBlock L_TeraTypeOverride = UiFactory.Label("L_TeraTypeOverride", "Override Tera Type:", true);
    public readonly ComboBox CB_TeraTypeOverride = UiFactory.Combo("CB_TeraTypeOverride", 100);
    private readonly Image PB_TeraType = UiFactory.Picture("PB_TeraType", 32);
    private global::Avalonia.Media.Imaging.Bitmap? TeraGemImage;
    private readonly Button BTN_RandomIVs = UiFactory.Button("BTN_RandomIVs", "Randomize IVs");
    private readonly Button BTN_RandomEVs = UiFactory.Button("BTN_RandomEVs", "Randomize EVs");
    private readonly Button BTN_RandomAVs = UiFactory.Button("BTN_RandomAVs", "Randomize AVs");
    private readonly StackPanel FLP_DynamaxLevel;
    private readonly TextBlock L_DynamaxLevel = UiFactory.Label("L_DynamaxLevel", "Dynamax Level:", true);
    public readonly ComboBox CB_DynamaxLevel = UiFactory.StringCombo("CB_DynamaxLevel", 60, "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10");
    public readonly CheckBox CHK_Gigantamax = UiFactory.Check("CHK_Gigantamax", "Gigantamax");
    private readonly StackPanel FLP_AlphaNoble;
    public readonly CheckBox CHK_IsAlpha = UiFactory.Check("CHK_IsAlpha", "Alpha");
    public readonly CheckBox CHK_IsNoble = UiFactory.Check("CHK_IsNoble", "Noble");

    private static TextBox ReadOnlyBox(string name, int maxLength) => new()
    {
        Name = name,
        IsReadOnly = true,
        MaxLength = maxLength,
        Width = 48,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        Padding = new global::Avalonia.Thickness(4, 2),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static NumericTextBox[] Make(string prefix, int maxLength, double width = 44)
    {
        string[] names = ["HP", "ATK", "DEF", "SPE", "SPA", "SPD"];
        var result = new NumericTextBox[6];
        for (int i = 0; i < 6; i++)
            result[i] = UiFactory.Numeric($"{prefix}{names[i]}", maxLength, width);
        return result;
    }

    public StatEditorView()
    {
        Orientation = Orientation.Vertical;
        Spacing = 6;

        MT_IVs = Make("TB_IV", 2);
        MT_EVs = Make("TB_EV", 3);
        MT_AVs = Make("TB_AV", 3);
        MT_GVs = Make("TB_GV", 3);
        MT_Stats = Make("Stat_", 5, 52);
        MT_Base = new TextBox[6];
        string[] names = ["HP", "ATK", "DEF", "SPE", "SPA", "SPD"];
        for (int i = 0; i < 6; i++)
        {
            var tb = ReadOnlyBox($"TB_Base{names[i]}", 3);
            tb.IsEnabled = false;
            MT_Base[i] = tb;
        }
        L_Stats = [Label_HP, Label_ATK, Label_DEF, Label_SPE, Label_SPA, Label_SPD];
        FLP_SPA = UiFactory.Row(Label_SPC, Label_SPA);
        FLP_SPA.Spacing = 0;

        BuildGrid();

        FLP_HPType = UiFactory.Row(Label_HiddenPowerPrefix, CB_HPType, Label_HiddenPowerPower);
        FLP_Characteristic = UiFactory.Row(Label_CharacteristicPrefix, L_Characteristic);
        var teraInner = UiFactory.Column(
            UiFactory.Row(PB_TeraType2, PB_TeraType1),
            UiFactory.Row(L_TeraTypeOriginal, CB_TeraTypeOriginal),
            UiFactory.Row(L_TeraTypeOverride, CB_TeraTypeOverride));
        FLP_TeraType = UiFactory.Row(teraInner, PB_TeraType);
        var buttons = UiFactory.Row(BTN_RandomIVs, BTN_RandomEVs, BTN_RandomAVs);
        buttons.HorizontalAlignment = HorizontalAlignment.Center;
        FLP_DynamaxLevel = UiFactory.Row(L_DynamaxLevel, CB_DynamaxLevel, CHK_Gigantamax);
        FLP_AlphaNoble = UiFactory.Row(CHK_IsAlpha, CHK_IsNoble);

        Children.Add(TLP_StatGrid);
        Children.Add(FLP_HPType);
        Children.Add(FLP_Characteristic);
        Children.Add(FLP_TeraType);
        Children.Add(buttons);
        Children.Add(FLP_DynamaxLevel);
        Children.Add(FLP_AlphaNoble);

        TB_IVHP.IsEnabled = true;
        foreach (var s in MT_Stats)
            s.IsEnabled = false;
        CHK_HackedStats.IsEnabled = false;

        // Events
        foreach (var iv in MT_IVs)
        {
            iv.OnTextChanged(s => UpdateIVs(s));
            iv.AttachClick(m => ClickIV(iv, m));
            iv.MouseWheelIncrement(1);
        }
        foreach (var ev in MT_EVs)
        {
            ev.OnTextChanged(s => UpdateEVs(s));
            ev.AttachClick(m => ClickEV(ev, m));
            ev.MouseWheelIncrement(4);
        }
        foreach (var av in MT_AVs)
        {
            av.OnTextChanged(s => UpdateAVs(s));
            av.AttachClick(m => ClickAV(av, m));
            av.MouseWheelIncrement(1);
        }
        foreach (var gv in MT_GVs)
        {
            gv.OnTextChanged(s => UpdateGVs(s));
            gv.AttachClick(m => ClickGV(gv, m));
            gv.MouseWheelIncrement(1);
        }
        foreach (var st in MT_Stats)
            st.OnTextChanged(s => UpdateHackedStatText(s));
        foreach (var l in L_Stats)
            l.AttachClick(m => ClickStatLabel(l, m));
        Label_SPC.AttachClick(m => ClickStatLabel(Label_SPC, m));
        CHK_HackedStats.IsCheckedChanged += (_, _) => UpdateHackedStats();
        CB_HPType.SelectionChanged += (_, _) => UpdateHPType();
        CB_TeraTypeOriginal.SelectionChanged += (_, _) => ChangeTeraType(CB_TeraTypeOriginal);
        CB_TeraTypeOverride.SelectionChanged += (_, _) => ChangeTeraType(CB_TeraTypeOverride);
        CHK_Gigantamax.IsCheckedChanged += (_, _) => { if (!ChangingFields) MainEditor.UpdateSprite(); };
        CHK_IsAlpha.IsCheckedChanged += (_, _) => { if (!ChangingFields) MainEditor.UpdateSprite(); };
        BTN_RandomIVs.Click += (_, _) => UpdateRandomIVs(MainWindow.CurrentModifiers);
        BTN_RandomEVs.Click += (_, _) => UpdateRandomEVs(MainWindow.CurrentModifiers);
        BTN_RandomAVs.Click += (_, _) => UpdateRandomAVs(MainWindow.CurrentModifiers);
        L_DynamaxLevel.AttachClick(_ => L_DynamaxLevel_Click());
        L_TeraTypeOriginal.AttachClick(_ => L_TeraTypeOriginal_Click());
        L_TeraTypeOverride.AttachClick(_ => L_TeraTypeOverride_Click());
        PB_TeraType1.AttachClickHandled(_ => SetOriginalTeraType(Entity.PersonalInfo.Type1));
        PB_TeraType2.AttachClickHandled(_ => SetOriginalTeraType(Entity.PersonalInfo.Type2));
        TB_EVTotal.SetForeColor(Color.Black);
    }

    private NumericTextBox TB_IVHP => MT_IVs[0];
    private NumericTextBox TB_IVSPD => MT_IVs[5];
    private NumericTextBox TB_EVSPA => MT_EVs[4];
    private NumericTextBox TB_EVSPD => MT_EVs[5];
    private NumericTextBox TB_IVATK => MT_IVs[1];

    private void BuildGrid()
    {
        var g = TLP_StatGrid;
        for (int c = 0; c < 7; c++)
            g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (int r = 0; r < 8; r++)
            g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        Add(CHK_HackedStats, 0, 0);
        Add(Label_Base, 0, 1); Add(Label_IVs, 0, 2); Add(Label_EVs, 0, 3); Add(Label_AVs, 0, 4); Add(Label_GVs, 0, 5); Add(Label_Stats, 0, 6);
        foreach (var l in new[] { Label_Base, Label_IVs, Label_EVs, Label_AVs, Label_GVs, Label_Stats })
            l.HorizontalAlignment = HorizontalAlignment.Center;

        // Row order: HP(1) ATK(2) DEF(3) SPA(4) SPD(5) SPE(6); array index order: HP ATK DEF SPE SPA SPD
        int[] rows = [1, 2, 3, 6, 4, 5];
        for (int i = 0; i < 6; i++)
        {
            var row = rows[i];
            Control label = i == 4 ? FLP_SPA : L_Stats[i];
            Add(label, row, 0);
            Add(MT_Base[i], row, 1);
            Add(MT_IVs[i], row, 2);
            Add(MT_EVs[i], row, 3);
            Add(MT_AVs[i], row, 4);
            Add(MT_GVs[i], row, 5);
            Add(MT_Stats[i], row, 6);
        }
        Add(Label_Total, 7, 0);
        Add(TB_BST, 7, 1);
        Add(TB_IVTotal, 7, 2);
        Add(TB_EVTotal, 7, 3);
        Add(TB_AVTotal, 7, 4);
        Add(L_Potential, 7, 6);
        L_Potential.HorizontalAlignment = HorizontalAlignment.Center;
        Label_SPC.IsVisible = false;
        return;

        void Add(Control c, int row, int col)
        {
            UiFactory.SetRowCol(c, row, col);
            if (c is TextBlock tb)
                tb.HorizontalAlignment = HorizontalAlignment.Right;
            g.Children.Add(c);
        }
    }

    public bool Valid
    {
        get
        {
            if (Entity.Format < 3)
                return true;
            if (CHK_HackedStats.IsChecked == true)
                return true;
            if (Entity is IAwakened a)
                return a.AwakeningAllValid();
            return Util.ToUInt32(TB_EVTotal.Text ?? "0") <= EffortValues.Max510;
        }
    }

    private PKM Entity => MainEditor.Entity;

    private bool ChangingFields
    {
        get => MainEditor.ChangingFields;
        set => MainEditor.ChangingFields = value;
    }

    private void ClickIV(NumericTextBox t, KeyModifiers mods)
    {
        switch (mods)
        {
            case KeyModifiers.Alt: // Min
                t.Text = 0.ToString();
                break;

            case KeyModifiers.Control: // Max
                {
                    var index = Array.IndexOf(MT_IVs, t);
                    t.Text = Entity.GetMaximumIV(index, true).ToString();
                    break;
                }

            case KeyModifiers.Shift when Entity is IHyperTrain h: // HT
                {
                    var index = Array.IndexOf(MT_IVs, t);
                    bool flag = h.HyperTrainInvert(index);
                    UpdateHyperTrainingFlag(index, flag);
                    UpdateStats();
                    break;
                }
        }
    }

    private void ClickEV(NumericTextBox t, KeyModifiers mods)
    {
        if ((mods & KeyModifiers.Control) != 0) // Max
        {
            int index = Array.IndexOf(MT_EVs, t);
            int newEV = Entity.GetMaximumEV(index);
            t.Text = newEV.ToString();
        }
        else if ((mods & KeyModifiers.Alt) != 0) // Min
        {
            t.Text = 0.ToString();
        }
    }

    private static void ClickAV(NumericTextBox t, KeyModifiers mods)
    {
        if ((mods & KeyModifiers.Control) != 0) // Max
        {
            var max = AwakeningUtil.AwakeningMax.ToString();
            t.Text = t.Text == max ? 0.ToString() : max;
        }
        else if ((mods & KeyModifiers.Alt) != 0) // Min
        {
            t.Text = 0.ToString();
        }
    }

    private void ClickGV(NumericTextBox t, KeyModifiers mods)
    {
        if (Entity is not IGanbaru g)
            return;

        if ((mods & KeyModifiers.Control) != 0) // Max
        {
            int index = Array.IndexOf(MT_GVs, t);
            var max = g.GetMax(Entity, index).ToString();
            t.Text = t.Text == max ? 0.ToString() : max;
        }
        else if ((mods & KeyModifiers.Alt) != 0) // Min
        {
            t.Text = 0.ToString();
        }
    }

    public void UpdateIVs(object? sender)
    {
        if (MainEditor is null || Entity is null)
            return;
        if (sender is NumericTextBox m)
        {
            int value = m.IntValue;
            if (value > Entity.MaxIV)
            {
                m.Text = Entity.MaxIV.ToString();
                return; // recursive on text set
            }

            int index = Array.IndexOf(MT_IVs, m);
            Entity.SetIV(index, value);
            if (Entity is IGanbaru g)
                RefreshGanbaru(Entity, g, index);
        }
        RefreshDerivedValues();
        UpdateStats();
    }

    private void RefreshDerivedValues()
    {
        if (Entity.Format < 3)
        {
            TB_IVHP.Text = Entity.IV_HP.ToString();
            TB_IVSPD.Text = Entity.IV_SPD.ToString();

            MainEditor.UpdateIVsGB(false);
        }

        if (!ChangingFields)
        {
            ChangingFields = true;
            CB_HPType.SetValue(Entity.HPType);
            Label_HiddenPowerPower.Text = Entity.HPPower.ToString();
            ChangingFields = false;
        }

        // Potential Reading
        L_Potential.Text = Entity.GetPotentialString(MainEditor.Unicode);

        TB_IVTotal.Text = Entity.IVTotal.ToString();
        UpdateCharacteristic(Entity.Characteristic);
    }

    private void UpdateEVs(object? sender)
    {
        if (MainEditor is null || Entity is null)
            return;
        if (sender is NumericTextBox m)
        {
            int value = m.IntValue;
            if (value > Entity.MaxEV)
            {
                m.Text = Entity.MaxEV.ToString();
                return; // recursive on text set
            }

            int index = Array.IndexOf(MT_EVs, m);
            Entity.SetEV(index, value);
        }

        UpdateEVTotals();

        if (Entity.Format < 3)
        {
            ChangingFields = true;
            TB_EVSPD.Text = TB_EVSPA.Text;
            ChangingFields = false;
        }

        UpdateStats();
    }

    private void UpdateAVs(object? sender)
    {
        if (MainEditor is null || Entity is not IAwakened a)
            return;
        if (sender is NumericTextBox m)
        {
            var value = (byte)Math.Min(byte.MaxValue, m.IntValue);
            if (value > AwakeningUtil.AwakeningMax)
            {
                m.Text = AwakeningUtil.AwakeningMax.ToString();
                return; // recursive on text set
            }

            int index = Array.IndexOf(MT_AVs, m);
            a.SetAV(index, value);
        }

        UpdateAVTotals();
        UpdateStats();
    }

    private void UpdateGVs(object? sender)
    {
        if (MainEditor is null || Entity is not IGanbaru g)
            return;
        if (sender is NumericTextBox m)
        {
            int value = m.IntValue;
            if (value > GanbaruExtensions.TrueMax)
            {
                m.Text = GanbaruExtensions.TrueMax.ToString();
                return; // recursive on text set
            }

            int index = Array.IndexOf(MT_GVs, m);
            g.SetGV(index, (byte)value);
            RefreshGanbaru(Entity, g, index);
        }

        UpdateStats();
    }

    private void UpdateRandomEVs(KeyModifiers mods)
    {
        Span<int> values = stackalloc int[6];
        switch (mods)
        {
            case KeyModifiers.Control:
                EffortValues.SetMax(values, Entity);
                break;
            case KeyModifiers.Alt:
                EffortValues.Clear(values);
                break;
            default:
                EffortValues.SetRandom(values, Entity.Format);
                break;
        }
        LoadEVs(values);
        UpdateEVs(null);
    }

    private void UpdateHackedStats()
    {
        var hacked = CHK_HackedStats.IsChecked == true;
        foreach (var s in MT_Stats)
            s.IsEnabled = hacked;
        if (!hacked && MainEditor is not null)
            UpdateStats();
    }

    private void UpdateHackedStatText(object? sender)
    {
        if (CHK_HackedStats.IsChecked != true || sender is not TextBox tb)
            return;

        string text = tb.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            tb.Text = "0";
        else if (Util.ToUInt32(text) > ushort.MaxValue)
            tb.Text = "65535";
    }

    private void UpdateHyperTrainingFlag(int index, bool value)
    {
        var tb = MT_IVs[index];
        if (value)
        {
            tb.SetForeColor(Color.Black);
            tb.SetBackColor(StatHyperTrained);
        }
        else
        {
            tb.ResetColors();
        }
    }

    private void UpdateHPType()
    {
        if (ChangingFields || MainEditor is null)
            return;

        // Change IVs to match the new Hidden Power
        Span<int> ivs = stackalloc int[6];
        Entity.GetIVs(ivs);
        int hpower = CB_HPType.GetValue();
        if (MainWindow.Settings.EntityEditor.HiddenPowerOnChangeMaxPower)
            ivs.Fill(Entity.MaxIV);
        HiddenPower.SetIVs(hpower, ivs, Entity.Context);
        LoadIVs(ivs);
    }

    private void ClickStatLabel(TextBlock sender, KeyModifiers mods)
    {
        if (Entity.Format < 3)
            return;

        if (mods == KeyModifiers.None)
            return;

        var label = sender == Label_SPC ? Label_SPA : sender;
        int index = Array.IndexOf(L_Stats, label) - 1;
        if (index < 0)
            return;

        var request = mods switch
        {
            KeyModifiers.Control => NatureAmpRequest.Neutral,
            KeyModifiers.Alt => NatureAmpRequest.Decrease,
            _ => NatureAmpRequest.Increase,
        };

        var newNature = request.GetNewNature(index, Entity.StatAlignment);
        if (newNature == Nature.Random)
            return;

        MainEditor.ChangeNature(newNature);
    }

    private void LoadHyperTraining()
    {
        if (Entity is not IHyperTrain h)
        {
            foreach (var iv in MT_IVs)
                iv.ResetColors();
            return;
        }

        for (int i = 0; i < MT_IVs.Length; i++)
            UpdateHyperTrainingFlag(i, h.IsHyperTrained(i));
    }

    private void UpdateAVTotals()
    {
        if (Entity is not IAwakened a)
            return;
        var total = a.AwakeningSum();
        TB_AVTotal.Text = total.ToString();
    }

    private void UpdateEVTotals()
    {
        var evtotal = Entity.EVTotal;
        TB_EVTotal.Text = evtotal.ToString();
        var color = GetEVTotalColor(evtotal);
        if (color is { } c)
        {
            TB_EVTotal.SetBackColor(c);
            TB_EVTotal.SetForeColor(Color.Black);
        }
        else
        {
            TB_EVTotal.ResetColors();
        }
        ToolTip.SetTip(TB_EVTotal, $"Remaining: {510 - evtotal}");
    }

    private static Color? GetEVTotalColor(int evtotal) => EffortValues.GetGrade(evtotal) switch
    {
        EffortValueGrade.Illegal => EVsInvalid, // Background turns Red
        EffortValueGrade.MaxLegal => EVsMaxed, // Maximum EVs
        EffortValueGrade.MaxEffective => EVsFishy, // Fishy EVs
        _ => null,
    };

    public void UpdateStats()
    {
        // Generate the stats.
        // Some entity formats don't store stat values regardless of Box/Party/Etc format.
        // If its attack stat is zero, we need to generate party stats.
        // PK1 format stores Current HP in the compact format, so we have to use attack stat!
        if (CHK_HackedStats.IsChecked != true || Entity.Stat_ATK == 0)
        {
            var pt = MainEditor.RequestSaveFile.Personal;
            var pi = pt.GetFormEntry(Entity.Species, Entity.Form);
            Span<ushort> stats = stackalloc ushort[6];
            Entity.LoadStats(pi, stats);
            Entity.SetStats(stats);
            LoadBST(pi);
            LoadPartyStats(Entity);
        }
        if (Entity is ITeraType)
        {
            var pi = Entity.PersonalInfo;
            PB_TeraType1.SetType(pi.Type1, false); // Personal Info are just regular move types.
            PB_TeraType2.SetType(pi.Type2, false); // Personal Info are just regular move types.
        }
    }

    private void LoadBST(IBaseStat pi)
    {
        int bst = 0;
        for (int index = 0; index < 6; index++)
        {
            var value = pi.GetBaseStatValue(index);
            var s = MT_Base[index];
            s.Text = value.ToString("000");
            s.SetForeColor(Color.Black);
            s.SetBackColor(ColorUtil.ColorBaseStat(value));
            bst += value;
        }

        TB_BST.Text = bst.ToString("000");
        TB_BST.SetForeColor(Color.Black);
        TB_BST.SetBackColor(ColorUtil.ColorBaseStatTotal(bst));
    }

    public void UpdateRandomIVs(KeyModifiers mods)
    {
        Span<int> ivs = stackalloc int[6];
        if (mods == KeyModifiers.Control)
        {
            ivs.Fill(Entity.MaxIV);
        }
        else if (mods == KeyModifiers.Alt)
        {
            ivs.Clear();
        }
        else
        {
            var pk = Entity;
            var la = new LegalityAnalysis(pk);
            var enc = la.EncounterMatch;
            if (enc is IFlawlessIVCount { FlawlessIVCount: not 0 } fc)
                pk.SetRandomIVs(ivs, fc.FlawlessIVCount);
            else if (enc is IFixedIVSet { IVs: { IsSpecified: true } iv })
                pk.SetRandomIVs(ivs, iv);
            else if (enc is IFlawlessIVCountConditional c && c.GetFlawlessIVCount(pk) is { Max: not 0 } x)
                pk.SetRandomIVs(ivs, Util.Rand.Next(x.Min, x.Max + 1));
            else
                pk.SetRandomIVs(ivs);
        }

        LoadIVs(ivs);
        if (Entity is IGanbaru g)
        {
            Entity.SetIVs(ivs);
            if (mods == KeyModifiers.Control)
                g.SetSuggestedGanbaruValues(Entity);
            else if (mods == KeyModifiers.Alt)
                g.ClearGanbaruValues();
            LoadGVs(g);
        }
    }

    private void UpdateRandomAVs(KeyModifiers mods)
    {
        if (Entity is not IAwakened a)
            return;

        switch (mods)
        {
            case KeyModifiers.Control:
                a.SetSuggestedAwakenedValues(Entity);
                break;
            case KeyModifiers.Alt:
                a.AwakeningMinimum(); // will still set AVs by level gain
                break;
            default:
                a.AwakeningSetRandom();
                break;
        }
        LoadAVs(a);
    }

    public void UpdateCharacteristic() => UpdateCharacteristic(Entity.Characteristic);

    private void UpdateCharacteristic(int characteristic)
    {
        L_Characteristic.IsVisible = Label_CharacteristicPrefix.IsVisible = characteristic > -1;
        if (characteristic > -1)
            L_Characteristic.Text = GameInfo.Strings.characteristics[characteristic];
    }

    public string UpdateNatureModification(Nature nature)
    {
        // Reset Label Colors
        foreach (var l in L_Stats)
            l.ResetForeColor();

        // Set Colored StatLabels only if Nature isn't Neutral
        var (up, dn) = nature.GetNatureModification();
        if (nature.IsNeutralOrInvalid(up, dn))
            return "-/-";

        var incr = L_Stats[up + 1];
        var decr = L_Stats[dn + 1];

        var increase = StatIncreased;
        var decrease = StatDecreased;
        if (App.IsDarkModeEnabled)
        {
            // Slightly whiten; regular color is too dark.
            increase = ColorUtil.Blend(increase, ColorUtilAvalonia.ControlText, 0.60f);
            decrease = ColorUtil.Blend(decrease, ColorUtilAvalonia.ControlText, 0.45f);
        }

        incr.SetForeColor(increase);
        decr.SetForeColor(decrease);
        return $"+{incr.Text} / -{decr.Text}".Replace(":", string.Empty);
    }

    public void SetATKIVGender(byte gender)
    {
        Entity.SetAttackIVFromGender(gender);
        TB_IVATK.Text = Entity.IV_ATK.ToString();
    }

    public void LoadPartyStats(PKM pk)
    {
        MT_Stats[0].Text = pk.Stat_HPCurrent.ToString();
        MT_Stats[1].Text = pk.Stat_ATK.ToString();
        MT_Stats[2].Text = pk.Stat_DEF.ToString();
        MT_Stats[4].Text = pk.Stat_SPA.ToString();
        MT_Stats[5].Text = pk.Stat_SPD.ToString();
        MT_Stats[3].Text = pk.Stat_SPE.ToString();
    }

    public void SavePartyStats(PKM pk)
    {
        pk.Stat_HPCurrent = MT_Stats[0].IntValue;
        pk.Stat_HPMax = MT_Stats[0].IntValue;
        pk.Stat_ATK = MT_Stats[1].IntValue;
        pk.Stat_DEF = MT_Stats[2].IntValue;
        pk.Stat_SPE = MT_Stats[3].IntValue;
        pk.Stat_SPA = MT_Stats[4].IntValue;
        pk.Stat_SPD = MT_Stats[5].IntValue;
        if (!HaX)
            pk.Stat_Level = pk.CurrentLevel;
    }

    public void LoadEVs(ReadOnlySpan<int> EVs)
    {
        ChangingFields = true;
        for (int i = 0; i < 6; i++)
            MT_EVs[i].Text = EVs[i].ToString();
        ChangingFields = false;
        UpdateStats();
    }

    public void LoadIVs(ReadOnlySpan<int> IVs)
    {
        ChangingFields = true;
        for (int i = 0; i < 6; i++)
            MT_IVs[i].Text = IVs[i].ToString();
        ChangingFields = false;
        LoadHyperTraining();
        RefreshDerivedValues();
        UpdateStats();
    }

    public void LoadAVs(IAwakened a)
    {
        ChangingFields = true;
        MT_AVs[0].Text = a.AV_HP.ToString();
        MT_AVs[1].Text = a.AV_ATK.ToString();
        MT_AVs[2].Text = a.AV_DEF.ToString();
        MT_AVs[3].Text = a.AV_SPE.ToString();
        MT_AVs[4].Text = a.AV_SPA.ToString();
        MT_AVs[5].Text = a.AV_SPD.ToString();
        ChangingFields = false;
        UpdateStats();
    }

    public void LoadGVs(IGanbaru a)
    {
        ChangingFields = true;
        MT_GVs[0].Text = a.GV_HP.ToString();
        MT_GVs[1].Text = a.GV_ATK.ToString();
        MT_GVs[2].Text = a.GV_DEF.ToString();
        MT_GVs[3].Text = a.GV_SPE.ToString();
        MT_GVs[4].Text = a.GV_SPA.ToString();
        MT_GVs[5].Text = a.GV_SPD.ToString();
        ChangingFields = false;
        for (int i = 0; i < 6; i++)
            RefreshGanbaru(Entity, a, i);
        UpdateStats();
    }

    private void L_DynamaxLevel_Click()
    {
        var cb = CB_DynamaxLevel;
        bool isMin = cb.SelectedIndex == 0;
        cb.SelectedIndex = isMin ? cb.Items.Count - 1 : 0;
    }

    private void RefreshGanbaru(PKM entity, IGanbaru ganbaru, int i)
    {
        int current = ganbaru.GetGV(i);
        var max = ganbaru.GetMax(entity, i);
        var tb = MT_GVs[i];
        if (current > max)
        {
            tb.SetForeColor(Color.Black);
            tb.SetBackColor(EVsInvalid);
        }
        else if (current == max)
        {
            tb.SetForeColor(Color.Black);
            tb.SetBackColor(StatHyperTrained);
        }
        else
        {
            tb.ResetColors();
        }
    }

    private void SetStatOrder(StatEditorStatOrder order)
    {
        if (order == StatOrder)
            return;

        // In Generation 1, Special Defense and Special Attack are combined.
        // Additionally, Speed is shown before Special.
        if (order == StatEditorStatOrder.Gen1Special)
        {
            SetStatGridRow(3, 4); // Speed
            SetStatGridRow(4, 5); // Special
            SetStatVisibility(5, false);

            Label_SPA.IsVisible = false;
            Label_SPC.IsVisible = true;
        }
        else if (order == StatEditorStatOrder.Current)
        {
            SetStatGridRow(4, 4); // SpA
            SetStatGridRow(3, 6); // Speed
            SetStatVisibility(5, true);

            Label_SPA.IsVisible = true;
            Label_SPC.IsVisible = false;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(order), order, null);
        }

        StatOrder = order;
    }

    private void SetStatVisibility(int statIndex, bool visible)
    {
        L_Stats[statIndex].IsVisible = visible;
        MT_Base[statIndex].IsVisible = visible;
        MT_IVs[statIndex].IsVisible = visible;
        MT_EVs[statIndex].IsVisible = visible;
        MT_AVs[statIndex].IsVisible = visible;
        MT_GVs[statIndex].IsVisible = visible;
        MT_Stats[statIndex].IsVisible = visible;
    }

    private void SetStatGridRow(int statIndex, int row)
    {
        Control stat = statIndex == 4 ? FLP_SPA : L_Stats[statIndex];
        Grid.SetRow(stat, row);
        Grid.SetRow(MT_Base[statIndex], row);
        Grid.SetRow(MT_IVs[statIndex], row);
        Grid.SetRow(MT_EVs[statIndex], row);
        Grid.SetRow(MT_AVs[statIndex], row);
        Grid.SetRow(MT_GVs[statIndex], row);
        Grid.SetRow(MT_Stats[statIndex], row);
    }

    private void SetTotalRowVisible(bool visible)
    {
        Label_Total.IsVisible = TB_BST.IsVisible = TB_IVTotal.IsVisible = TB_EVTotal.IsVisible = L_Potential.IsVisible = visible;
        TB_AVTotal.IsVisible = visible && ShowAVs;
    }

    private bool ShowAVs;

    private void SetColumnVisible(int index, bool visible)
    {
        foreach (var c in TLP_StatGrid.Children)
        {
            if (Grid.GetColumn(c) == index)
                c.IsVisible = visible;
        }
    }

    public void ToggleInterface(PKM pk, byte format)
    {
        FLP_Characteristic.IsVisible = format >= 3;
        FLP_HPType.IsVisible = format <= 7 || pk is PB8;
        FLP_TeraType.IsVisible = pk is ITeraType;
        Label_HiddenPowerPower.IsVisible = format <= 5;
        FLP_DynamaxLevel.IsVisible = format == 8;
        FLP_AlphaNoble.IsVisible = pk is IAlpha;
        CHK_IsNoble.IsVisible = pk is PA8;

        // Update stat ordering if necessary. Gen 1 shows Speed before Special, and combines Special Attack and Special Defense into one "Special" stat.
        // Later gens show Speed after Special, and have separate Special Attack and Special Defense stats.
        SetStatOrder(format == 1 ? StatEditorStatOrder.Gen1Special : StatEditorStatOrder.Current);

        switch (format) // EV Mask (Gen1/2 is 16-bit as opposed to 8-bit in later gens)
        {
            case 1 or 2:
                TB_IVHP.IsEnabled = false;
                foreach (var ev in MT_EVs)
                    ev.MaxLength = 5;
                break;
            default:
                TB_IVHP.IsEnabled = true;
                foreach (var ev in MT_EVs)
                    ev.MaxLength = 3;
                break;
        }

        // Misc stat properties: toggle columns if present for object.
        var showAVs = ShowAVs = pk is IAwakened;
        var showGVs = pk is IGanbaru;
        var showEVs = !showAVs || HaX;
        SetColumnVisible(3, showEVs);
        SetColumnVisible(4, showAVs);
        SetColumnVisible(5, showGVs);
        SetTotalRowVisible(format >= 3);
        if (format == 1)
            SetStatVisibility(5, false);

        BTN_RandomEVs.IsVisible = showEVs;
        BTN_RandomAVs.IsVisible = showAVs;
        // no randomizing GVs; maxing/zeroing is all that is needed.
    }

    private const string TeraOverrideNone = "---";
    private const byte TeraOverrideNoneValue = TeraTypeUtil.OverrideNone;
    private const byte TeraStellarValue = TeraTypeUtil.Stellar;
    private const byte TeraDisplayIndex = TeraTypeUtil.StellarTypeDisplayStringIndex;

    private void L_TeraTypeOriginal_Click()
    {
        var pi = Entity.PersonalInfo;
        if (!Entity.SV)
        {
            var expect = TeraTypeUtil.GetTeraTypeImport(pi.Type1, pi.Type2);
            SetOriginalTeraType((byte)expect);
            return;
        }
        var current = CB_TeraTypeOriginal.GetValue();
        var update = pi.Type1 == current ? pi.Type2 : pi.Type1;
        SetOriginalTeraType(update);
    }

    private void SetOriginalTeraType(byte value)
    {
        CB_TeraTypeOriginal.SetValue(value);
        CB_TeraTypeOverride.SetValue(TeraOverrideNoneValue);
    }

    public void InitializeDataSources()
    {
        ChangingFields = true;

        var types = GameInfo.Strings.types.AsSpan();
        CB_HPType.SetItems(Util.GetCBList(types.Slice(1, HiddenPower.TypeCount)));

        var tera = Util.GetCBList(types[..TeraDisplayIndex]);
        tera.Insert(0, new(TeraOverrideNone, TeraOverrideNoneValue));
        tera.Add(new(types[TeraDisplayIndex], TeraStellarValue));
        CB_TeraTypeOriginal.SetItems(tera);
        CB_TeraTypeOverride.SetItems([.. tera]);

        ChangingFields = false;
    }

    private void L_TeraTypeOverride_Click()
    {
        if (Entity.SV)
            CB_TeraTypeOverride.SetValue(TeraOverrideNoneValue);
        else
            CB_TeraTypeOverride.SetValue(CB_TeraTypeOriginal.GetValue());
    }

    private void ChangeTeraType(ComboBox sender)
    {
        if (MainEditor is null)
            return;
        if (ChangingFields && sender == CB_TeraTypeOriginal)
            return;

        var original = (byte)CB_TeraTypeOriginal.GetValue();
        var update = (byte)CB_TeraTypeOverride.GetValue();
        if (!ChangingFields && Entity is ITeraType t) // Store back
        {
            if (sender == CB_TeraTypeOriginal)
                t.TeraTypeOriginal = (MoveType)original;
            else if (sender == CB_TeraTypeOverride)
                t.TeraTypeOverride = (MoveType)update;
        }

        var type = update;
        if (type == TeraOverrideNoneValue)
            type = original;
        var old = TeraGemImage;
        using var gem = TypeSpriteUtil.GetTypeSpriteGem(type);
        TeraGemImage = gem?.ToAvaloniaBitmap();
        PB_TeraType.Source = TeraGemImage;
        old?.Dispose();
        if (!ChangingFields)
            MainEditor.UpdateSprite();
    }
}

/// <summary>
/// Colored box showing a move type (port of the WinForms <c>TypePictureBox</c>).
/// </summary>
public sealed class TypeBox : Border
{
    private byte Type;

    public TypeBox()
    {
        Width = 32;
        Height = 16;
        Cursor = new Cursor(StandardCursorType.Hand);
        PointerEntered += (_, _) =>
        {
            var index = Type;
            if (index == TeraTypeUtil.Stellar)
                index = TeraTypeUtil.StellarTypeDisplayStringIndex;
            var types = GameInfo.Strings.types;
            ToolTip.SetTip(this, index < types.Length ? types[index] : index.ToString());
        };
    }

    public void SetType(byte type, bool tera) => Background = (tera
        ? TypeColor.GetTeraSpriteColor(Type = type)
        : TypeColor.GetTypeSpriteColor(Type = type)).ToBrush();
}

/// <summary>
/// Stat display order for a stat editor.
/// </summary>
public enum StatEditorStatOrder
{
    Current = 0,
    Gen1Special,
}
