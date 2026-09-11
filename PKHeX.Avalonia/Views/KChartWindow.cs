using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Localization;
using PKHeX.Core;
using PKHeX.Drawing;
using PKHeX.Drawing.Misc;
using PKHeX.Drawing.PokeSprite;
using Bitmap = global::Avalonia.Media.Imaging.Bitmap;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Personal-table chart for the loaded save: one row per species/form present in the game
/// (port of the WinForms <c>KChart</c>).
/// </summary>
public sealed class KChartWindow : Window
{
    private readonly ObservableCollection<ChartRow> Rows = [];

    public KChartWindow(SaveFile sav)
    {
        Name = "KChart";
        Title = "KChart";
        Icon = AppIcon.Get();
        Width = 1180;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var grid = new DataGrid
        {
            Name = "DGV",
            ItemsSource = Rows,
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserReorderColumns = true,
            CanUserSortColumns = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            RowHeight = SpriteUtil.Spriter.Height + 1,
        };
        AddColumns(grid, sav);
        Content = grid;

        Populate(sav);
        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
    }

    private static void AddColumns(DataGrid grid, SaveFile sav)
    {
        grid.Columns.Add(Text("Species", nameof(ChartRow.Index), 96));
        grid.Columns.Add(Sprite("Sprite", nameof(ChartRow.Sprite), Math.Max(80, SpriteUtil.Spriter.Width + 2)));
        grid.Columns.Add(Text("Name", nameof(ChartRow.Name), 160));
        grid.Columns.Add(Text("Native", nameof(ChartRow.Native), 80));
        grid.Columns.Add(Colored("BST", nameof(ChartRow.BST), nameof(ChartRow.BSTColor), 76));
        grid.Columns.Add(Text("Catch Rate", nameof(ChartRow.CatchRate), 110));
        grid.Columns.Add(Sprite("Type 1", nameof(ChartRow.Type1), 84));
        grid.Columns.Add(Sprite("Type 2", nameof(ChartRow.Type2), 84));
        grid.Columns.Add(Colored("HP", nameof(ChartRow.HP), nameof(ChartRow.HPColor), 68));
        grid.Columns.Add(Colored("Atk", nameof(ChartRow.ATK), nameof(ChartRow.ATKColor), 68));
        grid.Columns.Add(Colored("Def", nameof(ChartRow.DEF), nameof(ChartRow.DEFColor), 68));
        grid.Columns.Add(Colored("SpA", nameof(ChartRow.SPA), nameof(ChartRow.SPAColor), 68));
        grid.Columns.Add(Colored("SpD", nameof(ChartRow.SPD), nameof(ChartRow.SPDColor), 68));
        grid.Columns.Add(Colored("Spe", nameof(ChartRow.SPE), nameof(ChartRow.SPEColor), 68));
        grid.Columns.Add(Text("Ability 1", nameof(ChartRow.Ability1), 130));
        grid.Columns.Add(Text("Ability 2", nameof(ChartRow.Ability2), 130));
        grid.Columns.Add(Text("Ability H", nameof(ChartRow.AbilityH), 130));
        if (sav is SAV9ZA)
            grid.Columns.Add(Text("Alpha Move", nameof(ChartRow.AlphaMove), 130));
    }

    private static DataGridColumn Text(string header, string path, double width) => new DataGridTextColumn
    {
        Header = header,
        Binding = new Binding(path),
        Width = new DataGridLength(width),
    };

