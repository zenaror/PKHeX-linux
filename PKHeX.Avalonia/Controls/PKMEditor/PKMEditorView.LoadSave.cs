using System;
using Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

public sealed partial class PKMEditorView
{
    private void LoadNickname(PKM pk)
    {
        CHK_NicknamedFlag.IsChecked = pk.IsNicknamed;
        TB_Nickname.Text = pk.Nickname;
    }

    private void SaveNickname(PKM pk)
    {
        pk.IsNicknamed = CHK_NicknamedFlag.IsChecked == true;
        pk.Nickname = TB_Nickname.Text ?? string.Empty;
    }

    private void LoadSpeciesLevelEXP(PKM pk)
    {
        if (!HaX)
        {
            // Sanity check level and EXP
            var current = pk.CurrentLevel;
            if (current == Experience.MaxLevel) // clamp back to max EXP
                pk.CurrentLevel = Experience.MaxLevel;
        }

        CB_Species.SetValue(pk.Species);
        var level = pk.Stat_Level;
        var exp = pk.EXP;
        TB_Level.Text = level.ToString();
        TB_EXP.Text = exp.ToString();

        var pi = pk.PersonalInfo;
        var growth = pi.EXPGrowth;
        ExperienceBar.Update(exp, growth); // don't trust level
    }

    private void SaveSpeciesLevelEXP(PKM pk)
    {
        pk.Species = (ushort)CB_Species.GetValue();
        pk.EXP = TB_EXP.UIntValue;
        pk.Stat_Level = (byte)Math.Max(1, TB_Level.IntValue);
    }

    private void LoadOT(PKM pk)
    {
        GB_OT.ResetForeColor(); // clear the Current Handler indicator just in case we switched formats.
        TB_OT.Text = pk.OriginalTrainerName;
        UC_OTGender.Gender = (byte)(pk.OriginalTrainerGender & 1);
    }

    private void SaveOT(PKM pk)
    {
        pk.OriginalTrainerName = TB_OT.Text ?? string.Empty;
        pk.OriginalTrainerGender = UC_OTGender.Gender;
    }

    private void LoadPokerus(PKM pk)
    {
        var infected = pk.IsPokerusInfected;
        var cured = pk.IsPokerusCured;
        CHK_Infected.IsChecked = Label_PKRS.IsVisible = CB_PKRSStrain.IsVisible = infected;
        Label_PKRSdays.IsVisible = CB_PKRSDays.IsVisible = !cured && infected;
        CHK_Cured.IsChecked = cured;
        ChangePKRSstrainDropDownLists(CB_PKRSStrain.SelectedIndex, pk.PokerusStrain, 0);
        CB_PKRSStrain.SetIndexClamped(pk.PokerusStrain);
        CB_PKRSDays.SetIndexClamped(pk.PokerusDays); // clamp to valid day values for the current strain
    }

    private void SavePokerus(PKM pk)
    {
        pk.PokerusDays = Math.Max(0, CB_PKRSDays.SelectedIndex);
        pk.PokerusStrain = Math.Max(0, CB_PKRSStrain.SelectedIndex);
    }

    private void LoadIVs(PKM pk)
    {
        Span<int> span = stackalloc int[6];
        pk.GetIVs(span);
        Stats.LoadIVs(span);
    }

    private void LoadEVs(PKM pk)
    {
        Span<int> span = stackalloc int[6];
        pk.GetEVs(span);
        Stats.LoadEVs(span);
    }

    private void LoadAVs(IAwakened pk) => Stats.LoadAVs(pk);
    private void LoadGVs(IGanbaru pk) => Stats.LoadGVs(pk);

    private void LoadMoves(PKM pk)
    {
        MC_Move1.SelectedMove = pk.Move1;
        MC_Move2.SelectedMove = pk.Move2;
        MC_Move3.SelectedMove = pk.Move3;
        MC_Move4.SelectedMove = pk.Move4;
        MC_Move1.PPUps = pk.Move1_PPUps;
        MC_Move2.PPUps = pk.Move2_PPUps;
        MC_Move3.PPUps = pk.Move3_PPUps;
        MC_Move4.PPUps = pk.Move4_PPUps;
        MC_Move1.PP = pk.Move1_PP;
        MC_Move2.PP = pk.Move2_PP;
        MC_Move3.PP = pk.Move3_PP;
        MC_Move4.PP = pk.Move4_PP;
    }

