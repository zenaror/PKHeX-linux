using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// List of the save's extra entity slots (GTS, fused, battle box, ...) grouped by storage type
/// (port of the WinForms <c>SlotList</c>).
/// </summary>
public sealed class SlotListView : UserControl, ISlotViewer<SlotView>
{
    private readonly List<SlotView> slots = [];
    private List<SlotInfoMisc> SlotOffsets = [];
    private readonly StackPanel FLP_Slots = new() { Orientation = Orientation.Vertical, Spacing = 2 };

    public int SlotCount { get; private set; }
    public SaveFile SAV => Host?.SAV ?? throw new ArgumentNullException(nameof(SAV));
    public ISaveHost? Host { get; set; }
    public bool FlagIllegal { get; set; }

    private Func<PKM, bool>? _searchFilter;

    public SlotListView() => Content = new ScrollViewer
    {
        Content = FLP_Slots,
        MaxHeight = 460,
        HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
    };

    /// <summary>
    /// Shows the given extra slots.
    /// </summary>
    /// <remarks>Slot views are pooled, matching the WinForms control.</remarks>
    public void Initialize(List<SlotInfoMisc> list)
    {
        SlotOffsets = list;
        LoadSlots(list.Count);
    }

    /// <summary>Hides every slot.</summary>
    public void HideAllSlots() => LoadSlots(0);

    private void LoadSlots(int count)
    {
        // The slot views are pooled and re-added on every save load. Clearing the outer panel only detaches the rows,
        // so each row has to release its slots as well; otherwise re-adding one throws "already has a visual parent".
        foreach (var child in FLP_Slots.Children)
        {
            if (child is Panel row)
                row.Children.Clear();
        }
        FLP_Slots.Children.Clear();
        if (count == 0)
        {
            SlotCount = 0;
            return;
        }
        AddSlots(count);
        AddControls(count);
        SlotCount = count;
    }

    private void AddSlots(int after)
    {
        var spriter = SpriteUtil.Spriter;
        for (int i = slots.Count; i < after; i++)
        {
            var pb = new SlotView(spriter.Width, spriter.Height) { Name = $"bpkm{i}", Margin = new global::Avalonia.Thickness(2) };
            pb.PointerPressed += (_, e) => Host?.SlotPointerPressed(this, pb, e);
            pb.PointerMoved += (_, e) => Host?.SlotPointerMoved(this, pb, e);
            DragDrop.SetAllowDrop(pb, true);
            pb.AddHandler(DragDrop.DragOverEvent, (_, e) => Host?.SlotDragOver(pb, e));
            pb.AddHandler(DragDrop.DropEvent, (_, e) => Host?.SlotDrop(pb, e));
            pb.PointerEntered += (_, _) => Host?.SlotPointerEntered(this, pb);
            pb.PointerExited += (_, _) => Host?.SlotPointerExited(this, pb);
            slots.Add(pb);
        }
    }

    private void AddControls(int countTotal)
    {
        var type = string.Empty;
        WrapPanel? row = null;
        for (int i = 0; i < countTotal; i++)
        {
            var safeType = GetSafeType(SlotOffsets[i].Type);
            var text = Translator.TranslateEnum(safeType, MainWindow.CurrentLanguage);
            if (text != type)
            {
                type = text;
                FLP_Slots.Children.Add(new TextBlock { Name = $"{DynamicLabelPrefix}{safeType}", Text = text, Margin = new global::Avalonia.Thickness(0, 6, 0, 0) });
                // WinForms stacks these in a flow panel; wrap so a six-slot group (Battle Box) stays on screen.
                row = new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = (2 * (SpriteUtil.Spriter.Width + 8)) + 8 };
                FLP_Slots.Children.Add(row);
            }
            row!.Children.Add(slots[i]);
        }
    }

    /// <summary>
    /// Groups the type into a parent type, if applicable. No need to differentiate many slots.
    /// </summary>
    private static StorageSlotType GetSafeType(StorageSlotType type) => type switch
    {
        StorageSlotType.FusedKyurem => StorageSlotType.Fused,
        StorageSlotType.FusedCalyrex => StorageSlotType.Fused,
        StorageSlotType.FusedNecrozmaS => StorageSlotType.Fused,
        StorageSlotType.FusedNecrozmaM => StorageSlotType.Fused,
        _ => type,
    };

    public const string DynamicLabelPrefix = $"L_{nameof(StorageSlotType)}";

    public void ResetSlots()
    {
        for (int i = 0; i < SlotOffsets.Count; i++)
        {
            var info = SlotOffsets[i];
            var pk = info.Read(SAV);
            SlotUtil.UpdateSlot(slots[i], info, pk, SAV, GetFlags(pk, info.HideLegality));
        }
    }

    private SlotVisibilityType GetFlags(PKM pk, bool ignoreLegality = false)
    {
        var result = SlotVisibilityType.None;
        if (FlagIllegal && !ignoreLegality)
            result |= SlotVisibilityType.CheckLegalityIndicate;
        if (_searchFilter != null && !_searchFilter(pk))
            result |= SlotVisibilityType.FilterMismatch;
        return result;
    }

    #region ISlotViewer

    public IList<SlotView> SlotPictureBoxes => slots;
    public int ViewIndex { get; set; } = -1;

    public void NotifySlotOld(ISlotInfo previous)
    {
        if (previous is not SlotInfoMisc m)
            return;
        var index = SlotOffsets.FindIndex(m.Equals);
        if (index < 0)
            return;
        slots[index].BackgroundBitmap = null;
    }

    public void NotifySlotChanged(ISlotInfo slot, SlotTouchType type, PKM pk)
    {
        if (slot is not SlotInfoMisc m)
            return;
        var index = GetViewIndex(m);
        if (index < 0)
            return;
        SlotUtil.UpdateSlot(slots[index], slot, pk, SAV, GetFlags(pk, m.HideLegality), type);
    }

    public void ApplyNewFilter(Func<PKM, bool>? filter, bool reload = true)
    {
        if (filter == _searchFilter)
            return;
        _searchFilter = filter;
        if (reload)
            ResetSlots();
    }

    public int GetViewIndex(ISlotInfo info) => SlotOffsets.FindIndex(info.Equals);

    public ISlotInfo GetSlotData(SlotView view) => GetSlotData(slots.IndexOf(view));

    public ISlotInfo GetSlotData(int slot) => SlotOffsets[slot];

    #endregion
}
