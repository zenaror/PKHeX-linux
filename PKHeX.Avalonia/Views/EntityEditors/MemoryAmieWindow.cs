using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Views.SaveEditors;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.EntityEditors;

/// <summary>
/// Memory / Amie editor (port of the WinForms <c>MemoryAmie</c>).
/// </summary>
public sealed class MemoryAmieWindow : SaveEditorWindow
{
    private readonly TextMarkup TextArgs;
    private readonly MemoryStrings MemStrings;
    private readonly PKM Entity;
    private readonly ComboBox[] PrevCountries;
    private readonly ComboBox[] PrevRegions;
    private bool init;

    private readonly TabControl tabControl1 = new() { Name = "tabControl1" };
    private readonly TabItem Tab_OTMemory = new() { Name = "Tab_OTMemory", Header = "Memories with: OT" };
    private readonly TabItem Tab_CTMemory = new() { Name = "Tab_CTMemory", Header = "Memories with: notOT" };
    private readonly TabItem Tab_Residence = new() { Name = "Tab_Residence", Header = "Residence" };
    private readonly TabItem Tab_Other = new() { Name = "Tab_Other", Header = "Other" };

    // OT memory
    private readonly GroupBoxView GB_M_OT;
    private readonly TextBlock L_OT_TextLine = UiFactory.Label("L_OT_TextLine", "Memory Type:");
    private readonly ComboBox CB_OTMemory = UiFactory.Combo("CB_OTMemory", 260);
    private readonly TextBlock LOTV = UiFactory.Label("LOTV", "VARIABLE:");
    private readonly ComboBox CB_OTVar = UiFactory.Combo("CB_OTVar", 260);
    private readonly TextBlock L_OT_Quality = UiFactory.Label("L_OT_Quality", "Intensity:");
    private readonly ComboBox CB_OTQual = UiFactory.StringCombo("CB_OTQual", 200);
    private readonly TextBlock L_OT_Feeling = UiFactory.Label("L_OT_Feeling", "Feeling:");
    private readonly ComboBox CB_OTFeel = UiFactory.StringCombo("CB_OTFeel", 200);
    private readonly TextBlock L_OT_Friendship = UiFactory.Label("L_OT_Friendship", "Friendship:");
    private readonly NumericTextBox M_OT_Friendship = UiFactory.Numeric("M_OT_Friendship", 3, 56);
    private readonly TextBlock L_OT_Affection = UiFactory.Label("L_OT_Affection", "Affection:");
    private readonly NumericTextBox M_OT_Affection = UiFactory.Numeric("M_OT_Affection", 3, 56);
    private readonly TextBlock RTB_OT = new() { Name = "RTB_OT", TextWrapping = TextWrapping.Wrap, MinHeight = 60, Margin = new Thickness(0, 6, 0, 0) };

    // HT memory
    private readonly GroupBoxView GB_M_CT;
    private readonly TextBlock L_CT_TextLine = UiFactory.Label("L_CT_TextLine", "Memory Type:");
    private readonly ComboBox CB_CTMemory = UiFactory.Combo("CB_CTMemory", 260);
    private readonly TextBlock LCTV = UiFactory.Label("LCTV", "VARIABLE");
    private readonly ComboBox CB_CTVar = UiFactory.Combo("CB_CTVar", 260);
    private readonly TextBlock L_CT_Quality = UiFactory.Label("L_CT_Quality", "Intensity:");
    private readonly ComboBox CB_CTQual = UiFactory.StringCombo("CB_CTQual", 200);
    private readonly TextBlock L_CT_Feeling = UiFactory.Label("L_CT_Feeling", "Feeling:");
    private readonly ComboBox CB_CTFeel = UiFactory.StringCombo("CB_CTFeel", 200);
    private readonly TextBlock L_CT_Friendship = UiFactory.Label("L_CT_Friendship", "Friendship:");
    private readonly NumericTextBox M_CT_Friendship = UiFactory.Numeric("M_CT_Friendship", 3, 56);
    private readonly TextBlock L_CT_Affection = UiFactory.Label("L_CT_Affection", "Affection:");
    private readonly NumericTextBox M_CT_Affection = UiFactory.Numeric("M_CT_Affection", 3, 56);
    private readonly TextBlock RTB_CT = new() { Name = "RTB_CT", TextWrapping = TextWrapping.Wrap, MinHeight = 60, Margin = new Thickness(0, 6, 0, 0) };

