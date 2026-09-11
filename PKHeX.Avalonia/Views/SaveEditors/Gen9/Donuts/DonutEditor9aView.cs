using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Core;
using PKHeX.Drawing.Misc;
using PKHeX.Drawing.PokeSprite;
using Bitmap = global::Avalonia.Media.Imaging.Bitmap;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen9.Donuts;

/// <summary>
/// Editor for a single <see cref="Donut9a"/> entry (port of the WinForms <c>DonutEditor9a</c> user control).
/// </summary>
public sealed class DonutEditor9aView : StackPanel
{
    private Donut9a Donut;

    /// <summary>Raised whenever the user changes one of the fields.</summary>
    public event EventHandler? ValueChanged;

    private static readonly DateTime Epoch = Donut9a.Epoch;

    private readonly ComboBox[] Berry =
    [
        UiFactory.Combo("CB_Berry0", 164), UiFactory.Combo("CB_Berry1", 164), UiFactory.Combo("CB_Berry2", 164),
        UiFactory.Combo("CB_Berry3", 164), UiFactory.Combo("CB_Berry4", 164), UiFactory.Combo("CB_Berry5", 164),
        UiFactory.Combo("CB_Berry6", 164), UiFactory.Combo("CB_Berry7", 164), UiFactory.Combo("CB_Berry8", 164),
    ];

    private readonly Image[] BerryIcons =
    [
        Sprite("PB_Berry0", 24, 24), Sprite("PB_Berry1", 24, 24), Sprite("PB_Berry2", 24, 24),
        Sprite("PB_Berry3", 24, 24), Sprite("PB_Berry4", 24, 24), Sprite("PB_Berry5", 24, 24),
        Sprite("PB_Berry6", 24, 24), Sprite("PB_Berry7", 24, 24), Sprite("PB_Berry8", 24, 24),
    ];

    private readonly ComboBox[] Flavor =
    [
        UiFactory.Combo("CB_Flavor0", 240), UiFactory.Combo("CB_Flavor1", 240), UiFactory.Combo("CB_Flavor2", 240),
    ];

    private readonly Image[] FlavorIcons = [Sprite("PB_Flavor0", 54, 27), Sprite("PB_Flavor1", 54, 27), Sprite("PB_Flavor2", 54, 27)];
    private readonly Image[] Stars = [Sprite("PB_Star1", 22, 22), Sprite("PB_Star2", 22, 22), Sprite("PB_Star3", 22, 22), Sprite("PB_Star4", 22, 22), Sprite("PB_Star5", 22, 22)];
    private readonly Image PB_Donut = Sprite("PB_Donut", 74, 74);

    private readonly NumericUpDown NUD_Stars = UiFactory.NumericUpDown("NUD_Stars", 0, 6, 104);
    private readonly NumericUpDown NUD_Calories = UiFactory.NumericUpDown("NUD_Calories", 0, 65535, 120);
    private readonly NumericUpDown NUD_LevelBoost = UiFactory.NumericUpDown("NUD_LevelBoost", 0, 255, 104);
    private readonly ComboBox CB_Donut = UiFactory.Combo("CB_Donut", 240);
    private readonly DatePicker CAL_Date = new() { Name = "CAL_Date", MinWidth = 0, MaxYear = new DateTimeOffset(new DateTime(4095, 12, 31), TimeSpan.Zero) };
    private readonly TimePicker CAL_Time = new() { Name = "CAL_Time", ClockIdentifier = "24HourClock", UseSeconds = true, MinWidth = 0 };
    private readonly TextBox TB_Milliseconds = UiFactory.Text("TB_Milliseconds", 20, 163);

    private bool Loading;

    public DonutEditor9aView()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 8;

