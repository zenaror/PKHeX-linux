using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Settings;
using PKHeX.Avalonia.Views;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls.Hover;

/// <summary>
/// Fills in the hover content for a slot: either the rich <see cref="SlotPreviewView"/> card or the plain
/// Showdown text, plus the cry (port of the WinForms <c>SummaryPreviewer</c>).
/// </summary>
/// <remarks>
/// WinForms shows the card in a separate non-activating window it positions by hand. Avalonia's tooltip already
/// behaves that way, so the card is assigned as the target's tooltip content and the framework places it.
/// </remarks>
public sealed class SummaryPreviewer
{
    private readonly CryPlayer Cry = new();
    private Control? Current;

    private static HoverSettings Settings => MainWindow.Settings.Hover;

    /// <summary>
    /// Shows the preview for an entity over the given control.
    /// </summary>
    public void Show(Control target, PKM pk, StorageSlotType type = StorageSlotType.None, KeyModifiers modifiers = KeyModifiers.None)
    {
        if (pk.Species == 0)
        {
            Clear();
            return;
        }

        var programLanguage = Language.GetLanguageValue(MainWindow.Settings.Startup.Language);
        var cfg = MainWindow.Settings.BattleTemplate;
        var settings = cfg.Hover.GetSettings(programLanguage, pk.Context);
        var hover = Settings;

        if (hover.HoverSlotShowPreview && modifiers != KeyModifiers.Alt)
        {
            var localize = LegalityLocalizationSet.GetLocalization(programLanguage);
            var la = new LegalityAnalysis(pk, type);
            var ctx = LegalityLocalizationContext.Create(la, localize);

            var view = new SlotPreviewView();
            view.Populate(pk, settings, ctx, hover);
            SetTip(target, view);
        }
        else if (hover.HoverSlotShowText)
        {
            if (!settings.Order.Contains(BattleTemplateToken.FirstLine))
            {
                var temp = new BattleTemplateToken[settings.Order.Length + 1];
                settings.Order.CopyTo(temp.AsSpan(1));
                temp[0] = BattleTemplateToken.FirstLine;
                settings = settings with { Order = temp };
            }
            var text = GetPreviewText(pk, settings);
            SlotPreviewView.TryAppendOtherStats(pk, ref text, settings);
            if (hover.HoverSlotShowEncounter)
            {
                var localize = LegalityLocalizationSet.GetLocalization(programLanguage);
                var la = new LegalityAnalysis(pk, type);
                text = AppendEncounterInfo(LegalityLocalizationContext.Create(la, localize), text);
            }
            SetTip(target, text);
        }

        if (hover.HoverSlotPlayCry)
            Cry.PlayCry(pk, pk.Context);
    }

    /// <summary>
    /// Shows the text preview for an encounter over the given control.
    /// </summary>
    public void Show(Control target, IEncounterInfo enc)
    {
        if (enc.Species == 0)
        {
            Clear();
            return;
        }

        if (Settings.HoverSlotShowText)
            SetTip(target, GetPreviewText(enc, Settings.HoverSlotShowEncounterVerbose));
        if (Settings.HoverSlotPlayCry)
            Cry.PlayCry(enc, enc.Context);
    }

    /// <summary>Hides the current preview and stops the cry.</summary>
    public void Clear()
    {
        if (Current is { } target)
        {
            ToolTip.SetIsOpen(target, false);
            Current = null;
        }
        Cry.Stop();
    }

    private void SetTip(Control target, object content)
    {
        Current = target;
        ToolTip.SetShowDelay(target, 200);
        ToolTip.SetTip(target, content);
    }

    public static string GetPreviewText(PKM pk, BattleTemplateExportSettings settings) => ShowdownParsing.GetLocalizedPreviewText(pk, settings);

    public static string AppendEncounterInfo(LegalityLocalizationContext la, string text)
    {
        var result = new List<string>(8);
        if (text.Length != 0) // add a blank line between the set and the encounter info if isn't already a blank line
        {
            result.Add(text);
            result.Add(string.Empty);
        }
        LegalityFormatting.AddEncounterInfo(la, result);
        return string.Join(Environment.NewLine, result);
    }

    private static string GetPreviewText(IEncounterInfo enc, bool verbose = false)
    {
        var lines = enc.GetTextLines(verbose, MainWindow.CurrentLanguage);
        return string.Join(Environment.NewLine, lines);
    }

    public static string AppendLegalityHint(in LegalityLocalizationContext la, string line)
    {
        // Get the first illegal check result, and append the localization of it as the hint.
        // If all legal, return the input string unchanged.
        var analysis = la.Analysis;
        if (analysis.Valid)
            return line;

        foreach (var chk in analysis.Results)
        {
            if (chk.Valid)
                continue;
            var hint = la.Humanize(chk, verbose: true);
            return Join(line, hint);
        }

        for (var i = 0; i < analysis.Info.Moves.Length; i++)
        {
            var chk = analysis.Info.Moves[i];
            if (chk.Valid)
                continue;
            var hint = la.FormatMove(chk, i + 1, la.Analysis.Info.Entity.Context);
            return Join(line, hint);
        }

        for (var i = 0; i < analysis.Info.Relearn.Length; i++)
        {
            var chk = analysis.Info.Relearn[i];
            if (chk.Valid)
                continue;
            var hint = la.FormatMove(chk, i + 1, la.Analysis.Info.Entity.Context);
            return Join(line, hint);
        }

        return line;

        static string Join(string line, string hint)
        {
            if (hint.Length > 67)
                hint = hint[..67] + "...";
            return string.IsNullOrEmpty(line) ? hint : $"{line}{Environment.NewLine}{hint}";
        }
    }
}