    // Residence
    private readonly GroupBoxView GB_Residence;
    private readonly TextBlock L_Country = UiFactory.Label("L_Country", "Country");
    private readonly TextBlock L_Region = UiFactory.Label("L_Region", "Region");
    private readonly Button B_ClearAll = UiFactory.Button("B_ClearAll", "Clear All");
    private readonly TextBlock[] L_Geo;

    // Other
    private readonly TextBlock L_Fullness = UiFactory.Label("L_Fullness", "Fullness:");
    private readonly NumericTextBox M_Fullness = UiFactory.Numeric("M_Fullness", 3, 56);
    private readonly TextBlock L_Enjoyment = UiFactory.Label("L_Enjoyment", "Enjoyment:");
    private readonly NumericTextBox M_Enjoyment = UiFactory.Numeric("M_Enjoyment", 3, 56);
    private readonly TextBlock L_Sociability = UiFactory.Label("L_Sociability", "Sociability:");
    private readonly NumericTextBox MT_Sociability = UiFactory.Numeric("MT_Sociability", 3, 56);
    private readonly TextBlock L_Handler = UiFactory.Label("L_Handler", "Current Handler:");
    private readonly ComboBox CB_Handler = UiFactory.StringCombo("CB_Handler", 220);

    // Translation source for the composite group texts (WinForms hidden label).
    private readonly TextBlock L_Arguments = new() { Name = "L_Arguments", IsVisible = false, Text = "Disabled ; Never left ; OT ; Past Gen ; Memories with ; Pokémon ; Area ; Item(s) ; Move ; Location" };

