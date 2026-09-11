using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views.SaveEditors;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.EntityEditors;

/// <summary>
/// String / trash byte editor (port of the WinForms <c>TrashEditor</c>).
/// </summary>
public sealed class TrashEditorWindow : SaveEditorWindow
{
    private readonly IStringConverter Converter;
    private readonly List<NumericTextBox> Bytes = [];
    private readonly byte[] Raw;
    private bool editing;

    /// <summary>Text as edited by the user (WinForms <c>FinalString</c>).</summary>
    public string FinalString { get; private set; }

    /// <summary>Raw bytes as edited by the user (WinForms <c>FinalBytes</c>).</summary>
    public byte[] FinalBytes { get; private set; }

    private readonly TextBlock L_String = UiFactory.Label("L_String", "String");
    private readonly TextBox TB_Text = UiFactory.Text("TB_Text", 100, 260);
    private readonly WrapPanel FLP_Characters = new() { Name = "FLP_Characters", Orientation = Orientation.Horizontal, MaxWidth = 320, IsVisible = false };
    private readonly WrapPanel FLP_Hex = new() { Name = "FLP_Hex", Orientation = Orientation.Horizontal, MaxWidth = 320, IsVisible = false };
    private readonly GroupBoxView GB_Trash;
    private readonly TextBlock L_Species = UiFactory.Label("L_Species", "Species");
    private readonly ComboBox CB_Species = UiFactory.Combo("CB_Species", 180);
    private readonly TextBlock L_Language = UiFactory.Label("L_Language", "Language");
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 180);
    private readonly TextBlock L_Generation = UiFactory.Label("L_Generation", "Generation");
    private readonly NumericUpDown NUD_Generation = UiFactory.NumericUpDown("NUD_Generation", 1, 9, 90);
    private readonly Button B_ApplyTrash = UiFactory.Button("B_ApplyTrash", "Apply Trash");
    private readonly Button B_ClearTrash = UiFactory.Button("B_ClearTrash", "Clear Trash");

    /// <summary>
    /// Opens the editor for the given text box, applying the result back to it (port of <c>TrashEditor.Show</c>).
    /// </summary>
    /// <returns>Edited trash bytes, or null if unchanged/cancelled.</returns>
    public static async Task<byte[]?> ShowAsync<T>(Window owner, TextBox tb, T provider, byte[] trash, bool readOnly = false)
        where T : IStringConverter, IGeneration, IContext
    {
        var form = new TrashEditorWindow(tb, provider, provider.Generation, provider.Context, trash);
        if (readOnly)
            form.B_Save.IsEnabled = false;
        await form.ShowDialog(owner);
        tb.Text = form.FinalString;
        return form.FinalBytes.Length == 0 ? null : form.FinalBytes;
    }

    private TrashEditorWindow(TextBox tb, IStringConverter converter, byte generation, EntityContext context, byte[] raw) : base("TrashEditor", "Special Characters")
    {
        Converter = converter;
        FinalString = tb.Text ?? string.Empty;

        editing = true;
        if (raw.Length != 0)
        {
            Raw = FinalBytes = [.. raw];
        }
        else
        {
            Raw = FinalBytes = [];
        }

        var trashGrid = UiFactory.FormGrid(3);
        UiFactory.AddFormRow(trashGrid, 0, L_Species, CB_Species);
        UiFactory.AddFormRow(trashGrid, 1, L_Language, CB_Language);
        UiFactory.AddFormRow(trashGrid, 2, L_Generation, NUD_Generation);
        var trashButtons = UiFactory.Row(B_ApplyTrash, B_ClearTrash);
        GB_Trash = new GroupBoxView("GB_Trash", "Trash Byte Layers", UiFactory.Column(trashGrid, trashButtons)) { IsVisible = false };

        var body = UiFactory.Column(UiFactory.Row(L_String, TB_Text), FLP_Characters, FLP_Hex, GB_Trash);
        SetBody(body);

        if (raw.Length != 0)
            AddTrashEditing(raw.Length, generation);
        AddCharEditing(context);
        TB_Text.MaxLength = tb.MaxLength;
        TB_Text.Text = tb.Text;

        B_ApplyTrash.Click += async (_, _) => await B_ApplyTrash_Click();
        B_ClearTrash.Click += (_, _) => B_ClearTrash_Click();
        editing = false;
    }

    protected override void OnSave()
    {
        FinalString = TB_Text.Text ?? string.Empty;
        if (FinalBytes.Length == 0)
            FinalBytes = Raw;
        Close();
    }

    private void AddCharEditing(EntityContext context)
    {
        var chars = GetChars(context);
        if (chars.Length == 0)
            return;

        FLP_Characters.IsVisible = true;
        foreach (ushort c in chars)
        {
            var text = ((char)c).ToString();
            var l = new TextBlock
            {
                Text = text,
                Margin = new Thickness(3, 2),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            l.AttachClick(_ =>
            {
                if ((TB_Text.Text ?? string.Empty).Length < TB_Text.MaxLength)
                    TB_Text.Text += text;
            });
            ToolTip.SetTip(l, $"Insert {text} (0x{c:X4})");
            FLP_Characters.Children.Add(l);
        }
    }

    private void AddTrashEditing(int count, byte generation)
    {
        FLP_Hex.IsVisible = true;
        GB_Trash.IsVisible = true;
        NUD_Generation.Value = generation;
        for (int i = 0; i < count; i++)
        {
            var l = new TextBlock { Text = $"${i:X2}", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0) };
            var n = UiFactory.Numeric($"NUD_Byte{i}", 2, 44, hex: true);
            n.Text = Raw[i].ToString("X2");
            n.AttachClick(mods =>
            {
                if (mods == KeyModifiers.Shift)
                    n.Text = "FF";
                else if (mods == KeyModifiers.Alt)
                    n.Text = "00";
            });
            n.OnTextChanged(_ => UpdateNUD(n));

            FLP_Hex.Children.Add(l);
            FLP_Hex.Children.Add(n);
            Bytes.Add(n);
        }
        TB_Text.OnTextChanged(UpdateString);

        var source = GameInfo.Sources; // don't use FilteredSources here -- allow any species
        CB_Species.SetItems(source.SpeciesDataSource);
        CB_Language.SetItems(GameInfo.LanguageDataSource(generation, Converter is IContext c ? c.Context : EntityContext.None));
        // WinForms binds via DataSource, which selects the first entry.
        CB_Species.SelectedIndex = 0;
        CB_Language.SelectedIndex = 0;
    }

    private void UpdateNUD(NumericTextBox nud)
    {
        if (editing)
            return;

        // build bytes
        editing = true;
        int index = Bytes.IndexOf(nud);
        Raw[index] = (byte)nud.IntValue;

        TB_Text.Text = GetString();
        editing = false;
    }

    private void UpdateString(TextBox tb)
    {
        if (editing)
            return;
        editing = true;
        // build bytes
        ReadOnlySpan<byte> data = SetString(tb.Text ?? string.Empty);
        if (data.Length > Raw.Length)
            data = data[..Raw.Length];
        data.CopyTo(Raw);
        for (int i = 0; i < Raw.Length; i++)
            Bytes[i].Text = Raw[i].ToString("X2");
        editing = false;
    }

    private async Task B_ApplyTrash_Click()
    {
        string text = GetTrashString();
        byte[] data = SetString(text);
        byte[] current = SetString(TB_Text.Text ?? string.Empty);
        if (data.Length <= current.Length)
        {
            await AppDialogs.Alert(this, "Trash byte layer is hidden by current text.",
                $"Current Bytes: {current.Length}" + Environment.NewLine + $"Layer Bytes: {data.Length}");
            return;
        }
        if (data.Length > Bytes.Count)
        {
            await AppDialogs.Alert(this, "Trash byte layer is too long to apply.");
            return;
        }
        for (int i = current.Length; i < data.Length; i++)
            Bytes[i].Text = data[i].ToString("X2");
    }

    private string GetTrashString()
    {
        var species = (ushort)CB_Species.GetValue();
        var language = CB_Language.GetValue();
        var gen = (byte)(NUD_Generation.Value ?? 0);
        string text = SpeciesName.GetSpeciesNameGeneration(species, language, gen);

        if (string.IsNullOrEmpty(text)) // no result
            text = CB_Species.GetText();
        return text;
    }

    private void B_ClearTrash_Click()
    {
        byte[] current = SetString(TB_Text.Text ?? string.Empty);
        for (int i = current.Length; i < Bytes.Count; i++)
            Bytes[i].Text = "00";
    }

    private byte[] SetString(ReadOnlySpan<char> text)
    {
        Span<byte> temp = stackalloc byte[Raw.Length];
        var written = Converter.SetString(temp, text, text.Length, StringConverterOption.None);
        return [.. temp[..written]];
    }

    private string GetString() => Converter.GetString(Raw);

    private static ReadOnlySpan<ushort> GetChars(EntityContext context) => context switch
    {
        EntityContext.Gen5 => SpecialCharsGen5,
        EntityContext.Gen6 or EntityContext.Gen7 or EntityContext.Gen7b => SpecialCharsGen67,
        _ when !context.IsEraPreSwitch => SpecialCharsGen8,
        _ => [], // Undocumented
    };

    // Unicode codepoints for special characters, incorrectly starting at 0x2460 instead of 0xE0xx.
    private static ReadOnlySpan<ushort> SpecialCharsGen5 =>
    [
        0x2460, 0x2461, 0x2462, 0x2463, 0x2464, 0x2465, 0x2466, 0x2467, 0x2468,
        // Skip 69-6B, can't be entered.
        0x246C, 0x246D, 0x246E, 0x246F, 0x2470, 0x2471, 0x2472, 0x2473, 0x2474, 0x2475, 0x2476, 0x2477,
        0x2478, 0x2479, 0x247A, 0x247B, 0x247C, 0x247D, 0x247E, 0x247F, 0x2480, 0x2481, 0x2482, 0x2483, 0x2484,
    ];

    private static ReadOnlySpan<ushort> SpecialCharsGen67 =>
    [
        0xE081, 0xE082, 0xE083, 0xE084, 0xE085, 0xE086, 0xE087, 0xE088, 0xE089,
        // Skip 8A-8C, can't be entered.
        0xE08D, 0xE08E, 0xE08F, 0xE090, 0xE091, 0xE092, 0xE093, 0xE094, 0xE095, 0xE096, 0xE097, 0xE098,
        0xE099, 0xE09A, 0xE09B, 0xE09C, 0xE09D, 0xE09E, 0xE09F, 0xE0A0, 0xE0A1, 0xE0A2, 0xE0A3, 0xE0A4, 0xE0A5,
    ];

    private static ReadOnlySpan<ushort> SpecialCharsGen8 =>
    [
        '…', '♂', '♀', '♠', '♣', '♥', '♦', '★', '◎', '○', '□', '△', '◇', '♪', '☀', '☁', '☂', '☃',
    ];
}
