using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using static PKHeX.Core.SCBlockUtil;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen8;

/// <summary>
/// Raw save block browser for the block-based saves from Sword/Shield onwards
/// (port of the WinForms <c>SAV_BlockDump8</c>).
/// </summary>
/// <remarks>
/// Lists every block by key with its friendly name when one is known, shows the raw bytes, and offers a
/// property grid for blocks whose layout is understood. The second tab diffs two save files block by block.
/// </remarks>
public sealed class BlockDump8Window : Window
{
    private readonly ISCBlockArray SAV;
    private readonly SCBlockMetadata Metadata;
    private readonly ComboItem[] SortedBlockKeys;

    private SCBlock CurrentBlock = null!;
    private string Filter = string.Empty;

    private readonly ComboBox CB_Key = UiFactory.Combo("CB_Key", 380);
    private readonly ComboBox CB_TypeToggle = UiFactory.Combo("CB_TypeToggle", 130);
    private readonly TextBlock L_BlockName = UiFactory.Label("L_BlockName", string.Empty);
    private readonly TextBlock L_Detail_R = UiFactory.Label("L_Detail_R", "Block Details");
    private readonly TextBox RTB_Hex = new() { Name = "RTB_Hex", Width = 460, Height = 240, AcceptsReturn = true, IsReadOnly = true, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, FontFamily = new global::Avalonia.Media.FontFamily("monospace") };
    private readonly PropertyGridView PG_BlockView = new() { Name = "PG_BlockView", Width = 460, Height = 240 };

    private readonly Button B_ExportAll = UiFactory.Button("B_ExportAll", "Export Blocks To Folder");
    private readonly Button B_ImportFolder = UiFactory.Button("B_ImportFolder", "Import Blocks From Folder");
    private readonly Button B_ExportCurrent = UiFactory.Button("B_ExportCurrent", "Export Current Block");
    private readonly Button B_ImportCurrent = UiFactory.Button("B_ImportCurrent", "Import Current Block");
    private readonly Button B_ExportAllSingle = UiFactory.Button("B_ExportAllSingle", "Export All (Single File)");
    private readonly CheckBox CHK_DataOnly = UiFactory.Check("CHK_DataOnly", "Data Blocks Only");
    private readonly CheckBox CHK_Key = UiFactory.Check("CHK_Key", "Include 32Bit Key");
    private readonly CheckBox CHK_Type = UiFactory.Check("CHK_Type", "Include Type Info");
    private readonly CheckBox CHK_FakeHeader = UiFactory.Check("CHK_FakeHeader", "Mark Block Start (ASCII)");

    private readonly TextBox TB_OldSAV = UiFactory.Text("TB_OldSAV", 260, 360);
    private readonly TextBox TB_NewSAV = UiFactory.Text("TB_NewSAV", 260, 360);
    private readonly Button B_LoadOld = UiFactory.Button("B_LoadOld", "Load Old");
    private readonly Button B_LoadNew = UiFactory.Button("B_LoadNew", "Load New");
    private readonly TextBox richTextBox1 = new() { Name = "richTextBox1", Width = 700, Height = 320, AcceptsReturn = true, IsReadOnly = true, FontFamily = new global::Avalonia.Media.FontFamily("monospace") };

    private readonly Button B_Cancel = UiFactory.Button("B_Cancel", "Close");

    public BlockDump8Window(ISCBlockArray sav)
    {
        Name = "SAV_BlockDump8";
        Title = "Block Data";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        SAV = sav;

        var extra = GetExtraKeyNames(sav);
        Metadata = new SCBlockMetadata(SAV.Accessor, extra, MainWindow.Settings.Advanced.GetExclusionList8());
        SortedBlockKeys = [.. Metadata.GetSortedBlockKeyList()];

        BuildLayout();

        CB_Key.SetItems(SortedBlockKeys);
        CB_TypeToggle.SetItems(new[]
        {
            new ComboItem(nameof(SCTypeCode.Bool1), (int)SCTypeCode.Bool1),
            new ComboItem(nameof(SCTypeCode.Bool2), (int)SCTypeCode.Bool2),
        });

        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
        CB_Key.SelectedIndex = 0;
    }

    private void BuildLayout()
    {
        var options = UiFactory.Column(CHK_DataOnly, CHK_Key, CHK_Type, CHK_FakeHeader);
        var buttons = UiFactory.Column(
            B_ExportCurrent, B_ImportCurrent,
            B_ExportAll, B_ImportFolder, B_ExportAllSingle);

        var viewers = new Panel();
        viewers.Children.Add(RTB_Hex);
        viewers.Children.Add(PG_BlockView);

        var dump = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_Key", "Block Key:"), CB_Key, CB_TypeToggle),
            L_BlockName,
            UiFactory.Row(UiFactory.Label("L_Detail_L", "Block Detail:"), L_Detail_R),
            viewers,
            UiFactory.Row(buttons, options));

