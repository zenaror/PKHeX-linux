using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using PKHeX.Drawing.Misc;

namespace PKHeX.Avalonia.Views.SaveEditors;

/// <summary>
/// Box names / wallpapers editor (port of the WinForms <c>SAV_BoxLayout</c>).
/// </summary>
public sealed class BoxLayoutWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SaveFile SAV;

    private readonly ListBox LB_BoxSelect = new() { Name = "LB_BoxSelect", Width = 160, Height = 300 };
    private readonly AvaloniaList<string> BoxNames = [];
    private readonly TextBlock L_BoxName = UiFactory.Label("L_BoxName", "Box Name:");
    private readonly TextBox TB_BoxName = UiFactory.Text("TB_BoxName", 15, 160);
    private readonly TextBlock L_BG = UiFactory.Label("L_BG", "Background:");
    private readonly ComboBox CB_BG = UiFactory.StringCombo("CB_BG", 180);
    private readonly Border PAN_BG = new() { Width = 400, Height = 288, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Button B_Up = UiFactory.Button("B_Up", "↑");
    private readonly Button B_Down = UiFactory.Button("B_Down", "↓");
    private readonly StackPanel FLP_Unlocked;
    private readonly TextBlock L_Unlocked = UiFactory.Label("L_Unlocked", "Unlocked:");
    private readonly ComboBox CB_Unlocked = UiFactory.StringCombo("CB_Unlocked", 70);
    private readonly StackPanel FLP_Flags;
    private readonly TextBlock L_Flag = UiFactory.Label("L_Flag", "Flags:");
    private global::Avalonia.Media.Imaging.Bitmap? Wallpaper;

    private NumericTextBox[] flagArr = [];
    private bool editing;
    private bool renamingBox;

    public BoxLayoutWindow(SaveFile sav, int box) : base("SAV_BoxLayout", "Box Layout Editor")
    {
        SAV = (Origin = sav).Clone();
        editing = true;

        LB_BoxSelect.ItemsSource = BoxNames;
        FLP_Unlocked = UiFactory.Row(L_Unlocked, CB_Unlocked);
        FLP_Flags = UiFactory.Row(L_Flag);
        var moves = UiFactory.Column(B_Up, B_Down);
        var left = UiFactory.Row(LB_BoxSelect, moves);
        var details = UiFactory.FormGrid(2);
        UiFactory.AddFormRow(details, 0, L_BoxName, TB_BoxName);
        UiFactory.AddFormRow(details, 1, L_BG, CB_BG);
        var right = UiFactory.Column(details, PAN_BG, FLP_Unlocked, FLP_Flags);
        var body = UiFactory.Row(left, right);
        body.Spacing = 10;
        left.VerticalAlignment = right.VerticalAlignment = VerticalAlignment.Top;

        bool any = LoadWallpapers(SAV);
        any |= LoadBoxNames(SAV);
        if (!any)
            _ = AppDialogs.Error(this, "Box layout is not supported for this game.", "Please close the window.");
        LoadFlags();
        LoadUnlockedCount();

        LB_BoxSelect.SelectionChanged += (_, _) => ChangeBox();
        TB_BoxName.OnTextChanged(_ => ChangeBoxDetails());
        CB_BG.SelectionChanged += (_, _) => ChangeBoxBackground();
        B_Up.Click += (_, _) => MoveBox(-1);
        B_Down.Click += (_, _) => MoveBox(+1);

        LB_BoxSelect.SelectedIndex = box;
        TB_BoxName.MaxLength = SAV.Generation switch
        {
            2 when SAV is SAV2 { Japanese: false, Korean: false } => 8 * 2,
            3 when SAV is SAV3RSBox => 8 + SAV3RSBox.BoxNamePrefix,
            6 or 7 => 14,
            >= 8 => 16,
            _ => 8,
        };
        editing = false;
        ChangeBox();
        SetBody(body);
        Closed += (_, _) => Wallpaper?.Dispose();
    }

    private bool LoadWallpapers(SaveFile sav)
    {
        if (sav is not IBoxDetailWallpaper)
        {
            L_BG.IsVisible = CB_BG.IsVisible = PAN_BG.IsVisible = false;
            return false;
        }

        CB_BG.Items.Clear();

        static void AddRange(ComboBox cb, ReadOnlySpan<string> names)
        {
            foreach (var name in names)
                cb.Items.Add(name);
        }

        static void AddPlaceholder(ComboBox cb, int count)
        {
            for (int i = 1; i <= count; i++)
                cb.Items.Add($"Wallpaper {i}");
        }

        var names = GameInfo.Strings.wallpapernames;
        switch (SAV.Generation)
        {
            case 3 when SAV is SAV3 or SAV3RSBox:
                AddRange(CB_BG, names.AsSpan(0, 16));
                return true;
            case 4 or 5 or 6:
                AddRange(CB_BG, names.AsSpan(0, 24));
                return true;
            case 7:
                AddRange(CB_BG, names.AsSpan(0, 16));
                return true;
            case 8 when SAV is SAV8BS:
                AddRange(CB_BG, names.AsSpan(0, 32));
                return true;
            case 8:
                AddPlaceholder(CB_BG, 19);
                return true;
            case 9:
                AddPlaceholder(CB_BG, 20);
                return true;
            default:
                return false;
        }
    }

    private bool LoadBoxNames(SaveFile sav)
    {
        if (sav is not IBoxDetailNameRead r)
        {
            L_BoxName.IsVisible = TB_BoxName.IsVisible = false;
            return false;
        }
        if (sav is not IBoxDetailName)
            TB_BoxName.IsEnabled = false;
        BoxNames.Clear();
        for (int i = 0; i < SAV.BoxCount; i++)
            BoxNames.Add(r.GetBoxName(i));
        return true;
    }

    private void LoadUnlockedCount()
    {
        if (SAV.BoxesUnlocked <= 0)
        {
            FLP_Unlocked.IsVisible = L_Unlocked.IsVisible = CB_Unlocked.IsVisible = false;
            return;
        }
        CB_Unlocked.Items.Clear();
        int max = SAV.BoxCount;
        for (int i = 0; i <= max; i++)
            CB_Unlocked.Items.Add(i.ToString());
        CB_Unlocked.SelectedIndex = Math.Min(max, SAV.BoxesUnlocked);
    }

    private void LoadFlags()
    {
        byte[] flags = SAV.BoxFlags;
        if (flags.Length == 0)
        {
            FLP_Flags.IsVisible = false;
            return;
        }

        flagArr = new NumericTextBox[flags.Length];
        for (int i = 0; i < flags.Length; i++)
        {
            flagArr[i] = UiFactory.Numeric($"NUD_Flag{i}", 2, 44, hex: true);
            flagArr[i].Text = flags[i].ToString("X2");
            FLP_Flags.Children.Add(flagArr[i]);
        }
    }

    private void ChangeBox()
    {
        if (renamingBox)
            return;
        editing = true;

        var box = LB_BoxSelect.SelectedIndex;
        if (box < 0)
        {
            editing = false;
            return;
        }
        if (SAV is IBoxDetailWallpaper wp)
        {
            var choice = wp.GetBoxWallpaper(box);
            var maxWallpaper = CB_BG.Items.Count - 1;
            CB_BG.SelectedIndex = Math.Clamp(choice, 0, maxWallpaper);
        }

        if (SAV is IBoxDetailNameRead r)
            TB_BoxName.Text = r.GetBoxName(box);

        editing = false;
        ChangeBoxBackground();
    }

    private void ChangeBoxDetails()
    {
        if (editing)
            return;

        renamingBox = true;
        var index = LB_BoxSelect.SelectedIndex;
        if (SAV is IBoxDetailName name && index >= 0)
        {
            var text = TB_BoxName.Text ?? string.Empty;
            name.SetBoxName(index, text);
            BoxNames[index] = text;
            if (LB_BoxSelect.SelectedIndex != index) // replacing the item clears the selection
                LB_BoxSelect.SelectedIndex = index;
        }
        renamingBox = false;
    }

    protected override void OnSave()
    {
        if (flagArr.Length != 0)
            SAV.BoxFlags = Array.ConvertAll(flagArr, i => (byte)i.IntValue);
        if (CB_Unlocked.IsVisible)
            SAV.BoxesUnlocked = CB_Unlocked.SelectedIndex;

        Origin.CopyChangesFrom(SAV);
        Close();
    }

    private void ChangeBoxBackground()
    {
        var box = LB_BoxSelect.SelectedIndex;
        if (box < 0)
            return;
        if (!editing)
        {
            if (SAV is IBoxDetailWallpaper wp && CB_BG.SelectedIndex >= 0)
                wp.SetBoxWallpaper(box, CB_BG.SelectedIndex);
        }

        var old = Wallpaper;
        Wallpaper = SAV.WallpaperImage(box).ToAvaloniaBitmapAndDispose();
        PAN_BG.Background = new ImageBrush(Wallpaper)
        {
            TileMode = TileMode.Tile,
            DestinationRect = new RelativeRect(0, 0, Wallpaper.PixelSize.Width, Wallpaper.PixelSize.Height, RelativeUnit.Absolute),
        };
        old?.Dispose();
    }

    private bool MoveItem(int direction)
    {
        var index = LB_BoxSelect.SelectedIndex;
        if (index < 0)
            return false; // No selected item - nothing to do

        int newIndex = index + direction;
        if ((uint)newIndex >= BoxNames.Count)
            return false; // Index out of range - nothing to do

        var selected = BoxNames[index];
        BoxNames.RemoveAt(index);
        BoxNames.Insert(newIndex, selected);
        LB_BoxSelect.SelectedIndex = newIndex;
        editing = renamingBox = false;
        return true;
    }

    private async void MoveBox(int dir)
    {
        int index = LB_BoxSelect.SelectedIndex;
        editing = renamingBox = true;
        if (!MoveItem(dir))
        {
            // nothing to move
        }
        else if (!SAV.SwapBox(index, index + dir)) // valid but locked
        {
            MoveItem(-dir); // undo
            editing = renamingBox = false;
            await AppDialogs.Alert(this, "Locked/Team slots prevent movement of box(es).");
            return;
        }
        else
        {
            editing = renamingBox = false;
            ChangeBox();
        }

        editing = renamingBox = false;
    }
}
