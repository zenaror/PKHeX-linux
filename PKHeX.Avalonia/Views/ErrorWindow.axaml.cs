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
        // No Name is set: Avalonia refuses to name an already-styled element, and the translator falls back to the
        // type name, which is the WinForms form name ("ErrorWindow") the lang_*.txt keys use.
        Localization.Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
    }

    public static async Task<DialogResult> ShowErrorDialog(Window? owner, string friendlyMessage, Exception ex, bool allowContinue)
    {
        var dialog = new ErrorWindow();
        dialog.FindControl<TextBlock>("L_Message")!.Text = friendlyMessage;
        dialog.FindControl<TextBox>("T_ExceptionDetails")!.Text = GetExceptionDetails(ex, friendlyMessage);
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

    /// <remarks>
    /// WinForms dumps every loaded assembly here; the version/OS/runtime lines carry the same "what was running"
    /// information in three lines instead of a hundred. The user message is included for the same reason WinForms
    /// includes it: it often carries the file path that failed.
    /// </remarks>
    private static string GetExceptionDetails(Exception ex, string friendlyMessage)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Exception Details: {DateTime.Now}");
        sb.AppendLine(ex.ToString());
        sb.AppendLine();
        sb.AppendLine($"PKHeX Version: {Program.CurrentVersion}");
        sb.AppendLine($"OS: {Environment.OSVersion} ({System.Runtime.InteropServices.RuntimeInformation.OSDescription})");
        sb.AppendLine($".NET: {Environment.Version}");
        sb.AppendLine();
        sb.AppendLine("User Message:");
        sb.AppendLine(friendlyMessage);
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
