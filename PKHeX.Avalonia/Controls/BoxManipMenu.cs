using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Box manipulation context menu: sort / delete / modify (port of the WinForms <c>BoxMenuStrip</c>).
/// </summary>
/// <remarks>
/// The Core <see cref="BoxManipulator"/> asks for confirmation synchronously; Avalonia dialogs are asynchronous,
/// so the prompt is shown before the manipulation and the region check is then done without prompting.
/// </remarks>
public sealed class BoxManipMenu : ContextMenu
{
    /// <summary>
    /// Avalonia matches a style (and therefore the theme's control template) by the exact style key, so a
    /// derived menu gets no template at all: the popup opens empty, unsized and unpositioned. Report the base key.
    /// </summary>
    protected override Type StyleKeyOverride => typeof(ContextMenu);

    private readonly SAVEditorView Editor;
    private readonly List<(MenuItem Item, IBoxManip Manip)> CustomItems = [];
    private readonly Manipulator Worker;

    public BoxManipMenu(SAVEditorView editor)
    {
        Editor = editor;
        Worker = new Manipulator(editor);
        var categories = BoxManipUtil.ManipCategories;
        var names = BoxManipUtil.ManipCategoryNames;
        for (int i = 0; i < categories.Length; i++)
        {
            var category = categories[i];
            var name = names[i];
            var parent = new MenuItem { Name = $"mnu_{name}", Header = name, Icon = GetIcon(TopLevelImages[i]) };
            foreach (var item in category)
                AddItem(parent, item);
            Items.Add(parent);
        }
        // The keys live under the Main form in lang_*.txt; WinForms reaches this menu through Main's control tree.
        Translator.TranslateControls(this, "Main", MainWindow.CurrentLanguage);
    }

    private void AddItem(MenuItem parent, IBoxManip item)
    {
        var name = item.Type.ToString();
        ManipTypeImage.TryGetValue(item.Type, out var img);
        var tsi = new MenuItem { Name = $"mnu_{name}", Header = name, Icon = GetIcon(img) };
        tsi.Click += async (_, _) => await Execute(item, Reverse);
        parent.Items.Add(tsi);
        CustomItems.Add((tsi, item));
    }

    /// <summary>WinForms inverts this menu's icons in dark mode (<c>SAVEditor</c> ctor, <c>InvertToolStripIcons</c>).</summary>
    private static Image? GetIcon(string? name)
    {
        if (name is null)
            return null;
        var bmp = App.IsDarkModeEnabled ? AppResources.GetImageBlackToWhite(name) : AppResources.GetImage(name);
        return bmp is null ? null : new Image { Source = bmp, Width = 16, Height = 16 };
    }

    private static readonly string[] TopLevelImages = ["nocheck", "swapBox", "settings", "wand"];

