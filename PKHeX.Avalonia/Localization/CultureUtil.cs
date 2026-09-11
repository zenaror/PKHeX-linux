using System.Globalization;
using System.Threading;
using PKHeX.Core;

namespace PKHeX.Avalonia.Localization;

/// <summary>
/// Culture helpers (ported from the WinForms <c>WinFormsUtil</c> culture methods; framework neutral).
/// </summary>
public static class CultureUtil
{
    /// <summary>
    /// Gets the language code for a supported <see cref="GameLanguage"/> based on the current UI culture.
    /// </summary>
    /// <remarks>
    /// Initially, CurrentUICulture is set based on the user's system language preferences.
    /// Once <see cref="SetCultureLanguage"/> is called, it becomes the current display language instead.
    /// </remarks>
    /// <returns>A supported language code.</returns>
    public static string GetCultureLanguage()
    {
        var ci = Thread.CurrentThread.CurrentUICulture;
        var name = ci.Name;
        var code = ci.TwoLetterISOLanguageName;
        return code switch
        {
            // For languages with multiple supported variants, map the language tag to one of the supported ones
            "es" => name switch
            {
                "es" or "es-ES" or "es-ES_tradnl" or "es-GQ"   => "es",     // Spanish (Spain)
                                                             _ => "es-419", // Spanish (Latin America)
            },
            "zh" => name switch
            {
                "zh-Hant" or "zh-HK" or "zh-MO" or "zh-TW"   => "zh-Hant", // Traditional Chinese (Hong Kong/Macau/Taiwan)
                                                           _ => "zh-Hans", // Simplified Chinese (China/Singapore)
            },

            // Use this language code if we support it, otherwise default to English
            _ => GameLanguage.IsLanguageValid(code) ? code : GameLanguage.DefaultLanguage,
        };
    }

    /// <summary>
    /// Sets the culture.
    /// </summary>
    /// <param name="lang">Language code</param>
    public static void SetCultureLanguage(string lang)
    {
        var ci = new CultureInfo(lang);
        Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = ci;
    }
}