    private void SaveMoves(PKM pk)
    {
        pk.Move1 = MC_Move1.SelectedMove;
        pk.Move2 = MC_Move2.SelectedMove;
        pk.Move3 = MC_Move3.SelectedMove;
        pk.Move4 = MC_Move4.SelectedMove;
        pk.Move1_PP = MC_Move1.PP;
        pk.Move2_PP = MC_Move2.PP;
        pk.Move3_PP = MC_Move3.PP;
        pk.Move4_PP = MC_Move4.PP;
        pk.Move1_PPUps = MC_Move1.PPUps;
        pk.Move2_PPUps = MC_Move2.PPUps;
        pk.Move3_PPUps = MC_Move3.PPUps;
        pk.Move4_PPUps = MC_Move4.PPUps;
    }

    private void LoadShadow3(IShadowCapture pk)
    {
        NUD_ShadowID.Value = pk.ShadowID;
        FLP_Purification.IsVisible = pk.ShadowID > 0;
        if (pk.ShadowID > 0)
        {
            int value = pk.Purification;
            if (value < NUD_Purification.Minimum)
                value = (int)NUD_Purification.Minimum;

            NUD_Purification.Value = value;
            CHK_Shadow.IsChecked = pk.IsShadow;

            NUD_ShadowID.Value = Math.Max(pk.ShadowID, (ushort)0);
        }
        else
        {
            NUD_Purification.Value = 0;
            CHK_Shadow.IsChecked = false;
            NUD_ShadowID.Value = 0;
        }
    }

    private void SaveShadow3(IShadowCapture pk)
    {
        pk.ShadowID = (ushort)(NUD_ShadowID.Value ?? 0);
        if (pk.ShadowID > 0)
            pk.Purification = (int)(NUD_Purification.Value ?? 0);
    }

    private void LoadRelearnMoves(PKM pk)
    {
        CB_RelearnMove1.SetValue(pk.RelearnMove1);
        CB_RelearnMove2.SetValue(pk.RelearnMove2);
        CB_RelearnMove3.SetValue(pk.RelearnMove3);
        CB_RelearnMove4.SetValue(pk.RelearnMove4);
    }

    private void SaveRelearnMoves(PKM pk)
    {
        pk.RelearnMove1 = (ushort)CB_RelearnMove1.GetValue();
        pk.RelearnMove2 = (ushort)CB_RelearnMove2.GetValue();
        pk.RelearnMove3 = (ushort)CB_RelearnMove3.GetValue();
        pk.RelearnMove4 = (ushort)CB_RelearnMove4.GetValue();
    }

    private void LoadMisc1(PKM pk)
    {
        LoadSpeciesLevelEXP(pk);
        LoadNickname(pk);
        LoadOT(pk);
        LoadIVs(pk);
        LoadEVs(pk);
        LoadMoves(pk);
    }

    private void SaveMisc1(PKM pk)
    {
        SaveSpeciesLevelEXP(pk);
        SaveNickname(pk);
        SaveOT(pk);
        SaveMoves(pk);
    }

    private void LoadMisc2(PKM pk)
    {
        LoadPokerus(pk);
        CHK_IsEgg.IsChecked = pk.IsEgg;
        CB_HeldItem.SetValue(pk.HeldItem);
        CB_Form.SetIndexClamped(pk.Form);
        L_FormArgument.IsVisible = pk is IFormArgument f && FA_Form.LoadArgument(f, pk.Species, pk.Form, pk.Context);

        TB_Friendship.Text = pk.OriginalTrainerFriendship.ToString();

        Label_HatchCounter.IsVisible = CHK_IsEgg.IsChecked == true;
        Label_Friendship.IsVisible = CHK_IsEgg.IsChecked != true;
    }

    private void SaveMisc2(PKM pk)
    {
        SavePokerus(pk);
        pk.IsEgg = CHK_IsEgg.IsChecked == true;
        pk.HeldItem = CB_HeldItem.GetValue();
        pk.Form = (byte)(CB_Form.IsEnabled ? Math.Max(0, CB_Form.SelectedIndex) & 0x1F : 0);
        if (Entity is IFormArgument f)
            FA_Form.SaveArgument(f);

        var friendship = (byte)TB_Friendship.IntValue;
        pk.OriginalTrainerFriendship = friendship;
    }

