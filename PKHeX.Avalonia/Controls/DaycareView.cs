using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Drawing;
using PKHeX.Core;
using PKHeX.Drawing;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Daycare group: the two deposited entities with their occupancy/experience readouts, the egg flag and the seed
/// (port of the daycare half of the WinForms <c>SAVEditor</c>'s Other tab).
/// </summary>
public sealed class DaycareView : UserControl, ISlotViewer<SlotView>
{
    public IList<SlotView> SlotPictureBoxes { get; } = [];
    public SaveFile SAV => Host?.SAV ?? throw new ArgumentNullException(nameof(SAV));
    public ISaveHost? Host { get; set; }
    public bool FlagIllegal { get; set; }

    /// <summary>Index of the daycare being shown when the save has more than one.</summary>
    public int DaycareIndex { get; set; }

    /// <summary>Raised when the group header is clicked and the save has multiple daycares.</summary>
    public event Action? SwitchRequested;

    private readonly TextBlock[] L_SlotOccupied = [UiFactory.Label("L_DC1", "1:"), UiFactory.Label("L_DC2", "2:")];
    private readonly TextBlock[] L_SlotEXP = [UiFactory.Label("L_XP1", "EXP:"), UiFactory.Label("L_XP2", "EXP:")];
    private readonly TextBox[] TB_SlotEXP = [UiFactory.Text("TB_Daycare1XP", 10, 90), UiFactory.Text("TB_Daycare2XP", 10, 90)];
    private readonly CheckBox DayCare_HasEgg = UiFactory.Check("DayCare_HasEgg", "Egg Available");
    private readonly TextBlock L_DaycareSeed = UiFactory.Label("L_DaycareSeed", "Seed:");
    private readonly NumericTextBox TB_RNGSeed = UiFactory.Numeric("TB_RNGSeed", 32, 200, hex: true);
    private readonly GroupBoxView GB_Daycare;

    public DaycareView()
    {
        var spriter = SpriteUtil.Spriter;
        var rows = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
        for (int i = 0; i < 2; i++)
        {
            var slot = new SlotView(spriter.Width, spriter.Height) { Name = $"dcpkx{i + 1}" };
            SlotPictureBoxes.Add(slot);
            TB_SlotEXP[i].IsReadOnly = true;
            rows.Children.Add(UiFactory.Row(slot, UiFactory.Column(L_SlotOccupied[i], UiFactory.Row(L_SlotEXP[i], TB_SlotEXP[i]))));
        }

        // The WinForms checkbox has no change handler: it reports the flag rather than editing it.
        DayCare_HasEgg.IsHitTestVisible = false;
        DayCare_HasEgg.Focusable = false;

        var body = UiFactory.Column(rows, DayCare_HasEgg, UiFactory.Row(L_DaycareSeed, TB_RNGSeed));
        GB_Daycare = new GroupBoxView("GB_Daycare", "Daycare", body) { HorizontalAlignment = HorizontalAlignment.Left };
        GB_Daycare.AttachHeaderClick(() => SwitchRequested?.Invoke());
        Content = GB_Daycare;

        foreach (var pb in SlotPictureBoxes)
        {
            pb.PointerPressed += (_, e) => Host?.SlotPointerPressed(this, pb, e);
            pb.PointerMoved += (_, e) => Host?.SlotPointerMoved(this, pb, e);
            DragDrop.SetAllowDrop(pb, true);
            pb.AddHandler(DragDrop.DragOverEvent, (_, e) => Host?.SlotDragOver(pb, e));
            pb.AddHandler(DragDrop.DropEvent, (_, e) => Host?.SlotDrop(pb, e));
            pb.PointerEntered += (_, _) => Host?.SlotPointerEntered(this, pb);
            pb.PointerExited += (_, _) => Host?.SlotPointerExited(this, pb);
        }

        TB_RNGSeed.LostFocus += (_, _) => SaveSeed();
    }

    /// <summary>Daycare currently being shown, or null when the save has none.</summary>
    public IDaycareStorage? Current
    {
        get
        {
            if (Host is null)
                return null;
            if (SAV is IDaycareMulti m)
            {
                if (DaycareIndex < m.DaycareCount)
                    return m[DaycareIndex];
                return m[DaycareIndex = 0];
            }
            if (SAV is IDaycareStorage s)
                return s;
            return null;
        }
    }

    public void ResetSlots()
    {
        if (Current is not { } s)
            return;

        var slotCount = s.DaycareSlotCount;
        for (int i = 0; i < SlotPictureBoxes.Count; i++)
        {
            if (i >= slotCount)
            {
                L_SlotOccupied[i].IsVisible = false;
                L_SlotEXP[i].IsVisible = TB_SlotEXP[i].IsVisible = false;
                SlotPictureBoxes[i].IsVisible = false;
                continue;
            }

            SlotPictureBoxes[i].IsVisible = true;
            if (s is IDaycareExperience dExp)
            {
                L_SlotEXP[i].IsVisible = TB_SlotEXP[i].IsVisible = true;
                TB_SlotEXP[i].Text = dExp.GetDaycareEXP(i).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                L_SlotEXP[i].IsVisible = TB_SlotEXP[i].IsVisible = false;
            }

            var occupied = s.IsDaycareOccupied(i);
            L_SlotOccupied[i].IsVisible = true;
            L_SlotOccupied[i].Text = occupied ? $"{i + 1}: ✓" : $"{i + 1}: ✘";
            UpdateSlot(i, fade: !occupied);
        }

        LoadEggState(s);
        LoadSeed(s);
    }

