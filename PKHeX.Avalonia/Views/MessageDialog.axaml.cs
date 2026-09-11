using Avalonia.Controls;
using Avalonia.Interactivity;
using PKHeX.Avalonia.Services;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Simple modal message dialog (replacement for the WinForms <c>MessageBox</c>).
/// </summary>
public sealed partial class MessageDialog : Window
{
    private DialogResult Result = DialogResult.None;

    public MessageDialog()
    {
        InitializeComponent();
    }

    public MessageDialog(string title, string message, MessageBoxButtons buttons, MessageBoxIcon icon) : this()
    {
        Title = title;
        var tb = this.FindControl<SelectableTextBlock>("TB_Message")!;
        tb.Text = message;

        var iconLabel = this.FindControl<TextBlock>("L_Icon")!;
        var glyph = icon switch
        {
            MessageBoxIcon.Information => "ℹ",
            MessageBoxIcon.Question => "?",
            MessageBoxIcon.Warning => "⚠",
            MessageBoxIcon.Error => "⛔",
            _ => string.Empty,
        };
        iconLabel.Text = glyph;
        iconLabel.IsVisible = glyph.Length != 0;

        var ok = this.FindControl<Button>("B_OK")!;
        var cancel = this.FindControl<Button>("B_Cancel")!;
        var yes = this.FindControl<Button>("B_Yes")!;
        var no = this.FindControl<Button>("B_No")!;
        switch (buttons)
        {
            case MessageBoxButtons.OK:
                ok.IsVisible = true; ok.IsDefault = true;
                break;
            case MessageBoxButtons.OKCancel:
                ok.IsVisible = cancel.IsVisible = true; ok.IsDefault = true;
                break;
            case MessageBoxButtons.YesNo:
                yes.IsVisible = no.IsVisible = true; yes.IsDefault = true;
                break;
            case MessageBoxButtons.YesNoCancel:
                yes.IsVisible = no.IsVisible = cancel.IsVisible = true; yes.IsDefault = true;
                break;
        }
        Icon = AppIcon.Get();
    }

    private void Finish(DialogResult result)
    {
        Result = result;
        Close(result);
    }

    private void B_OK_Click(object? sender, RoutedEventArgs e) => Finish(DialogResult.OK);
    private void B_Cancel_Click(object? sender, RoutedEventArgs e) => Finish(DialogResult.Cancel);
    private void B_Yes_Click(object? sender, RoutedEventArgs e) => Finish(DialogResult.Yes);
    private void B_No_Click(object? sender, RoutedEventArgs e) => Finish(DialogResult.No);

    /// <summary>
    /// Shows the dialog and returns the pressed button; closing the window counts as <see cref="DialogResult.Cancel"/>
    /// (or <see cref="DialogResult.No"/> / <see cref="DialogResult.OK"/> when no cancel button is present).
    /// </summary>
    public async System.Threading.Tasks.Task<DialogResult> ShowAsync(Window? owner, MessageBoxButtons buttons)
    {
        if (owner is not null)
            await ShowDialog(owner);
        else
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource();
            Closed += (_, _) => tcs.SetResult();
            Show();
            await tcs.Task;
        }
        if (Result != DialogResult.None)
            return Result;
        return buttons switch
        {
            MessageBoxButtons.OK => DialogResult.OK,
            MessageBoxButtons.YesNo => DialogResult.No,
            _ => DialogResult.Cancel,
        };
    }
}
