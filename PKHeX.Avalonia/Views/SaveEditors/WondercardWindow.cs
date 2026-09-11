using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using PKHeX.Drawing.Misc;
using PKHeX.Drawing.PokeSprite;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Views.SaveEditors;

/// <summary>
/// Wonder Card album editor (port of the WinForms <c>SAV_Wondercard</c>).
/// </summary>
/// <remarks>
/// Deviation from WinForms: a plain left click views the slot instead of starting a drag, because Alt+click
/// (delete) is captured by most Linux window managers. Dragging still works after the pointer moves.
/// </remarks>
public sealed class WondercardWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SaveFile SAV;
    private readonly IMysteryGiftStorage Cards;
    private readonly IMysteryGiftFlags? Flags;
    private readonly DataMysteryGift[] Album;
    private readonly List<SlotView> Slots = [];

    private readonly StackPanel FLP_Gifts = new() { Name = "FLP_Gifts", Orientation = Orientation.Vertical, Spacing = 2 };
    private readonly TextBlock L_Details = UiFactory.Label("L_Details", "Details:");
    private readonly TextBox RTB = new() { Name = "RTB", AcceptsReturn = true, IsReadOnly = true, Width = 260, Height = 190, TextWrapping = global::Avalonia.Media.TextWrapping.NoWrap };
    private readonly Image PB_Preview = new() { Name = "PB_Preview", Width = 68, Height = 56, Stretch = global::Avalonia.Media.Stretch.None };
    private readonly Button B_Import = UiFactory.Button("B_Import", "Import");
    private readonly Button B_Output = UiFactory.Button("B_Output", "Export");
    private readonly Button L_QR = UiFactory.Button("L_QR", "QR!");
    private readonly TextBlock L_Received = UiFactory.Label("L_Received", "Received List:");
    private readonly ListBox LB_Received = new() { Name = "LB_Received", Width = 100, Height = 150, SelectionMode = SelectionMode.Multiple };
    private readonly ObservableCollection<string> Received = [];
    private readonly Button B_UsedAll = UiFactory.Button("B_UsedAll", "All Used");
    private readonly Button B_UnusedAll = UiFactory.Button("B_UnusedAll", "All Unused");

    private DataMysteryGift? mg;
    private int DragSlot = -1;
    private Point? DragStart;
    private SlotView? DragStartSlot;
    private PointerPressedEventArgs? DragPress;

    public WondercardWindow(SaveFile sav, DataMysteryGift? gift = null) : base("SAV_Wondercard", "Wonder Card I/O")
    {
        SAV = (Origin = sav).Clone();
        Cards = SAV is IMysteryGiftStorageProvider provider
            ? provider.MysteryGiftStorage
            : throw new ArgumentException("Save file does not support Mystery Gifts.", nameof(sav));
        Flags = Cards as IMysteryGiftFlags;
        Album = LoadMysteryGifts();

        BuildGiftSlots(SAV.Generation);
        LB_Received.ItemsSource = Received;

        var right = UiFactory.Column(
            L_Details,
            RTB,
            UiFactory.Row(PB_Preview, UiFactory.Column(B_Import, B_Output, L_QR)),
            L_Received,
            LB_Received,
            UiFactory.Row(B_UsedAll, B_UnusedAll));
        right.Margin = new Thickness(10, 0, 0, 0);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        body.Children.Add(new ScrollViewer { Content = FLP_Gifts, MaxHeight = 460, VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        body.Children.Add(right);
        SetBody(body);

        B_Import.Click += async (_, _) => await ClickImport();
        B_Output.Click += async (_, _) => await ClickExport();
        L_QR.Click += async (_, _) => await ClickQR();
        B_UsedAll.Click += (_, _) => SetAllUsed(true);
        B_UnusedAll.Click += (_, _) => SetAllUsed(false);
        LB_Received.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Delete)
                RemoveSelectedFlags();
        };

        SetGiftBoxes();
        GetReceivedFlags();

        if (Album[0] is WR7) // gift used is not a valid property
            B_UnusedAll.IsVisible = B_UsedAll.IsVisible = L_QR.IsVisible = false;

        AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = DragDropEffects.Copy);
        AddHandler(DragDrop.DropEvent, async (_, e) => await WindowDrop(e));
        DragDrop.SetAllowDrop(this, true);

        if (gift is null)
            ClickView(0);
        else
            ViewGiftData(gift);
    }

    private DataMysteryGift[] LoadMysteryGifts()
    {
        var count = Cards.GiftCountMax;
        var size = SAV is SAV4HGSS ? count + 1 : count;
        var result = new DataMysteryGift[size];
        for (int i = 0; i < count; i++)
            result[i] = Cards.GetMysteryGift(i);
        if (SAV is SAV4HGSS s4)
            result[^1] = s4.LockCapsuleSlot;
        return result;
    }

    #region Slot construction

    private void BuildGiftSlots(byte generation)
    {
        switch (generation)
        {
            case 4: BuildGiftsG4(); break;
            case 5 or 6 or 7: BuildGiftsG567(); break;
            default: throw new ArgumentOutOfRangeException(nameof(generation), generation, "Game not supported.");
        }
    }

    private void BuildGiftsG4()
    {
        FLP_Gifts.Children.Add(GiftRow($"{nameof(PGT)} 1-6", 0, 6));
        FLP_Gifts.Children.Add(GiftRow($"{nameof(PGT)} 7-8", 6, 2));
        var row3 = GiftRow($"{nameof(PCD)} 1-3", 8, 3);
        row3.Margin = new Thickness(0, 12, 0, 0);
        FLP_Gifts.Children.Add(row3);
        if (Album.Length == 12) // lock capsule
            FLP_Gifts.Children.Add(GiftRow(GameInfo.Strings.Item[533], 11, 1));
    }

    private void BuildGiftsG567()
    {
        const int cellsPerRow = 6;
        int remaining = Album.Length;
        for (int i = 0; remaining > 0; i++)
        {
            int count = Math.Min(cellsPerRow, remaining);
            remaining -= count;
            int start = (i * cellsPerRow) + 1;
            FLP_Gifts.Children.Add(GiftRow($"{start}-{start + count - 1}", i * cellsPerRow, count));
        }
    }

    private Control GiftRow(string label, int start, int count)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new TextBlock
        {
            Text = label,
            MinWidth = 60,
            TextAlignment = global::Avalonia.Media.TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var spriter = SpriteUtil.Spriter;
        for (int i = 0; i < count; i++)
        {
            int index = start + i;
            var slot = new SlotView(spriter.Width, spriter.Height) { Margin = new Thickness(1) };
            AttachSlot(slot, index);
            row.Children.Add(slot);
            Slots.Add(slot);
        }
        return row;
    }

    private void AttachSlot(SlotView slot, int index)
    {
        slot.ContextMenu = BuildSlotMenu(index);
        slot.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(slot).Properties.IsLeftButtonPressed)
                return;
            e.Handled = true;
            switch (e.KeyModifiers)
            {
                case KeyModifiers.Shift: _ = ClickSet(index); return;
                case KeyModifiers.Alt: ClickDelete(index); return;
            }
            ClickView(index);
            DragStart = e.GetPosition(this);
            DragStartSlot = slot;
            DragPress = e;
        }, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);

        slot.PointerMoved += (_, e) => SlotPointerMoved(slot, index, e);
        slot.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = DragSlot >= 0 ? DragDropEffects.Move : DragDropEffects.Copy;
            e.Handled = true;
        });
        slot.AddHandler(DragDrop.DropEvent, async (_, e) => await SlotDrop(index, e));
        DragDrop.SetAllowDrop(slot, true);
    }

    private ContextMenu BuildSlotMenu(int index)
    {
        var view = new MenuItem { Name = "mnuView", Header = "View" };
        var set = new MenuItem { Name = "mnuSet", Header = "Set" };
        var del = new MenuItem { Name = "mnuDelete", Header = "Delete" };
        view.Click += (_, _) => ClickView(index);
        set.Click += async (_, _) => await ClickSet(index);
        del.Click += (_, _) => ClickDelete(index);
        var menu = new ContextMenu { Name = "mnuVSD" };
        menu.Items.Add(view);
        menu.Items.Add(set);
        menu.Items.Add(del);
        return menu;
    }

    #endregion

    #region Display

    private void SetBackground(int index, SlotTouchType type)
    {
        var image = SlotUtil.GetTouchTypeBackground(type);
        for (int i = 0; i < Slots.Count; i++)
            Slots[i].BackgroundBitmap = index == i ? image : null;
    }

    private void SetGiftBoxes()
    {
        for (int i = 0; i < Album.Length && i < Slots.Count; i++)
        {
            Slots[i].Sprite = Album[i].Sprite().ToAvaloniaBitmapAndDispose();
        }
    }

    private void ViewGiftData(DataMysteryGift g)
    {
        try
        {
            if (IsVisible && g.GiftUsed)
                _ = PromptGiftUsed(g);

            RTB.Text = string.Join(Environment.NewLine, g.GetDescription());
            PB_Preview.Source = g.Sprite().ToAvaloniaBitmapAndDispose();
            mg = g;
        }
        catch (Exception e)
        {
            // Some user input mystery gifts can have out-of-bounds values.
            RTB.Text = string.Empty;
            _ = AppDialogs.Error(this, MsgMysteryGiftParseTypeUnknown, e);
        }
    }

    private async Task PromptGiftUsed(DataMysteryGift g)
    {
        if (await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgMsyteryGiftUsedAlert, MsgMysteryGiftUsedFix) == DialogResult.Yes)
            g.GiftUsed = false;
    }

    private void GetReceivedFlags()
    {
        Received.Clear();
        if (Flags is not { } f)
            return;
        var count = f.MysteryGiftReceivedFlagMax;
        for (int i = 1; i < count; i++)
        {
            if (f.GetMysteryGiftReceivedFlag(i))
                Received.Add(i.ToString("0000"));
        }
        if (Received.Count > 0)
            LB_Received.SelectedIndex = 0;
    }

    private void SetCardID(int cardID)
    {
        if (Flags is null || (uint)cardID >= Flags.MysteryGiftReceivedFlagMax)
            return;

        var card = cardID.ToString("0000");
        if (!Received.Contains(card))
            Received.Add(card);
        LB_Received.SelectedIndex = Received.IndexOf(card);
    }

    private void RemoveSelectedFlags()
    {
        var selected = LB_Received.SelectedItems?.Cast<string>().ToArray();
        if (selected is not { Length: not 0 })
            return;
        int lastIndex = LB_Received.SelectedIndex;
        foreach (var item in selected)
            Received.Remove(item);
        if (Received.Count == 0)
            return;
        LB_Received.SelectedIndex = Math.Min(lastIndex, Received.Count - 1);
    }

    private void SetAllUsed(bool used)
    {
        foreach (var g in Album)
            g.GiftUsed = used;
        SetGiftBoxes();
    }

    #endregion

    #region Slot actions

    private void ClickView(int index)
    {
        SetBackground(index, SlotTouchType.Get);
        ViewGiftData(Album[index]);
    }

    private static int GetLastUnfilledByType(DataMysteryGift gift, ReadOnlySpan<DataMysteryGift> album)
    {
        for (int i = 0; i < album.Length; i++)
        {
            var exist = album[i];
            if (!exist.IsEmpty)
                continue;
            if (exist.Type != gift.Type)
                continue;
            return i;
        }
        return -1;
    }

    private async Task ClickSet(int index)
    {
        if (mg is not { } gift)
            return;

        if (!gift.IsCardCompatible(SAV, out var msg))
        {
            await AppDialogs.Alert(this, MsgMysteryGiftSlotFail, msg);
            return;
        }

        // Hijack to the latest unfilled slot if the index creates interstitial empty slots.
        int lastUnfilled = GetLastUnfilledByType(gift, Album);
        if (lastUnfilled > -1 && lastUnfilled < index)
            index = lastUnfilled;
        if (gift is PCD { IsLockCapsule: true })
            index = 11;

        var other = Album[index];
        if (gift is PCD { CanConvertToPGT: true } pcd && other is PGT)
        {
            gift = pcd.Gift;
        }
        else if (gift.Type != other.Type)
        {
            await AppDialogs.Alert(this, MsgMysteryGiftSlotFail, $"{gift.Type} != {other.Type}");
            return;
        }
        else if (gift is PCD g && (g is { IsLockCapsule: true } != (index == 11)))
        {
            await AppDialogs.Alert(this, MsgMysteryGiftSlotFail, $"{GameInfo.Strings.Item[533]} slot not valid.");
            return;
        }

        Album[index] = gift.Clone();
        SetBackground(index, SlotTouchType.Set);
        SetGiftBoxes();
        SetCardID(gift.CardID);
    }

    private void ClickDelete(int index)
    {
        Album[index].Clear();

        // Shuffle the blank card down.
        int i = index;
        while (i < Album.Length - 1)
        {
            if (Album[i + 1].IsEmpty)
                break;
            if (Album[i + 1].Type != Album[i].Type)
                break;

            i++;
            (Album[i - 1], Album[i]) = (Album[i], Album[i - 1]);
        }
        SetBackground(i, SlotTouchType.Delete);
        SetGiftBoxes();
    }

    private int SwapSlots(int dest, int src)
    {
        var s1 = Album[dest];
        var s2 = Album[src];

        if (s1.Type != s2.Type)
        {
            if (s2 is PCD { CanConvertToPGT: true } && s1 is PGT)
            {
                var firstEmpty = Array.FindIndex(Album, static z => z.IsEmpty);
                if ((uint)firstEmpty < dest)
                    dest = firstEmpty;

                ViewGiftData(s2);
                _ = ClickSet(dest);
                _ = AppDialogs.Alert(this, string.Format(MsgMysteryGiftSlotAlternate, s2.Type, s1.Type));
            }
            else
            {
                _ = AppDialogs.Alert(this, string.Format(MsgMysteryGiftSlotFailSwap, s2.Type, s1.Type));
            }
            return -1;
        }

        if ((s1 is PCD && dest == 11) || (s2 is PCD && src == 11))
        {
            _ = AppDialogs.Alert(this, MsgMysteryGiftSlotFail, $"{GameInfo.Strings.Item[533]} swap not valid.");
            return -1;
        }

        if (!s1.IsEmpty)
        {
            (Album[src], Album[dest]) = (s1, s2);
            return dest;
        }

        // An empty slot was created; bubble it to the end of its list.
        for (int i = src; i != dest; i++)
        {
            if (Album[i + 1].IsEmpty)
                return i;
            (Album[i + 1], Album[i]) = (Album[i], Album[i + 1]);
        }
        return dest;
    }

    #endregion

    #region Drag and drop

    private void SlotPointerMoved(SlotView slot, int index, PointerEventArgs e)
    {
        if (DragStart is not { } start || DragStartSlot != slot)
            return;
        if (!e.GetCurrentPoint(slot).Properties.IsLeftButtonPressed)
        {
            ResetDrag();
            return;
        }

        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - start.X) < 4 && Math.Abs(pos.Y - start.Y) < 4)
            return;

        DragStart = null; // only start once
        if (DragPress is { } press)
            _ = StartSlotDrag(index, press);
    }

    private void ResetDrag()
    {
        DragStart = null;
        DragStartSlot = null;
        DragPress = null;
        DragSlot = -1;
    }

    private async Task StartSlotDrag(int index, PointerPressedEventArgs e)
    {
        var gift = Album[index];
        if (gift.IsEmpty)
        {
            ResetDrag();
            return;
        }

        DragSlot = index;
        var newFile = Path.Combine(Path.GetTempPath(), PathUtil.CleanFileName(gift.FileName));
        try
        {
            await File.WriteAllBytesAsync(newFile, gift.Write().ToArray());
            var file = await StorageProvider.TryGetFileFromPathAsync(new Uri(newFile));
            if (file is null)
                return;
            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.CreateFile(file));
            await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Move | DragDropEffects.Copy);
        }
        catch (Exception x)
        {
            await AppDialogs.Error(this, "Drag && Drop Error", x);
        }
        finally
        {
            DeleteTempAsync(newFile);
            ResetDrag();
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

    private async Task SlotDrop(int index, DragEventArgs e)
    {
        e.Handled = true;
        int source = DragSlot;

        if (source >= 0) // internal swap
        {
            var moved = SwapSlots(index, source);
            ResetDrag();
            if (moved == -1)
                return;
            SetBackground(moved, SlotTouchType.Get);
            SetGiftBoxes();
            return;
        }

        var path = GetDroppedFile(e);
        if (path is null)
            return;
        var gift = await ReadGift(path);
        if (gift is null)
            return;

        // Hijack to the latest unfilled slot if the index creates interstitial empty slots.
        int lastUnfilled = GetLastUnfilledByType(gift, Album);
        if (lastUnfilled > -1 && lastUnfilled < index && Album[lastUnfilled].Type == Album[index].Type)
            index = lastUnfilled;
        if (gift is PCD { IsLockCapsule: true })
            index = 11;

        var dest = Album[index];
        if (gift is PCD { CanConvertToPGT: true } pcd && dest is PGT)
        {
            gift = pcd.Gift;
        }
        else if (gift.Type != dest.Type)
        {
            await AppDialogs.Alert(this, MsgMysteryGiftSlotFail, $"{gift.Type} != {dest.Type}");
            return;
        }

        Album[index] = gift.Clone();
        SetBackground(index, SlotTouchType.Set);
        SetCardID(Album[index].CardID);
        ViewGiftData(Album[index]);
        SetGiftBoxes();
    }

    private async Task WindowDrop(DragEventArgs e)
    {
        if (DragSlot >= 0)
            return; // handled by the slot
        var path = GetDroppedFile(e);
        if (path is null)
            return;
        var gift = await ReadGift(path);
        if (gift is not null)
            ViewGiftData(gift);
    }

    private static string? GetDroppedFile(DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is not { Length: not 0 })
            return null;
        return files[0].TryGetLocalPath();
    }

    private async Task<DataMysteryGift?> ReadGift(string path)
    {
        var fi = new FileInfo(path);
        if (!fi.Exists || !MysteryGift.IsMysteryGift(fi.Length))
        {
            await AppDialogs.Alert(this, MsgFileUnsupported, path);
            return null;
        }

        var data = await File.ReadAllBytesAsync(path);
        var gift = MysteryGift.GetMysteryGift(data, fi.Extension);
        if (gift is not null)
            return gift;
        await AppDialogs.Alert(this, MsgFileUnsupported, path);
        return null;
    }

    #endregion

    #region File I/O

    private async Task ClickImport()
    {
        var path = await FileDialogs.OpenSingleFile(this, FileDialogs.GetMysterGiftFilter(SAV.Context));
        if (path is null)
            return;

        var data = await File.ReadAllBytesAsync(path);
        var gift = MysteryGift.GetMysteryGift(data, Path.GetExtension(path));
        if (gift is null)
        {
            await AppDialogs.Error(this, MsgMysteryGiftInvalid, path);
            return;
        }
        ViewGiftData(gift);
    }

    private async Task ClickExport()
    {
        if (mg is null)
            return;
        await FileDialogs.ExportMGDialog(this, mg);
    }

    private async Task ClickQR()
    {
        if (mg is null)
            return;
        if (mg.IsEmpty)
        {
            await AppDialogs.Alert(this, MsgMysteryGiftSlotNone);
            return;
        }
        if (SAV.Generation == 6 && mg.ItemID == 726 && mg.IsItem)
        {
            await AppDialogs.Alert(this, MsgMysteryGiftQREonTicket, MsgMysteryGiftQREonTicketAdvice);
            return;
        }

        using var qr = QREncode.GenerateQRCode(mg);
        var sprite = mg.Sprite();
        var lines = new List<string> { $"({mg.Type})" };
        lines.AddRange(mg.GetDescription());
        var form = new QRWindow(qr, sprite, [.. lines.Take(3)], "PKHeX Wonder Card @ ProjectPokemon.org");
        sprite.Release();
        await form.ShowDialog(this);
    }

    #endregion

    protected override void OnSave()
    {
        SaveReceivedFlags();
        SaveReceivedCards();

        Origin.CopyChangesFrom(SAV);
        Close();
    }

    private void SaveReceivedCards()
    {
        if (Cards is MysteryBlock4 s4)
        {
            s4.IsDeliveryManActive = Album.Any(g => !g.IsEmpty);
            MysteryBlock4.UpdateSlotPGT(Album, SAV is SAV4HGSS);
            if (SAV is SAV4HGSS hgss)
                hgss.LockCapsuleSlot = (PCD)Album[^1];
        }
        int count = Cards.GiftCountMax;
        for (int i = 0; i < count; i++)
            Cards.SetMysteryGift(i, Album[i]);
        if (Cards is MysteryBlock5 s5)
            s5.EndAccess(); // the at-rest data needs to be encrypted with the seed
    }

    private void SaveReceivedFlags()
    {
        if (Flags is null)
            return;

        Flags.ClearReceivedFlags();
        foreach (var item in Received)
        {
            if (int.TryParse(item, out var index))
                Flags.SetMysteryGiftReceivedFlag(index, true);
        }
    }
}
