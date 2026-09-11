using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen6;

/// <summary>
/// Secret Base editor for Omega Ruby / Alpha Sapphire (port of the WinForms <c>SAV_SecretBase</c>).
/// </summary>
/// <remarks>
/// Holds the player's own base plus thirty bases received from other players. Every base has 28 decoration
/// placements; the received ones additionally carry a three-member team stored in a cut-down entity format.
/// </remarks>
public sealed class SecretBase6Window : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV6AO SAV;

    private SecretBase6? CurrentBase;
    private int CurrentPKMIndex = -1;
    private SecretBase6PKM? CurrentPKM;
    private int CurrentPlacementIndex = -1;
    private SecretBase6GoodPlacement? CurrentPlacement;
    private bool loading = true;

    private readonly ListBox LB_Bases = new() { Name = "LB_Bases", Width = 190, Height = 420 };
    private readonly ObservableCollection<string> BaseItems = [];
    private readonly PropertyGridView PG_Base = new() { Name = "PG_Base", Width = 330, Height = 300 };

    // Object layout
    private readonly NumericUpDown NUD_FObject = UiFactory.NumericUpDown("NUD_FObject", 0, SecretBase6.COUNT_GOODS - 1, 100);
    private readonly NumericUpDown NUD_FObjType = UiFactory.NumericUpDown("NUD_FObjType", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_FX = UiFactory.NumericUpDown("NUD_FX", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_FY = UiFactory.NumericUpDown("NUD_FY", 0, ushort.MaxValue, 110);
    private readonly NumericUpDown NUD_FRot = UiFactory.NumericUpDown("NUD_FRot", 0, byte.MaxValue, 110);

    // Participant
    private readonly NumericUpDown NUD_FPKM = UiFactory.NumericUpDown("NUD_FPKM", 0, SecretBase6Other.COUNT_TEAM - 1, 100);
    private readonly TextBox TB_EC = UiFactory.Text("TB_EC", 8, 110);
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);
    private readonly ComboBox CB_Form = UiFactory.StringCombo("CB_Form", 130);
    private readonly ComboBox CB_HeldItem = UiFactory.Combo("CB_HeldItem", 180);
    private readonly ComboBox CB_Ability = UiFactory.Combo("CB_Ability", 180);
    private readonly ComboBox CB_Nature = UiFactory.Combo("CB_Nature", 180);
    private readonly ComboBox CB_Ball = UiFactory.Combo("CB_Ball", 180);
    private readonly TextBlock Label_Gender = UiFactory.Label("Label_Gender", "-", true);
    private readonly CheckBox CHK_Shiny = UiFactory.Check("CHK_Shiny", "☆");
    private readonly TextBox TB_Friendship = UiFactory.Text("TB_Friendship", 3, 70);
    private readonly TextBox TB_Level = UiFactory.Text("TB_Level", 3, 70);
    private readonly ComboBox[] CB_Moves = new ComboBox[4];
    private readonly ComboBox[] CB_PPu = new ComboBox[4];
    private readonly TextBox[] TB_IVs = new TextBox[6];
    private readonly TextBox[] TB_EVs = new TextBox[6];
    private StackPanel PAN_PKM = null!;

    private readonly NumericUpDown NUD_CapturedRecord = UiFactory.NumericUpDown("NUD_CapturedRecord", 0, uint.MaxValue, 140);
    private readonly Button B_GiveDecor = UiFactory.Button("B_GiveDecor", "Give All Decorations");
    private readonly Button B_FDelete = UiFactory.Button("B_FDelete", "X");
    private readonly Button B_Import = UiFactory.Button("B_Import", "Import");
    private readonly Button B_Export = UiFactory.Button("B_Export", "Export");

    public SecretBase6Window(SAV6AO sav) : base("SAV_SecretBase", "Secret Base Editor")
    {
        SAV = (SAV6AO)(Origin = sav).Clone();

        for (int i = 0; i < CB_Moves.Length; i++)
        {
            CB_Moves[i] = UiFactory.Combo($"CB_Move{i + 1}", 180);
            CB_PPu[i] = UiFactory.StringCombo($"CB_PPu{i + 1}", 60, "0", "1", "2", "3");
        }
        string[] stats = ["HP", "ATK", "DEF", "SPA", "SPD", "SPE"];
        for (int i = 0; i < stats.Length; i++)
        {
            TB_IVs[i] = UiFactory.Text($"TB_{stats[i]}IV", 2, 50);
            TB_EVs[i] = UiFactory.Text($"TB_{stats[i]}EV", 3, 60);
        }

        BuildLayout();
        SetupComboBoxes();

        ReloadSecretBaseList();
        LB_Bases.SelectedIndex = 0;
        ChangeIndexBase();

        NUD_CapturedRecord.SetValueClamped(SAV.Records.GetRecord(080));
    }

    private void SetupComboBoxes()
    {
        var filtered = GameInfo.FilteredSources;
        CB_Ball.SetItems(filtered.Balls);
        CB_HeldItem.SetItems(filtered.Items);
        CB_Species.SetItems(filtered.Species);
        CB_Nature.SetItems(filtered.Natures);
        foreach (var cb in CB_Moves)
            cb.SetItems(filtered.Moves);
    }

    private void BuildLayout()
    {
        LB_Bases.ItemsSource = BaseItems;

        var placement = UiFactory.FormGrid(5);
        UiFactory.AddFormRow(placement, 0, UiFactory.Label("L_Index", "Index:"), NUD_FObject);
        UiFactory.AddFormRow(placement, 1, UiFactory.Label("L_Decoration", "Decoration:"), NUD_FObjType);
        UiFactory.AddFormRow(placement, 2, UiFactory.Label("L_X", "X Coordinate:"), NUD_FX);
        UiFactory.AddFormRow(placement, 3, UiFactory.Label("L_Y", "Y Coordinate:"), NUD_FY);
        UiFactory.AddFormRow(placement, 4, UiFactory.Label("L_Rotation", "Rotation Val:"), NUD_FRot);
        var gbObject = new GroupBoxView("GB_Object", "Object Layout", placement);

        var main = UiFactory.Column(
            PG_Base,
            gbObject,
            UiFactory.Row(UiFactory.Label("L_FlagsCaptured", "Flags Captured: "), NUD_CapturedRecord),
            UiFactory.Row(B_GiveDecor, B_Import, B_Export, B_FDelete));

        PAN_PKM = BuildParticipant();

        var tabs = new TabControl { Name = "TC_SecretBase" };
        tabs.Items.Add(new TabItem { Name = "f_MAIN", Header = "Main", Content = new ScrollViewer { Content = main, MaxHeight = 600 } });
        tabs.Items.Add(new TabItem { Name = "f_PKM", Header = "Pokemon", Content = new ScrollViewer { Content = PAN_PKM, MaxHeight = 600 } });

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(UiFactory.Column(UiFactory.Label("L_Favorite", "Favorites:"), LB_Bases));
        body.Children.Add(tabs);
        SetBody(body);

        LB_Bases.SelectionChanged += (_, _) => ChangeIndexBase();
        NUD_FObject.ValueChanged += (_, _) => ChangeIndexPlacement();
        NUD_FPKM.ValueChanged += (_, _) => ChangeIndexPKM();
        CB_Species.SelectionChanged += (_, _) => UpdateSpecies();
        CB_Form.SelectionChanged += (_, _) => UpdateForm();
        Label_Gender.AttachClick(_ => ClickGender());
        B_GiveDecor.Click += (_, _) => SAV.Blocks.SecretBase.GiveAllGoods();
        B_FDelete.Click += async (_, _) => await ClickDelete();
        B_Import.Click += async (_, _) => await ClickImport();
        B_Export.Click += async (_, _) => await ClickExport();
    }

    private StackPanel BuildParticipant()
    {
        var top = UiFactory.FormGrid(8);
        UiFactory.AddFormRow(top, 0, UiFactory.Label("L_Participant", "Participant:"), NUD_FPKM);
        UiFactory.AddFormRow(top, 1, UiFactory.Label("L_EncryptionConstant", "ENC:"), TB_EC);
        UiFactory.AddFormRow(top, 2, UiFactory.Label("L_Species", "Species:"), UiFactory.Row(CB_Species, Label_Gender, CHK_Shiny));
        UiFactory.AddFormRow(top, 3, UiFactory.Label("L_Form", "Form:"), CB_Form);
        UiFactory.AddFormRow(top, 4, UiFactory.Label("L_HeldItem", "Item:"), CB_HeldItem);
        UiFactory.AddFormRow(top, 5, UiFactory.Label("L_Ability", "Ability:"), CB_Ability);
        UiFactory.AddFormRow(top, 6, UiFactory.Label("L_Nature", "Nature:"), CB_Nature);
        UiFactory.AddFormRow(top, 7, UiFactory.Label("L_Ball", "Ball:"), CB_Ball);

        var moves = UiFactory.FormGrid(4);
        for (int i = 0; i < CB_Moves.Length; i++)
            UiFactory.AddFormRow(moves, i, UiFactory.Label($"L_Move{i + 1}", $"Move {i + 1}:"), UiFactory.Row(CB_Moves[i], CB_PPu[i]));
        var gbMoves = new GroupBoxView("GB_Moves", "Moves", UiFactory.Column(UiFactory.Label("L_PPups", "PP Ups"), moves));

        string[] stats = ["HP", "ATK", "DEF", "SpA", "SpD", "SPE"];
        var statGrid = UiFactory.FormGrid(7);
        UiFactory.AddFormRow(statGrid, 0, null, UiFactory.Row(UiFactory.Label("L_IVs", "IVs"), UiFactory.Label("L_EVs", "EVs")));
        for (int i = 0; i < stats.Length; i++)
            UiFactory.AddFormRow(statGrid, i + 1, UiFactory.Label($"L_{stats[i]}", stats[i]), UiFactory.Row(TB_IVs[i], TB_EVs[i]));
        var gbStats = new GroupBoxView("GB_Stats", "Stats", statGrid);

        var misc = UiFactory.FormGrid(2);
        UiFactory.AddFormRow(misc, 0, UiFactory.Label("L_PKFriendship", "Friendship"), TB_Friendship);
        UiFactory.AddFormRow(misc, 1, UiFactory.Label("L_Level", "Level"), TB_Level);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(UiFactory.Column(top, misc));
        row.Children.Add(UiFactory.Column(gbMoves, gbStats));
        return UiFactory.Column(row);
    }

    #region Base list

    private void ReloadSecretBaseList()
    {
        loading = true;
        int index = LB_Bases.SelectedIndex;
        BaseItems.Clear();

        var block = SAV.SecretBase;
        BaseItems.Add($"* {block.GetSecretBaseSelf().TrainerName}");
        for (int i = 0; i < SecretBase6Block.OtherSecretBaseCount; i++)
        {
            var name = block.GetSecretBaseOther(i).TrainerName;
            if (string.IsNullOrWhiteSpace(name))
                name = "Empty";
            BaseItems.Add($"{i + 1:00} {name}");
        }

        if (index >= 0 && index < BaseItems.Count)
            LB_Bases.SelectedIndex = index;
        loading = false;
    }

    /// <summary>Wipes the cached references so nothing is inadvertently saved into the wrong base.</summary>
    private void ResetLoadNew()
    {
        CurrentPKM = null;
        CurrentPlacement = null;
        CurrentBase = null;
        CurrentPKMIndex = -1;
        CurrentPlacementIndex = -1;
    }

    private SecretBase6 GetSecretBaseReference(int index)
        => index == 0 ? SAV.SecretBase.GetSecretBaseSelf() : SAV.SecretBase.GetSecretBaseOther(index - 1);

    private void ChangeIndexBase()
    {
        int index = LB_Bases.SelectedIndex;
        if (index < 0 || loading)
            return;

        if (CurrentBase is { } previous)
            SaveCurrent(previous);

        ResetLoadNew();
        CurrentBase = GetSecretBaseReference(index);
        LoadCurrent(CurrentBase);
    }

    private void LoadCurrent(SecretBase6 bdata)
    {
        loading = true;
        CurrentBase = bdata;
        PG_Base.SetObject(bdata);

        var pIndex = (int)(NUD_FObject.Value ?? 0);
        LoadPlacement(bdata.GetPlacement(pIndex), pIndex);
        if (bdata is SecretBase6Other o)
            LoadOtherData(o);
        else
            PAN_PKM.IsVisible = false;
        loading = false;
    }

    private void SaveCurrent(SecretBase6 bdata)
    {
        SavePlacement((int)(NUD_FObject.Value ?? 0));
        if (bdata is SecretBase6Other o)
            SaveOtherData(o);
    }

    #endregion

    #region Placement

    private void ChangeIndexPlacement()
    {
        if (CurrentBase is not { } bdata)
            return;
        SavePlacement(CurrentPlacementIndex);
        var pIndex = (int)(NUD_FObject.Value ?? 0);
        LoadPlacement(bdata.GetPlacement(pIndex), pIndex);
    }

    private void LoadPlacement(SecretBase6GoodPlacement p, int index)
    {
        SavePlacement(index);
        CurrentPlacement = p;
        CurrentPlacementIndex = index;

        NUD_FObjType.SetValueClamped(p.Good);
        NUD_FX.SetValueClamped(p.X);
        NUD_FY.SetValueClamped(p.Y);
        NUD_FRot.SetValueClamped(p.Rotation);
    }

    private void SavePlacement(int index)
    {
        if (CurrentPlacement is not { } p || index < 0)
            return;
        p.Good = (ushort)(NUD_FObjType.Value ?? 0);
        p.X = (ushort)(NUD_FX.Value ?? 0);
        p.Y = (ushort)(NUD_FY.Value ?? 0);
        p.Rotation = (byte)(NUD_FRot.Value ?? 0);
    }

    #endregion

    #region Participant

    private void LoadOtherData(SecretBase6Other full)
    {
        var pIndex = CurrentPKMIndex = (int)(NUD_FPKM.Value ?? 0);
        LoadPKM(full.GetParticipant(pIndex));
        PAN_PKM.IsVisible = true;
    }

    private void SaveOtherData(SecretBase6Other full)
    {
        if (CurrentPKM is not { } pk || CurrentPKMIndex < 0)
            return;
        SavePKM(pk);
        full.SetParticipant(CurrentPKMIndex, pk);
    }

    private void ChangeIndexPKM()
    {
        if (CurrentBase is not SecretBase6Other o)
            return;
        if (CurrentPKM is not { } pk || CurrentPKMIndex < 0)
            return;

        SavePKM(pk);
        o.SetParticipant(CurrentPKMIndex, pk);

        CurrentPKMIndex = (int)(NUD_FPKM.Value ?? 0);
        LoadPKM(o.GetParticipant(CurrentPKMIndex));
    }

    private void LoadPKM(SecretBase6PKM pk)
    {
        loading = true;
        CurrentPKM = pk;

        TB_EC.Text = pk.EncryptionConstant.ToString("X8");
        SetGenderLabel(pk.Gender);
        CB_Species.SetValue(pk.Species);
        CB_HeldItem.SetValue(pk.HeldItem);
        SetForms();
        CB_Form.SelectedIndex = Math.Clamp(pk.Form, 0, Math.Max(0, CB_Form.ItemCount - 1));
        CB_Nature.SetValue((int)pk.Nature);
        CB_Ball.SetValue(pk.Ball);

        ReadOnlySpan<int> ivs = [pk.IV_HP, pk.IV_ATK, pk.IV_DEF, pk.IV_SPA, pk.IV_SPD, pk.IV_SPE];
        ReadOnlySpan<int> evs = [pk.EV_HP, pk.EV_ATK, pk.EV_DEF, pk.EV_SPA, pk.EV_SPD, pk.EV_SPE];
        for (int i = 0; i < TB_IVs.Length; i++)
        {
            TB_IVs[i].Text = ivs[i].ToString();
            TB_EVs[i].Text = evs[i].ToString();
        }

        TB_Friendship.Text = pk.CurrentFriendship.ToString();
        TB_Level.Text = pk.CurrentLevel.ToString();

        ReadOnlySpan<ushort> moves = [pk.Move1, pk.Move2, pk.Move3, pk.Move4];
        ReadOnlySpan<int> ppUps = [pk.Move1_PPUps, pk.Move2_PPUps, pk.Move3_PPUps, pk.Move4_PPUps];
        for (int i = 0; i < CB_Moves.Length; i++)
        {
            CB_Moves[i].SetValue(moves[i]);
            CB_PPu[i].SelectedIndex = Math.Clamp(ppUps[i], 0, 3);
        }

        CHK_Shiny.IsChecked = pk.IsShiny;
        SetAbilityList(pk.Species, pk.Form, pk.AbilityNumber >> 1);
        loading = false;
    }

    private void SavePKM(SecretBase6PKM pk)
    {
        pk.EncryptionConstant = Util.GetHexValue(TB_EC.Text);
        pk.Species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
        pk.HeldItem = CB_HeldItem.GetSelectedItem()?.Value ?? 0;
        pk.Ability = CB_Ability.GetSelectedItem()?.Value ?? 0;
        pk.AbilityNumber = Math.Max(0, CB_Ability.SelectedIndex) << 1;
        pk.Nature = (Nature)(CB_Nature.GetSelectedItem()?.Value ?? 0);
        pk.Gender = EntityGender.GetFromString(Label_Gender.Text ?? "-");
        pk.Form = (byte)Math.Max(0, CB_Form.SelectedIndex);

        pk.EV_HP  = ClampEV(TB_EVs[0]);
        pk.EV_ATK = ClampEV(TB_EVs[1]);
        pk.EV_DEF = ClampEV(TB_EVs[2]);
        pk.EV_SPA = ClampEV(TB_EVs[3]);
        pk.EV_SPD = ClampEV(TB_EVs[4]);
        pk.EV_SPE = ClampEV(TB_EVs[5]);

        pk.IV_HP  = ClampIV(TB_IVs[0]);
        pk.IV_ATK = ClampIV(TB_IVs[1]);
        pk.IV_DEF = ClampIV(TB_IVs[2]);
        pk.IV_SPA = ClampIV(TB_IVs[3]);
        pk.IV_SPD = ClampIV(TB_IVs[4]);
        pk.IV_SPE = ClampIV(TB_IVs[5]);

        pk.Move1 = (ushort)(CB_Moves[0].GetSelectedItem()?.Value ?? 0);
        pk.Move2 = (ushort)(CB_Moves[1].GetSelectedItem()?.Value ?? 0);
        pk.Move3 = (ushort)(CB_Moves[2].GetSelectedItem()?.Value ?? 0);
        pk.Move4 = (ushort)(CB_Moves[3].GetSelectedItem()?.Value ?? 0);
        pk.Move1_PPUps = Math.Max(0, CB_PPu[0].SelectedIndex);
        pk.Move2_PPUps = Math.Max(0, CB_PPu[1].SelectedIndex);
        pk.Move3_PPUps = Math.Max(0, CB_PPu[2].SelectedIndex);
        pk.Move4_PPUps = Math.Max(0, CB_PPu[3].SelectedIndex);

        pk.IsShiny = CHK_Shiny.IsChecked == true;
        pk.CurrentFriendship = (byte)Math.Min(byte.MaxValue, Util.ToUInt32(TB_Friendship.Text));
        pk.Ball = (byte)(CB_Ball.GetSelectedItem()?.Value ?? 0);
        pk.CurrentLevel = (byte)Math.Min(byte.MaxValue, Util.ToUInt32(TB_Level.Text));

        static int ClampEV(TextBox tb) => (int)Math.Min(EffortValues.Max252, Util.ToUInt32(tb.Text));
        static int ClampIV(TextBox tb) => (int)(Util.ToUInt32(tb.Text) & 0x1F);
    }

    #endregion

    #region Species / form / gender

    private void SetAbilityList() => SetAbilityList(
        (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0),
        (byte)Math.Max(0, CB_Form.SelectedIndex),
        CB_Ability.SelectedIndex);

    private void SetAbilityList(ushort species, byte form, int abilityIndex)
    {
        var abilities = PersonalTable.AO.GetFormEntry(species, form);
        CB_Ability.SetItems(GameInfo.FilteredSources.GetAbilityList(abilities));
        CB_Ability.SelectedIndex = abilityIndex is >= 0 and < 3 ? abilityIndex : 0;
    }

    private void SetForms()
    {
        var species = (ushort)(CB_Species.GetSelectedItem()?.Value ?? 0);
        bool hasForms = FormInfo.HasFormSelection(PersonalTable.AO[species], species, 6);
        CB_Form.IsEnabled = CB_Form.IsVisible = hasForms;
        CB_Form.ItemsSource = FormConverter.GetFormList(species, GameInfo.Strings.types, GameInfo.Strings.forms, GameInfo.GenderSymbolUnicode, SAV.Context);
    }

    private void UpdateSpecies()
    {
        if (loading)
            return;
        SetForms();

        var species = CB_Species.GetSelectedItem()?.Value ?? 0;
        var pi = SAV.Personal[species];
        var gender = pi.IsDualGender ? EntityGender.GetFromString(Label_Gender.Text ?? "-") : pi.FixedGender();
        SetGenderLabel(gender);
        SetAbilityList();
    }

    private void UpdateForm()
    {
        if (loading)
            return;
        SetAbilityList();

        // If the form has a single gender, account for it.
        var text = CB_Form.SelectedItem as string ?? string.Empty;
        if (EntityGender.GetFromString(text) < 2)
            Label_Gender.Text = GameInfo.GenderSymbolUnicode[Math.Max(0, CB_Form.SelectedIndex)];
    }

    private void ClickGender()
    {
        var species = CB_Species.GetSelectedItem()?.Value ?? 0;
        var pi = SAV.Personal[species];
        byte gender = pi.IsDualGender
            ? (byte)((EntityGender.GetFromString(Label_Gender.Text ?? "-") ^ 1) & 1)
            : pi.FixedGender();
        SetGenderLabel(gender);
    }

    private void SetGenderLabel(byte gender)
    {
        var symbols = GameInfo.GenderSymbolUnicode;
        if ((uint)gender >= symbols.Count)
            gender = 0;
        Label_Gender.Text = symbols[gender];
    }

    #endregion

    #region I/O

    private async Task ClickImport()
    {
        var path = await FileDialogs.OpenSingleFile(this, "Secret Base Data|*.sb6");
        if (path is null)
            return;
        if (new FileInfo(path).Length is not (SecretBase6.SIZE or SecretBase6Other.SIZE))
            return;

        var obj = SecretBase6.Read(File.ReadAllBytes(path));
        if (obj is null || CurrentBase is not { } sb)
            return;

        ResetLoadNew();
        sb.Load(obj);
        ReloadSecretBaseList();
        LoadCurrent(sb);
    }

    private async Task ClickExport()
    {
        if (CurrentBase is not { } sb)
            return;
        SaveCurrent(sb);

        var tr = sb.TrainerName;
        if (string.IsNullOrWhiteSpace(tr))
            tr = "Trainer";
        var suggested = $"{sb.BaseLocation:D2} - {PathUtil.CleanFileName(tr)}.sb6";
        var path = await FileDialogs.SaveFileDialog(this, "Secret Base Data|*.sb6", suggested);
        if (path is null)
            return;
        await File.WriteAllBytesAsync(path, sb.Data.ToArray());
    }

    private async Task ClickDelete()
    {
        if (LB_Bases.SelectedIndex < 1)
        {
            await AppDialogs.Alert(this, MsgSecretBaseDeleteSelf);
            return;
        }

        int index = LB_Bases.SelectedIndex - 1;
        if (CurrentBase is not { } bdata)
            return;

        var name = bdata.TrainerName;
        if (string.IsNullOrEmpty(name))
            name = "Empty";

        var dr = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, string.Format(MsgSecretBaseDeleteConfirm, name, index));
        if (dr != DialogResult.Yes)
            return;

        SAV.Blocks.SecretBase.DeleteOther(index);
        ReloadSecretBaseList();
        ResetLoadNew();
        LoadCurrent(CurrentBase = GetSecretBaseReference(index + 1));
        LB_Bases.SelectedIndex = index + 1;
    }

    #endregion

    protected override void OnSave()
    {
        SAV.Records.SetRecord(080, (int)(uint)(NUD_CapturedRecord.Value ?? 0));
        if (CurrentBase is { } bdata)
            SaveCurrent(bdata);
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
