using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Views;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors;

/// <summary>
/// Block viewer for the save formats with a named block accessor (port of the WinForms <c>SAV_Accessor</c>).
/// </summary>
/// <remarks>The WinForms form is generic over the accessor type only to construct the metadata; the metadata itself
/// works on the interface, so this window takes it directly.</remarks>
public sealed class BlockAccessorWindow : Window
{
    private readonly SaveBlockMetadata<BlockInfo> Metadata;

    private readonly ComboBox CB_Key = UiFactory.StringCombo("CB_Key", 280);
    private readonly PropertyGridView PG_BlockView = new() { MinHeight = 380, MinWidth = 420 };

    public BlockAccessorWindow(ISaveBlockAccessor<BlockInfo> accessor)
    {
        Name = "SAV_Accessor";
        Title = "Block Data";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.WidthAndHeight;

        Metadata = new SaveBlockMetadata<BlockInfo>(accessor);
        foreach (var name in Metadata.GetSortedBlockList())
            CB_Key.Items.Add(name);

        var header = UiFactory.Row(UiFactory.Label("L_Key", "Block:"), CB_Key);
        header.Margin = new global::Avalonia.Thickness(0, 0, 0, 6);

        var tabs = new TabControl { Name = "TC_Tabs" };
        tabs.Items.Add(new TabItem { Name = "Tab_Blocks", Header = "Blocks", Content = PG_BlockView });
        tabs.SelectedIndex = 0;

        Content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new global::Avalonia.Thickness(8),
            Children = { header, tabs },
        };

        CB_Key.SelectionChanged += (_, _) => LoadBlock();
        CB_Key.SelectedIndex = 0;
        LoadBlock();

        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
    }

    private void LoadBlock()
    {
        if (CB_Key.SelectedItem is not string name)
            return;
        PG_BlockView.SetObject(Metadata.GetBlock(name));
    }
}