    private void LoadMisc3(PKM pk)
    {
        TB_PID.Text = pk.PID.ToString("X8");
        UC_Gender.Gender = pk.Gender;
        CB_Nature.SetValue((int)pk.Nature);
        CB_Language.SetValue(pk.Language);
        CB_GameOrigin.SetValue((int)pk.Version);
        CB_Ball.SetValue(pk.Ball);
        CB_MetLocation.SetValue(pk.MetLocation);
        TB_MetLevel.Text = pk.MetLevel.ToString();
        CHK_Fateful.IsChecked = pk.FatefulEncounter;

        if (pk is IContestStatsReadOnly s)
            s.CopyContestStatsTo(Contest);

        TID_Trainer.LoadTrainer(pk, pk.Format);

        // Load Extrabyte Value
        var text = CB_ExtraBytes.GetText();
        if (text.Length != 0)
        {
            var offset = Convert.ToInt32(text, 16);
            var value = pk.Data[offset];
            TB_ExtraByte.Text = value.ToString();
        }
    }

    private void SaveMisc3(PKM pk)
    {
        pk.PID = TB_PID.UIntValue;
        pk.Nature = (Nature)CB_Nature.GetValue();
        pk.Gender = UC_Gender.Gender;

        if (pk is IContestStats s)
            Contest.CopyContestStatsTo(s);

        pk.FatefulEncounter = CHK_Fateful.IsChecked == true;
        pk.Ball = (byte)CB_Ball.GetValue();
        pk.Version = (GameVersion)CB_GameOrigin.GetValue();
        pk.Language = (byte)CB_Language.GetValue();
        pk.MetLevel = (byte)TB_MetLevel.IntValue;
        pk.MetLocation = (ushort)CB_MetLocation.GetValue();
    }

    private void LoadMisc4(PKM pk)
    {
        SetDate(CAL_MetDate, pk.MetDate?.ToDateTime(new TimeOnly()) ?? new(2000, 1, 1));
        if (!EncounterStateUtil.IsMetAsEgg(pk))
        {
            CHK_AsEgg.IsChecked = GB_EggConditions.IsEnabled = false;
            SetDate(CAL_EggDate, new DateTime(2000, 01, 01));
        }
        else
        {
            // Was obtained initially as an egg.
            CHK_AsEgg.IsChecked = GB_EggConditions.IsEnabled = true;
            SetDate(CAL_EggDate, pk.EggMetDate?.ToDateTime(new TimeOnly()) ?? new(2000, 1, 1));
        }
        CB_EggLocation.SetValue(pk.EggLocation);
    }

    private void SaveMisc4(PKM pk)
    {
        if (CHK_AsEgg.IsChecked == true) // If encountered as an egg, load the Egg Met data from fields.
        {
            pk.EggMetDate = DateOnly.FromDateTime(GetDate(CAL_EggDate));
            pk.EggLocation = (ushort)CB_EggLocation.GetValue();
        }
        else // Default Dates
        {
            pk.EggMetDate = null; // clear
            pk.EggLocation = LocationEdits.GetNoneLocation(pk);
        }

        // Met Data
        if (pk.IsEgg && pk.MetLocation == LocationEdits.GetNoneLocation(pk)) // If still an egg, it has no hatch location/date. Zero it!
            pk.MetDate = null; // clear
        else
            pk.MetDate = DateOnly.FromDateTime(GetDate(CAL_MetDate));

        pk.Ability = (HaX || pk is PA9 ? DEV_Ability : CB_Ability).GetValue();
    }

    private void LoadMisc6(PKM pk)
    {
        TB_EC.Text = pk.EncryptionConstant.ToString("X8");
        DEV_Ability.SetValue(pk.Ability);

        // with some simple error handling
        var bitNumber = pk.AbilityNumber;
        int abilityIndex = AbilityVerifier.IsValidAbilityBits(bitNumber) ? bitNumber >> 1 : 0;
        CB_Ability.SetIndexClamped(abilityIndex);
        TB_AbilityNumber.Text = bitNumber.ToString();

        LoadRelearnMoves(pk);
        LoadHandlingTrainer(pk);

        if (pk is IRegionOriginReadOnly tr)
            LoadGeolocation(tr);
    }

    private void SaveMisc6(PKM pk)
    {
        pk.EncryptionConstant = TB_EC.UIntValue;
        if (PIDVerifier.GetTransferEC(pk, out var ec))
            pk.EncryptionConstant = ec;

        pk.AbilityNumber = TB_AbilityNumber.IntValue;

        SaveRelearnMoves(pk);
        SaveHandlingTrainer(pk);

        if (pk is IRegionOrigin tr)
            SaveGeolocation(tr);
    }

    private void LoadGeolocation(IRegionOriginReadOnly pk)
    {
        CB_Country.SetValue(pk.Country);
        CB_SubRegion.SetValue(pk.Region);
        CB_3DSReg.SetValue(pk.ConsoleRegion);
    }

