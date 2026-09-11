using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Views.EntityEditors;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen1;

/// <summary>
/// Hall of Fame editor for Generation 1 saves (port of the WinForms <c>SAV_HallOfFame1</c>).
/// </summary>
/// <remarks>
/// The block keeps the last teams that beat the Elite Four. Each team holds up to six members with only a
/// species, level and nickname; the list on the left shows how many members each stored team has.
/// </remarks>
public sealed class HallOfFame1Window : SaveEditorWindow
{
    private readonly SAV1 Origin;
    private readonly SAV1 SAV;
    private readonly HallOfFameReader1 Fame;

    private readonly ListBox LB_DataEntry = new() { Name = "LB_DataEntry", Width = 130, Height = 320 };
    private readonly ObservableCollection<string> Teams = [];
    private readonly TextBox RTB_Team = new() { Name = "RTB_Team", Width = 260, Height = 200, AcceptsReturn = true, IsReadOnly = true };

    private readonly TextBlock L_PartyNum = UiFactory.Label("L_PartyNum", "Party Index:");
    private readonly NumericUpDown NUP_PartyIndex = UiFactory.NumericUpDown("NUP_PartyIndex", 1, 6, 90);
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);
    private readonly NumericUpDown NUD_Level = UiFactory.NumericUpDown("NUD_Level", 0, byte.MaxValue, 90);
    private readonly CheckBox CHK_Nicknamed = UiFactory.Check("CHK_Nicknamed", "Nickname:");
    private readonly TextBox TB_Nickname = UiFactory.Text("TB_Nickname", 10, 160);
    private readonly Image PB_Sprite = UiFactory.Picture("PB_Sprite", 56);

    private readonly Button B_Delete = UiFactory.Button("B_Delete", "Clear Team");
    private readonly Button B_ClearSlot = UiFactory.Button("B_ClearSlot", "Clear Slot");
    private readonly Button B_SetParty = UiFactory.Button("B_SetParty", "Add Current Party");
    private readonly Button B_ClearAll = UiFactory.Button("B_ClearAll", "Clear All");
    private readonly TextBlock L_Clears = UiFactory.Label("L_Clears", "Teams:");
    private readonly NumericUpDown NUD_Clears = UiFactory.NumericUpDown("NUD_Clears", 0, byte.MaxValue, 90);

    private int Team = -1;
    private int Slot = -1;
    private bool LoadingFields;

    public HallOfFame1Window(SAV1 sav) : base("SAV_HallOfFame1", "Hall of Fame")
    {
        SAV = (SAV1)(Origin = sav).Clone();
        Fame = SAV.HallOfFame;

        B_Save.Content = "Save"; // the WinForms form names this button B_Close
        BuildLayout();
        Setup();

        LB_DataEntry.SelectedIndex = 0;
        UpdateTeamPreview(0);
        NUD_Clears.Value = SAV.HallOfFameCount;
    }

    private void BuildLayout()
    {
        LB_DataEntry.ItemsSource = Teams;
        TB_Nickname.MaxLength = 10;

        var entry = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(entry, 0, L_PartyNum, NUP_PartyIndex);
        UiFactory.AddFormRow(entry, 1, UiFactory.Label("Label_Species", "Species:"), UiFactory.Row(CB_Species, PB_Sprite));
        UiFactory.AddFormRow(entry, 2, UiFactory.Label("L_Level", "Level:"), NUD_Level);
        UiFactory.AddFormRow(entry, 3, CHK_Nicknamed, TB_Nickname);
        var GB_Entry = new GroupBoxView("GB_Entry", "Entry", UiFactory.Column(entry, UiFactory.Row(B_ClearSlot, B_Delete)));

        var right = UiFactory.Column(
            RTB_Team,
            GB_Entry,
            UiFactory.Row(B_SetParty, B_ClearAll),
            UiFactory.Row(L_Clears, NUD_Clears));

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        body.Children.Add(LB_DataEntry);
        body.Children.Add(right);
        SetBody(body);

        LB_DataEntry.SelectionChanged += (_, _) => DisplayEntry();
        NUP_PartyIndex.ValueChanged += (_, _) => DisplayEntry();
        CB_Species.SelectionChanged += (_, _) => ChangeSpecies();
        NUD_Level.ValueChanged += (_, _) => ChangeLevel();
        CHK_Nicknamed.IsCheckedChanged += (_, _) => UpdateNickname();
        TB_Nickname.OnTextChanged(_ => ChangeNickname());
        TB_Nickname.AttachClick(async mods =>
        {
            if (mods == KeyModifiers.Control)
                await ShowTrashEditor();
        });
        B_Delete.Click += (_, _) => ClickDeleteTeam();
        B_ClearSlot.Click += (_, _) => ClickClearSlot();
        B_SetParty.Click += (_, _) => ClickSetParty();
        B_ClearAll.Click += (_, _) => ClickClearAll();
    }

    private void Setup()
    {
        TB_Nickname.MaxLength = SAV.Japanese ? 5 : 10;
        CB_Species.SetItems(GameInfo.FilteredSources.Species);
        Teams.Clear();
        for (int i = 0; i < HallOfFameReader1.TeamCount; i++)
            Teams.Add(GetTeamIndication(i));
    }

    private void ResetListBox()
    {
        for (int i = 0; i < HallOfFameReader1.TeamCount; i++)
            ResetListBox(i);
    }

    private void ResetListBox(int team)
    {
        if ((uint)team < Teams.Count)
            Teams[team] = GetTeamIndication(team);
    }

    private string GetTeamIndication(int team) => $"{team + 1:00} ({GetTeamState(team)})";

    private string GetTeamState(int i) => Fame.GetTeamMemberCount(i) switch
    {
        0 => "✕",
        6 => "✓",
        var count => $"{count}/6",
    };

    #region Entry handling

    private void UpdateTeamPreview() => UpdateTeamPreview(Team);

    private void UpdateTeamPreview(int team)
    {
        if (team < 0)
            return;
        bool loading = LoadingFields;
        LoadingFields = true;
        ResetListBox(team);
        RTB_Team.Text = Fame.GetTeamSummary(team, GameInfo.Strings.specieslist);
        LoadingFields = loading;
    }

    private void DisplayEntry()
    {
        if (LoadingFields)
            return;
        int team = LB_DataEntry.SelectedIndex;
        if (team < 0)
            return;
        var slot = (int)(NUP_PartyIndex.Value ?? 1) - 1;

        SaveEntity();
        LoadEntity(Fame.GetEntity(team, slot));

        B_Delete.IsEnabled = team > 0;
        B_ClearSlot.IsEnabled = slot > 0;
        Team = team;
        Slot = slot;
        UpdateTeamPreview();
    }

    private void SaveEntity()
    {
        if (Team < 0 || Slot < 0)
            return;
        var pk = Fame.GetEntity(Team, Slot);
        SaveEntity(pk);
        UpdateTeamPreview(Team);
    }

    private void LoadEntity(HallOfFameEntity1 pk)
    {
        LoadingFields = true;
        CB_Species.SetValue(pk.Species);
        NUD_Level.Value = pk.Level;
        var nick = pk.Nickname;
        TB_Nickname.Text = nick;
        CHK_Nicknamed.IsChecked = IsNicknamed(pk.Species, nick);
        TB_Nickname.IsReadOnly = CHK_Nicknamed.IsChecked != true;
        SetSprite(pk.Species);
        LoadingFields = false;
    }

    private void SaveEntity(HallOfFameEntity1 pk)
    {
        var species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
        if (species is 0 or > 151)
        {
            pk.Clear();
            return;
        }
        pk.Species = species;
        pk.Level = (byte)(NUD_Level.Value ?? 1);
        if (pk.Nickname != TB_Nickname.Text) // preserve trash bytes
            pk.Nickname = TB_Nickname.Text ?? string.Empty;
    }

    private void SetSprite(ushort species)
    {
        var sprite = SpriteUtil.GetSprite(species, 0, 0, 0, 0, false, 0, EntityContext.Gen1);
        PB_Sprite.Source = sprite.ToAvaloniaBitmapAndDispose();
    }

    private bool IsNicknamed(ushort species, string nickname)
        => nickname != SpeciesName.GetSpeciesNameGeneration(species, SAV.Language, 1);

    private void ChangeSpecies()
    {
        if (LoadingFields || Team < 0)
            return;
        SaveEntity();
        var pk = Fame.GetEntity(Team, Slot);
        if (CHK_Nicknamed.IsChecked != true)
            TB_Nickname.Text = SpeciesName.GetSpeciesNameGeneration(pk.Species, SAV.Language, 1);
        SetSprite(pk.Species);
    }

    private void ChangeLevel()
    {
        if (LoadingFields || Team < 0)
            return;
        SaveEntity();
    }

    private void ChangeNickname()
    {
        if (LoadingFields || Team < 0)
            return;
        SaveEntity();
        var pk = Fame.GetEntity(Team, Slot);
        CHK_Nicknamed.IsChecked = IsNicknamed(pk.Species, TB_Nickname.Text ?? string.Empty);
        TB_Nickname.IsReadOnly = CHK_Nicknamed.IsChecked != true;
    }

    private void UpdateNickname()
    {
        if (LoadingFields)
            return;
        if (CHK_Nicknamed.IsChecked != true && Team >= 0 && Slot >= 0)
        {
            var species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
            bool isNone = species is 0 or > (int)Species.Mew;
            var pk = Fame.GetEntity(Team, Slot);
            var name = isNone ? string.Empty : SpeciesName.GetSpeciesNameGeneration(species, SAV.Language, 1);
            TB_Nickname.Text = name;
            if (pk.Nickname != name) // preserve trash bytes
                pk.Nickname = name;
        }
        TB_Nickname.IsReadOnly = CHK_Nicknamed.IsChecked != true;
    }

    private async Task ShowTrashEditor()
    {
        if (Team < 0 || Slot < 0)
            return;
        var pk = Fame.GetEntity(Team, Slot);
        if (TB_Nickname.Text != pk.Nickname) // preserve trash bytes
            pk.Nickname = TB_Nickname.Text ?? string.Empty;
        await TrashEditorWindow.ShowAsync(this, TB_Nickname, SAV, pk.NicknameTrash.ToArray());
    }

    #endregion

    #region Commands

    private void ClickDeleteTeam()
    {
        var index = Team;
        LoadingFields = true;
        Fame.Delete(index);
        LoadEntity(Fame.GetEntity(Team, Slot));
        ResetListBox();
        UpdateTeamPreview(index);
        LoadingFields = false;
    }

    private void ClickClearSlot()
    {
        var entity = Fame.GetEntity(LB_DataEntry.SelectedIndex, (int)(NUP_PartyIndex.Value ?? 1) - 1);
        entity.Clear();
        LoadEntity(entity);
        UpdateTeamPreview();
    }

    private void ClickSetParty()
    {
        LoadingFields = true;
        var count = Fame.RegisterParty(SAV, SAV.HallOfFameCount);
        ResetListBox();
        NUD_Clears.Value = SAV.HallOfFameCount = count;
        Team = -1;
        LoadingFields = false;

        var index = count - 1;
        ResetListBox(index);
        LB_DataEntry.SelectedIndex = index;
    }

    private void ClickClearAll()
    {
        Fame.Clear();
        NUD_Clears.Value = 0;
        SaveAndClose(entity: false);
    }

    #endregion

    protected override void OnSave() => SaveAndClose();

    private void SaveAndClose(bool entity = true)
    {
        if (entity)
            SaveEntity();
        SAV.HallOfFameCount = (byte)(NUD_Clears.Value ?? 0);
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
