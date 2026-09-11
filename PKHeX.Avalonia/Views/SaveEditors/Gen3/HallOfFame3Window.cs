using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Views.EntityEditors;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen3;

/// <summary>
/// Hall of Fame editor for Generation 3 saves (port of the WinForms <c>SAV_HallOfFame3</c>).
/// </summary>
/// <remarks>
/// The Hall of Fame keeps 50 entries of up to six members each, stored as a trimmed-down entity that only
/// records the IDs, nickname, level and species.
/// </remarks>
public sealed class HallOfFame3Window : SaveEditorWindow
{
    private const int EntryCount = 50;
    private const int TeamSize = 6;

    private readonly SAV3 Origin;
    private readonly SAV3 SAV;
    private readonly HallFame3Entry[] Fame;

    private int prevEntry;
    private int prevMember;
    private bool Loading;

    private readonly ListBox LB_Entries = new() { Name = "LB_Entries", Width = 90, Height = 320 };
    private readonly ObservableCollection<string> EntryItems = [];
    private readonly NumericUpDown NUD_Members = UiFactory.NumericUpDown("NUD_Members", 0, 5, 100);
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);
    private readonly TextBox TB_TID = UiFactory.Text("TB_TID", 5, 90);
    private readonly TextBox TB_SID = UiFactory.Text("TB_SID", 5, 90);
    private readonly TextBox TB_PID = UiFactory.Text("TB_PID", 8, 100);
    private readonly TextBox TB_Nickname = UiFactory.Text("TB_Nickname", 10, 150);
    private readonly NumericUpDown NUD_Level = UiFactory.NumericUpDown("NUD_Level", 0, 255, 100);
    private readonly CheckBox CHK_Shiny = UiFactory.Check("CHK_Shiny", "Shiny");
    private readonly Image PB_Sprite = UiFactory.Picture("PB_Sprite", 68);
    private readonly Button B_Clear = UiFactory.Button("B_Clear", "Clear");
    private readonly Button B_ImportParty = UiFactory.Button("B_ImportParty", "Import All From Party");

    public HallOfFame3Window(SAV3 sav) : base("SAV_HallOfFame3", "Hall of Fame Editor")
    {
        SAV = (SAV3)(Origin = sav).Clone();
        Fame = HallFame3Entry.GetEntries(SAV);

        for (int i = 0; i < EntryCount; i++)
            EntryItems.Add(i.ToString());

        BuildLayout();

        CB_Species.SetItems(GameInfo.FilteredSources.Species.ToList());
        CHK_Shiny.IsEnabled = false; // computed from the IDs

        LB_Entries.SelectedIndex = 0;
        NUD_Members.Value = 0;
        LoadEntry(Fame[0].Team[0]);
        UpdateSprite();

        foreach (var tb in new[] { TB_TID, TB_SID, TB_PID })
            tb.LostFocus += (_, _) => ValidateIDs();
        TB_Nickname.AttachClick(ClickNickname);
        CB_Species.SelectionChanged += (_, _) => UpdateSprite();
        NUD_Members.ValueChanged += (_, _) => ChangeMember();
        LB_Entries.SelectionChanged += (_, _) => ChangeEntry();
        B_Clear.Click += (_, _) => ClearFields();
        B_ImportParty.AttachClickHandled(ClickImportParty);
    }

    private void BuildLayout()
    {
        LB_Entries.ItemsSource = EntryItems;

        var grid = UiFactory.FormGrid(6);
        UiFactory.AddFormRow(grid, 0, UiFactory.Label("L_Species", "Species:"), CB_Species);
        UiFactory.AddFormRow(grid, 1, UiFactory.Label("L_Level", "Level:"), NUD_Level);
        UiFactory.AddFormRow(grid, 2, UiFactory.Label("L_TID", "TID:"), TB_TID);
        UiFactory.AddFormRow(grid, 3, UiFactory.Label("L_SID", "SID:"), TB_SID);
        UiFactory.AddFormRow(grid, 4, UiFactory.Label("L_PID", "PID:"), UiFactory.Row(TB_PID, CHK_Shiny));
        UiFactory.AddFormRow(grid, 5, UiFactory.Label("L_Nickname", "Nickname:"), TB_Nickname);

        var right = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_Members", "Member:"), NUD_Members),
            UiFactory.Row(PB_Sprite, grid),
            UiFactory.Row(B_Clear, B_ImportParty));

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(LB_Entries);
        body.Children.Add(right);
        SetBody(body);
    }

    private HallFame3PKM Current => Fame[Math.Max(0, LB_Entries.SelectedIndex)].Team[(int)(NUD_Members.Value ?? 0)];

    private void ChangeMember()
    {
        SaveEntry(Fame[prevEntry].Team[prevMember]);
        LoadEntry(Current);
        prevMember = (int)(NUD_Members.Value ?? 0);
        prevEntry = Math.Max(0, LB_Entries.SelectedIndex);
        UpdateSprite();
    }

    private void ChangeEntry()
    {
        if (LB_Entries.SelectedIndex < 0)
            return;
        SaveEntry(Fame[prevEntry].Team[prevMember]);
        NUD_Members.Value = 0;
        LoadEntry(Fame[LB_Entries.SelectedIndex].Team[0]);
        prevMember = 0;
        prevEntry = LB_Entries.SelectedIndex;
        UpdateSprite();
    }

    private void ClearFields()
    {
        TB_TID.Text = TB_SID.Text = "0";
        TB_PID.Text = "0";
        TB_Nickname.Text = string.Empty;
        NUD_Level.Value = 0;
        CB_Species.SelectedIndex = 0;
    }

    private void LoadEntry(HallFame3PKM pk)
    {
        Loading = true;
        TB_TID.Text = pk.TID16.ToString("00000");
        TB_SID.Text = pk.SID16.ToString("00000");
        TB_PID.Text = pk.PID.ToString("X8");
        TB_Nickname.Text = pk.Nickname;
        NUD_Level.SetValueClamped(pk.Level);
        CB_Species.SetValue(pk.Species);
        Loading = false;
    }

    private void SaveEntry(HallFame3PKM pk)
    {
        pk.TID16 = (ushort)Math.Min(ushort.MaxValue, Util.ToUInt32(TB_TID.Text));
        pk.SID16 = (ushort)Math.Min(ushort.MaxValue, Util.ToUInt32(TB_SID.Text));
        pk.PID = Util.GetHexValue(TB_PID.Text);
        if (pk.Nickname != TB_Nickname.Text) // preserve trash
            pk.Nickname = TB_Nickname.Text ?? string.Empty;
        pk.Level = (int)(NUD_Level.Value ?? 0);
        pk.Species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
    }

    private void ValidateIDs()
    {
        if (Loading)
            return;
        var pid = Util.GetHexValue(TB_PID.Text);
        if (pid.ToString("X") != TB_PID.Text && pid.ToString("X8") != TB_PID.Text)
            TB_PID.Text = pid.ToString("X8");

        var tid = Math.Min(ushort.MaxValue, Util.ToUInt32(TB_TID.Text));
        if (tid.ToString() != TB_TID.Text)
            TB_TID.Text = tid.ToString();

        var sid = Math.Min(ushort.MaxValue, Util.ToUInt32(TB_SID.Text));
        if (sid.ToString() != TB_SID.Text)
            TB_SID.Text = sid.ToString();

        CHK_Shiny.IsChecked = ShinyUtil.GetIsShiny3((sid << 16) | tid, pid);
        UpdateSprite();
    }

    private void UpdateSprite()
    {
        if (Loading)
            return;

        var entry = Current;
        SaveEntry(entry);
        CHK_Shiny.IsChecked = entry.IsShiny;
        var shiny = entry.IsShiny ? Shiny.Always : Shiny.Never;
        var sprite = SpriteUtil.GetSprite(entry.Species, entry.DisplayForm(SAV.Version), 0, 0, 0, false, shiny, EntityContext.Gen3);
        PB_Sprite.Source = sprite.ToAvaloniaBitmapAndDispose();
    }

    private async void ClickNickname(KeyModifiers mods)
    {
        if (mods != KeyModifiers.Control) // Special Character Form
            return;

        var pk = Current;
        if (TB_Nickname.Text != pk.Nickname) // preserve trash
            pk.Nickname = TB_Nickname.Text ?? string.Empty;
        await TrashEditorWindow.ShowAsync(this, TB_Nickname, SAV, pk.NicknameTrash.ToArray());
    }

    /// <summary>Copies the current party into this entry; Shift applies it to every entry.</summary>
    private void ClickImportParty(KeyModifiers mods)
    {
        var party = new PK3[TeamSize];
        var partyCount = SAV.PartyCount;
        for (int i = 0; i < partyCount && i < party.Length; i++)
            party[i] = (PK3)SAV.GetPartySlotAtIndex(i);
        for (int i = partyCount; i < party.Length; i++)
            party[i] = new PK3(); // ensure data is clean

        var current = Fame[prevEntry];
        current.CopyFrom(party);
        LoadEntry(current.GetMember(prevMember)); // reload from updated data

        if (mods == KeyModifiers.Shift)
        {
            for (int i = 0; i < Fame.Length; i++)
            {
                if (i != prevEntry)
                    Fame[i].CopyFrom(party);
            }
        }

        UpdateSprite();
    }

    protected override void OnSave()
    {
        SaveEntry(Current);
        HallFame3Entry.SetEntries(SAV, Fame);
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
