using System;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Form argument editor (port of the WinForms <c>FormArgumentEditor</c>).
/// </summary>
public sealed class FormArgumentEditorView : StackPanel
{
    private FormArgumentType Mode { get; set; }
    private ushort Species { get; set; }
    private byte Form { get; set; }
    private EntityContext Context { get; set; }
    public bool IsControlVisible { get; private set; }

    private bool FieldsLoaded;

    private readonly ComboBox CB_FormArg = UiFactory.StringCombo("CB_FormArg", 140);
    private readonly NumericUpDown NUD_FormArg = UiFactory.NumericUpDown("NUD_FormArg", 0, 9999);
    private readonly NumericUpDown NUD_FormArgMax = UiFactory.NumericUpDown("NUD_FormArgMax", 0, 255, 70);
    private readonly NumericUpDown NUD_FormArgElapsed = UiFactory.NumericUpDown("NUD_FormArgElapsed", 0, 255, 70);
    private readonly NumericUpDown NUD_FormArgRemain = UiFactory.NumericUpDown("NUD_FormArgRemain", 0, 255, 70);

    public FormArgumentEditorView()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 4;
        Children.Add(CB_FormArg);
        Children.Add(NUD_FormArg);
        Children.Add(NUD_FormArgMax);
        Children.Add(NUD_FormArgElapsed);
        Children.Add(NUD_FormArgRemain);
        ToolTip.SetTip(NUD_FormArgMax, nameof(NUD_FormArgMax));
        ToolTip.SetTip(NUD_FormArgElapsed, nameof(NUD_FormArgElapsed));
        ToolTip.SetTip(NUD_FormArgRemain, nameof(NUD_FormArgRemain));

        CB_FormArg.SelectionChanged += SelectionChanged;
        NUD_FormArg.ValueChanged += SelectionChanged;
        NUD_FormArgMax.ValueChanged += SelectionChanged;
        NUD_FormArgElapsed.ValueChanged += SelectionChanged;
        NUD_FormArgRemain.ValueChanged += SelectionChanged;
    }

    public bool LoadArgument(IFormArgument f, ushort species, byte form, EntityContext context)
    {
        FieldsLoaded = false;
        Initialize(f, species, form, context);
        FieldsLoaded = true;
        return IsControlVisible = Mode != FormArgumentType.None;
    }

    private void Initialize(IFormArgument f, ushort species, byte form, EntityContext context)
    {
        var mode = FormArgumentUtil.GetType(species, form, context);
        var same = Mode == mode && Species == species && Form == form && Context == context;
        Context = context;
        Species = species;
        Form = form;
        Mode = mode;

        CB_FormArg.IsVisible = mode == FormArgumentType.Named;
        NUD_FormArg.IsVisible = mode == FormArgumentType.Raw;
        NUD_FormArgMax.IsVisible = mode == FormArgumentType.Triple;
        NUD_FormArgRemain.IsVisible = mode is FormArgumentType.Triple or FormArgumentType.TripleParty;
        NUD_FormArgElapsed.IsVisible = mode is FormArgumentType.Triple or FormArgumentType.TripleParty;

        LoadValue(f, species, form, context, mode, same);
    }

    private void LoadValue(IFormArgument f, ushort species, byte form, EntityContext context, FormArgumentType mode, bool isSame)
    {
        if (mode == FormArgumentType.Named)
        {
            if (!isSame) // Already initialized.
            {
                var args = FormConverter.GetFormArgumentStrings(species);
                CB_FormArg.Items.Clear();
                foreach (var a in args)
                    CB_FormArg.Items.Add(a);
            }

            CB_FormArg.SelectedIndex = Math.Clamp((int)f.FormArgument, 0, CB_FormArg.Items.Count - 1);
            return;
        }

        var max = FormArgumentUtil.GetFormArgumentMaxEdge(species, form, context);
        if (mode == FormArgumentType.Raw)
        {
            NUD_FormArg.Maximum = max;
            NUD_FormArg.Value = Math.Clamp(f.FormArgument, 0, max);
        }
        else if (mode == FormArgumentType.Triple)
        {
            NUD_FormArgMax.Value = f.FormArgumentMaximum;
            NUD_FormArgRemain.Value = f.FormArgumentRemain;
            NUD_FormArgElapsed.Value = f.FormArgumentElapsed;
        }
        else if (mode == FormArgumentType.TripleParty)
        {
            NUD_FormArgRemain.Value = f.FormArgumentRemain;
            NUD_FormArgElapsed.Value = f.FormArgumentElapsed;
        }
    }

    public void SaveArgument(IFormArgument f)
    {
        if (Mode == FormArgumentType.TripleParty)
        {
            var streak = Species != (ushort)Core.Species.Furfrou ? (byte)0 : (byte)(NUD_FormArgElapsed.Value ?? 0);
            f.FormArgumentMaximum = streak;
            f.FormArgumentElapsed = streak;
            f.FormArgumentRemain = (byte)(NUD_FormArgRemain.Value ?? 0);
        }
        else if (Mode == FormArgumentType.Triple)
        {
            f.FormArgumentMaximum = (byte)(NUD_FormArgMax.Value ?? 0);
            f.FormArgumentElapsed = (byte)(NUD_FormArgElapsed.Value ?? 0);
            f.FormArgumentRemain = (byte)(NUD_FormArgRemain.Value ?? 0);
        }
        else if (Mode == FormArgumentType.Named)
        {
            f.FormArgument = (uint)Math.Max(0, CB_FormArg.SelectedIndex);
        }
        else if (Mode == FormArgumentType.Raw)
        {
            f.FormArgument = (uint)(NUD_FormArg.Value ?? 0);
        }
        else
        {
            f.FormArgument = 0;
        }
    }

    public event EventHandler? ValueChanged;

    private void SelectionChanged(object? sender, EventArgs e)
    {
        if (FieldsLoaded)
            ValueChanged?.Invoke(sender, e);
    }
}
