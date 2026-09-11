using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views.EntityEditors;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen6;

/// <summary>
/// Hall of Fame editor for Generation 6 saves (port of the WinForms <c>SAV_HallOfFame</c>).
/// </summary>
/// <remarks>
/// Each of the stored clears keeps a date and up to six party members with their species, moves, held item,
/// nickname and original trainer. Every field writes back immediately, exactly like the WinForms form.
/// </remarks>
public sealed class HallOfFame6Window : SaveEditorWindow
{
    private readonly SAV6 Origin;
    private readonly SAV6 SAV;
    private readonly HallOfFame6 Fame;
    private readonly IReadOnlyList<string> gendersymbols = GameInfo.GenderSymbolUnicode;

    private bool editing;

    private readonly ListBox LB_DataEntry = new() { Name = "LB_DataEntry", Width = 90, Height = 380 };
    private readonly ObservableCollection<string> Entries = [];
    private readonly TextBox RTB = new() { Name = "RTB", Width = 300, Height = 380, AcceptsReturn = true, IsReadOnly = true, FontFamily = new global::Avalonia.Media.FontFamily("monospace") };
    private readonly Button B_CopyText = UiFactory.Button("B_CopyText", "Copy Text");
    private readonly Button B_Delete = UiFactory.Button("B_Delete", "Delete");

