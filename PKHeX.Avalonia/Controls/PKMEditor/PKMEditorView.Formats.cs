using System;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

public sealed partial class PKMEditorView
{
    private void PopulateFieldsPK1()
    {
        if (Entity is not PK1 pk1)
            throw new FormatException(nameof(Entity));

        LoadMisc1(pk1);
        TID_Trainer.LoadTrainer(pk1, pk1.Format);
        CR_PK1.LoadPK1(pk1);

        // Attempt to detect language
        var language = RequestSaveFile.Language;
        CB_Language.SetValue(pk1.IsSpeciesNameMatch(language) ? language : pk1.GuessedLanguage(language));

        LoadPartyStats(pk1);
        UpdateStats();
    }

    private PK1 PreparePK1()
    {
        if (Entity is not PK1 pk1)
            throw new FormatException(nameof(Entity));

        SaveMisc1(pk1);

        SavePartyStats(pk1);
        pk1.FixMoves();
        pk1.RefreshChecksum();
        return pk1;
    }

    private void PopulateFieldsPK2()
    {
        if (Entity is not (GBPKM pk2 and ICaughtData2 c2))
            throw new FormatException(nameof(Entity));

        if (Entity is SK2 sk2)
        {
            var sav = RequestSaveFile;
            CoerceStadium2Language(sk2, sav);
        }
        LoadMisc1(pk2);
        LoadMisc2(pk2);

        TID_Trainer.LoadTrainer(pk2, pk2.Format);
        TB_MetLevel.Text = c2.MetLevel.ToString();
        CB_MetLocation.SetValue(c2.MetLocation);
        CB_MetTimeOfDay.SelectedIndex = c2.MetTimeOfDay;

        // Attempt to detect language
        var language = RequestSaveFile.Language;
        CB_Language.SetValue(pk2.IsSpeciesNameMatch(language) ? language : pk2.GuessedLanguage(language));

        LoadPartyStats(pk2);
        UpdateStats();
    }

    private static void CoerceStadium2Language(SK2 sk2, SaveFile sav)
    {
        if (sk2.Japanese == (sav.Language == 1))
            return;

        var la = new LegalityAnalysis(sk2);
        if (la.Valid || !sk2.IsPossible(sav.Language == 1))
            return;

        sk2.SwapLanguage();
        la = new LegalityAnalysis(sk2);
        if (la.Valid)
            return;

        Span<char> nickname = stackalloc char[sk2.MaxStringLengthNickname];
        int len = sk2.LoadString(sk2.NicknameTrash, nickname);
        var lang = SpeciesName.GetSpeciesNameLanguage(sk2.Species, nickname[..len], EntityContext.Gen2);
        if (lang >= 1 && (lang == 1 != sk2.Japanese)) // force match language
            sk2.SwapLanguage();
        else if (sk2.Japanese != (sav.Language == 1)) // force match save file
            sk2.SwapLanguage();
    }

    private GBPKM PreparePK2()
    {
        if (Entity is not (GBPKM pk2 and ICaughtData2 c2))
            throw new FormatException(nameof(Entity));

        SaveMisc1(pk2);
        SaveMisc2(pk2);

        c2.MetLevel = (byte)TB_MetLevel.IntValue;
        c2.MetLocation = (ushort)CB_MetLocation.GetValue();
        c2.MetTimeOfDay = Math.Max(0, CB_MetTimeOfDay.SelectedIndex);

        SavePartyStats(pk2);
        pk2.FixMoves();
        return pk2;
    }

    private void PopulateFieldsPK3()
    {
        if (Entity is not G3PKM pk3)
            throw new FormatException(nameof(Entity));

        LoadMisc3(pk3);
        LoadMisc1(pk3);
        LoadMisc2(pk3);

        CB_Ability.SelectedIndex = pk3.AbilityBit && CB_Ability.GetItemCount() > 1 ? 1 : 0;
        if (pk3 is IShadowCapture s)
            LoadShadow3(s);

        LoadPartyStats(pk3);
        UpdateStats();
    }

    private G3PKM PreparePK3()
    {
        if (Entity is not G3PKM pk3)
            throw new FormatException(nameof(Entity));

        SaveMisc3(pk3); // save Language first so that Nickname/etc encode properly
        SaveMisc2(pk3); // save IsEgg prior to setting ^
        SaveMisc1(pk3);

        pk3.AbilityBit = CB_Ability.SelectedIndex != 0;
        if (Entity is IShadowCapture ck3)
            SaveShadow3(ck3);

        SavePartyStats(pk3);
        pk3.FixMoves();
        pk3.RefreshChecksum();
        return pk3;
    }

