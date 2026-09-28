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

    /// <summary>
    /// A western Crystal save that carries the extra SRAM banks of a Mobile Adapter cart (64 KB / MBC30 instead of the
    /// retail 32 KB): a mobile ROM hack's save, not one the vanilla Crystal can have.
    /// </summary>
    /// <remarks>
    /// <see cref="SAV2.EnableGSBallMobileEvent"/> writes the backup flag at 0x3E44, where the vanilla Crystal keeps it.
    /// These builds give the player a longer profile (a zip code field), so their backup sits past it - and 0x3E44 is a
    /// character of that zip code. Pressing the button would leave the backup unset (the game copies it back over the
    /// flag, undoing the event) and damage the profile, so the button is disabled here instead. The Japanese Crystal is
    /// not affected: it is 64 KB retail, and its 0xA000 / 0xA083 are the addresses Core already writes.
    /// </remarks>
    private static bool IsMobileHackSave(SAV2 sav)
        => sav is { Japanese: false, Version: GameVersion.C } && sav.Data.Length > SaveUtil.SIZE_G2RAW_U;

    public Misc2Window(SAV2 sav) : base("SAV_Misc2", "Misc Edits")
    {
        SAV = (SAV2)(Origin = sav).Clone();

        var mobileHack = IsMobileHackSave(SAV);
        B_VirtualConsoleGSBall.IsVisible = SAV.Version is GameVersion.C;
        B_VirtualConsoleGSBall.IsEnabled = !mobileHack && !SAV.IsEnabledGSBallMobileEvent;
        if (mobileHack)
        {
            ToolTip.SetTip(B_VirtualConsoleGSBall,
                "Disabled: this save carries the extra SRAM banks of a Mobile Adapter build, which keeps the event's "
                + "backup flag past a longer player profile - not at 0x3E44, where this button would write it and "
                + "overwrite a character of the player's zip code. Use the Mobile Adapter plugin's own GS Ball button "
                + "on the Flags tab, which writes the address the build actually reads.");
            // Avalonia hides a tooltip on a disabled control unless it is told otherwise, and the reason matters most there.
            ToolTip.SetShowOnDisabled(B_VirtualConsoleGSBall, true);
        }
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
