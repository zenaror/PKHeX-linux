using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Controls.Hover;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Views.SaveEditors;

/// <summary>
/// Viewer for the registered teams of a Stadium save (port of the WinForms <c>SAV_GroupViewer</c>).
/// </summary>
public sealed class GroupViewerWindow : Window
{
    private readonly SaveFile SAV;
    private readonly IPKMView View;
    private readonly IReadOnlyList<SlotGroup> Groups;
    private readonly SummaryPreviewer Preview = new();

    private readonly PokeGrid Box = new();
    private readonly ComboBox CB_BoxSelect = UiFactory.StringCombo("CB_BoxSelect", 220);
    private readonly Button B_BoxLeft = UiFactory.Button("B_BoxLeft", "<");
    private readonly Button B_BoxRight = UiFactory.Button("B_BoxRight", ">");

    private int groupSelected = -1;
    private int slotSelected = -1;

    public int CurrentGroup { get; private set; } = -1;

    public GroupViewerWindow(SaveFile sav, IPKMView view, IReadOnlyList<SlotGroup> groups)
    {
        SAV = sav;
        View = view;
        Groups = groups;

        Name = "SAV_GroupViewer";
        Title = "Group Viewer";
        Icon = AppIcon.Get();
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Regenerate(groups[0].Slots.Length);

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 4,
            Children = { B_BoxLeft, CB_BoxSelect, B_BoxRight },
        };
        Content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new global::Avalonia.Thickness(8),
            Spacing = 6,
            Children = { header, Box },
        };

        foreach (var group in groups)
            CB_BoxSelect.Items.Add(group.GroupName);

        CB_BoxSelect.SelectionChanged += (_, _) => LoadGroup(CB_BoxSelect.SelectedIndex);
        B_BoxLeft.AttachClickHandled(mods => CB_BoxSelect.SelectedIndex = MoveLeft(mods.HasFlag(KeyModifiers.Control)));
        B_BoxRight.AttachClickHandled(mods => CB_BoxSelect.SelectedIndex = MoveRight(mods.HasFlag(KeyModifiers.Control)));

        // Mouse wheel cycles through the teams, as in WinForms.
        AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            CB_BoxSelect.SelectedIndex = e.Delta.Y > 0 ? MoveLeft() : MoveRight();
            e.Handled = true;
        }, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);

        for (int i = 0; i < Box.Entries.Count; i++)
        {
            var index = i;
            var pb = Box.Entries[i];
            pb.AttachClickHandled(mods =>
            {
                if (mods == KeyModifiers.Control)
                    ClickView(index);
            });
            pb.PointerEntered += (_, _) => HoverSlot(index);
            pb.PointerExited += (_, _) => Preview.Clear();
        }
        Closed += (_, _) => Preview.Clear();

        CB_BoxSelect.SelectedIndex = GetFirstTeamWithContent(groups);
        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
    }

    private void HoverSlot(int index)
    {
        if (CurrentGroup < 0)
            return;
        var group = Groups[CurrentGroup];
        if ((uint)index >= group.Slots.Length)
            return;
        Preview.Show(Box.Entries[index], group.Slots[index], group.Type);
    }

    private static int GetFirstTeamWithContent(IReadOnlyList<SlotGroup> groups)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i].Slots.Any(z => z.Species != 0))
                return i;
        }
        return 0;
    }

    private void Regenerate(int count)
    {
        var height = count / 5;
        var width = count / height;
        Box.InitializeGrid(width, height, SpriteUtil.Spriter);
    }

    private void LoadGroup(int index)
    {
        if (index < 0 || index == CurrentGroup)
            return;

        var (_, slots, type) = Groups[index];
        for (int i = 0; i < slots.Length && i < Box.Entries.Count; i++)
            Box.Entries[i].Sprite = slots[i].Sprite(SAV, visibility: SlotVisibilityType.CheckLegalityIndicate, storage: type).ToAvaloniaBitmapAndDispose();

        if (slotSelected != -1 && (uint)slotSelected < Box.Entries.Count)
            Box.Entries[slotSelected].BackgroundBitmap = groupSelected != index ? null : SlotUtil.GetTouchTypeBackground(SlotTouchType.Get);

        CurrentGroup = index;
    }

    public int MoveLeft(bool max = false)
    {
        int newBox = max ? 0 : (CurrentGroup + Groups.Count - 1) % Groups.Count;
        LoadGroup(newBox);
        return newBox;
    }

    public int MoveRight(bool max = false)
    {
        int newBox = max ? Groups.Count - 1 : (CurrentGroup + 1) % Groups.Count;
        LoadGroup(newBox);
        return newBox;
    }

    private void ClickView(int index)
    {
        if (CurrentGroup < 0)
            return;
        var group = Groups[CurrentGroup];
        if ((uint)index >= group.Slots.Length)
            return;
        View.PopulateFields(group.Slots[index], false);

        if (slotSelected != index && (uint)slotSelected < Box.Entries.Count)
            Box.Entries[slotSelected].BackgroundBitmap = null;

        groupSelected = CurrentGroup;
        slotSelected = index;
        Box.Entries[index].BackgroundBitmap = SlotUtil.GetTouchTypeBackground(SlotTouchType.Get);
    }
}
