using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Views.SaveEditors;
using PKHeX.Core;
using PKHeX.Drawing.Misc;

namespace PKHeX.Avalonia.Views.EntityEditors;

/// <summary>
/// Ribbon editor (port of the WinForms <c>RibbonEditor</c>).
/// </summary>
public sealed class RibbonWindow : SaveEditorWindow
{
    private readonly PKM Entity;
    private readonly IReadOnlyList<RibbonInfo> riblist;
    private readonly RibbonStrings RibbonStrings = GameInfo.Strings.Ribbons;

    private readonly Button B_None = UiFactory.Button("B_None", "Remove All");
    private readonly Button B_All = UiFactory.Button("B_All", "Give All");
    private readonly ComboBox CB_Affixed = UiFactory.Combo("CB_Affixed", 200);
    private readonly WrapPanel FLP_Ribbons = new() { Orientation = Orientation.Horizontal, Width = 3 * 44 };
    private readonly Grid TLP_Ribbons = new() { ColumnSpacing = 6, RowSpacing = 2 };

    private readonly Dictionary<string, Border> Sprites = [];
    private readonly Dictionary<string, global::Avalonia.Media.Imaging.Bitmap?> SpriteCache = [];
    private readonly List<CheckBox> Checks = [];
    private readonly List<NumericUpDown> Counts = [];
    private bool EnableBackgroundChange;
    private Border? LastToggledOn;

