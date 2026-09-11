using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Provides slot viewers with the save file context and interaction routing.
/// </summary>
public interface ISaveHost : ISlotHost
{
    SaveFile SAV { get; }
    SlotPublisher<SlotView> Publisher { get; }
    bool IsControlHeld { get; }
}