    private void PopulateFieldsPK4()
    {
        if (Entity is not G4PKM pk4)
            throw new FormatException(nameof(Entity));

        LoadMisc1(pk4);
        LoadMisc2(pk4);
        LoadMisc3(pk4);
        LoadMisc4(pk4);

        CB_GroundTile.SetValue(pk4.Gen4 ? (int)pk4.GroundTile : 0);
        CB_GroundTile.IsVisible = Label_GroundTile.IsVisible = Entity.Gen4;

        if (HaX)
            DEV_Ability.SetValue(pk4.Ability);
        else
            LoadAbility4(pk4);

        // Minor properties
        ShinyLeaf.SetValue(pk4.ShinyLeaf);
        NUD_WalkingMood.Value = pk4.WalkingMood;

        LoadPartyStats(pk4);
        UpdateStats();
    }

    private G4PKM PreparePK4()
    {
        if (Entity is not G4PKM pk4)
            throw new FormatException(nameof(Entity));

        SaveMisc1(pk4);
        SaveMisc2(pk4);
        SaveMisc3(pk4);
        SaveMisc4(pk4);

        pk4.GroundTile = (GroundTileType)CB_GroundTile.GetValue();

        // Minor properties
        pk4.ShinyLeaf = ShinyLeaf.GetValue();
        pk4.WalkingMood = (sbyte)(NUD_WalkingMood.Value ?? 0);

        SavePartyStats(pk4);
        pk4.FixMoves();
        pk4.RefreshChecksum();
        return pk4;
    }

    private void PopulateFieldsPK5()
    {
        if (Entity is not PK5 pk5)
            throw new FormatException(nameof(Entity));

        LoadMisc1(pk5);
        LoadMisc2(pk5);
        LoadMisc3(pk5);
        LoadMisc4(pk5);
        CB_GroundTile.SetValue(pk5.Gen4 ? (int)pk5.GroundTile : 0);
        CB_GroundTile.IsVisible = Label_GroundTile.IsVisible = pk5.Gen4;
        CHK_NSparkle.IsChecked = pk5.NSparkle;
        NUD_PokeStarFame.Value = pk5.PokeStarFame;

        if (HaX)
            DEV_Ability.SetValue(pk5.Ability);
        else if (pk5.HiddenAbility)
            CB_Ability.SelectedIndex = 2;
        else
            LoadAbility4(pk5);

        LoadPartyStats(pk5);
        UpdateStats();
    }

    private PK5 PreparePK5()
    {
        if (Entity is not PK5 pk5)
            throw new FormatException(nameof(Entity));

        SaveMisc1(pk5);
        SaveMisc2(pk5);
        SaveMisc3(pk5);
        SaveMisc4(pk5);

        pk5.GroundTile = (GroundTileType)CB_GroundTile.GetValue();
        pk5.NSparkle = CHK_NSparkle.IsChecked == true;
        pk5.PokeStarFame = (byte)(NUD_PokeStarFame.Value ?? 0);
        if (!HaX)
        {
            pk5.HiddenAbility = CB_Ability.SelectedIndex is 2;
        }
        else
        {
            var pi = pk5.PersonalInfo;
            pk5.HiddenAbility = pi.HasHiddenAbility && pk5.Ability == pi.AbilityH;
        }

        SavePartyStats(pk5);
        pk5.FixMoves();
        pk5.RefreshChecksum();
        return pk5;
    }

    private void PopulateFieldsPK6()
    {
        if (Entity is not PK6 pk6)
            throw new FormatException(nameof(Entity));

        LoadMisc1(pk6);
        LoadMisc2(pk6);
        LoadMisc3(pk6);
        LoadMisc4(pk6);
        LoadMisc6(pk6);

        CB_GroundTile.SetValue(pk6.Gen4 ? (int)pk6.GroundTile : 0);
        CB_GroundTile.IsVisible = Label_GroundTile.IsVisible = pk6.Gen4;

        LoadPartyStats(pk6);
        UpdateStats();
    }

    private PK6 PreparePK6()
    {
        if (Entity is not PK6 pk6)
            throw new FormatException(nameof(Entity));

        SaveMisc1(pk6);
        SaveMisc2(pk6);
        SaveMisc3(pk6);
        SaveMisc4(pk6);
        SaveMisc6(pk6);

        pk6.GroundTile = (GroundTileType)CB_GroundTile.GetValue();

        // Toss in Party Stats
        SavePartyStats(pk6);

        // Ensure party stats are essentially clean.
        pk6.Data[0xFE..].Clear();
        // Status Condition is allowed to be mutated to pre-set conditions like Burn for Guts.

        pk6.FixMoves();
        pk6.FixRelearn();
        if (ModifyPKM)
            pk6.FixMemories();
        pk6.RefreshChecksum();
        return pk6;
    }