    public MemoryAmieWindow(PKM pk) : base("MemoryAmie", "Memory / Amie Editor")
    {
        Entity = pk;
        MemStrings = new MemoryStrings(GameInfo.Strings);

        var countries = new ComboBox[5];
        var regions = new ComboBox[5];
        L_Geo = new TextBlock[5];
        var residence = UiFactory.FormGrid(6);
        var headerRow = UiFactory.Row(L_Country, L_Region);
        UiFactory.AddFormRow(residence, 0, null, headerRow);
        for (int i = 0; i < 5; i++)
        {
            countries[i] = UiFactory.Combo($"CB_Country{i}", 180);
            regions[i] = UiFactory.Combo($"CB_Region{i}", 180);
            L_Geo[i] = UiFactory.Label($"L_Geo{i}", i == 0 ? "Latest:" : $"Past {i}:", clickable: true);
            UiFactory.AddFormRow(residence, i + 1, L_Geo[i], UiFactory.Row(countries[i], regions[i]));
        }
        PrevCountries = countries;
        PrevRegions = regions;
        GB_Residence = new GroupBoxView("GB_Residence", "Pokémon has Resided in:", UiFactory.Column(residence, B_ClearAll));
        B_ClearAll.HorizontalAlignment = HorizontalAlignment.Left;

        var ot = UiFactory.FormGrid(6);
        UiFactory.AddFormRow(ot, 0, L_OT_TextLine, CB_OTMemory);
        UiFactory.AddFormRow(ot, 1, LOTV, CB_OTVar);
        UiFactory.AddFormRow(ot, 2, L_OT_Quality, CB_OTQual);
        UiFactory.AddFormRow(ot, 3, L_OT_Feeling, CB_OTFeel);
        UiFactory.AddFormRow(ot, 4, L_OT_Friendship, M_OT_Friendship);
        UiFactory.AddFormRow(ot, 5, L_OT_Affection, M_OT_Affection);
        GB_M_OT = new GroupBoxView("GB_M_OT", "Memories with Original Trainer", UiFactory.Column(ot, RTB_OT));

        var ct = UiFactory.FormGrid(6);
        UiFactory.AddFormRow(ct, 0, L_CT_TextLine, CB_CTMemory);
        UiFactory.AddFormRow(ct, 1, LCTV, CB_CTVar);
        UiFactory.AddFormRow(ct, 2, L_CT_Quality, CB_CTQual);
        UiFactory.AddFormRow(ct, 3, L_CT_Feeling, CB_CTFeel);
        UiFactory.AddFormRow(ct, 4, L_CT_Friendship, M_CT_Friendship);
        UiFactory.AddFormRow(ct, 5, L_CT_Affection, M_CT_Affection);
        GB_M_CT = new GroupBoxView("GB_M_CT", "Memories with Current Trainer", UiFactory.Column(ct, RTB_CT));

        var other = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(other, 0, L_Fullness, M_Fullness);
        UiFactory.AddFormRow(other, 1, L_Enjoyment, M_Enjoyment);
        UiFactory.AddFormRow(other, 2, L_Sociability, MT_Sociability);
        UiFactory.AddFormRow(other, 3, L_Handler, CB_Handler);

        Tab_OTMemory.Content = GB_M_OT;
        Tab_CTMemory.Content = GB_M_CT;
        Tab_Residence.Content = GB_Residence;
        Tab_Other.Content = other;
        tabControl1.Items.Add(Tab_OTMemory);
        tabControl1.Items.Add(Tab_CTMemory);
        tabControl1.Items.Add(Tab_Residence);
        tabControl1.Items.Add(Tab_Other);

        var body = UiFactory.Column(tabControl1, L_Arguments);
        SetBody(body); // translate before reading the composite argument strings

        string[] arguments = (L_Arguments.Text ?? string.Empty).Split(" ; ");
        TextArgs = new TextMarkup(arguments);

        if (Entity is IGeoTrack)
        {
            foreach (var cb in PrevCountries)
                cb.SetCountrySubRegion("countries");
        }
        else
        {
            tabControl1.Items.Remove(Tab_Residence);
        }

        foreach (var l in L_Geo)
            l.AttachClick(_ => ClickResetLocation(l));
        B_ClearAll.Click += (_, _) => B_ClearAll_Click();
        for (int i = 0; i < PrevCountries.Length; i++)
            PrevCountries[i].SelectionChanged += (s, _) => ChangeCountryIndex(s as ComboBox);
        foreach (var cb in new[] { CB_OTVar, CB_OTMemory, CB_OTQual, CB_OTFeel, CB_CTVar, CB_CTMemory, CB_CTQual, CB_CTFeel })
            cb.SelectionChanged += (s, _) => ChangeMemory(s as ComboBox);
        foreach (var tb in new[] { M_OT_Friendship, M_OT_Affection, M_CT_Friendship, M_CT_Affection, M_Fullness, M_Enjoyment, MT_Sociability })
            tb.OnTextChanged(Update255);

        GetLangStrings();
        LoadFields();
    }

