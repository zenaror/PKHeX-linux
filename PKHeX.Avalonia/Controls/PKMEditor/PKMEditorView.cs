using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Settings;
using PKHeX.Avalonia.Views;
using PKHeX.Avalonia.Views.EntityEditors;
using PKHeX.Core;
using PKHeX.Drawing;
using PKHeX.Drawing.Misc;
using PKHeX.Drawing.PokeSprite;
using static PKHeX.Core.MessageStrings;
using Color = System.Drawing.Color;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Entity editor (port of the WinForms <c>PKMEditor</c>).
/// </summary>
public sealed partial class PKMEditorView : UserControl, IMainEditor
{
    public bool IsEditorInitialized { get; private set; }

    private readonly Dictionary<Image, string> InitialImages = [];
    private readonly Dictionary<Image, global::Avalonia.Media.Imaging.Bitmap> OwnedImages = [];

    public PKMEditorView()
    {
        Entity = FakeSaveFile.Default.BlankPKM;
        BuildLayout();
        InitializeMarkingImages();

        // Commonly reused Control arrays
        Moves = [MC_Move1, MC_Move2, MC_Move3, MC_Move4];
        Relearn = [CB_RelearnMove1, CB_RelearnMove2, CB_RelearnMove3, CB_RelearnMove4];
        Markings = [PB_Mark1, PB_Mark2, PB_Mark3, PB_Mark4, PB_Mark5, PB_Mark6];

        // Legality Indicators
        relearnPB = [PB_WarnRelearn1, PB_WarnRelearn2, PB_WarnRelearn3, PB_WarnRelearn4];
        BTN_NicknameWarn.IsVisible = BTN_OTNameWarn.IsVisible = false;

        // Validation of incompletely entered data fields
        bool Criteria(Control c) => c is ComboBox { SelectedItem: null } cb && cb.GetItemCount() != 0;
        ValidatedControls =
        [
            new([MC_Move1.CB_Move, MC_Move2.CB_Move, MC_Move3.CB_Move, MC_Move4.CB_Move], _ => true, Criteria),
            new([CB_Species], _ => true, Criteria),
            new([CB_HeldItem], pk => pk.Format >= 2, Criteria),
            new([CB_Ability, CB_Nature, CB_MetLocation, CB_Ball], pk => pk.Format >= 3, Criteria),
            new([CB_EggLocation], pk => pk.Format >= 4, Criteria),
            new([CB_Country, CB_SubRegion], pk => pk is PK6 or PK7, Criteria),
            new(Relearn, pk => pk.Format >= 6, Criteria),
            new([CB_StatAlignment], pk => pk.Format >= 8, Criteria),
            new([CB_AlphaMastered], pk => pk is PA8, Criteria),
        ];
        foreach (var m in Moves)
        {
            m.CB_PPUps.SelectionChanged += (_, _) => m.HealPP(Entity);
            m.CB_Move.DisplayMemberBinding = null;
            m.CB_Move.ItemTemplate = CreateMoveTemplate();
            m.CB_Move.SelectionBoxItemTemplate = new FuncDataTemplate<ComboItem>((item, _) => new TextBlock { Text = item?.Text, VerticalAlignment = VerticalAlignment.Center });
            m.CB_Move.DropDownOpened += (s, _) => ValidateMoveDropDown(s);
            m.CB_Move.SelectionChanged += (s, _) => ValidateMove(s);
        }

        Stats.MainEditor = this;
        CR_PK1.MainEditor = this;
        LoadShowdownSet = LoadShowdownSetDefault;
        TID_Trainer.UpdatedID += (_, _) => Update_ID();

        TB_EXP.MouseWheelIncrement(1);
        TB_Level.MouseWheelIncrement(1);
        TB_Friendship.MouseWheelIncrement(1);
        ExperienceBar.ValueChanged += (_, _) => TB_EXP.Text = ExperienceBar.EXP.ToString();

        WireEvents();
    }

    private void WireEvents()
    {
        // Main tab
        BTN_Shinytize.Click += (_, _) => UpdateShinyPID();
        BTN_RerollPID.Click += (_, _) => UpdateRandomPID(BTN_RerollPID);
        UC_Gender.Click += (_, _) => ClickGender();
        CB_Species.SelectionChanged += (_, _) => { ValidateComboBox2(CB_Species); UpdateSpecies(CB_Species); };
        Label_Species.AttachClick(mods => UpdateNicknameLabel(mods));
        CHK_Nicknamed.AttachClick(_ => CHK_NicknamedFlag.IsChecked = CHK_NicknamedFlag.IsChecked != true);
        CHK_NicknamedFlag.IsCheckedChanged += (_, _) => UpdateNickname(CHK_NicknamedFlag);
        TB_Nickname.OnTextChanged(_ => UpdateIsNicknamed());
        BTN_NicknameWarn.Click += (_, _) => _ = FontWarn(TB_Nickname.Text ?? string.Empty, MsgPKMNicknameWarn, BTN_NicknameWarn);
        TB_EXP.OnTextChanged(_ => UpdateEXPLevel(TB_EXP));
        TB_Level.OnTextChanged(_ => UpdateEXPLevel(TB_Level));
        TB_Level.AttachClick(mods => { if (mods == KeyModifiers.Control) TB_Level.Text = "100"; });
        Label_CurLevel.AttachClick(m => _ = ClickMetLocation());
        Label_Nature.AttachClick(_ => ClickNature(Label_Nature));
        L_StatAlignment.AttachClick(_ => ClickNature(L_StatAlignment));
        CB_Nature.SelectionChanged += (_, _) => ValidateComboBox2(CB_Nature);
        CB_StatAlignment.SelectionChanged += (_, _) => ValidateComboBox2(CB_StatAlignment);
        CB_Form.SelectionChanged += (_, _) => UpdateForm(CB_Form);
        FA_Form.ValueChanged += (_, _) => UpdateFormArgument();
        CB_HeldItem.SelectionChanged += (_, _) => ValidateComboBox2(CB_HeldItem);
        CB_Ability.SelectionChanged += (_, _) => ValidateComboBox2(CB_Ability);
        DEV_Ability.AttachClick(mods => ClickManualAbility(mods));
        TB_AbilityNumber.AttachClick(mods => ClickManualAbility(mods));
        CB_Language.SelectionChanged += (_, _) => UpdateNickname(CB_Language);
        CHK_IsEgg.IsCheckedChanged += (_, _) => UpdateIsEgg();
        CHK_Infected.IsCheckedChanged += (_, _) => UpdatePKRSInfected();
        CHK_Cured.IsCheckedChanged += (_, _) => UpdatePKRSCured();
        CB_PKRSStrain.SelectionChanged += (_, _) => UpdatePKRSstrain();
        CB_PKRSDays.SelectionChanged += (_, _) => UpdatePKRSdays();
        NUD_ShadowID.ValueChanged += (_, _) => UpdateShadowID();
        NUD_Purification.ValueChanged += (_, _) => UpdatePurification();
        CHK_Shadow.IsCheckedChanged += (_, _) => UpdateShadowCHK();

        // Met tab
        CB_GameOrigin.SelectionChanged += (_, _) => UpdateOriginGame();
        CB_BattleVersion.SelectionChanged += (_, _) => CB_BattleVersion_SelectedValueChanged();
        CB_MetLocation.SelectionChanged += (_, _) => ValidateLocation(CB_MetLocation);
        CB_EggLocation.SelectionChanged += (_, _) => ValidateLocation(CB_EggLocation);
        Label_MetLocation.AttachClick(m => _ = ClickMetLocation());
        CB_Ball.SelectionChanged += (_, _) => { ValidateComboBox2(CB_Ball); UpdateBall(); };
        Label_Ball.AttachClick(mods => _ = ClickBall(mods));
        PB_Ball.AttachClickHandled(mods => _ = ClickBall(mods));
        CHK_AsEgg.IsCheckedChanged += (_, _) => UpdateMetAsEgg();
        L_ObedienceLevel.AttachClick(_ => L_Obedience_Click());

        // Moves tab
        GB_CurrentMoves.AttachClick(mods => _ = ClickMoves(GB_CurrentMoves, mods));
        GB_RelearnMoves.AttachClick(mods => _ = ClickMoves(GB_RelearnMoves, mods));
        Label_CurPP.AttachClick(_ => ClickPP());
        Label_PPups.AttachClick(mods => ClickPPUps(mods));
        foreach (var cb in Relearn)
            cb.SelectionChanged += (s, _) => { ValidateMove(s); ValidateComboBox2(s); };
        CB_AlphaMastered.SelectionChanged += (s, _) => ValidateMove(s);
        B_RelearnFlags.Click += (_, _) => Run(B_Records_Click);
        B_MoveShop.Click += (_, _) => Run(B_MoveShop_Click);
        B_PlusRecord.Click += (_, _) => Run(B_PlusRecord_Click);

        // Cosmetic tab
        foreach (var pb in Markings)
            pb.AttachClickHandled(_ => ClickMarking(pb));
        PB_Favorite.AttachClickHandled(_ => ClickFavorite());
        PB_MarkShiny.AttachClickHandled(_ => PB_MarkShiny_Click());
        PB_MarkCured.AttachClickHandled(_ => PB_MarkCured_Click());
        PB_Origin.AttachClickHandled(_ => ClickVersionMarking(PB_Origin));
        PB_BattleVersion.AttachClickHandled(_ => ClickVersionMarking(PB_BattleVersion));

        // OT/Misc tab
        Label_OT.AttachClick(_ => ClickOT());
        Label_PrevOT.AttachClick(_ => ClickCT());
        GB_OT.AttachClick(_ => ClickGT(GB_OT));
        GB_nOT.AttachClick(_ => ClickGT(GB_nOT));
        TB_OT.OnTextChanged(_ => RefreshFontWarningButton());
        TB_Nickname.AttachClick(mods => _ = UpdateNicknameClick(TB_Nickname, mods));
        TB_OT.AttachClick(mods => _ = UpdateNicknameClick(TB_OT, mods));
        TB_HT.AttachClick(mods => _ = UpdateNicknameClick(TB_HT, mods));
        BTN_OTNameWarn.Click += (_, _) => _ = FontWarn(TB_OT.Text ?? string.Empty, MsgPKMOTNameWarn, BTN_OTNameWarn);
        TB_HT.OnTextChanged(_ => UpdateNotOT());
        Label_Friendship.AttachClick(mods => ClickFriendship(mods));
        Label_HatchCounter.AttachClick(mods => ClickFriendship(mods));
        TB_Friendship.OnTextChanged(_ => Update255_MTB(TB_Friendship));
        TB_FriendshipHT.OnTextChanged(_ => Update255_MTB(TB_FriendshipHT));
        CB_Country.SelectionChanged += (_, _) => UpdateCountry(CB_Country);
        CB_Handler.SelectionChanged += (_, _) => ChangeHandlerIndex();
        CB_ExtraBytes.SelectionChanged += (_, _) => UpdateExtraByteIndex();
        TB_ExtraByte.OnTextChanged(_ => UpdateExtraByteValue(TB_ExtraByte));
        TB_HomeTracker.OnTextChanged(_ => Update_ID64(TB_HomeTracker));
        BTN_RerollEC.Click += (_, _) => UpdateRandomEC();
        TB_PID.OnTextChanged(_ => { Update_ID(); UpdateTSV(); });
        TB_EC.OnTextChanged(_ => Update_ID());

        BTN_Ribbons.Click += (_, _) => Run(OpenRibbons);
        BTN_Medals.Click += (_, _) => Run(OpenSuperTrainRegimen);
        BTN_History.Click += (_, _) => Run(OpenHistory);
    }

    private void ClickManualAbility(KeyModifiers mods)
    {
        if (mods != KeyModifiers.Control)
            return;
        var value = TB_AbilityNumber.IntValue;
        if (value is not (1 or 2 or 4))
            return;

        var pk = Entity;
        IPersonalAbility pi;
        if (pk is PA9 pa9)
        {
            var la = new LegalityAnalysis(pa9);
            var enc = la.EncounterMatch;
            pi = PersonalTable.ZA[enc.Species, enc.Form];
        }
        else
        {
            pi = Entity.PersonalInfo;
        }
        DEV_Ability.SetValue(pi.GetAbilityAtIndex(value >> 1));
    }

