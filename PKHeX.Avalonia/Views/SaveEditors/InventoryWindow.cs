using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Views.SaveEditors;

/// <summary>
/// Bag / inventory editor (port of the WinForms <c>SAV_Inventory</c>).
/// </summary>
/// <remarks>
/// Each pouch is a tab with a virtualized list of editable rows instead of a <c>DataGridView</c>.
/// </remarks>
public sealed class InventoryWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly PlayerBag Bag;
    private readonly bool ItemColumnReadOnly;
    private readonly bool HasFreeSpace;
    private readonly bool HasFreeSpaceIndex;
    private readonly bool HasFavorite;
    private readonly bool HasNew;
    private readonly bool HasNewShop;
    private readonly bool HasHeld;
    private readonly bool HaX = MainWindow.HaX;

    private readonly TabControl tabControl1 = new() { Name = "tabControl1" };
    private readonly Dictionary<InventoryType, PouchView> ControlGrids = [];

    private readonly TextBlock L_Count = UiFactory.Label("L_Count", "Count:");
    private readonly NumericUpDown NUD_Count = UiFactory.NumericUpDown("NUD_Count", 1, 999, 120);
    private readonly Button B_GiveAll = UiFactory.Button("B_GiveAll", "Give All");
    private readonly Button B_Sort = UiFactory.Button("B_Sort", "Sort");
    private readonly MenuFlyout giveMenu = new();
    private readonly MenuFlyout sortMenu = new();
    private readonly MenuItem giveAll = new() { Name = "giveAll", Header = "All" };
    private readonly MenuItem giveNone = new() { Name = "giveNone", Header = "None" };
    private readonly MenuItem giveModify = new() { Name = "giveModify", Header = "Modify" };
    private readonly MenuItem mnuSortName = new() { Name = "mnuSortName", Header = "Name" };
    private readonly MenuItem mnuSortNameReverse = new() { Name = "mnuSortNameReverse", Header = "Name (Reverse)" };
    private readonly MenuItem mnuSortCount = new() { Name = "mnuSortCount", Header = "Count" };
    private readonly MenuItem mnuSortCountReverse = new() { Name = "mnuSortCountReverse", Header = "Count (Reverse)" };
    private readonly MenuItem mnuSortIndex = new() { Name = "mnuSortIndex", Header = "Index" };
    private readonly MenuItem mnuSortIndexReverse = new() { Name = "mnuSortIndexReverse", Header = "Index (Reverse)" };

    private readonly string[] itemlist;
    private readonly Dictionary<int, global::Avalonia.Media.Imaging.Bitmap?> SpriteCache = [];

    public InventoryWindow(SaveFile sav) : base("SAV_Inventory", "Inventory Editor")
    {
        Origin = sav;
        itemlist = [.. GameInfo.Strings.GetItemStrings(sav.Context, sav.Version)]; // copy
        for (int i = 0; i < itemlist.Length; i++)
        {
            if (string.IsNullOrEmpty(itemlist[i]))
                itemlist[i] = $"(Item #{i:000})";
        }

        Bag = sav.Inventory;
        ItemColumnReadOnly = sav is SAV9ZA or SAV9SV;
        var item0 = Bag.Pouches[0].Items[0];
        HasFreeSpace = item0 is IItemFreeSpace;
        HasFreeSpaceIndex = item0 is IItemFreeSpaceIndex;
        HasFavorite = item0 is IItemFavorite;
        HasNew = item0 is IItemNewFlag;
        HasNewShop = item0 is IItemNewShopFlag;
        HasHeld = item0 is IItemHeldFlag;

        SizeToContent = SizeToContent.Manual;
        CanResize = true;
        Width = 500 + (HasNewShop ? 44 : 0) + (HasHeld ? 44 : 0);
        Height = 480;
        MinWidth = 380;
        MinHeight = 300;

        // Menus
        giveMenu.Items.Add(giveAll);
        giveMenu.Items.Add(giveNone);
        giveMenu.Items.Add(giveModify);
        sortMenu.Items.Add(mnuSortName);
        sortMenu.Items.Add(mnuSortNameReverse);
        sortMenu.Items.Add(mnuSortCount);
        sortMenu.Items.Add(mnuSortCountReverse);
        sortMenu.Items.Add(mnuSortIndex);
        sortMenu.Items.Add(mnuSortIndexReverse);
        B_GiveAll.Flyout = giveMenu;
        B_Sort.Flyout = sortMenu;
        giveMenu.Placement = sortMenu.Placement = PlacementMode.Bottom;

        giveAll.Click += async (_, _) => await GiveAllItems();
        giveNone.Click += async (_, _) => await RemoveAllItems();
        giveModify.Click += async (_, _) => await ModifyAllItems();
        mnuSortName.Click += (_, _) => ModifyPouch(CurrentPouch, p => p.SortByName(itemlist));
        mnuSortNameReverse.Click += (_, _) => ModifyPouch(CurrentPouch, p => p.SortByName(itemlist, reverse: true));
        mnuSortCount.Click += (_, _) => ModifyPouch(CurrentPouch, p => p.SortByCount());
        mnuSortCountReverse.Click += (_, _) => ModifyPouch(CurrentPouch, p => p.SortByCount(reverse: true));
        mnuSortIndex.Click += (_, _) => ModifyPouch(CurrentPouch, p => p.SortByIndex());
        mnuSortIndexReverse.Click += (_, _) => ModifyPouch(CurrentPouch, p => p.SortByIndex(reverse: true));

        // Flyout items are not in the logical tree; translate them explicitly.
        var lang = MainWindow.CurrentLanguage;
        foreach (var mi in giveMenu.Items.OfType<MenuItem>().Concat(sortMenu.Items.OfType<MenuItem>()))
            mi.Header = Translator.TranslateText(Translator.GetKey("SAV_Inventory", mi.Name!), (string)mi.Header!, lang);

        ButtonBar.Children.Insert(0, L_Count);
        ButtonBar.Children.Insert(1, NUD_Count);
        ButtonBar.Children.Insert(2, B_GiveAll);
        ButtonBar.Children.Insert(3, B_Sort);
        ButtonBar.HorizontalAlignment = HorizontalAlignment.Stretch;
        B_Cancel.HorizontalAlignment = HorizontalAlignment.Right;

        CreateBagViews();
        SetBody(tabControl1);
        tabControl1.SelectionChanged += (_, _) => ChangeViewedPouch(CurrentPouch);

        Opened += async (_, _) =>
        {
            await LoadAllBags();
            ChangeViewedPouch(0);
        };
        Closed += (_, _) =>
        {
            foreach (var bmp in SpriteCache.Values)
                bmp?.Dispose();
            SpriteCache.Clear();
        };
    }

    private int CurrentPouch => Math.Max(0, tabControl1.SelectedIndex);
    private PouchView GetGrid(InventoryType type) => ControlGrids[type];
    private PouchView GetGrid(int pouch) => ControlGrids[Bag.Pouches[pouch].Type];

    protected override void OnSave()
    {
        SetBags();
        Bag.CopyTo(Origin);
        Close();
    }

    private void CreateBagViews()
    {
        foreach (var pouch in Bag.Pouches)
        {
            var icon = AppResources.GetImage(GetImageName(pouch.Type));
            var header = new Image { Source = icon, Width = 24, Height = 24, Stretch = Stretch.Uniform };
            var tab = new TabItem { Header = header, Padding = new Thickness(6, 4), MinHeight = 0 };
            ToolTip.SetTip(tab, pouch.Type.ToString());
            var view = GetPouchView(pouch);
            ControlGrids.Add(pouch.Type, view);
            tab.Content = view;
            tabControl1.Items.Add(tab);
        }
    }

    private PouchView GetPouchView(InventoryPouch pouch)
    {
        var itemarr = HaX ? itemlist : GetStringsForPouch(pouch.GetAllItems());
        var view = new PouchView(this, pouch, itemarr);
        var items = pouch.Items;
        for (int i = 0; i < items.Length; i++)
            view.Rows.Add(new InventoryRow(this, pouch, itemlist[0]));
        return view;
    }

    private async System.Threading.Tasks.Task LoadAllBags()
    {
        foreach (var pouch in Bag.Pouches)
        {
            var view = GetGrid(pouch.Type);

            // Sanity Screen
            var invalid = Array.FindAll(pouch.Items, item => item.Index != 0 && !pouch.CanContain((ushort)item.Index));
            var outOfBounds = Array.FindAll(invalid, item => item.Index >= itemlist.Length);
            var incorrectPouch = Array.FindAll(invalid, item => item.Index < itemlist.Length);

            if (outOfBounds.Length != 0)
                await AppDialogs.Error(this, MsgItemPouchUnknown, $"Item ID(s): {string.Join(", ", outOfBounds.Select(item => item.Index))}");
            if (!HaX && incorrectPouch.Length != 0)
                await AppDialogs.Alert(this, string.Format(MsgItemPouchRemoved, pouch.Type), string.Join(", ", incorrectPouch.Select(item => itemlist[item.Index])), MsgItemPouchWarning);

            pouch.Sanitize(itemlist.Length - 1, HaX);
            GetBag(view, pouch);
        }
    }

    private void SetBags()
    {
        foreach (var pouch in Bag.Pouches)
            SetBag(GetGrid(pouch.Type), pouch);
    }

    private void GetBag(PouchView view, InventoryPouch pouch)
    {
        var valid = pouch.GetAllItems();
        var rows = view.Rows;
        for (int i = 0; i < rows.Count; i++)
        {
            var item = pouch.Items[i];
            if (item.Index != 0 && !valid.Contains((ushort)item.Index) && !HaX)
                item = pouch.Items[i] = pouch.GetEmpty();

            var row = rows[i];
            row.Load(item, itemlist[item.Index]);
        }

        if (ItemColumnReadOnly) // Sort the rows alphabetically.
        {
            var sorted = rows.OrderBy(z => z.ItemName, StringComparer.CurrentCulture).ToArray();
            rows.Clear();
            foreach (var r in sorted)
                rows.Add(r);
        }
    }

    private void SetBag(PouchView view, InventoryPouch pouch)
    {
        int ctr = 0;
        foreach (var row in view.Rows)
        {
            var itemID = Array.IndexOf(itemlist, row.ItemName);

            if (itemID <= 0 && !HasNew) // Compression of Empty Slots
                continue;

            bool result = int.TryParse(row.CountText, out var count);
            if (!result)
                continue;
            if (!Bag.IsQuantitySane(pouch.Type, itemID, ref count, HasNew, HaX))
                continue; // ignore item

            // create clean item data when saving
            var item = pouch.GetEmpty(itemID, count);
            if (item is IItemFreeSpace f)
                f.IsFreeSpace = row.IsFreeSpace;
            if (item is IItemFreeSpaceIndex fi)
                fi.FreeSpaceIndex = uint.TryParse(row.FreeSpaceIndexText, out var fsi) ? fsi : 0;
            if (item is IItemFavorite v)
                v.IsFavorite = row.IsFavorite;
            if (item is IItemNewFlag n)
                n.IsNew = row.IsNew;
            if (item is IItemNewShopFlag ns)
                ns.IsNewShop = row.IsNewShop;
            if (item is IItemHeldFlag g)
                g.IsHeld = row.IsHeld;

            pouch.Items[ctr] = item;
            ctr++;
        }
        for (int i = ctr; i < pouch.Items.Length; i++)
            pouch.Items[i] = pouch.GetEmpty(); // Empty Slots at the end
    }

    /// <summary>
    /// Called by a row when its item or count changes; clamps the count and refreshes the sprite.
    /// </summary>
    private void RowValueChanged(InventoryRow row, InventoryPouch pouch)
    {
        var itemID = Array.IndexOf(itemlist, row.ItemName);
        if (itemID < 0)
            return;
        row.Sprite = GetSprite(itemID);

        // Sanity check the item count against its maximum
        var text = row.CountText;
        var count = Util.ToInt32(text);
        var original = count;
        if (Bag.IsQuantitySane(pouch.Type, itemID, ref count, HasNew, HaX) && count == original && text == count.ToString())
            return;
        // Defer: the text box ignores source changes raised while it is pushing its own value.
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => row.CountText = count.ToString());
    }

    private global::Avalonia.Media.Imaging.Bitmap? GetSprite(int itemID)
    {
        var context = Origin.Context;
        itemID = ItemConverter.GetItemDisplay(itemID, context);
        if (itemID == 0)
            return null;
        if (SpriteCache.TryGetValue(itemID, out var cached))
            return cached;
        var sk = SpriteUtil.Spriter.GetItemSprite(itemID, context); // shared resource bitmap; do not dispose
        return SpriteCache[itemID] = sk.ToAvaloniaBitmap();
    }

    private void ChangeViewedPouch(int index)
    {
        if ((uint)index >= Bag.Pouches.Count)
            return;
        var pouch = Bag.Pouches[index];
        NUD_Count.Maximum = pouch.MaxCount;

        bool disable = pouch.Type is InventoryType.PCItems or InventoryType.FreeSpace && Origin is not SAV8LA;
        NUD_Count.IsVisible = L_Count.IsVisible = B_GiveAll.IsVisible = !disable;
        if (disable && !HaX)
        {
            giveMenu.Items.Remove(giveAll);
            giveMenu.Items.Remove(giveModify);
        }
        else if (!giveMenu.Items.Contains(giveAll))
        {
            giveMenu.Items.Insert(0, giveAll);
            giveMenu.Items.Add(giveModify);
        }
        NUD_Count.Value = Math.Max(1, pouch.MaxCount - 4);
    }

    private string[] GetStringsForPouch(ReadOnlySpan<ushort> items, bool sort = true)
    {
        var result = new string[items.Length + 1];
        for (int i = 0; i < result.Length - 1; i++)
            result[i] = itemlist[items[i]];
        result[items.Length] = itemlist[0];
        if (sort)
            Array.Sort(result);
        return result;
    }

    private int CountValue => (int)(NUD_Count.Value ?? 1);

    private async System.Threading.Tasks.Task GiveAllItems()
    {
        var pouch = Bag.Pouches[CurrentPouch];
        var settings = await GetModifySettings(pouch);
        if (settings is not { } s)
            return;

        var items = pouch.GetAllItems().ToArray();
        // No need to trim the list on truncation; we filter by IsLegal.
        // GiveItem reaching a full pouch will fail silently (no exception thrown).
        // This is equivalent to filtering and truncating eagerly.
        if (s.Shuffle)
            Util.Rand.Shuffle(items);

        ModifyPouch(CurrentPouch, p => p.GiveAllItems(Bag, items, CountValue));
    }

    private async System.Threading.Tasks.Task<(bool Truncate, bool Shuffle)?> GetModifySettings(InventoryPouch pouch)
    {
        if (!pouch.IsCramped)
            return (false, false);

        var dr = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, MsgItemPouchSizeSmall,
            string.Format(MsgItemPouchRandom, Environment.NewLine));
        if (dr == DialogResult.Cancel)
            return null;
        return (true, dr == DialogResult.No);
    }

    private async System.Threading.Tasks.Task RemoveAllItems()
    {
        ModifyPouch(CurrentPouch, p => p.RemoveAll());
        await AppDialogs.Alert(this, MsgItemCleared);
    }

    private async System.Threading.Tasks.Task ModifyAllItems()
    {
        ModifyPouch(CurrentPouch, p => p.ModifyAllCount(Bag, CountValue));
        await AppDialogs.Alert(this, MsgItemPouchCountUpdated);
    }

    private void ModifyPouch(int pouch, Action<InventoryPouch> func)
    {
        var view = GetGrid(pouch);
        var p = Bag.Pouches[pouch];
        SetBag(view, p); // save current
        func(p); // update
        GetBag(view, p); // load current
    }

    private static string GetImageName(InventoryType type) => type switch
    {
        InventoryType.Items => "bag_items",
        InventoryType.KeyItems => "bag_key",
        InventoryType.TMHMs => "bag_tech",
        InventoryType.Medicine => "bag_medicine",
        InventoryType.Berries => "bag_berries",
        InventoryType.Balls => "bag_balls",
        InventoryType.BattleItems => "bag_battle",
        InventoryType.MailItems => "bag_mail",
        InventoryType.PCItems => "bag_pcitems",
        InventoryType.FreeSpace => "bag_free",
        InventoryType.ZCrystals => "bag_z",
        InventoryType.Candy => "bag_candy",
        InventoryType.Treasure => "bag_treasure",
        InventoryType.Ingredients => "bag_ingredient",
        InventoryType.MegaStones => "bag_mega",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    /// <summary>
    /// One editable pouch slot.
    /// </summary>
    public sealed class InventoryRow(InventoryWindow owner, InventoryPouch pouch, string itemName) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private bool loading;

        private string itemName = itemName;
        private string countText = "0";
        private string freeSpaceIndexText = "0";
        private bool isFreeSpace, isFavorite, isNew, isNewShop, isHeld;
        private global::Avalonia.Media.Imaging.Bitmap? sprite;

        public string ItemName { get => itemName; set { if (Set(ref itemName, value)) Changed(); } }
        public string CountText { get => countText; set { if (Set(ref countText, value)) Changed(); } }
        public string FreeSpaceIndexText { get => freeSpaceIndexText; set => Set(ref freeSpaceIndexText, value); }
        public bool IsFreeSpace { get => isFreeSpace; set => Set(ref isFreeSpace, value); }
        public bool IsFavorite { get => isFavorite; set => Set(ref isFavorite, value); }
        public bool IsNew { get => isNew; set => Set(ref isNew, value); }
        public bool IsNewShop { get => isNewShop; set => Set(ref isNewShop, value); }
        public bool IsHeld { get => isHeld; set => Set(ref isHeld, value); }
        public global::Avalonia.Media.Imaging.Bitmap? Sprite { get => sprite; set => Set(ref sprite, value); }

        internal void Load(InventoryItem item, string name)
        {
            loading = true;
            ItemName = name;
            CountText = item.Count.ToString();
            if (item is IItemFreeSpace f)
                IsFreeSpace = f.IsFreeSpace;
            if (item is IItemFreeSpaceIndex fi)
                FreeSpaceIndexText = fi.FreeSpaceIndex.ToString();
            if (item is IItemFavorite v)
                IsFavorite = v.IsFavorite;
            if (item is IItemNewFlag n)
                IsNew = n.IsNew;
            if (item is IItemNewShopFlag ns)
                IsNewShop = ns.IsNewShop;
            if (item is IItemHeldFlag g)
                IsHeld = g.IsHeld;
            Sprite = owner.GetSprite(item.Index);
            loading = false;
        }

        private void Changed()
        {
            if (!loading)
                owner.RowValueChanged(this, pouch);
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }

    /// <summary>
    /// Header + virtualized list of <see cref="InventoryRow"/> entries for one pouch.
    /// </summary>
    private sealed class PouchView : DockPanel
    {
        public readonly ObservableCollection<InventoryRow> Rows = [];
        private const double WidthSprite = 30, WidthItem = 150, WidthCount = 52, WidthCheck = 44;

        public PouchView(InventoryWindow owner, InventoryPouch pouch, string[] itemNames)
        {
            Name = $"DGV_{pouch.Type}";
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 4, 0, 2) };
            header.Children.Add(HeaderCell(string.Empty, WidthSprite));
            header.Children.Add(HeaderCell("Item", WidthItem));
            header.Children.Add(HeaderCell("Count", WidthCount));
            if (owner.HasFavorite) header.Children.Add(HeaderCell("Fav", WidthCheck));
            if (owner.HasNew) header.Children.Add(HeaderCell("New", WidthCheck));
            if (owner.HasFreeSpace) header.Children.Add(HeaderCell("Free", WidthCheck));
            if (owner.HasFreeSpaceIndex) header.Children.Add(HeaderCell("Free", WidthCount));
            if (owner.HasNewShop) header.Children.Add(HeaderCell("Shop", WidthCheck));
            if (owner.HasHeld) header.Children.Add(HeaderCell("Held", WidthCheck));
            SetDock(header, Dock.Top);
            Children.Add(header);

            var list = new ListBox
            {
                ItemsSource = Rows,
                SelectionMode = SelectionMode.Single,
                ItemTemplate = new FuncDataTemplate<InventoryRow>((_, _) => BuildRow(owner, itemNames)),
            };
            list.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
            {
                Setters =
                {
                    new Setter(PaddingProperty, new Thickness(4, 1)),
                    new Setter(MinHeightProperty, 0d),
                },
            });
            Children.Add(list);
        }

        private static TextBlock HeaderCell(string text, double width) => new()
        {
            Text = text,
            Width = width,
            FontWeight = FontWeight.Bold,
            TextAlignment = TextAlignment.Center,
        };

        private static Control BuildRow(InventoryWindow owner, string[] itemNames)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };

            var sprite = new Image { Width = WidthSprite, Height = 24, Stretch = Stretch.Uniform };
            sprite.Bind(Image.SourceProperty, new Binding(nameof(InventoryRow.Sprite)));
            panel.Children.Add(sprite);

            var combo = new ComboBox { Width = WidthItem, MinHeight = 0, Padding = new Thickness(6, 2), ItemsSource = itemNames, IsEnabled = !owner.ItemColumnReadOnly };
            combo.Bind(SelectingItemsControl.SelectedItemProperty, new Binding(nameof(InventoryRow.ItemName)) { Mode = BindingMode.TwoWay });
            panel.Children.Add(combo);

            panel.Children.Add(CountBox(nameof(InventoryRow.CountText), WidthCount));
            if (owner.HasFavorite) panel.Children.Add(Check(nameof(InventoryRow.IsFavorite)));
            if (owner.HasNew) panel.Children.Add(Check(nameof(InventoryRow.IsNew)));
            if (owner.HasFreeSpace) panel.Children.Add(Check(nameof(InventoryRow.IsFreeSpace)));
            if (owner.HasFreeSpaceIndex) panel.Children.Add(CountBox(nameof(InventoryRow.FreeSpaceIndexText), WidthCount));
            if (owner.HasNewShop) panel.Children.Add(Check(nameof(InventoryRow.IsNewShop)));
            if (owner.HasHeld) panel.Children.Add(Check(nameof(InventoryRow.IsHeld)));
            return panel;
        }

        private static NumericTextBox CountBox(string property, double width)
        {
            var tb = new NumericTextBox { Width = width, MaxLength = 5, MinHeight = 0 }; // enough to cover ushort.MaxValue
            tb.Bind(TextBox.TextProperty, new Binding(property) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            return tb;
        }

        private static CheckBox Check(string property)
        {
            var chk = new CheckBox { Width = WidthCheck, MinHeight = 0, HorizontalAlignment = HorizontalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(12, 0, 0, 0) };
            chk.Bind(global::Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding(property) { Mode = BindingMode.TwoWay });
            return chk;
        }
    }
}
