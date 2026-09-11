using System;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Trainer ID / Secret ID entry (port of the WinForms <c>TrainerID</c>, <c>TrainerTID</c> and <c>TrainerSID</c> controls).
/// </summary>
public sealed class TrainerIDView : StackPanel
{
    private readonly TrainerIDManager _manager;
    public event EventHandler? UpdatedID;

    private readonly TextBlock Label_TID = UiFactory.Label("Label_TID", "TID:");
    private readonly TextBlock Label_SID = UiFactory.Label("Label_SID", "SID:");
    private readonly TrainerTIDBox TIDFields = new();
    private readonly TrainerSIDBox SIDFields = new();

    public TrainerIDView()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 4;
        Children.Add(Label_TID);
        Children.Add(TIDFields.TB_Five);
        Children.Add(TIDFields.TB_Six);
        Children.Add(Label_SID);
        Children.Add(SIDFields.TB_Five);
        Children.Add(SIDFields.TB_Four);
        _manager = new TrainerIDManager(TIDFields, SIDFields);
        _manager.ValueChanged += (sender, e) => UpdatedID?.Invoke(sender, e);
    }

    public void LoadTrainer<T>(T trainer) where T : ITrainerID32, IGeneration
    {
        _manager.LoadTrainer(trainer);
        Label_SID.IsVisible = trainer.Generation >= 3;
    }

    public void LoadTrainer(ITrainerID32 trainer, byte generation)
    {
        _manager.LoadTrainer(trainer, generation);
        Label_SID.IsVisible = generation >= 3;
    }

    public void LoadTrainer() => _manager.LoadTrainer();

    /// <summary>Writes the displayed trainer ID back into <paramref name="trainer"/>.</summary>
    public void SaveTrainer(ITrainerID32 trainer) => _manager.SaveTrainer(trainer);

    public void SetToolTip() => _manager.SetToolTip();
}

internal sealed class TrainerTIDBox : ITrainerIDControl
{
    private TrainerIDFormat Format { get; set; }
    private bool IsSixDigit => Format is TrainerIDFormat.SixDigit;
    public event EventHandler? ValueChanged;

    public readonly NumericTextBox TB_Five = UiFactory.Numeric("TB_TID5", 5, 56);
    public readonly NumericTextBox TB_Six = UiFactory.Numeric("TB_TID6", 6, 64);

    public TrainerTIDBox()
    {
        TB_Five.OnTextChanged(s => ValueChanged?.Invoke(s, EventArgs.Empty));
        TB_Six.OnTextChanged(s => ValueChanged?.Invoke(s, EventArgs.Empty));
    }

    public void LoadTrainer(ITrainerID32 trainer, TrainerIDFormat displayType)
    {
        Format = displayType;
        TB_Five.IsVisible = !IsSixDigit;
        TB_Six.IsVisible = IsSixDigit;

        if (IsSixDigit)
            TB_Six.Text = trainer.GetTrainerTID7().ToString(TrainerIDExtensions.TID7);
        else
            TB_Five.Text = trainer.TID16.ToString(TrainerIDExtensions.TID16);
    }

    public void SaveTrainer(ITrainerID32 trainer)
    {
        if (IsSixDigit)
            Save6(trainer);
        else
            Save5(trainer);
    }

    public bool IsValueSame(ITrainerID32 trainer)
    {
        if (IsSixDigit)
            return trainer.GetTrainerTID7() == (uint.TryParse(TB_Six.Text, out var value) ? value : 0);
        return trainer.TID16 == (ushort.TryParse(TB_Five.Text, out var value5) ? value5 : 0);
    }

    private void Save5(ITrainerID32 trainer)
    {
        var box = TB_Five;
        var text = box.Text ?? string.Empty;
        if (text.Length == 0)
        {
            trainer.TID16 = 0;
            return;
        }

        const ushort max = ushort.MaxValue;
        if (!uint.TryParse(text, out var value))
        {
            value = 0;
        }
        else if (value > max)
        {
            value = max;
        }
        else
        {
            trainer.TID16 = (ushort)value;
            return;
        }

        trainer.TID16 = (ushort)value;
        box.Text = value.ToString(TrainerIDExtensions.TID16);
    }

