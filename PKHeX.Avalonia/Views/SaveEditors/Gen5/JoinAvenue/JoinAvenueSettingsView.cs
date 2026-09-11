using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen5.JoinAvenue;

/// <summary>
/// The avenue's own settings and the remembered visiting players (port of <c>JoinAvenueSettingsEditor</c>).
/// </summary>
public sealed class JoinAvenueSettingsView : StackPanel
{
    private readonly TextBox TB_Name = UiFactory.Text("TB_Name", 20, 200);
    private readonly TextBox TB_Title = UiFactory.Text("TB_Title", 20, 200);
    private readonly NumericUpDown NUD_Experience = UiFactory.NumericUpDown("NUD_Experience", 0, uint.MaxValue, 140);
    private readonly NumericUpDown NUD_Rank = UiFactory.NumericUpDown("NUD_Rank", 0, ushort.MaxValue, 120);
    private readonly ComboBox CB_CeilingColor = UiFactory.Combo("CB_CeilingColor", 170);
    private readonly NumericUpDown NUD_Flags = UiFactory.NumericUpDown("NUD_Flags", 0, uint.MaxValue, 140);
    private readonly NumericUpDown NUD_PlayerCount = UiFactory.NumericUpDown("NUD_PlayerCount", 0, ushort.MaxValue, 120);
    private readonly NumericUpDown NUD_PlayerInsert = UiFactory.NumericUpDown("NUD_PlayerInsert", 0, ushort.MaxValue, 120);
    private readonly NumericUpDown NUD_Seed = UiFactory.NumericUpDown("NUD_Seed", 0, uint.MaxValue, 140);
    private readonly NumericUpDown NUD_PromotionDaysElapsed = UiFactory.NumericUpDown("NUD_PromotionDaysElapsed", 0, ushort.MaxValue, 120);
    private readonly CheckBox CHK_IsPromotionActive = UiFactory.Check("CHK_IsPromotionActive", "Promotion Active");
    private readonly ObservableCollection<PlayerRow> PlayerRows = [];

    public JoinAvenueSettingsView()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 10;

        var colors = Translator.GetEnumTranslation<JoinAvenueCeilingColor5>(MainWindow.CurrentLanguage)
            .Select((z, i) => new ComboItem(z, i)).ToList();
        CB_CeilingColor.SetItems(colors);

        var grid = UiFactory.FormGrid(11);
        int r = 0;
        Row(grid, r++, "L_Name", "Name:", TB_Name);
        Row(grid, r++, "L_Title", "Title:", TB_Title);
        Row(grid, r++, "L_Experience", "Experience:", NUD_Experience);
        Row(grid, r++, "L_Rank", "Rank:", NUD_Rank);
        Row(grid, r++, "L_CeilingColor", "Ceiling Color:", CB_CeilingColor);
        Row(grid, r++, "L_Flags", "Flags:", NUD_Flags);
        Row(grid, r++, "L_PlayerCount", "Player Count:", NUD_PlayerCount);
        Row(grid, r++, "L_PlayerInsert", "Insert Index:", NUD_PlayerInsert);
        Row(grid, r++, "L_Seed", "Seed:", NUD_Seed);
        Row(grid, r++, "L_PromotionDaysElapsed", "Promotion Days:", NUD_PromotionDaysElapsed);
        Row(grid, r, "L_IsPromotionActive", string.Empty, CHK_IsPromotionActive);

        for (int i = 0; i < JoinAvenueSettings5.CountVisitingPlayersRemembered; i++)
            PlayerRows.Add(new PlayerRow { Index = i + 1 });

