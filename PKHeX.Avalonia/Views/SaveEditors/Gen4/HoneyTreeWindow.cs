using System;
using System.Linq;
using Avalonia.Controls;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen4;

/// <summary>
/// Honey tree editor for Diamond/Pearl/Platinum (port of the WinForms <c>SAV_HoneyTree</c>).
/// </summary>
public sealed class HoneyTreeWindow : SaveEditorWindow
{
    private static readonly string[] TreeNames =
    [
        "Route 205, Floaroma Town side", "Route 205, Eterna City side", "Route 206", "Route 207", "Route 208", "Route 209",
        "Route 210, Solaceon Town side", "Route 210, Celestic Town side", "Route 211", "Route 212, Hearthome City side",
        "Route 212, Pastoria City side", "Route 213", "Route 214", "Route 215", "Route 218", "Route 221", "Route 222",
        "Valley Windworks", "Eterna Forest", "Fuego Ironworks", "Floaroma Meadow",
    ];

    private readonly SAV4Sinnoh Origin;
    private readonly SAV4Sinnoh SAV;
    private readonly byte[] MunchlaxTrees;
    private int entry;
    private bool loading;
    private HoneyTreeValue? Tree;

    private readonly TextBlock L_HoneyTree = UiFactory.Label("L_HoneyTree", "Honey Tree");
    private readonly ComboBox CB_TreeList = UiFactory.StringCombo("CB_TreeList", 260, TreeNames);
    private readonly TextBlock L_Munchlax = UiFactory.Label("L_Munchlax", "Munchlax Trees:");
    private readonly TextBlock L_Tree0 = UiFactory.Label("L_Tree0", string.Empty);
    private readonly TextBlock L_Time = UiFactory.Label("L_Time", "Time left (minutes)");
    private readonly NumericUpDown NUD_Time = UiFactory.NumericUpDown("NUD_Time", 0, 1080, 110);
    private readonly TextBlock L_Shake = UiFactory.Label("L_Shake", "Shake");
    private readonly NumericUpDown NUD_Shake = UiFactory.NumericUpDown("NUD_Shake", 0, 3, 90);
    private readonly TextBlock L_Group = UiFactory.Label("L_Group", "Group");
    private readonly NumericUpDown NUD_Group = UiFactory.NumericUpDown("NUD_Group", 0, 20, 90);
    private readonly TextBlock L_Slot = UiFactory.Label("L_Slot", "Slot");
    private readonly NumericUpDown NUD_Slot = UiFactory.NumericUpDown("NUD_Slot", 0, 5, 90);
    private readonly TextBlock L_Species = UiFactory.Label("L_Species", "Species");
    private readonly Button B_Catchable = UiFactory.Button("B_Catchable", "Make catchable");

    public HoneyTreeWindow(SAV4Sinnoh sav) : base("SAV_HoneyTree", "Honey Tree Editor")
    {
        SAV = (SAV4Sinnoh)(Origin = sav).Clone();

        // Get Munchlax tree for this savegame in screen
        MunchlaxTrees = new byte[4];
        HoneyTreeUtil.CalculateMunchlaxTrees(SAV.ID32, MunchlaxTrees);

        const string sep = "- ";
        L_Tree0.Text = string.Join(Environment.NewLine,
            sep + TreeNames[MunchlaxTrees[0]],
            sep + TreeNames[MunchlaxTrees[1]],
            sep + TreeNames[MunchlaxTrees[2]],
            sep + TreeNames[MunchlaxTrees[3]]);

        var info = UiFactory.FormGrid(5);
        UiFactory.AddFormRow(info, 0, L_Time, UiFactory.Row(NUD_Time, B_Catchable));
        UiFactory.AddFormRow(info, 1, L_Shake, NUD_Shake);
        UiFactory.AddFormRow(info, 2, L_Group, NUD_Group);
        UiFactory.AddFormRow(info, 3, L_Slot, NUD_Slot);
        UiFactory.AddFormRow(info, 4, L_Species, L_Species2);

        var body = UiFactory.Column(
            UiFactory.Row(L_HoneyTree, CB_TreeList),
            new GroupBoxView("GB_TreeInfo", "Tree Info", info),
            L_Munchlax, L_Tree0);
        SetBody(body);

        B_Catchable.Click += (_, _) => NUD_Time.Value = 1080;
        CB_TreeList.SelectionChanged += (_, _) => ChangeTree();
        foreach (var nud in new[] { NUD_Group, NUD_Slot })
            nud.ValueChanged += (_, _) => _ = ChangeGroupSlot();

        CB_TreeList.SelectedIndex = 0;
    }

    private readonly TextBlock L_Species2 = UiFactory.Label("L_Species2", string.Empty);

    private ushort TreeSpecies => SAV.GetHoneyTreeSpecies((int)(NUD_Group.Value ?? 0), (int)(NUD_Slot.Value ?? 0));

    private async System.Threading.Tasks.Task ChangeGroupSlot()
    {
        var species = TreeSpecies;
        L_Species2.Text = GetLabelText(species);

        if (loading)
            return;

        if (species == (int)Species.Munchlax && !MunchlaxTrees.Contains((byte)CB_TreeList.SelectedIndex))
            await AppDialogs.Alert(this, "Catching Munchlax in this tree will make it illegal for this savegame's TID16/SID16 combination.");
    }

    private static string GetLabelText(ushort species)
    {
        var str = GameInfo.Strings;
        var arr = str.specieslist;
        if (species != (int)Species.Silcoon)
            return arr[species];

        // Silcoon/Cascoon
        var games = str.gamelist;
        return $"{arr[species + 0]} ({games[(int)GameVersion.D]})" + Environment.NewLine +
               $"{arr[species + 2]} ({games[(int)GameVersion.P]})";
    }

    private void ChangeTree()
    {
        SaveTree();
        entry = Math.Max(0, CB_TreeList.SelectedIndex);
        ReadTree();
    }

    private void ReadTree()
    {
        loading = true;
        Tree = SAV.GetHoneyTree(entry);

        NUD_Time.Value = Math.Min(NUD_Time.Maximum, Tree.Time);
        NUD_Shake.Value = Math.Min(NUD_Shake.Maximum, Tree.Shake);
        NUD_Group.Value = Math.Min(NUD_Group.Maximum, Tree.Group);
        NUD_Slot.Value = Math.Min(NUD_Slot.Maximum, Tree.Slot);

        _ = ChangeGroupSlot();
        loading = false;
    }

    private void SaveTree()
    {
        if (Tree is null)
            return;

        Tree.Time = (uint)(NUD_Time.Value ?? 0);
        Tree.Shake = (int)(NUD_Shake.Value ?? 0);
        Tree.Group = (int)(NUD_Group.Value ?? 0);
        Tree.Slot = (int)(NUD_Slot.Value ?? 0);

        SAV.SetHoneyTree(Tree, entry);
    }

    protected override void OnSave()
    {
        SaveTree();
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