    private sealed class ValidationRequiredSet(Control[] controls, Func<PKM, bool> shouldCheck, Func<Control, bool> isState)
    {
        public Control[] Controls => controls;

        public Control? IsNotValid(PKM pk)
        {
            if (!shouldCheck(pk))
                return null;
            return Array.Find(controls, z => isState(z));
        }
    }

    public void InitializeBinding()
    {
        IsEditorInitialized = true;
    }

    private void UpdateStats()
    {
        Stats.UpdateStats();
        if (Entity is IScaledSizeAbsolute)
            SizeCP.TryResetStats();
        StatusView.LoadPKM(Entity);
    }

    private void LoadPartyStats(PKM pk) => Stats.LoadPartyStats(pk);

    private void SavePartyStats(PKM pk) => Stats.SavePartyStats(pk);

    public PKM CurrentPKM { get => PreparePKM(); set => Entity = value; }
    public bool ModifyPKM { private get; set; } = true;

    public bool HideSecretValues
    {
        private get;
        set
        {
            field = value;
            var sav = RequestSaveFile;
            ToggleSecrets(field, sav.Generation);
        }
    }

    public DrawConfig Draw { private get; set; } = new();
    public bool Unicode { get; set; } = true;

    public bool HaX
    {
        get;
        set => field = Stats.HaX = value;
    }

    private byte[] LastData { get; set; } = [];
    public void NotifyWasExported(PKM pk) => LastData = pk.Data.ToArray();

    public PKM Data => Entity;
    public PKM Entity { get; private set; }
    public bool FieldsLoaded { get; private set; }
    public bool ChangingFields { get; set; }

    /// <summary>
    /// Currently loaded met location group that is populating Met and Egg location comboboxes
    /// </summary>
    private GameVersion origintrack;

    private EntityContext originFormat = EntityContext.None;

    private Action GetFieldsfromPKM = null!;
    private Func<PKM> GetPKMfromFields = null!;

    /// <summary>
    /// Latest legality check result used to show legality indication.
    /// </summary>
    private LegalityAnalysis Legality = null!;

    private readonly LegalMoveSource<ComboItem> LegalMoveSource = new(new LegalMoveComboSource());

    private IReadOnlyList<string> gendersymbols = GameInfo.GenderSymbolUnicode;

    public event Action<bool>? LegalityChanged;
    public event EventHandler? UpdatePreviewSprite;
    public event EventHandler? RequestShowdownImport;
    public event EventHandler? RequestShowdownExport;
    public Func<SaveFile> SaveFileRequested { get; set; } = () => FakeSaveFile.Default;

    private readonly Image[] relearnPB;
    public SaveFile RequestSaveFile => SaveFileRequested();
    public bool PKMIsUnsaved => FieldsLoaded && LastData.AsSpan().ContainsAnyExcept<byte>(0) && !CurrentPKM.Data.SequenceEqual(LastData);

    private readonly MoveChoiceView[] Moves;
    private readonly ComboBox[] Relearn;
    private readonly ValidationRequiredSet[] ValidatedControls;
    private readonly Image[] Markings;

    private bool forceValidation;

    private Window? OwnerWindow => TopLevel.GetTopLevel(this) as Window;

    public PKM PreparePKM(bool click = true)
    {
        if (click)
        {
            forceValidation = true;
            ValidateAllComboBoxes();
            forceValidation = false;
        }

        var pk = GetPKMfromFields();
        return pk.Clone();
    }

    private void ValidateAllComboBoxes()
    {
        foreach (var set in ValidatedControls)
        {
            foreach (var c in set.Controls)
                ValidateComboBox(c);
        }
    }

    public bool EditsComplete
    {
        get
        {
            // Find the first unfilled control, indicate as invalid.
            var invalid = GetInvalidParentTab();
            if (invalid is null)
                return true; // No issue.

            if (MainWindow.CurrentModifiers == (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt))
                return true; // Override

            TC_Editor.SelectedItem = invalid; // no system sound available; show the offending tab instead
            return false;
        }
    }

    private TabItem? GetInvalidParentTab()
    {
        if (!Stats.Valid)
            return Tab_Stats;
        if (CB_Species.GetValue() == 0 && !HaX) // can't set an empty slot...
            return Tab_Main;
        foreach (var type in ValidatedControls)
        {
            var cb = type.IsNotValid(Entity);
            if (cb is null)
                continue;
            return GetParentTab(cb) ?? throw new InvalidOperationException("Expected a tab parent.");
        }
        return null;
    }

    private static TabItem? GetParentTab(Control c)
    {
        Control? current = c;
        while (current is not null)
        {
            if (current is TabItem t)
                return t;
            current = current.Parent as Control;
        }
        return null;
    }

    public void SetPKMFormatMode(PKM pk)
    {
        // Load Extra Byte List
        SetPKMFormatExtraBytes(pk);
        (GetFieldsfromPKM, GetPKMfromFields) = GetLoadSet(pk);
        foreach (var move in Moves)
            move.SetContext(pk.Context);
    }

    private (Action Load, Func<PKM> Set) GetLoadSet(PKM pk) => GetLoadSet(pk.Context);

    private (Action Load, Func<PKM> Set) GetLoadSet(EntityContext context) => context switch
    {
        EntityContext.Gen1 => (PopulateFieldsPK1, PreparePK1),
        EntityContext.Gen2 => (PopulateFieldsPK2, PreparePK2),
        EntityContext.Gen3 => (PopulateFieldsPK3, PreparePK3),
        EntityContext.Gen4 => (PopulateFieldsPK4, PreparePK4),
        EntityContext.Gen5 => (PopulateFieldsPK5, PreparePK5),
        EntityContext.Gen6 => (PopulateFieldsPK6, PreparePK6),
        EntityContext.Gen7 => (PopulateFieldsPK7, PreparePK7),
        EntityContext.Gen8 => (PopulateFieldsPK8, PreparePK8),
        EntityContext.Gen9 => (PopulateFieldsPK9, PreparePK9),

        EntityContext.Gen7b => (PopulateFieldsPB7, PreparePB7),
        EntityContext.Gen8a => (PopulateFieldsPA8, PreparePA8),
        EntityContext.Gen8b => (PopulateFieldsPB8, PreparePB8),
        EntityContext.Gen9a => (PopulateFieldsPA9, PreparePA9),
        _ => throw new ArgumentOutOfRangeException(nameof(context), context, null),
    };

    private void SetPKMFormatExtraBytes(PKM pk)
    {
        var extraBytes = pk.ExtraBytes;
        L_ExtraBytes.IsVisible = FLP_ExtraBytes.IsVisible = FLP_ExtraBytes.IsEnabled = extraBytes.Length != 0;
        CB_ExtraBytes.Items.Clear();
        foreach (var b in extraBytes)
            CB_ExtraBytes.Items.Add($"0x{b:X2}");
        if (FLP_ExtraBytes.IsEnabled)
            CB_ExtraBytes.SelectedIndex = 0;
    }

    public void PopulateFields(PKM pk, bool focus = true, bool skipConversionCheck = false) => LoadFieldsFromPKM(pk, focus, skipConversionCheck);

    private void LoadFieldsFromPKM(PKM pk, bool focus = true, bool skipConversionCheck = true)
    {
        if (focus)
            TC_Editor.SelectedItem = Tab_Main;

        var input = pk;
        if (!skipConversionCheck && !EntityConverter.TryMakePKMCompatible(pk, Entity, out var c, out pk))
        {
            var msg = c.GetDisplayString(input, Entity.GetType());
            _ = AppDialogs.Alert(OwnerWindow, msg);
            return;
        }

        FieldsLoaded = false;

        Entity = pk.Clone();

#if !DEBUG
        try { GetFieldsfromPKM(); }
        catch { }
#else
        GetFieldsfromPKM();
#endif

        Stats.UpdateIVs(null);
        UpdatePKRSInfected();
        UpdatePKRSCured();
        UpdateNatureModification(CB_StatAlignment, Entity.StatAlignment);

        if (HaX)
        {
            if (pk.PartyStatsPresent) // stats present
                Stats.LoadPartyStats(pk);
        }
        FieldsLoaded = true;

        UpdateAffixed(pk);
        SetMarkings();
        UpdateLegality();
        UpdateSprite();
        NotifyWasExported(PreparePKM());
        RefreshFontWarningButton();
    }

    public void UpdateLegality(LegalityAnalysis? la = null, UpdateLegalityArgs args = 0)
    {
        if (!FieldsLoaded)
            return;

        Legality = la ?? new LegalityAnalysis(Entity, RequestSaveFile.Personal);
        if (!Legality.Parsed || HaX || Entity.Species == 0)
        {
            MC_Move1.HideLegality = MC_Move2.HideLegality = MC_Move3.HideLegality = MC_Move4.HideLegality = true;
            PB_WarnRelearn1.IsVisible = PB_WarnRelearn2.IsVisible = PB_WarnRelearn3.IsVisible = PB_WarnRelearn4.IsVisible = false;
            LegalityChanged?.Invoke(Legality.Valid);
            return;
        }
        PB_WarnRelearn1.IsVisible = PB_WarnRelearn2.IsVisible = PB_WarnRelearn3.IsVisible = PB_WarnRelearn4.IsVisible = true;
        MC_Move1.HideLegality = MC_Move2.HideLegality = MC_Move3.HideLegality = MC_Move4.HideLegality = false;

        // Refresh Move Legality
        var info = Legality.Info;
        var moves = info.Moves;
        for (int i = 0; i < 4; i++)
            Moves[i].UpdateLegality(moves[i], Entity, i);

        if (Entity.Format >= 6)
        {
            var relearn = info.Relearn;
            for (int i = 0; i < 4; i++)
                relearnPB[i].Source = MoveDisplayState.GetMoveImage(!relearn[i].Valid, Entity, i);
        }

        if (args.HasFlag(UpdateLegalityArgs.SkipMoveRepopulation))
            return;
        // Resort moves
        FieldsLoaded = false;
        LegalMoveSource.ReloadMoves(Legality);
        FieldsLoaded = true;
        LegalityChanged?.Invoke(Legality.Valid);
    }

    public void UpdateUnicode(IReadOnlyList<string> symbols)
    {
        gendersymbols = symbols;
        BTN_Shinytize.Content = Unicode ? Draw.ShinyUnicode : Draw.ShinyDefault;
    }

    public void UpdateSprite()
    {
        if (FieldsLoaded && !forceValidation)
            UpdatePreviewSprite?.Invoke(this, EventArgs.Empty);
    }

    // General Use Functions //
    private void SetDetailsOT<T>(T tr) where T : ITrainerInfo, ITrainerID32
    {
        if (string.IsNullOrWhiteSpace(tr.OT))
            return;

        // Get Save Information
        TB_OT.Text = tr.OT;
        UC_OTGender.Gender = (byte)(tr.Gender & 1);
        TID_Trainer.LoadTrainer(tr, tr.Generation);

        if (tr.Version.IsValidSavedVersion())
            CB_GameOrigin.SetValue((int)tr.Version);

        var lang = tr.Language;
        if (lang <= 0)
            lang = (int)LanguageID.English;
        CB_Language.SetValue(lang);
        if (tr is IRegionOriginReadOnly o)
        {
            CB_3DSReg.SetValue(o.ConsoleRegion);
            CB_Country.SetValue(o.Country);
            CB_SubRegion.SetValue(o.Region);
        }

        // Copy OT trash bytes for sensitive games (Gen1/2)
        if (tr is SAV1 s1 && Entity is PK1 p1) s1.OriginalTrainerTrash.CopyTo(p1.OriginalTrainerTrash);
        else if (tr is SAV2 s2 && Entity is PK2 p2) s2.OriginalTrainerTrash.CopyTo(p2.OriginalTrainerTrash);

        UpdateNickname(this);
    }

