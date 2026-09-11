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

namespace PKHeX.Avalonia.Views.SaveEditors.Gen8;

/// <summary>
/// Event flag and work value editor for Brilliant Diamond / Shining Pearl (port of <c>SAV_FlagWork8b</c>).
/// </summary>
/// <remarks>
/// BD/SP keeps three separate arrays — story flags, system flags and work values — each grouped into labelled
/// categories. Every category page has its own search box, and the bottom bar edits one entry by raw index.
/// </remarks>
public sealed class FlagWork8bWindow : SaveEditorWindow
{
    private readonly SAV8BS Origin;
    private readonly SAV8BS SAV;
    private readonly FlagWork8b Work;

    private readonly Dictionary<int, BoolRow> FlagRows = [];
    private readonly Dictionary<int, BoolRow> SystemRows = [];
    private readonly Dictionary<int, WorkRow> WorkRows = [];
    private bool editing;

    private readonly TabControl TC_Features = new() { Name = "TC_Features" };
    private readonly TabControl TC_Flags = new() { Name = "TC_Flags" };
    private readonly TabControl TC_System = new() { Name = "TC_System" };
    private readonly TabControl TC_Work = new() { Name = "TC_Work" };

    private readonly NumericUpDown NUD_Flag = UiFactory.NumericUpDown("NUD_Flag", 0, FlagWork8b.COUNT_FLAG - 1, 110);
    private readonly CheckBox CHK_CustomFlag = UiFactory.Check("CHK_CustomFlag", string.Empty);
    private readonly Button B_ApplyFlag = UiFactory.Button("B_ApplyFlag", "Apply");
    private readonly NumericUpDown NUD_System = UiFactory.NumericUpDown("NUD_System", 0, FlagWork8b.COUNT_SYSTEM - 1, 110);
    private readonly CheckBox CHK_CustomSystem = UiFactory.Check("CHK_CustomSystem", string.Empty);
    private readonly Button B_ApplySystemFlag = UiFactory.Button("B_ApplySystemFlag", "Apply");
    private readonly NumericUpDown NUD_WorkIndex = UiFactory.NumericUpDown("NUD_WorkIndex", 0, FlagWork8b.COUNT_WORK - 1, 110);
    private readonly NumericUpDown NUD_Work = UiFactory.NumericUpDown("NUD_Work", int.MinValue, int.MaxValue, 140);
    private readonly Button B_ApplyWork = UiFactory.Button("B_ApplyWork", "Apply");

    private readonly Button B_LoadOld = UiFactory.Button("B_LoadOld", "Load Old");
    private readonly Button B_LoadNew = UiFactory.Button("B_LoadNew", "Load New");
    private readonly TextBox TB_OldSAV = UiFactory.Text("TB_OldSAV", 260, 360);
    private readonly TextBox TB_NewSAV = UiFactory.Text("TB_NewSAV", 260, 360);
    private readonly TextBox RTB_Diff = new() { Name = "RTB_Diff", AcceptsReturn = true, IsReadOnly = true, Height = 380, TextWrapping = global::Avalonia.Media.TextWrapping.NoWrap };

    public FlagWork8bWindow(SAV8BS sav) : base("SAV_FlagWork8b", "Event Flag/Work Editor")
    {
        SizeToContent = SizeToContent.Manual;
        CanResize = true;
        Width = 940;
        Height = 700;

        Origin = sav;
        SAV = (SAV8BS)sav.Clone();
        Work = SAV.FlagWork;

        BuildLayout();

        editing = true;
        var labels = new EventLabelCollectionSystem(GetGameFilePrefix(SAV.Version), Work.CountFlag - 1, Work.CountSystem - 1, Work.CountWork - 1);
        AddBoolLists(TC_Flags, labels.Flag, "F", FlagRows, Work.GetFlag, (i, v) => Work.SetFlag(i, v), () => (int)(NUD_Flag.Value ?? 0), CHK_CustomFlag);
        AddBoolLists(TC_System, labels.System, "S", SystemRows, Work.GetSystemFlag, (i, v) => Work.SetSystemFlag(i, v), () => (int)(NUD_System.Value ?? 0), CHK_CustomSystem);
        AddWorkLists(labels.Work);
        editing = false;

        CHK_CustomFlag.IsChecked = Work.GetFlag(0);
        CHK_CustomSystem.IsChecked = Work.GetSystemFlag(0);
        NUD_Work.SetValueClamped(Work.GetWork(0));

        NUD_Flag.ValueChanged += (_, _) => CHK_CustomFlag.IsChecked = Work.GetFlag((int)(NUD_Flag.Value ?? 0));
        NUD_System.ValueChanged += (_, _) => CHK_CustomSystem.IsChecked = Work.GetSystemFlag((int)(NUD_System.Value ?? 0));
        NUD_WorkIndex.ValueChanged += (_, _) => NUD_Work.SetValueClamped(Work.GetWork((int)(NUD_WorkIndex.Value ?? 0)));
        B_ApplyFlag.Click += (_, _) => ApplyFlag();
        B_ApplySystemFlag.Click += (_, _) => ApplySystemFlag();
        B_ApplyWork.Click += (_, _) => ApplyWork();

        Title = $"{Title} ({sav.Version})";
    }

