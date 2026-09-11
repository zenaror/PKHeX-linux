using System;
using System.Collections.Generic;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Core;
using PKHeX.Core.Searching;
using PKHeX.Drawing.Misc;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Box viewer: box selector with navigation buttons and a grid of slots.
/// </summary>
/// <remarks>Port of the WinForms <c>BoxEditor</c>.</remarks>
public sealed class BoxView : UserControl, ISlotViewer<SlotView>
{
    private bool _gridInitialized;
    public IList<SlotView> SlotPictureBoxes { get; private set; } = [];
    public SaveFile SAV => Host?.SAV ?? throw new ArgumentNullException(nameof(SAV));
    public int BoxSlotCount { get; private set; }
    public ISaveHost? Host { get; set; }
    public bool FlagIllegal { get; set; }
    public bool CanSetCurrentBox { get; set; }

    public BoxEdit Editor { get; set; } = null!;

    public readonly ComboBox CB_BoxSelect = new() { Name = "CB_BoxSelect", MinWidth = 160, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly AvaloniaList<string> BoxNames = [];
    public readonly Button B_BoxLeft = new() { Name = "B_BoxLeft", Content = "<", Width = 32 };
    public readonly Button B_BoxRight = new() { Name = "B_BoxRight", Content = ">", Width = 32 };
    /// <summary>Opens the box search popout. WinForms floats this button over the grid's right edge.</summary>
    public readonly Button B_SearchBox = new() { Name = "B_SearchBox", Content = "Search", Padding = new global::Avalonia.Thickness(8, 2), MinHeight = 0 };
    public readonly PokeGrid BoxPokeGrid = new() { Name = "BoxPokeGrid" };

    private bool _suppressBoxChange;

    public BoxView()
    {
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 4,
            Margin = new global::Avalonia.Thickness(0, 4, 0, 4),
        };
        header.Children.Add(B_BoxLeft);
        header.Children.Add(CB_BoxSelect);
        header.Children.Add(B_BoxRight);
        header.Children.Add(B_SearchBox);

        var layout = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
        layout.Children.Add(header);
        layout.Children.Add(BoxPokeGrid);
        Content = layout;

        CB_BoxSelect.ItemsSource = BoxNames;
        B_BoxLeft.Click += (_, _) => ClickBoxLeft();
        B_BoxRight.Click += (_, _) => ClickBoxRight();
        CB_BoxSelect.SelectionChanged += (_, _) => GetBox();
        BoxPokeGrid.PointerWheelChanged += (_, e) =>
        {
            if (!SAV.HasBox || Host is null)
                return;
            CurrentBox = e.Delta.Y > 0 ? Editor.MoveLeft() : Editor.MoveRight();
            e.Handled = true;
        };
    }

    internal bool InitializeGrid()
    {
        var count = SAV.BoxSlotCount;
        var width = count / 5;
        var height = count / width;
        if (!BoxPokeGrid.InitializeGrid(width, height, SpriteUtil.Spriter))
            return false;
        _gridInitialized = true;
        InitializeSlots();
        return true;
    }

    private void InitializeSlots()
    {
        SlotPictureBoxes = BoxPokeGrid.Entries;
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
        if (previous is not SlotInfoBox b || b.Box != CurrentBox)
            return;

        var pb = SlotPictureBoxes[previous.Slot];
        pb.BackgroundBitmap = null;
    }

    public void NotifySlotChanged(ISlotInfo slot, SlotTouchType type, PKM pk)
    {
        int index = GetViewIndex(slot);
        if (index < 0)
            return;

        var pb = SlotPictureBoxes[index];
        var flags = GetFlags(pk);
        SlotUtil.UpdateSlot(pb, slot, pk, SAV, flags, type);
    }

    public void ApplyNewFilter(Func<PKM, bool>? filter, bool reload = true)
    {
        if (filter == _searchFilter)
            return;
        _searchFilter = filter;
        _lastSearchResult = null;
        if (reload && SAV.HasBox)
            ResetSlots();
    }

    private (int Box, int Slot)? _lastSearchResult;

    /// <summary>
    /// Moves to the next (or previous) slot matching the filter, wrapping around.
    /// </summary>
    /// <returns>False if no slot matches.</returns>
    public bool SeekNext(Func<PKM, bool> searchFilter, bool reverse = false)
    {
        // Search from next box, wrapping around
        var (box, slot) = _lastSearchResult ?? (CurrentBox, -1);
        if (!SearchUtil.TrySeekNext(SAV, searchFilter, out var result, box, slot, reverse))
            return false;

        CurrentBox = result.Box;
        SlotPictureBoxes[result.Slot].Focus();
        _lastSearchResult = result;
        return true;
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
        if (slot is not SlotInfoBox b || b.Box != CurrentBox)
            return -1;
        return slot.Slot;
    }

