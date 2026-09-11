using Avalonia.Controls;
using Avalonia.Input;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Contest stat entry (port of the WinForms <c>ContestStat</c>).
/// </summary>
public sealed class ContestStatView : Grid, IContestStats
{
    private readonly NumericTextBox TB_Cool = UiFactory.Numeric("TB_Cool", 3, 44);
    private readonly NumericTextBox TB_Beauty = UiFactory.Numeric("TB_Beauty", 3, 44);
    private readonly NumericTextBox TB_Cute = UiFactory.Numeric("TB_Cute", 3, 44);
    private readonly NumericTextBox TB_Smart = UiFactory.Numeric("TB_Smart", 3, 44);
    private readonly NumericTextBox TB_Tough = UiFactory.Numeric("TB_Tough", 3, 44);
    private readonly NumericTextBox TB_Sheen = UiFactory.Numeric("TB_Sheen", 3, 44);
    private readonly TextBlock Label_Smart = UiFactory.Label("Label_Smart", "Smart");
    private readonly TextBlock Label_Clever = UiFactory.Label("Label_Clever", "Clever");

    public ContestStatView()
    {
        ColumnSpacing = 4;
        RowSpacing = 2;
        for (int i = 0; i < 6; i++)
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var labels = new[] { UiFactory.Label("Label_Cool", "Cool"), UiFactory.Label("Label_Beauty", "Beauty"), UiFactory.Label("Label_Cute", "Cute"), null, UiFactory.Label("Label_Tough", "Tough"), UiFactory.Label("Label_Sheen", "Sheen") };
        var boxes = new[] { TB_Cool, TB_Beauty, TB_Cute, TB_Smart, TB_Tough, TB_Sheen };
        for (int i = 0; i < 6; i++)
        {
            Control label = labels[i] is { } l ? l : UiFactory.Row(Label_Smart, Label_Clever);
            label.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
            UiFactory.SetRowCol(label, 0, i);
            Children.Add(label);
            UiFactory.SetRowCol(boxes[i], 1, i);
            Children.Add(boxes[i]);
            boxes[i].OnTextChanged(s => Update255(s));
            boxes[i].AttachClick(mods => ClickTextBox(boxes[i], mods));
        }
    }

    public byte ContestCool { get => (byte)TB_Cool.IntValue; set => TB_Cool.Text = value.ToString(); }
    public byte ContestBeauty { get => (byte)TB_Beauty.IntValue; set => TB_Beauty.Text = value.ToString(); }
    public byte ContestCute { get => (byte)TB_Cute.IntValue; set => TB_Cute.Text = value.ToString(); }
    public byte ContestSmart { get => (byte)TB_Smart.IntValue; set => TB_Smart.Text = value.ToString(); }
    public byte ContestTough { get => (byte)TB_Tough.IntValue; set => TB_Tough.Text = value.ToString(); }
    public byte ContestSheen { get => (byte)TB_Sheen.IntValue; set => TB_Sheen.Text = value.ToString(); }

    private static void Update255(object? sender)
    {
        if (sender is not NumericTextBox tb)
            return;
        if (tb.IntValue > byte.MaxValue)
            tb.Text = "255";
    }

    public void ToggleInterface(object o, EntityContext context)
    {
        if (o is not IContestStatsReadOnly)
        {
            IsVisible = false;
            return;
        }

        IsVisible = true;
        bool smart = context.IsEraPre3DS;
        Label_Smart.IsVisible = smart; // show "Smart" for Gen3-5
        Label_Clever.IsVisible = !smart; // show "Clever" for Gen6+
    }

    private static void ClickTextBox(NumericTextBox tb, KeyModifiers keys)
    {
        if (keys == KeyModifiers.None)
            return;
        if (keys == KeyModifiers.Control)
            tb.Text = "255";
        else if (keys == KeyModifiers.Alt)
            tb.Text = "0";
    }
}