    private void SaveGeolocation(IRegionOrigin pk)
    {
        pk.Country = (byte)CB_Country.GetValue();
        pk.Region = (byte)CB_SubRegion.GetValue();
        pk.ConsoleRegion = (byte)CB_3DSReg.GetValue();
    }

    private void LoadHandlingTrainer(PKM pk)
    {
        var handler = pk.HandlingTrainerName;
        byte gender = (byte)(pk.HandlingTrainerGender & 1);

        TB_HT.Text = handler;
        UC_HTGender.Gender = gender;
        TB_FriendshipHT.Text = pk.HandlingTrainerFriendship.ToString();
        ToggleHandlerVisibility(handler.Length != 0);

        // Indicate who is currently in possession of the PKM
        UpdateHandlingTrainerBackground(pk.CurrentHandler);
    }

    private void ToggleHandlerVisibility(bool hasValue)
    {
        L_CurrentHandler.IsVisible = CB_Handler.IsVisible = UC_HTGender.IsVisible = hasValue;
    }

    private void UpdateHandlingTrainerBackground(int handler)
    {
        if (handler == 0) // OT
        {
            GB_OT.SetForeColor(ColorUtilAvalonia.ColorWarn);
            GB_nOT.ResetForeColor();
            CB_Handler.SelectedIndex = 0;
        }
        else // Handling Trainer
        {
            GB_nOT.SetForeColor(ColorUtilAvalonia.ColorWarn);
            GB_OT.ResetForeColor();
            CB_Handler.SelectedIndex = 1;
        }
    }

    private void SaveHandlingTrainer(PKM pk)
    {
        pk.HandlingTrainerName = TB_HT.Text ?? string.Empty;
        pk.HandlingTrainerGender = UC_HTGender.Gender;
        pk.HandlingTrainerFriendship = (byte)TB_FriendshipHT.IntValue;
    }

    private void LoadAbility4(PKM pk)
    {
        var index = GetAbilityIndex4(pk);
        CB_Ability.SetIndexClamped(index);
    }

    private static int GetAbilityIndex4(PKM pk)
    {
        var pi = pk.PersonalInfo;
        var ability = pk.Ability;
        int abilityIndex = pi.GetIndexOfAbility(ability);
        if (abilityIndex >= 2)
            return 2;
        if (abilityIndex < 0)
        {
            if (ability == (int)Ability.Reckless && pk is { Context: EntityContext.Gen5, Species: (ushort)Species.Basculin, Form: 1 })
                return 3; // manually appended "extra" bug case for Gen5 Basculin-Blue.
            return 0; // fall back to first ability.
        }

        var abils = (IPersonalAbility12)pi;
        if (abils.IsAbility12Same)
            return pk.PIDAbility;
        return abilityIndex;
    }

    private void LoadMisc8(PK8 pk8)
    {
        CB_StatAlignment.SetValue((int)pk8.StatAlignment);
        Stats.CB_DynamaxLevel.SetIndexClamped(pk8.DynamaxLevel);
        Stats.CHK_Gigantamax.IsChecked = pk8.CanGigantamax;
        CB_HTLanguage.SetValue(pk8.HandlingTrainerLanguage);
        TB_HomeTracker.Text = pk8.Tracker.ToString("X16");
        CB_BattleVersion.SetValue((int)pk8.BattleVersion);
    }

    private void SaveMisc8(PK8 pk8)
    {
        pk8.StatAlignment = (Nature)CB_StatAlignment.GetValue();
        pk8.DynamaxLevel = (byte)Math.Max(0, Stats.CB_DynamaxLevel.SelectedIndex);
        pk8.CanGigantamax = Stats.CHK_Gigantamax.IsChecked == true;
        pk8.HandlingTrainerLanguage = (byte)CB_HTLanguage.GetValue();
        pk8.BattleVersion = (GameVersion)CB_BattleVersion.GetValue();
    }

    private void LoadMisc8(PB8 pk8)
    {
        CB_StatAlignment.SetValue((int)pk8.StatAlignment);
        Stats.CB_DynamaxLevel.SetIndexClamped(pk8.DynamaxLevel);
        Stats.CHK_Gigantamax.IsChecked = pk8.CanGigantamax;
        CB_HTLanguage.SetValue(pk8.HandlingTrainerLanguage);
        TB_HomeTracker.Text = pk8.Tracker.ToString("X16");
        CB_BattleVersion.SetValue((int)pk8.BattleVersion);
    }

