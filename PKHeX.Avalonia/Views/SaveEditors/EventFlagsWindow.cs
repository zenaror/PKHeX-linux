using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Views.SaveEditors;

/// <summary>
/// Event flag / event constant editor shared by the generation-specific windows
/// (port of the WinForms <c>SAV_EventFlags</c> and <c>SAV_EventFlags2</c>, which duplicate this layout).
/// </summary>
/// <typeparam name="TSave">Save (or save block) exposing the flag and work arrays.</typeparam>
/// <typeparam name="TWork">Storage type of one event constant: <c>ushort</c> from Gen 3 on, <c>byte</c> in Gen 2.</typeparam>
public abstract class EventFlagsWindowBase<TSave, TWork> : SaveEditorWindow
    where TSave : class, IEventFlagArray, IEventWorkArray<TWork>
    where TWork : unmanaged, IEquatable<TWork>, INumber<TWork>, IMinMaxValue<TWork>
{
    private readonly EventWorkspace<TSave, TWork> Editor;
    private readonly Dictionary<int, FlagRow> FlagRows = [];
    private readonly Dictionary<int, WorkRow> WorkRows = [];
    private bool editing;

    private readonly TabControl tabControl1 = new();
    private readonly TabItem GB_Flags = new() { Name = "GB_Flags", Header = "Event Flags" };
    private readonly TabItem GB_Constants = new() { Name = "GB_Constants", Header = "Event Constants" };
    private readonly TabItem GB_Research = new() { Name = "GB_Research", Header = "Research" };
    private readonly TabControl TC_Flags = new() { Name = "TC_Flags" };
    private readonly TabControl TC_Const = new() { Name = "TC_Const" };

    private readonly TextBlock L_EventFlagWarn = UiFactory.Label("L_EventFlagWarn", "Altering Event Flags may impact other story events.\nSave file backups are recommended.");
    private readonly CheckBox CHK_CustomFlag = UiFactory.Check("CHK_CustomFlag", "Flag:");
    private readonly NumericUpDown NUD_Flag = UiFactory.NumericUpDown("NUD_Flag", 0, ushort.MaxValue, 110);
    private readonly CheckBox c_CustomFlag = UiFactory.Check("c_CustomFlag", string.Empty);
    private readonly TextBlock L_Stats = UiFactory.Label("L_Stats", "Constant:");
    private readonly ComboBox CB_Stats = UiFactory.StringCombo("CB_Stats", 110);
    private readonly NumericTextBox MT_Stat = UiFactory.Numeric("MT_Stat", 5, 70);

    // Researcher tab
    private readonly Button B_LoadOld = UiFactory.Button("B_LoadOld", "Load Old");
    private readonly TextBox TB_OldSAV = UiFactory.Text("TB_OldSAV", 260, 380);
    private readonly Button B_LoadNew = UiFactory.Button("B_LoadNew", "Load New");
    private readonly TextBox TB_NewSAV = UiFactory.Text("TB_NewSAV", 260, 380);
    private readonly TextBlock L_IsSet = UiFactory.Label("L_IsSet", "IsSet:");
    private readonly TextBox TB_IsSet = UiFactory.Text("TB_IsSet", 4000, 380);
    private readonly TextBlock L_UnSet = UiFactory.Label("L_UnSet", "UnSet:");
    private readonly TextBox TB_UnSet = UiFactory.Text("TB_UnSet", 4000, 380);

    protected EventFlagsWindowBase(TSave sav, GameVersion version, string formName) : base(formName, "Event Flag Editor")
    {
        SizeToContent = SizeToContent.Manual;
        CanResize = true;
        Width = 880;
        Height = 640;

        var editor = Editor = new EventWorkspace<TSave, TWork>(sav, version);

        var status = UiFactory.Row(CHK_CustomFlag, NUD_Flag, c_CustomFlag, L_Stats, CB_Stats, MT_Stat);
        CHK_CustomFlag.IsHitTestVisible = false; // label-like, matching the WinForms group header

        GB_Flags.Content = TC_Flags;
        GB_Constants.Content = TC_Const;
        GB_Research.Content = BuildResearchTab();
        tabControl1.Items.Add(GB_Flags);
        tabControl1.Items.Add(GB_Constants);
        tabControl1.Items.Add(GB_Research);

        var body = new DockPanel();
        DockPanel.SetDock(L_EventFlagWarn, Dock.Top);
        DockPanel.SetDock(status, Dock.Bottom);
        L_EventFlagWarn.SetForeColor(ColorUtilAvalonia.ColorWarn);
        body.Children.Add(L_EventFlagWarn);
        body.Children.Add(status);
        body.Children.Add(tabControl1);
        SetBody(body);
        Title = $"{Title} ({version})";

        editing = true;
        CB_Stats.Items.Clear();
        for (int i = 0; i < editor.Values.Length; i++)
            CB_Stats.Items.Add(i.ToString());

        AddFlagList(editor.Labels, editor.Flags);
        AddConstList(editor.Labels, editor.Values);

        if (CB_Stats.Items.Count > 0)
        {
            CB_Stats.SelectedIndex = 0;
        }
        else
        {
            L_Stats.IsVisible = CB_Stats.IsVisible = MT_Stat.IsVisible = false;
            tabControl1.Items.Remove(GB_Constants);
        }
        NUD_Flag.Maximum = editor.Flags.Length - 1;
        NUD_Flag.Value = 0;
        c_CustomFlag.IsChecked = editor.Flags[0];
        editing = false;

        NUD_Flag.ValueChanged += (_, _) => ChangeCustomFlag();
        c_CustomFlag.IsCheckedChanged += (_, _) => ChangeCustomBool();
        CB_Stats.SelectionChanged += (_, _) => ChangeConstantIndex();
        MT_Stat.OnTextChanged(_ => ChangeCustomConst());
    }

    protected override void OnSave()
    {
        Editor.Save();
        Close();
    }

    private Control BuildResearchTab()
    {
        var grid = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(grid, 0, null, UiFactory.Row(B_LoadOld, TB_OldSAV));
        UiFactory.AddFormRow(grid, 1, null, UiFactory.Row(B_LoadNew, TB_NewSAV));
        UiFactory.AddFormRow(grid, 2, L_IsSet, TB_IsSet);
        UiFactory.AddFormRow(grid, 3, L_UnSet, TB_UnSet);
        B_LoadOld.Click += async (_, _) => await OpenSAV(TB_OldSAV);
        B_LoadNew.Click += async (_, _) => await OpenSAV(TB_NewSAV);
        return grid;
    }

    private async Task OpenSAV(TextBox dest)
    {
        var path = await FileDialogs.OpenSingleFile(this);
        if (path is null)
            return;
        dest.Text = path;
        await ChangeSAV();
    }

    private async Task ChangeSAV()
    {
        if ((TB_NewSAV.Text ?? string.Empty).Length != 0 && (TB_OldSAV.Text ?? string.Empty).Length != 0)
            await DiffSaves();
    }

    private async Task DiffSaves()
    {
        var diff = new EventBlockDiff<TSave, TWork>(TB_OldSAV.Text ?? string.Empty, TB_NewSAV.Text ?? string.Empty);
        if (diff.Message != EventWorkDiffCompatibility.Valid)
        {
            await AppDialogs.Alert(this, diff.Message.GetMessage());
            return;
        }

        TB_IsSet.Text = string.Join(", ", diff.SetFlags.Select(z => $"{z:0000}"));
        TB_UnSet.Text = string.Join(", ", diff.ClearedFlags.Select(z => $"{z:0000}"));

        if (diff.WorkDiff.Count == 0)
        {
            await AppDialogs.Alert(this, "No Event Constant diff found.");
            return;
        }

        var promptCopy = await AppDialogs.Prompt(this, MessageBoxButtons.YesNo, "Copy Event Constant diff to clipboard?");
        if (promptCopy == DialogResult.Yes)
            await ClipboardService.SetText(this, string.Join(Environment.NewLine, diff.WorkDiff));
    }

    private void AddFlagList(EventLabelCollection list, bool[] values)
    {
        FlagRows.Clear();
        TC_Flags.Items.Clear();

        var labels = list.Flag;
        if (labels.Count == 0)
        {
            TC_Flags.IsVisible = false;
            var research = UiFactory.Label("TLP_Flags_Research", MsgResearchRequired);
            research.Margin = new Thickness(20);
            research.SetForeColor(ColorUtilAvalonia.ColorWarn);
            GB_Flags.Content = research;
            return;
        }

        foreach (var group in labels.GroupBy(z => z.Type).OrderBy(z => z.Key))
        {
            var entries = new ObservableCollection<FlagRow>();
            foreach (var (name, index, _) in group)
            {
                var row = new FlagRow(this, index) { Name = name, IsSet = values[index] };
                entries.Add(row);
                FlagRows[index] = row;
            }

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
            var search = CreateSearchBox(text => ApplyFilter(listBox, all, text, z => z.Name));
            var host = new DockPanel();
            DockPanel.SetDock(search, Dock.Top);
            host.Children.Add(search);
            host.Children.Add(listBox);

            var tab = new TabItem
            {
                Name = $"Tab_F{group.Key}",
                Header = Translator.TranslateEnum(group.Key, MainWindow.CurrentLanguage),
                Content = host,
            };
            TC_Flags.Items.Add(tab);
        }
    }

    private void AddConstList(EventLabelCollection list, TWork[] values)
    {
        WorkRows.Clear();
        TC_Const.Items.Clear();

        var labels = list.Work;
        if (labels.Count == 0)
        {
            TC_Const.IsVisible = false;
            var research = UiFactory.Label("TLP_Const_Research", MsgResearchRequired);
            research.Margin = new Thickness(20);
            research.SetForeColor(ColorUtilAvalonia.ColorWarn);
            GB_Constants.Content = research;
            return;
        }

        foreach (var group in labels.GroupBy(z => z.Type).OrderBy(z => z.Key))
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2, Margin = new Thickness(4) };
            var rows = new List<(string Search, Control Row)>();
            foreach (var entry in group)
            {
                var row = new WorkRow(this, entry, values[entry.Index]);
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
            var host = new DockPanel();
            DockPanel.SetDock(search, Dock.Top);
            host.Children.Add(search);
            host.Children.Add(scroll);

            var tab = new TabItem
            {
                Name = $"Tab_W{group.Key}",
                Header = Translator.TranslateEnum(group.Key, MainWindow.CurrentLanguage),
                Content = host,
            };
            TC_Const.Items.Add(tab);
        }
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

    private void ChangeCustomBool()
    {
        if (editing)
            return;
        editing = true;
        var index = (int)(NUD_Flag.Value ?? 0);
        Editor.Flags[index] = c_CustomFlag.IsChecked == true;
        if (FlagRows.TryGetValue(index, out var row))
            row.SetQuiet(c_CustomFlag.IsChecked == true);
        editing = false;
    }

    private void ChangeCustomFlag()
    {
        int flag = (int)(NUD_Flag.Value ?? 0);
        if ((uint)flag < Editor.Flags.Length)
            c_CustomFlag.IsChecked = Editor.Flags[flag];
    }

    private void ChangeConstantIndex()
    {
        var constants = Editor.Values;
        var index = CB_Stats.SelectedIndex;
        if ((uint)index < constants.Length)
            MT_Stat.Text = constants[index].ToString();
    }

    private void ChangeCustomConst()
    {
        if (editing)
            return;
        editing = true;
        var index = CB_Stats.SelectedIndex;
        var parse = TWork.TryParse(MT_Stat.Text, CultureInfo.CurrentCulture, out var value) ? value : TWork.Zero;
        if ((uint)index < Editor.Values.Length)
        {
            Editor.Values[index] = parse;
            if (WorkRows.TryGetValue(index, out var row))
                row.SetQuiet(parse);
        }
        editing = false;
    }

    /// <summary>One event flag row; writes straight into the workspace flag array.</summary>
    private sealed class FlagRow(EventFlagsWindowBase<TSave, TWork> owner, int index) : INotifyPropertyChanged
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
                owner.Editor.Flags[index] = value;
                if ((int)(owner.NUD_Flag.Value ?? 0) == index)
                    owner.c_CustomFlag.IsChecked = value;
            }
        }

        /// <summary>Updates the display without writing back (the value came from the workspace).</summary>
        public void SetQuiet(bool value)
        {
            quiet = true;
            IsSet = value;
            quiet = false;
        }
    }

    /// <summary>One event constant row: name, predefined value picker and raw value.</summary>
    private sealed class WorkRow
    {
        public Control View { get; }
        private readonly NumericUpDown Numeric;
        private readonly ComboBox Combo;
        private readonly List<ComboItem> Map;
        private bool updating;

        public WorkRow(EventFlagsWindowBase<TSave, TWork> owner, NamedEventWork entry, TWork value)
        {
            Map = entry.PredefinedValues.Select(z => new ComboItem(z.Name, z.Value)).ToList();
            Numeric = UiFactory.NumericUpDown($"NUD_W{entry.Index}", decimal.CreateChecked(TWork.MinValue), decimal.CreateChecked(TWork.MaxValue), 90);
            Combo = UiFactory.Combo($"CB_W{entry.Index}", 190);
            Combo.SetItems(Map);

            var label = UiFactory.Label($"L_W{entry.Index}", entry.Name, clickable: true);
            label.AttachClick(_ => Numeric.Value = 0);
            label.MinWidth = 240;

            var row = UiFactory.Row(label, Combo, Numeric);
            View = row;

            void ChangeConstValue()
            {
                if (updating)
                    return;
                updating = true;
                var current = TWork.CreateTruncating(Numeric.Value ?? 0);
                var raw = int.CreateTruncating(current);
                var (_, valueID) = Map.Find(z => z.Value == raw) ?? Map[0];
                // GetValue reports 0 for "nothing selected", so compare the item itself; otherwise value 0 never gets selected.
                if (Combo.GetSelectedItem()?.Value != valueID)
                    Combo.SetValue(valueID);

                owner.Editor.Values[entry.Index] = current;
                if (owner.CB_Stats.SelectedIndex == entry.Index)
                    owner.MT_Stat.Text = current.ToString();
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

            Numeric.Value = decimal.CreateChecked(value);
            if (Numeric.Value == 0)
                ChangeConstValue(); // setting the same value raises no event, so seed the picker explicitly
        }

        /// <summary>Updates the display without writing back.</summary>
        public void SetQuiet(TWork value)
        {
            updating = true;
            Numeric.Value = decimal.CreateChecked(value);
            var raw = int.CreateTruncating(value);
            var (_, valueID) = Map.Find(z => z.Value == raw) ?? Map[0];
            Combo.SetValue(valueID);
            updating = false;
        }
    }
}

/// <summary>
/// Event flag editor for Generation 3 through 7 saves (port of the WinForms <c>SAV_EventFlags</c>).
/// </summary>
public sealed class EventFlagsWindow(IEventFlag37 sav, GameVersion version)
    : EventFlagsWindowBase<IEventFlag37, ushort>(sav, version, "SAV_EventFlags");

/// <summary>
/// Event flag editor for Generation 2 saves, whose event constants are single bytes
/// (port of the WinForms <c>SAV_EventFlags2</c>).
/// </summary>
public sealed class EventFlags2Window(SAV2 sav)
    : EventFlagsWindowBase<SAV2, byte>(sav, sav.Version, "SAV_EventFlags2");