    private void PopulateFieldsPK7()
    {
        if (Entity is not PK7 pk7)
            throw new FormatException(nameof(Entity));

        LoadMisc1(pk7);
        LoadMisc2(pk7);
        LoadMisc3(pk7);
        LoadMisc4(pk7);
        LoadMisc6(pk7);

        LoadPartyStats(pk7);
        UpdateStats();
    }

    private PK7 PreparePK7()
    {
        if (Entity is not PK7 pk7)
            throw new FormatException(nameof(Entity));

        SaveMisc1(pk7);
        SaveMisc2(pk7);
        SaveMisc3(pk7);
        SaveMisc4(pk7);
        SaveMisc6(pk7);

        // Toss in Party Stats
        SavePartyStats(pk7);

        // Ensure party stats are essentially clean.
        pk7.Data[0xFE..].Clear();
        // Status Condition is allowed to be mutated to pre-set conditions like Burn for Guts.

        pk7.FixMoves();
        pk7.FixRelearn();
        if (ModifyPKM)
            pk7.FixMemories();
        pk7.RefreshChecksum();
        return pk7;
    }

    private void PopulateFieldsPB7()
    {
        if (Entity is not PB7 pk7)
            throw new FormatException(nameof(Entity));

        LoadMisc1(pk7);
        LoadMisc2(pk7);
        LoadMisc3(pk7);
        LoadMisc4(pk7);
        LoadMisc6(pk7);
        LoadAVs(pk7);
        SizeCP.LoadPKM(pk7);

        NUD_Spirit7b.Value = pk7.Spirit;
        NUD_Mood7b.Value = pk7.Mood;

        try
        {
            if (pk7 is { ReceivedDate: { } d, ReceivedTime: { } t })
                SetDateTime(new DateTime(d, t));
            else
                SetDateTime(DateTime.Now);
        }
        catch (ArgumentOutOfRangeException)
        {
            /* Don't care if garbage, just reset. */
            SetDateTime(DateTime.Now);
        }

        LoadPartyStats(pk7);
        UpdateStats();
    }

    private void SetDateTime(DateTime value)
    {
        SetDate(CAL_ReceivedDate, value);
        CAL_ReceivedTime.SelectedTime = value.TimeOfDay;
    }

    private DateTime GetDateTime()
    {
        var date = GetDate(CAL_ReceivedDate);
        var time = CAL_ReceivedTime.SelectedTime ?? TimeSpan.Zero;
        return date.Date + time;
    }

    private PB7 PreparePB7()
    {
        if (Entity is not PB7 pk7)
            throw new FormatException(nameof(Entity));

        SaveMisc1(pk7);
        SaveMisc2(pk7);
        SaveMisc3(pk7);
        SaveMisc4(pk7);
        SaveMisc6(pk7);

        // Toss in Party Stats
        SavePartyStats(pk7);

        if (pk7.Stat_CP == 0)
            pk7.ResetCP();

        var date = GetDateTime();
        pk7.ReceivedYear = (byte)(date.Year - 2000);
        pk7.ReceivedMonth = (byte)date.Month;
        pk7.ReceivedDay = (byte)date.Day;
        pk7.ReceivedHour = (byte)date.Hour;
        pk7.ReceivedMinute = (byte)date.Minute;
        pk7.ReceivedSecond = (byte)date.Second;

        pk7.Spirit = (byte)(NUD_Spirit7b.Value ?? 0);
        pk7.Mood = (byte)(NUD_Mood7b.Value ?? 0);

        pk7.FixMoves();
        pk7.FixRelearn();
        if (ModifyPKM)
            pk7.FixMemories();
        pk7.RefreshChecksum();
        return pk7;
    }

    private void PopulateFieldsPK8()
    {
        if (Entity is not PK8 pk8)
            throw new FormatException(nameof(Entity));

        LoadMisc1(pk8);
        LoadMisc2(pk8);
        LoadMisc3(pk8);
        LoadMisc4(pk8);
        LoadMisc6(pk8);
        SizeCP.LoadPKM(pk8);
        LoadMisc8(pk8);

        LoadPartyStats(pk8);
        UpdateStats();
    }

