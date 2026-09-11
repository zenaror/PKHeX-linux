using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Styling;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen7;

/// <summary>
/// Event flag and work value editor for Let's Go (port of the WinForms <c>SAV_EventWork</c>).
/// </summary>
/// <remarks>
/// Let's Go splits its event variables into named groups; each group gets its own searchable page. A work
/// value with a matching predefined option locks the raw box, exactly as the WinForms editor does.
/// </remarks>
public sealed class EventWork7bWindow : SaveEditorWindow
{
    private readonly SAV7b Origin;
    private readonly EventWork7b SAV;
    private readonly SplitEventEditor<int> Editor;
    private bool editing;

    private readonly TabControl TC_Features = new() { Name = "TC_Features" };
    private readonly TabControl TC_Flag = new() { Name = "TC_Flag" };
    private readonly TabControl TC_Work = new() { Name = "TC_Work" };
    private readonly TabItem GB_Constants = new() { Name = "GB_Constants", Header = "Work" };

    private readonly NumericUpDown NUD_Flag = UiFactory.NumericUpDown("NUD_Flag", 0, ushort.MaxValue, 110);
    private readonly CheckBox c_CustomFlag = UiFactory.Check("c_CustomFlag", string.Empty);
    private readonly Button B_ApplyFlag = UiFactory.Button("B_ApplyFlag", "Apply");
    private readonly TextBlock L_Stats = UiFactory.Label("L_Stats", "Work:");
    private readonly ComboBox CB_Stats = UiFactory.StringCombo("CB_Stats", 110);
    private readonly NumericUpDown NUD_Stat = UiFactory.NumericUpDown("NUD_Stat", int.MinValue, int.MaxValue, 140);
    private readonly Button B_ApplyWork = UiFactory.Button("B_ApplyWork", "Apply");

    private readonly Button B_LoadOld = UiFactory.Button("B_LoadOld", "Load Old");
    private readonly Button B_LoadNew = UiFactory.Button("B_LoadNew", "Load New");
    private readonly TextBox TB_OldSAV = UiFactory.Text("TB_OldSAV", 260, 360);
    private readonly TextBox TB_NewSAV = UiFactory.Text("TB_NewSAV", 260, 360);
    private readonly TextBox RTB_Diff = new() { Name = "RTB_Diff", AcceptsReturn = true, IsReadOnly = true, Height = 380, TextWrapping = global::Avalonia.Media.TextWrapping.NoWrap };

    public EventWork7bWindow(SAV7b sav) : base("SAV_EventWork", "Event Flag/Work Editor")
    {
        SizeToContent = SizeToContent.Manual;
        CanResize = true;
        Width = 940;
        Height = 700;

        Origin = sav;
        SAV = sav.Blocks.EventWork;

        var prefix = GetGameFilePrefix(sav.Version);
        var work = GameLanguage.GetStrings(prefix, GameInfo.CurrentLanguage, "const");
        var flag = GameLanguage.GetStrings(prefix, GameInfo.CurrentLanguage, "flags");
        Editor = new SplitEventEditor<int>(SAV, work, flag);

        BuildLayout();

        editing = true;
        for (int i = 0; i < SAV.CountWork; i++)
            CB_Stats.Items.Add(i.ToString());
        LoadFlags(Editor.Flag);
        LoadWork(Editor.Work);
        editing = false;

        if (CB_Stats.ItemCount > 0)
        {
            CB_Stats.SelectedIndex = 0;
        }
        else
        {
            L_Stats.IsVisible = CB_Stats.IsVisible = NUD_Stat.IsVisible = B_ApplyWork.IsVisible = false;
            TC_Features.Items.Remove(GB_Constants);
        }

        NUD_Flag.Maximum = SAV.CountFlag - 1;
        NUD_Flag.Value = 0;
        c_CustomFlag.IsChecked = SAV.GetFlag(0);

        NUD_Flag.ValueChanged += (_, _) => c_CustomFlag.IsChecked = SAV.GetFlag((int)(NUD_Flag.Value ?? 0));
        CB_Stats.SelectionChanged += (_, _) => NUD_Stat.SetValueClamped(SAV.GetWork(Math.Max(0, CB_Stats.SelectedIndex)));
        B_ApplyFlag.Click += (_, _) => SAV.SetFlag((int)(NUD_Flag.Value ?? 0), c_CustomFlag.IsChecked == true);
        B_ApplyWork.Click += (_, _) => SAV.SetWork(Math.Max(0, CB_Stats.SelectedIndex), (int)(NUD_Stat.Value ?? 0));

        Title = $"{Title} ({sav.Version})";
    }