    private void SaveMisc8(PB8 pk8)
    {
        pk8.StatAlignment = (Nature)CB_StatAlignment.GetValue();
        pk8.DynamaxLevel = (byte)Math.Max(0, Stats.CB_DynamaxLevel.SelectedIndex);
        pk8.CanGigantamax = Stats.CHK_Gigantamax.IsChecked == true;
        pk8.HandlingTrainerLanguage = (byte)CB_HTLanguage.GetValue();
        pk8.BattleVersion = (GameVersion)CB_BattleVersion.GetValue();
    }

    private void LoadMisc8(PA8 pk8)
    {
        CB_StatAlignment.SetValue((int)pk8.StatAlignment);
        Stats.CB_DynamaxLevel.SetIndexClamped(pk8.DynamaxLevel);
        Stats.CHK_Gigantamax.IsChecked = pk8.CanGigantamax;
        CB_HTLanguage.SetValue(pk8.HandlingTrainerLanguage);
        TB_HomeTracker.Text = pk8.Tracker.ToString("X16");
        CB_BattleVersion.SetValue((int)pk8.BattleVersion);
        Stats.CHK_IsAlpha.IsChecked = pk8.IsAlpha;
        Stats.CHK_IsNoble.IsChecked = pk8.IsNoble;
        CB_AlphaMastered.SetValue(pk8.AlphaMove);
    }

    private void SaveMisc8(PA8 pk8)
    {
        pk8.StatAlignment = (Nature)CB_StatAlignment.GetValue();
        pk8.DynamaxLevel = (byte)Math.Max(0, Stats.CB_DynamaxLevel.SelectedIndex);
        pk8.CanGigantamax = Stats.CHK_Gigantamax.IsChecked == true;
        pk8.HandlingTrainerLanguage = (byte)CB_HTLanguage.GetValue();
        pk8.BattleVersion = (GameVersion)CB_BattleVersion.GetValue();
        pk8.IsAlpha = Stats.CHK_IsAlpha.IsChecked == true;
        pk8.IsNoble = Stats.CHK_IsNoble.IsChecked == true;
        pk8.AlphaMove = (ushort)CB_AlphaMastered.GetValue();
    }

    private void LoadMisc9(PK9 pk9)
    {
        CB_StatAlignment.SetValue((int)pk9.StatAlignment);
        CB_HTLanguage.SetValue(pk9.HandlingTrainerLanguage);
        TB_HomeTracker.Text = pk9.Tracker.ToString("X16");
        CB_BattleVersion.SetValue((int)pk9.BattleVersion);
        Stats.CB_TeraTypeOriginal.SetValue((int)pk9.TeraTypeOriginal);
        Stats.CB_TeraTypeOverride.SetValue((int)pk9.TeraTypeOverride);
        TB_ObedienceLevel.Text = pk9.ObedienceLevel.ToString();
    }

    private void SaveMisc9(PK9 pk9)
    {
        pk9.StatAlignment = (Nature)CB_StatAlignment.GetValue();
        pk9.HandlingTrainerLanguage = (byte)CB_HTLanguage.GetValue();
        pk9.BattleVersion = (GameVersion)CB_BattleVersion.GetValue();
        pk9.TeraTypeOriginal = (MoveType)Stats.CB_TeraTypeOriginal.GetValue();
        pk9.TeraTypeOverride = (MoveType)Stats.CB_TeraTypeOverride.GetValue();
        pk9.ObedienceLevel = (byte)TB_ObedienceLevel.IntValue;
    }

    private void LoadMisc9(PA9 pk9)
    {
        CB_StatAlignment.SetValue((int)pk9.StatAlignment);
        CB_HTLanguage.SetValue(pk9.HandlingTrainerLanguage);
        TB_HomeTracker.Text = pk9.Tracker.ToString("X16");
        CB_BattleVersion.SetValue((int)pk9.BattleVersion);
        TB_ObedienceLevel.Text = pk9.ObedienceLevel.ToString();
        Stats.CHK_IsAlpha.IsChecked = pk9.IsAlpha;
    }

    private void SaveMisc9(PA9 pk9)
    {
        pk9.StatAlignment = (Nature)CB_StatAlignment.GetValue();
        pk9.HandlingTrainerLanguage = (byte)CB_HTLanguage.GetValue();
        pk9.BattleVersion = (GameVersion)CB_BattleVersion.GetValue();
        pk9.ObedienceLevel = (byte)TB_ObedienceLevel.IntValue;
        pk9.IsAlpha = Stats.CHK_IsAlpha.IsChecked == true;
    }
}
