using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen6;

/// <summary>
/// Super Training editor for Generation 6 saves (port of the WinForms <c>SAV_SuperTrain</c>).
/// </summary>
/// <remarks>
/// Holds the record holders of each of the 32 training stages plus the twelve training bags in the bag case.
/// </remarks>
public sealed class SuperTrainWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV6 SAV;
    private readonly SuperTrainBlock STB;
    private readonly string[] trba;

    private readonly ListBox listBox1 = new() { Name = "listBox1", Width = 230, Height = 380 };
    private readonly ComboBox CB_Species1 = UiFactory.Combo("CB_Species1", 180);
    private readonly ComboBox CB_Species2 = UiFactory.Combo("CB_Species2", 180);
    private readonly NumericTextBox MTB_Gender1 = UiFactory.Numeric("MTB_Gender1", 1, 44);
    private readonly NumericTextBox MTB_Gender2 = UiFactory.Numeric("MTB_Gender2", 1, 44);
    private readonly NumericTextBox MTB_Form1 = UiFactory.Numeric("MTB_Form1", 2, 44);
    private readonly NumericTextBox MTB_Form2 = UiFactory.Numeric("MTB_Form2", 2, 44);
    private readonly TextBox TB_Time1 = UiFactory.Text("TB_Time1", 12, 90);
    private readonly TextBox TB_Time2 = UiFactory.Text("TB_Time2", 12, 90);
    private readonly DataGrid dataGridView1 = new() { Name = "dataGridView1", AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column, CanUserSortColumns = false, Height = 320, Width = 230 };
    private readonly ObservableCollection<BagRow> BagRows = [];

    private bool loading = true;

    public SuperTrainWindow(SAV6 sav) : base("SAV_SuperTrain", "Super Training")
    {
        SAV = (SAV6)(Origin = sav).Clone();
        trba = GameInfo.Strings.trainingbags;
        trba[0] = "---";
        STB = ((ISaveBlock6Main)SAV).SuperTrain;

        var stages = GameInfo.Strings.trainingstage;
        var items = new ObservableCollection<string>();
        for (int i = 0; i < 32; i++)
            items.Add($"{i + 1:00} - {stages[i]}");
        listBox1.ItemsSource = items;

        CB_Species1.SetItems(GameInfo.FilteredSources.Species);
        CB_Species2.SetItems(GameInfo.FilteredSources.Species);

        BuildLayout();
        FillTrainingBags();

        listBox1.SelectionChanged += (_, _) => ChangeListRecordSelection();
        CB_Species1.SelectionChanged += (_, _) => ChangeRecordSpecies(1);
        CB_Species2.SelectionChanged += (_, _) => ChangeRecordSpecies(2);
        MTB_Gender1.OnTextChanged(_ => ChangeRecordMisc(1));
        MTB_Form1.OnTextChanged(_ => ChangeRecordMisc(1));
        MTB_Gender2.OnTextChanged(_ => ChangeRecordMisc(2));
        MTB_Form2.OnTextChanged(_ => ChangeRecordMisc(2));
        TB_Time1.OnTextChanged(_ => ChangeRecordTime(1));
        TB_Time2.OnTextChanged(_ => ChangeRecordTime(2));

        listBox1.SelectedIndex = 0;
    }

    private void BuildLayout()
    {
        var holders = UiFactory.FormGrid(3);
        UiFactory.AddFormRow(holders, 0, UiFactory.Label("L_Species", "Species:"), UiFactory.Row(CB_Species1, MTB_Gender1, MTB_Form1));
        UiFactory.AddFormRow(holders, 1, UiFactory.Label("L_Species2", "Species:"), UiFactory.Row(CB_Species2, MTB_Gender2, MTB_Form2));
        UiFactory.AddFormRow(holders, 2, UiFactory.Label("L_Time0", "Time:"), UiFactory.Row(TB_Time1, TB_Time2));

        dataGridView1.Columns.Add(new DataGridTextColumn
        {
            Header = "Slot",
            Binding = new global::Avalonia.Data.Binding(nameof(BagRow.Slot)),
            IsReadOnly = true,
            Width = new DataGridLength(50),
        });
        dataGridView1.Columns.Add(DataGridUtil.StringComboColumn("Bag", [.. trba.Where(z => z.Length != 0)], nameof(BagRow.Bag), 160));
        dataGridView1.ItemsSource = BagRows;

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        body.Children.Add(UiFactory.Column(UiFactory.Label("L_Records", "Records"), listBox1));
        body.Children.Add(UiFactory.Column(holders));
        body.Children.Add(UiFactory.Column(UiFactory.Label("L_Bags", "Training Bags"), dataGridView1));
        SetBody(body);
    }

    private void FillTrainingBags()
    {
        BagRows.Clear();
        for (int i = 0; i < 12; i++)
            BagRows.Add(new BagRow { Slot = (i + 1).ToString(), Bag = trba[STB.GetBag(i)] });
    }

    private void ChangeListRecordSelection()
    {
        int index = listBox1.SelectedIndex;
        if (index < 0)
            return;

        loading = true;
        var holder1 = STB.GetHolder1(index);
        var holder2 = STB.GetHolder2(index);
        CB_Species1.SetValue(holder1.Species);
        MTB_Gender1.Text = holder1.Gender.ToString();
        MTB_Form1.Text = holder1.Form.ToString();
        CB_Species2.SetValue(holder2.Species);
        MTB_Gender2.Text = holder2.Gender.ToString();
        MTB_Form2.Text = holder2.Form.ToString();
        TB_Time1.Text = STB.GetTime1(index).ToString(CultureInfo.InvariantCulture);
        TB_Time2.Text = STB.GetTime2(index).ToString(CultureInfo.InvariantCulture);
        loading = false;
    }

    private void ChangeRecordSpecies(int slot)
    {
        int index = listBox1.SelectedIndex;
        if (index < 0 || loading)
            return;
        var holder = slot == 1 ? STB.GetHolder1(index) : STB.GetHolder2(index);
        var combo = slot == 1 ? CB_Species1 : CB_Species2;
        holder.Species = (ushort)(combo.GetSelectedItem()?.Value ?? 0);
    }

    private void ChangeRecordMisc(int slot)
    {
        int index = listBox1.SelectedIndex;
        if (index < 0 || loading)
            return;
        var holder = slot == 1 ? STB.GetHolder1(index) : STB.GetHolder2(index);
        var formBox = slot == 1 ? MTB_Form1 : MTB_Form2;
        var genderBox = slot == 1 ? MTB_Gender1 : MTB_Gender2;
        if (byte.TryParse(formBox.Text, out var form))
            holder.Form = form;
        if (byte.TryParse(genderBox.Text, out var gender))
            holder.Gender = gender;
    }

    private void ChangeRecordTime(int slot)
    {
        int index = listBox1.SelectedIndex;
        if (index < 0 || loading)
            return;
        var box = slot == 1 ? TB_Time1 : TB_Time2;
        if (!float.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return;
        if (slot == 1)
            STB.SetTime1(index, value);
        else
            STB.SetTime2(index, value);
    }

    protected override void OnSave()
    {
        // Copy the bags back, skipping empty slots so the filled ones stay contiguous.
        int emptySlots = 0;
        for (int i = 0; i < BagRows.Count; i++)
        {
            var index = Array.IndexOf(trba, BagRows[i].Bag);
            if (index <= 0)
            {
                emptySlots++;
                continue;
            }
            STB.SetBag(i - emptySlots, (byte)index);
        }

        Origin.CopyChangesFrom(SAV);
        Close();
    }

    private sealed class BagRow
    {
        public required string Slot { get; init; }
        public string Bag { get; set; } = string.Empty;
    }
}
