using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Views.EntityEditors;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen3;

/// <summary>
/// Secret Base editor for Generation 3 Hoenn saves (port of the WinForms <c>SAV_SecretBase3</c>).
/// </summary>
/// <remarks>
/// Lists the secret bases received by record mixing; each holds a trainer header and a team of six
/// cut-down entities that only store species, PID, held item, four moves, level and a single EV value.
/// </remarks>
public sealed class SecretBase3Window : SaveEditorWindow
{
    private static readonly char[] UnownForms =
        ['A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z','!','?'];

    private readonly SaveFile Origin;
    private readonly SAV3 SAV;
    private readonly SecretBaseManager3 Manager;

    private readonly ListBox LB_Bases = new() { Name = "LB_Bases", Width = 160, Height = 340 };
    private readonly ObservableCollection<string> BaseItems = [];

    private readonly TextBox TB_Name = UiFactory.Text("TB_Name", 7, 140);
    private readonly GenderToggleView T_TrainerGender = new() { Name = "T_TrainerGender" };
    private readonly TextBox TB_TID = UiFactory.Text("TB_TID", 5, 90);
    private readonly TextBox TB_SID = UiFactory.Text("TB_SID", 5, 90);
    private readonly TextBox TB_Entered = UiFactory.Text("TB_Entered", 3, 90);
    private readonly TextBox TB_Class = UiFactory.Text("TB_Class", 20, 160);
    private readonly CheckBox CHK_Battled = UiFactory.Check("CHK_Battled", "Battled Today");
    private readonly CheckBox CHK_Registered = UiFactory.Check("CHK_Registered", "Registered");
    private readonly Button B_UpdateTrainer = UiFactory.Button("B_UpdateTrainer", "Update Trainer");

