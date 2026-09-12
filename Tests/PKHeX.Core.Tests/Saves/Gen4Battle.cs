using System;
using FluentAssertions;
using Xunit;

namespace PKHeX.Core.Tests.Saves;

/// <summary>
/// Generation 4 battle data structures: the Battle Video entity format and the Battle Revolution pass.
/// </summary>
public class Gen4Battle
{
    private static PK4 GetEntity() => new()
    {
        PID = 0x12345678,
        Species = (ushort)Species.Lucario,
        HeldItem = 0xDA, // Life Orb
        ID32 = 0xABCD1234,
        EXP = 125000,
        OriginalTrainerFriendship = 70,
        Ability = (int)Core.Ability.Steadfast,
        EV_HP = 4, EV_ATK = 252, EV_SPE = 252,
        Move1 = (ushort)Move.CloseCombat,
        Move2 = (ushort)Move.ExtremeSpeed,
        IV_HP = 31, IV_ATK = 31, IV_SPE = 30,
        Nickname = "LUCARIO",
        OriginalTrainerName = "REON",
        Ball = (byte)Ball.Ultra,
        Language = (int)LanguageID.English,
    };

    [Fact]
    public void BattleVideo4_RoundTripKeepsSpeciesAndHeldItem()
    {
        var pk = GetEntity();

        Span<byte> video = stackalloc byte[BattleVideo4.SizeVideoPoke];
        BattleVideo4.DeflateFromPK4(pk.Data, video);

        var result = new PK4();
        BattleVideo4.InflateToPK4(video, result.Data);

        result.Species.Should().Be(pk.Species);
        result.HeldItem.Should().Be(pk.HeldItem);
        result.PID.Should().Be(pk.PID);
        result.ID32.Should().Be(pk.ID32);
        result.EXP.Should().Be(pk.EXP);
        result.Move1.Should().Be(pk.Move1);
        result.IV_ATK.Should().Be(pk.IV_ATK);
        result.Nickname.Should().Be(pk.Nickname);
        result.OriginalTrainerName.Should().Be(pk.OriginalTrainerName);
        result.Ball.Should().Be(pk.Ball);
    }

    [Fact]
    public void BattlePass_ReadsPartySlotWithItsTrailingMetadata()
    {
        // A pass slot is the stored entity plus four bytes of box/slot metadata; only the entity is written and decrypted.
        var pass = new BattlePass(new byte[BattlePass.Size]);
        var pk = EntityConverter.ConvertToType(GetEntity(), typeof(BK4), out var result);
        pk.Should().NotBeNull($"the conversion should succeed ({result})");

        pass.SetPartySlotBoxSlot(0, 1, 2);
        pass.SetPartySlotAtIndex(pk, 0);

        var slot = pass.GetPartySlotAtIndex(0);
        slot.Species.Should().Be(pk.Species);
        slot.PID.Should().Be(pk.PID);
        slot.Nickname.Should().Be(pk.Nickname);
        pass.GetPartySlotBoxSlot(0).Should().Be(((byte)1, (byte)2), "the metadata past the entity must survive the write");
    }
}