        var researcher = new GroupBoxView("GB_Researcher", "Load Two Save Files", UiFactory.Column(
            UiFactory.Row(B_LoadOld, TB_OldSAV),
            UiFactory.Row(B_LoadNew, TB_NewSAV)));
        var compare = UiFactory.Column(researcher, richTextBox1);

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Name = "Tab_Dump", Header = "Dump", Content = dump });
        tabs.Items.Add(new TabItem { Name = "Tab_Compare", Header = "Compare", Content = compare });

        B_Cancel.MinWidth = 80;
        B_Cancel.HorizontalAlignment = HorizontalAlignment.Right;
        B_Cancel.Click += (_, _) => Close();

        var root = new DockPanel { Margin = new global::Avalonia.Thickness(10) };
        DockPanel.SetDock(B_Cancel, Dock.Bottom);
        root.Children.Add(B_Cancel);
        root.Children.Add(tabs);
        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

        CB_Key.SelectionChanged += (_, _) => ChangeBlockKey();
        CB_TypeToggle.SelectionChanged += (_, _) => ChangeBooleanType();
        CB_Key.KeyDown += (_, e) => { if (e.Key == Key.Enter) ApplyFilter(); };
        B_ExportCurrent.Click += async (_, _) => await ExportSelectBlock();
        B_ImportCurrent.Click += async (_, _) => await ImportSelectBlock();
        B_ExportAll.Click += async (_, _) => await ExportAllToFolder();
        B_ImportFolder.Click += async (_, _) => await ImportFromFolder();
        B_ExportAllSingle.Click += async (_, _) => await ExportAllSingleFile();
        B_LoadOld.Click += async (_, _) => await LoadCompare(TB_OldSAV);
        B_LoadNew.Click += async (_, _) => await LoadCompare(TB_NewSAV);
    }

    private static IEnumerable<string> GetExtraKeyNames(ISCBlockArray obj)
    {
        var extra = MainWindow.Settings.Advanced.PathBlockKeyList;
        if (extra.Length != 0 && !Directory.Exists(extra))
            return [];
        var file = Path.Combine(extra, $"{obj.GetType().Name}.txt");
        return File.Exists(file) ? File.ReadLines(file) : [];
    }

    #region Block view

    private void ChangeBlockKey()
    {
        if (CB_Key.GetSelectedItem() is not { } item)
            return;
        CurrentBlock = SAV.Accessor.GetBlock((uint)item.Value);
        UpdateBlockSummaryControls();

        if (CurrentBlock.Type.IsBoolean())
        {
            CB_TypeToggle.SetValue((int)CurrentBlock.Type);
            CB_TypeToggle.IsVisible = true;
        }
        else
        {
            CB_TypeToggle.IsVisible = false;
        }
    }

    private void UpdateBlockSummaryControls()
    {
        var block = CurrentBlock;
        L_Detail_R.Text = GetBlockSummary(block);

        var sb = new StringBuilder();
        foreach (var b in block.Data)
            sb.Append($"{b:X2} ");
        RTB_Hex.Text = sb.ToString();

        var blockName = Metadata.GetBlockName(block, out var obj);
        L_BlockName.IsVisible = blockName is not null;
        if (blockName is not null)
            L_BlockName.Text = blockName;

        // Show the property grid for blocks with a known layout; otherwise show the raw hex.
        var mods = MainWindow.CurrentModifiers;
        if (mods != KeyModifiers.Control)
        {
            if (obj is not null)
            {
                var props = ReflectUtil.GetPropertiesCanWritePublicDeclared(obj.GetType());
                if (props.Count() > 1 || mods == KeyModifiers.Shift)
                {
                    PG_BlockView.SetObject(obj);
                    SetBlockView(true);
                    return;
                }
            }

            var editable = SCBlockMetadata.GetEditableBlockObject(block);
            if (editable is not null)
            {
                PG_BlockView.SetObject(editable);
                SetBlockView(true);
                return;
            }
        }
        SetBlockView(false);
    }

    /// <summary>
    /// The property grid has a transparent background, so unlike the WinForms grid it does not cover the hex
    /// view stacked underneath it; hide the hex view instead of letting the two render on top of each other.
    /// </summary>
    private void SetBlockView(bool showGrid)
    {
        PG_BlockView.IsVisible = showGrid;
        RTB_Hex.IsVisible = !showGrid;
    }

    private void ChangeBooleanType()
    {
        if (CurrentBlock is null || CB_TypeToggle.GetSelectedItem() is not { } item)
            return;
        var desired = (SCTypeCode)item.Value;
        if (CurrentBlock.Type == desired)
            return;
        CurrentBlock.ChangeBooleanType(desired);
        UpdateBlockSummaryControls();
    }

    /// <summary>Enter in the key box either jumps to a hexadecimal key or filters the list by name.</summary>
    private void ApplyFilter()
    {
        var text = (CB_Key.Text ?? string.Empty).Trim();
        if (text.Length == 8)
        {
            var hex = (int)Util.GetHexValue(text);
            if (hex != 0)
            {
                if (Filter.Length != 0)
                {
                    CB_Key.SetItems(SortedBlockKeys);
                    Filter = string.Empty;
                }
                if (SortedBlockKeys.Any(z => z.Value == hex))
                {
                    CB_Key.SetValue(hex);
                    return;
                }
            }
        }

        if (Filter.Equals(text, StringComparison.InvariantCultureIgnoreCase))
            return;

        Filter = text;
        if (text.Length == 0)
        {
            CB_Key.SetItems(SortedBlockKeys);
            CB_Key.SelectedIndex = 0;
            return;
        }

        var filtered = Array.FindAll(SortedBlockKeys, x => x.Text.Contains(text, StringComparison.InvariantCultureIgnoreCase));
        if (filtered.Length == 0)
            return;
        CB_Key.SetItems(filtered);
        CB_Key.SelectedIndex = 0;
    }

    #endregion

    #region File I/O

    private SCBlockExportOption GetExportOption()
    {
        var option = SCBlockExportOption.None;
        if (CHK_DataOnly.IsChecked == true)
            option |= SCBlockExportOption.DataOnly;
        if (CHK_Key.IsChecked == true)
            option |= SCBlockExportOption.Key;
        if (CHK_Type.IsChecked == true)
            option |= SCBlockExportOption.TypeInfo;
        if (CHK_FakeHeader.IsChecked == true)
            option |= SCBlockExportOption.FakeHeader;
        return option;
    }

    private async Task ExportSelectBlock()
    {
        var name = GetBlockFileNameWithoutExtension(CurrentBlock);
        var path = await FileDialogs.SaveFileDialog(this, null, $"{name}.bin");
        if (path is null)
            return;
        await File.WriteAllBytesAsync(path, CurrentBlock.Data.ToArray());
    }

    private async Task ImportSelectBlock()
    {
        var path = await FileDialogs.OpenSingleFile(this);
        if (path is null)
            return;
        var file = new FileInfo(path);
        if (file.Length != CurrentBlock.Data.Length)
        {
            await AppDialogs.Error(this, string.Format(MessageStrings.MsgFileSize, $"0x{file.Length:X8}"));
            return;
        }
        CurrentBlock.ChangeData(await File.ReadAllBytesAsync(path));
        UpdateBlockSummaryControls();
    }

    private async Task ExportAllToFolder()
    {
        var path = await FileDialogs.PickFolder(this);
        if (path is null)
            return;
        foreach (var b in SAV.AllBlocks.Where(z => z.Data.Length != 0))
            await File.WriteAllBytesAsync(Path.Combine(path, $"{GetBlockFileNameWithoutExtension(b)}.bin"), b.Data.ToArray());
    }

    private async Task ImportFromFolder()
    {
        var path = await FileDialogs.PickFolder(this);
        if (path is null)
            return;
        var failed = ImportBlocksFromFolder(path, SAV);
        if (failed.Count != 0)
            await AppDialogs.Error(this, "Failed to import:", string.Join(Environment.NewLine, failed));
        UpdateBlockSummaryControls();
    }

    private async Task ExportAllSingleFile()
    {
        var path = await FileDialogs.SaveFileDialog(this, null, "raw.bin");
        if (path is null)
            return;
        ExportAllBlocksAsSingleFile(SAV.Accessor.BlockInfo, path, GetExportOption());
    }

    private async Task LoadCompare(TextBox dest)
    {
        var path = await FileDialogs.OpenSingleFile(this);
        if (path is null)
            return;
        dest.Text = path;
        if ((TB_OldSAV.Text ?? string.Empty).Length != 0 && (TB_NewSAV.Text ?? string.Empty).Length != 0)
            CompareSaves();
    }

    private void CompareSaves()
    {
        var p1 = TB_OldSAV.Text ?? string.Empty;
        var p2 = TB_NewSAV.Text ?? string.Empty;
        if (!SaveUtil.TryGetSaveFile(p1, out var s1) || s1 is not ISCBlockArray w1)
            return;
        if (!SaveUtil.TryGetSaveFile(p2, out var s2) || s2 is not ISCBlockArray w2)
            return;

        var extra = GetExtraKeyNames(w1);
        var compare = new SCBlockCompare(w1.Accessor, w2.Accessor, extra);
        richTextBox1.Text = string.Join(Environment.NewLine, compare.Summary());
    }

    #endregion
}
