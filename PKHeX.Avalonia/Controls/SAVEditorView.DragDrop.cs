using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Slot drag &amp; drop (port of the WinForms <c>SlotChangeManager</c> drag handling).
/// </summary>
/// <remarks>
/// Dragging a slot writes a temporary entity file so the drag also works towards other applications;
/// drops inside the program are resolved through the slot info recorded when the drag started.
/// </remarks>
public sealed partial class SAVEditorView
{
    private SlotViewInfo<SlotView>? DragSource;
    private Point? DragStart;
    private SlotView? DragStartSlot;
    private PointerPressedEventArgs? DragPress;

    /// <summary>Pixels the pointer must travel before a slot drag begins.</summary>
    private static double DragThreshold => Math.Max(4, MainWindow.Settings.Advanced.DragStartThreshold);

    private void SlotPointerPressedForDrag(ISlotViewer<SlotView> viewer, SlotView view, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(view).Properties.IsLeftButtonPressed)
            return;
        if (view.Sprite is null)
            return; // empty slot: nothing to drag
        DragStart = e.GetPosition(this);
        DragStartSlot = view;
        DragPress = e; // the drag API starts from the press event
        DragSource = new SlotViewInfo<SlotView>(viewer.GetSlotData(view), viewer);
    }

    private void SlotPointerMoved(SlotView view, PointerEventArgs e)
    {
        if (DragStart is not { } start || DragStartSlot != view || DragSource is null)
            return;
        if (!e.GetCurrentPoint(view).Properties.IsLeftButtonPressed)
        {
            ResetDrag();
            return;
        }

        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - start.X) < DragThreshold && Math.Abs(pos.Y - start.Y) < DragThreshold)
            return;

        DragStart = null; // only start once
        if (DragPress is { } press)
            _ = StartSlotDrag(view, press);
    }

    private Point? BoxDragStart;
    private PointerPressedEventArgs? BoxDragPress;

    /// <summary>
    /// Arms a box-binary drag when the Box tab header is pressed (WinForms <c>TabMouseDown</c>).
    /// </summary>
    /// <remarks>Opt-in, like upstream: <see cref="SlotExportSettings.AllowBoxDataDrop"/> defaults to off.</remarks>
    private void BoxTabPointerPressed(PointerPressedEventArgs e)
    {
        ResetBoxDrag();
        if (!MainWindow.Settings.SlotExport.AllowBoxDataDrop)
            return;
        if (!e.GetCurrentPoint(Tab_Box).Properties.IsLeftButtonPressed)
            return;
        if (e.KeyModifiers is KeyModifiers.Alt or KeyModifiers.Shift)
            return;
        if (!SAV.HasBox)
            return;

        BoxDragStart = e.GetPosition(this);
        BoxDragPress = e; // the drag API starts from the press event
    }

    /// <summary>
    /// Starts the drag once the pointer leaves the press position (WinForms <c>TabMouseMove</c>).
    /// </summary>
    private void BoxTabPointerMoved(PointerEventArgs e)
    {
        if (BoxDragStart is not { } start || BoxDragPress is not { } press)
            return;
        if (!e.GetCurrentPoint(Tab_Box).Properties.IsLeftButtonPressed)
        {
            ResetBoxDrag();
            return;
        }

        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - start.X) < DragThreshold && Math.Abs(pos.Y - start.Y) < DragThreshold)
            return;

        BoxDragStart = null; // only start once
        _ = StartBoxDrag(press);
    }

    private void ResetBoxDrag()
    {
        BoxDragStart = null;
        BoxDragPress = null;
    }

    /// <summary>
    /// Writes the current box as a binary temp file and drags it out, so it can be dropped into another application
    /// (or back into the program, which imports it through <see cref="OpenPCBoxBin"/>).
    /// </summary>
    private async Task StartBoxDrag(PointerPressedEventArgs e)
    {
        if (Owner is null)
            return;

        var src = Box.CurrentBox;
        var bin = SAV.GetBoxBinary(src);
        if (bin.Length == 0)
        {
            ResetBoxDrag();
            return;
        }

        var newFile = Path.Combine(Path.GetTempPath(), $"box_{src}.bin");
        try
        {
            await File.WriteAllBytesAsync(newFile, bin);
            var file = await Owner.StorageProvider.TryGetFileFromPathAsync(newFile);
            if (file is null)
                return;
            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.CreateFile(file));
            await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Copy);
        }
        // Tons of things can happen with drag & drop; don't try to handle things, just indicate failure.
        catch (Exception x)
        {
            await AppDialogs.Error(Owner, "Drag && Drop Error", x);
        }
        finally
        {
            DeleteTempAsync(newFile);
            ResetBoxDrag();
        }
    }

    private void ResetDrag()
    {
        DragStart = null;
        DragStartSlot = null;
        DragSource = null;
        DragPress = null;
    }

    private async Task StartSlotDrag(SlotView view, PointerPressedEventArgs e)
    {
        var source = DragSource;
        if (source is null || Owner is null)
            return;

        var pk = source.ReadCurrent();
        if (pk.Species == 0)
        {
            ResetDrag();
            return;
        }

        bool encrypt = MainWindow.CurrentModifiers == KeyModifiers.Control;
        var data = new byte[pk.SIZE_PARTY];
        if (!encrypt)
            pk.WriteDecryptedDataParty(data);
        else
            pk.WriteEncryptedDataParty(data);

        var newFile = FileUtil.GetPKMTempFileName(pk, encrypt);
        try
        {
            await File.WriteAllBytesAsync(newFile, data);
            var file = await Owner.StorageProvider.TryGetFileFromPathAsync(newFile);
            if (file is null)
                return;
            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.CreateFile(file));
            SlotUtil.UpdateSlot(view, source.Slot, pk, source.View.SAV, SlotVisibilityType.None, SlotTouchType.Get);
            await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Move | DragDropEffects.Copy);
        }
        catch (Exception x)
        {
            await AppDialogs.Error(Owner, "Drag && Drop Error", x);
        }
        finally
        {
            DeleteTempAsync(newFile);
            ResetDrag();
            ReloadSlots();
        }
    }

    private static void DeleteTempAsync(string path) => _ = Task.Run(async () =>
    {
        await Task.Delay(20_000).ConfigureAwait(false);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // Ignore; the receiving application may still hold the file.
        }
    });

    private void SlotDragOverCore(SlotView view, DragEventArgs e)
    {
        var info = GetSlotInfo(view);
        bool canWrite = info?.CanWriteTo() == true;
        e.DragEffects = !canWrite ? DragDropEffects.None
            : DragSource is not null ? DragDropEffects.Move
            : DragDropEffects.Copy;
        e.Handled = true;
    }

    private SlotViewInfo<SlotView>? GetSlotInfo(SlotView view)
    {
        if (Box.SlotPictureBoxes.Contains(view))
            return new SlotViewInfo<SlotView>(Box.GetSlotData(view), Box);
        if (SL_Party.SlotPictureBoxes.Contains(view))
            return new SlotViewInfo<SlotView>(SL_Party.GetSlotData(view), SL_Party);
        return null;
    }

    private async Task SlotDropCore(SlotView view, DragEventArgs e)
    {
        e.Handled = true;
        var dest = GetSlotInfo(view);
        if (dest is null || !dest.CanWriteTo())
        {
            ResetDrag();
            return;
        }

        var source = DragSource;
        if (source is not null)
        {
            await DropFromSlot(source, dest);
            ResetDrag();
            ReloadSlots();
            UpdateUndoRedo();
            return;
        }

        // External file drop onto a slot.
        var files = e.DataTransfer.TryGetFiles();
        if (files is not { Length: not 0 })
            return;
        var path = files[0].TryGetLocalPath();
        if (path is null)
            return;
        if (Directory.Exists(path))
        {
            var result = await LoadBoxes(path);
            if (result.Message.Length != 0)
                await AppDialogs.Alert(Owner, result.Message);
            return;
        }
        await DropFromFile(path, dest);
        ReloadSlots();
        UpdateUndoRedo();
    }

    /// <summary>
    /// Moves / swaps / clones the dragged entity into the destination slot (WinForms <c>TrySetPKMDestination</c>).
    /// </summary>
    private async Task DropFromSlot(SlotViewInfo<SlotView> source, SlotViewInfo<SlotView> dest)
    {
        if (source.Slot.Equals(dest.Slot))
            return;

        var pk = source.ReadCurrent();
        if (dest.CanWriteTo(pk) != WriteBlockedMessage.None)
        {
            await AppDialogs.Alert(Owner, MessageStrings.MsgSaveSlotEmpty);
            return;
        }

        var mod = GetDropModifier();
        if (mod != DropModifier.Clone)
        {
            if (dest.IsEmpty() || mod == DropModifier.Overwrite)
            {
                EditEnv.Slots.Delete(source.Slot);
            }
            else // swap
            {
                var other = dest.ReadCurrent();
                EditEnv.Slots.Set(source.Slot, other, SlotTouchType.Swap);
            }
        }

        var type = mod == DropModifier.Clone ? SlotTouchType.Set : SlotTouchType.Swap;
        EditEnv.Slots.Set(dest.Slot, pk, type);
    }

    /// <summary>
    /// Loads an entity file into the destination slot (WinForms <c>TryLoadFiles</c>).
    /// </summary>
    private async Task DropFromFile(string path, SlotViewInfo<SlotView> dest)
    {
        var sav = dest.View.SAV;
        var temp = FileUtil.GetSingleFromPath(path, sav);
        if (temp is null)
        {
            await AppDialogs.Alert(Owner, MessageStrings.MsgSaveSlotBadData);
            return;
        }

        var pk = EntityConverter.ConvertToType(temp, sav.PKMType, out var result);
        if (pk is null)
        {
            await AppDialogs.Error(Owner, result.GetDisplayString(temp, sav.PKMType));
            return;
        }

        if (!dest.CanWriteTo() && (pk.Species == 0 || pk.IsEgg))
            return;

        if (sav is ILangDeviantSave il && !EntityConverter.IsCompatibleGB(temp, il.Japanese, pk.Japanese))
        {
            var str = EntityConverterResult.IncompatibleLanguageGB.GetIncompatibleGBMessage(pk, il.Japanese);
            await AppDialogs.Error(Owner, str);
            return;
        }

        var errata = sav.EvaluateCompatibility(pk);
        if (errata.Count > 0)
        {
            string concat = string.Join(Environment.NewLine, errata);
            if (await AppDialogs.Prompt(Owner, MessageBoxButtons.YesNo, concat, MessageStrings.MsgContinue) != DialogResult.Yes)
                return;
        }

        EditEnv.Slots.Set(dest.Slot, pk);
    }

    /// <summary>
    /// Drop behavior from the held modifier keys (WinForms <c>SlotUtil.GetDropModifier</c>).
    /// </summary>
    private static DropModifier GetDropModifier() => MainWindow.CurrentModifiers switch
    {
        KeyModifiers.Shift => DropModifier.Clone,
        KeyModifiers.Alt => DropModifier.Overwrite,
        _ => DropModifier.None,
    };
}

/// <summary>
/// Drop behavior modifier (port of the WinForms <c>DropModifier</c>).
/// </summary>
public enum DropModifier
{
    /// <summary>No modifier is applied: move, or swap when the destination is occupied.</summary>
    None,

    /// <summary>Copies the entity, leaving the source slot untouched.</summary>
    Clone,

    /// <summary>Overwrites the destination and clears the source slot.</summary>
    Overwrite,
}
