using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen9;

/// <summary>
/// Fashion/hair unlock editor for Scarlet-Violet and Legends: Z-A (port of the WinForms <c>SAV_Fashion9</c>).
/// </summary>
public sealed class Fashion9Window : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SaveFile SAV;
    private readonly FashionBlockEditor[] Grids;

    private readonly TabControl TC_Features = new() { Name = "TC_Features" };
    private readonly Button B_SetAllOwned = UiFactory.Button("B_SetAllOwned", "Set All Owned");

    public Fashion9Window(SaveFile sav) : base("SAV_Fashion9", "Fashion")
    {
        SAV = (Origin = sav).Clone();

        // Many categories: let the tab strip wrap, matching the WinForms multiline tab control.
        TC_Features.ItemsPanel = new FuncTemplate<Panel?>(() => new WrapPanel { Orientation = Orientation.Horizontal });
        TC_Features.MaxWidth = 900; // let the wide tab strip wrap instead of stretching the window

        Grids = SAV switch
        {
            SAV9SV sv => GetBlocks9SV(sv.Blocks),
            SAV9ZA za => GetBlocks9ZA(za.Blocks),
            _ => throw new ArgumentException("Invalid SaveFile Type", nameof(sav)),
        };
        B_SetAllOwned.IsVisible = SAV is SAV9ZA;

        foreach (var grid in Grids)
            TC_Features.Items.Add(grid.Tab);
        TC_Features.SelectedIndex = 0;

        ButtonBar.Children.Insert(0, B_SetAllOwned);
        B_SetAllOwned.AttachClickHandled(ClickSetAllOwned);

        SetBody(TC_Features);

        foreach (var grid in Grids)
            grid.Load();

        // Dropping a block dump on the window replaces the selected category's block, as in WinForms.
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = DragDropEffects.Copy; e.Handled = true; });
        AddHandler(DragDrop.DropEvent, async (_, e) => await WindowDrop(e));
    }

    private FashionBlockEditor[] GetBlocks9SV(SaveBlockAccessor9SV accessor) =>
    [
        Create(accessor, SaveBlockAccessor9SV.KFashionUnlockedEyewear, nameof(SaveBlockAccessor9SV.KFashionUnlockedEyewear)),
        Create(accessor, SaveBlockAccessor9SV.KFashionUnlockedGloves, nameof(SaveBlockAccessor9SV.KFashionUnlockedGloves)),
        Create(accessor, SaveBlockAccessor9SV.KFashionUnlockedBag, nameof(SaveBlockAccessor9SV.KFashionUnlockedBag)),
        Create(accessor, SaveBlockAccessor9SV.KFashionUnlockedFootwear, nameof(SaveBlockAccessor9SV.KFashionUnlockedFootwear)),
        Create(accessor, SaveBlockAccessor9SV.KFashionUnlockedHeadwear, nameof(SaveBlockAccessor9SV.KFashionUnlockedHeadwear)),
        Create(accessor, SaveBlockAccessor9SV.KFashionUnlockedLegwear, nameof(SaveBlockAccessor9SV.KFashionUnlockedLegwear)),
        Create(accessor, SaveBlockAccessor9SV.KFashionUnlockedClothing, nameof(SaveBlockAccessor9SV.KFashionUnlockedClothing)),
        Create(accessor, SaveBlockAccessor9SV.KFashionUnlockedPhoneCase, nameof(SaveBlockAccessor9SV.KFashionUnlockedPhoneCase)),
    ];

    private FashionBlockEditor[] GetBlocks9ZA(SaveBlockAccessor9ZA accessor) =>
    [
        Create(accessor, SaveBlockAccessor9ZA.KFashionTops, nameof(SaveBlockAccessor9ZA.KFashionTops)),
        Create(accessor, SaveBlockAccessor9ZA.KFashionBottoms, nameof(SaveBlockAccessor9ZA.KFashionBottoms)),
        Create(accessor, SaveBlockAccessor9ZA.KFashionAllInOne, nameof(SaveBlockAccessor9ZA.KFashionAllInOne)),
        Create(accessor, SaveBlockAccessor9ZA.KFashionHeadwear, nameof(SaveBlockAccessor9ZA.KFashionHeadwear)),
        Create(accessor, SaveBlockAccessor9ZA.KFashionEyewear, nameof(SaveBlockAccessor9ZA.KFashionEyewear)),
        Create(accessor, SaveBlockAccessor9ZA.KFashionGloves, nameof(SaveBlockAccessor9ZA.KFashionGloves)),
        Create(accessor, SaveBlockAccessor9ZA.KFashionLegwear, nameof(SaveBlockAccessor9ZA.KFashionLegwear)),
        Create(accessor, SaveBlockAccessor9ZA.KFashionFootwear, nameof(SaveBlockAccessor9ZA.KFashionFootwear)),
        Create(accessor, SaveBlockAccessor9ZA.KFashionSatchels, nameof(SaveBlockAccessor9ZA.KFashionSatchels)),
        Create(accessor, SaveBlockAccessor9ZA.KFashionEarrings, nameof(SaveBlockAccessor9ZA.KFashionEarrings)),

        Create(accessor, SaveBlockAccessor9ZA.KHairMake00StyleHair, nameof(SaveBlockAccessor9ZA.KHairMake00StyleHair), hair: true),
        Create(accessor, SaveBlockAccessor9ZA.KHairMake01StyleBangs, nameof(SaveBlockAccessor9ZA.KHairMake01StyleBangs), hair: true),
        Create(accessor, SaveBlockAccessor9ZA.KHairMake02ColorHair, nameof(SaveBlockAccessor9ZA.KHairMake02ColorHair), hair: true),
        Create(accessor, SaveBlockAccessor9ZA.KHairMake03ColorHair, nameof(SaveBlockAccessor9ZA.KHairMake03ColorHair), hair: true),
        Create(accessor, SaveBlockAccessor9ZA.KHairMake04ColorHair, nameof(SaveBlockAccessor9ZA.KHairMake04ColorHair), hair: true),
        Create(accessor, SaveBlockAccessor9ZA.KHairMake05StyleEyebrow, nameof(SaveBlockAccessor9ZA.KHairMake05StyleEyebrow), hair: true),
        Create(accessor, SaveBlockAccessor9ZA.KHairMake06ColorEyebrow, nameof(SaveBlockAccessor9ZA.KHairMake06ColorEyebrow), hair: true),
        Create(accessor, SaveBlockAccessor9ZA.KHairMake07StyleEyes, nameof(SaveBlockAccessor9ZA.KHairMake07StyleEyes), hair: true),
        Create(accessor, SaveBlockAccessor9ZA.KHairMake08ColorEyes, nameof(SaveBlockAccessor9ZA.KHairMake08ColorEyes), hair: true),
        Create(accessor, SaveBlockAccessor9ZA.KHairMake09StyleEyelash, nameof(SaveBlockAccessor9ZA.KHairMake09StyleEyelash), hair: true),
        Create(accessor, SaveBlockAccessor9ZA.KHairMake10ColorEyelash, nameof(SaveBlockAccessor9ZA.KHairMake10ColorEyelash), hair: true),
        Create(accessor, SaveBlockAccessor9ZA.KHairMake11Lips, nameof(SaveBlockAccessor9ZA.KHairMake11Lips), hair: true),
        Create(accessor, SaveBlockAccessor9ZA.KHairMake12BeautyMark, nameof(SaveBlockAccessor9ZA.KHairMake12BeautyMark), hair: true),
        Create(accessor, SaveBlockAccessor9ZA.KHairMake13Freckles, nameof(SaveBlockAccessor9ZA.KHairMake13Freckles), hair: true),
        Create(accessor, SaveBlockAccessor9ZA.KHairMake14DarkCircles, nameof(SaveBlockAccessor9ZA.KHairMake14DarkCircles), hair: true),
    ];

    private static string GetTabText(string name)
    {
        if (name.StartsWith("KHairMake", StringComparison.Ordinal))
            return name.Replace("KHairMake", string.Empty);
        if (name.StartsWith("KFashionUnlocked", StringComparison.Ordinal))
            return name.Replace("KFashionUnlocked", string.Empty);
        return name.Replace("KFashion", string.Empty);
    }

    private FashionBlockEditor Create(SCBlockAccessor accessor, uint blockLoc, string name, bool hair = false)
    {
        var block = accessor.GetBlock(blockLoc);
        FashionBlockEditor editor = accessor switch
        {
            SaveBlockAccessor9SV => new FashionItem9Editor(block, name),
            SaveBlockAccessor9ZA when hair => new HairMake9aEditor(block, name),
            SaveBlockAccessor9ZA => new FashionItem9aEditor(block, name),
            _ => throw new ArgumentException("Invalid Accessor Type", nameof(accessor)),
        };
        editor.Tab.Name = $"Tab_{name}";
        editor.Tab.Header = GetTabText(name);
        return editor;
    }

    private FashionBlockEditor? Selected => TC_Features.SelectedIndex >= 0 ? Grids[TC_Features.SelectedIndex] : null;

    private void ClickSetAllOwned(KeyModifiers mods)
    {
        var state = !mods.HasFlag(KeyModifiers.Alt);
        if (mods.HasFlag(KeyModifiers.Shift))
        {
            foreach (var editor in Grids)
                editor.SetAllOwned(state);
            return;
        }
        Selected?.SetAllOwned(state);
    }

    private async Task WindowDrop(DragEventArgs e)
    {
        e.Handled = true;
        if (e.DataTransfer.TryGetFiles() is not { Length: not 0 } files)
            return;
        if (files[0].TryGetLocalPath() is not { } path)
            return;
        if (Selected is not { } editor)
            return;

        var size = new FileInfo(path).Length;
        if (size > 0x1_0000)
        {
            await AppDialogs.Alert(this, "File too large to be a valid block file.");
            return;
        }
        if (await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, "Load block to current tab?") != DialogResult.Yes)
            return;

        var data = File.ReadAllBytes(path);
        var block = editor.Block;
        if (data.Length != block.Raw.Length)
        {
            await AppDialogs.Alert(this, $"File size does not match block size of {block.Raw.Length} bytes.");
            return;
        }
        block.ChangeData(data);
        editor.Load();
    }

    protected override void OnSave()
    {
        foreach (var grid in Grids)
            grid.Save();
        Origin.CopyChangesFrom(SAV);
        Close();
    }

    #region Editors

    /// <summary>One category tab: a grid of rows mapped onto the category's save block.</summary>
    private abstract class FashionBlockEditor
    {
        public SCBlock Block { get; }
        public string Name { get; }
        public TabItem Tab { get; }
        protected ObservableCollection<FashionRow> Rows { get; } = [];

        protected FashionBlockEditor(SCBlock block, string name, bool full)
        {
            Block = block;
            Name = name;

            var grid = new DataGrid
            {
                Name = $"DGV_{name}",
                ItemsSource = Rows,
                AutoGenerateColumns = false,
                CanUserReorderColumns = false,
                CanUserSortColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                IsReadOnly = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = full ? 640 : 220,
                MaxHeight = 480,
            };
            grid.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new global::Avalonia.Data.Binding(nameof(FashionRow.Value)) { Mode = global::Avalonia.Data.BindingMode.TwoWay }, Width = new DataGridLength(110) });
            grid.Columns.Add(DataGridUtil.CheckColumn("IsNew", nameof(FashionRow.IsNew), 90));
            if (full)
            {
                grid.Columns.Add(DataGridUtil.CheckColumn("IsNewShop", nameof(FashionRow.IsNewShop), 110));
                grid.Columns.Add(DataGridUtil.CheckColumn("IsNewGroup", nameof(FashionRow.IsNewGroup), 115));
                grid.Columns.Add(DataGridUtil.CheckColumn("IsEquipped", nameof(FashionRow.IsEquipped), 115));
                grid.Columns.Add(DataGridUtil.CheckColumn("IsOwned", nameof(FashionRow.IsOwned), 100));
            }
            Tab = new TabItem { Content = grid };
        }

        public abstract void Load();
        public abstract void Save();
        public virtual void SetAllOwned(bool state) { }
    }

    private sealed class FashionItem9Editor(SCBlock block, string name) : FashionBlockEditor(block, name, full: false)
    {
        public override void Load()
        {
            Rows.Clear();
            foreach (var item in FashionItem9.GetArray(Block.Data))
                Rows.Add(new FashionRow { Value = item.Value, Flags = item.Flags });
        }

        public override void Save()
        {
            var array = FashionItem9.GetArray(Block.Data);
            for (int i = 0; i < array.Length && i < Rows.Count; i++)
            {
                array[i].Value = Rows[i].Value;
                array[i].IsNew = Rows[i].IsNew;
            }
            FashionItem9.SetArray(array, Block.Data);
        }
    }

    private sealed class HairMake9aEditor(SCBlock block, string name) : FashionBlockEditor(block, name, full: false)
    {
        public override void Load()
        {
            Rows.Clear();
            foreach (var item in HairMakeItem9a.GetArray(Block.Data))
                Rows.Add(new FashionRow { Value = item.Value, Flags = item.Flags });
        }

        public override void Save()
        {
            var array = HairMakeItem9a.GetArray(Block.Data);
            for (int i = 0; i < array.Length && i < Rows.Count; i++)
            {
                array[i].Value = Rows[i].Value;
                array[i].IsNew = Rows[i].IsNew;
            }
            HairMakeItem9a.SetArray(array, Block.Data);
        }
    }

    private sealed class FashionItem9aEditor(SCBlock block, string name) : FashionBlockEditor(block, name, full: true)
    {
        public override void Load()
        {
            Rows.Clear();
            foreach (var item in FashionItem9a.GetArray(Block.Data))
                Rows.Add(new FashionRow { Value = item.Value, Flags = item.Flags });
        }

        public override void Save()
        {
            var array = FashionItem9a.GetArray(Block.Data);
            for (int i = 0; i < array.Length && i < Rows.Count; i++)
            {
                array[i].Value = Rows[i].Value;
                array[i].Flags = Rows[i].Flags;
            }
            FashionItem9a.SetArray(array, Block.Data);
        }

        public override void SetAllOwned(bool state)
        {
            Save();
            FashionItem9a.ModifyAll(Block.Data, z => z.IsOwned = state);
            Load();
        }
    }

    /// <summary>Grid row; the flag bits match <see cref="FashionItem9a"/>.</summary>
    private sealed class FashionRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private uint value;
        private uint flags;

        public required uint Value
        {
            get => value;
            set { if (this.value == value) return; this.value = value; Raise(); }
        }

        public uint Flags
        {
            get => flags;
            set { if (flags == value) return; flags = value; Raise(); }
        }

        public bool IsNew { get => Get(0x1); set => Set(0x1, value); }
        public bool IsNewShop { get => Get(0x2); set => Set(0x2, value); }
        public bool IsNewGroup { get => Get(0x4); set => Set(0x4, value); }
        public bool IsEquipped { get => Get(0x8); set => Set(0x8, value); }
        public bool IsOwned { get => Get(0x10); set => Set(0x10, value); }

        private bool Get(uint mask) => (flags & mask) != 0;

        private void Set(uint mask, bool state, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        {
            var updated = state ? (flags | mask) : (flags & ~mask);
            if (updated == flags)
                return;
            flags = updated;
            Raise(name);
        }

        private void Raise([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion
}
