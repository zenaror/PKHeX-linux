using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen5;

/// <summary>
/// Pokémon Global Link editor (port of the WinForms <c>SAV_GlobalLink5</c>).
/// </summary>
public sealed class GlobalLink5Window : SaveEditorWindow
{
    private readonly SAV5 Origin;
    private readonly SAV5 SAV;
    private readonly GlobalLink5 Block;

    private readonly TabControl Tabs = new();
    private readonly TabItem Tab_General = new() { Name = "Tab_General", Header = "General" };
    private readonly TabItem Tab_Items = new() { Name = "Tab_Items", Header = "Items" };
    private readonly TabItem Tab_Furniture = new() { Name = "Tab_Furniture", Header = "Furniture" };

    private readonly TextBlock L_UploadDate = UiFactory.Label("L_UploadDate", "Upload Date:");
    private readonly CheckBox CHK_DateSet = UiFactory.Check("CHK_DateSet", "Set");
    private readonly DatePicker CAL_UploadDate = new() { Name = "CAL_UploadDate", MinWidth = 0 };
    private readonly TextBlock L_UploadCount = UiFactory.Label("L_UploadCount", "Upload Count:");
    private readonly NumericUpDown NUD_UploadCount = UiFactory.NumericUpDown("NUD_UploadCount", 0, int.MaxValue, 130);
    private readonly TextBlock L_UploadStatus = UiFactory.Label("L_UploadStatus", "Upload Status:");
    private readonly NumericUpDown NUD_UploadStatus = UiFactory.NumericUpDown("NUD_UploadStatus", 0, 255, 110);
    private readonly CheckBox CHK_IsSlotPresent = UiFactory.Check("CHK_IsSlotPresent", "Upload Slot Tucked In");
    private readonly CheckBox CHK_IsRegistered = UiFactory.Check("CHK_IsRegistered", "Game Card Registered");
    private readonly CheckBox CHK_IsFullAccess = UiFactory.Check("CHK_IsFullAccess", "Full Access");
    private readonly TextBlock L_Musical = UiFactory.Label("L_Musical", "Musical:");
    private readonly NumericUpDown NUD_Musical = UiFactory.NumericUpDown("NUD_Musical", 0, 255, 110);
    private readonly TextBlock L_CGearSkin = UiFactory.Label("L_CGearSkin", "CGear Skin:");
    private readonly NumericUpDown NUD_CGearSkin = UiFactory.NumericUpDown("NUD_CGearSkin", 0, 255, 110);
    private readonly TextBlock L_DexSkin = UiFactory.Label("L_DexSkin", "Pokédex Skin:");
    private readonly NumericUpDown NUD_DexSkin = UiFactory.NumericUpDown("NUD_DexSkin", 0, 255, 110);

    private readonly TextBlock L_FurnitureSelected = UiFactory.Label("L_FurnitureSelected", "Selected:");
    private readonly NumericUpDown NUD_FurnitureSelected = UiFactory.NumericUpDown("NUD_FurnitureSelected", 0, 255, 110);
    private readonly CheckBox CHK_FurnitureSynchronized = UiFactory.Check("CHK_FurnitureSynchronized", "Synchronized");
    private readonly NumericUpDown[] NUD_Furniture = new NumericUpDown[GlobalLink5.CountFurniture];
    private readonly TextBox[] TB_Furniture = new TextBox[GlobalLink5.CountFurniture];

    private readonly DataGrid DGV_Items = new() { AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column, IsReadOnly = false, CanUserSortColumns = false };
    private readonly ObservableCollection<ItemRow> ItemRows = [];