    private PK8 PreparePK8()
    {
        if (Entity is not PK8 pk8)
            throw new FormatException(nameof(Entity));

        SaveMisc1(pk8);
        SaveMisc2(pk8);
        SaveMisc3(pk8);
        SaveMisc4(pk8);
        SaveMisc6(pk8);
        SaveMisc8(pk8);

        // Toss in Party Stats
        SavePartyStats(pk8);

        pk8.FixMoves();
        pk8.FixRelearn();
        if (ModifyPKM)
            pk8.FixMemories();
        pk8.RefreshChecksum();
        return pk8;
    }

    private PB8 PreparePB8()
    {
        if (Entity is not PB8 pk8)
            throw new FormatException(nameof(Entity));

        SaveMisc1(pk8);
        SaveMisc2(pk8);
        SaveMisc3(pk8);
        SaveMisc4(pk8);
        SaveMisc6(pk8);
        SaveMisc8(pk8);

        // Toss in Party Stats
        SavePartyStats(pk8);

        pk8.FixMoves();
        pk8.FixRelearn();
        if (ModifyPKM)
            pk8.FixMemories();
        pk8.RefreshChecksum();
        return pk8;
    }

    private void PopulateFieldsPB8()
    {
        if (Entity is not PB8 pk8)
            throw new FormatException(nameof(Entity));

        LoadMisc1(pk8);
        LoadMisc2(pk8);
        LoadMisc3(pk8);
        LoadMisc4(pk8);
        LoadMisc6(pk8);
        SizeCP.LoadPKM(pk8);
        LoadMisc8(pk8);

        LoadPartyStats(pk8);
        UpdateStats();
    }

    private PA8 PreparePA8()
    {
        if (Entity is not PA8 pk8)
            throw new FormatException(nameof(Entity));

        SaveMisc1(pk8);
        SaveMisc2(pk8);
        SaveMisc3(pk8);
        SaveMisc4(pk8);
        SaveMisc6(pk8);
        SaveMisc8(pk8);

        // Toss in Party Stats
        SavePartyStats(pk8);

        pk8.FixMoves();
        pk8.FixRelearn();
        if (ModifyPKM)
            pk8.FixMemories();
        pk8.RefreshChecksum();
        return pk8;
    }

    private void PopulateFieldsPA8()
    {
        if (Entity is not PA8 pk8)
            throw new FormatException(nameof(Entity));

        LoadMisc1(pk8);
        LoadMisc2(pk8);
        LoadMisc3(pk8);
        LoadMisc4(pk8);
        LoadMisc6(pk8);
        LoadGVs(pk8);
        SizeCP.LoadPKM(pk8);
        LoadMisc8(pk8);

        LoadPartyStats(pk8);
        UpdateStats();
    }

    private void PopulateFieldsPK9()
    {
        if (Entity is not PK9 pk9)
            throw new FormatException(nameof(Entity));

        LoadMisc1(pk9);
        LoadMisc2(pk9);
        LoadMisc3(pk9);
        LoadMisc4(pk9);
        LoadMisc6(pk9);
        SizeCP.LoadPKM(pk9);
        LoadMisc9(pk9);

        LoadPartyStats(pk9);
        UpdateStats();
    }

    private PK9 PreparePK9()
    {
        if (Entity is not PK9 pk9)
            throw new FormatException(nameof(Entity));

        SaveMisc1(pk9);
        SaveMisc2(pk9);
        SaveMisc3(pk9);
        SaveMisc4(pk9);
        SaveMisc6(pk9);
        SaveMisc9(pk9);

        // Toss in Party Stats
        SavePartyStats(pk9);

        pk9.FixMoves();
        pk9.FixRelearn();
        if (ModifyPKM)
            pk9.FixMemories();
        pk9.RefreshChecksum();
        return pk9;
    }

    private void PopulateFieldsPA9()
    {
        if (Entity is not PA9 pa9)
            throw new FormatException(nameof(Entity));

        LoadMisc1(pa9);
        LoadMisc2(pa9);
        LoadMisc3(pa9);
        LoadMisc4(pa9);
        LoadMisc6(pa9);
        SizeCP.LoadPKM(pa9);
        LoadMisc9(pa9);

        LoadPartyStats(pa9);
        UpdateStats();
    }

    private PA9 PreparePA9()
    {
        if (Entity is not PA9 pa9)
            throw new FormatException(nameof(Entity));

        SaveMisc1(pa9);
        SaveMisc2(pa9);
        SaveMisc3(pa9);
        SaveMisc4(pa9);
        SaveMisc6(pa9);
        SaveMisc9(pa9);

        // Toss in Party Stats
        SavePartyStats(pa9);

        pa9.FixMoves();
        pa9.FixRelearn();
        if (ModifyPKM)
            pa9.FixMemories();
        pa9.RefreshChecksum();
        return pa9;
    }
}
