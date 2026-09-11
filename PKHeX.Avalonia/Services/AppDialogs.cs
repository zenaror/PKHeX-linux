using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PKHeX.Avalonia.Views;
using PKHeX.Core;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Message dialogs (port of the WinForms <c>WinFormsUtil</c> message displays and <c>DialogUtil</c>).
/// </summary>
/// <remarks>All dialogs are asynchronous (Avalonia has no blocking modal loop).</remarks>
public static class AppDialogs
{
    /// <summary>
    /// When set, message dialogs are not shown and are answered with their default result (used for automated runs).
    /// </summary>
    internal static bool Quiet { get; set; }

    private static Window? GetOwner(Window? owner)
    {
        if (owner is not null)
            return owner;
        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var w in desktop.Windows)
            {
                if (w.IsActive)
                    return w;
            }
            return desktop.MainWindow;
        }
        return null;
    }

    private static string Join(ReadOnlySpan<string?> lines) => string.Join(Environment.NewLine + Environment.NewLine, lines.ToArray());

    private static Task<DialogResult> Show(Window? owner, string title, string message, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        var dialog = new MessageDialog(title, message, buttons, icon);
        return dialog.ShowAsync(GetOwner(owner), buttons);
    }

    /// <summary>
    /// Displays a dialog showing the details of an error.
    /// </summary>
    public static Task<DialogResult> Error(Window? owner, string friendlyMessage, Exception exception)
        => ErrorWindow.ShowErrorDialog(GetOwner(owner), friendlyMessage, exception, true);

    /// <summary>
    /// Displays a dialog showing the details of an error.
    /// </summary>
    public static Task<DialogResult> Error(Window? owner, params ReadOnlySpan<string?> lines)
        => Show(owner, "Error", Join(lines), MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static Task<DialogResult> Alert(Window? owner, params ReadOnlySpan<string?> lines)
        => Show(owner, "Alert", Join(lines), MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static Task<DialogResult> Prompt(Window? owner, MessageBoxButtons btn, params ReadOnlySpan<string?> lines)
        => Show(owner, "Prompt", Join(lines), btn, MessageBoxIcon.Question);

    /// <summary>
    /// Asks the user to choose one entry from a list of options.
    /// </summary>
    public static async Task<int> TrySelectIndex(Window? owner, string caption, string text, IReadOnlyList<string> options, int preSelect = -1)
    {
        var dialog = new SelectIndexDialog(caption, text, options, preSelect);
        return await dialog.ShowAsync(GetOwner(owner));
    }

    /// <summary>
    /// Asks the user whether an existing file should be overwritten, or a new path selected.
    /// </summary>
    /// <returns><see cref="DialogResult.Yes"/> to overwrite, <see cref="DialogResult.No"/> to select a new path, <see cref="DialogResult.Cancel"/> otherwise.</returns>
    public static async Task<DialogResult> RequestOverwrite(Window? owner, string exist)
    {
        var options = new[] { MessageStrings.MsgDialogFileOverwrite, MessageStrings.MsgDialogFileSaveAs };
        var index = await TrySelectIndex(owner, MessageStrings.MsgDialogFileSaveReplace, exist, options, 0);
        return index switch
        {
            0 => DialogResult.Yes,
            1 => DialogResult.No,
            _ => DialogResult.Cancel,
        };
    }
}