    public GlobalLink5Window(SAV5 sav) : base("SAV_GlobalLink5", "Pokémon Global Link Editor")
    {
        Origin = sav;
        SAV = (SAV5)sav.Clone();
        Block = SAV.GlobalLink;
        SizeToContent = SizeToContent.Manual;
        CanResize = true;
        Width = 560;
        Height = 520;

        var general = UiFactory.FormGrid(9);
        UiFactory.AddFormRow(general, 0, L_UploadDate, UiFactory.Row(CHK_DateSet, CAL_UploadDate));
        UiFactory.AddFormRow(general, 1, L_UploadCount, NUD_UploadCount);
        UiFactory.AddFormRow(general, 2, L_UploadStatus, NUD_UploadStatus);
        UiFactory.AddFormRow(general, 3, null, CHK_IsSlotPresent);
        UiFactory.AddFormRow(general, 4, null, CHK_IsRegistered);
        UiFactory.AddFormRow(general, 5, null, CHK_IsFullAccess);
        UiFactory.AddFormRow(general, 6, L_Musical, NUD_Musical);
        UiFactory.AddFormRow(general, 7, L_CGearSkin, NUD_CGearSkin);
        UiFactory.AddFormRow(general, 8, L_DexSkin, NUD_DexSkin);
        Tab_General.Content = general;

        var items = GameInfo.FilteredSources.Items;
        DGV_Items.Columns.Add(new DataGridTemplateColumn
        {
            Header = string.Empty,
            Width = new DataGridLength(40),
            CellTemplate = new FuncDataTemplate<ItemRow>((_, _) =>
            {
                var img = new Image { Width = 24, Height = 24, Stretch = Stretch.Uniform };
                img.Bind(Image.SourceProperty, new Binding(nameof(ItemRow.Sprite)));
                return img;
            }),
        });
        DGV_Items.Columns.Add(DataGridUtil.ComboColumn("Item", items, nameof(ItemRow.ItemID), 220));
        DGV_Items.Columns.Add(new DataGridTextColumn { Header = "Count", Binding = new Binding(nameof(ItemRow.Count)) { Mode = BindingMode.TwoWay }, Width = new DataGridLength(70) });
        DGV_Items.ItemsSource = ItemRows;
        Tab_Items.Content = DGV_Items;

        var furniture = UiFactory.FormGrid(GlobalLink5.CountFurniture + 2);
        UiFactory.AddFormRow(furniture, 0, L_FurnitureSelected, NUD_FurnitureSelected);
        UiFactory.AddFormRow(furniture, 1, null, CHK_FurnitureSynchronized);
        for (int i = 0; i < GlobalLink5.CountFurniture; i++)
        {
            NUD_Furniture[i] = UiFactory.NumericUpDown($"NUD_Furniture{i + 1}", 0, 65535, 120);
            TB_Furniture[i] = UiFactory.Text($"TB_Furniture{i + 1}", 20, 200);
            UiFactory.AddFormRow(furniture, i + 2, null, UiFactory.Row(NUD_Furniture[i], TB_Furniture[i]));
        }
        Tab_Furniture.Content = furniture;

        Tabs.Items.Add(Tab_General);
        Tabs.Items.Add(Tab_Items);
        Tabs.Items.Add(Tab_Furniture);
        SetBody(Tabs);

        CHK_DateSet.IsCheckedChanged += (_, _) => CAL_UploadDate.IsVisible = CHK_DateSet.IsChecked == true;

        LoadData();
    }

