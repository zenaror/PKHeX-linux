using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen6;

/// <summary>
/// Pokémon Link gift editor for Generation 6 saves (port of the WinForms <c>SAV_Link6</c>).
/// </summary>
/// <remarks>
/// The Link block holds the items, Battle Points, PokéMiles and up to six Pokémon that the Pokémon Link
/// service delivered. The Pokémon themselves are shown read-only; only the item and currency rows are editable.
/// </remarks>
public sealed class Link6Window : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly ISaveBlock6Main SAV;
    private readonly PL6 Gifts;

    private readonly TextBox RTB_LinkSource = new() { Name = "RTB_LinkSource", Width = 320, Height = 60, AcceptsReturn = true };
    private readonly CheckBox CHK_LinkAvailable = UiFactory.Check("CHK_LinkAvailable", "Pokémon Link Enabled");
    private readonly NumericUpDown NUD_BP = UiFactory.NumericUpDown("NUD_BP", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_Pokemiles = UiFactory.NumericUpDown("NUD_Pokemiles", 0, ushort.MaxValue, 110);
    private readonly ComboBox[] CB_Items = new ComboBox[6];
    private readonly NumericUpDown[] NUD_Items = new NumericUpDown[6];
    private readonly TextBox[] TB_PKM = new TextBox[6];
    private readonly Button B_Import = UiFactory.Button("B_Import", "Import");
    private readonly Button B_Export = UiFactory.Button("B_Export", "Export");

    public Link6Window(SaveFile sav) : base("SAV_Link6", "Pokémon Link")
    {
        SAV = (ISaveBlock6Main)(Origin = sav).Clone();
        Gifts = SAV.Link.Gifts;

        var filtered = GameInfo.FilteredSources;
        for (int i = 0; i < 6; i++)
        {
            CB_Items[i] = UiFactory.Combo($"CB_Item{i + 1}", 200);
            CB_Items[i].SetItems(filtered.Items);
            NUD_Items[i] = UiFactory.NumericUpDown($"NUD_Item{i + 1}", 0, byte.MaxValue, 90);
            TB_PKM[i] = UiFactory.Text($"TB_PKM{i + 1}", 30, 180);
            TB_PKM[i].IsReadOnly = true;
        }

        var main = UiFactory.FormGrid(3);
        UiFactory.AddFormRow(main, 0, null, CHK_LinkAvailable);
        UiFactory.AddFormRow(main, 1, UiFactory.Label("L_BP", "Battle Points:"), NUD_BP);
        UiFactory.AddFormRow(main, 2, UiFactory.Label("L_Pokemiles", "PokéMiles:"), NUD_Pokemiles);
        var TAB_Main = new TabItem { Name = "TAB_Main", Header = "Main", Content = UiFactory.Column(RTB_LinkSource, main) };

        var pkmGrid = UiFactory.FormGrid(6);
        for (int i = 0; i < 6; i++)
            UiFactory.AddFormRow(pkmGrid, i, UiFactory.Label($"L_PKM{i + 1}", $"#{i + 1}:"), TB_PKM[i]);
        var TAB_PKM = new TabItem { Name = "TAB_PKM", Header = "Pokémon", Content = pkmGrid };

        var itemGrid = UiFactory.FormGrid(6);
        for (int i = 0; i < 6; i++)
            UiFactory.AddFormRow(itemGrid, i, UiFactory.Label($"L_Item{i + 1}", $"Item {i + 1}:"), UiFactory.Row(CB_Items[i], NUD_Items[i]));
        var TAB_Items = new TabItem { Name = "TAB_Items", Header = "Items", Content = itemGrid };

        var tabs = new TabControl();
        tabs.Items.Add(TAB_Main);
        tabs.Items.Add(TAB_PKM);
        tabs.Items.Add(TAB_Items);

        ButtonBar.Children.Insert(0, B_Import);
        ButtonBar.Children.Insert(1, B_Export);
        SetBody(tabs);

        B_Import.Click += async (_, _) => await ClickImport();
        B_Export.Click += async (_, _) => await ClickExport();

        LoadLinkData();
    }

    private void LoadLinkData()
    {
        RTB_LinkSource.Text = Gifts.Origin;
        NUD_BP.Value = Gifts.BattlePoints;
        NUD_Pokemiles.Value = Gifts.Pokemiles;

        CB_Items[0].SetValue(Gifts.Item1);
        CB_Items[1].SetValue(Gifts.Item2);
        CB_Items[2].SetValue(Gifts.Item3);
        CB_Items[3].SetValue(Gifts.Item4);
        CB_Items[4].SetValue(Gifts.Item5);
        CB_Items[5].SetValue(Gifts.Item6);

        NUD_Items[0].Value = Gifts.Quantity1;
        NUD_Items[1].Value = Gifts.Quantity2;
        NUD_Items[2].Value = Gifts.Quantity3;
        NUD_Items[3].Value = Gifts.Quantity4;
        NUD_Items[4].Value = Gifts.Quantity5;
        NUD_Items[5].Value = Gifts.Quantity6;

        TB_PKM[0].Text = GetSpecies(Gifts.Entity1.Species);
        TB_PKM[1].Text = GetSpecies(Gifts.Entity2.Species);
        TB_PKM[2].Text = GetSpecies(Gifts.Entity3.Species);
        TB_PKM[3].Text = GetSpecies(Gifts.Entity4.Species);
        TB_PKM[4].Text = GetSpecies(Gifts.Entity5.Species);
        TB_PKM[5].Text = GetSpecies(Gifts.Entity6.Species);

        if (Gifts.Enabled)
            NUD_BP.IsEnabled = NUD_Pokemiles.IsEnabled = B_Export.IsEnabled = true;
        CHK_LinkAvailable.IsChecked = Gifts.Enabled;
    }

    private static string GetSpecies(ushort species)
    {
        var arr = GameInfo.Strings.Species;
        return species < arr.Count ? arr[species] : species.ToString();
    }

    private void SaveLinkData()
    {
        Gifts.Origin = RTB_LinkSource.Text ?? string.Empty;
        Gifts.Enabled = CHK_LinkAvailable.IsChecked == true;
        Gifts.BattlePoints = (ushort)(NUD_BP.Value ?? 0);
        Gifts.Pokemiles = (ushort)(NUD_Pokemiles.Value ?? 0);

        Gifts.Item1 = ItemAt(0); Gifts.Item2 = ItemAt(1); Gifts.Item3 = ItemAt(2);
        Gifts.Item4 = ItemAt(3); Gifts.Item5 = ItemAt(4); Gifts.Item6 = ItemAt(5);

        Gifts.Quantity1 = QuantityAt(0); Gifts.Quantity2 = QuantityAt(1); Gifts.Quantity3 = QuantityAt(2);
        Gifts.Quantity4 = QuantityAt(3); Gifts.Quantity5 = QuantityAt(4); Gifts.Quantity6 = QuantityAt(5);
    }

    private ushort ItemAt(int index) => (ushort)(CB_Items[index].GetSelectedItem()?.Value ?? 0);
    private byte QuantityAt(int index) => (byte)(NUD_Items[index].Value ?? 0);

    private async Task ClickImport()
    {
        var path = await FileDialogs.OpenSingleFile(this, PL6.Filter);
        if (path is null)
            return;
        if (new FileInfo(path).Length != PL6.Size)
        {
            await AppDialogs.Alert(this, "Invalid file length");
            return;
        }
        var data = await File.ReadAllBytesAsync(path);
        data.CopyTo(Gifts.Data);
        LoadLinkData();
    }

    private async Task ClickExport()
    {
        var path = await FileDialogs.SaveFileDialog(this, PL6.Filter, "link.bin");
        if (path is null)
            return;
        await File.WriteAllBytesAsync(path, Gifts.Data.ToArray());
        await AppDialogs.Alert(this, "Pokémon Link data saved to:", path);
    }

    protected override void OnSave()
    {
        SaveLinkData();
        SAV.Link.RefreshChecksum();
        Origin.CopyChangesFrom((SaveFile)SAV);
        Close();
    }
}
