using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Dialog asking the user to pick one option (replacement for the WinForms <c>TaskDialog</c> radio button page).
/// </summary>
public sealed partial class SelectIndexDialog : Window
{
    private readonly List<RadioButton> Buttons = [];
    private int Result = -1;

    public SelectIndexDialog()
    {
        InitializeComponent();
    }

    public SelectIndexDialog(string caption, string text, IReadOnlyList<string> options, int preSelect) : this()
    {
        Title = caption;
        Icon = AppIcon.Get();
        this.FindControl<SelectableTextBlock>("TB_Text")!.Text = text;
        var panel = this.FindControl<StackPanel>("SP_Options")!;
        for (int i = 0; i < options.Count; i++)
        {
            var rb = new RadioButton { Content = options[i], GroupName = "options", IsChecked = i == preSelect };
            Buttons.Add(rb);
            panel.Children.Add(rb);
        }
    }

    private void B_OK_Click(object? sender, RoutedEventArgs e)
    {
        Result = Buttons.FindIndex(z => z.IsChecked == true);
        Close();
    }

    private void B_Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Result = -1;
        Close();
    }

    /// <summary>
    /// Shows the dialog; returns the selected index, or -1 if cancelled.
    /// </summary>
    public async Task<int> ShowAsync(Window? owner)
    {
        if (owner is not null)
        {
            await ShowDialog(owner);
        }
        else
        {
            var tcs = new TaskCompletionSource();
            Closed += (_, _) => tcs.SetResult();
            Show();
            await tcs.Task;
        }
        return Result;
    }
}
