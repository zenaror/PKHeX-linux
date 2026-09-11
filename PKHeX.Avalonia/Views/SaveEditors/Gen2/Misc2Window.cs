using Avalonia.Controls;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen2;

/// <summary>
/// Miscellaneous Gen 2 edits (port of the WinForms <c>SAV_Misc2</c>).
/// </summary>
public sealed class Misc2Window : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV2 SAV;
    private readonly Button B_VirtualConsoleGSBall = UiFactory.Button("B_VirtualConsoleGSBall", "Enable GS Ball Event (Virtual Console)");

    public Misc2Window(SAV2 sav) : base("SAV_Misc2", "Misc Edits")
    {
        SAV = (SAV2)(Origin = sav).Clone();

        B_VirtualConsoleGSBall.IsVisible = SAV.Version is GameVersion.C;
        B_VirtualConsoleGSBall.IsEnabled = !SAV.IsEnabledGSBallMobileEvent;
        B_VirtualConsoleGSBall.Click += (_, _) =>
        {
            // Don't bother checking if the save is from Virtual Console.
            // Can be moved between VC and GB era, and can be a quick way to enable the event on either.
            SAV.EnableGSBallMobileEvent();
            B_VirtualConsoleGSBall.IsEnabled = false;
        };

        SetBody(UiFactory.Column(B_VirtualConsoleGSBall));
    }

    protected override void OnSave()
    {
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
