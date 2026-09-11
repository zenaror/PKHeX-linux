using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen6;

/// <summary>
/// Read-only view of the X/Y berry field plots (port of the WinForms <c>SAV_BerryFieldXY</c>).
/// </summary>
/// <remarks>
/// The plot structure is only partially understood upstream, so this shows the raw 16-bit values
/// exactly like the WinForms form does, including its "needs more research" note.
/// </remarks>
public sealed class BerryFieldXYWindow : Window
{
    private readonly SAV6XY SAV;
    private readonly ListBox listBox1 = new() { Name = "listBox1", Width = 70, Height = 320 };
    private readonly TextBox[] Fields;

    public BerryFieldXYWindow(SAV6XY sav)
    {
        Name = "SAV_BerryFieldXY";
        Title = "Berry Field";
        Icon = AppIcon.Get();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        SAV = sav;

        listBox1.ItemsSource = new ObservableCollection<string>(Enumerable.Range(1, 36).Select(i => i.ToString("00")));

        var grid = UiFactory.FormGrid(8);
        Fields =
        [
            UiFactory.Text("TB_Berry", 6, 90),
            UiFactory.Text("TB_u1", 6, 90), UiFactory.Text("TB_u2", 6, 90), UiFactory.Text("TB_u3", 6, 90),
            UiFactory.Text("TB_u4", 6, 90), UiFactory.Text("TB_u5", 6, 90), UiFactory.Text("TB_u6", 6, 90),
            UiFactory.Text("TB_u7", 6, 90),
        ];
        foreach (var tb in Fields)
            tb.IsReadOnly = true;

        UiFactory.AddFormRow(grid, 0, UiFactory.Label("L_Berry", "Berry:"), Fields[0]);
        for (int i = 1; i < Fields.Length; i++)
            UiFactory.AddFormRow(grid, i, UiFactory.Label($"L_u{i}", $"Unknown {i}:"), Fields[i]);

        var note = UiFactory.Label("L_Unfinished", "Unfinished - Needs More Research");
        note.SetForeColor(ColorUtilAvalonia.ColorWarn);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        body.Children.Add(UiFactory.Column(UiFactory.Label("L_Field", "Field"), listBox1));
        body.Children.Add(UiFactory.Column(grid, note));

        var B_Cancel = UiFactory.Button("B_Cancel", "Cancel");
        B_Cancel.MinWidth = 80;
        B_Cancel.HorizontalAlignment = HorizontalAlignment.Right;
        B_Cancel.Click += (_, _) => Close();

        var root = new DockPanel { Margin = new global::Avalonia.Thickness(10) };
        DockPanel.SetDock(B_Cancel, Dock.Bottom);
        root.Children.Add(B_Cancel);
        root.Children.Add(body);
        Content = root;
        KeyDown += (_, e) => { if (e.Key == global::Avalonia.Input.Key.Escape) Close(); };

        listBox1.SelectionChanged += (_, _) => ChangeField();
        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
        listBox1.SelectedIndex = 0;
    }

    private void ChangeField()
    {
        var index = listBox1.SelectedIndex;
        if (index < 0)
            return;
        var span = SAV.BerryField.GetPlot(index);
        for (int i = 0; i < Fields.Length; i++)
            Fields[i].Text = BinaryPrimitives.ReadUInt16LittleEndian(span[(2 * i)..]).ToString();
    }
}
