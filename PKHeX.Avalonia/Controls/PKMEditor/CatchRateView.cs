using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Generation 1 catch rate editor (port of the WinForms <c>CatchRate</c>).
/// </summary>
public sealed class CatchRateView : StackPanel
{
    private PK1? Entity;
    public IMainEditor? MainEditor { get; set; }

    private readonly NumericUpDown NUD_CatchRate = UiFactory.NumericUpDown("NUD_CatchRate", 0, 255, 80);
    private readonly Button B_Clear = UiFactory.Button("B_Clear", "Clear");
    private readonly Button B_Reset = UiFactory.Button("B_Reset", "Reset");

    public CatchRateView()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 4;
        Children.Add(NUD_CatchRate);
        Children.Add(B_Clear);
        Children.Add(B_Reset);
        NUD_CatchRate.ValueChanged += (_, _) => Entity?.CatchRate = (byte)(NUD_CatchRate.Value ?? 0);
        B_Clear.Click += (_, _) => NUD_CatchRate.Value = 0;
        B_Reset.Click += (_, _) => Reset();
    }

    public void LoadPK1(PK1 pk) => NUD_CatchRate.Value = (Entity = pk).CatchRate;

    private void Reset()
    {
        if (Entity is null || MainEditor is null)
            return;
        var sav = MainEditor.RequestSaveFile;
        NUD_CatchRate.Value = CatchRateApplicator.GetSuggestedCatchRate(Entity, sav);
    }
}