    private void Save6(ITrainerID32 trainer)
    {
        var box = TB_Six;
        var text = box.Text ?? string.Empty;
        if (text.Length == 0)
        {
            trainer.SetTrainerTID7(0);
            return;
        }

        const uint max = 999_999u;
        if (!uint.TryParse(text, out var value))
        {
            value = 0;
        }
        if (value > max)
        {
            value = max;
        }
        else if (value == max && trainer.IsValidTrainerID7(sid7: value, trainer.GetTrainerTID7()))
        {
            value = max - 1;
        }
        else
        {
            trainer.SetTrainerTID7(value);
            return;
        }

        trainer.SetTrainerTID7(value);
        box.Text = value.ToString(TrainerIDExtensions.TID7);
    }

    public void SetToolTip(string text)
    {
        ToolTip.SetTip(TB_Five, text);
        ToolTip.SetTip(TB_Six, text);
    }
}

internal sealed class TrainerSIDBox : ITrainerIDControl
{
    private TrainerIDFormat Format { get; set; }
    private bool IsFourDigit => Format is TrainerIDFormat.SixDigit;
    private bool IsFiveDigit => Format is TrainerIDFormat.SixteenBit;
    public event EventHandler? ValueChanged;

    public readonly NumericTextBox TB_Five = UiFactory.Numeric("TB_SID5", 5, 56);
    public readonly NumericTextBox TB_Four = UiFactory.Numeric("TB_SID4", 4, 48);

    public TrainerSIDBox()
    {
        TB_Five.OnTextChanged(s => ValueChanged?.Invoke(s, EventArgs.Empty));
        TB_Four.OnTextChanged(s => ValueChanged?.Invoke(s, EventArgs.Empty));
    }

    public void LoadTrainer(ITrainerID32 trainer, TrainerIDFormat displayType)
    {
        Format = displayType;
        TB_Five.IsVisible = IsFiveDigit;
        TB_Four.IsVisible = IsFourDigit;
        if (IsFourDigit)
            TB_Four.Text = trainer.GetTrainerSID7().ToString(TrainerIDExtensions.SID7);
        else if (IsFiveDigit)
            TB_Five.Text = trainer.SID16.ToString(TrainerIDExtensions.SID16);
    }

    public void SaveTrainer(ITrainerID32 trainer)
    {
        if (IsFourDigit)
            Save4(trainer);
        else if (IsFiveDigit)
            Save5(trainer);
    }

    public bool IsValueSame(ITrainerID32 trainer)
    {
        if (IsFourDigit)
            return trainer.GetTrainerSID7() == (uint.TryParse(TB_Four.Text, out var value) ? value : 0);
        if (IsFiveDigit)
            return trainer.SID16 == (ushort.TryParse(TB_Five.Text, out var value5) ? value5 : 0);
        return true;
    }

    private void Save4(ITrainerID32 trainer)
    {
        var box = TB_Four;
        var text = box.Text ?? string.Empty;
        if (text.Length == 0)
        {
            trainer.SetTrainerSID7(0);
            return;
        }

        const uint max = 4294u;
        if (!uint.TryParse(text, out var value))
        {
            value = 0;
        }
        else if (value > max)
        {
            value = max;
            if (!trainer.IsValidTrainerID7(sid7: value, trainer.GetTrainerTID7()))
                value = max - 1;
        }
        else
        {
            if (trainer.IsValidTrainerID7(sid7: value, trainer.GetTrainerTID7()))
            {
                trainer.SetTrainerSID7(value);
                return;
            }
            value = max - 1;
        }

        trainer.SetTrainerSID7(value);
        box.Text = value.ToString(TrainerIDExtensions.SID7);
    }

    private void Save5(ITrainerID32 trainer)
    {
        var box = TB_Five;
        var text = box.Text ?? string.Empty;
        if (text.Length == 0)
        {
            trainer.SID16 = 0;
            return;
        }

        const ushort max = ushort.MaxValue;
        if (!uint.TryParse(text, out var value))
        {
            value = 0;
        }
        else if (value > max)
        {
            value = max;
        }
        else
        {
            trainer.SID16 = (ushort)value;
            return;
        }

        trainer.SID16 = (ushort)value;
        box.Text = value.ToString(TrainerIDExtensions.SID16);
    }

    public void SetToolTip(string text)
    {
        ToolTip.SetTip(TB_Five, text);
        ToolTip.SetTip(TB_Four, text);
    }
}