    private readonly NumericUpDown NUD_TeamMember = UiFactory.NumericUpDown("NUD_TeamMember", 1, 6, 100);
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);
    private readonly ComboBox CB_Form = UiFactory.StringCombo("CB_Form", 70);
    private readonly TextBox TB_PID = UiFactory.Text("TB_PID", 8, 110);
    private readonly ComboBox CB_Item = UiFactory.Combo("CB_Item", 180);
    private readonly ComboBox CB_Move1 = UiFactory.Combo("CB_Move1", 180);
    private readonly ComboBox CB_Move2 = UiFactory.Combo("CB_Move2", 180);
    private readonly ComboBox CB_Move3 = UiFactory.Combo("CB_Move3", 180);
    private readonly ComboBox CB_Move4 = UiFactory.Combo("CB_Move4", 180);
    private readonly NumericUpDown NUD_Level = UiFactory.NumericUpDown("NUD_Level", 0, 100, 100);
    private readonly NumericUpDown NUD_EVs = UiFactory.NumericUpDown("NUD_EVs", 0, 85, 100);
    private readonly Button B_UpdatePKM = UiFactory.Button("B_UpdatePKM", "Update PKM");

    private bool Loading;

    public SecretBase3Window(SAV3 sav) : base("SAV_SecretBase3", "Secret Base Editor")
    {
        SAV = (SAV3)(Origin = sav).Clone();
        Manager = ((ISaveBlock3LargeHoenn)SAV.LargeBlock).SecretBases;

        BuildLayout();

        var filtered = GameInfo.FilteredSources;
        var moves = filtered.Moves;
        foreach (var cb in new[] { CB_Move1, CB_Move2, CB_Move3, CB_Move4 })
            cb.SetItems(moves);
        CB_Item.SetItems(filtered.Items);
        CB_Species.SetItems(filtered.Species);
        CB_Form.ItemsSource = UnownForms.Select(z => z.ToString()).ToList();

        RefreshBaseList();
        if (Manager.Count > 0)
        {
            LB_Bases.SelectedIndex = 0;
            ShowTrainer();
        }
        else
        {
            B_Save.IsEnabled = false;
        }
    }

    private void BuildLayout()
    {
        LB_Bases.ItemsSource = BaseItems;
        TB_Class.IsReadOnly = true;

        var trainer = UiFactory.FormGrid(6);
        UiFactory.AddFormRow(trainer, 0, UiFactory.Label("L_Name", "Name:"), UiFactory.Row(TB_Name, T_TrainerGender));
        UiFactory.AddFormRow(trainer, 1, UiFactory.Label("L_TID", "TID:"), TB_TID);
        UiFactory.AddFormRow(trainer, 2, UiFactory.Label("L_SID", "SID:"), TB_SID);
        UiFactory.AddFormRow(trainer, 3, UiFactory.Label("L_Entered", "Number of entrances:"), TB_Entered);
        UiFactory.AddFormRow(trainer, 4, UiFactory.Label("L_Class", "Trainer class:"), TB_Class);
        UiFactory.AddFormRow(trainer, 5, null, UiFactory.Row(CHK_Battled, CHK_Registered));
        var gbTrainer = new GroupBoxView("GB_Trainer", "Trainer", UiFactory.Column(trainer, B_UpdateTrainer));

        var team = UiFactory.FormGrid(9);
        UiFactory.AddFormRow(team, 0, UiFactory.Label("L_Member", "Team Member:"), NUD_TeamMember);
        UiFactory.AddFormRow(team, 1, UiFactory.Label("L_Species", "Species:"), UiFactory.Row(CB_Species, CB_Form));
        UiFactory.AddFormRow(team, 2, UiFactory.Label("L_PID", "PID:"), TB_PID);
        UiFactory.AddFormRow(team, 3, UiFactory.Label("L_Item", "Item:"), CB_Item);
        UiFactory.AddFormRow(team, 4, UiFactory.Label("L_Move1", "Move 1:"), CB_Move1);
        UiFactory.AddFormRow(team, 5, UiFactory.Label("L_Move2", "Move 2:"), CB_Move2);
        UiFactory.AddFormRow(team, 6, UiFactory.Label("L_Move3", "Move 3:"), CB_Move3);
        UiFactory.AddFormRow(team, 7, UiFactory.Label("L_Move4", "Move 4:"), CB_Move4);
        UiFactory.AddFormRow(team, 8, UiFactory.Label("L_Level", "LV:"), UiFactory.Row(NUD_Level, UiFactory.Label("L_EVs", "EVs:"), NUD_EVs));
        var gbTeam = new GroupBoxView("GB_Team", "Team", UiFactory.Column(team, B_UpdatePKM));

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(UiFactory.Column(UiFactory.Label("L_Trainers", "Trainers:"), LB_Bases));
        body.Children.Add(gbTrainer);
        body.Children.Add(gbTeam);
        SetBody(new ScrollViewer { Content = body, MaxHeight = 620 });

        LB_Bases.SelectionChanged += (_, _) => { if (!Loading) ShowTrainer(); };
        NUD_TeamMember.ValueChanged += (_, _) => ChangeTeamMember();
        CB_Species.SelectionChanged += (_, _) => ChangeSpecies();
        B_UpdateTrainer.Click += (_, _) => UpdateTrainer();
        B_UpdatePKM.Click += (_, _) => UpdatePKM();
        TB_Name.AttachClick(ClickName);

        TB_TID.LostFocus += (_, _) => ClampText(TB_TID, ushort.MaxValue);
        TB_SID.LostFocus += (_, _) => ClampText(TB_SID, ushort.MaxValue);
        TB_Entered.LostFocus += (_, _) => ClampText(TB_Entered, byte.MaxValue);
        TB_PID.LostFocus += (_, _) =>
        {
            var text = TB_PID.Text ?? string.Empty;
            if (text.Length == 0)
                TB_PID.Text = "0";
            else if (!text.All(char.IsAsciiHexDigit))
                TB_PID.Text = uint.MaxValue.ToString("X8");
        };
    }

    private static void ClampText(TextBox tb, uint max)
    {
        var text = tb.Text ?? string.Empty;
        if (text.Length == 0)
        {
            tb.Text = "0";
            return;
        }
        var value = Util.ToUInt32(text);
        if (value > max)
            tb.Text = max.ToString();
    }

    private SecretBase3? Selected
    {
        get
        {
            var index = LB_Bases.SelectedIndex;
            return (uint)index < Manager.Bases.Count ? Manager.Bases[index] : null;
        }
    }

    private void RefreshBaseList()
    {
        Loading = true;
        var selected = LB_Bases.SelectedIndex;
        BaseItems.Clear();
        foreach (var b in Manager.Bases)
            BaseItems.Add(b.OriginalTrainerName);
        if ((uint)selected < BaseItems.Count)
            LB_Bases.SelectedIndex = selected;
        Loading = false;
    }

    private void ShowTrainer()
    {
        if (Selected is not { } secret)
            return;
        Loading = true;
        TB_Name.Text = secret.OriginalTrainerName;
        T_TrainerGender.Gender = secret.OriginalTrainerGender;
        TB_TID.Text = secret.TID16.ToString();
        TB_SID.Text = secret.SID16.ToString();
        TB_Entered.Text = secret.TimesEntered.ToString();
        TB_Class.Text = secret.OriginalTrainerClassName;
        CHK_Battled.IsChecked = secret.BattledToday;
        CHK_Registered.IsChecked = secret.RegistryStatus == 1;
        NUD_TeamMember.Value = 1;
        Loading = false;
        ShowPKM(secret.Team.Team[0]);
    }

    private void ChangeTeamMember()
    {
        if (Loading || Selected is not { } secret)
            return;
        ShowPKM(secret.Team.Team[(int)(NUD_TeamMember.Value ?? 1) - 1]);
    }

    private void ShowPKM(SecretBase3PKM pk)
    {
        Loading = true;
        CB_Species.SetValue(pk.Species);
        if (pk.Species == (int)Species.Unown)
        {
            CB_Form.SelectedIndex = Math.Clamp(pk.Form - 1, 0, UnownForms.Length - 1);
            CB_Form.IsVisible = true;
        }
        else
        {
            CB_Form.IsVisible = false;
        }
        TB_PID.Text = pk.PID.ToString("X8");
        CB_Item.SetValue(pk.HeldItem);
        CB_Move1.SetValue(pk.Move1);
        CB_Move2.SetValue(pk.Move2);
        CB_Move3.SetValue(pk.Move3);
        CB_Move4.SetValue(pk.Move4);
        NUD_Level.SetValueClamped(pk.Level);
        NUD_EVs.SetValueClamped(pk.EVAll);
        Loading = false;
        ChangeSpecies();
    }

    /// <summary>An empty slot disables everything else, as in WinForms.</summary>
    private void ChangeSpecies()
    {
        bool present = CB_Species.SelectedIndex > 0;
        TB_PID.IsEnabled = CB_Item.IsEnabled = NUD_Level.IsEnabled = NUD_EVs.IsEnabled = present;
        CB_Move1.IsEnabled = CB_Move2.IsEnabled = CB_Move3.IsEnabled = CB_Move4.IsEnabled = present;
        NUD_Level.Minimum = present ? 2 : 0;
        if (present || Loading)
            return;

        TB_PID.Text = "0";
        CB_Item.SelectedIndex = 0;
        CB_Move1.SelectedIndex = CB_Move2.SelectedIndex = CB_Move3.SelectedIndex = CB_Move4.SelectedIndex = 0;
        NUD_Level.Value = 0;
        NUD_EVs.Value = 0;
    }

    private void UpdateTrainer()
    {
        if (Selected is not { } secret)
            return;
        secret.OriginalTrainerName = TB_Name.Text ?? string.Empty;
        secret.OriginalTrainerGender = T_TrainerGender.Gender;
        secret.TID16 = (ushort)Math.Min(ushort.MaxValue, Util.ToUInt32(TB_TID.Text));
        secret.SID16 = (ushort)Math.Min(ushort.MaxValue, Util.ToUInt32(TB_SID.Text));
        secret.TimesEntered = (byte)Math.Min(byte.MaxValue, Util.ToUInt32(TB_Entered.Text));
        secret.BattledToday = CHK_Battled.IsChecked == true;
        secret.RegistryStatus = CHK_Registered.IsChecked == true ? 1 : 0;
        RefreshBaseList();
    }

    private void UpdatePKM()
    {
        if (Selected is not { } secret)
            return;
        var team = secret.Team;
        var pk = team.Team[(int)(NUD_TeamMember.Value ?? 1) - 1];
        pk.Species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
        pk.PID = Util.GetHexValue(TB_PID.Text);
        pk.HeldItem = (ushort)(CB_Item.GetSelectedItem()?.Value ?? 0);
        pk.Move1 = (ushort)(CB_Move1.GetSelectedItem()?.Value ?? 0);
        pk.Move2 = (ushort)(CB_Move2.GetSelectedItem()?.Value ?? 0);
        pk.Move3 = (ushort)(CB_Move3.GetSelectedItem()?.Value ?? 0);
        pk.Move4 = (ushort)(CB_Move4.GetSelectedItem()?.Value ?? 0);
        pk.Level = (byte)(NUD_Level.Value ?? 0);
        pk.EVAll = (byte)(NUD_EVs.Value ?? 0);
        secret.Team = team; // save changes
    }

    private async void ClickName(KeyModifiers mods)
    {
        if (mods != KeyModifiers.Control)
            return;
        if (Selected is not { } secret)
            return;

        // Secret bases store the trainer name in the sender's language, not the save's.
        var language = secret.Language;
        var converter = new CustomStringConverter
        {
            Context = EntityContext.Gen3,
            Generation = 3,
            Get = data => StringConverter3.GetString(data, language),
            Load = (data, result) => StringConverter3.LoadString(data, result, language),
            Set = (data, value, maxLength, option) => StringConverter3.SetString(data, value, maxLength, language, option),
        };
        await TrashEditorWindow.ShowAsync(this, TB_Name, converter, secret.OriginalTrainerTrash.ToArray());
    }

    protected override void OnSave()
    {
        Manager.Save();
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
