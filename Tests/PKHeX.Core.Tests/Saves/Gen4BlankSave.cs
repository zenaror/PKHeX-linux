using System;
using FluentAssertions;
using Xunit;

namespace PKHeX.Core.Tests.Saves;

/// <summary>
/// A blank Generation 4 save has to survive a write and a re-read.
/// </summary>
/// <remarks>
/// Linux port regression guard. The blank constructor used to allocate only the General and Storage buffers and leave
/// <see cref="SaveFile.Data"/> empty, so writing walked the extra blocks over an empty span and threw, and would have
/// produced no data even without the throw. The port builds Generation 4 fixtures this way, because no real Diamond,
/// Pearl, Platinum, HeartGold or SoulSilver save is available to test those editors with.
/// </remarks>
public class Gen4BlankSave
{
    [Theory]
    [InlineData(GameVersion.D)]
    [InlineData(GameVersion.P)]
    [InlineData(GameVersion.Pt)]
    [InlineData(GameVersion.HG)]
    [InlineData(GameVersion.SS)]
    public void BlankSaveWritesAndReadsBack(GameVersion version)
    {
        var blank = BlankSaveFile.Get(version, null);
        var data = blank.Write();
        data.Length.Should().Be(SaveUtil.SIZE_G4RAW);

        var reloaded = SaveUtil.GetSaveFile(data);
        reloaded.Should().NotBeNull("a written blank save has to be recognized again");
        reloaded!.Generation.Should().Be(4);
        reloaded.ChecksumsValid.Should().BeTrue();
        reloaded.State.Exportable.Should().BeTrue();
    }

    [Theory]
    [InlineData(GameVersion.Pt)]
    [InlineData(GameVersion.HG)]
    public void BlankSaveRoundTripsUnchanged(GameVersion version)
    {
        var first = BlankSaveFile.Get(version, null).Write();
        var second = SaveUtil.GetSaveFile(first)!.Write();
        second.Span.SequenceEqual(first.Span).Should().BeTrue("writing what was just read must not change a byte");
    }
}