    private void LoadData()
    {
        var date = Block.UploadDate;
        if (date.IsValid)
        {
            var d = date.ToDateOnly();
            CAL_UploadDate.SelectedDate = UiFactory.ToOffset(d.ToDateTime(TimeOnly.MinValue));
            CAL_UploadDate.IsVisible = true;
            CHK_DateSet.IsChecked = true;
        }
        else
        {
            CAL_UploadDate.SelectedDate = UiFactory.ToOffset(DateTime.Today);
            CAL_UploadDate.IsVisible = false;
            CHK_DateSet.IsChecked = false;
        }

        NUD_UploadCount.Value = Block.UploadCount;
        NUD_UploadStatus.Value = Block.UploadStatus;
        CHK_IsSlotPresent.IsChecked = Block.IsSlotPresent;
        CHK_IsRegistered.IsChecked = Block.IsRegistered;
        CHK_IsFullAccess.IsChecked = Block.IsAccountFullAccess;
        NUD_Musical.Value = Block.Musical;
        NUD_CGearSkin.Value = Block.CGearSkin;
        NUD_DexSkin.Value = Block.DexSkin;

        NUD_FurnitureSelected.Value = Block.SelectedFurnitureIndex;
        CHK_FurnitureSynchronized.IsChecked = Block.IsFurnitureSynchronized;

        ItemRows.Clear();
        for (int i = 0; i < GlobalLink5.CountItems; i++)
        {
            var itemID = Block.GetItem(i);
            var quantity = Block.GetItemQuantity(i);
            ItemRows.Add(new ItemRow { ItemID = itemID, Count = quantity.ToString() });
        }

        for (int i = 0; i < GlobalLink5.CountFurniture; i++)
        {
            var furniture = Block.GetFurniture(i);
            NUD_Furniture[i].Value = furniture.Value;
            TB_Furniture[i].Text = furniture.Name;
        }
    }

    protected override void OnSave()
    {
        var value = CAL_UploadDate.SelectedDate?.DateTime ?? DateTime.Today;
        var date = Block.UploadDate;
        if (CHK_DateSet.IsChecked == true)
            date.FromDateOnly(new DateOnly(value.Year, value.Month, value.Day));
        else
            date.SetEmpty();

        Block.UploadCount = (int)(NUD_UploadCount.Value ?? 0);
        Block.UploadStatus = (byte)(NUD_UploadStatus.Value ?? 0);
        Block.IsSlotPresent = CHK_IsSlotPresent.IsChecked == true;
        Block.IsRegistered = CHK_IsRegistered.IsChecked == true;
        Block.IsAccountFullAccess = CHK_IsFullAccess.IsChecked == true;
        Block.Musical = (byte)(NUD_Musical.Value ?? 0);
        Block.CGearSkin = (byte)(NUD_CGearSkin.Value ?? 0);
        Block.DexSkin = (byte)(NUD_DexSkin.Value ?? 0);

        Block.SelectedFurnitureIndex = (byte)(NUD_FurnitureSelected.Value ?? 0);
        Block.IsFurnitureSynchronized = CHK_FurnitureSynchronized.IsChecked == true;

        for (int i = 0; i < GlobalLink5.CountItems && i < ItemRows.Count; i++)
        {
            var row = ItemRows[i];
            Block.SetItem(i, (ushort)row.ItemID);
            byte.TryParse(row.Count, out var quantity);
            Block.SetItemQuantity(i, quantity);
        }

        for (int i = 0; i < GlobalLink5.CountFurniture; i++)
        {
            var furniture = Block.GetFurniture(i);
            furniture.Value = (ushort)(NUD_Furniture[i].Value ?? 0);
            furniture.Name = TB_Furniture[i].Text ?? string.Empty;
        }

        Origin.CopyChangesFrom(SAV);
        Close();
    }

    private sealed class ItemRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private int itemID;
        private string count = "0";

        public int ItemID
        {
            get => itemID;
            set
            {
                if (itemID == value)
                    return;
                itemID = value;
                Raise();
                Raise(nameof(Sprite));
            }
        }

        public string Count
        {
            get => count;
            set { count = value; Raise(); }
        }

        public global::Avalonia.Media.Imaging.Bitmap? Sprite => GetSprite(itemID);

        private static readonly Dictionary<int, global::Avalonia.Media.Imaging.Bitmap?> Cache = [];

        private static global::Avalonia.Media.Imaging.Bitmap? GetSprite(int itemID)
        {
            if (itemID == 0)
                return null;
            lock (Cache)
            {
                if (Cache.TryGetValue(itemID, out var cached))
                    return cached;
                var sk = SpriteUtil.GetItemSprite(itemID); // shared resource; do not dispose
                return Cache[itemID] = sk?.ToAvaloniaBitmap();
            }
        }

        private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
