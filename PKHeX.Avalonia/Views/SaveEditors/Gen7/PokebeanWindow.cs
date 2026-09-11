using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Data;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen7;

/// <summary>
/// Poké Bean editor (port of the WinForms <c>SAV_Pokebean</c>).
/// </summary>
public sealed class PokebeanWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV7 SAV;
    private readonly ObservableCollection<BeanRow> Rows = [];
    private readonly DataGrid dgv = new() { AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column, IsReadOnly = false, CanUserSortColumns = false, Height = 420 };
    private readonly Button B_All = UiFactory.Button("B_All", "All");
    private readonly Button B_None = UiFactory.Button("B_None", "None");

    public PokebeanWindow(SAV7 sav) : base("SAV_Pokebean", "Poké Bean Editor")
    {
        SAV = (SAV7)(Origin = sav).Clone();

        dgv.Columns.Add(new DataGridTextColumn { Header = "Slot", Binding = new Binding(nameof(BeanRow.Name)), IsReadOnly = true, Width = new DataGridLength(220) });
        dgv.Columns.Add(new DataGridTextColumn { Header = "Count", Binding = new Binding(nameof(BeanRow.Count)) { Mode = BindingMode.TwoWay }, Width = new DataGridLength(80) });
        dgv.ItemsSource = Rows;
        SetBody(UiFactory.Column(dgv, UiFactory.Row(B_All, B_None)));

        B_All.Click += (_, _) => { SAV.ResortSave.FillBeans(); LoadValues(); };
        B_None.Click += (_, _) => { SAV.ResortSave.ClearBeans(); LoadValues(); };

        LoadValues();
    }

    private void LoadValues()
    {
        Rows.Clear();
        var names = ResortSave7.GetBeanIndexNames();
        var beans = SAV.ResortSave.GetBeans();
        for (int i = 0; i < beans.Length; i++)
            Rows.Add(new BeanRow { Name = names[i], Count = beans[i].ToString() });
    }

    protected override void OnSave()
    {
        var beans = SAV.ResortSave.GetBeans();
        for (int i = 0; i < beans.Length && i < Rows.Count; i++)
        {
            var count = int.TryParse(Rows[i].Count, out var val) ? val : 0;
            beans[i] = (byte)Math.Min(byte.MaxValue, count);
        }
        Origin.CopyChangesFrom(SAV);
        Close();
    }

    private sealed class BeanRow
    {
        public required string Name { get; init; }
        public required string Count { get; set; }
    }
}