    private void SetDetailsHT<T>(T tr) where T : ITrainerInfo
    {
        var trainer = tr.OT;
        if (trainer.Length == 0)
            return;

        if (!tr.IsOriginalHandler(Entity, false))
        {
            TB_HT.Text = trainer;
            UC_HTGender.Gender = (byte)(tr.Gender & 1);
            if (Entity is IHandlerLanguage)
                CB_HTLanguage.SetValue(tr.Language);
        }
        else if ((TB_HT.Text ?? string.Empty).Length != 0)
        {
            if (CB_HTLanguage.SelectedIndex == 0 && Entity is IHandlerLanguage)
                CB_HTLanguage.SetValue(tr.Language);
        }
    }

    private void SetForms()
    {
        var species = Entity.Species;
        var pi = RequestSaveFile.Personal[species];
        UC_Gender.AllowClick = pi.IsDualGender;

        bool hasForms = FormInfo.HasFormSelection(pi, species, Entity.Format);
        CB_Form.IsEnabled = CB_Form.IsVisible = Label_Form.IsVisible = hasForms;

        if (HaX && Entity.Format >= 4)
            Label_Form.IsVisible = true; // show with value entry textbox

        if (!hasForms)
        {
            if (HaX)
                return;
            Entity.Form = 0;
            if (CB_Form.GetItemCount() > 0)
                CB_Form.SelectedIndex = 0;
            return;
        }

        var str = GameInfo.Strings;
        var forms = FormConverter.GetFormList(species, str.types, str.forms, gendersymbols, Entity.Context);
        if (forms.Length <= 1) // no choices
            CB_Form.IsEnabled = CB_Form.IsVisible = Label_Form.IsVisible = false;
        else
            CB_Form.ItemsSource = forms;
    }

    private void SetAbilityList()
    {
        if (Entity.Format < 3) // no abilities
            return;

        if (Entity.Format > 3 && FieldsLoaded) // has forms
            Entity.Form = (byte)Math.Max(0, CB_Form.SelectedIndex); // update pk field for form specific abilities

        int ability = CB_Ability.SelectedIndex;

        bool tmp = FieldsLoaded;
        FieldsLoaded = false;
        var items = GameInfo.FilteredSources.GetAbilityList(Entity.PersonalInfo);
        if (Entity is { Context: EntityContext.Gen5, Species: (ushort)Species.Basculin, Form: 1 })
            items = [.. items, FilteredGameDataSource.GetAbilityItem(GameInfo.Strings.abilitylist, (int)Ability.Reckless, '*')];
        CB_Ability.SetItems(items);
        CB_Ability.SelectedIndex = Math.Clamp(ability, 0, items.Count - 1); // restore original index if available
        FieldsLoaded = tmp;
    }

    private void UpdateIsShiny()
    {
        // Set the Controls
        var type = ShinyExtensions.GetType(Entity);
        BTN_Shinytize.IsVisible = BTN_Shinytize.IsEnabled = type == Shiny.Never;
        PB_ShinyStar.IsVisible = type == Shiny.AlwaysStar;
        PB_ShinySquare.IsVisible = type == Shiny.AlwaysSquare;

        // Refresh Markings (for Shiny Star if applicable)
        SetMarkings();
    }

    private void SetMarkings()
    {
        SetOwnedImage(PB_MarkShiny, GetMarkSprite(PB_MarkShiny, !BTN_Shinytize.IsEnabled));
        SetOwnedImage(PB_MarkCured, GetMarkSprite(PB_MarkCured, CHK_Cured.IsChecked == true));

        SetOwnedImage(PB_Favorite, GetMarkSprite(PB_Favorite, Entity is IFavorite { IsFavorite: true }));
        PB_Origin.Source = GetOriginSprite(Entity);

        var pba = Markings;
        if (Entity is IAppliedMarkings<bool> b)
        {
            for (int i = 0; i < b.MarkingCount; i++)
                SetMarkingImage(pba[i], Draw.MarkDefault, b.GetMarking(i));
        }
        else if (Entity is IAppliedMarkings<MarkingColor> c)
        {
            for (int i = 0; i < pba.Length; i++)
            {
                var state = c.GetMarking(i);
                _ = Draw.GetMarkingColor(state, out var color);
                SetMarkingImage(pba[i], color, state != MarkingColor.None);
            }
        }
        return;

        void SetMarkingImage(Image pb, Color color, bool active)
        {
            var bmp = AppResources.GetSkBitmap(InitialImages[pb]);
            ArgumentNullException.ThrowIfNull(bmp);

            if (color.ToArgb() != Color.Black.ToArgb())
                bmp.ChangeAllColorTo(color);
            if (!active)
                bmp.ChangeOpacity(1 / 8f);
            SetOwnedImage(pb, bmp);
        }
    }

    /// <summary>
    /// Assigns a generated bitmap to the image control and disposes the previously generated one.
    /// </summary>
    private void SetOwnedImage(Image pb, SkiaSharp.SKBitmap? bmp)
    {
        if (OwnedImages.Remove(pb, out var old))
            old.Dispose();
        if (bmp is null)
        {
            pb.Source = null;
            return;
        }
        var converted = bmp.ToAvaloniaBitmapAndDispose();
        OwnedImages[pb] = converted;
        pb.Source = converted;
    }

    private static global::Avalonia.Media.Imaging.Bitmap? GetOriginSprite(PKM pk)
    {
        var name = GetOriginSpriteResource(pk);
        if (name is null)
            return null;
        return App.IsDarkModeEnabled ? AppResources.GetImageBlackToWhite(name) : AppResources.GetImage(name);
    }

    private static string? GetOriginSpriteResource(PKM pk) => OriginMarkUtil.GetOriginMark(pk) switch
    {
        OriginMark.Gen6Pentagon => "gen_6",
        OriginMark.Gen7Clover => "gen_7",
        OriginMark.Gen8Galar => "gen_8",
        OriginMark.Gen8Trio => "gen_bs",
        OriginMark.Gen8Arc => "gen_la",
        OriginMark.Gen9Paldea => "gen_sv",
        OriginMark.GameBoy => "gen_vc",
        OriginMark.Gen9ZA => "gen_za",
        OriginMark.GO => "gen_go",
        OriginMark.LetsGo => "gen_gg",
        _ => null,
    };

    private static void SetCountrySubRegion(ComboBox cb, string type)
    {
        int oldIndex = cb.SelectedIndex;
        cb.SetItems(Util.GetCountryRegionList(type, GameInfo.CurrentLanguage));

        if (oldIndex > 0 && oldIndex < cb.GetItemCount())
            cb.SelectedIndex = oldIndex;
    }

    // Prompted Updates of PKM //
    private void ClickFriendship(KeyModifiers mods)
    {
        var pk = Entity;
        bool worst = (mods == KeyModifiers.Control) ^ pk.IsEgg;
        var current = TB_Friendship.IntValue;
        var value = worst
            ? pk.IsEgg ? EggStateLegality.GetMinimumEggHatchCycles(pk) : 0
            : pk.IsEgg ? EggStateLegality.GetMaximumEggHatchCycles(pk) : current == 255 ? pk.PersonalInfo.BaseFriendship : 255;
        TB_Friendship.Text = value.ToString();
    }

    private void ClickGender()
    {
        var pi = Entity.PersonalInfo;
        if (!pi.IsDualGender)
        {
            var expect = pi.FixedGender();
            if (UC_Gender.Gender != expect)
                UC_Gender.Gender = expect;
            return; // can't toggle
        }

        var canToggle = UC_Gender.CanToggle();
        if (canToggle)
            UC_Gender.ToggleGender();
        var gender = UC_Gender.Gender;
        if (!canToggle)
            gender = UC_Gender.Gender = 0; // fix bad genders
        if (Entity.Format <= 2)
        {
            Stats.SetATKIVGender(gender);
            UpdateIsShiny();
        }
        else if (Entity.Format <= 4)
        {
            Entity.Version = (GameVersion)CB_GameOrigin.GetValue();
            Entity.Nature = (Nature)CB_Nature.GetValue();
            Entity.Form = (byte)Math.Max(0, CB_Form.SelectedIndex);

            Entity.SetPIDGender(gender);
            TB_PID.Text = Entity.PID.ToString("X8");
        }
        Entity.Gender = gender;

        if (EntityGender.GetFromString(CB_Form.GetText()) < 2) // Gendered Forms
            CB_Form.SelectedIndex = Math.Min(gender, CB_Form.GetItemCount() - 1);

        UpdatePreviewSprite?.Invoke(UC_Gender, EventArgs.Empty);
    }

    private void ClickPP()
    {
        foreach (var cb in Moves)
            cb.HealPP(Entity);
    }

    private void ClickPPUps(KeyModifiers mods)
    {
        bool min = (mods & KeyModifiers.Control) != 0 || !Legal.IsPPUpAvailable(Entity);
        if (min)
        {
            MC_Move1.PPUps = MC_Move2.PPUps = MC_Move3.PPUps = MC_Move4.PPUps = 0;
            return;
        }

        static int GetValue(ushort move) => Legal.IsPPUpAvailable(move) ? 3 : 0;
        foreach (var cb in Moves)
            cb.PPUps = GetValue(cb.SelectedMove);
    }

    private void ClickMarking(Image sender)
    {
        int index = Array.IndexOf(Markings, sender);
        Entity.ToggleMarking(index);
        SetMarkings();
    }

    private void ClickFavorite()
    {
        if (Entity is IFavorite pb7)
            pb7.IsFavorite ^= true;
        SetMarkings();
    }

    private void ClickOT() => SetDetailsOT(SaveFileRequested());
    private void ClickCT() => SetDetailsHT(SaveFileRequested());

    private async Task ClickBall(KeyModifiers mods)
    {
        Entity.Ball = (byte)CB_Ball.GetValue();
        if ((mods & KeyModifiers.Alt) != 0)
        {
            CB_Ball.SetValue((int)Ball.Poke);
            return;
        }
        if ((mods & KeyModifiers.Shift) != 0)
        {
            CB_Ball.SetValue((int)BallApplicator.ApplyBallLegalByColor(Entity));
            return;
        }

        var owner = OwnerWindow;
        if (owner is null)
            return;
        var frm = new BallBrowserWindow();
        frm.LoadBalls(Entity);
        await frm.ShowDialog(owner);
        if (!frm.WasBallChosen)
            return;

        // Set to the entity, then check the updated value.
        // Gen4 has split fields for HG/SS and D/P/Pt segregation. If the value refused to update, show the refused value.
        Entity.Ball = frm.BallChoice;
        CB_Ball.SetValue(Entity.Ball);
    }

    private async Task ClickMetLocation()
    {
        if (HaX)
            return;

        Entity = PreparePKM();
        UpdateLegality(args: UpdateLegalityArgs.SkipMoveRepopulation);
        if (Legality.Valid)
            return;
        if (!await SetSuggestedMetLocation())
            return;

        Entity = PreparePKM();
        UpdateLegality();
    }

    private void ClickGT(TextBlock sender)
    {
        if (!GB_nOT.IsVisible)
            return;

        byte handler = 0;
        if (sender == GB_OT)
            handler = 0;
        else if ((TB_HT.Text ?? string.Empty).Length != 0)
            handler = 1;
        UpdateHandlerSelected(handler);
    }

    private void ChangeHandlerIndex()
    {
        if (CB_Handler.SelectedIndex < 0)
            return;
        UpdateHandlerSelected((byte)(CB_Handler.SelectedIndex & 1));
    }

    private void UpdateHandlerSelected(byte handler)
    {
        Entity.CurrentHandler = handler;
        UpdateHandlingTrainerBackground(Entity.CurrentHandler);
    }

    private void ClickNature(TextBlock sender)
    {
        if (Entity.Format < 8)
            return;
        if (sender == Label_Nature)
            CB_Nature.SelectedIndex = CB_StatAlignment.SelectedIndex;
        else
            CB_StatAlignment.SelectedIndex = CB_Nature.SelectedIndex;
    }

