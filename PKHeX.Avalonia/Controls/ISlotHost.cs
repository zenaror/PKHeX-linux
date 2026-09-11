using Avalonia.Input;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Receives slot interaction events from slot viewers (box, party, ...).
/// </summary>
public interface ISlotHost
{
    void SlotPointerPressed(ISlotViewer<SlotView> viewer, SlotView view, PointerPressedEventArgs e);
    void SlotPointerMoved(ISlotViewer<SlotView> viewer, SlotView view, global::Avalonia.Input.PointerEventArgs e);
    void SlotDragOver(SlotView view, global::Avalonia.Input.DragEventArgs e);
    void SlotDrop(SlotView view, global::Avalonia.Input.DragEventArgs e);
    void SlotPointerEntered(ISlotViewer<SlotView> viewer, SlotView view);
    void SlotPointerExited(ISlotViewer<SlotView> viewer, SlotView view);
}
