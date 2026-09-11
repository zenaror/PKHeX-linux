using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace PKHeX.Avalonia.Localization;

/// <summary>
/// Key/value translation lookup loaded from a <c>lang_*.txt</c> file (same format as the WinForms frontend).
/// </summary>
/// <remarks>Ported from the WinForms <c>TranslationContext</c>; framework neutral.</remarks>
public sealed class TranslationContext
{
    public const char Separator = '=';
    private readonly Dictionary<string, string> Translation = [];
    public IReadOnlyDictionary<string, string> Lookup => Translation;
    public bool AddNew { get; set; }

    public void Clear() => Translation.Clear();

    public TranslationContext(ReadOnlySpan<char> content, char separator = Separator)
    {
        var iterator = content.EnumerateLines();
        foreach (var line in iterator)
            LoadLine(line, separator);
    }

    private void LoadLine(ReadOnlySpan<char> line, char separator = Separator)
    {
        var split = line.IndexOf(separator);
        if (split < 0)
            return; // ignore
        var key = line[..split].ToString();
        var value = line[(split + 1)..].ToString();
        Translation.TryAdd(key, value);
    }

    [return: NotNullIfNotNull(nameof(fallback))]
    public string? GetTranslatedText(string val, string? fallback)
    {
        if (Translation.TryGetValue(val, out var translated))
            return translated;

        if (fallback is not null && AddNew)
            Translation.Add(val, fallback);
        return fallback;
    }

    public IEnumerable<string> Write(char separator = Separator)
    {
        return Translation.Select(z => $"{z.Key}{separator}{z.Value}").OrderBy(z => z.Contains('.')).ThenBy(z => z);
    }
}