    private async Task ClickMoves(TextBlock sender, KeyModifiers mods)
    {
        UpdateLegality(args: UpdateLegalityArgs.SkipMoveRepopulation);
        if (sender == GB_CurrentMoves)
        {
            bool random = mods == KeyModifiers.Control;
            if (!await SetSuggestedMoves(random))
                return;
        }
        else if (sender == GB_RelearnMoves)
        {
            if (!await SetSuggestedRelearnMoves())
                return;
        }
        else
        {
            return;
        }

        UpdateLegality();
    }

    private async Task<bool> SetSuggestedMoves(bool random = false, bool silent = false)
    {
        var moves = new ushort[4];
        Entity.GetMoveSet(moves, random);
        if (moves[0] == 0)
        {
            if (!silent)
                await AppDialogs.Alert(OwnerWindow, MsgPKMSuggestionFormat);
            return false;
        }

        var current = new ushort[4];
        Entity.GetMoves(current);
        var same = Entity.IsEgg ? current.AsSpan().SequenceEqual(moves) : IsAllElementsShared(current, moves);
        if (same)
            return false;

        if (!silent)
        {
            var msg = GetMoveListPrint(moves, GameInfo.Strings.movelist);
            if (DialogResult.Yes != await AppDialogs.Prompt(OwnerWindow, MessageBoxButtons.YesNo, MsgPKMSuggestionMoves, msg))
                return false;
        }

        Entity.SetMoves(moves);
        if (Entity is ITechRecord tr)
        {
            tr.ClearRecordFlags();
            var la = new LegalityAnalysis(Entity);
            tr.SetRecordFlags(moves, la.Info.EvoChainsAllGens.Get(Entity.Context));
        }
        FieldsLoaded = false;
        LoadMoves(Entity);
        ClickPP();
        FieldsLoaded = true;
        return true;
    }

    private static bool IsAllElementsShared(ReadOnlySpan<ushort> seq1, ReadOnlySpan<ushort> seq2)
    {
        foreach (var entry in seq2)
        {
            if (!seq1.Contains(entry))
                return false;
        }
        return true;
    }

    private async Task<bool> SetSuggestedRelearnMoves(bool silent = false)
    {
        if (Entity.Format < 6)
            return false;

        var moves = new ushort[4];
        Legality.GetSuggestedRelearnMoves(moves);
        var current = new ushort[4];
        Entity.GetRelearnMoves(current);
        if (moves.AsSpan().SequenceEqual(current))
            return false;

        if (!silent)
        {
            var msg = GetMoveListPrint(moves, GameInfo.Strings.movelist);
            if (DialogResult.Yes != await AppDialogs.Prompt(OwnerWindow, MessageBoxButtons.YesNo, MsgPKMSuggestionRelearn, msg))
                return false;
        }

        CB_RelearnMove4.SetValue(moves[3]);
        CB_RelearnMove3.SetValue(moves[2]);
        CB_RelearnMove2.SetValue(moves[1]);
        CB_RelearnMove1.SetValue(moves[0]);
        return true;
    }

    private static string GetMoveListPrint(ReadOnlySpan<ushort> moves, ReadOnlySpan<string> names)
    {
        var sb = new StringBuilder();
        foreach (var move in moves)
        {
            if (move != 0)
                sb.AppendLine(names[move]);
        }
        return sb.ToString();
    }

    private async Task<bool> SetSuggestedMetLocation(bool silent = false)
    {
        var encounter = EncounterSuggestion.GetSuggestedMetInfo(Entity);
        if (encounter is null || (Entity.Format >= 3 && encounter.Location == 0))
        {
            if (!silent)
                await AppDialogs.Alert(OwnerWindow, MsgPKMSuggestionNone);
            return false;
        }

        var level = encounter.LevelMin;
        int minLevel = EncounterSuggestion.GetLowestLevel(Entity, level);
        if (minLevel == 0)
            minLevel = level;
        ushort location = encounter.Location;
        if (Entity.Format < 3 && encounter.Encounter is { } x && !x.Version.Contains(GameVersion.C))
            location = 0;

        if (Entity.CurrentLevel >= minLevel && Entity.MetLevel == level && Entity.MetLocation == location)
        {
            if (!encounter.HasGroundTile(Entity.Format) || CB_GroundTile.GetValue() == (int)encounter.GetSuggestedGroundTile())
                return false;
        }
        if (minLevel < level)
            minLevel = level;

        if (!silent)
        {
            var suggestions = EntitySuggestionUtil.GetMetLocationSuggestionMessage(Entity, level, location, minLevel, encounter.Encounter);
            if (suggestions.Count <= 1) // no suggestion
                return false;

            var msg = string.Join(Environment.NewLine, suggestions);
            if (await AppDialogs.Prompt(OwnerWindow, MessageBoxButtons.YesNo, msg) != DialogResult.Yes)
                return false;
        }

        if (Entity.Format >= 3)
        {
            Entity.MetLocation = location;
            TB_MetLevel.Text = encounter.GetSuggestedMetLevel(Entity).ToString();
            CB_MetLocation.SetValue(location);

            if (encounter.HasGroundTile(Entity.Format))
                CB_GroundTile.SetValue((int)encounter.GetSuggestedGroundTile());

            if (Entity is { Gen6: true, WasEgg: true } && ModifyPKM)
                Entity.SetHatchMemory6();
        }
        else
        {
            Entity.MetLocation = location;
            TB_MetLevel.Text = encounter.GetSuggestedMetLevel(Entity).ToString();
            CB_MetLocation.SetValue(location);
            CB_MetTimeOfDay.SelectedIndex = location == 0 ? 0 : encounter.GetSuggestedMetTimeOfDay();
        }

        if (Entity.CurrentLevel < minLevel)
            TB_Level.Text = minLevel.ToString();

        return true;
    }

    public void UpdateIVsGB(bool skipForm)
    {
        if (!FieldsLoaded)
            return;
        UC_Gender.Gender = Entity.Gender;
        if (Entity.Species == (int)Species.Unown && !skipForm)
            CB_Form.SelectedIndex = Entity.Form;

        UpdateIsShiny();
        UpdateSprite();
    }

    private void UpdateBall()
    {
        using var sprite = SpriteUtil.GetBallSprite((byte)CB_Ball.GetValue());
        SetOwnedImage(PB_Ball, sprite.CloneBitmap());
    }

    private void UpdateEXPLevel(Control sender)
    {
        if (ChangingFields || !IsFormatReady)
            return;
        ChangingFields = true;

        var pi = Entity.PersonalInfo;
        var gr = pi.EXPGrowth;
        if (sender == TB_EXP)
        {
            // Change the Level
            var expInput = TB_EXP.UIntValue;
            var expCalc = expInput;
            var lvlExp = Experience.GetLevel(expInput, gr);
            if (lvlExp == Experience.MaxLevel)
                expCalc = Experience.GetEXP(Experience.MaxLevel, gr);

            var lvlInput = Experience.ClampLevel((byte)TB_Level.IntValue);
            if (lvlInput != lvlExp)
                TB_Level.Text = lvlExp.ToString();
            if (expInput != expCalc && !HaX)
                TB_EXP.Text = expCalc.ToString();

            ExperienceBar.Update(expCalc, gr, lvlExp);
        }
        else
        {
            // Change the XP
            var input = TB_Level.IntValue;
            var level = (byte)Math.Clamp(input, Experience.MinLevel, Experience.MaxLevel);
            if (input != level && !string.IsNullOrWhiteSpace(TB_Level.Text))
                TB_Level.Text = level.ToString();

            var expCalc = Experience.GetEXP(level, gr);
            TB_EXP.Text = expCalc.ToString();
            ExperienceBar.Update(expCalc, gr, level);
        }
        ChangingFields = false;
        if (FieldsLoaded) // store values back
            Entity.EXP = TB_EXP.UIntValue;
        UpdateStats();
        UpdateLegality();
    }

    /// <summary>
    /// True once a save file context has been provided (data sources populated); guards early events during construction.
    /// </summary>
    private bool IsFormatReady => IsEditorInitialized && GetPKMfromFields is not null;

    private void UpdateRandomPID(Control sender)
    {
        if (Entity.Format < 3)
            return;
        if (FieldsLoaded)
            Entity.PID = TB_PID.UIntValue;

        if (sender == UC_Gender)
            Entity.SetPIDGender(Entity.Gender);
        else if (sender == CB_Nature && Entity.Nature != (Nature)CB_Nature.GetValue())
            Entity.SetPIDNature((Nature)CB_Nature.GetValue());
        else if (sender == BTN_RerollPID)
            Entity.SetPIDGender(Entity.Gender);
        else if (sender == CB_Ability && CB_Ability.SelectedIndex != Entity.PIDAbility && Entity.PIDAbility > -1)
            Entity.SetAbilityIndex(CB_Ability.SelectedIndex);

        TB_PID.Text = Entity.PID.ToString("X8");
        if (Entity.Format >= 6 && (Entity.Gen3 || Entity.Gen4 || Entity.Gen5))
            TB_EC.Text = TB_PID.Text;
        Update_ID();
    }

    private void UpdateRandomEC()
    {
        if (Entity.Format < 6)
            return;

        Entity.SetRandomEC();
        TB_EC.Text = Entity.EncryptionConstant.ToString("X8");
        Update_ID();
        UpdateLegality();
    }

    private void Update255_MTB(NumericTextBox tb)
    {
        if (!FieldsLoaded)
            return;
        if (tb.IntValue > byte.MaxValue)
            tb.Text = "255";
        if (tb == TB_Friendship && byte.TryParse(TB_Friendship.Text, out var value))
        {
            Entity.OriginalTrainerFriendship = value;
            UpdateStats();
        }
        else if (tb == TB_FriendshipHT && byte.TryParse(TB_FriendshipHT.Text, out var level))
        {
            Entity.HandlingTrainerFriendship = level;
            UpdateStats();
        }
    }

    private void UpdateFormArgument()
    {
        if (FieldsLoaded && Entity.Species == (int)Species.Alcremie)
            UpdateSprite();
    }

    private void UpdateForm(Control sender)
    {
        if (!IsFormatReady)
            return;
        if (FieldsLoaded && sender == CB_Form)
        {
            Entity.Form = (byte)Math.Max(0, CB_Form.SelectedIndex);
            uint exp = Experience.GetEXP(Entity.CurrentLevel, Entity.PersonalInfo.EXPGrowth);
            TB_EXP.Text = exp.ToString();
        }

        UpdateStats();
        SetAbilityList();

        // Gender Forms
        if (CB_Species.GetValue() == (int)Species.Unown && FieldsLoaded)
        {
            if (Entity.Format == 3)
            {
                Entity.SetPIDUnown3((byte)Math.Max(0, CB_Form.SelectedIndex));
                TB_PID.Text = Entity.PID.ToString("X8");
            }
            else if (Entity.Format == 2)
            {
                int desiredForm = CB_Form.SelectedIndex;
                while (Entity.Form != desiredForm)
                {
                    FieldsLoaded = false;
                    Stats.UpdateRandomIVs(KeyModifiers.None);
                    FieldsLoaded = true;
                }
            }
        }
        else if (CB_Form.IsEnabled && EntityGender.GetFromString(CB_Form.GetText()) < 2)
        {
            if (CB_Form.GetItemCount() == 2) // actually M/F; Pumpkaboo forms in German are S,M,L,XL
            {
                Entity.Gender = (byte)Math.Max(0, CB_Form.SelectedIndex);
                UC_Gender.Gender = Entity.GetSaneGender();
            }
        }
        else
        {
            UC_Gender.Gender = Entity.GetSaneGender();
        }

        RefreshFormArguments();
        if (ChangingFields)
            return;
        UpdateSprite();
    }