    private void LoadFields()
    {
        // Load the region/country values.
        if (Entity is IGeoTrack g)
        {
            PrevCountries[0].SetValue(g.Geo1_Country);
            PrevCountries[1].SetValue(g.Geo2_Country);
            PrevCountries[2].SetValue(g.Geo3_Country);
            PrevCountries[3].SetValue(g.Geo4_Country);
            PrevCountries[4].SetValue(g.Geo5_Country);
            PrevRegions[0].SetValue(g.Geo1_Region);
            PrevRegions[1].SetValue(g.Geo2_Region);
            PrevRegions[2].SetValue(g.Geo3_Region);
            PrevRegions[3].SetValue(g.Geo4_Region);
            PrevRegions[4].SetValue(g.Geo5_Region);
        }

        // Load the Fullness, and Enjoyment
        if (Entity is IFullnessEnjoyment f)
        {
            M_Fullness.Text = f.Fullness.ToString();
            M_Enjoyment.Text = f.Enjoyment.ToString();
        }
        else
        {
            L_Fullness.IsVisible = M_Fullness.IsVisible = L_Enjoyment.IsVisible = M_Enjoyment.IsVisible = false;
        }

        M_OT_Friendship.Text = Entity.OriginalTrainerFriendship.ToString();
        M_CT_Friendship.Text = Entity.HandlingTrainerFriendship.ToString();

        if (Entity is IAffection a)
        {
            M_OT_Affection.Text = a.OriginalTrainerAffection.ToString();
            M_CT_Affection.Text = a.HandlingTrainerAffection.ToString();
        }

        if (Entity is ISociability s)
            MT_Sociability.Text = Math.Min(byte.MaxValue, s.Sociability).ToString();

        if (Entity is ITrainerMemories m)
        {
            // Load the OT Memories
            CB_OTQual.SelectedIndex = m.OriginalTrainerMemoryIntensity;
            CB_OTMemory.SetValue(m.OriginalTrainerMemory);
            CB_OTVar.SetValue(m.OriginalTrainerMemoryVariable);
            CB_OTFeel.SelectedIndex = m.OriginalTrainerMemoryFeeling;

            // Load the HT Memories
            CB_CTQual.SelectedIndex = m.HandlingTrainerMemoryIntensity;
            CB_CTMemory.SetValue(m.HandlingTrainerMemory);
            CB_CTVar.SetValue(m.HandlingTrainerMemoryVariable);
            CB_CTFeel.SelectedIndex = m.HandlingTrainerMemoryFeeling;
        }

        var translation = GeneralLocalization.Get(MainWindow.CurrentLanguage);
        var lOT = translation.OriginalTrainer;
        var lHT = translation.HandlingTrainer;
        CB_Handler.Items.Clear();
        CB_Handler.Items.Add($"{Entity.OriginalTrainerName} ({lOT})"); // OTNAME : OT

        var ht = Entity.HandlingTrainerName;
        if (string.IsNullOrWhiteSpace(ht))
            ht = "----EMPTY----";
        CB_Handler.Items.Add($"{ht} ({lHT})");

        CB_Handler.SelectedIndex = Entity.CurrentHandler;
        tabControl1.SelectedIndex = Entity.CurrentHandler;

        if (!Entity.IsEgg)
        {
            bool enable;
            if (Entity.Generation < 6)
            {
                // Previous Generation Mon
                GB_M_OT.Header = $"{TextArgs.PastGen} {Entity.OriginalTrainerName}: {lOT}"; // Past Gen OT : OTNAME
                GB_M_CT.Header = $"{TextArgs.MemoriesWith} {Entity.HandlingTrainerName} ({lHT})"; // Memories with : HTNAME
                enable = false;
                // Reset to no memory -- don't reset affection as OR/AS can raise it (+20 * n) via Contests
                CB_OTQual.SelectedIndex = CB_OTFeel.SelectedIndex = 0;
                CB_OTVar.SetValue(0);
                CB_OTMemory.SetValue(0);
            }
            else
            {
                enable = true;
                GB_M_OT.Header = $"{TextArgs.MemoriesWith} {Entity.OriginalTrainerName} ({lOT})"; // Memories with : OTNAME
                if (Entity.HandlingTrainerName.Length == 0)
                {
                    for (int i = 1; i < PrevCountries.Length; i++)
                        PrevCountries[i].IsEnabled = PrevRegions[i].IsEnabled = false;
                    GB_M_CT.IsEnabled = false;
                    GB_M_CT.Header = $"{TextArgs.NeverLeft} {TextArgs.OT} - {TextArgs.Disabled}"; // Never Left : OT : Disabled
                }
                else
                {
                    GB_M_CT.Header = $"{TextArgs.MemoriesWith} {Entity.HandlingTrainerName} ({lHT})";
                }
            }
            RTB_OT.IsVisible = CB_OTQual.IsEnabled = CB_OTMemory.IsEnabled = CB_OTFeel.IsEnabled = CB_OTVar.IsEnabled = enable;
            M_OT_Affection.IsEnabled = true;
        }
        else
        {
            GB_M_OT.Header = GB_M_CT.Header = $"N/A: {GameInfo.Strings.EggName}";
        }

        init = true;

        // Manually load the Memory Parse
        RTB_CT.Text = GetMemoryString(CB_CTMemory, CB_CTVar, CB_CTQual, CB_CTFeel, Entity.HandlingTrainerName);
        RTB_OT.Text = GetMemoryString(CB_OTMemory, CB_OTVar, CB_OTQual, CB_OTFeel, Entity.OriginalTrainerName);

        // Affection no longer stored in Gen8+, so only show in Gen6/7.
        L_OT_Affection.IsVisible = L_CT_Affection.IsVisible = M_OT_Affection.IsVisible = M_CT_Affection.IsVisible = Entity.Format <= 7;
        L_Sociability.IsVisible = MT_Sociability.IsVisible = Entity is ISociability;
    }

