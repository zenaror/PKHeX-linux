using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using PKHeX.Core;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Clipboard access (port of the WinForms <c>WinFormsUtil.SetClipboardText</c> and related helpers).
/// </summary>
public static class ClipboardService
{
    private static IClipboard? GetClipboard(TopLevel? top) => top?.Clipboard;

    /// <summary>
    /// Copies text to the clipboard; shows an error dialog on failure.
    /// </summary>
    public static async Task<bool> SetText(Window? owner, string text)
    {
        var clipboard = GetClipboard(owner);
        if (clipboard is null)
        {
            await AppDialogs.Error(owner, MessageStrings.MsgClipboardFailWrite);
            return false;
        }
        try
        {
            await clipboard.SetTextAsync(text);
            return true;
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(owner, MessageStrings.MsgClipboardFailWrite, ex);
            return false;
        }
    }

    /// <summary>
    /// Gets the clipboard text, or null if the clipboard has no text.
    /// </summary>
    public static async Task<string?> GetText(Window? owner)
    {
        var clipboard = GetClipboard(owner);
        if (clipboard is null)
            return null;
        try
        {
            return await clipboard.TryGetTextAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return null;
        }
    }
}