    public RibbonWindow(PKM pk) : base("RibbonEditor", "Ribbon Editor")
    {
        Entity = pk;
        riblist = RibbonInfo.GetRibbonInfo(pk);
        SizeToContent = SizeToContent.Manual;
        CanResize = true;
        Width = 560;
        Height = 520;

        TLP_Ribbons.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        TLP_Ribbons.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        ButtonBar.Children.Insert(0, B_None);
        ButtonBar.Children.Insert(1, B_All);
        B_All.Click += (_, _) => B_All_Click();
        B_None.Click += (_, _) => B_None_Click();

        var left = UiFactory.Column(CB_Affixed, new ScrollViewer { Content = FLP_Ribbons });
        var right = new ScrollViewer { Content = TLP_Ribbons, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var body = new Grid { ColumnSpacing = 10 };
        body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetColumn(right, 1);
        body.Children.Add(left);
        body.Children.Add(right);

        PopulateRibbons();
        InitializeAffixed(pk);
        SetBody(body);
        EnableBackgroundChange = true;
        Closed += (_, _) =>
        {
            foreach (var bmp in SpriteCache.Values)
                bmp?.Dispose();
            SpriteCache.Clear();
        };
    }

    private void InitializeAffixed(PKM pk)
    {
        if (pk is not IRibbonSetAffixed affixed)
        {
            CB_Affixed.IsVisible = false;
            return;
        }

        const int count = AffixedRibbon.Max + 1; // 0 is a valid index, and max is inclusive with that index.

        var none = GameInfo.GetStrings(MainWindow.CurrentLanguage).Move[0];
        var ds = new List<ComboItem>(1 + count) { new(none, AffixedRibbon.None) };
        var list = Enumerable.Range(0, count).Select(GetComboItem).OrderBy(z => z.Text);
        ds.AddRange(list);

        CB_Affixed.SetItems(ds);
        CB_Affixed.SetValue((int)affixed.AffixedRibbon);
    }

    private ComboItem GetComboItem(int ribbonIndex) => new(GetRibbonPropertyName(ribbonIndex), ribbonIndex);
    private string GetRibbonPropertyName(int z) => RibbonStrings.GetName($"Ribbon{(RibbonIndex)z}");

    private void PopulateRibbons()
    {
        // Add Ribbons
        foreach (var rib in riblist)
            AddRibbonSprite(rib);

        var pk = Entity;
        var la = new LegalityAnalysis(pk);
        Span<RibbonResult> ribbons = stackalloc RibbonResult[riblist.Count];
        var args = new RibbonVerifierArguments(pk, la.EncounterOriginal, la.Info.EvoChainsAllGens);
        var count = RibbonVerifier.GetRibbonResults(args, ribbons);
        var slice = ribbons[..count];

        var dict = new Dictionary<string, RibbonResult>(slice.Length);
        foreach (var r in slice)
            dict.Add(r.PropertyName, r);

        // Find which ribbons are valid by brute forcing all valid ribbons onto the entity.
        // The final ribbon state is what we will use to indicate which are possible.
        var clone = pk.Clone();
        RibbonApplicator.SetAllValidRibbons(clone);
        var otherList = RibbonInfo.GetRibbonInfo(clone);
        var preferred = riblist
            .OrderBy(z => GetSortOrder(z.Name, dict, otherList))
            .ThenBy(z => RibbonStrings.GetName(z.Name));

        foreach (var rib in preferred)
        {
            var name = rib.Name;
            System.Drawing.Color color = dict.TryGetValue(name, out var r)
                ? r.IsMissing ? ColorUtilAvalonia.ColorHint : ColorUtilAvalonia.ColorSuspect
                : GetColor(otherList, name);
            AddRibbonChoice(rib, color);
        }
    }

    private static int GetSortOrder(string name, Dictionary<string, RibbonResult> dict, List<RibbonInfo> otherList)
    {
        if (name.StartsWith("RibbonMark"))
            return 99;
        var other = otherList.Find(z => z.Name == name);
        if (other is { HasRibbon: true })
            return 0;
        if (dict.TryGetValue(name, out var r))
            return r.IsMissing ? 1 : 2;
        return 3; // last
    }

    private static System.Drawing.Color GetColor(List<RibbonInfo> otherList, string ribName)
    {
        if (ribName.StartsWith("RibbonMark"))
            return ColorUtilAvalonia.ColorAlternate;
        var other = otherList.Find(z => z.Name == ribName);
        if (other is null)
            return System.Drawing.Color.Transparent;
        if (!other.HasRibbon)
            return System.Drawing.Color.Transparent;
        return ColorUtilAvalonia.ColorValid;
    }

    private global::Avalonia.Media.Imaging.Bitmap? GetSprite(string resourceKey, Func<SkiaSharp.SKBitmap?> fetch)
    {
        if (SpriteCache.TryGetValue(resourceKey, out var cached))
            return cached;
        var sk = fetch(); // shared resource; do not dispose
        return SpriteCache[resourceKey] = sk?.ToAvaloniaBitmap();
    }

    private void AddRibbonSprite(RibbonInfo rib)
    {
        var name = rib.Name;
        var img = new Image { Width = 40, Height = 40, Stretch = Stretch.None, Source = GetSprite(name, () => RibbonSpriteUtil.GetRibbonSprite(name)) };
        var pb = new Border { Width = 40, Height = 40, Margin = new Thickness(2), IsVisible = false, Child = img, Name = "PB_" + name };
        var display = RibbonStrings.GetName(name);
        ToolTip.SetTip(pb, display);
        if (Entity is IRibbonSetAffixed)
            pb.AttachClick(_ => CB_Affixed.SelectedItem = CB_Affixed.Items.OfType<ComboItem>().FirstOrDefault(z => z.Text == display));
        FLP_Ribbons.Children.Add(pb);
        Sprites[name] = pb;
    }

    private void AddRibbonChoice(RibbonInfo rib, System.Drawing.Color color)
    {
        int row = TLP_Ribbons.RowDefinitions.Count;
        TLP_Ribbons.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var label = new TextBlock
        {
            Name = "L_" + rib.Name,
            Text = RibbonStrings.GetName(rib.Name),
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4, 2),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        var labelBorder = new Border { Child = label, Background = color.A == 0 ? Brushes.Transparent : color.ToBrush() };
        if (color.A != 0)
            label.Foreground = Brushes.Black;
        Grid.SetRow(labelBorder, row);
        Grid.SetColumn(labelBorder, 1);
        TLP_Ribbons.Children.Add(labelBorder);

        if (rib.Type is RibbonValueType.Byte) // numeric count ribbon
            AddRibbonNumericUpDown(rib, row, label);
        else // boolean ribbon
            AddRibbonCheckBox(rib, row, label);
    }

    private void AddRibbonNumericUpDown(RibbonInfo rib, int row, Control label)
    {
        var nud = UiFactory.NumericUpDown("NUD_" + rib.Name, 0, rib.MaxCount, 100);
        nud.ValueChanged += (_, _) =>
        {
            var value = (int)(nud.Value ?? 0);
            var pb = Sprites[rib.Name];
            pb.IsVisible = (rib.RibbonCount = (byte)value) != 0;

            var max = rib.MaxCount;
            if (max == 8 && rib.Name is nameof(IRibbonSetMemory6.RibbonCountMemoryBattle) && Entity.Format >= 9)
                max = 7;
            ((Image)pb.Child!).Source = GetSprite($"{rib.Name}|{max}|{value}", () => RibbonSpriteUtil.GetRibbonSprite(rib.Name, max, value));

            ToggleNewRibbon(rib, pb);
        };

        // Setting value will trigger above event
        nud.Value = Math.Min(rib.MaxCount, rib.RibbonCount);
        if (nud.Value == 0)
            Sprites[rib.Name].IsVisible = false;
        Grid.SetRow(nud, row);
        Grid.SetColumn(nud, 0);
        TLP_Ribbons.Children.Add(nud);
        Counts.Add(nud);

        label.AttachClick(_ => nud.Value = (nud.Value == 0) ? nud.Maximum : 0);
    }

    private void AddRibbonCheckBox(RibbonInfo rib, int row, Control label)
    {
        var chk = UiFactory.Check("CHK_" + rib.Name, string.Empty);
        chk.HorizontalAlignment = HorizontalAlignment.Center;
        chk.IsCheckedChanged += (_, _) =>
        {
            rib.HasRibbon = chk.IsChecked == true;
            var pb = Sprites[rib.Name];
            pb.IsVisible = rib.HasRibbon;
            ToggleNewRibbon(rib, pb);
        };

        // Setting value will trigger above event
        chk.IsChecked = rib.HasRibbon;
        Grid.SetRow(chk, row);
        Grid.SetColumn(chk, 0);
        TLP_Ribbons.Children.Add(chk);
        Checks.Add(chk);

        label.AttachClick(_ => chk.IsChecked = chk.IsChecked != true);
    }

    private void ToggleNewRibbon(RibbonInfo rib, Border pb)
    {
        if (!EnableBackgroundChange)
            return;
        if (LastToggledOn is not null)
            LastToggledOn.Background = Brushes.Transparent;
        pb.Background = rib.HasRibbon ? ColorUtilAvalonia.ColorAccept.ToBrush() : Brushes.Transparent;
        LastToggledOn = pb;
    }

    protected override void OnSave()
    {
        foreach (var rib in riblist)
            ReflectUtil.SetValue(Entity, rib.Name, rib.Type is RibbonValueType.Boolean ? rib.HasRibbon : rib.RibbonCount);

        if (Entity is IRibbonSetAffixed affixed)
            affixed.AffixedRibbon = (sbyte)CB_Affixed.GetValue();
        Close();
    }

    private void B_All_Click()
    {
        if (MainWindow.CurrentModifiers == KeyModifiers.Shift)
        {
            RibbonApplicator.RemoveAllValidRibbons(Entity);
            RibbonApplicator.SetAllValidRibbons(Entity);
            Close();
            return;
        }

        EnableBackgroundChange = false;
        foreach (var chk in Checks)
            chk.IsChecked = true;
        foreach (var nud in Counts)
            nud.Value = nud.Maximum;
        EnableBackgroundChange = true;
    }

    private void B_None_Click()
    {
        if (MainWindow.CurrentModifiers == KeyModifiers.Shift)
        {
            RibbonApplicator.RemoveAllValidRibbons(Entity);
            if (Entity is IRibbonSetAffixed affixed)
                affixed.AffixedRibbon = AffixedRibbon.None;
            Close();
            return;
        }

        CB_Affixed.SetValue((int)AffixedRibbon.None);
        foreach (var chk in Checks)
            chk.IsChecked = false;
        foreach (var nud in Counts)
            nud.Value = 0;
    }
}