    protected override void OnSave()
    {
        // Save Region & Country Data
        if (Entity is IGeoTrack g)
        {
            g.Geo1_Region = (byte)PrevRegions[0].GetValue();
            g.Geo2_Region = (byte)PrevRegions[1].GetValue();
            g.Geo3_Region = (byte)PrevRegions[2].GetValue();
            g.Geo4_Region = (byte)PrevRegions[3].GetValue();
            g.Geo5_Region = (byte)PrevRegions[4].GetValue();
            g.Geo1_Country = (byte)PrevCountries[0].GetValue();
            g.Geo2_Country = (byte)PrevCountries[1].GetValue();
            g.Geo3_Country = (byte)PrevCountries[2].GetValue();
            g.Geo4_Country = (byte)PrevCountries[3].GetValue();
            g.Geo5_Country = (byte)PrevCountries[4].GetValue();
        }

        // Save 0-255 stats
        Entity.HandlingTrainerFriendship = (byte)M_CT_Friendship.IntValue;
        Entity.OriginalTrainerFriendship = (byte)M_OT_Friendship.IntValue;

        if (Entity is IAffection a)
        {
            a.OriginalTrainerAffection = (byte)M_OT_Affection.IntValue;
            a.HandlingTrainerAffection = (byte)M_CT_Affection.IntValue;
        }
        if (Entity is IFullnessEnjoyment f)
        {
            f.Fullness = (byte)M_Fullness.IntValue;
            f.Enjoyment = (byte)M_Enjoyment.IntValue;
        }

        // Save Memories
        if (Entity is ITrainerMemories m)
        {
            m.OriginalTrainerMemory = (byte)CB_OTMemory.GetValue();
            m.OriginalTrainerMemoryVariable = CB_OTVar.IsEnabled ? (ushort)CB_OTVar.GetValue() : (ushort)0;
            m.OriginalTrainerMemoryIntensity = CB_OTFeel.IsEnabled ? (byte)Math.Max(0, CB_OTQual.SelectedIndex) : (byte)0;
            m.OriginalTrainerMemoryFeeling = CB_OTFeel.IsEnabled ? (byte)Math.Max(0, CB_OTFeel.SelectedIndex) : (byte)0;

            m.HandlingTrainerMemory = (byte)CB_CTMemory.GetValue();
            m.HandlingTrainerMemoryVariable = CB_CTVar.IsEnabled ? (ushort)CB_CTVar.GetValue() : (ushort)0;
            m.HandlingTrainerMemoryIntensity = CB_CTFeel.IsEnabled ? (byte)Math.Max(0, CB_CTQual.SelectedIndex) : (byte)0;
            m.HandlingTrainerMemoryFeeling = CB_CTFeel.IsEnabled ? (byte)Math.Max(0, CB_CTFeel.SelectedIndex) : (byte)0;
        }

        if (Entity is G8PKM pk8)
            pk8.Sociability = (byte)MT_Sociability.IntValue;
        Close();
    }