    private void RefreshFormArguments()
    {
        if (Entity is not IFormArgument f)
        {
            L_FormArgument.IsVisible = false;
            return;
        }

        if (FieldsLoaded)
            FA_Form.SaveArgument(f);
        L_FormArgument.IsVisible = FA_Form.LoadArgument(f, Entity.Species, Entity.Form, Entity.Context);
    }

    private void UpdatePKRSstrain()
    {
        if (!FieldsLoaded)
            return;

        // Change the PokerusState Days to the legal bounds.
        ChangePKRSstrainDropDownLists(-1, CB_PKRSStrain.SelectedIndex, CB_PKRSDays.SelectedIndex);
    }

    private void ChangePKRSstrainDropDownLists(int oldStrain, int newStrain, int currentDuration)
    {
        if (oldStrain == newStrain)
            return;

        CB_PKRSDays.Items.Clear();
        int max = Pokerus.GetMaxDuration(Math.Max(0, newStrain));
        for (int day = 0; day <= max; day++)
            CB_PKRSDays.Items.Add(day.ToString());

        // Set the days back if they're legal
        CB_PKRSDays.SelectedIndex = Math.Max(0, Math.Min(max, currentDuration));
    }

    private void UpdatePKRSdays()
    {
        if (!FieldsLoaded)
            return;

        var days = CB_PKRSDays.SelectedIndex;
        if (days != 0)
            return;

        // If no days are selected
        var strain = CB_PKRSStrain.SelectedIndex;
        if (Pokerus.IsSusceptible(strain, days))
            CHK_Cured.IsChecked = CHK_Infected.IsChecked = false; // No Strain = Never Cured / Infected, triggers Strain update
        else if (Pokerus.IsImmune(strain, days))
            CHK_Cured.IsChecked = true; // Any Strain = Cured
    }

    private void UpdatePKRSCured()
    {
        if (!FieldsLoaded)
            return;

        // Cured PokeRus is toggled
        if (CHK_Cured.IsChecked == true)
        {
            // If we're cured we have to have a strain infection.
            if (CB_PKRSStrain.SelectedIndex == 0)
                CB_PKRSStrain.SelectedIndex = 1;

            // Has Had PokeRus
            Label_PKRSdays.IsVisible = CB_PKRSDays.IsVisible = false;
            CB_PKRSDays.SelectedIndex = 0;

            Label_PKRS.IsVisible = CB_PKRSStrain.IsVisible = true;
            CHK_Infected.IsChecked = true;
        }
        else if (CHK_Infected.IsChecked != true)
        {
            // Not Infected, Disable the other
            Label_PKRS.IsVisible = CB_PKRSStrain.IsVisible = false;
            CB_PKRSStrain.SelectedIndex = 0;
        }
        else
        {
            // Still Infected for a duration
            Label_PKRSdays.IsVisible = CB_PKRSDays.IsVisible = true;
            CB_PKRSDays.SelectedIndex = Math.Min(1, CB_PKRSDays.Items.Count - 1);
        }
        // if not cured yet, days > 0
        if (CHK_Cured.IsChecked != true && CHK_Infected.IsChecked == true && CB_PKRSDays.SelectedIndex == 0)
            CB_PKRSDays.SelectedIndex = Math.Min(1, CB_PKRSDays.Items.Count - 1);

        SetMarkings();
    }

    private void UpdatePKRSInfected()
    {
        if (!FieldsLoaded)
            return;

        if (CHK_Cured.IsChecked == true)
        {
            if (CHK_Infected.IsChecked != true)
                CHK_Cured.IsChecked = false;
            return;
        }

        Label_PKRS.IsVisible = CB_PKRSStrain.IsVisible = CHK_Infected.IsChecked == true;
        if (CHK_Infected.IsChecked != true)
        {
            CB_PKRSStrain.SelectedIndex = 0;
            CB_PKRSDays.SelectedIndex = 0;
            Label_PKRSdays.IsVisible = CB_PKRSDays.IsVisible = false;
        }
        else if (CB_PKRSStrain.SelectedIndex == 0)
        {
            CB_PKRSStrain.SelectedIndex = 1;
            CB_PKRSDays.SelectedIndex = Math.Min(1, CB_PKRSDays.Items.Count - 1);
            Label_PKRSdays.IsVisible = CB_PKRSDays.IsVisible = true;
            UpdatePKRSCured();
        }
    }

    private void UpdateCountry(ComboBox c)
    {
        int index;
        if ((index = c.GetValue()) > 0)
            SetCountrySubRegion(CB_SubRegion, $"sr_{index:000}");
    }

    private void UpdateSpecies(Control sender)
    {
        if (!IsFormatReady)
            return;
        // Get Species dependent information
        if (FieldsLoaded)
            Entity.Species = (ushort)CB_Species.GetValue();
        ToolTip.SetTip(CB_Species, Entity.Species.ToString("000"));
        SetAbilityList();
        SetForms();
        UpdateForm(sender);

        if (!FieldsLoaded)
            return;

        // Recalculate EXP for Given Level
        uint exp = Experience.GetEXP(Entity.CurrentLevel, Entity.PersonalInfo.EXPGrowth);
        TB_EXP.Text = exp.ToString();

        // Check for Gender Changes
        UC_Gender.Gender = Entity.GetSaneGender();

        // If species changes and no nickname, set the new name == speciesName.
        if (CHK_NicknamedFlag.IsChecked != true)
            UpdateNickname(sender);

        UpdateLegality();
    }

    private void UpdateOriginGame()
    {
        if (!IsFormatReady)
            return;
        GameVersion version = (GameVersion)CB_GameOrigin.GetValue();
        if (version is 0 || version.IsValidSavedVersion())
        {
            CheckMetLocationChange(version, Entity.Context);
            if (FieldsLoaded)
                Entity.Version = version;
        }

        // Visibility logic for Gen 4 ground tile; only show for Gen 4 Pokémon.
        if (Entity is IGroundTile)
        {
            bool g4 = Entity.Gen4;
            CB_GroundTile.IsVisible = Label_GroundTile.IsVisible = g4 && Entity.Format < 7;
            if (FieldsLoaded && !g4)
                CB_GroundTile.SetValue((int)GroundTileType.None);
        }

        if (!FieldsLoaded)
            return;

        PB_Origin.Source = GetOriginSprite(Entity);
        TID_Trainer.LoadTrainer(Entity, Entity.Format);
        UpdateLegality();
    }

    private void CheckMetLocationChange(GameVersion version, EntityContext context)
    {
        // Does the list of locations need to be changed to another group?
        var group = GameUtil.GetMetLocationVersionGroup(version);
        if (group is GameVersion.Invalid)
        {
            var sav = RequestSaveFile;
            group = GameUtil.GetMetLocationVersionGroup(sav.Version);
            if (group is GameVersion.Invalid || version is GameVersion.Any)
                version = group = context.GetSingleGameVersion();
        }
        if (group != origintrack || context != originFormat)
            ReloadMetLocations(version, context);
        origintrack = group;
        originFormat = context;
    }

    private void ReloadMetLocations(GameVersion version, EntityContext context)
    {
        var metList = GameInfo.GetLocationList(version, context, egg: false);
        CB_MetLocation.SetItems(metList);

        var eggList = GameInfo.GetLocationList(version, context, egg: true);
        CB_EggLocation.SetItems(eggList);

        if (FieldsLoaded)
        {
            SetMarkings(); // Set/Remove the Nativity marking when gamegroup changes too
            var metLoc = EncounterSuggestion.TryGetSuggestedTransferLocation(Entity);
            var eggLoc = CHK_AsEgg.IsChecked == true
                ? EncounterSuggestion.GetSuggestedEncounterEggLocationEgg(Entity, true)
                : LocationEdits.GetNoneLocation(Entity);

            CB_MetLocation.SetValue(Math.Max((ushort)0, metLoc));
            CB_EggLocation.SetValue(eggLoc);
        }
        else
        {
            ValidateComboBox(CB_MetLocation); // hacky validation forcing
            ValidateComboBox(CB_EggLocation);
        }
    }

    private void UpdateExtraByteValue(NumericTextBox mtb)
    {
        if (!FieldsLoaded || CB_ExtraBytes.Items.Count == 0)
            return;
        // Changed Extra Byte's Value
        var value = mtb.IntValue;
        if (value > byte.MaxValue)
        {
            mtb.Text = "255";
            return; // above statement triggers the event again.
        }

        var text = CB_ExtraBytes.GetText();
        if (text.Length == 0)
            return;
        int offset = Convert.ToInt32(text, 16);
        Entity.Data[offset] = (byte)value;
    }

    private void UpdateExtraByteIndex()
    {
        if (CB_ExtraBytes.Items.Count == 0 || CB_ExtraBytes.SelectedIndex < 0)
            return;
        // Byte changed, need to refresh the Text box for the byte's value.
        var offset = Convert.ToInt32(CB_ExtraBytes.GetText(), 16);
        TB_ExtraByte.Text = Entity.Data[offset].ToString();
    }

    public void ChangeNature(Nature newNature)
    {
        if (Entity.Format < 3)
            return;

        var cb = Entity.Format >= 8 ? CB_StatAlignment : CB_Nature;
        cb.SetValue((int)newNature);
    }

    private void UpdateNatureModification(ComboBox cb, Nature nature)
    {
        string text = Stats.UpdateNatureModification(nature);
        ToolTip.SetTip(cb, text);
    }

    private void UpdateIsNicknamed()
    {
        if (!FieldsLoaded)
            return;

        RefreshFontWarningButton();
        var update = TB_Nickname.Text ?? string.Empty;
        Entity.Nickname = update;
        if (CHK_NicknamedFlag.IsChecked == true)
            return;

        var species = (ushort)CB_Species.GetValue();
        if (species is 0 || species > Entity.MaxSpeciesID)
            return;

        if (!IsPossibleNotNicknamed(Entity, update))
            CHK_NicknamedFlag.IsChecked = true;
    }

    private static bool IsPossibleNotNicknamed(PKM pk, ReadOnlySpan<char> current)
    {
        var species = pk.Species;
        if (pk.IsEgg)
            species = 0; // get the egg name.

        var context = pk.Context;
        if (!SpeciesName.IsNicknamedAnyLanguage(species, current, context))
            return true;

        // Auto-decapitalization did not happen until Gen6.
        // If transferred from Gen3/4=>Gen5, it can be either ALL-CAPS or decapitalized.
        if (pk.IsEgg)
            return false;
        if (context != EntityContext.Gen5 || species > 493 || pk.Gen5)
            return false;

        if (!SpeciesName.IsNicknamedAnyLanguage(species, current, EntityContext.Gen4))
            return true;
        return false;
    }

    private void UpdateNicknameLabel(KeyModifiers mods)
    {
        if (!FieldsLoaded)
            return;
        switch (mods)
        {
            case KeyModifiers.Control: RequestShowdownImport?.Invoke(this, EventArgs.Empty); return;
            case KeyModifiers.Alt: RequestShowdownExport?.Invoke(this, EventArgs.Empty); return;
        }
    }

    private void UpdateNickname(object sender)
    {
        if (!FieldsLoaded)
            return;

        if (CHK_NicknamedFlag.IsChecked == true)
            return;

        // Fetch Current Species and set it as Nickname Text
        var species = (ushort)CB_Species.GetValue();
        if (species is 0 || species > Entity.MaxSpeciesID)
        {
            TB_Nickname.Text = string.Empty;
            return;
        }

        var current = TB_Nickname.Text ?? string.Empty;
        if (IsPossibleNotNicknamed(Entity, current))
            return;

        string nickname;
        int language = CB_Language.GetValue();
        if (CHK_IsEgg.IsChecked == true)
        {
            // Get the egg name.
            nickname = SpeciesName.GetEggName(language, Entity.Format);
        }
        else
        {
            // If name is that of another language, don't replace the nickname
            if (sender != CB_Language && !SpeciesName.IsNicknamedAnyLanguage(species, current, Entity.Context))
                return;
            nickname = SpeciesName.GetSpeciesNameGeneration(species, language, Entity.Format);
        }

        TB_Nickname.Text = nickname;
        if (Entity is GBPKM pk)
            pk.SetNotNicknamed(language);
    }