    /// <remarks>The WinForms grid colours the stat cells with <see cref="ColorUtil"/>; the same colours are bound here.</remarks>
    private static DataGridColumn Colored(string header, string path, string colorPath, double width)
    {
        var template = new global::Avalonia.Controls.Templates.FuncDataTemplate<object>((_, _) =>
        {
            var border = new Border { Padding = new global::Avalonia.Thickness(4, 0) };
            border.Bind(Border.BackgroundProperty, new Binding(colorPath));
            var text = new TextBlock { Foreground = Brushes.Black, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
            text.Bind(TextBlock.TextProperty, new Binding(path));
            border.Child = text;
            return border;
        });
        return new DataGridTemplateColumn { Header = header, CellTemplate = template, Width = new DataGridLength(width) };
    }

    private static DataGridColumn Sprite(string header, string path, double width)
    {
        var template = new global::Avalonia.Controls.Templates.FuncDataTemplate<object>((_, _) =>
        {
            var image = new Image { Stretch = Stretch.None, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center };
            image.Bind(Image.SourceProperty, new Binding(path));
            return image;
        });
        return new DataGridTemplateColumn { Header = header, CellTemplate = template, Width = new DataGridLength(width) };
    }

    private void Populate(SaveFile sav)
    {
        var pt = sav.Personal;
        var strings = GameInfo.Strings;
        var species = strings.specieslist;
        var abilities = strings.abilitylist;
        var moves = strings.movelist;
        var format = sav.Generation >= 9 ? "0000" : "000";

        for (ushort s = 1; s <= pt.MaxSpeciesID; s++)
        {
            var fc = pt[s, 0].FormCount;
            var formNames = fc <= 1
                ? []
                : FormConverter.GetFormList(s, strings.Types, strings.forms, GameInfo.GenderSymbolUnicode, sav.Context);

            for (byte f = 0; f < fc; f++)
            {
                if (!pt.IsPresentInGame(s, f))
                    continue;
                var name = f == 0 ? species[s] : $"{species[s]}-{(f < formNames.Length ? formNames[f] : f.ToString())}";
                Rows.Add(BuildRow(sav, pt, s, f, name, format, abilities, moves));
            }
        }
    }

    private static ChartRow BuildRow(SaveFile sav, IPersonalTable pt, ushort species, byte form, string name, string format, string[] abilities, string[] moves)
    {
        var p = pt.GetFormEntry(species, form);
        var abils = p.AbilityCount;
        return new ChartRow
        {
            Index = species.ToString(format) + (form > 0 ? $"-{form:00}" : string.Empty),
            Sprite = SpriteUtil.GetSprite(species, form, 0, 0, 0, false, Shiny.Never, sav.Context)?.ToAvaloniaBitmapAndDispose(),
            Name = name,
            Native = GetIsNative(p, species).ToString(),
            BST = p.BST.ToString("000"),
            BSTColor = ColorUtil.ColorBaseStatTotal(p.BST).ToBrush(),
            CatchRate = p.CatchRate.ToString("000"),
            Type1 = GetTypeSprite(p.Type1, sav.Generation),
            Type2 = p.Type1 == p.Type2 ? null : GetTypeSprite(p.Type2, sav.Generation),
            HP = p.HP.ToString("000"), HPColor = ColorUtil.ColorBaseStat(p.HP).ToBrush(),
            ATK = p.ATK.ToString("000"), ATKColor = ColorUtil.ColorBaseStat(p.ATK).ToBrush(),
            DEF = p.DEF.ToString("000"), DEFColor = ColorUtil.ColorBaseStat(p.DEF).ToBrush(),
            SPA = p.SPA.ToString("000"), SPAColor = ColorUtil.ColorBaseStat(p.SPA).ToBrush(),
            SPD = p.SPD.ToString("000"), SPDColor = ColorUtil.ColorBaseStat(p.SPD).ToBrush(),
            SPE = p.SPE.ToString("000"), SPEColor = ColorUtil.ColorBaseStat(p.SPE).ToBrush(),
            Ability1 = abilities[abils > 0 ? p.GetAbilityAtIndex(0) : 0],
            Ability2 = abilities[abils > 1 ? p.GetAbilityAtIndex(1) : 0],
            AbilityH = abilities[abils > 2 ? p.GetAbilityAtIndex(2) : 0],
            AlphaMove = p is PersonalInfo9ZA za ? moves[za.AlphaMove] : string.Empty,
        };
    }

    private static readonly Dictionary<int, Bitmap?> TypeSprites = [];

    private static Bitmap? GetTypeSprite(byte type, byte generation)
    {
        var key = (generation << 8) | type;
        if (TypeSprites.TryGetValue(key, out var cached))
            return cached;
        return TypeSprites[key] = TypeSpriteUtil.GetTypeSpriteWide(type, generation)?.ToAvaloniaBitmapAndDispose();
    }

    private static bool GetIsNative(IPersonalInfo personalInfo, ushort s) => personalInfo switch
    {
        PersonalInfo7 => IsAlolanNative(s),
        PersonalInfo8SWSH ss => ss.IsInDex,
        PersonalInfo8BDSP bs => bs.IsInDex,
        PersonalInfo8LA bs => bs.IsPresentInGame,
        PersonalInfo9SV sv => sv.IsInDex,
        PersonalInfo9ZA za => za is { IsLumioseNative: true },
        _ => true,
    };

    private static ReadOnlySpan<byte> PastGenAlolanNatives =>
    [
        0x00, 0x1C, 0xF8, 0x1E, 0xF8, 0xC7, 0xFC, 0xFF, 0x1F, 0x9F, 0x47, 0x7F, 0xF3, 0x13, 0xCA, 0xEF,
        0xFF, 0xD7, 0x38, 0x00, 0xE8, 0x7F, 0x0A, 0x46, 0xFE, 0x51, 0xD6, 0xCC, 0x1A, 0xCA, 0x47, 0x00,
        0x00, 0xC0, 0xFF, 0x18, 0x00, 0xCB, 0x38, 0xC0, 0xF3, 0x67, 0xB8, 0xEA, 0xA3, 0x46, 0xFE, 0x01,
        0x00, 0x00, 0x00, 0x0F, 0xCC, 0x6E, 0xC0, 0xF9, 0x1F, 0x7F, 0xEC, 0x54, 0x00, 0x00, 0x00, 0x1C,
        0x00, 0x70, 0x08, 0x00, 0xFC, 0xE3, 0xF2, 0x17, 0xF0, 0x09, 0x05, 0x20, 0x00, 0x6C, 0x79, 0x10,
        0x00, 0x00, 0xE0, 0x28, 0x1C, 0x40, 0xD7, 0xF5, 0x3F, 0x44,
    ];

    private static bool IsAlolanNative(ushort species)
    {
        if (species >= 721)
            return true;
        if (species == 720)
            return false;

        // [0, 719]; always will be a bit in the array.
        return FlagUtil.GetFlag(PastGenAlolanNatives, species);
    }

    private sealed class ChartRow
    {
        public required string Index { get; init; }
        public required Bitmap? Sprite { get; init; }
        public required string Name { get; init; }
        public required string Native { get; init; }
        public required string BST { get; init; }
        public required IBrush BSTColor { get; init; }
        public required string CatchRate { get; init; }
        public required Bitmap? Type1 { get; init; }
        public required Bitmap? Type2 { get; init; }
        public required string HP { get; init; }
        public required IBrush HPColor { get; init; }
        public required string ATK { get; init; }
        public required IBrush ATKColor { get; init; }
        public required string DEF { get; init; }
        public required IBrush DEFColor { get; init; }
        public required string SPA { get; init; }
        public required IBrush SPAColor { get; init; }
        public required string SPD { get; init; }
        public required IBrush SPDColor { get; init; }
        public required string SPE { get; init; }
        public required IBrush SPEColor { get; init; }
        public required string Ability1 { get; init; }
        public required string Ability2 { get; init; }
        public required string AbilityH { get; init; }
        public required string AlphaMove { get; init; }
    }
}