    private void GetLangStrings()
    {
        var strings = MemStrings;
        CB_OTMemory.SetItems(strings.Memory);
        CB_CTMemory.SetItems(strings.Memory);

        // Quality Chooser
        AddIntensity(strings.Species[0].Text); // None
        foreach (var q in strings.GetMemoryQualities()[1..])
            AddIntensity(q);

        // Feeling Chooser
        foreach (var q in strings.GetMemoryFeelings(Entity.Generation))
            CB_OTFeel.Items.Add(q);
        foreach (var q in strings.GetMemoryFeelings(Entity.Format))
            CB_CTFeel.Items.Add(q);

        // Same for each game.
        void AddIntensity(string line)
        {
            CB_CTQual.Items.Add(line);
            CB_OTQual.Items.Add(line);
        }
    }

    private void UpdateMemoryDisplay(ComboBox sender)
    {
        if (sender == CB_OTMemory)
        {
            int memoryGen = Entity.Generation;
            if (memoryGen == 0)
                memoryGen = Entity.Format;

            var memory = (byte)sender.GetValue();
            var memIndex = Memories.GetMemoryArgType(memory, memoryGen);
            var args = MemStrings.GetArgumentStrings(memIndex, memoryGen);
            CB_OTVar.SetItems(args);
            if (CB_OTVar.SelectedIndex < 0 && args.Count != 0)
                CB_OTVar.SelectedIndex = 0; // WinForms DataSource assignment selects the first entry
            LOTV.Text = TextArgs.GetMemoryCategory(memIndex, memoryGen);
            LOTV.IsVisible = CB_OTVar.IsVisible = CB_OTVar.IsEnabled = args.Count > 1;
        }
        else
        {
            int memoryGen = Entity.Format;
            var memory = (byte)sender.GetValue();
            var memIndex = Memories.GetMemoryArgType(memory, memoryGen);
            var argvals = MemStrings.GetArgumentStrings(memIndex, memoryGen);
            CB_CTVar.SetItems(argvals);
            if (CB_CTVar.SelectedIndex < 0 && argvals.Count != 0)
                CB_CTVar.SelectedIndex = 0; // WinForms DataSource assignment selects the first entry
            LCTV.Text = TextArgs.GetMemoryCategory(memIndex, memoryGen);
            LCTV.IsVisible = CB_CTVar.IsVisible = CB_CTVar.IsEnabled = argvals.Count > 1;
        }
    }

    private string GetMemoryString(ComboBox m, ComboBox arg, ComboBox q, ComboBox f, string tr)
    {
        var messages = GameInfo.Strings.memories;
        string result;
        bool enabled;
        int mem = m.GetValue();
        if (mem == 0)
        {
            string nn = Entity.Nickname;
            result = string.Format(messages[0], nn);
            enabled = false;
        }
        else
        {
            var msg = (uint)mem < messages.Length ? messages[mem] : $"{mem}";
            string nn = Entity.Nickname;
            string a = arg.GetText();
            result = string.Format(msg, nn, tr, a, f.GetText(), q.GetText());
            enabled = true;
        }

        // Show labels if the memory allows for them.
        if (q == CB_CTQual)
            L_CT_Quality.IsVisible = L_CT_Feeling.IsVisible = enabled;
        else
            L_OT_Quality.IsVisible = L_OT_Feeling.IsVisible = enabled;

        // Show Quality and Feeling.
        q.IsVisible = q.IsEnabled = f.IsVisible = f.IsEnabled = enabled;

        return result;
    }

