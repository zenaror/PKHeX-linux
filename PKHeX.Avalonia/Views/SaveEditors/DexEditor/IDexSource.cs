using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.DexEditor;

/// <summary>
/// Everything the shared Pokédex editor needs from one generation's dex block.
/// </summary>
/// <remarks>
/// <see cref="Zukan5"/> and <see cref="Zukan6"/> expose the same operations but share no base type in Core,
/// and each generation adds one or two extras (the foreign-origin flag in X/Y, the DexNav counters in OR/AS).
/// This adapter keeps the editor itself generation-neutral without touching Core.
/// </remarks>
public interface IDexSource
{
    /// <summary>Highest species index the block can store.</summary>
    ushort MaxSpecies { get; }

    ushort InitialSpecies { get; set; }
    bool IsNationalDexUnlocked { get; set; }
    bool IsNationalDexMode { get; set; }
    uint Spinda { get; set; }

    /// <summary>True while the Spinda spot pattern applies to the selected species.</summary>
    bool ShowSpinda(ushort species);

    /// <summary>X/Y records whether the entry came from another language's game.</summary>
    bool SupportsForeignFlag { get; }
    bool ForeignFlagApplies(ushort species);
    bool GetForeignFlag(ushort species);
    void SetForeignFlag(ushort species, bool value);

    /// <summary>OR/AS keeps DexNav seen / obtained counters per species.</summary>
    bool SupportsCounts { get; }
    ushort GetCountSeen(ushort species);
    void SetCountSeen(ushort species, ushort value);
    ushort GetCountObtained(ushort species);
    void SetCountObtained(ushort species, ushort value);
    void SetAllCountSeen(ushort value);

    /// <summary>Generation 5 only stores language flags for the first 493 species.</summary>
    bool LanguageEditable(ushort species);

    bool GetCaught(ushort species);
    void SetCaught(ushort species, bool value);
    bool GetSeen(ushort species, int region);
    void SetSeen(ushort species, int region, bool value);
    bool GetDisplayed(ushort species, int region);
    void SetDisplayed(ushort species, int region, bool value);
    bool GetLanguageFlag(ushort species, int index);
    void SetLanguageFlag(ushort species, int index, bool value);

    (int Index, int Count) GetFormIndex(ushort species);
    bool GetFormFlag(int formIndex, int region);
    void SetFormFlag(int formIndex, int region, bool value);

    void GiveAll(ushort species, bool state, bool shinyToo, LanguageID language, bool allLanguages);
    void SeenNone();
    void SeenAll(bool shinyToo);
    void CaughtNone();
    void CaughtAll(LanguageID language, bool allLanguages);
    void ClearFormSeen();
    void SetFormsSeen1(bool shinyToo);
    void SetFormsSeen(bool shinyToo);
}

/// <summary>Adapter for the Generation 5 dex block.</summary>
public sealed class Zukan5Source(Zukan5 dex, ushort maxSpecies) : IDexSource
{
    public ushort MaxSpecies => maxSpecies;
    public ushort InitialSpecies { get => dex.InitialSpecies; set => dex.InitialSpecies = value; }
    public bool IsNationalDexUnlocked { get => dex.IsNationalDexUnlocked; set => dex.IsNationalDexUnlocked = value; }
    public bool IsNationalDexMode { get => dex.IsNationalDexMode; set => dex.IsNationalDexMode = value; }
    public uint Spinda { get => dex.Spinda; set => dex.Spinda = value; }
    public bool ShowSpinda(ushort species) => true; // the Gen 5 form always shows the field

    public bool SupportsForeignFlag => false;
    public bool ForeignFlagApplies(ushort species) => false;
    public bool GetForeignFlag(ushort species) => false;
    public void SetForeignFlag(ushort species, bool value) { }

    public bool SupportsCounts => false;
    public ushort GetCountSeen(ushort species) => 0;
    public void SetCountSeen(ushort species, ushort value) { }
    public ushort GetCountObtained(ushort species) => 0;
    public void SetCountObtained(ushort species, ushort value) { }
    public void SetAllCountSeen(ushort value) { }

    public bool LanguageEditable(ushort species) => species <= 493;

    public bool GetCaught(ushort species) => dex.GetCaught(species);
    public void SetCaught(ushort species, bool value) => dex.SetCaught(species, value);
    public bool GetSeen(ushort species, int region) => dex.GetSeen(species, region);
    public void SetSeen(ushort species, int region, bool value) => dex.SetSeen(species, region, value);
    public bool GetDisplayed(ushort species, int region) => dex.GetDisplayed(species, region);
    public void SetDisplayed(ushort species, int region, bool value) => dex.SetDisplayed(species, region, value);
    public bool GetLanguageFlag(ushort species, int index) => dex.GetLanguageFlag(species, index);
    public void SetLanguageFlag(ushort species, int index, bool value) => dex.SetLanguageFlag(species, index, value);

