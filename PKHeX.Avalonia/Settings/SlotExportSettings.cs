using PKHeX.Core;

namespace PKHeX.Avalonia.Settings;

/// <summary>
/// Mirrors the WinForms <c>SlotExportSettings</c> (JSON compatible).
/// </summary>
public sealed class SlotExportSettings
{
    [LocalizedDescription("Settings to use for box exports.")]
    public BoxExportSettings BoxExport { get; set; } = new();

    [LocalizedDescription("Selected File namer to use for box exports for the GUI, if multiple are available.")]
    public string DefaultBoxExportNamer { get; set; } = "";

    [LocalizedDescription("Allow drag and drop of boxdata binary files from the GUI via the Box tab.")]
    public bool AllowBoxDataDrop { get; set; } // default to false, clunky to use
}