    private void UpdateNotOT()
    {
        var text = TB_HT.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            ClickGT(GB_OT); // Switch CT over to OT.
            UC_HTGender.IsVisible = false;
            UC_HTGender.Gender = 0;
            ToggleHandlerVisibility(false);
        }
        else if (!UC_HTGender.IsVisible)
        {
            ToggleHandlerVisibility(true);
        }
    }

    private void UpdateIsEgg()
    {
        // Display hatch counter if it is an egg, Display Friendship if it is not.
        Label_HatchCounter.IsVisible = CHK_IsEgg.IsChecked == true && Entity.Format > 1;
        Label_Friendship.IsVisible = CHK_IsEgg.IsChecked != true && Entity.Format > 1;

        if (!FieldsLoaded)
            return;

        if (Entity.Format == 3 && CHK_IsEgg.IsChecked == true)
            Entity.OriginalTrainerName = TB_OT.Text ?? string.Empty; // going to be remapped

        Entity.IsEgg = CHK_IsEgg.IsChecked == true;
        if (CHK_IsEgg.IsChecked == true)
        {
            TB_Friendship.Text = EggStateLegality.GetMinimumEggHatchCycles(Entity).ToString();

            // If we are an egg, it won't have a met location.
            CHK_AsEgg.IsChecked = true;
            GB_EggConditions.IsEnabled = true;

            SetDate(CAL_MetDate, new DateTime(2000, 01, 01));

            // if egg wasn't originally obtained by OT => Link Trade, else => None
            if (Entity.Format >= 4)
            {
                var sav = SaveFileRequested();
                bool isTraded = sav.OT != TB_OT.Text || sav.TID16 != Entity.TID16 || sav.SID16 != Entity.SID16;
                var loc = isTraded
                    ? Locations.TradedEggLocation(sav.Generation, sav.Version)
                    : LocationEdits.GetNoneLocation(Entity);
                CB_MetLocation.SetValue(loc);
            }
            else if (Entity.Format == 3)
            {
                CB_Language.SetValue(Entity.Language); // JPN
                TB_OT.Text = Entity.OriginalTrainerName;
            }

            CHK_NicknamedFlag.IsChecked = EggStateLegality.IsNicknameFlagSet(Entity);
            TB_Nickname.Text = SpeciesName.GetEggName(CB_Language.GetValue(), Entity.Format);

            // Wipe egg memories
            if (Entity.Format >= 6 && ModifyPKM)
                Entity.ClearMemories();

            if (Entity is PK9) // Eggs in S/V have a Version value of 0 until hatched.
                CB_GameOrigin.SetValue(0);
        }
        else // Not Egg
        {
            if (CHK_NicknamedFlag.IsChecked != true)
                UpdateNickname(this);

            TB_Friendship.Text = Entity.PersonalInfo.BaseFriendship.ToString();

            if (CB_EggLocation.SelectedIndex == 0)
            {
                SetDate(CAL_MetDate, DateTime.Now);
                SetDate(CAL_EggDate, new DateTime(2000, 01, 01));
                CHK_AsEgg.IsChecked = false;
                GB_EggConditions.IsEnabled = false;
            }
            else
            {
                SetDate(CAL_MetDate, GetDate(CAL_EggDate));
                CB_MetLocation.SetValue(EncounterSuggestion.GetSuggestedEggMetLocation(Entity));
            }

            var nick = SpeciesName.GetEggName(CB_Language.GetValue(), Entity.Format);
            if (TB_Nickname.Text == nick)
                CHK_NicknamedFlag.IsChecked = false;
        }

        UpdateNickname(this);
        UpdateSprite();
    }

    private void UpdateMetAsEgg()
    {
        GB_EggConditions.IsEnabled = CHK_AsEgg.IsChecked == true;
        if (CHK_AsEgg.IsChecked == true)
        {
            if (!FieldsLoaded)
                return;

            SetDate(CAL_EggDate, DateTime.Now);

            bool isTradedEgg = Entity.IsEgg && Entity.Version != RequestSaveFile.Version;
            CB_EggLocation.SetValue(EncounterSuggestion.GetSuggestedEncounterEggLocationEgg(Entity, isTradedEgg));
            return;
        }
        if (!FieldsLoaded)
            return;
        // Remove egg met data
        CHK_IsEgg.IsChecked = false;
        SetDate(CAL_EggDate, new DateTime(2000, 01, 01));
        CB_EggLocation.SetValue(LocationEdits.GetNoneLocation(Entity));

        UpdateLegality();
    }

    private void UpdateShinyPID()
    {
        var mods = MainWindow.CurrentModifiers;
        var changePID = Entity.Format >= 3 && (mods & KeyModifiers.Alt) == 0;
        UpdateShiny(changePID, mods);
    }

    private void UpdateShiny(bool changePID, KeyModifiers mods)
    {
        Entity.PID = TB_PID.UIntValue;
        Entity.Nature = (Nature)CB_Nature.GetValue();
        Entity.Gender = UC_Gender.Gender;
        Entity.Form = (byte)Math.Max(0, CB_Form.SelectedIndex);
        Entity.Version = (GameVersion)CB_GameOrigin.GetValue();

        if (Entity.Format > 2)
        {
            var type = (mods & ~KeyModifiers.Alt) switch
            {
                KeyModifiers.Shift => Shiny.AlwaysSquare,
                KeyModifiers.Control => Shiny.AlwaysStar,
                _ => Shiny.Random,
            };
            if (changePID)
            {
                Entity.SetShiny(type);
                TB_PID.Text = Entity.PID.ToString("X8");

                var gen = Entity.Generation;
                bool pre3DS = gen is 3 or 4 or 5;
                if (pre3DS && Entity.Format >= 6)
                    TB_EC.Text = TB_PID.Text;
            }
            else
            {
                Entity.SetShinySID(type);
                TID_Trainer.LoadTrainer();
            }
        }
        else
        {
            Entity.SetShiny();
            LoadIVs(Entity);
            Stats.UpdateIVs(null);
        }

        UpdateIsShiny();
        UpdatePreviewSprite?.Invoke(this, EventArgs.Empty);
        UpdateLegality();
    }

    private void UpdateTSV()
    {
        if (Entity.Format <= 2 || !FieldsLoaded)
            return;

        TID_Trainer.SetToolTip();

        Entity.PID = TB_PID.UIntValue;
        var tip = $"PSV: {Entity.PSV:d4}";
        if (Entity.IsShiny)
            tip += $" | Xor = {Entity.ShinyXor}";
        ToolTip.SetTip(TB_PID, tip);
    }

    private void Update_ID()
    {
        if (!FieldsLoaded)
            return;
        // Trim out non-hexadecimal characters
        var pidText = (Entity.PID = TB_PID.UIntValue).ToString("X8");
        if (TB_PID.Text != pidText)
            TB_PID.Text = pidText;
        var ecText = (Entity.EncryptionConstant = TB_EC.UIntValue).ToString("X8");
        if (TB_EC.Text != ecText)
            TB_EC.Text = ecText;

        UpdateIsShiny();
        UpdateSprite();
        Stats.UpdateCharacteristic();   // If the EC is changed, EC%6 (Characteristic) might be changed.
        if (Entity.Format <= 4)
        {
            FieldsLoaded = false;
            Entity.PID = TB_PID.UIntValue;
            CB_Nature.SetValue((int)Entity.Nature);
            UC_Gender.Gender = Entity.Gender;
            UpdateNatureModification(CB_Nature, Entity.Nature);
            FieldsLoaded = true;
        }
    }

    private void Update_ID64(NumericTextBox sender)
    {
        if (!FieldsLoaded)
            return;
        // Trim out nonhex characters
        if (sender == TB_HomeTracker && Entity is IHomeTrack home)
        {
            var value = Util.GetHexValue64(TB_HomeTracker.Text ?? string.Empty);
            home.Tracker = value;
            var text = value.ToString("X16");
            if (TB_HomeTracker.Text != text)
                TB_HomeTracker.Text = text;
        }
    }

    private void UpdateShadowID()
    {
        if (!FieldsLoaded)
            return;
        FLP_Purification.IsVisible = (NUD_ShadowID.Value ?? 0) > 0;
    }

    private void UpdatePurification()
    {
        if (!FieldsLoaded)
            return;
        FieldsLoaded = false;
        var value = NUD_Purification.Value ?? 0;
        CHK_Shadow.IsChecked = Entity is CK3 ? value != CK3.Purified : value > 0;
        FieldsLoaded = true;
    }

    private void UpdateShadowCHK()
    {
        if (!FieldsLoaded)
            return;
        FieldsLoaded = false;
        NUD_Purification.Value = CHK_Shadow.IsChecked == true ? 1 : Entity is CK3 && (NUD_ShadowID.Value ?? 0) != 0 ? CK3.Purified : 0;
        ((IShadowCapture)Entity).Purification = (int)(NUD_Purification.Value ?? 0);
        UpdatePreviewSprite?.Invoke(this, EventArgs.Empty);
        FieldsLoaded = true;
    }

    private void ValidateComboBox(Control c)
    {
        if (c is not ComboBox cb)
            return;
        if (cb.SelectedItem is null && cb.GetItemCount() > 0)
        {
            cb.SelectedIndex = 0;
        }
        else if (cb.SelectedItem is null)
        {
            cb.SetInvalid(Draw.InvalidSelection);
        }
        else
        {
            cb.ResetColors();
        }
    }

    private void ValidateComboBox2(object? sender)
    {
        if (!FieldsLoaded || sender is not ComboBox cb)
            return;

        ValidateComboBox(cb);
        UpdateSprite();
        if (sender == CB_Ability)
        {
            if (Entity.Format >= 6)
                TB_AbilityNumber.Text = (1 << Math.Max(0, CB_Ability.SelectedIndex)).ToString();
            else if (Entity.Format <= 5 && CB_Ability.SelectedIndex < 2) // Format <= 5, not hidden
                UpdateRandomPID(CB_Ability);
            UpdateLegality();
        }
        else if (sender == CB_Nature)
        {
            if (Entity.Format <= 4)
                UpdateRandomPID(CB_Nature);
            Entity.Nature = (Nature)CB_Nature.GetValue();
            UpdateNatureModification(CB_Nature, Entity.Nature);
            Stats.UpdateIVs(null); // updating Nature will trigger stats to update as well
            UpdateLegality();
        }
        else if (sender == CB_StatAlignment)
        {
            Entity.StatAlignment = (Nature)CB_StatAlignment.GetValue();
            UpdateNatureModification(CB_StatAlignment, Entity.StatAlignment);
            Stats.UpdateIVs(null); // updating Nature will trigger stats to update as well
            UpdateLegality();
        }
        else if (sender == CB_HeldItem)
        {
            UpdateLegality();
        }
    }

    private void ValidateMove(object? sender)
    {
        if (!FieldsLoaded || sender is not ComboBox cb)
            return;

        ValidateComboBox(cb);

        // Store value back, repopulate legality.
        var value = (ushort)cb.GetValue();
        int index = Array.FindIndex(Moves, z => z.CB_Move == cb);
        if (index != -1)
        {
            Moves[index].HealPP(Entity);
            Entity.SetMove(index, value);
        }
        else if ((index = Array.IndexOf(Relearn, cb)) != -1)
        {
            Entity.SetRelearnMove(index, value);
        }
        else if (cb == CB_AlphaMastered && Entity is PA8 pa8)
        {
            pa8.AlphaMove = value;
        }
        else
        {
            // Shouldn't hit here.
            throw new InvalidOperationException();
        }
        UpdateLegality(args: UpdateLegalityArgs.SkipMoveRepopulation);
    }

    private static readonly Dictionary<byte, global::Avalonia.Media.Imaging.Bitmap?> TypeIconCache = [];

    private static global::Avalonia.Media.Imaging.Bitmap? GetTypeIcon(byte type)
    {
        if (TypeIconCache.TryGetValue(type, out var cached))
            return cached;
        using var img = TypeSpriteUtil.GetTypeSpriteIconSmall(type);
        var result = img?.ToAvaloniaBitmap();
        TypeIconCache[type] = result;
        return result;
    }

    /// <summary>
    /// Move list item: type icon + name, highlighted when the move can be learned (port of <c>ValidateMovePaint</c>).
    /// </summary>
    private IDataTemplate CreateMoveTemplate() => new FuncDataTemplate<ComboItem>((item, _) =>
    {
        var (text, value) = item;
        var valid = LegalMoveSource.Info.CanLearn((ushort)value) && !HaX;
        var type = MoveInfo.GetType((ushort)value, Entity.Context);
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        panel.Children.Add(new Image { Source = GetTypeIcon(type), Width = 16, Height = 16, Stretch = Stretch.None });
        panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return new Border
        {
            Background = valid ? ColorUtilAvalonia.ColorValid.ToBrush() : Brushes.Transparent,
            Padding = new global::Avalonia.Thickness(2, 0),
            Child = panel,
        };
    });

    private void ValidateMoveDropDown(object? sender)
    {
        if (sender is not ComboBox s)
            return;
        var index = Array.FindIndex(Moves, z => z.CB_Move == s);

        // Populating the combobox drop-down list is deferred until the dropdown is entered into at least once.
        // Saves some lag delays when viewing a pk.
        if (LegalMoveSource.Display.GetIsMoveBoxOrdered(index))
            return;
        SetMoveDataSource(s);
        LegalMoveSource.Display.SetIsMoveBoxOrdered(index, true);
    }

    private void SetMoveDataSource(ComboBox c)
    {
        FieldsLoaded = false;
        var index = c.GetValue();
        c.SetItems(LegalMoveSource.Display.DataSource);
        c.SetValue(index);
        FieldsLoaded = true;
    }

    private void ValidateLocation(ComboBox sender)
    {
        if (!FieldsLoaded)
            return;

        ValidateComboBox(sender);
        Entity.MetLocation = (ushort)CB_MetLocation.GetValue();
        Entity.EggLocation = (ushort)CB_EggLocation.GetValue();
        UpdateLegality();
        ToolTip.SetTip(CB_MetLocation, Entity.MetLocation.ToString("000"));
    }

    private void UpdateAffixed(PKM pk)
    {
        if (pk is IRibbonSetAffixed a)
        {
            var affixed = a.AffixedRibbon;
            if (affixed != AffixedRibbon.None)
            {
                SetOwnedImage(PB_Affixed, RibbonSpriteUtil.GetRibbonSprite((RibbonIndex)affixed));
                PB_Affixed.IsVisible = true;
                // Update the tooltip with the ribbon name.
                var name = GameInfo.Strings.Ribbons.GetNameSafe($"Ribbon{(RibbonIndex)affixed}", out var result) ? result : affixed.ToString();
                if (pk is IRibbonSetMarks { RibbonMarkCount: > 1 } y)
                    name += Environment.NewLine + GetRibbonAffixCount(y);
                ToolTip.SetTip(PB_Affixed, name);
                return;
            }
            if (pk is IRibbonSetMarks { RibbonMarkCount: not 0 } x)
            {
                SetOwnedImage(PB_Affixed, null);
                PB_Affixed.Source = AppResources.GetImage("ribbon_affix_none");
                PB_Affixed.IsVisible = true;
                ToolTip.SetTip(PB_Affixed, GetRibbonAffixCount(x));
                return;
            }
            static string GetRibbonAffixCount(IRibbonSetMarks x) => $"{x.RibbonMarkCount} available to affix.";
        }
        PB_Affixed.IsVisible = false;
    }

    /// <summary>
    /// Runs a fire-and-forget UI task, surfacing exceptions instead of losing them on an unobserved task.
    /// </summary>
    private void Run(Func<Task> action) => _ = RunAsync(action);

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await AppDialogs.Error(OwnerWindow, ex.Message, ex);
        }
    }

    private async Task OpenRibbons()
    {
        if (OwnerWindow is not { } owner)
            return;
        var form = new RibbonWindow(Entity);
        await form.ShowDialog(owner);

        UpdateAffixed(Entity);
    }

    private async Task OpenSuperTrainRegimen()
    {
        if (Entity is not ISuperTrainRegimen st || OwnerWindow is not { } owner)
            return;
        var form = new SuperTrainingWindow(st);
        await form.ShowDialog(owner);
    }

    private async Task OpenHistory()
    {
        if (OwnerWindow is not { } owner)
            return;

        // Write back current values that will be displayed in the popup form
        var pk = Entity;
        pk.IsEgg = CHK_IsEgg.IsChecked == true;
        pk.OriginalTrainerName = TB_OT.Text ?? string.Empty;
        pk.OriginalTrainerFriendship = (byte)TB_Friendship.IntValue;
        pk.HandlingTrainerName = TB_HT.Text ?? string.Empty;
        pk.HandlingTrainerFriendship = (byte)TB_FriendshipHT.IntValue;
        pk.CurrentHandler = (byte)CB_Handler.GetValue();
        var form = new MemoryAmieWindow(pk);
        await form.ShowDialog(owner);

        TB_Friendship.Text = pk.OriginalTrainerFriendship.ToString();
        TB_FriendshipHT.Text = pk.HandlingTrainerFriendship.ToString();
    }

    private async Task B_Records_Click()
    {
        if (Entity is not ITechRecord t)
            return;

        if (MainWindow.CurrentModifiers == KeyModifiers.Shift || OwnerWindow is not { } owner)
        {
            t.SetRecordFlags(Entity, TechnicalRecordApplicatorOption.LegalCurrent);
            UpdateLegality();
            return;
        }

        var form = new TechRecordWindow(t, Entity);
        await form.ShowDialog(owner);
        UpdateLegality();
    }

    private async Task B_MoveShop_Click()
    {
        if (Entity is not IMoveShop8Mastery m)
            return;

        if (MainWindow.CurrentModifiers == KeyModifiers.Shift || OwnerWindow is not { } owner)
        {
            m.ClearMoveShopFlags();
            var enc = Legality.EncounterMatch;
            if (enc is IMasteryInitialMoveShop8 shop)
                shop.SetInitialMastery(Entity, enc);
            m.SetMoveShopFlags(Entity);
            UpdateLegality();
            return;
        }

        var form = new MoveShopWindow(m, m, Entity);
        await form.ShowDialog(owner);
        UpdateLegality();
    }

    private async Task B_PlusRecord_Click()
    {
        if (Entity is not IPlusRecord m || Entity.PersonalInfo is not IPermitPlus p)
            return;

        if (MainWindow.CurrentModifiers.HasFlag(KeyModifiers.Shift) || OwnerWindow is not { } owner)
        {
            m.SetPlusFlags(Entity, p, PlusRecordApplicatorOption.LegalCurrent);
            UpdateLegality();
            return;
        }

        var form = new PlusRecordWindow(m, p, Entity);
        await form.ShowDialog(owner);
        UpdateLegality();
    }

    /// <summary>
    /// Ctrl+click on a name box opens the trash byte / special character editor (port of <c>UpdateNicknameClick</c>).
    /// </summary>
    private async Task UpdateNicknameClick(TextBox tb, KeyModifiers mods)
    {
        if (mods != KeyModifiers.Control || OwnerWindow is not { } owner)
            return;

        // Set the string back to the entity in the right spot, so the trash fetch has the latest data.
        byte[] trash;
        if (tb == TB_Nickname)
        {
            Entity.Nickname = tb.Text ?? string.Empty;
            trash = Entity.NicknameTrash.ToArray();
        }
        else if (tb == TB_OT)
        {
            Entity.OriginalTrainerName = tb.Text ?? string.Empty;
            trash = Entity.OriginalTrainerTrash.ToArray();
        }
        else if (tb == TB_HT)
        {
            Entity.HandlingTrainerName = tb.Text ?? string.Empty;
            trash = Entity.HandlingTrainerTrash.ToArray();
        }
        else
        {
            return;
        }

        var result = await TrashEditorWindow.ShowAsync(owner, tb, Entity, trash);
        if (result is null)
            return;

        // Write the edited trash bytes back into the entity's span.
        if (tb == TB_Nickname)
            result.CopyTo(Entity.NicknameTrash);
        else if (tb == TB_OT)
            result.CopyTo(Entity.OriginalTrainerTrash);
        else
            result.CopyTo(Entity.HandlingTrainerTrash);
    }

    /// <summary>
    /// Refreshes the interface for the current PKM format.
    /// </summary>
    /// <param name="sav">Save File context the editor is editing for</param>
    /// <param name="pk">Pokémon data to edit</param>
    public bool ToggleInterface(SaveFile sav, PKM pk)
    {
        Entity = sav.GetCompatiblePKM(pk);
        ToggleInterface(Entity);
        return FinalizeInterface(sav);
    }

    private void ToggleInterface(PKM t)
    {
        var pb7 = t is PB7;
        var format = t.Format;
        L_ShadowID.IsVisible = NUD_ShadowID.IsVisible = L_HeartGauge.IsVisible = FLP_Purification.IsVisible = t is IShadowCapture;
        bool sizeCP = format >= 8 || pb7;
        SizeCP.IsVisible = sizeCP;
        if (sizeCP)
            SizeCP.ToggleVisibility(t);
        PB_Favorite.IsVisible = t is IFavorite;
        PB_BattleVersion.IsVisible = FLP_BattleVersion.IsVisible = t is IBattleVersion;
        BTN_History.IsVisible = format >= 6 && !pb7;
        BTN_Ribbons.IsVisible = format >= 3; // pb7 has ribbons on HOME Meltan lol
        BTN_Medals.IsVisible = format is 6 or 7 && !pb7;
        L_ArrivedDateTime.IsVisible = FLP_ReceivedDateTime.IsVisible = pb7;
        Label_Country.IsVisible = CB_Country.IsVisible = Label_SubRegion.IsVisible = CB_SubRegion.IsVisible = Label_3DSRegion.IsVisible = CB_3DSReg.IsVisible = t is IRegionOrigin;
        FLP_Spirit7b.IsVisible = FLP_Mood7b.IsVisible = pb7;
        B_RelearnFlags.IsVisible = t is ITechRecord;
        B_MoveShop.IsVisible = t is IMoveShop8Mastery;
        B_PlusRecord.IsVisible = t is IPlusRecord;
        L_AlphaMastered.IsVisible = CB_AlphaMastered.IsVisible = FLP_AlphaMove.IsVisible = t is PA8;
        FLP_ObedienceLevel.IsVisible = t is IObedienceLevel;
        Contest.ToggleInterface(Entity, Entity.Context);
        if (t is not IFormArgument)
            L_FormArgument.IsVisible = false;
        StatusView.IsVisible = MainWindow.Settings.EntityEditor.ShowStatusCondition;
        ExperienceBar.IsVisible = MainWindow.Settings.EntityEditor.ShowExperienceBar;

        DEV_Ability.IsEnabled = DEV_Ability.IsVisible = (format > 3 && HaX) || t is PA9;
        ToggleInterface(Entity.Format);
    }

    private void ToggleSecrets(bool hidden, byte format)
    {
        Label_EncryptionConstant.IsVisible = FLP_EncryptionConstant.IsVisible = format >= 6 && !hidden;
        BTN_RerollPID.IsVisible = Label_PID.IsVisible = TB_PID.IsVisible = format >= 3 && !hidden;
        L_HomeTracker.IsVisible = TB_HomeTracker.IsVisible = format >= 8 && !hidden;
    }

    private void ToggleInterface(byte format)
    {
        ToggleSecrets(HideSecretValues, format);
        Label_PrevOT.IsVisible = FLP_HT.IsVisible = GB_nOT.IsVisible = FLP_Handler.IsVisible =
        FLP_Relearn4.IsVisible = FLP_Relearn3.IsVisible = FLP_Relearn2.IsVisible = FLP_Relearn1.IsVisible = GB_RelearnMoves.IsVisible = format >= 6;

        PB_Origin.IsVisible = format >= 6;
        L_NSparkle.IsVisible = CHK_NSparkle.IsVisible = FLP_PokeStarFame.IsVisible = format == 5;

        CHK_AsEgg.IsVisible = GB_EggConditions.IsVisible = PB_Mark5.IsVisible = PB_Mark6.IsVisible = format >= 4;
        ShinyLeaf.IsVisible = FLP_WalkingMood.IsVisible = format == 4;

        CB_Ability.IsVisible = !DEV_Ability.IsEnabled && format >= 3;
        Label_Nature.IsVisible = CB_Nature.IsVisible = format >= 3;
        L_StatAlignment.IsVisible = CB_StatAlignment.IsVisible = format >= 8;
        Label_Ability.IsVisible = FLP_AbilityRight.IsVisible = format >= 3;
        FLP_ExtraBytes.IsVisible = format >= 3;
        GB_Markings.IsVisible = format >= 3;
        CB_Form.IsEnabled = format >= 3;
        FA_Form.IsVisible = format >= 6;

        Label_Friendship.IsVisible = TB_Friendship.IsVisible = format >= 2;
        L_FriendshipHT.IsVisible = TB_FriendshipHT.IsVisible = format >= 6;
        L_LanguageHT.IsVisible = CB_HTLanguage.IsVisible = format >= 8;
        Label_HeldItem.IsVisible = CB_HeldItem.IsVisible = format >= 2;
        CHK_IsEgg.IsVisible = format >= 2;
        Label_PKRS.IsVisible = Label_PKRSdays.IsVisible = FLP_EggPKRSRight.IsVisible = format >= 2;
        UC_OTGender.IsVisible = format >= 2;
        UC_Gender.IsVisible = format >= 2 || (format == 1 && MainWindow.Settings.EntityEditor.ShowGenderGen1);
        L_CatchRate.IsVisible = CR_PK1.IsVisible = format == 1;

        // HaX override, needs to be after DEV_Ability enabled assignment.
        TB_AbilityNumber.IsVisible = format >= 6 && DEV_Ability.IsEnabled;

        // Met Tab
        FLP_MetDate.IsVisible = format >= 4;
        CHK_Fateful.IsVisible = FLP_Ball.IsVisible = FLP_OriginGame.IsVisible = format >= 3;
        FLP_MetLocation.IsVisible = FLP_MetLevel.IsVisible = format >= 2;
        FLP_GroundTile.IsVisible = format is 4 or 5 or 6;
        FLP_TimeOfDay.IsVisible = format == 2;

        Stats.ToggleInterface(Entity, format);
    }

    private bool FinalizeInterface(SaveFile sav)
    {
        FieldsLoaded = false;

        bool isTranslationRequired = false;
        PopulateFilteredDataSources(sav);
        PopulateFields(Entity);

        // Save File Specific Limits
        TB_OT.MaxLength = Entity.MaxStringLengthTrainer;
        TB_HT.MaxLength = Entity.MaxStringLengthTrainer;
        TB_Nickname.MaxLength = Entity.MaxStringLengthNickname;

        // Hide Unused Tabs
        Tab_Met.IsVisible = Entity.Format != 1;
        Tab_Cosmetic.IsVisible = Entity.Format > 2;

        if (!HaX && sav is SAV7b)
        {
            Label_HeldItem.IsVisible = CB_HeldItem.IsVisible = false;
            Label_Country.IsVisible = CB_Country.IsVisible = false;
            Label_SubRegion.IsVisible = CB_SubRegion.IsVisible = false;
            Label_3DSRegion.IsVisible = CB_3DSReg.IsVisible = false;
        }

        if (!HaX && sav is SAV8LA)
        {
            Label_HeldItem.IsVisible = CB_HeldItem.IsVisible = false;
        }

        // pk2 save files do not have an Origin Game stored. Prompt the met location list to update.
        if (Entity.Format == 2)
            CheckMetLocationChange(GameVersion.C, Entity.Context);
        return isTranslationRequired;
    }

    public Action<IBattleTemplate> LoadShowdownSet { get; set; }

    private void LoadShowdownSetDefault(IBattleTemplate set)
    {
        var pk = PreparePKM();
        pk.ApplySetDetails(set);
        PopulateFields(pk);
    }

    private void CB_BattleVersion_SelectedValueChanged()
    {
        if (Entity is not IBattleVersion b)
            return;
        var value = (GameVersion)CB_BattleVersion.GetValue();
        if (FieldsLoaded)
            b.BattleVersion = value;
        var sprite = GetMarkSprite(PB_BattleVersion, value != 0);
        if (App.IsDarkModeEnabled)
            sprite.ChangeAllColorTo(Color.White);
        SetOwnedImage(PB_BattleVersion, sprite);
    }

    private SkiaSharp.SKBitmap GetMarkSprite(Image p, bool opaque, double trans = 0.175)
    {
        var bmp = AppResources.GetSkBitmap(InitialImages[p]);
        ArgumentNullException.ThrowIfNull(bmp);
        if (!opaque)
            bmp.ChangeOpacity(trans);
        return bmp;
    }

    private void ClickVersionMarking(Image sender)
    {
        TC_Editor.SelectedItem = Tab_Met;
        if (sender == PB_BattleVersion)
            CB_BattleVersion.IsDropDownOpen = true;
        else
            CB_GameOrigin.IsDropDownOpen = true;
    }

    /// <summary>
    /// Re-applies localized size class names after a language change.
    /// </summary>
    public void TryResetSizeStats() => SizeCP.TryResetStats();

    public void ChangeLanguage(ITrainerInfo sav)
    {
        // Force an update to the met locations
        origintrack = GameVersion.Invalid;

        InitializeLanguage(sav);
    }

    private void L_Obedience_Click()
    {
        if (Entity is not IObedienceLevel l)
            return;

        var met = TB_MetLevel.IntValue;
        var metLevel = (byte)Math.Clamp(met, 0, 100);
        var suggest = l.GetSuggestedObedienceLevel(Entity, metLevel);

        var current = TB_ObedienceLevel.IntValue;
        if (suggest != current)
            TB_ObedienceLevel.Text = suggest.ToString();
    }

    private void InitializeLanguage(ITrainerInfo sav)
    {
        var source = GameInfo.FilteredSources;
        // Set the various ComboBox DataSources up with their allowed entries
        SetCountrySubRegion(CB_Country, "countries");
        CB_3DSReg.SetItems(source.ConsoleRegions);

        CB_GroundTile.SetItems(source.G4GroundTiles);
        CB_Nature.SetItems(source.Natures);
        CB_StatAlignment.SetItems(source.Natures);

        // Sub-editors
        Stats.InitializeDataSources();

        PopulateFilteredDataSources(sav, true);
    }

    private static void SetIfDifferentCount(IReadOnlyList<ComboItem> update, ComboBox exist, bool force = false)
    {
        if (!force && exist.ItemsSource is IReadOnlyCollection<ComboItem> b && b.Count == update.Count)
            return;
        exist.SetItems(update);
    }

    private void PopulateFilteredDataSources(ITrainerInfo sav, bool force = false)
    {
        var source = GameInfo.FilteredSources;
        SetIfDifferentCount(source.Languages, CB_Language, force);

        if (sav.Generation >= 2)
        {
            var game = sav.Version;
            if (game <= 0)
                game = Entity.Context.GetSingleGameVersion();
            else if (game is GameVersion.COLO or GameVersion.XD)
                game = GameVersion.CXD;
            CheckMetLocationChange(game, sav.Context);
            SetIfDifferentCount(source.Items, CB_HeldItem, force);
        }

        if (sav.Generation >= 3)
        {
            SetIfDifferentCount(source.Balls, CB_Ball, force);
            SetIfDifferentCount(source.Games, CB_GameOrigin, force);
        }

        if (sav.Generation >= 4)
            SetIfDifferentCount(source.Abilities, DEV_Ability, force);

        if (sav.Generation >= 8)
        {
            var lang = source.Languages;
            var langWith0 = new List<ComboItem>(1 + lang.Count) { GameInfo.Sources.Empty };
            langWith0.AddRange(lang);
            SetIfDifferentCount(langWith0, CB_HTLanguage, force);

            var game = source.Games;
            SetIfDifferentCount(game, CB_BattleVersion, force);
        }
        SetIfDifferentCount(source.Species, CB_Species, force);

        // Set the Move ComboBoxes too.
        LegalMoveSource.ChangeMoveSource(source.Moves);
        foreach (var cb in Relearn)
            SetIfDifferentCount(source.Relearn, cb, force);
        foreach (var cb in Moves)
            SetIfDifferentCount(source.Moves, cb.CB_Move, force);
        if (sav is SAV8LA)
            SetIfDifferentCount(source.Moves, CB_AlphaMastered, force);
    }

    private void RefreshFontWarningButton()
    {
        if (!FieldsLoaded)
            return;
        if (!IsFontDocumented(Entity))
        {
            BTN_NicknameWarn.IsVisible = BTN_OTNameWarn.IsVisible = false;
            return;
        }

        var context = Entity.Context;
        var langPk = (LanguageID)CB_Language.GetValue();
        var langSav = (LanguageID)RequestSaveFile.Language;
        var nickname = TB_Nickname.Text ?? string.Empty;

        // Gen 7 unnicknamed Chinese Pokémon will always be valid after remapping
        var isUnnicknamedChinese = Entity is PK7 && (SpeciesName.GetSpeciesNameLanguage(Entity.Species, (int)langPk, nickname, EntityContext.Gen7) is (int)LanguageID.ChineseS or (int)LanguageID.ChineseT);

        BTN_NicknameWarn.IsVisible = !isUnnicknamedChinese && StringFontUtil.HasUndefinedCharacters(nickname, context, langPk, langSav);
        BTN_OTNameWarn.IsVisible = StringFontUtil.HasUndefinedCharacters(TB_OT.Text ?? string.Empty, context, langPk, langSav);

        static bool IsFontDocumented(PKM pk)
        {
            if (pk.Generation < 5)
                return pk is (CK3 or XK3); // Only two that are fully documented and definitive
            return true;
        }
    }

    private async Task FontWarn(string name, string message, Control ctrl)
    {
        var langPk = (LanguageID)CB_Language.GetValue();
        var langSav = (LanguageID)RequestSaveFile.Language;
        var displayed = StringFontUtil.ReplaceUndefinedCharacters(name, Entity.Context, langPk, langSav);
        if (displayed == name) // save language was changed
        {
            ctrl.IsVisible = false;
            return;
        }
        await AppDialogs.Alert(OwnerWindow, string.Format(message, name, displayed));
    }

    private void PB_MarkShiny_Click()
    {
        if (Entity.Format <= 2)
        {
            TC_Editor.SelectedItem = Tab_Stats;
            Stats.Focus();
            return;
        }
        TC_Editor.SelectedItem = Tab_Main;
        TB_PID.Focus();
    }

    private void PB_MarkCured_Click()
    {
        // Toggle Pokérus cured state.
        if (CHK_Cured.IsChecked != true)
        {
            CHK_Infected.IsChecked = true;
            if (CB_PKRSDays.SelectedIndex != 0)
                CB_PKRSDays.SelectedIndex = 0;
        }
        else
        {
            CHK_Cured.IsChecked = false;
        }
        TC_Editor.SelectedItem = Tab_Main;
        CB_PKRSStrain.IsDropDownOpen = true;
    }

    // Date helpers (WinForms DateTimePicker.Value equivalents)
    private static DateTime GetDate(DatePicker picker) => picker.SelectedDate?.DateTime ?? new DateTime(2000, 1, 1);
    private static void SetDate(DatePicker picker, DateTime value)
    {
        if (value < picker.MinYear.DateTime)
            value = picker.MinYear.DateTime;
        // DateTime.Now is Kind=Local, and pairing a local DateTime with a zero offset throws; normalise first.
        picker.SelectedDate = UiFactory.ToOffset(value);
    }
}
