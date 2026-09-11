using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Party viewer: 2x3 grid of party slots.
/// </summary>
/// <remarks>Port of the WinForms <c>PartyEditor</c>.</remarks>
public sealed class PartyView : UserControl, ISlotViewer<SlotView>
{
    public IList<SlotView> SlotPictureBoxes { get; private set; } = [];
    public SaveFile SAV => Host?.SAV ?? throw new ArgumentNullException(nameof(SAV));

    public int BoxSlotCount { get; private set; }
    public ISaveHost? Host { get; set; }
    public bool FlagIllegal { get; set; }

    private Func<PKM, bool>? _searchFilter;

    public readonly PokeGrid PartyPokeGrid = new() { Name = "PartyPokeGrid" };

    public PartyView()
    {
        Content = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new global::Avalonia.Thickness(0, 8, 0, 0),
            Children = { PartyPokeGrid },
        };
    }

    public void ApplyNewFilter(Func<PKM, bool>? filter, bool reload = true)
    {
        if (filter == _searchFilter)
            return;
        _searchFilter = filter;
        if (reload && SAV.HasParty)
            ResetSlots();
    }

    internal bool InitializeGrid()
    {
        const int width = 2;
        const int height = 3;
        if (!PartyPokeGrid.InitializeGrid(width, height, SpriteUtil.Spriter))
            return false;

        InitializeSlots();
        return true;
    }

    private void InitializeSlots()
    {
        SlotPictureBoxes = PartyPokeGrid.Entries;
        BoxSlotCount = SlotPictureBoxes.Count;
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
    }

    public void NotifySlotOld(ISlotInfo previous)
    {
        if (previous is not SlotInfoParty p)
            return;

        var pb = SlotPictureBoxes[p.Slot];
        pb.BackgroundBitmap = null;
    }

    public void NotifySlotChanged(ISlotInfo slot, SlotTouchType type, PKM pk)
    {
        int index = GetViewIndex(slot);
        if (index < 0)
            return;

        if (type == SlotTouchType.Delete)
        {
            ResetSlots();
            return;
        }

        var pb = SlotPictureBoxes[index];
        var flags = GetFlags(pk);
        SlotUtil.UpdateSlot(pb, slot, pk, SAV, flags, type);
    }

    private SlotVisibilityType GetFlags(PKM pk)
    {
        var result = SlotVisibilityType.None;
        if (FlagIllegal)
            result |= SlotVisibilityType.CheckLegalityIndicate;
        if (_searchFilter != null && !_searchFilter(pk))
            result |= SlotVisibilityType.FilterMismatch;
        return result;
    }

    public int GetViewIndex(ISlotInfo slot)
    {
        if (slot is not SlotInfoParty p)
            return -1;
        return p.Slot;
    }

    public ISlotInfo GetSlotData(SlotView view)
    {
        int slot = GetSlot(view);
        return new SlotInfoParty(slot);
    }

    private int GetSlot(SlotView sender) => SlotPictureBoxes.IndexOf(sender);
    public int ViewIndex => -999;

    public void ResetSlots()
    {
        if (SlotPictureBoxes.Count == 0)
            return;
        foreach (var pb in SlotPictureBoxes)
        {
            var slot = (SlotInfoParty)GetSlotData(pb);
            var pk = slot.Read(SAV);
            SlotUtil.UpdateSlot(pb, slot, pk, SAV, GetFlags(pk));
        }

        if (Host?.Publisher.Previous is SlotInfoParty p)
            SlotPictureBoxes[p.Slot].BackgroundBitmap = SlotUtil.GetTouchTypeBackground(Host.Publisher.PreviousType);
    }

    public bool InitializeFromSAV(SaveFile sav)
    {
        IsVisible = sav.HasParty;
        return InitializeGrid();
    }
}