        var dgv = new DataGrid
        {
            Name = "DGV_VisitingPlayerDatabase",
            ItemsSource = PlayerRows,
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            CanUserReorderColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Width = 280,
            Height = 460,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        dgv.Columns.Add(new DataGridTextColumn { Header = "#", Binding = new Binding(nameof(PlayerRow.Index)), IsReadOnly = true, Width = new DataGridLength(50) });
        dgv.Columns.Add(new DataGridTextColumn { Header = "TID", Binding = new Binding(nameof(PlayerRow.TID)) { Mode = BindingMode.TwoWay }, Width = new DataGridLength(100) });
        dgv.Columns.Add(new DataGridTextColumn { Header = "SID", Binding = new Binding(nameof(PlayerRow.SID)) { Mode = BindingMode.TwoWay }, Width = new DataGridLength(100) });

        Children.Add(grid);
        Children.Add(new GroupBoxView("GB_VisitingPlayers", "Visiting Players", dgv));
        return;

        static void Row(Grid g, int row, string name, string text, Control editor)
            => UiFactory.AddFormRow(g, row, UiFactory.Label(name, text), editor);
    }

    private sealed class PlayerRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private string _tid = "00000";
        private string _sid = "00000";

        public required int Index { get; init; }

        public string TID
        {
            get => _tid;
            set => Set(ref _tid, value, nameof(TID));
        }

        public string SID
        {
            get => _sid;
            set => Set(ref _sid, value, nameof(SID));
        }

        private void Set(ref string field, string value, string name)
        {
            if (field == value)
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public void LoadObject(JoinAvenueSettings5 settings)
    {
        TB_Name.Text = settings.Name;
        TB_Title.Text = settings.PlayerTitle;
        NUD_Experience.SetValueClamped(settings.Experience);
        NUD_Rank.SetValueClamped(settings.Rank);
        CB_CeilingColor.SetValue((int)settings.CeilingColor);
        NUD_Flags.SetValueClamped(settings.Flags);
        NUD_PlayerCount.SetValueClamped(settings.VisitingPlayerDatabaseCount);
        NUD_PlayerInsert.SetValueClamped(settings.VistiingPlayerDatabaseInsertIndex);
        NUD_Seed.SetValueClamped(settings.Seed);
        NUD_PromotionDaysElapsed.SetValueClamped(settings.PromotionDaysElapsed);
        CHK_IsPromotionActive.IsChecked = settings.IsPromotionActive;

        for (int i = 0; i < JoinAvenueSettings5.CountVisitingPlayersRemembered && i < PlayerRows.Count; i++)
        {
            var value = settings.GetVisitingPlayerTrainerID(i);
            PlayerRows[i].TID = ((ushort)value).ToString("00000");
            PlayerRows[i].SID = ((ushort)(value >> 16)).ToString("00000");
        }
    }

    public void SaveObject(JoinAvenueSettings5 settings)
    {
        settings.Name = TB_Name.Text ?? string.Empty;
        settings.PlayerTitle = TB_Title.Text ?? string.Empty;
        settings.Experience = (uint)(NUD_Experience.Value ?? 0);
        settings.Rank = (ushort)(NUD_Rank.Value ?? 0);
        settings.CeilingColor = (JoinAvenueCeilingColor5)(CB_CeilingColor.GetSelectedItem()?.Value ?? 0);
        settings.Flags = (uint)(NUD_Flags.Value ?? 0);
        settings.VisitingPlayerDatabaseCount = (ushort)(NUD_PlayerCount.Value ?? 0);
        settings.VistiingPlayerDatabaseInsertIndex = (ushort)(NUD_PlayerInsert.Value ?? 0);
        settings.Seed = (uint)(NUD_Seed.Value ?? 0);
        settings.PromotionDaysElapsed = (ushort)(NUD_PromotionDaysElapsed.Value ?? 0);
        settings.IsPromotionActive = CHK_IsPromotionActive.IsChecked == true;

        for (int i = 0; i < JoinAvenueSettings5.CountVisitingPlayersRemembered && i < PlayerRows.Count; i++)
        {
            var tid = ParseUInt16(PlayerRows[i].TID);
            var sid = ParseUInt16(PlayerRows[i].SID);
            settings.SetVisitingPlayerTrainerID(i, tid | ((uint)sid << 16));
        }
    }

    private static ushort ParseUInt16(string? value) => ushort.TryParse(value, out var result) ? result : (ushort)0;
}