    public ISlotInfo GetSlotData(SlotView view)
    {
        int slot = GetSlot(view);
        return new SlotInfoBox(ViewIndex, slot, SAV);
    }

    private int GetSlot(SlotView sender) => SlotPictureBoxes.IndexOf(sender);
    public int ViewIndex => CurrentBox;

    public bool ControlsVisible
    {
        get => CB_BoxSelect.IsVisible;
        set => CB_BoxSelect.IsVisible = B_BoxLeft.IsVisible = B_BoxRight.IsVisible = value;
    }

    public bool ControlsEnabled
    {
        get => CB_BoxSelect.IsEnabled;
        set => CB_BoxSelect.IsEnabled = B_BoxLeft.IsEnabled = B_BoxRight.IsEnabled = value;
    }

    public int CurrentBox
    {
        get => CB_BoxSelect.SelectedIndex;
        set
        {
            _suppressBoxChange = true;
            CB_BoxSelect.SelectedIndex = value;
            _suppressBoxChange = false;
            if (value < 0)
                return;
            Editor.LoadBox(value);
            if (SAV.CurrentBox != value && CanSetCurrentBox)
                SAV.CurrentBox = value;
            ResetSlots();
        }
    }

    public string CurrentBoxName => CB_BoxSelect.SelectedItem as string ?? string.Empty;

    /// <summary>
    /// Updates the list of Box Names to select from, and selects the box index specified. If no box is specified, the previous index is used.
    /// </summary>
    /// <param name="box">Box to display after reload.</param>
    public void ResetBoxNames(int box = -1)
    {
        if (!SAV.HasBox)
            return;

        var currentIndex = CurrentBox;
        if (box < 0)
            box = currentIndex;

        var update = BoxUtil.GetBoxNames(SAV);
        var current = BoxNames;
        bool rebuilt = false;
        if (!GetIsSame(update, current))
        {
            _suppressBoxChange = true;
            // try to keep list elements if same length
            if (update.Length == current.Count)
            {
                for (int i = 0; i < update.Length; i++)
                    current[i] = update[i];
            }
            else // rebuild completely
            {
                CB_BoxSelect.SelectedIndex = -1;
                current.Clear();
                current.AddRange(update);
                rebuilt = true;
            }
            _suppressBoxChange = false;
        }

        box = Math.Clamp(box, 0, current.Count - 1);
        if (rebuilt || box != CurrentBox)
            CurrentBox = box;
        else
            CB_BoxSelect.SelectedItem = current[box]; // refresh displayed text
    }

    private static bool GetIsSame(ReadOnlySpan<string> a, IList<string> b)
    {
        if (a.Length != b.Count)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (b[i] != a[i])
                return false;
        }
        return true;
    }

    public void ResetSlots()
    {
        if (!_gridInitialized)
            return;
        Editor.Reload();
        int box = CurrentBox;
        if (box < 0)
            return;
        BoxPokeGrid.SetBackground(SAV.WallpaperImage(box));

        int index = box * SAV.BoxSlotCount;
        for (int i = 0; i < BoxSlotCount; i++)
        {
            var pb = SlotPictureBoxes[i];
            if (i >= SAV.BoxSlotCount || index + i >= SAV.SlotCount)
            {
                pb.IsVisible = false;
                continue;
            }
            pb.IsVisible = true;
            var pk = Editor[i];
            var flags = GetFlags(pk);
            SlotUtil.UpdateSlot(pb, (SlotInfoBox)GetSlotData(pb), pk, SAV, flags);
        }

        if (Host?.Publisher.Previous is SlotInfoBox b && b.Box == CurrentBox)
            SlotPictureBoxes[b.Slot].BackgroundBitmap = SlotUtil.GetTouchTypeBackground(Host.Publisher.PreviousType);
    }

    public void Reset()
    {
        ResetBoxNames();
        ResetSlots();
    }

    private void GetBox()
    {
        if (_suppressBoxChange || Host is null || !SAV.HasBox)
            return;
        var box = CB_BoxSelect.SelectedIndex;
        if (box < 0)
            return;
        CurrentBox = box;
    }

    private void ClickBoxLeft() => CurrentBox = Editor.MoveLeft(Host?.IsControlHeld == true);
    private void ClickBoxRight() => CurrentBox = Editor.MoveRight(Host?.IsControlHeld == true);

    public bool InitializeFromSAV(SaveFile sav)
    {
        Editor = new BoxEdit(sav);
        bool result = InitializeGrid();

        int box = sav.CurrentBox;
        if ((uint)box >= sav.BoxCount)
            box = 0;

        // Display the Box Names
        ResetBoxNames(box);
        Editor.LoadBox(box);
        return result;
    }

    private Func<PKM, bool>? _searchFilter;
}