    private void ChangeMemory(ComboBox? m)
    {
        if (m is null)
            return;
        if (m == CB_CTMemory || m == CB_OTMemory)
            UpdateMemoryDisplay(m);

        if (!init)
            return;
        RTB_OT.Text = GetMemoryString(CB_OTMemory, CB_OTVar, CB_OTQual, CB_OTFeel, Entity.OriginalTrainerName);
        RTB_CT.Text = GetMemoryString(CB_CTMemory, CB_CTVar, CB_CTQual, CB_CTFeel, Entity.HandlingTrainerName);
    }

    private void ChangeCountryIndex(ComboBox? cb)
    {
        if (cb is null)
            return;
        int index = Array.IndexOf(PrevCountries, cb);
        if (index < 0)
            return;
        int val = cb.GetValue();
        if (val > 0)
        {
            PrevRegions[index].SetCountrySubRegion($"sr_{val:000}");
            PrevRegions[index].IsEnabled = true;
        }
        else
        {
            PrevRegions[index].SetItems([new ComboItem("", 0)]);
            PrevRegions[index].IsEnabled = false;
            PrevRegions[index].SetValue(0);
        }
    }

    private static void Update255(TextBox tb)
    {
        if (Util.ToInt32(tb.Text ?? string.Empty) > byte.MaxValue)
            tb.Text = "255";
    }

    private void ClickResetLocation(TextBlock l)
    {
        int index = Array.IndexOf(L_Geo, l);
        if (index < 0)
            return;
        PrevCountries[index].SetValue(0);
        PrevRegions[index].SetItems([new ComboItem("", 0)]);
        PrevRegions[index].SetValue(0);
    }

    private void B_ClearAll_Click()
    {
        for (int i = 0; i < 5; i++)
            PrevCountries[i].SetValue(0);
    }

    private sealed class TextMarkup
    {
        public string Disabled { get; } = nameof(Disabled);
        public string NeverLeft { get; } = "Never left";
        public string OT { get; } = "OT";
        public string PastGen { get; } = "Past Gen";
        public string MemoriesWith { get; } = "Memories with";

        private string Species { get; } = "Species:";
        private string Area { get; } = "Area:";
        private string Item { get; } = "Item:";
        private string Move { get; } = "Move:";
        private string Location { get; } = "Location:";

        public TextMarkup(string[] args)
        {
            Array.Resize(ref args, 10);
            if (!string.IsNullOrWhiteSpace(args[0])) Disabled = args[0];
            if (!string.IsNullOrWhiteSpace(args[1])) NeverLeft = args[1];
            if (!string.IsNullOrWhiteSpace(args[2])) OT = args[2];
            if (!string.IsNullOrWhiteSpace(args[3])) PastGen = args[3];
            if (!string.IsNullOrWhiteSpace(args[4])) MemoriesWith = args[4];

            // Pokémon ; Area ; Item(s) ; Move ; Location
            if (!string.IsNullOrWhiteSpace(args[5])) Species = args[5] + ":";
            if (!string.IsNullOrWhiteSpace(args[6])) Area = args[6] + ":";
            if (!string.IsNullOrWhiteSpace(args[7])) Item = args[7] + ":";
            if (!string.IsNullOrWhiteSpace(args[8])) Move = args[8] + ":";
            if (!string.IsNullOrWhiteSpace(args[9])) Location = args[9] + ":";
        }

        public string GetMemoryCategory(MemoryArgType type, int memoryGen) => type switch
        {
            MemoryArgType.GeneralLocation => Area,
            MemoryArgType.SpecificLocation when memoryGen <= 7 => Location,
            MemoryArgType.Species => Species,
            MemoryArgType.Move => Move,
            MemoryArgType.Item => Item,
            _ => string.Empty,
        };
    }
}