    private void UpdateSlot(int index, bool fade = false)
    {
        var info = GetSlotData(index);
        var pb = SlotPictureBoxes[index];
        var pk = info.Read(SAV);
        SlotUtil.UpdateSlot(pb, info, pk, SAV, GetFlags(pk));
        if (!fade || pb.Sprite is null)
            return;

        // An unoccupied slot still holds data; show it faded, as WinForms does.
        var sk = info.Read(SAV).Sprite(SAV, -1, -1, GetFlags(pk), info.Type);
        var faded = ImageUtil.CopyChangeOpacity(sk, 0.6);
        sk.Release();
        pb.Sprite = faded.ToAvaloniaBitmapAndDispose();
    }

    private void LoadEggState(IDaycareStorage s)
    {
        if (s is IDaycareEggState dEgg)
        {
            DayCare_HasEgg.IsVisible = true;
            DayCare_HasEgg.IsChecked = dEgg.IsEggAvailable;
        }
        else
        {
            DayCare_HasEgg.IsVisible = false;
        }
    }

    private void LoadSeed(IDaycareStorage s)
    {
        switch (s)
        {
            case IDaycareRandomState<ushort> u16: SetSeed(4, $"{u16.Seed:X4}"); return;
            case IDaycareRandomState<uint> u32: SetSeed(8, $"{u32.Seed:X8}"); return;
            case IDaycareRandomState<ulong> u64: SetSeed(16, $"{u64.Seed:X16}"); return;
            case IDaycareRandomState<UInt128> u128: SetSeed(32, $"{u128.Seed:X32}"); return;
            default:
                L_DaycareSeed.IsVisible = TB_RNGSeed.IsVisible = false;
                return;
        }
    }

    private void SetSeed(int maxLength, string text)
    {
        TB_RNGSeed.MaxLength = maxLength;
        TB_RNGSeed.Text = text;
        L_DaycareSeed.IsVisible = TB_RNGSeed.IsVisible = true;
    }

    private void SaveSeed()
    {
        if (Current is not { } daycare)
            return;
        var text = Util.GetOnlyHex(TB_RNGSeed.Text ?? string.Empty);
        const NumberStyles hex = NumberStyles.HexNumber;
        switch (daycare)
        {
            case IDaycareRandomState<ushort> u16 when ushort.TryParse(text, hex, null, out var v16): u16.Seed = v16; break;
            case IDaycareRandomState<uint> u32 when uint.TryParse(text, hex, null, out var v32): u32.Seed = v32; break;
            case IDaycareRandomState<ulong> u64 when ulong.TryParse(text, hex, null, out var v64): u64.Seed = v64; break;
            case IDaycareRandomState<UInt128> u128 when UInt128.TryParse(text, hex, null, out var v128): u128.Seed = v128; break;
        }
        LoadSeed(daycare);
    }

    private SlotVisibilityType GetFlags(PKM pk)
    {
        var result = SlotVisibilityType.None;
        if (FlagIllegal)
            result |= SlotVisibilityType.CheckLegalityIndicate;
        return result;
    }

    #region ISlotViewer

    public int ViewIndex => -2;

    public void NotifySlotOld(ISlotInfo previous)
    {
        var index = GetViewIndex(previous);
        if (index < 0)
            return;
        SlotPictureBoxes[index].BackgroundBitmap = null;
    }

    public void NotifySlotChanged(ISlotInfo slot, SlotTouchType type, PKM pk)
    {
        var index = GetViewIndex(slot);
        if (index < 0)
            return;
        SlotUtil.UpdateSlot(SlotPictureBoxes[index], slot, pk, SAV, GetFlags(pk), type);
    }

    public int GetViewIndex(ISlotInfo slot)
    {
        if (Current is not { } dc)
            return -1;
        for (int i = 0; i < SlotPictureBoxes.Count; i++)
        {
            if (dc.DaycareSlotCount == i)
                break;
            if (GetSlotData(i).Equals(slot))
                return i;
        }
        return -1;
    }

    public ISlotInfo GetSlotData(SlotView view) => GetSlotData(SlotPictureBoxes.IndexOf(view));

    private SlotInfoMisc GetSlotData(int index)
    {
        if (Current is not { } s)
            throw new InvalidOperationException("No daycare is loaded.");
        return new SlotInfoMisc(s.GetDaycareSlot(index), index) { Type = StorageSlotType.Daycare };
    }

    public void ApplyNewFilter(Func<PKM, bool>? filter, bool reload = true) { }

    #endregion
}