    private static string GetGameFilePrefix(GameVersion version) => version switch
    {
        GameVersion.BD or GameVersion.SP or GameVersion.BDSP => "bdsp",
        _ => throw new IndexOutOfRangeException(nameof(version)),
    };

    #region Layout

    private void BuildLayout()
    {
        // Three groups of controls; wrap rather than clip when the window is narrow.
        var status = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 2) };
        status.Children.Add(UiFactory.Row(UiFactory.Label("L_Flag", "Flag:"), NUD_Flag, CHK_CustomFlag, B_ApplyFlag));
        status.Children.Add(UiFactory.Row(UiFactory.Label("L_System", "System:"), NUD_System, CHK_CustomSystem, B_ApplySystemFlag));
        status.Children.Add(UiFactory.Row(UiFactory.Label("L_Work", "Work:"), NUD_WorkIndex, NUD_Work, B_ApplyWork));

        TC_Features.Items.Add(new TabItem { Name = "Tab_Flags", Header = "Flags", Content = TC_Flags });
        TC_Features.Items.Add(new TabItem { Name = "Tab_System", Header = "System", Content = TC_System });
        TC_Features.Items.Add(new TabItem { Name = "Tab_Work", Header = "Work", Content = TC_Work });
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
        var body = UiFactory.Column(
            UiFactory.Row(B_LoadOld, TB_OldSAV),
            UiFactory.Row(B_LoadNew, TB_NewSAV),
            RTB_Diff);
        return body;
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
        var diff = new EventWorkDiff8b(TB_OldSAV.Text!, TB_NewSAV.Text!);
        if (diff.Message != EventWorkDiffCompatibility.Valid)
        {
            await AppDialogs.Alert(this, diff.Message.GetMessage());
            return;
        }
        RTB_Diff.Text = string.Join(Environment.NewLine, diff.Summarize());
    }

    #endregion

    #region Lists

    private void AddBoolLists(TabControl host, IReadOnlyList<NamedEventValue> labels, string prefix, Dictionary<int, BoolRow> sink,
        Func<int, bool> get, Action<int, bool> set, Func<int> customIndex, CheckBox customCheck)
    {
        sink.Clear();
        host.Items.Clear();
        foreach (var group in labels.GroupBy(z => z.Type).OrderBy(z => z.Key))
        {
            var entries = new ObservableCollection<BoolRow>();
            foreach (var (name, index, _) in group)
            {
                var row = new BoolRow(index, set, customIndex, customCheck) { Name = name, IsSet = get(index) };
                entries.Add(row);
                sink[index] = row;
            }

            var listBox = MakeCheckList(entries);
            var all = entries.ToList();
            var search = CreateSearchBox(text => ApplyFilter(listBox, all, text, z => z.Name));
            host.Items.Add(new TabItem
            {
                Name = $"Tab_{prefix}{group.Key}",
                Header = Translator.TranslateEnum(group.Key, MainWindow.CurrentLanguage),
                Content = Host(search, listBox),
            });
        }
    }

    private void AddWorkLists(IReadOnlyList<NamedEventWork> labels)
    {
        WorkRows.Clear();
        TC_Work.Items.Clear();
        foreach (var group in labels.GroupBy(z => z.Type).OrderBy(z => z.Key))
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2, Margin = new Thickness(4) };
            var rows = new List<(string Search, Control Row)>();
            foreach (var entry in group)
            {
                var row = new WorkRow(this, entry, Work.GetWork(entry.Index));
                WorkRows[entry.Index] = row;
                panel.Children.Add(row.View);
                rows.Add((entry.Name, row.View));
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
                Name = $"Tab_W{group.Key}",
                Header = Translator.TranslateEnum(group.Key, MainWindow.CurrentLanguage),
                Content = Host(search, scroll),
            });
        }
    }

    private static ListBox MakeCheckList(ObservableCollection<BoolRow> entries)
    {
        var listBox = new ListBox
        {
            ItemsSource = entries,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new FuncDataTemplate<BoolRow>((_, _) =>
            {
                var chk = new CheckBox { MinHeight = 0, Padding = new Thickness(6, 0, 0, 0) };
                chk.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(BoolRow.IsSet)) { Mode = BindingMode.TwoWay });
                chk.Bind(ContentControl.ContentProperty, new Binding(nameof(BoolRow.Name)));
                return chk;
            }),
        };
        listBox.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(2, 0)), new Setter(MinHeightProperty, 0d) },
        });
        return listBox;
    }

    private static Control Host(Control search, Control content)
    {
        var host = new DockPanel();
        DockPanel.SetDock(search, Dock.Top);
        host.Children.Add(search);
        host.Children.Add(content);
        return host;
    }

    private static void ApplyFilter<T>(ListBox box, List<T> all, string text, Func<T, string> selector)
    {
        var query = text.Trim();
        box.ItemsSource = query.Length == 0
            ? all
            : all.Where(z => selector(z).Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }

    private static TextBox CreateSearchBox(Action<string> applyFilter)
    {
        var box = new TextBox { PlaceholderText = "Search...", MinHeight = 0, Padding = new Thickness(4, 2) };
        box.OnTextChanged(tb => applyFilter(tb.Text ?? string.Empty));
        return box;
    }

    #endregion

    #region Custom index bar

    private void ApplyFlag()
    {
        var index = (int)(NUD_Flag.Value ?? 0);
        var value = CHK_CustomFlag.IsChecked == true;
        Work.SetFlag(index, value);
        if (FlagRows.TryGetValue(index, out var row))
            row.SetQuiet(value);
    }

    private void ApplySystemFlag()
    {
        var index = (int)(NUD_System.Value ?? 0);
        var value = CHK_CustomSystem.IsChecked == true;
        Work.SetSystemFlag(index, value);
        if (SystemRows.TryGetValue(index, out var row))
            row.SetQuiet(value);
    }

    private void ApplyWork()
    {
        var index = (int)(NUD_WorkIndex.Value ?? 0);
        var value = (int)(NUD_Work.Value ?? 0);
        Work.SetWork(index, value);
        if (WorkRows.TryGetValue(index, out var row))
            row.SetQuiet(value);
    }

    #endregion

    #region Rows

    private sealed class BoolRow(int index, Action<int, bool> set, Func<int> customIndex, CheckBox customCheck) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private bool isSet;
        private bool quiet;

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
                if (quiet)
                    return;
                set(index, value);
                if (customIndex() == index)
                    customCheck.IsChecked = value;
            }
        }

        /// <summary>Updates the display without writing back (the value came from the block).</summary>
        public void SetQuiet(bool value)
        {
            quiet = true;
            IsSet = value;
            quiet = false;
        }
    }

    /// <summary>One work row: name, predefined value picker and raw value.</summary>
    private sealed class WorkRow
    {
        public Control View { get; }
        private readonly NumericUpDown Numeric;
        private readonly ComboBox Combo;
        private readonly List<ComboItem> Map;
        private bool updating;

        public WorkRow(FlagWork8bWindow owner, NamedEventWork entry, int value)
        {
            Map = entry.PredefinedValues.Select(z => new ComboItem(z.Name, z.Value)).ToList();
            Numeric = UiFactory.NumericUpDown($"NUD_W{entry.Index}", int.MinValue, int.MaxValue, 110);
            Combo = UiFactory.Combo($"CB_W{entry.Index}", 190);
            Combo.SetItems(Map);

            var label = UiFactory.Label($"L_W{entry.Index}", entry.Name, clickable: true);
            label.AttachClick(_ => Numeric.Value = 0);
            // Fixed width so the columns line up across rows, as the WinForms table layout does.
            label.Width = 290;
            label.TextTrimming = global::Avalonia.Media.TextTrimming.CharacterEllipsis;
            ToolTip.SetTip(label, entry.Name);
            View = UiFactory.Row(label, Combo, Numeric);

            void ChangeConstValue()
            {
                if (updating)
                    return;
                updating = true;
                var current = (int)(Numeric.Value ?? 0);
                var (_, valueID) = Map.Find(z => z.Value == current) ?? Map[0];
                // GetValue reports 0 for "nothing selected", so compare the item itself.
                if (Combo.GetSelectedItem()?.Value != valueID)
                    Combo.SetValue(valueID);

                owner.Work.SetWork(entry.Index, current);
                if ((int)(owner.NUD_WorkIndex.Value ?? 0) == entry.Index)
                    owner.NUD_Work.SetValueClamped(current);
                updating = false;
            }

            Numeric.ValueChanged += (_, _) => ChangeConstValue();
            Combo.SelectionChanged += (_, _) =>
            {
                if (owner.editing || updating)
                    return;
                var selected = Combo.GetValue();
                Numeric.Value = selected == NamedEventConst.CustomMagicValue ? 0 : selected;
            };

            Numeric.SetValueClamped(value);
            if (Numeric.Value == 0)
                ChangeConstValue(); // setting the same value raises no event, so seed the picker explicitly
        }

        /// <summary>Updates the display without writing back.</summary>
        public void SetQuiet(int value)
        {
            updating = true;
            Numeric.SetValueClamped(value);
            var (_, valueID) = Map.Find(z => z.Value == value) ?? Map[0];
            Combo.SetValue(valueID);
            updating = false;
        }
    }

    #endregion

    protected override void OnSave()
    {
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
