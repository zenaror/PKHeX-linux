using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Shows the stored status condition; click to change (port of the WinForms <c>StatusConditionView</c>).
/// </summary>
public sealed class StatusConditionView : Border
{
    private PKM? pk;
    private bool Loading;
    private readonly Image PB_Status = new() { Stretch = Stretch.None, Width = 32, Height = 32 };
    private global::Avalonia.Media.Imaging.Bitmap? Current;

    public StatusConditionView()
    {
        Width = 32;
        Height = 32;
        Background = Brushes.Transparent;
        Cursor = new Cursor(StandardCursorType.Hand);
        Child = PB_Status;
        this.AttachClickHandled(m => _ = PB_Status_Click());
    }

    public void LoadPKM(PKM entity)
    {
        pk = entity;
        LoadStoredValues();
    }

    public void LoadStoredValues()
    {
        if (pk is null || Loading)
            return;
        Loading = true;
        if (!pk.PartyStatsPresent)
            ClearStatus();
        else if (pk.Stat_HPCurrent == 0)
            SetFaint();
        else
            SetStatus(pk.Status_Condition, pk.Format);
        Loading = false;
    }

    private void SetImage(SkiaSharp.SKBitmap? img)
    {
        var old = Current;
        Current = img?.ToAvaloniaBitmapAndDispose();
        PB_Status.Source = Current;
        old?.Dispose();
    }

    private void SetFaint()
    {
        SetImage(PKHeX.Drawing.PokeSprite.Properties.Resources.ResourceManager.GetObject("sickfaint"));
        ToolTip.SetTip(this, null);
    }

    private void ClearStatus()
    {
        SetImage(null);
        ToolTip.SetTip(this, null);
    }

    private void SetStatus(int value, int generation)
    {
        if (generation <= 4)
        {
            StatusCondition status = (StatusCondition)(value & 0xFF);
            SetImage(status.GetStatusSprite());
            var text = Translator.TranslateEnum(status, MainWindow.CurrentLanguage);
            ToolTip.SetTip(this, $"Status Condition: {text}");
        }
        else
        {
            StatusType status = (StatusType)(value & 0xFF);
            SetImage(status.GetStatusSprite());
            var text = Translator.TranslateEnum(status, MainWindow.CurrentLanguage);
            ToolTip.SetTip(this, $"Status Condition: {text}");
        }
    }

    private async System.Threading.Tasks.Task PB_Status_Click()
    {
        if (pk is null)
            return;
        int generation = pk.Format;
        var form = new StatusBrowserWindow(generation);
        form.LoadList(pk);
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
            return;
        await form.ShowDialog(owner);
        if (!form.WasChosen)
            return;
        var current = pk.Status_Condition;
        current &= ~0xFF;
        current |= generation <= 4 ? (int)form.Choice : (int)form.Choice.GetStatusType();
        pk.Status_Condition = current;
        LoadStoredValues();
    }
}
