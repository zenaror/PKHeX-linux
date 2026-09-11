using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Views.SaveEditors;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.EntityEditors;

/// <summary>
/// Move Shop purchase/mastery flag editor (port of the WinForms <c>MoveShopEditor</c>).
/// </summary>
public sealed class MoveShopWindow : SaveEditorWindow
{
    private readonly IMoveShop8 Shop;
    private readonly IMoveShop8Mastery Master;
    private readonly PKM Entity;
    private readonly FlagRowList dgv = new("Purchased", "Mastered", flagsFirst: false);
    private readonly Button B_None = UiFactory.Button("B_None", "Remove All");
    private readonly Button B_All = UiFactory.Button("B_All", "Give All");
    private const int Bias = 1;

    public MoveShopWindow(IMoveShop8 s, IMoveShop8Mastery m, PKM pk) : base("MoveShopEditor", "Move Shop Editor")
    {
        Shop = s;
        Master = m;
        Entity = pk;
        SizeToContent = SizeToContent.Manual;
        CanResize = true;
        Width = 480;
        Height = 440;

        ButtonBar.Children.Insert(0, B_None);
        ButtonBar.Children.Insert(1, B_All);
        B_All.Click += (_, _) => B_All_Click();
        B_None.Click += (_, _) => { Save(); Shop.ClearMoveShopFlags(); Close(); };

        PopulateRecords();
        SetBody(dgv);
    }

    private void PopulateRecords()
    {
        var names = GameInfo.Strings.Move;
        var indexes = Shop.Permit.RecordPermitIndexes;
        for (int i = 0; i < indexes.Length; i++)
        {
            var isValid = Shop.Permit.IsRecordPermitted(i);
            var move = indexes[i];
            var type = MoveInfo.GetType(move, Entity.Context);
            dgv.Rows.Add(new FlagRow
            {
                RecordIndex = i,
                IndexText = $"{i + Bias:00}",
                Type = type,
                TypeIcon = FlagRowList.GetTypeIcon(type),
                Name = names[move],
                SortKey = type.ToString("00") + (isValid ? 0 : 1) + names[move], // type -> valid -> name sorting
                FlagBrush = isValid ? ColorUtilAvalonia.ColorValid.ToBrush() : Brushes.Transparent,
                HasFlag = Shop.GetPurchasedRecordFlag(i),
                HasFlag2 = Master.GetMasteredRecordFlag(i),
            });
        }
    }

    protected override void OnSave()
    {
        Save();
        Close();
    }

    private void Save()
    {
        foreach (var row in dgv.Rows)
        {
            Shop.SetPurchasedRecordFlag(row.RecordIndex, row.HasFlag);
            Master.SetMasteredRecordFlag(row.RecordIndex, row.HasFlag2);
        }
    }

    private void B_All_Click()
    {
        Save();
        switch (MainWindow.CurrentModifiers)
        {
            case KeyModifiers.Shift:
                Master.SetPurchasedFlagsAll(Entity);
                Master.SetMoveShopFlagsAll(Entity);
                break;
            case KeyModifiers.Control:
                Shop.ClearMoveShopFlags();
                Master.SetMoveShopFlags(Entity);
                break;
            default:
                Master.SetMoveShopFlags(Entity);
                break;
        }
        Close();
    }
}