    private static string GetGameFilePrefix(GameVersion version) => version switch
    {
        GameVersion.GP or GameVersion.GE or GameVersion.GG => "gg",
        _ => throw new IndexOutOfRangeException(nameof(version)),
    };

    #region Layout

    private void BuildLayout()
    {
        var status = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 2) };
        status.Children.Add(UiFactory.Row(UiFactory.Label("L_Flag", "Flag:"), NUD_Flag, c_CustomFlag, B_ApplyFlag));
        status.Children.Add(UiFactory.Row(L_Stats, CB_Stats, NUD_Stat, B_ApplyWork));

        TC_Features.Items.Add(new TabItem { Name = "GB_Flags", Header = "Flags", Content = TC_Flag });
        GB_Constants.Content = TC_Work;
        TC_Features.Items.Add(GB_Constants);
        TC_Features.Items.Add(new TabItem { Name = "GB_Research", Header = "Research", Content = BuildResearchTab() });

        var body = new DockPanel();
        DockPanel.SetDock(status, Dock.Bottom);
        body.Children.Add(status);
        body.Children.Add(TC_Features);
        SetBody(body);
    }

    private Control BuildResearchTab()
    {
        B_LoadOld.Click += async (_, _) => await PickSave(TB_OldSAV);
        B_LoadNew.Click += async (_, _) => await PickSave(TB_NewSAV);
        return UiFactory.Column(
            UiFactory.Row(B_LoadOld, TB_OldSAV),
            UiFactory.Row(B_LoadNew, TB_NewSAV),
            RTB_Diff);
    }

    private async Task PickSave(TextBox target)
    {
        var path = await FileDialogs.OpenSingleFile(this);
        if (path is null)
            return;
        target.Text = path;
        if ((TB_NewSAV.Text ?? string.Empty).Length != 0 && (TB_OldSAV.Text ?? string.Empty).Length != 0)
            await DiffSaves();
    }

    private async Task DiffSaves()
    {
        var diff = new EventWorkDiff7b(TB_OldSAV.Text!, TB_NewSAV.Text!);
        if (diff.Message != EventWorkDiffCompatibility.Valid)
        {
            await AppDialogs.Alert(this, diff.Message.GetMessage());
            return;
        }
        RTB_Diff.Text = string.Join(Environment.NewLine, diff.Summarize());
    }

    #endregion

    #region Lists

    private void LoadFlags(IEnumerable<EventVarGroup> groups)
    {
        TC_Flag.Items.Clear();
        foreach (var g in groups)
        {
            var entries = new ObservableCollection<FlagRow>();
            foreach (var f in g.Vars.OfType<EventFlag>())
                entries.Add(new FlagRow(f) { Name = f.Name });

            var listBox = new ListBox
            {
                ItemsSource = entries,
                SelectionMode = SelectionMode.Single,
                ItemTemplate = new FuncDataTemplate<FlagRow>((_, _) =>
                {
                    var chk = new CheckBox { MinHeight = 0, Padding = new Thickness(6, 0, 0, 0) };
                    chk.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(FlagRow.IsSet)) { Mode = BindingMode.TwoWay });
                    chk.Bind(ContentControl.ContentProperty, new Binding(nameof(FlagRow.Name)));
                    return chk;
                }),
            };
            listBox.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
            {
                Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(2, 0)), new Setter(MinHeightProperty, 0d) },
            });

            var all = entries.ToList();
            var search = CreateSearchBox(text =>
            {
                var query = text.Trim();
                listBox.ItemsSource = query.Length == 0
                    ? all
                    : all.Where(z => z.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
            });
            TC_Flag.Items.Add(new TabItem
            {
                Name = $"Tab_F{g.Type}",
                Header = Translator.TranslateEnum(g.Type, MainWindow.CurrentLanguage),
                Content = Host(search, listBox),
            });
        }
    }

    private void LoadWork(IEnumerable<EventVarGroup> groups)
    {
        TC_Work.Items.Clear();
        foreach (var g in groups)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2, Margin = new Thickness(4) };
            var rows = new List<(string Search, Control Row)>();
            foreach (var f in g.Vars.OfType<EventWork<int>>())
            {
                var row = new WorkRow(this, f);
                panel.Children.Add(row.View);
                rows.Add((f.Name, row.View));
            }

            var scroll = new ScrollViewer { Content = panel };
            var search = CreateSearchBox(text =>
            {
                var query = text.Trim();
                foreach (var (name, control) in rows)
                    control.IsVisible = query.Length == 0 || name.Contains(query, StringComparison.CurrentCultureIgnoreCase);
            });
            TC_Work.Items.Add(new TabItem
            {
                Name = $"Tab_W{g.Type}",
                Header = Translator.TranslateEnum(g.Type, MainWindow.CurrentLanguage),
                Content = Host(search, scroll),
            });
        }
    }

    private static Control Host(Control search, Control content)
    {
        var host = new DockPanel();
        DockPanel.SetDock(search, Dock.Top);
        host.Children.Add(search);
        host.Children.Add(content);
        return host;
    }

    private static TextBox CreateSearchBox(Action<string> applyFilter)
    {
        var box = new TextBox { PlaceholderText = "Search...", MinHeight = 0, Padding = new Thickness(4, 2) };
        box.OnTextChanged(tb => applyFilter(tb.Text ?? string.Empty));
        return box;
    }

    #endregion

    #region Rows

    private sealed class FlagRow(EventFlag flag) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private bool isSet = flag.Flag;

        public required string Name { get; init; }

        public bool IsSet
        {
            get => isSet;
            set
            {
                if (isSet == value)
                    return;
                isSet = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSet)));
                flag.Flag = value;
            }
        }
    }

    /// <summary>One work row; the raw box is only editable while the picker is on a custom option.</summary>
    private sealed class WorkRow
    {
        public Control View { get; }

        public WorkRow(EventWork7bWindow owner, EventWork<int> entry)
        {
            var options = entry.Options.ConvertAll(z => new ComboItem(z.Text, z.Value));
            var numeric = UiFactory.NumericUpDown($"NUD_W{entry.RawIndex}", int.MinValue, int.MaxValue, 110);
            var combo = UiFactory.Combo($"CB_W{entry.RawIndex}", 190);
            combo.SetItems(options);

            var label = UiFactory.Label($"L_W{entry.RawIndex}", entry.Name);
            // Fixed width so the columns line up across rows, as the WinForms table layout does.
            label.Width = 290;
            label.TextTrimming = global::Avalonia.Media.TextTrimming.CharacterEllipsis;
            ToolTip.SetTip(label, entry.Name);
            View = UiFactory.Row(label, combo, numeric);

            numeric.SetValueClamped(entry.Value);
            combo.SetValue(entry.Value);
            if (combo.SelectedIndex < 0)
                combo.SelectedIndex = 0;

            var match = entry.Options.Find(z => z.Value == entry.Value);
            if (match is not null)
            {
                combo.SetValue(match.Value);
                numeric.IsEnabled = false;
            }

            combo.SelectionChanged += (_, _) =>
            {
                if (owner.editing)
                    return;
                var value = combo.GetValue();
                owner.editing = true;
                var selected = entry.Options.Find(z => z.Value == value);
                numeric.IsEnabled = selected?.Custom == true;
                if (!numeric.IsEnabled)
                {
                    numeric.SetValueClamped((ushort)value);
                    entry.Value = value;
                }
                owner.editing = false;
            };
            numeric.ValueChanged += (_, _) =>
            {
                if (owner.editing)
                    return;
                var value = (int)(numeric.Value ?? 0);
                entry.Value = value;
                owner.editing = true;
                if (entry.RawIndex == owner.CB_Stats.SelectedIndex)
                    owner.NUD_Stat.SetValueClamped(value);
                owner.editing = false;
            };
        }
    }

    #endregion

    protected override void OnSave()
    {
        Editor.Save();
        Origin.State.Edited = true;
        Close();
    }
}