    public (int Index, int Count) GetFormIndex(ushort species)
    {
        var (index, count) = dex.GetFormIndex(species);
        return (index, count);
    }

    public bool GetFormFlag(int formIndex, int region) => dex.GetFormFlag(formIndex, region);
    public void SetFormFlag(int formIndex, int region, bool value) => dex.SetFormFlag(formIndex, region, value);

    public void GiveAll(ushort species, bool state, bool shinyToo, LanguageID language, bool allLanguages)
        => dex.GiveAll(species, state, shinyToo, language, allLanguages);

    public void SeenNone() => dex.SeenNone();
    public void SeenAll(bool shinyToo) => dex.SeenAll(shinyToo);
    public void CaughtNone() => dex.CaughtNone();
    public void CaughtAll(LanguageID language, bool allLanguages) => dex.CaughtAll(language, allLanguages);
    public void ClearFormSeen() => dex.ClearFormSeen();
    public void SetFormsSeen1(bool shinyToo) => dex.SetFormsSeen1(shinyToo);
    public void SetFormsSeen(bool shinyToo) => dex.SetFormsSeen(shinyToo);
}

/// <summary>Adapter for the Generation 6 dex block (X/Y and OR/AS).</summary>
public sealed class Zukan6Source(Zukan6 dex, ushort maxSpecies) : IDexSource
{
    private readonly Zukan6XY? XY = dex as Zukan6XY;
    private readonly Zukan6AO? AO = dex as Zukan6AO;

    public ushort MaxSpecies => maxSpecies;
    public ushort InitialSpecies { get => dex.InitialSpecies; set => dex.InitialSpecies = value; }
    public bool IsNationalDexUnlocked { get => dex.IsNationalDexUnlocked; set => dex.IsNationalDexUnlocked = value; }
    public bool IsNationalDexMode { get => dex.IsNationalDexMode; set => dex.IsNationalDexMode = value; }
    public uint Spinda { get => dex.Spinda; set => dex.Spinda = value; }
    public bool ShowSpinda(ushort species) => species == (int)Species.Spinda;

    public bool SupportsForeignFlag => XY is not null;
    public bool ForeignFlagApplies(ushort species) => XY is not null && species <= (int)Species.Genesect;
    public bool GetForeignFlag(ushort species) => XY?.GetForeignFlag(species) == true;
    public void SetForeignFlag(ushort species, bool value) => XY?.SetForeignFlag(species, value);

    public bool SupportsCounts => AO is not null;
    public ushort GetCountSeen(ushort species) => AO?.GetCountSeen(species) ?? 0;
    public void SetCountSeen(ushort species, ushort value) => AO?.SetCountSeen(species, value);
    public ushort GetCountObtained(ushort species) => AO?.GetCountObtained(species) ?? 0;
    public void SetCountObtained(ushort species, ushort value) => AO?.SetCountObtained(species, value);

    public void SetAllCountSeen(ushort value)
    {
        if (AO is null)
            return;
        for (ushort i = 0; i < maxSpecies; i++)
            AO.SetCountSeen(i, value);
    }

    public bool LanguageEditable(ushort species) => true;

    public bool GetCaught(ushort species) => dex.GetCaught(species);
    public void SetCaught(ushort species, bool value) => dex.SetCaught(species, value);
    public bool GetSeen(ushort species, int region) => dex.GetSeen(species, region);
    public void SetSeen(ushort species, int region, bool value) => dex.SetSeen(species, region, value);
    public bool GetDisplayed(ushort species, int region) => dex.GetDisplayed(species, region);
    public void SetDisplayed(ushort species, int region, bool value) => dex.SetDisplayed(species, region, value);
    public bool GetLanguageFlag(ushort species, int index) => dex.GetLanguageFlag(species, index);
    public void SetLanguageFlag(ushort species, int index, bool value) => dex.SetLanguageFlag(species, index, value);

    public (int Index, int Count) GetFormIndex(ushort species)
    {
        var (index, count) = dex.GetFormIndex(species);
        return (index, count);
    }

    public bool GetFormFlag(int formIndex, int region) => dex.GetFormFlag(formIndex, region);
    public void SetFormFlag(int formIndex, int region, bool value) => dex.SetFormFlag(formIndex, region, value);

    public void GiveAll(ushort species, bool state, bool shinyToo, LanguageID language, bool allLanguages)
        => dex.GiveAll(species, state, shinyToo, language, allLanguages);

    public void SeenNone() => dex.SeenNone();
    public void SeenAll(bool shinyToo) => dex.SeenAll(shinyToo);
    public void CaughtNone() => dex.CaughtNone();
    public void CaughtAll(LanguageID language, bool allLanguages) => dex.CaughtAll(language, allLanguages);
    public void ClearFormSeen() => dex.ClearFormSeen();
    public void SetFormsSeen1(bool shinyToo) => dex.SetFormsSeen1(shinyToo);
    public void SetFormsSeen(bool shinyToo) => dex.SetFormsSeen(shinyToo);
}