    private readonly NumericUpDown NUP_PartyIndex = UiFactory.NumericUpDown("NUP_PartyIndex", 1, 6, 90);
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 170);
    private readonly ComboBox CB_HeldItem = UiFactory.Combo("CB_HeldItem", 170);
    private readonly ComboBox[] CB_Moves =
    [
        UiFactory.Combo("CB_Move1", 170), UiFactory.Combo("CB_Move2", 170),
        UiFactory.Combo("CB_Move3", 170), UiFactory.Combo("CB_Move4", 170),
    ];
    private readonly ComboBox CB_Form = UiFactory.StringCombo("CB_Form", 150);
    private readonly NumericTextBox TB_EC = UiFactory.Numeric("TB_EC", 8, 90, hex: true);
    private readonly NumericTextBox TB_TID = UiFactory.Numeric("TB_TID", 5, 70);
    private readonly NumericTextBox TB_SID = UiFactory.Numeric("TB_SID", 5, 70);
    private readonly NumericTextBox TB_Level = UiFactory.Numeric("TB_Level", 3, 60);
    private readonly NumericTextBox TB_VN = UiFactory.Numeric("TB_VN", 3, 60);
    private readonly TextBox TB_Nickname = UiFactory.Text("TB_Nickname", 12, 160);
    private readonly TextBox TB_OT = UiFactory.Text("TB_OT", 12, 160);
    private readonly CheckBox CHK_Shiny = UiFactory.Check("CHK_Shiny", "Shiny");
    private readonly CheckBox CHK_Nicknamed = UiFactory.Check("CHK_Nicknamed", "Nicknamed");
    private readonly TextBlock Label_Gender = UiFactory.Label("Label_Gender", "-", clickable: true);
    private readonly TextBlock Label_OTGender = UiFactory.Label("Label_OTGender", "-", clickable: true);
    private readonly DatePicker CAL_MetDate = new() { Name = "CAL_MetDate" };
    private readonly Image bpkx = UiFactory.Picture("bpkx", 68);
    private GroupBoxView groupBox1 = null!;

    public HallOfFame6Window(SAV6 sav) : base("SAV_HallOfFame", "Hall of Fame")
    {
        SAV = (SAV6)(Origin = sav).Clone();
        Fame = ((ISaveBlock6Main)SAV).HallOfFame;

        BuildLayout();
        Setup();

        LB_DataEntry.SelectedIndex = 0;
        ChangePartyIndex(initial: true);
        editing = true;
    }

    private void BuildLayout()
    {
        LB_DataEntry.ItemsSource = Entries;
        Entries.Add("First"); // first clear, then the 15 most recent entries
        for (int i = 1; i < HallOfFame6.Entries; i++)
            Entries.Add($"{i:00}");

        var detail = UiFactory.FormGrid(11);
        UiFactory.AddFormRow(detail, 0, UiFactory.Label("L_PartyIndex", "Party Index:"), NUP_PartyIndex);
        UiFactory.AddFormRow(detail, 1, UiFactory.Label("L_Species", "Species:"), UiFactory.Row(CB_Species, Label_Gender, bpkx));
        UiFactory.AddFormRow(detail, 2, UiFactory.Label("L_Form", "Form:"), CB_Form);
        UiFactory.AddFormRow(detail, 3, UiFactory.Label("L_HeldItem", "Held Item:"), CB_HeldItem);
        UiFactory.AddFormRow(detail, 4, UiFactory.Label("L_Moves", "Moves:"), UiFactory.Column(CB_Moves));
        UiFactory.AddFormRow(detail, 5, UiFactory.Label("L_Level", "Level:"), UiFactory.Row(TB_Level, CHK_Shiny));
        UiFactory.AddFormRow(detail, 6, CHK_Nicknamed, TB_Nickname);
        UiFactory.AddFormRow(detail, 7, UiFactory.Label("L_OT", "OT:"), UiFactory.Row(TB_OT, Label_OTGender));
        UiFactory.AddFormRow(detail, 8, UiFactory.Label("L_TrainerID", "TID/SID:"), UiFactory.Row(TB_TID, TB_SID));
        UiFactory.AddFormRow(detail, 9, UiFactory.Label("L_EC", "EC:"), TB_EC);
        UiFactory.AddFormRow(detail, 10, UiFactory.Label("L_MetDate", "Date:"), UiFactory.Row(CAL_MetDate, TB_VN));
        groupBox1 = new GroupBoxView("groupBox1", "Entry", detail);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(UiFactory.Column(LB_DataEntry, B_Delete));
        body.Children.Add(UiFactory.Column(RTB, B_CopyText));
        body.Children.Add(new ScrollViewer { Content = groupBox1, MaxHeight = 560 });
        SetBody(body);

        LB_DataEntry.SelectionChanged += (_, _) => DisplayEntry(refreshParty: true);
        NUP_PartyIndex.ValueChanged += (_, _) => ChangePartyIndex(initial: false);
        CB_Species.SelectionChanged += (_, _) => { SetForms(); UpdateNickname(); };
        CB_HeldItem.SelectionChanged += (_, _) => WriteEntry();
        foreach (var cb in CB_Moves)
            cb.SelectionChanged += (_, _) => WriteEntry();
        CB_Form.SelectionChanged += (_, _) => WriteEntry();
        CHK_Shiny.IsCheckedChanged += (_, _) => UpdateShiny();
        CHK_Nicknamed.IsCheckedChanged += (_, _) => UpdateNickname();
        foreach (var tb in new[] { TB_EC, TB_TID, TB_SID, TB_Level, TB_VN })
            tb.OnTextChanged(_ => WriteEntry());
        TB_Nickname.OnTextChanged(_ => WriteEntry());
        TB_OT.OnTextChanged(_ => WriteEntry());
        CAL_MetDate.SelectedDateChanged += (_, _) => WriteEntry();
        Label_Gender.AttachClick(_ => UpdateGender());
        Label_OTGender.AttachClick(_ => UpdateOTGender());
        TB_Nickname.AttachClick(async mods =>
        {
            if (mods == KeyModifiers.Control)
                await ShowTrashEditor();
        });
        B_CopyText.Click += async (_, _) => await ClipboardService.SetText(this, RTB.Text ?? string.Empty);
        B_Delete.Click += async (_, _) => await ClickDelete();
    }

    private void Setup()
    {
        var filtered = GameInfo.FilteredSources;
        CB_Species.SetItems(filtered.Species);
        CB_HeldItem.SetItems(filtered.Items);
        foreach (var cb in CB_Moves)
            cb.SetItems(filtered.Moves);
    }

    #region Display

    private void DisplayEntry(bool refreshParty)
    {
        editing = false;
        int index = LB_DataEntry.SelectedIndex;
        if (index < 0)
            return;

        var span = Fame.GetEntry(index);
        var vnd = new HallFame6Index(span[^4..]);
        TB_VN.Text = vnd.ClearIndex.ToString("000");

        var lines = new List<string> { $"Entry #{vnd.ClearIndex}" };
        if (!vnd.HasData)
        {
            lines.Add("No records in this slot.");
            groupBox1.IsEnabled = false;
            editing = false;
            ChangePartyIndex(initial: true);
        }
        else
        {
            groupBox1.IsEnabled = true;
            var count = AddEntries(span, lines, vnd);
            if (refreshParty)
            {
                NUP_PartyIndex.Maximum = count == 0 ? 1 : count;
                NUP_PartyIndex.Value = 1;
                ChangePartyIndex(initial: false);
            }
            else
            {
                editing = true;
            }
        }

        RTB.Text = string.Join(Environment.NewLine, lines);
    }

    private int AddEntries(Span<byte> data, List<string> s, HallFame6Index vnd)
    {
        var year = vnd.Year + 2000;
        var month = vnd.Month;
        var day = vnd.Day;

        s.Add($"Date: {year}/{month:00}/{day:00}");
        s.Add(string.Empty);
        CAL_MetDate.SelectedDate = UiFactory.ToOffset(new DateTime((int)year, (int)month, (int)day));

        int count = 0;
        for (int i = 0; i < 6; i++)
        {
            var slice = data[(i * HallFame6Entity.SIZE)..];
            var entry = new HallFame6Entity(slice, SAV.Language);
            if (entry.Species == 0)
                continue;
            count++;
            AddEntryDescription(s, entry);
        }
        return count;
    }

    private void AddEntryDescription(List<string> s, HallFame6Entity entry)
    {
        var str = GameInfo.Strings;
        s.Add($"Name: {entry.Nickname}");
        s.Add($" ({str.Species[entry.Species]} - {gendersymbols[(int)entry.Gender]})");
        s.Add($"Level: {entry.Level}");
        s.Add($"Shiny: {(entry.IsShiny ? "Yes" : "No")}");
        s.Add($"Held Item: {str.Item[entry.HeldItem]}");
        s.Add($"Move 1: {str.Move[entry.Move1]}");
        s.Add($"Move 2: {str.Move[entry.Move2]}");
        s.Add($"Move 3: {str.Move[entry.Move3]}");
        s.Add($"Move 4: {str.Move[entry.Move4]}");
        s.Add($"OT: {entry.OriginalTrainerName} ({gendersymbols[(int)entry.OriginalTrainerGender]}) ({entry.TID16}/{entry.SID16})");
        s.Add(string.Empty);
    }

    private void ChangePartyIndex(bool initial)
    {
        editing = false;
        int index = LB_DataEntry.SelectedIndex;
        if (index < 0)
            return;
        var member = (int)(NUP_PartyIndex.Value ?? 1) - 1;
        var slice = Fame.GetEntity(index, member);
        var entry = new HallFame6Entity(slice, SAV.Language);

        CB_Species.SetValue(entry.Species);
        CB_HeldItem.SetValue(entry.HeldItem);
        CB_Moves[0].SetValue(entry.Move1);
        CB_Moves[1].SetValue(entry.Move2);
        CB_Moves[2].SetValue(entry.Move3);
        CB_Moves[3].SetValue(entry.Move4);

        TB_EC.Text = entry.EncryptionConstant.ToString("X8");
        TB_TID.Text = entry.TID16.ToString("00000");
        TB_SID.Text = entry.SID16.ToString("00000");
        TB_Nickname.Text = entry.Nickname;
        TB_OT.Text = entry.OriginalTrainerName;
        CHK_Shiny.IsChecked = entry.IsShiny;
        TB_Level.Text = entry.Level.ToString("000");
        CHK_Nicknamed.IsChecked = entry.IsNicknamed;

        SetForms();
        CB_Form.SelectedIndex = Math.Min(entry.Form, Math.Max(0, CB_Form.ItemCount - 1));
        SetGenderLabel((byte)entry.Gender);
        Label_OTGender.Text = gendersymbols[(int)entry.OriginalTrainerGender];
        UpdateNickname();
        SetSprite(entry.Species, entry.Form, (byte)entry.Gender, entry.HeldItem, entry.IsShiny);
        editing = !initial || editing;
        editing = true;
    }

    private void SetSprite(ushort species, byte form, byte gender, int item, bool isShiny)
    {
        var shiny = isShiny ? Shiny.Always : Shiny.Never;
        var sprite = SpriteUtil.GetSprite(species, form, gender, 0, item, false, shiny, EntityContext.Gen6);
        bpkx.Source = sprite.ToAvaloniaBitmapAndDispose();
    }

    #endregion

    #region Write

    private void WriteEntry()
    {
        if (!editing)
            return;

        ValidateTextBoxes();

        int index = LB_DataEntry.SelectedIndex;
        int member = (int)(NUP_PartyIndex.Value ?? 1) - 1;
        if (index < 0)
            return;

        var slice = Fame.GetEntity(index, member);
        var entry = new HallFame6Entity(slice, SAV.Language)
        {
            Species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0),
            HeldItem = (ushort)(CB_HeldItem.GetSelectedItem()?.Value ?? 0),
            Move1 = (ushort)(CB_Moves[0].GetSelectedItem()?.Value ?? 0),
            Move2 = (ushort)(CB_Moves[1].GetSelectedItem()?.Value ?? 0),
            Move3 = (ushort)(CB_Moves[2].GetSelectedItem()?.Value ?? 0),
            Move4 = (ushort)(CB_Moves[3].GetSelectedItem()?.Value ?? 0),
            EncryptionConstant = Util.GetHexValue(TB_EC.Text ?? string.Empty),
            TID16 = (ushort)Util.ToUInt32(TB_TID.Text ?? string.Empty),
            SID16 = (ushort)Util.ToUInt32(TB_SID.Text ?? string.Empty),
            Form = (byte)Math.Max(0, CB_Form.SelectedIndex),
            Gender = (uint)EntityGender.GetFromString(Label_Gender.Text ?? string.Empty) & 0x3,
            Level = (ushort)Util.ToUInt32(TB_Level.Text ?? string.Empty),
            IsShiny = CHK_Shiny.IsChecked == true,
            IsNicknamed = CHK_Nicknamed.IsChecked == true,
            Nickname = TB_Nickname.Text ?? string.Empty,
            OriginalTrainerName = TB_OT.Text ?? string.Empty,
            OriginalTrainerGender = (uint)EntityGender.GetFromString(Label_OTGender.Text ?? string.Empty) & 1,
        };

        var date = CAL_MetDate.SelectedDate?.Date ?? new DateTime(2000, 1, 1);
        var span = Fame.GetEntry(index);
        _ = new HallFame6Index(span[^4..])
        {
            ClearIndex = (ushort)Util.ToUInt32(TB_VN.Text ?? string.Empty),
            Year = (uint)date.Year - 2000,
            Month = (uint)date.Month,
            Day = (uint)date.Day,
            HasData = true,
        };

        SetSprite(entry.Species, entry.Form, (byte)entry.Gender, entry.HeldItem, entry.IsShiny);
        DisplayEntry(refreshParty: false);
    }

    private void ValidateTextBoxes()
    {
        TB_Level.Text = Math.Min(Util.ToInt32(TB_Level.Text ?? string.Empty), 100).ToString("000");
        TB_VN.Text = Math.Min(Util.ToInt32(TB_VN.Text ?? string.Empty), byte.MaxValue).ToString("000");
        TB_TID.Text = Math.Min(Util.ToInt32(TB_TID.Text ?? string.Empty), ushort.MaxValue).ToString("00000");
        TB_SID.Text = Math.Min(Util.ToInt32(TB_SID.Text ?? string.Empty), ushort.MaxValue).ToString("00000");
    }

    private void UpdateNickname()
    {
        if (CHK_Nicknamed.IsChecked != true)
        {
            var species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
            bool isNone = species is 0 or > (int)Species.Volcanion;
            TB_Nickname.Text = isNone ? string.Empty : SpeciesName.GetSpeciesNameGeneration(species, SAV.Language, 6);
        }
        TB_Nickname.IsReadOnly = CHK_Nicknamed.IsChecked != true;
        WriteEntry();
    }

    private void SetForms()
    {
        var species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
        var pi = PersonalTable.AO[species];
        bool hasForms = FormInfo.HasFormSelection(pi, species, 6);
        CB_Form.IsEnabled = CB_Form.IsVisible = hasForms;

        CB_Form.Items.Clear();
        foreach (var f in FormConverter.GetFormList(species, GameInfo.Strings.types, GameInfo.Strings.forms, gendersymbols, SAV.Context))
            CB_Form.Items.Add(f);
    }

    private void UpdateShiny()
    {
        if (!editing)
            return;
        var species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
        var form = (byte)(Math.Max(0, CB_Form.SelectedIndex) & 0x1F);
        var gender = EntityGender.GetFromString(Label_Gender.Text ?? string.Empty);
        var item = CB_HeldItem.GetSelectedItem()?.Value ?? 0;
        SetSprite(species, form, gender, item, CHK_Shiny.IsChecked == true);
        WriteEntry();
    }

    private void UpdateOTGender()
    {
        var g = EntityGender.GetFromString(Label_OTGender.Text ?? string.Empty);
        Label_OTGender.Text = gendersymbols[g ^ 1];
        WriteEntry();
    }

    private void UpdateGender()
    {
        var species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
        var pi = SAV.Personal[species];
        if (!pi.IsDualGender)
        {
            Label_Gender.Text = gendersymbols[pi.FixedGender()];
            return;
        }

        var fg = EntityGender.GetFromString(Label_Gender.Text ?? string.Empty);
        fg ^= 1;
        fg &= 1;
        Label_Gender.Text = gendersymbols[fg];

        var g = EntityGender.GetFromString(CB_Form.SelectedItem as string ?? string.Empty);
        if (g == 0 && Label_Gender.Text != gendersymbols[0])
            CB_Form.SelectedIndex = 1;
        else if (g == 1 && Label_Gender.Text != gendersymbols[1])
            CB_Form.SelectedIndex = 0;

        if (species == (int)Species.Pyroar)
            CB_Form.SelectedIndex = EntityGender.GetFromString(Label_Gender.Text ?? string.Empty);

        WriteEntry();
    }

    private void SetGenderLabel(byte gender)
    {
        Label_Gender.Text = gender switch
        {
            0 => gendersymbols[0],
            1 => gendersymbols[1],
            _ => gendersymbols[2],
        };
        WriteEntry();
    }

    private async Task ShowTrashEditor()
    {
        var team = LB_DataEntry.SelectedIndex;
        var member = (int)(NUP_PartyIndex.Value ?? 1) - 1;
        if (team < 0)
            return;
        var data = Fame.GetEntity(team, member);
        var nickTrash = data.Slice(0x18, 26);
        SAV.SetString(nickTrash, TB_Nickname.Text ?? string.Empty, 12, StringConverterOption.None);
        await TrashEditorWindow.ShowAsync(this, TB_Nickname, SAV, nickTrash.ToArray());
    }

    private async Task ClickDelete()
    {
        int index = LB_DataEntry.SelectedIndex;
        if (index < 1)
        {
            await AppDialogs.Alert(this, "Cannot delete your first Hall of Fame Clear entry.");
            return;
        }
        if (await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, $"Delete Entry {index} from your records?") != DialogResult.Yes)
            return;

        Fame.ClearEntry(index);
        DisplayEntry(refreshParty: true);
    }

    #endregion

    protected override void OnSave()
    {
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
