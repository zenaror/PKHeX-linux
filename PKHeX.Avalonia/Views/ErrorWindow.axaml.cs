using System;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PKHeX.Avalonia.Services;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Displays an exception with details (port of the WinForms <c>ErrorWindow</c>).
/// </summary>
public sealed partial class ErrorWindow : Window
{
    private DialogResult Result = DialogResult.Abort;

    public ErrorWindow()
    {
        InitializeComponent();
        Icon = AppIcon.Get();
    }

    public static async Task<DialogResult> ShowErrorDialog(Window? owner, string friendlyMessage, Exception ex, bool allowContinue)
    {
        var dialog = new ErrorWindow();
        dialog.FindControl<TextBlock>("L_Message")!.Text = friendlyMessage;
        dialog.FindControl<TextBox>("T_ExceptionDetails")!.Text = GetExceptionDetails(ex);
        dialog.FindControl<Button>("B_Continue")!.IsVisible = allowContinue;
        if (owner is not null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            var tcs = new TaskCompletionSource();
            dialog.Closed += (_, _) => tcs.SetResult();
            dialog.Show();
            await tcs.Task;
        }
        return dialog.Result;
    }

    private static string GetExceptionDetails(Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Exception Details: {DateTime.Now}");
        sb.AppendLine(ex.ToString());
        sb.AppendLine();
        sb.AppendLine($"PKHeX Version: {Program.CurrentVersion}");
        sb.AppendLine($"OS: {Environment.OSVersion} ({System.Runtime.InteropServices.RuntimeInformation.OSDescription})");
        sb.AppendLine($".NET: {Environment.Version}");
        return sb.ToString();
    }

    private async void B_Copy_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var text = this.FindControl<TextBox>("T_ExceptionDetails")!.Text ?? string.Empty;
            await ClipboardService.SetText(this, text);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }

    private void B_Continue_Click(object? sender, RoutedEventArgs e)
    {
        Result = DialogResult.OK;
        Close();
    }

    private void B_Abort_Click(object? sender, RoutedEventArgs e)
    {
        Result = DialogResult.Abort;
        Close();
    }
}
