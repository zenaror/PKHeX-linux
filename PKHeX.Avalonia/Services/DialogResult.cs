namespace PKHeX.Avalonia.Services;

/// <summary>
/// Result of a message dialog (equivalent to the WinForms <c>DialogResult</c> values used by the application).
/// </summary>
public enum DialogResult
{
    None,
    OK,
    Cancel,
    Abort,
    Yes,
    No,
}

/// <summary>
/// Button sets for message dialogs (equivalent to the WinForms <c>MessageBoxButtons</c> values used by the application).
/// </summary>
public enum MessageBoxButtons
{
    OK,
    OKCancel,
    YesNo,
    YesNoCancel,
}

/// <summary>
/// Icon shown in a message dialog.
/// </summary>
public enum MessageBoxIcon
{
    None,
    Information,
    Question,
    Warning,
    Error,
}
