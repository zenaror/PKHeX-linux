using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen7;

/// <summary>
/// Pokédex editor for Let's Go Pikachu / Eevee (port of the WinForms <c>SAV_PokedexGG</c>).
/// </summary>
/// <remarks>
/// Shares the Generation 7 entry-index layout, and adds the four size records the games keep per species and
/// form: the smallest and largest height and weight ever registered, each with its own "was recorded" flag.
/// </remarks>
public sealed class PokedexGGWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV7b SAV;
    private readonly Zukan7b Dex;
    private readonly CheckBox[] CP;
    private readonly CheckBox[] CL;
    private readonly CheckBox[] RecordUsed;
    private readonly CheckBox[] RecordFlag;
    private readonly NumericUpDown[] RecordHeight;
    private readonly NumericUpDown[] RecordWeight;

    private bool editing;
    private bool allModifying;
    private int currentIndex = -1;

    private readonly ListBox LB_Species = new() { Name = "LB_Species", Width = 240, Height = 430 };
    private readonly ObservableCollection<string> SpeciesItems = [];
    private readonly ListBox LB_Forms = new() { Name = "LB_Forms", Width = 160, Height = 180 };
    private readonly ObservableCollection<string> FormItems = [];
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);
    private readonly Button B_GiveAll = UiFactory.Button("B_GiveAll", "Check All");
    private readonly Button B_Modify = UiFactory.Button("B_Modify", "Modify...");
    private readonly Button B_Counts = UiFactory.Button("B_Counts", "Capture Records");
    private GroupBoxView GB_SizeRecords = null!;

    public PokedexGGWindow(SAV7b sav) : base("SAV_PokedexGG", "Pokédex Editor")
    {
        SAV = (SAV7b)(Origin = sav).Clone();
        Dex = SAV.Blocks.Zukan;

        CP = [
            UiFactory.Check("CHK_P1", "Owned"),
            UiFactory.Check("CHK_P2", "Male"), UiFactory.Check("CHK_P3", "Female"),
            UiFactory.Check("CHK_P4", "Shiny Male"), UiFactory.Check("CHK_P5", "Shiny Female"),
            UiFactory.Check("CHK_P6", "Male"), UiFactory.Check("CHK_P7", "Female"),
            UiFactory.Check("CHK_P8", "Shiny Male"), UiFactory.Check("CHK_P9", "Shiny Female"),
        ];
        CL = [
            UiFactory.Check("CHK_L1", "Japanese"), UiFactory.Check("CHK_L2", "English"),
            UiFactory.Check("CHK_L3", "French"), UiFactory.Check("CHK_L4", "Italian"),
            UiFactory.Check("CHK_L5", "German"), UiFactory.Check("CHK_L6", "Spanish"),
            UiFactory.Check("CHK_L7", "Korean"), UiFactory.Check("CHK_L8", "Chinese"),
            UiFactory.Check("CHK_L9", "Chinese2"),
        ];
        RecordUsed = [
            UiFactory.Check("CHK_RMinHeight", "Recorded"), UiFactory.Check("CHK_RMaxHeight", "Recorded"),
            UiFactory.Check("CHK_RMinWeight", "Recorded"), UiFactory.Check("CHK_RMaxWeight", "Recorded"),
        ];
        RecordFlag = [
            UiFactory.Check("CHK_MinH", "Flag"), UiFactory.Check("CHK_MaxH", "Flag"),
            UiFactory.Check("CHK_MinW", "Flag"), UiFactory.Check("CHK_MaxW", "Flag"),
        ];
        RecordHeight = [
            UiFactory.NumericUpDown("NUD_RHeightMin", 0, byte.MaxValue, 90),
            UiFactory.NumericUpDown("NUD_RHeightMax", 0, byte.MaxValue, 90),
            UiFactory.NumericUpDown("NUD_RWeightMinHeight", 0, byte.MaxValue, 90),
            UiFactory.NumericUpDown("NUD_RWeightMaxHeight", 0, byte.MaxValue, 90),
        ];
        RecordWeight = [
            UiFactory.NumericUpDown("NUD_RHeightMinWeight", 0, byte.MaxValue, 90),
            UiFactory.NumericUpDown("NUD_RHeightMaxWeight", 0, byte.MaxValue, 90),
            UiFactory.NumericUpDown("NUD_RWeightMin", 0, byte.MaxValue, 90),
            UiFactory.NumericUpDown("NUD_RWeightMax", 0, byte.MaxValue, 90),
        ];

        editing = true;
        BuildLayout();

        CB_Species.SetItems(GameInfo.FilteredSources.Species.Skip(1).ToList());
        foreach (var n in Dex.GetEntryNames(GameInfo.Strings.Species))
            SpeciesItems.Add(n);

        editing = false;
        LB_Species.SelectedIndex = 0;
    }

    private void BuildLayout()
    {
        LB_Species.ItemsSource = SpeciesItems;
        LB_Forms.ItemsSource = FormItems;

        var owned = new GroupBoxView("GB_Owned", "Owned", UiFactory.Column(CP[0]));
        var seen = new GroupBoxView("GB_Encountered", "Seen", UiFactory.Column(CP[1], CP[2], CP[3], CP[4]));
        var displayed = new GroupBoxView("GB_Displayed", "Displayed", UiFactory.Column(CP[5], CP[6], CP[7], CP[8]));
        var languages = new GroupBoxView("GB_Language", "Languages", UiFactory.Column(CL));

        var flags = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        flags.Children.Add(UiFactory.Column(owned, seen, displayed));
        flags.Children.Add(languages);

        var records = UiFactory.FormGrid(4);
        string[] labels = ["Min Height:", "Max Height:", "Min Weight:", "Max Weight:"];
        for (int i = 0; i < RecordUsed.Length; i++)
            UiFactory.AddFormRow(records, i, UiFactory.Label($"L_Record{i}", labels[i]), UiFactory.Row(RecordUsed[i], RecordHeight[i], RecordWeight[i], RecordFlag[i]));
        GB_SizeRecords = new GroupBoxView("GB_SizeRecords", "Size Records", records);

        var right = UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_goto", "goto:"), CB_Species),
            flags,
            GB_SizeRecords,
            UiFactory.Row(B_GiveAll, B_Modify, B_Counts));

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        body.Children.Add(LB_Species);
        body.Children.Add(UiFactory.Column(UiFactory.Label("L_Forms", "Forms"), LB_Forms));
        body.Children.Add(new ScrollViewer { Content = right, MaxHeight = 620 });
        SetBody(body);

        LB_Species.SelectionChanged += (_, _) => ChangeLBSpecies();
        LB_Forms.SelectionChanged += (_, _) => ChangeLBForms();
        CB_Species.SelectionChanged += (_, _) => ChangeCBSpecies();
        foreach (var c in CP.Skip(5))
            c.IsCheckedChanged += (s, _) => ChangeDisplayed((CheckBox)s!);
        for (int i = 1; i <= 4; i++)
        {
            var c = CP[i];
            c.IsCheckedChanged += (_, _) => ChangeEncountered(c);
        }
        for (int i = 0; i < RecordUsed.Length; i++)
        {
            var index = i;
            RecordUsed[i].IsCheckedChanged += (_, _) => RecordUsedChanged(index);
        }
        B_GiveAll.AttachClickHandled(ClickGiveAll);
        B_Counts.Click += async (_, _) => await ClickCounts();
        B_Modify.Flyout = BuildModifyMenu();
    }

    private async System.Threading.Tasks.Task ClickCounts()
    {
        SetEntry();
        await new Capture7GGWindow(SAV).ShowDialog(this);
    }

    private MenuFlyout BuildModifyMenu()
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.Bottom };
        AddItem(flyout, "mnuSeenNone", "Seen none", ModifyMode.SeenNone);
        AddItem(flyout, "mnuSeenAll", "Seen all", ModifyMode.SeenAll);
        AddItem(flyout, "mnuCaughtNone", "Caught none", ModifyMode.CaughtNone);
        AddItem(flyout, "mnuCaughtAll", "Caught all", ModifyMode.CaughtAll);
        AddItem(flyout, "mnuComplete", "Complete Dex", ModifyMode.Complete);
        return flyout;
    }

    private void AddItem(MenuFlyout flyout, string name, string header, ModifyMode mode)
    {
        var item = new MenuItem { Name = name, Header = header };
        item.Click += (_, _) => ModifyAll(mode);
        flyout.Items.Add(item);
    }

    private enum ModifyMode { SeenNone, SeenAll, CaughtNone, CaughtAll, Complete }

    #region Selection

    private void SetCurrentIndex(int index)
    {
        currentIndex = index;
        LB_Species.SelectedIndex = index;
    }

    private void ChangeCBSpecies()
    {
        if (editing || CB_Species.GetSelectedItem() is not { } item)
            return;
        SetEntry();

        editing = true;
        SetCurrentIndex(item.Value - 1);
        LB_Species.ScrollIntoView(LB_Species.SelectedIndex);
        if (!allModifying)
            FillFormList();
        GetEntry();
        editing = false;
    }

    private void ChangeLBSpecies()
    {
        if (editing || LB_Species.SelectedIndex < 0)
            return;
        SetEntry();

        editing = true;
        SetCurrentIndex(LB_Species.SelectedIndex);
        var species = Dex.GetBaseSpecies(currentIndex);
        CB_Species.SetValue(species);
        if (!allModifying)
            FillFormList();
        GetEntry();
        editing = false;
    }

    private void ChangeLBForms()
    {
        if (allModifying || editing || LB_Forms.SelectedIndex < 0)
            return;
        SetEntry();

        editing = true;
        var species = Dex.GetBaseSpecies(currentIndex);
        var form = (byte)LB_Forms.SelectedIndex;
        var index = Dex.GetEntryIndex(species, form);
        SetCurrentIndex(index);

        CB_Species.SetValue(species);
        LB_Species.ScrollIntoView(index);
        GetEntry();
        editing = false;
    }

    private void FillFormList()
    {
        if (allModifying)
            return;
        FormItems.Clear();

        var index = currentIndex;
        var species = Dex.GetBaseSpecies(index);
        bool hasForms = FormInfo.HasFormSelection(SAV.Personal[species], species, 7);
        LB_Forms.IsEnabled = hasForms;
        if (!hasForms)
            return;

        var ds = FormConverter.GetFormList(species, GameInfo.Strings.types, GameInfo.Strings.forms, GameInfo.GenderSymbolUnicode, SAV.Context).ToList();
        if (ds.Count == 1 && string.IsNullOrEmpty(ds[0]))
        {
            LB_Forms.IsEnabled = false;
            return;
        }

        // Let's Go has no dex bits for totem forms; drop the extra entry.
        int count = SAV.Personal[species].FormCount;
        if (count < ds.Count)
            ds.RemoveAt(count);

        foreach (var s in ds)
            FormItems.Add(s);

        if (index < SAV.MaxSpeciesID)
        {
            LB_Forms.SelectedIndex = 0;
            return;
        }

        var fc = SAV.Personal[species].FormCount;
        if (fc <= 1)
            return;
        int f = Dex.GetCountFormsPriorTo(species, fc);
        if (f < 0)
            return;
        var form = index - f - (SAV.MaxSpeciesID - 1);
        LB_Forms.SelectedIndex = form < FormItems.Count ? form : -1;
    }

    /// <summary>Only one gender/shiny variant can be the displayed one.</summary>
    private void ChangeDisplayed(CheckBox sender)
    {
        if (sender.IsChecked != true)
            return;
        for (int i = 5; i < CP.Length; i++)
            CP[i].IsChecked = ReferenceEquals(CP[i], sender);
        for (int i = 0; i < 4; i++)
        {
            if (CP[i + 5].IsChecked == true)
                CP[i + 1].IsChecked = true;
        }
    }

    /// <summary>Keeps the displayed flags consistent with the seen flags.</summary>
    private void ChangeEncountered(CheckBox sender)
    {
        if (!AnySeen())
        {
            for (int i = 5; i < CP.Length; i++)
                CP[i].IsChecked = false;
            return;
        }
        if (AnyDisplayed())
            return;
        for (int i = 1; i <= 4; i++)
        {
            if (ReferenceEquals(CP[i], sender) && sender.IsChecked == true)
            {
                CP[i + 4].IsChecked = true;
                return;
            }
        }
    }

    private void RecordUsedChanged(int index)
    {
        var ck = RecordUsed[index];
        var h = RecordHeight[index];
        var w = RecordWeight[index];
        h.IsEnabled = w.IsEnabled = ck.IsChecked == true;
        if (editing || ck.IsChecked == true)
            return;

        RecordFlag[index].IsChecked = false;
        h.Value = Zukan7b.DefaultEntryValueH;
        w.Value = Zukan7b.DefaultEntryValueW;
    }

    #endregion

    #region Entry read / write

    private void GetEntry()
    {
        var index = currentIndex;
        if (index < 0)
            return;
        var species = (ushort)(index + 1);
        bool isSpeciesEntry = species <= SAV.MaxSpeciesID;

        editing = true;
        CP[0].IsEnabled = isSpeciesEntry;
        CP[0].IsChecked = isSpeciesEntry && Dex.GetCaught(species);

        var gt = Dex.GetBaseSpeciesGenderValue(index);
        bool canBeMale = gt != PersonalInfo.RatioMagicFemale;
        bool canBeFemale = gt is not (PersonalInfo.RatioMagicMale or PersonalInfo.RatioMagicGenderless);
        CP[1].IsEnabled = CP[3].IsEnabled = CP[5].IsEnabled = CP[7].IsEnabled = canBeMale;
        CP[2].IsEnabled = CP[4].IsEnabled = CP[6].IsEnabled = CP[8].IsEnabled = canBeFemale;

        for (int i = 0; i < 4; i++)
            CP[i + 1].IsChecked = Dex.GetSeen(species, i);
        for (int i = 0; i < 4; i++)
            CP[i + 5].IsChecked = Dex.GetDisplayed(index, i);

        for (int i = 0; i < CL.Length; i++)
        {
            CL[i].IsEnabled = isSpeciesEntry;
            CL[i].IsChecked = isSpeciesEntry && Dex.GetLanguageFlag(index, i);
        }

        if (!isSpeciesEntry)
            species = Dex.GetBaseSpecies(index);
        LoadRecord(species, (byte)Math.Max(0, LB_Forms.SelectedIndex));
        editing = false;
    }

    private void SetEntry()
    {
        if (currentIndex < 0)
            return;
        var index = currentIndex;
        var species = (ushort)(index + 1);
        var isSpeciesEntry = species <= SAV.MaxSpeciesID;

        for (int i = 0; i < 4; i++)
            Dex.SetSeen(species, i, CP[i + 1].IsChecked == true);
        for (int i = 0; i < 4; i++)
            Dex.SetDisplayed(index, i, CP[i + 5].IsChecked == true);

        if (!isSpeciesEntry)
            return;

        Dex.SetCaught(species, CP[0].IsChecked == true);
        for (int i = 0; i < CL.Length; i++)
            Dex.SetLanguageFlag(index, i, CL[i].IsChecked == true);

        SetRecord(species, (byte)Math.Max(0, LB_Forms.SelectedIndex));
    }

    private void LoadRecord(ushort species, byte form)
    {
        bool hasRecord = Zukan7b.TryGetSizeEntryIndex(species, form, out var index);
        GB_SizeRecords.IsVisible = hasRecord;
        if (!hasRecord)
            return;

        Set(DexSizeType.MinHeight, 0);
        Set(DexSizeType.MaxHeight, 1);
        Set(DexSizeType.MinWeight, 2);
        Set(DexSizeType.MaxWeight, 3);

        void Set(DexSizeType type, int slot)
        {
            var used = Dex.GetSizeData(type, index, out byte h, out byte w, out bool isFlagged);
            RecordUsed[slot].IsChecked = used;
            RecordHeight[slot].IsEnabled = RecordWeight[slot].IsEnabled = used;
            RecordFlag[slot].IsChecked = isFlagged;
            RecordHeight[slot].Value = h;
            RecordWeight[slot].Value = w;
        }
    }

    private void SetRecord(ushort species, byte form)
    {
        if (!Zukan7b.TryGetSizeEntryIndex(species, form, out var index))
            return;

        Dex.SetSizeData(DexSizeType.MinHeight, index, H(0), W(0), RecordFlag[0].IsChecked == true);
        Dex.SetSizeData(DexSizeType.MaxHeight, index, H(1), W(1), RecordFlag[1].IsChecked == true);
        Dex.SetSizeData(DexSizeType.MinWeight, index, H(2), W(2), RecordFlag[2].IsChecked == true);
        Dex.SetSizeData(DexSizeType.MaxWeight, index, H(3), W(3), RecordFlag[3].IsChecked == true);

        byte H(int slot) => RecordUsed[slot].IsChecked != true ? Zukan7b.DefaultEntryValueH : (byte)(RecordHeight[slot].Value ?? 0);
        byte W(int slot) => RecordUsed[slot].IsChecked != true ? Zukan7b.DefaultEntryValueW : (byte)(RecordWeight[slot].Value ?? 0);
    }

    #endregion

    #region Bulk edits

    private void ClickGiveAll(KeyModifiers mods)
    {
        bool clear = mods == KeyModifiers.Control;
        if (CL[0].IsEnabled)
        {
            foreach (var cb in CL)
                cb.IsChecked = !clear;
        }
        if (CP[0].IsEnabled)
            CP[0].IsChecked = !clear;

        var gt = Dex.GetBaseSpeciesGenderValue(currentIndex);
        bool canBeMale = gt != PersonalInfo.RatioMagicFemale;
        bool canBeFemale = gt is not (PersonalInfo.RatioMagicMale or PersonalInfo.RatioMagicGenderless);
        CP[1].IsChecked = CP[3].IsChecked = canBeMale && !clear;
        CP[2].IsChecked = CP[4].IsChecked = canBeFemale && !clear;

        if (clear)
        {
            for (int i = 5; i < CP.Length; i++)
                CP[i].IsChecked = false;
        }
        else if (!AnyDisplayed())
        {
            (canBeMale ? CP[5] : CP[6]).IsChecked = true;
        }
    }

    private void ModifyAll(ModifyMode mode)
    {
        allModifying = true;
        LB_Forms.IsEnabled = LB_Forms.IsVisible = false;

        int lang = SAV.Language;
        if (lang > 5)
            lang--;
        lang--;

        if (mode is ModifyMode.SeenAll or ModifyMode.CaughtAll or ModifyMode.Complete)
            SetAll(mode, lang);
        else
            ClearAll(mode);

        SetEntry();
        allModifying = false;
        LB_Forms.IsEnabled = LB_Forms.IsVisible = true;

        // Move the cursor back to the first entry without letting the list's event write the bulk state into it.
        editing = true;
        SetCurrentIndex(0);
        CB_Species.SetValue(Dex.GetBaseSpecies(0));
        editing = false;
        FillFormList();
        GetEntry();
    }

    /// <summary>Moves the editing cursor without touching the list controls.</summary>
    private void SelectEntry(int index)
    {
        SetEntry();
        currentIndex = index;
        GetEntry();
    }

    private void ClearAll(ModifyMode mode)
    {
        for (int i = 0; i < SpeciesItems.Count; i++)
        {
            SelectEntry(i);
            foreach (var chk in CL)
                chk.IsChecked = false;
            CP[0].IsChecked = false;
            if (mode == ModifyMode.CaughtNone)
                continue;
            for (int j = 1; j < CP.Length; j++)
                CP[j].IsChecked = false;
            foreach (var ck in RecordUsed)
                ck.IsChecked = false;
        }
    }

    private void SetAll(ModifyMode mode, int lang)
    {
        foreach (var species in GetLegalSpecies())
        {
            int index = species - 1;
            var gt = Dex.GetBaseSpeciesGenderValue(index);
            SelectEntry(index);
            SetSeen(mode, gt, false);
            if (mode != ModifyMode.SeenAll)
            {
                SetCaught(mode, gt, lang, false);
                SetRecords();
            }

            // The partner Pikachu/Eevee keeps its own buddy bit, which the bulk edit must not touch.
            if (species is (int)Species.Pikachu or (int)Species.Eevee)
                continue;

            foreach (var f in Dex.GetAllFormEntries(species).Where(z => z >= SAV.MaxSpeciesID).Distinct())
            {
                SelectEntry(f);
                SetSeen(mode, gt, true);
                if (mode != ModifyMode.SeenAll)
                    SetCaught(mode, gt, lang, true);
            }
        }
    }

    private void SetRecords()
    {
        if (!GB_SizeRecords.IsVisible)
            return;
        for (var i = 0; i < RecordUsed.Length; i++)
        {
            if (RecordUsed[i].IsChecked == true)
                continue;
            RecordUsed[i].IsChecked = true;
            RecordHeight[i].Value = i % 2 == 0 ? 0 : 255;
            RecordWeight[i].Value = i % 2 == 0 ? 0 : 255;
        }
    }

    private static IEnumerable<ushort> GetLegalSpecies()
    {
        for (ushort z = 1; z <= 151; z++)
            yield return z;
        yield return 808; // Meltan
        yield return 809; // Melmetal
    }

    private void SetCaught(ModifyMode mode, byte gt, int lang, bool isForm)
    {
        CP[0].IsChecked = mode != ModifyMode.CaughtNone;
        for (int j = 0; j < CL.Length; j++)
            CL[j].IsChecked = CL[j].IsEnabled && (mode == ModifyMode.Complete || (mode != ModifyMode.CaughtNone && j == lang));

        bool canBeMale = gt != PersonalInfo.RatioMagicFemale;
        if (mode == ModifyMode.CaughtNone)
        {
            if (isForm)
                return;
            if (!AnySeen() && !AnyDisplayed())
                (canBeMale ? CP[5] : CP[6]).IsChecked = true;
            return;
        }

        if (mode == ModifyMode.Complete)
        {
            for (int i = 1; i <= 4; i++)
                CP[i].IsChecked = CP[i].IsEnabled;
        }
        else if (!AnySeen())
        {
            (canBeMale ? CP[1] : CP[2]).IsChecked = true;
        }

        if (isForm)
            return;
        if (!AnyDisplayed())
            (canBeMale ? CP[5] : CP[6]).IsChecked = CP[0].IsEnabled;
    }

    private void SetSeen(ModifyMode mode, byte gt, bool isForm)
    {
        bool clear = mode == ModifyMode.SeenNone;
        for (int i = 1; i <= 4; i++)
            CP[i].IsChecked = !clear && CP[i].IsEnabled;

        if (!clear)
        {
            if (isForm)
                return;
            if (!AnyDisplayed())
                (gt != PersonalInfo.RatioMagicFemale ? CP[5] : CP[6]).IsChecked = true;
        }
        else
        {
            foreach (var t in CP)
                t.IsChecked = false;
        }

        if (CP[0].IsChecked != true)
        {
            foreach (var t in CL)
                t.IsChecked = false;
        }
    }

    private bool AnySeen() => CP[1].IsChecked == true || CP[2].IsChecked == true || CP[3].IsChecked == true || CP[4].IsChecked == true;
    private bool AnyDisplayed() => CP[5].IsChecked == true || CP[6].IsChecked == true || CP[7].IsChecked == true || CP[8].IsChecked == true;

    #endregion

    protected override void OnSave()
    {
        SetEntry();
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
