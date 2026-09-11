using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen9.EventWork;

/// <summary>Name/hash lookup and reverse lookup (port of the WinForms <c>EventWorkLookup</c>).</summary>
public sealed class EventWorkLookup(Dictionary<ulong, string> forward)
{
    private readonly Dictionary<string, ulong> _reverse = forward
        .GroupBy(z => z.Value)
        .ToDictionary(z => z.Key, z => z.First().Key);

    public string GetName(ulong hash) => forward.TryGetValue(hash, out var name) ? name : hash.ToString("X16");

    public ulong GetHash(string nameOrHex)
    {
        if (_reverse.TryGetValue(nameOrHex, out var h))
            return h;
        if (ulong.TryParse(nameOrHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return FnvHash.HashFnv1a_64(nameOrHex);
    }
}

/// <summary>A page of the flag/work editor that can be loaded from and saved back to a block.</summary>
public interface IEventWorkGrid
{
    void Load();
    void Save();
}
