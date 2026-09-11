using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors;

/// <summary>
/// Mail editor for Generation 2 through 5 saves (port of the WinForms <c>SAV_MailBox</c>).
/// </summary>
/// <remarks>
/// Mail lives in two places: attached to a party member, and stored in the PC mailbox. Both lists are shown
/// side by side; the fields on the right belong to whichever entry is selected. Which fields apply depends on
/// the generation, so the unused ones are hidden the same way the WinForms editor hides them.
/// </remarks>
public sealed class MailBoxWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SaveFile SAV;
    private readonly MailDetail[] m;
    private readonly EntityContext Context;
    private readonly byte ResetVer, ResetLang;
    private readonly int PartyBoxCount;
    private readonly int[] MailItemID;
    private readonly IList<PKM> p;

    private bool editing;
    private int entry = -1;
    private string loadedLabel = string.Empty;

    // Lists
    private readonly TextBlock L_PartyHeld = UiFactory.Label("L_PartyHeld", "MailBox (Party)");
    private readonly TextBlock L_PCBOX = UiFactory.Label("L_PCBOX", "MailBox (PC)");
    private readonly ListBox LB_PartyHeld = new() { Name = "LB_PartyHeld", Width = 190, Height = 130 };
    private readonly ListBox LB_PCBOX = new() { Name = "LB_PCBOX", Width = 190, Height = 200 };
    private readonly ObservableCollection<string> PartyItems = [];
    private readonly ObservableCollection<string> BoxItems = [];
    private readonly Button B_PartyUp = UiFactory.Button("B_PartyUp", "^");
    private readonly Button B_PartyDown = UiFactory.Button("B_PartyDown", "v");
    private readonly Button B_BoxUp = UiFactory.Button("B_BoxUp", "^");
    private readonly Button B_BoxDown = UiFactory.Button("B_BoxDown", "v");
    private readonly Button B_Delete = UiFactory.Button("B_Delete", "Delete");
    private readonly TextBlock L_BoxSize = UiFactory.Label("L_BoxSize", "MailBox (PC) Served:");
    private readonly NumericUpDown NUD_BoxSize = UiFactory.NumericUpDown("NUD_BoxSize", 0, 10, 110);

    // Author
    private readonly TextBox TB_AuthorName = UiFactory.Text("TB_AuthorName", 10, 120);
    private readonly NumericUpDown NUD_AuthorTID = UiFactory.NumericUpDown("NUD_AuthorTID", 0, ushort.MaxValue, 90);
    private readonly NumericUpDown NUD_AuthorSID = UiFactory.NumericUpDown("NUD_AuthorSID", 0, ushort.MaxValue, 90);
    private readonly ComboBox CB_AuthorLang = UiFactory.Combo("CB_AuthorLang", 110);
    private readonly ComboBox CB_AuthorVersion = UiFactory.Combo("CB_AuthorVersion", 130);
    private readonly GenderToggleView GT_AuthorGender = new() { Name = "GT_AuthorGender" };

    // Mail body
    private readonly TextBlock L_MailType = UiFactory.Label("L_MailType", "Mail Type:");
    private readonly ComboBox CB_MailType = UiFactory.StringCombo("CB_MailType", 150);
    private readonly TextBlock L_AppearPKM = UiFactory.Label("L_AppearPKM", "Appear PKM:");
    private readonly ComboBox[] AppearPKMs =
    [
        UiFactory.Combo("CB_AppearPKM1", 140),
        UiFactory.Combo("CB_AppearPKM2", 140),
        UiFactory.Combo("CB_AppearPKM3", 140),
    ];
    private readonly TextBox TB_MessageBody21 = UiFactory.Text("TB_MessageBody21", 16, 220);
    private readonly TextBox TB_MessageBody22 = UiFactory.Text("TB_MessageBody22", 16, 220);
    private readonly CheckBox CHK_UserEntered = UiFactory.Check("CHK_UserEntered", "User-Entered");
    private readonly NumericUpDown[][] Messages;
    private readonly NumericUpDown NUD_MessageEnding = UiFactory.NumericUpDown("NUD_MessageEnding", 0, ushort.MaxValue, 90);
    private readonly TextBlock L_MiscValue = UiFactory.Label("L_MiscValue", "Misc:");
    private readonly NumericUpDown[] Miscs;

    // Party held mail ids
    private readonly TextBlock[] PKMLabels;
    private readonly TextBlock[] PKMHeldItems;
    private readonly NumericUpDown[] PKMNUDs;
    private readonly StackPanel[] PKMRows;

    private GroupBoxView GB_MessageTB = null!;
    private GroupBoxView GB_MessageNUD = null!;
    private GroupBoxView GB_PKM = null!;
    private StackPanel BoxSizeRow = null!;
    private StackPanel AppearRow = null!;
    private StackPanel MiscRow = null!;
    private StackPanel EndingRow = null!;
    private StackPanel SIDRow = null!;
    private StackPanel VersionRow = null!;
    private TextBlock L_AuthorSID = null!;
    private TextBlock L_AuthorVersion = null!;

    public MailBoxWindow(SaveFile sav) : base("SAV_MailBox", "Mail Editor")
    {
        SAV = (Origin = sav).Clone();
        Context = SAV.Context;
        p = SAV.PartyData;
        editing = true;

        Messages =
        [
            [Msg("00"), Msg("01"), Msg("02"), Msg("03")],
            [Msg("10"), Msg("11"), Msg("12"), Msg("13")],
            [Msg("20"), Msg("21"), Msg("22"), Msg("23")],
        ];
        Miscs = [UiFactory.NumericUpDown("NUD_Misc1", 0, ushort.MaxValue, 105), UiFactory.NumericUpDown("NUD_Misc2", 0, ushort.MaxValue, 105), UiFactory.NumericUpDown("NUD_Misc3", 0, ushort.MaxValue, 105)];
        PKMLabels = [.. Enumerable.Range(1, 6).Select(i => UiFactory.Label($"L_PKM{i}", "-"))];
        PKMHeldItems = [.. Enumerable.Range(1, 6).Select(i => UiFactory.Label($"L_HeldItem{i}", "(Mail)"))];
        PKMNUDs = [.. Enumerable.Range(1, 6).Select(i => UiFactory.NumericUpDown($"NUD_MailID{i}", -1, 5, 100))];
        PKMRows = new StackPanel[6];

        (m, MailItemID, PartyBoxCount, ResetLang, ResetVer) = LoadMail();

        BuildLayout();
        ApplyVisibility();

        var filtered = GameInfo.FilteredSources;
        var source = filtered.Source;
        if (Context is EntityContext.Gen2 or EntityContext.Gen3)
        {
            AppearPKMs[0].SetItems(filtered.Species);
        }
        else if (Context is EntityContext.Gen4 or EntityContext.Gen5)
        {
            foreach (var a in AppearPKMs)
                a.SetItems(filtered.Species);
            var vers = source.VersionDataSource.Where(z => ((GameVersion)z.Value).Context == Context).ToList();
            CB_AuthorVersion.SetItems(vers);
        }
        CB_AuthorLang.SetItems(GameInfo.LanguageDataSource(SAV.Generation, SAV.Context));

        var itemList = source.Strings.GetItemStrings(SAV.Context, SAV.Version);
        CB_MailType.Items.Clear();
        CB_MailType.Items.Add(itemList[0]);
        foreach (int item in MailItemID)
            CB_MailType.Items.Add(itemList[item]);

        for (int i = p.Count; i < 6; i++)
            PKMRows[i].IsVisible = false;
        if (Context != EntityContext.Gen3)
        {
            for (int i = 0; i < PKMNUDs.Length; i++)
            {
                PKMNUDs[i].Value = i;
                PKMNUDs[i].IsEnabled = false;
            }
        }

        MakePartyList();
        MakePCList();
        LoadPKM(true);
        editing = false;

        if (PartyItems.Count > 0)
            LB_PartyHeld.SelectedIndex = 0;
        else if (BoxItems.Count > 0)
            LB_PCBOX.SelectedIndex = 0;
    }

    private static NumericUpDown Msg(string suffix) => UiFactory.NumericUpDown($"NUD_Message{suffix}", 0, ushort.MaxValue, 112);

    private (MailDetail[] Mail, int[] ItemIDs, int PartyCount, byte Lang, byte Ver) LoadMail()
    {
        switch (SAV)
        {
            case SAV2 sav2:
            {
                var mail = new MailDetail[6 + 10];
                for (int i = 0; i < mail.Length; i++)
                    mail[i] = new Mail2(sav2, i);
                NUD_BoxSize.Maximum = 10;
                NUD_BoxSize.Value = Math.Min(NUD_BoxSize.Maximum, SAV.Data[Mail2.GetMailboxOffset(SAV.Language)]);
                return (mail, [0x9E, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xBB, 0xBC, 0xBD], 6, 0, 0);
            }
            case SAV2Stadium sav2Stadium:
            {
                var mail = new MailDetail[SAV2Stadium.MailboxHeldMailCount + SAV2Stadium.MailboxMailCount];
                for (int i = 0; i < mail.Length; i++)
                    mail[i] = new Mail2(sav2Stadium, i);
                NUD_BoxSize.Maximum = SAV2Stadium.MailboxMailCount;
                NUD_BoxSize.Value = Math.Min(NUD_BoxSize.Maximum, SAV.Data[Mail2.GetMailboxOffsetStadium2(SAV.Language)]);
                return (mail, [0x9E, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xBB, 0xBC, 0xBD], SAV2Stadium.MailboxHeldMailCount, 0, 0);
            }
            case SAV3 sav3:
            {
                var mail = new MailDetail[6 + 10];
                for (int i = 0; i < mail.Length; i++)
                    mail[i] = sav3.LargeBlock.GetMail(i);
                return (mail, [121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132], 6, 0, 0);
            }
            case SAV4 sav4:
            {
                var mail = new MailDetail[p.Count + 20];
                for (int i = 0; i < p.Count; i++)
                    mail[i] = new Mail4(((PK4)p[i]).HeldMail.ToArray());
                for (int i = p.Count, j = 0; i < mail.Length; i++, j++)
                    mail[i] = sav4.GetMail(j);
                var l4 = (Mail4)mail[^1];
                return (mail, [137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148], p.Count, l4.AuthorLanguage, l4.AuthorVersion);
            }
            case SAV5 sav5:
            {
                var mail = new MailDetail[p.Count + 20];
                for (int i = 0; i < p.Count; i++)
                    mail[i] = new Mail5(((PK5)p[i]).HeldMail.ToArray());
                for (int i = p.Count, j = 0; i < mail.Length; i++, j++)
                    mail[i] = sav5.GetMail(j);
                var l5 = (Mail5)mail[^1];
                return (mail, [137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148], p.Count, l5.AuthorLanguage, l5.AuthorVersion);
            }
            default:
                throw new ArgumentException("Save file does not store mail.", nameof(SAV));
        }
    }

    #region Layout

    private void BuildLayout()
    {
        LB_PartyHeld.ItemsSource = PartyItems;
        LB_PCBOX.ItemsSource = BoxItems;

        foreach (var b in new[] { B_PartyUp, B_PartyDown, B_BoxUp, B_BoxDown })
            b.Width = 34;

        var partyBlock = UiFactory.Column(L_PartyHeld, LB_PartyHeld, UiFactory.Row(B_PartyUp, B_PartyDown));
        BoxSizeRow = UiFactory.Row(L_BoxSize, NUD_BoxSize);
        var boxBlock = UiFactory.Column(L_PCBOX, LB_PCBOX, UiFactory.Row(B_BoxUp, B_BoxDown), BoxSizeRow);
        var lists = UiFactory.Column(partyBlock, boxBlock, B_Delete);

        // Author group
        var author = UiFactory.FormGrid(5);
        UiFactory.AddFormRow(author, 0, UiFactory.Label("L_AuthorName", "Name:"), UiFactory.Row(TB_AuthorName, GT_AuthorGender));
        SIDRow = UiFactory.Row(NUD_AuthorSID);
        L_AuthorSID = UiFactory.Label("L_AuthorSID", "SID:");
        L_AuthorVersion = UiFactory.Label("L_AuthorVersion", "Version:");
        UiFactory.AddFormRow(author, 1, UiFactory.Label("L_AuthorTID", "TID:"), NUD_AuthorTID);
        UiFactory.AddFormRow(author, 2, L_AuthorSID, SIDRow);
        UiFactory.AddFormRow(author, 3, UiFactory.Label("L_AuthorLang", "Language:"), CB_AuthorLang);
        VersionRow = UiFactory.Row(CB_AuthorVersion);
        UiFactory.AddFormRow(author, 4, L_AuthorVersion, VersionRow);
        var GB_Author = new GroupBoxView("GB_Author", "Author", author);

        // Message groups
        var tb = UiFactory.Column(TB_MessageBody21, TB_MessageBody22, CHK_UserEntered);
        GB_MessageTB = new GroupBoxView("GB_MessageTB", "Message", tb);

        var nudGrid = new Grid { ColumnSpacing = 4, RowSpacing = 3 };
        for (int i = 0; i < 4; i++)
            nudGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (int y = 0; y < 3; y++)
        {
            nudGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (int x = 0; x < 4; x++)
            {
                UiFactory.SetRowCol(Messages[y][x], y, x);
                nudGrid.Children.Add(Messages[y][x]);
            }
        }
        EndingRow = UiFactory.Row(UiFactory.Label("L_MessageEnding", "Ending:"), NUD_MessageEnding);
        GB_MessageNUD = new GroupBoxView("GB_MessageNUD", "Message", UiFactory.Column(nudGrid, EndingRow));

        AppearRow = UiFactory.Row(L_AppearPKM, AppearPKMs[0], AppearPKMs[1], AppearPKMs[2]);
        MiscRow = UiFactory.Row(L_MiscValue, Miscs[0], Miscs[1], Miscs[2]);

        // Held mail id group
        var pkmGrid = UiFactory.Column();
        for (int i = 0; i < 6; i++)
        {
            PKMLabels[i].MinWidth = 110;
            PKMHeldItems[i].MinWidth = 90;
            PKMRows[i] = UiFactory.Row(PKMLabels[i], PKMHeldItems[i], PKMNUDs[i]);
            pkmGrid.Children.Add(PKMRows[i]);
        }
        GB_PKM = new GroupBoxView("GB_PKM", "Held MailID", pkmGrid);

        var right = UiFactory.Column(
            GB_Author,
            UiFactory.Row(L_MailType, CB_MailType),
            AppearRow,
            GB_MessageTB,
            GB_MessageNUD,
            MiscRow,
            GB_PKM);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        body.Children.Add(lists);
        body.Children.Add(new ScrollViewer { Content = right, MaxHeight = 620 });
        SetBody(body);

        LB_PartyHeld.SelectionChanged += (_, _) => EntryControl(true);
        LB_PCBOX.SelectionChanged += (_, _) => EntryControl(false);
        NUD_BoxSize.ValueChanged += (_, _) => { if (!editing) MakePCList(); };
        B_Delete.Click += async (_, _) => await ClickDelete();
        B_PartyUp.Click += async (_, _) => await SwapSlots(true, false);
        B_PartyDown.Click += async (_, _) => await SwapSlots(true, true);
        B_BoxUp.Click += async (_, _) => await SwapSlots(false, false);
        B_BoxDown.Click += async (_, _) => await SwapSlots(false, true);
        for (int i = 0; i < PKMNUDs.Length; i++)
        {
            int index = i;
            PKMNUDs[i].ValueChanged += (_, _) => ChangeHeldMailID(index);
        }
    }

    private void ApplyVisibility()
    {
        bool gen2 = Context == EntityContext.Gen2;
        BoxSizeRow.IsVisible = CHK_UserEntered.IsVisible = gen2;
        GB_MessageTB.IsVisible = gen2;
        GB_MessageNUD.IsVisible = !gen2;
        bool gen45 = Context is EntityContext.Gen4 or EntityContext.Gen5;
        Messages[0][3].IsVisible = Messages[1][3].IsVisible = Messages[2][3].IsVisible = gen45;
        SIDRow.IsVisible = L_AuthorSID.IsVisible = !gen2;
        GT_AuthorGender.IsVisible = VersionRow.IsVisible = L_AuthorVersion.IsVisible = gen45;
        AppearRow.IsVisible = Context != EntityContext.Gen5;
        AppearPKMs[1].IsVisible = AppearPKMs[2].IsVisible = Context == EntityContext.Gen4;
        EndingRow.IsVisible = Context == EntityContext.Gen5;
        MiscRow.IsVisible = Context == EntityContext.Gen5;
        GB_PKM.IsVisible = SAV is not SAV2Stadium;
        B_PartyUp.IsEnabled = B_PartyDown.IsEnabled = SAV is not SAV2Stadium;
    }

    #endregion

    #region Lists

    private string GetLabel(int index) => m[index].IsEmpty != true ? $"{index}: From {m[index].AuthorName}" : $"{index}:  (empty)";

    private void LoadList()
    {
        if (entry < PartyBoxCount)
            MakePartyList();
        else
            MakePCList();
    }

    private void MakePartyList()
    {
        var selected = LB_PartyHeld.SelectedIndex;
        PartyItems.Clear();
        for (int i = 0; i < PartyBoxCount; i++)
            PartyItems.Add(GetLabel(i));
        if ((uint)selected < PartyItems.Count)
            LB_PartyHeld.SelectedIndex = selected;
    }

    private void MakePCList()
    {
        var selected = LB_PCBOX.SelectedIndex;
        BoxItems.Clear();
        if (Context == EntityContext.Gen2)
        {
            int boxSize = (int)(NUD_BoxSize.Value ?? 0);
            for (int i = PartyBoxCount, j = 0; i < m.Length; i++, j++)
            {
                if (j < boxSize)
                    BoxItems.Add(GetLabel(i));
            }
        }
        else
        {
            for (int i = PartyBoxCount; i < m.Length; i++)
                BoxItems.Add(GetLabel(i));
        }
        if ((uint)selected < BoxItems.Count)
            LB_PCBOX.SelectedIndex = selected;
    }

    private void LoadPKM(bool isInit)
    {
        editing = true;
        for (int i = 0; i < p.Count && i < PKMLabels.Length; i++)
        {
            if (isInit)
                PKMLabels[i].Text = GetSpeciesNameFromCB(p[i].Species);
            int j = Array.IndexOf(MailItemID, p[i].HeldItem);
            PKMHeldItems[i].Text = j >= 0 ? CB_MailType.Items[j + 1]?.ToString() : "(not Mail)";
            if (Context != EntityContext.Gen3)
                continue;
            int k = ((PK3)p[i]).HeldMailID;
            PKMNUDs[i].Value = k is >= -1 and <= 5 ? k : -1;
        }
        editing = false;
    }

    private string GetSpeciesNameFromCB(int index)
    {
        if (AppearPKMs[0].ItemsSource is IEnumerable<ComboItem> items)
        {
            var match = items.FirstOrDefault(z => z.Value == index);
            if (match is not null)
                return match.Text;
        }
        return "PKM";
    }

    #endregion

    #region Entry editing

    private void EntryControl(bool fromParty)
    {
        if (editing)
            return;
        editing = true;
        int partyIndex = LB_PartyHeld.SelectedIndex;
        int pcIndex = LB_PCBOX.SelectedIndex;
        if (entry >= 0)
        {
            TempSave();
            if (GetLabel(entry) != loadedLabel)
                LoadList();
        }
        if (fromParty && partyIndex >= 0)
        {
            entry = partyIndex;
            LB_PCBOX.SelectedIndex = -1;
        }
        else if (!fromParty && pcIndex >= 0)
        {
            entry = PartyBoxCount + pcIndex;
            LB_PartyHeld.SelectedIndex = -1;
        }
        else
        {
            entry = -1;
        }
        editing = false;
        if (entry >= 0)
        {
            LoadMailEntry();
            loadedLabel = GetLabel(entry);
        }
    }

    private void LoadMailEntry()
    {
        editing = true;
        var mail = m[entry];
        TB_AuthorName.Text = mail.AuthorName;
        NUD_AuthorTID.Value = mail.AuthorTID;
        CB_AuthorLang.SetValue(mail.AuthorLanguage);
        CB_MailType.SelectedIndex = MailTypeToCBIndex(mail);
        var species = mail.AppearPKM;
        if (Context == EntityContext.Gen2)
        {
            AppearPKMs[0].SetValue(species);
            TB_MessageBody21.Text = mail.GetMessage(false);
            TB_MessageBody22.Text = mail.GetMessage(true);
            CB_AuthorLang.IsEnabled = mail.AuthorLanguage is not (byte)LanguageID.Japanese and not (byte)LanguageID.Korean;
            CHK_UserEntered.IsChecked = mail.UserEntered;
            editing = false;
            return;
        }
        NUD_AuthorSID.Value = mail.AuthorSID;
        for (int y = 0, xc = Context == EntityContext.Gen3 ? 3 : 4; y < 3; y++)
        {
            for (int x = 0; x < xc; x++)
                Messages[y][x].Value = mail.GetMessage(y, x);
        }
        if (Context == EntityContext.Gen3)
        {
            AppearPKMs[0].SetValue(SpeciesConverter.GetNational3(species));
            editing = false;
            return;
        }
        CB_AuthorVersion.SetValue(mail.AuthorVersion);
        GT_AuthorGender.Gender = (byte)(mail.AuthorGender & 1);
        switch (mail)
        {
            case Mail4 m4:
                for (int i = 0; i < AppearPKMs.Length; i++)
                    AppearPKMs[i].SetValue(Math.Max(0, m4.GetAppearSpecies(i) - 7));
                break;
            case Mail5 m5:
                for (int i = 0; i < Miscs.Length; i++)
                    Miscs[i].Value = m5.GetMisc(i);
                NUD_MessageEnding.Value = m5.MessageEnding;
                break;
        }
        editing = false;
    }

    private void TempSave()
    {
        if (entry < 0)
            return;
        var mail = m[entry];
        mail.AuthorName = TB_AuthorName.Text ?? string.Empty;
        mail.AuthorTID = (ushort)(NUD_AuthorTID.Value ?? 0);
        var lang = CB_AuthorLang.GetSelectedItem()?.Value ?? (int)LanguageID.English;
        mail.AuthorLanguage = (byte)lang;
        mail.MailType = CBIndexToMailType(CB_MailType.SelectedIndex);
        var species = (ushort)(AppearPKMs[0].GetSelectedItem()?.Value ?? 0);
        if (Context == EntityContext.Gen2)
        {
            mail.AppearPKM = species;
            mail.SetMessage(TB_MessageBody21.Text ?? string.Empty, TB_MessageBody22.Text ?? string.Empty, CHK_UserEntered.IsChecked == true);
            return;
        }
        mail.AuthorSID = (ushort)(NUD_AuthorSID.Value ?? 0);
        for (int y = 0, xc = Context == EntityContext.Gen3 ? 3 : 4; y < 3; y++)
        {
            for (int x = 0; x < xc; x++)
                mail.SetMessage(y, x, (ushort)(Messages[y][x].Value ?? 0));
        }
        if (Context == EntityContext.Gen3)
        {
            mail.AppearPKM = SpeciesConverter.GetInternal3(species);
            return;
        }

        mail.AuthorVersion = (byte)(CB_AuthorVersion.GetSelectedItem()?.Value ?? 0);
        mail.AuthorGender = (byte)((mail.AuthorGender & 0xFE) | GT_AuthorGender.Gender);
        switch (mail)
        {
            case Mail4 m4:
                for (int i = 0; i < AppearPKMs.Length; i++)
                {
                    var index = AppearPKMs[i].GetSelectedItem()?.Value ?? -1;
                    index = index == -1 ? 0 : index + 7;
                    m4.SetAppearSpecies(i, (ushort)index);
                }
                break;
            case Mail5 m5:
                for (int i = 0; i < Miscs.Length; i++)
                    m5.SetMisc(i, (ushort)(Miscs[i].Value ?? 0));
                m5.MessageEnding = (ushort)(NUD_MessageEnding.Value ?? 0);
                break;
        }
    }

    private void ChangeHeldMailID(int index)
    {
        if (editing || Context != EntityContext.Gen3 || index >= p.Count)
            return;
        ((PK3)p[index]).HeldMailID = (sbyte)(PKMNUDs[index].Value ?? -1);
    }

    private int MailTypeToCBIndex(MailDetail mail) => Context <= EntityContext.Gen3
        ? 1 + Array.IndexOf(MailItemID, mail.MailType)
        : (mail.IsEmpty == false ? 1 + mail.MailType : 0);

    private int CBIndexToMailType(int cbIndex) => Context <= EntityContext.Gen3
        ? (cbIndex > 0 ? MailItemID[cbIndex - 1] : 0)
        : (cbIndex > 0 ? cbIndex - 1 : 0xFF);

    private bool ItemIsMail(int itemID) => MailItemID.Contains(itemID);

    #endregion

    #region Commands

    private async Task ClickDelete()
    {
        if (entry < 0)
            return;
        if (entry < p.Count && await ModifyHeldItem() == DialogResult.Cancel)
            return;

        switch (m[entry])
        {
            case Mail4 m4: m4.SetBlank(ResetLang, ResetVer); break;
            case Mail5 m5: m5.SetBlank(ResetLang, ResetVer); break;
            default: m[entry].SetBlank(); break;
        }
        LoadList();
        LoadMailEntry();
    }

    /// <summary>
    /// Offers to clear the held item of every party member pointing at the mail being deleted.
    /// </summary>
    private async Task<DialogResult> ModifyHeldItem()
    {
        var affected = p.Select((pk, i) => (sbyte)(PKMNUDs[i].Value ?? -1) == entry && ItemIsMail(pk.HeldItem) ? pk : null).ToArray();
        if (affected.All(v => v is null))
            return DialogResult.Abort;

        var lines = affected
            .Select((v, i) => v is null ? string.Empty : $"  {PKMLabels[i].Text}: {PKMHeldItems[i].Text} -> {CB_MailType.Items[0]}")
            .Where(z => z.Length != 0);
        var msg = $"Modify PKM's HeldItem?{Environment.NewLine}{string.Join(Environment.NewLine, lines)}{Environment.NewLine}{Environment.NewLine}Yes: Delete Mail & Modify PKM{Environment.NewLine}No: Delete Mail";

        var result = await AppDialogs.Prompt(this, MessageBoxButtons.YesNoCancel, msg);
        if (result != DialogResult.Yes)
            return result;

        foreach (var pk in affected)
        {
            if (pk is null)
                continue;
            pk.HeldItem = 0;
            if (Context == EntityContext.Gen3)
                ((PK3)pk).HeldMailID = -1;
        }
        LoadPKM(false);
        return result;
    }

    private async Task SwapSlots(bool party, bool down)
    {
        var lb = party ? LB_PartyHeld : LB_PCBOX;
        var items = party ? PartyItems : BoxItems;
        if (lb.SelectedIndex == -1)
            return;

        int index = lb.SelectedIndex;
        var otherIndex = index + (down ? 1 : -1);
        if ((uint)otherIndex >= items.Count)
        {
            await AppDialogs.Alert(this, "No adjacent slot.");
            return;
        }

        if (!party)
        {
            index += PartyBoxCount;
            otherIndex += PartyBoxCount;
        }

        editing = true;
        (m[otherIndex], m[index]) = (m[index], m[otherIndex]);
        if ((entry >= PartyBoxCount) == !party)
            entry = otherIndex;
        LoadList();
        lb.SelectedIndex = party ? otherIndex : otherIndex - PartyBoxCount;
        editing = false;
    }

    #endregion

    protected override void OnSave() => _ = SaveAsync();

    private async Task SaveAsync()
    {
        if (entry >= 0)
            TempSave();
        WriteMail();

        var errors = CheckValid();
        if (errors.Count != 0)
        {
            var msg = $"Validation Error. Save?{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, errors)}";
            if (await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, msg) != DialogResult.Yes)
                return;
        }
        Origin.CopyChangesFrom(SAV);
        Close();
    }

    private void WriteMail()
    {
        switch (Context)
        {
            case EntityContext.Gen2:
                foreach (var n in m)
                    n.CopyTo(SAV);
                if (SAV is SAV2)
                {
                    // The game keeps a duplicate copy of the mailbox; mirror the edits into it.
                    int ofs = 0x600;
                    int len = Mail2.GetMailSize(SAV.Language) * 6;
                    SAV.Data.Slice(ofs, len).CopyTo(SAV.Data.Slice(ofs + len, len));
                    ofs += len << 1;
                    SAV.Data[ofs] = (byte)(NUD_BoxSize.Value ?? 0);
                    len = (Mail2.GetMailSize(SAV.Language) * 10) + 1;
                    SAV.Data.Slice(ofs, len).CopyTo(SAV.Data.Slice(ofs + len, len));
                }
                else if (SAV is SAV2Stadium)
                {
                    int ofs = Mail2.GetMailboxOffsetStadium2(SAV.Language);
                    SAV.Data[ofs] = (byte)(NUD_BoxSize.Value ?? 0);
                }
                break;
            case EntityContext.Gen3:
                foreach (var n in m)
                    n.CopyTo(SAV);
                break;
            case EntityContext.Gen4:
                for (int i = 0; i < p.Count; i++)
                    m[i].CopyTo((PK4)p[i]);
                for (int i = p.Count; i < m.Length; i++)
                    m[i].CopyTo(SAV);
                break;
            case EntityContext.Gen5:
                for (int i = 0; i < p.Count; i++)
                    m[i].CopyTo((PK5)p[i]);
                for (int i = p.Count; i < m.Length; i++)
                    m[i].CopyTo(SAV);
                break;
        }
        if (p.Count > 0)
            SAV.PartyData = p;
    }

    /// <summary>
    /// Cross-checks mail against the party members that reference it (port of <c>CheckValid</c>).
    /// </summary>
    private List<string> CheckValid()
    {
        var ret = new List<string>();
        if (Context == EntityContext.Gen3)
        {
            Span<int> heldMailIDs = stackalloc int[p.Count];
            for (int i = 0; i < p.Count; i++)
            {
                int h = ((PK3)p[i]).HeldMailID;
                heldMailIDs[i] = h;
                if (ItemIsMail(p[i].HeldItem))
                {
                    if (h is < 0 or > 5)
                        ret.Add($"Party#{i + 1} MailID mismatch");
                    else if (m[h].IsEmpty == true)
                        ret.Add($"Party#{i + 1} MailID mismatch");
                }
                else if (h != -1)
                {
                    ret.Add($"Party#{i + 1} MailID mismatch");
                }
            }
            for (int i = 0; i < 6; i++)
            {
                var count = heldMailIDs.Count(i);
                if (count > 1)
                    ret.Add($"MailID{i} duplicated");
                if (m[i].IsEmpty == false && count == 0)
                    ret.Add($"MailID{i} not referred");
            }
        }
        else if (Context is EntityContext.Gen2 or EntityContext.Gen4)
        {
            for (int i = 0; i < p.Count; i++)
            {
                if (ItemIsMail(p[i].HeldItem))
                {
                    if (m[i].IsEmpty == true)
                        ret.Add($"MailID{i} MailType mismatch");
                }
                else if (m[i].IsEmpty == false)
                {
                    ret.Add($"MailID{i} MailType mismatch");
                }
            }
        }
        else if (Context == EntityContext.Gen5)
        {
            for (int i = 0; i < p.Count; i++)
            {
                if (ItemIsMail(p[i].HeldItem) && m[i].IsEmpty == true)
                    ret.Add($"MailID{i} MailType mismatch");
            }
        }

        for (int i = 0; i < m.Length; i++)
        {
            if (m[i].IsEmpty is null)
                ret.Add($"MailID{i} MailType mismatch");
        }
        return ret;
    }
}