        Children.Add(BuildBerryGrid());
        Children.Add(BuildDetailGrid());
        Children.Add(BuildPreview());
    }

    private static Image Sprite(string name, double width, double height) => new()
    {
        Name = name,
        Width = width,
        Height = height,
        Stretch = Stretch.Uniform,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private Control BuildBerryGrid()
    {
        var grid = new Grid { ColumnSpacing = 4, RowSpacing = 2 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        foreach (var _ in Berry)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int i = 0; i < Berry.Length; i++)
        {
            var label = UiFactory.Label($"L_Berry{i}", i == 0 ? "Berry (Name)" : $"Berry {i}");
            UiFactory.SetRowCol(label, i, 0);
            UiFactory.SetRowCol(BerryIcons[i], i, 1);
            UiFactory.SetRowCol(Berry[i], i, 2);
            grid.Children.Add(label);
            grid.Children.Add(BerryIcons[i]);
            grid.Children.Add(Berry[i]);
        }
        return grid;
    }

    private Control BuildDetailGrid()
    {
        var grid = UiFactory.FormGrid(9);
        UiFactory.AddFormRow(grid, 0, UiFactory.Label("L_Stars", "Stars:"), Left(NUD_Stars));
        UiFactory.AddFormRow(grid, 1, UiFactory.Label("L_Calories", "Calories:"), Left(NUD_Calories));
        UiFactory.AddFormRow(grid, 2, UiFactory.Label("L_LevelBoost", "Level Boost:"), Left(NUD_LevelBoost));
        UiFactory.AddFormRow(grid, 3, UiFactory.Label("L_Donut", "Donut:"), CB_Donut);
        UiFactory.AddFormRow(grid, 4, UiFactory.Label("L_Flavor0", "Flavor 1:"), Flavor[0]);
        UiFactory.AddFormRow(grid, 5, UiFactory.Label("L_Flavor1", "Flavor 2:"), Flavor[1]);
        UiFactory.AddFormRow(grid, 6, UiFactory.Label("L_Flavor2", "Flavor 3:"), Flavor[2]);
        UiFactory.AddFormRow(grid, 7, null, UiFactory.Row(CAL_Date, CAL_Time));
        UiFactory.AddFormRow(grid, 8, UiFactory.Label("L_Milliseconds", "Milliseconds:"), Left(TB_Milliseconds));
        return grid;
    }

    private static Control Left(Control c)
    {
        c.HorizontalAlignment = HorizontalAlignment.Left;
        return c;
    }

    private Control BuildPreview()
    {
        var stars = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, VerticalAlignment = VerticalAlignment.Top };
        foreach (var star in Stars)
            stars.Children.Add(star);

        var flavors = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var icon in FlavorIcons)
            flavors.Children.Add(icon);

        return UiFactory.Column(UiFactory.Row(PB_Donut, stars), flavors);
    }

    /// <summary>
    /// Fills the combo boxes; must be called once before the first <see cref="LoadDonut"/>.
    /// </summary>
    public void InitializeLists(ReadOnlySpan<string> flavors, ReadOnlySpan<string> items, ReadOnlySpan<string> donutNames)
    {
        var berryList = GetBerryList(ItemStorage9ZA.Berry, items, items[0]);
        var flavorList = GetFlavorList(flavors, items[0]);
        var donutList = GetDonutList(donutNames);

        foreach (var cb in Berry)
            cb.SetItems(berryList);
        foreach (var cb in Flavor)
            cb.SetItems(flavorList);
        CB_Donut.SetItems(donutList);

        // The value-changed notification bubbles up to the owning form; the icon refreshes run regardless of the load state.
        foreach (var nud in new[] { NUD_Calories, NUD_LevelBoost, NUD_Stars })
            nud.ValueChanged += (_, _) => RaiseValueChanged();
        for (int i = 0; i < Berry.Length; i++)
        {
            var index = i;
            Berry[i].SelectionChanged += (_, _) => { RefreshBerryIcon(index); RaiseValueChanged(); };
        }
        for (int i = 0; i < Flavor.Length; i++)
        {
            var index = i;
            Flavor[i].SelectionChanged += (_, _) => { RefreshFlavorIcon(index); RaiseValueChanged(); };
        }

        CB_Donut.SelectionChanged += (_, _) => { RaiseValueChanged(); ChangeDonutKind(); };

        CAL_Date.SelectedDateChanged += (_, _) => { RaiseValueChanged(); ChangeDateTime(); };
        CAL_Time.SelectedTimeChanged += (_, _) => { RaiseValueChanged(); ChangeDateTime(); };
        TB_Milliseconds.TextChanged += (_, _) => { RaiseValueChanged(); ChangeMilliseconds(); };
        NUD_Stars.ValueChanged += (_, _) => LoadDonutStarCount((byte)(NUD_Stars.Value ?? 0));
    }

    private static List<ComboItem> GetDonutList(ReadOnlySpan<string> names)
    {
        List<ComboItem> result = new(names.Length);
        for (int i = 0; i < names.Length; i++)
            result.Add(new ComboItem(names[i], i));
        return result;
    }

    private static List<ComboItem> GetBerryList(ReadOnlySpan<ushort> berries, ReadOnlySpan<string> localized, string none)
    {
        List<ComboItem> result = [new(none, 0)];
        foreach (var berryItemID in berries)
            result.Add(new ComboItem(localized[berryItemID], berryItemID));
        return result;
    }

    /// <remarks>
    /// The WinForms control binds the internal flavor name as the combo's value; Avalonia's shared combo helper works on
    /// integers, so the list index into <see cref="DonutInfo.Flavors"/> (offset by the "none" entry) is used instead.
    /// </remarks>
    private static List<ComboItem> GetFlavorList(ReadOnlySpan<string> localized, string none)
    {
        var all = DonutInfo.Flavors;
        List<ComboItem> result = [new(none, 0)];
        for (int i = 0; i < all.Count; i++)
            result.Add(new ComboItem(localized[i], i + 1));
        return result;
    }

    /// <summary>
    /// Displays the requested donut; the editor writes back into the same buffer on <see cref="SaveDonut"/>.
    /// </summary>
    public void LoadDonut(Donut9a donut)
    {
        Loading = true;
        Donut = donut;

        NUD_Stars.SetValueClamped(donut.Stars);
        NUD_Calories.SetValueClamped(donut.Calories);
        NUD_LevelBoost.SetValueClamped(donut.LevelBoost);

        if (!CB_Donut.SetValue(donut.Donut))
            CB_Donut.SelectedIndex = 0;

        LoadDonutStarCount(donut.Stars); // acknowledge existing star count

        Berry[0].SetValue(donut.BerryName);
        Berry[1].SetValue(donut.Berry1);
        Berry[2].SetValue(donut.Berry2);
        Berry[3].SetValue(donut.Berry3);
        Berry[4].SetValue(donut.Berry4);
        Berry[5].SetValue(donut.Berry5);
        Berry[6].SetValue(donut.Berry6);
        Berry[7].SetValue(donut.Berry7);
        Berry[8].SetValue(donut.Berry8);
        for (int i = 0; i < Berry.Length; i++)
            RefreshBerryIcon(i);

        LoadDonutFlavorHash(Flavor[0], donut.Flavor0);
        LoadDonutFlavorHash(Flavor[1], donut.Flavor1);
        LoadDonutFlavorHash(Flavor[2], donut.Flavor2);
        for (int i = 0; i < Flavor.Length; i++)
            RefreshFlavorIcon(i);

        var dt = donut.HasDateTime() ? donut.DateTime1900.Timestamp : Epoch;
        try
        {
            SetDateTime(dt);
        }
        catch
        {
            SetDateTime(Epoch);
        }

        TB_Milliseconds.Text = donut.MillisecondsSince1970.ToString();
        RefreshDonutSprite();

        Loading = false;
    }

    /// <summary>
    /// Writes the fields back into the donut currently being edited.
    /// </summary>
    public void SaveDonut()
    {
        var donut = Donut;
        if (donut.Raw.IsEmpty)
            return;

        donut.Stars = (byte)(NUD_Stars.Value ?? 0);
        donut.Calories = (ushort)(NUD_Calories.Value ?? 0);
        donut.LevelBoost = (byte)(NUD_LevelBoost.Value ?? 0);

        donut.Donut = (ushort)CB_Donut.GetValue();

        donut.BerryName = (ushort)Berry[0].GetValue();
        donut.Berry1 = (ushort)Berry[1].GetValue();
        donut.Berry2 = (ushort)Berry[2].GetValue();
        donut.Berry3 = (ushort)Berry[3].GetValue();
        donut.Berry4 = (ushort)Berry[4].GetValue();
        donut.Berry5 = (ushort)Berry[5].GetValue();
        donut.Berry6 = (ushort)Berry[6].GetValue();
        donut.Berry7 = (ushort)Berry[7].GetValue();
        donut.Berry8 = (ushort)Berry[8].GetValue();

        donut.Flavor0 = GetDonutFlavorHash(Flavor[0]);
        donut.Flavor1 = GetDonutFlavorHash(Flavor[1]);
        donut.Flavor2 = GetDonutFlavorHash(Flavor[2]);

        var dt = GetDateTime();

        // if date is sufficiently equal to the Epoch (zero), set to zero. Can't set a date of 1900/00/00 via the controls...
        if (dt is { Year: 1900, Month: <= 1, Day: <= 1 } and { Day: 1, Hour: 0, Minute: 0, Second: 0 })
        {
            donut.ClearDateTime();
        }
        else
        {
            try
            {
                donut.DateTime1900.Timestamp = dt;
            }
            catch
            {
                donut.ClearDateTime();
            }
        }
        donut.MillisecondsSince1970 = ulong.TryParse(TB_Milliseconds.Text, out var unk) ? unk : 0;
    }

    /// <summary>Resets the edited donut to an empty entry.</summary>
    public void Reset()
    {
        if (Donut.Raw.IsEmpty)
            return;
        Donut.Clear();
        LoadDonut(Donut);
    }

    /// <summary>Localized name of the selected donut kind (used for the export file name).</summary>
    public string GetDonutName() => CB_Donut.GetText();

    private void SetDateTime(DateTime value)
    {
        CAL_Date.SelectedDate = UiFactory.ToOffset(value);
        CAL_Time.SelectedTime = value.TimeOfDay;
    }

    private DateTime GetDateTime()
    {
        var date = CAL_Date.SelectedDate?.Date ?? Epoch.Date;
        var time = CAL_Time.SelectedTime ?? TimeSpan.Zero;
        return date + new TimeSpan(time.Hours, time.Minutes, time.Seconds);
    }

    private static void LoadDonutFlavorHash(ComboBox cb, ulong flavorHash)
    {
        // Find the matching flavor by hash
        if (flavorHash != 0)
        {
            var all = DonutInfo.Flavors;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Hash != flavorHash)
                    continue;
                cb.SetValue(i + 1);
                return;
            }
        }
        cb.SelectedIndex = 0; // No flavor
    }

    private static ulong GetDonutFlavorHash(ComboBox cb)
    {
        var value = cb.GetValue();
        if (value <= 0)
            return 0; // No flavor
        return DonutInfo.Flavors[value - 1].Hash;
    }

    private void RefreshFlavorIcon(int index)
    {
        var value = Flavor[index].GetValue();
        FlavorIcons[index].Source = value <= 0 ? null : GetFlavorImage(DonutInfo.Flavors[value - 1].Name);
    }

    private void RefreshBerryIcon(int index)
    {
        var itemID = Berry[index].GetValue();
        BerryIcons[index].Source = itemID <= 0 ? null : GetItemSprite(itemID);
    }

    private void LoadDonutStarCount(byte count)
    {
        var star = StarImage;
        for (int i = 0; i < Stars.Length; i++)
            Stars[i].Source = i < count ? star : null;
    }

    private void ChangeDonutKind()
    {
        if (!Loading && !Donut.Raw.IsEmpty)
            Donut.Donut = (ushort)CB_Donut.GetValue();
        RefreshDonutSprite();
    }

    private void RefreshDonutSprite()
    {
        if (Donut.Raw.IsEmpty)
            return;
        PB_Donut.Source = GetDonutSprite((ushort)CB_Donut.GetValue(), Donut.Stars);
    }

    private void ChangeMilliseconds()
    {
        if (Loading)
            return;

        if (!ulong.TryParse(TB_Milliseconds.Text, out var ms))
            return;

        try
        {
            var ticks = Epoch.AddMilliseconds(ms);

            // If date is same, don't update the ticks.
            if (IsDateEquivalent(GetDateTime(), ticks))
                return;

            Loading = true;
            SetDateTime(ticks);
            Loading = false;
        }
        catch
        {
            ResetDateTime();
        }
    }

    private void ChangeDateTime()
    {
        if (Loading)
            return;

        if (!ulong.TryParse(TB_Milliseconds.Text, out var ms))
            return;

        try
        {
            var ticks = Epoch.AddMilliseconds(ms);

            // If date is same, don't update the ticks.
            var date = GetDateTime();
            if (IsDateEquivalent(date, ticks))
                return;

            var delta = (ulong)(date - Epoch).TotalMilliseconds;
            // retain existing ticks _xxx component, since datetime picker does not configure millis
            var exist = ms % 1000;
            delta -= delta % 1000;
            delta += exist;

            Loading = true;
            TB_Milliseconds.Text = delta.ToString();
            Loading = false;
        }
        catch
        {
            ResetDateTime();
        }
    }

    /// <summary>Fallback for an out-of-range timestamp: show "now" in both representations.</summary>
    private void ResetDateTime()
    {
        Loading = true;
        var now = DateTime.Now;
        SetDateTime(now);
        TB_Milliseconds.Text = ((ulong)(GetDateTime() - Epoch).TotalMilliseconds).ToString();
        Loading = false;
    }

    private static bool IsDateEquivalent(DateTime a, DateTime b) =>
        a.Year == b.Year && a.Month == b.Month && a.Day == b.Day &&
        a.Hour == b.Hour && a.Minute == b.Minute && a.Second == b.Second;

    /// <summary>
    /// Bubbles up to the parent control, if subscribed. Suppressed while the fields are being populated: the WinForms
    /// control relies on the parent form's own guard, but the parent has to flush the fields here (see the editor's
    /// value-changed handler), which must not run against half-loaded controls.
    /// </summary>
    private void RaiseValueChanged()
    {
        if (Loading)
            return;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    #region Sprite caches

    private static Bitmap? StarImageCache;
    private static Bitmap? StarImage => StarImageCache ??= DonutSpriteUtil.StarSprite?.ToAvaloniaBitmapAndDispose();

    private static readonly Dictionary<string, Bitmap?> FlavorImages = [];
    private static readonly Dictionary<int, Bitmap?> ItemSprites = [];
    private static readonly Dictionary<int, Bitmap?> DonutSprites = [];

    private static Bitmap? GetFlavorImage(string name)
    {
        if (FlavorImages.TryGetValue(name, out var cached))
            return cached;
        return FlavorImages[name] = DonutSpriteUtil.GetDonutFlavorImage(name)?.ToAvaloniaBitmapAndDispose();
    }

    private static Bitmap? GetItemSprite(int itemID)
    {
        if (ItemSprites.TryGetValue(itemID, out var cached))
            return cached;
        return ItemSprites[itemID] = SpriteUtil.GetItemSpriteA(itemID)?.ToAvaloniaBitmapAndDispose();
    }

    private static Bitmap? GetDonutSprite(ushort kind, byte stars)
    {
        var key = (kind << 8) | stars;
        if (DonutSprites.TryGetValue(key, out var cached))
            return cached;
        var probe = new Donut9a(new byte[Donut9a.Size]) { Donut = kind, Stars = stars };
        return DonutSprites[key] = probe.Sprite()?.ToAvaloniaBitmapAndDispose();
    }

    #endregion
}