    private static readonly Dictionary<BoxManipType, string> ManipTypeImage = new()
    {
        [BoxManipType.DeleteAll] = "nocheck",
        [BoxManipType.DeleteEggs] = "about",
        [BoxManipType.DeletePastGen] = "bak",
        [BoxManipType.DeleteForeign] = "language",
        [BoxManipType.DeleteUntrained] = "gift",
        [BoxManipType.DeleteItemless] = "main",
        [BoxManipType.DeleteIllegal] = "export",
        [BoxManipType.DeleteClones] = "users",

        [BoxManipType.SortSpecies] = "numlohi",
        [BoxManipType.SortSpeciesReverse] = "numhilo",
        [BoxManipType.SortLevel] = "vallohi",
        [BoxManipType.SortLevelReverse] = "valhilo",
        [BoxManipType.SortDate] = "date",
        [BoxManipType.SortName] = "alphaAZ",
        [BoxManipType.SortFavorite] = "heart",
        [BoxManipType.SortParty] = "users",
        [BoxManipType.SortShiny] = "showdown",
        [BoxManipType.SortAlpha] = "alpha",
        [BoxManipType.SortRandom] = "wand",

        [BoxManipType.SortUsage] = "heart",
        [BoxManipType.SortPotential] = "numhilo",
        [BoxManipType.SortTraining] = "showdown",
        [BoxManipType.SortOwner] = "users",
        [BoxManipType.SortType] = "main",
        [BoxManipType.SortTypeTera] = "main",
        [BoxManipType.SortVersion] = "numlohi",
        [BoxManipType.SortBST] = "vallohi",
        [BoxManipType.SortCP] = "vallohi",
        [BoxManipType.SortScale] = "vallohi",
        [BoxManipType.SortRibbons] = "valhilo",
        [BoxManipType.SortMarks] = "valhilo",
        [BoxManipType.SortLegal] = "export",
        [BoxManipType.SortEncounterType] = "about",

        [BoxManipType.ModifyHatchEggs] = "about",
        [BoxManipType.ModifyMaxFriendship] = "users",
        [BoxManipType.ModifyMaxLevel] = "showdown",
        [BoxManipType.ModifyResetMoves] = "date",
        [BoxManipType.ModifyRandomMoves] = "wand",
        [BoxManipType.ModifyHyperTrain] = "vallohi",
        [BoxManipType.ModifyGanbaru] = "vallohi",
        [BoxManipType.ModifyRemoveNicknames] = "alphaAZ",
        [BoxManipType.ModifyRemoveItem] = "gift",
        [BoxManipType.ModifyHeal] = "heart",
    };

    /// <param name="reverse">Invert the criteria / reverse the sort (Ctrl on a menu entry).</param>
    private async Task Execute(IBoxManip item, bool reverse)
    {
        bool all = All;
        var prompt = item.GetPrompt(all);
        if (!string.IsNullOrEmpty(prompt))
        {
            if (await AppDialogs.Prompt(Editor.Owner, MessageBoxButtons.YesNo, prompt) != DialogResult.Yes)
                return;
        }

        var start = all ? 0 : Editor.CurrentBox;
        var stop = all ? Editor.SAV.BoxCount - 1 : Editor.CurrentBox;
        if (Editor.SAV.IsAnySlotLockedInBox(start, stop))
        {
            var fail = item.GetFail(all);
            if (!string.IsNullOrEmpty(fail))
                await AppDialogs.Alert(Editor.Owner, fail);
            return;
        }

        Worker.Execute(item, Editor.CurrentBox, all, reverse);
        if (Worker.LastMessage is { Length: > 0 } message)
            await AppDialogs.Alert(Editor.Owner, message);
    }

    /// <summary>
    /// Hides the operations that the current save file cannot perform.
    /// </summary>
    public void ToggleVisibility()
    {
        var sav = Editor.SAV;
        foreach (var (item, manip) in CustomItems)
            item.IsVisible = manip.Usable(sav);
    }

    /// <summary>Alt+click shortcut: clear the box (all boxes with Shift).</summary>
    public Task Clear() => Execute(BoxManipType.DeleteAll.GetManip(), false);

    /// <summary>Ctrl+click shortcut: sort the box by species (all boxes with Shift).</summary>
    public Task Sort() => Execute(BoxManipType.SortSpecies.GetManip(), false);

    private static bool All => MainWindow.CurrentModifiers.HasFlag(KeyModifiers.Shift);
    private static bool Reverse => MainWindow.CurrentModifiers.HasFlag(KeyModifiers.Control);

    /// <summary>
    /// Box manipulator that records the completion message instead of prompting (prompts happen before execution).
    /// </summary>
    private sealed class Manipulator(SAVEditorView editor) : BoxManipulator
    {
        protected override SaveFile SAV => editor.SAV;
        public string? LastMessage { get; private set; }

        protected override bool CanManipulateRegion(int start, int end, string prompt, string fail)
            => base.CanManipulateRegion(start, end, prompt, fail);

        protected override void FinishBoxManipulation(string message, bool all, int count)
        {
            editor.SetPKMBoxes();
            editor.UpdateBoxViewers(all);
            LastMessage = string.IsNullOrWhiteSpace(message) ? null : $"{message} ({count})";
        }
    }
}
