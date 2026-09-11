using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using PKHeX.Avalonia.Startup;

namespace PKHeX.Avalonia.Localization;

/// <summary>
/// Translates the user interface using the WinForms <c>lang_*.txt</c> translation files.
/// </summary>
/// <remarks>
/// Keys follow the WinForms convention: <c>{FormName}.{ControlName}</c> (e.g. <c>Main.Menu_Open</c>) and <c>{FormName}</c> for the window title.
/// Controls must carry the same <see cref="StyledElement.Name"/> as their WinForms counterpart to receive a translation.
/// An external <c>lang_{lang}.txt</c> in the executable directory overrides the embedded file, like WinForms.
/// </remarks>
public static class Translator
{
    private static readonly Dictionary<string, TranslationContext> Context = [];

    public static string GetKey(ReadOnlySpan<char> formName, ReadOnlySpan<char> name) => $"{formName}.{name}";
    public static IReadOnlyDictionary<string, string> GetDictionary(string lang) => GetContext(lang).Lookup;

    public static string TranslateText(string key, string fallback, string lang) => GetContext(lang).GetTranslatedText(key, fallback);

    public static string TranslateEnum<T>(T value, string lang) where T : Enum =>
        TranslateEnum(typeof(T).Name, value.ToString(), lang);

    public static string[] GetEnumTranslation<T>(string lang) where T : struct, Enum
    {
        var type = typeof(T);
        var names = Enum.GetNames(type);
        var result = new string[names.Length];
        for (int i = 0; i < names.Length; i++)
            result[i] = TranslateEnum(type.Name, names[i], lang);
        return result;
    }

    private static string TranslateEnum(string type, string value, string lang)
    {
        var context = GetContext(lang);
        var key = $"{type}.{value}";
        return context.GetTranslatedText(key, value);
    }

    private static TranslationContext GetContext(string lang)
    {
        if (Context.TryGetValue(lang, out var context))
            return context;

        var lines = GetTranslationFile(lang);
        Context.Add(lang, context = new TranslationContext(lines));
        return context;
    }

    private static string GetTranslationFile(string lang)
    {
        // Check to see if the desired translation file exists in the same folder as the executable
        var externalLangPath = Path.Combine(AppPaths.ExecutableDirectory, $"lang_{lang}.txt");
        if (File.Exists(externalLangPath))
        {
            try { return File.ReadAllText(externalLangPath); }
            catch { /* In use? Just return the internal resource. */ }
        }

        return AppResources.GetText($"lang_{lang}") ?? string.Empty;
    }

    /// <summary>
    /// Translates the window title and all named descendant controls of the <paramref name="window"/>.
    /// </summary>
    /// <param name="window">Window to translate; its <see cref="StyledElement.Name"/> is the WinForms form name.</param>
    /// <param name="lang">Language code</param>
    public static void TranslateInterface(Window window, string lang)
    {
        var context = GetContext(lang);
        var formName = window.Name ?? window.GetType().Name;
        window.Title = context.GetTranslatedText(formName, window.Title ?? string.Empty);
        TranslateChildren(window, formName, context);
    }

    /// <summary>
    /// Translates all named descendant controls of the <paramref name="root"/> using <paramref name="formName"/> as the key prefix.
    /// </summary>
    public static void TranslateControls(ILogical root, string formName, string lang)
        => TranslateChildren(root, formName, GetContext(lang));

    private static void TranslateChildren(ILogical root, string formName, TranslationContext context)
    {
        foreach (var child in root.GetLogicalDescendants())
        {
            if (child is not StyledElement { Name: { Length: > 0 } name } element)
                continue;
            TranslateControl(element, name, formName, context);
        }
    }

    private static void TranslateControl(StyledElement element, string name, string formName, TranslationContext context)
    {
        var key = GetKey(formName, name);
        switch (element)
        {
            case MenuItem { Header: string current } m:
                m.Header = Translate(context, key, current);
                break;
            case TextBlock tb:
                tb.Text = Translate(context, key, tb.Text ?? string.Empty, false);
                break;
            case HeaderedContentControl { Header: string current } h: // TabItem, Expander, HeaderedContentControl
                h.Header = Translate(context, key, current);
                break;
            case HeaderedSelectingItemsControl { Header: string current } hs:
                hs.Header = Translate(context, key, current);
                break;
            case ContentControl { Content: string current } c: // Button, CheckBox, RadioButton, Label
                c.Content = Translate(context, key, current);
                break;
        }
    }

    private static string Translate(TranslationContext context, string key, string current, bool accessKeys = true)
    {
        var translated = context.GetTranslatedText(key, null);
        if (translated is null)
            return current; // no translation available; keep the current (already converted) text
        return accessKeys ? ConvertAccessKeys(translated) : translated;
    }

    /// <summary>
    /// Converts WinForms mnemonics (<c>&amp;File</c>) to Avalonia access keys (<c>_File</c>).
    /// </summary>
    public static string ConvertAccessKeys(string text)
    {
        if (!text.Contains('&') && !text.Contains('_'))
            return text;
        var sb = new System.Text.StringBuilder(text.Length + 2);
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '_')
            {
                sb.Append("__"); // literal underscore
            }
            else if (c == '&')
            {
                if (i + 1 < text.Length && text[i + 1] == '&')
                {
                    sb.Append('&'); // literal ampersand
                    i++;
                }
                else
                {
                    sb.Append('_');
                }
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
