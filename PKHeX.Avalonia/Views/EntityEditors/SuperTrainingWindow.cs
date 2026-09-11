using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Views.SaveEditors;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.EntityEditors;

/// <summary>
/// Super Training medal editor (port of the WinForms <c>SuperTrainingEditor</c>).
/// </summary>
public sealed class SuperTrainingWindow : SaveEditorWindow
{
    private readonly CheckBox[] Regular;
    private readonly CheckBox[] Distribution;
    private readonly ISuperTrainRegimen Entity;
    private const string PrefixCHK = "CHK_";

    private readonly Button B_None = UiFactory.Button("B_None", "Remove All");
    private readonly Button B_All = UiFactory.Button("B_All", "Give All");
    private readonly CheckBox CHK_SecretUnlocked = UiFactory.Check("CHK_SecretUnlocked", "Secret Training Unlocked");
    private readonly CheckBox CHK_SecretComplete = UiFactory.Check("CHK_SecretComplete", "Secret Training Complete");
    private readonly TextBlock L_Bag = UiFactory.Label("L_Bag", "Last Used Bag:");
    private readonly ComboBox CB_Bag = UiFactory.StringCombo("CB_Bag", 160);
    private readonly TextBlock L_Hits = UiFactory.Label("L_Hits", "Hits Remaining:");
    private readonly NumericUpDown NUD_BagHits = UiFactory.NumericUpDown("NUD_BagHits", 0, 255, 110);

    public SuperTrainingWindow(ISuperTrainRegimen pk) : base("SuperTrainingEditor", "Medal Editor")
    {
        Entity = pk;
        var TLP_SuperTrain = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        var TLP_DistSuperTrain = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        Regular = new CheckBox[SuperTrainRegimenExtensions.CountRegimen];
        for (int i = 0; i < Regular.Length; i++)
        {
            var name = SuperTrainRegimenExtensions.GetRegimenName(i);
            TLP_SuperTrain.Children.Add(Regular[i] = UiFactory.Check(PrefixCHK + name, name));
        }
        Distribution = new CheckBox[SuperTrainRegimenExtensions.CountRegimenDistribution];
        for (int i = 0; i < Distribution.Length; i++)
        {
            var name = SuperTrainRegimenExtensions.GetRegimenNameDistribution(i);
            TLP_DistSuperTrain.Children.Add(Distribution[i] = UiFactory.Check(PrefixCHK + name, name));
        }

        var bag = UiFactory.FormGrid(2);
        UiFactory.AddFormRow(bag, 0, L_Bag, CB_Bag);
        UiFactory.AddFormRow(bag, 1, L_Hits, NUD_BagHits);
        var right = UiFactory.Column(TLP_DistSuperTrain, CHK_SecretUnlocked, CHK_SecretComplete, bag);
        var body = UiFactory.Row(new ScrollViewer { Content = TLP_SuperTrain, MaxHeight = 400 }, right);
        body.Spacing = 16;
        right.VerticalAlignment = VerticalAlignment.Top;

        ButtonBar.Children.Insert(0, B_None);
        ButtonBar.Children.Insert(1, B_All);
        B_All.Click += (_, _) => B_All_Click();
        B_None.Click += (_, _) => B_None_Click();
        CHK_SecretUnlocked.IsCheckedChanged += (_, _) => CHK_Secret_CheckedChanged();

        SetBody(body);
        LoadRegimens();

        CHK_SecretUnlocked.IsChecked = Entity.SecretSuperTrainingUnlocked;
        CHK_SecretComplete.IsChecked = Entity.SuperTrainSupremelyTrained;

        if (pk is PK6 pk6)
        {
            var strings = GameInfo.Strings;
            var bags = strings.trainingbags.AsSpan();
            CB_Bag.Items.Clear();
            CB_Bag.Items.Add(strings.Species[0]); // (None)
            for (int i = 1; i < bags.Length; i++)
                CB_Bag.Items.Add(bags[i]);

            var bagIndex = pk6.TrainingBag;
            if (bagIndex >= bags.Length)
                bagIndex = 0;
            var hits = pk6.TrainingBagHits;
            if (hits > NUD_BagHits.Maximum)
                hits = 0;

            CB_Bag.SelectedIndex = bagIndex;
            NUD_BagHits.Value = hits;

            if (CHK_SecretUnlocked.IsChecked != true) // force update to disable checkboxes
                CHK_Secret_CheckedChanged();
        }
        else
        {
            L_Bag.IsVisible = CB_Bag.IsVisible = L_Hits.IsVisible = NUD_BagHits.IsVisible = false;
            CHK_SecretUnlocked.IsVisible = CHK_SecretComplete.IsVisible = false;
        }
    }

    private void LoadRegimens()
    {
        for (int i = 0; i < Regular.Length; i++)
            Regular[i].IsChecked = Entity.GetRegimenState(i);
        for (int i = 0; i < Distribution.Length; i++)
            Distribution[i].IsChecked = Entity.GetRegimenStateDistribution(i);
    }

    protected override void OnSave()
    {
        for (int i = 0; i < Regular.Length; i++)
            Entity.SetRegimenState(i, Regular[i].IsChecked == true);
        for (int i = 0; i < Distribution.Length; i++)
            Entity.SetRegimenStateDistribution(i, Distribution[i].IsChecked == true);

        if (Entity is PK6 pk6)
        {
            pk6.SecretSuperTrainingUnlocked = CHK_SecretUnlocked.IsChecked == true;
            pk6.SuperTrainSupremelyTrained = CHK_SecretComplete.IsChecked == true;
            pk6.TrainingBag = (byte)Math.Max(0, CB_Bag.SelectedIndex);
            pk6.TrainingBagHits = (byte)(NUD_BagHits.Value ?? 0);
        }
        else // clear flags if manually cleared
        {
            Entity.SecretSuperTrainingUnlocked &= CHK_SecretUnlocked.IsChecked == true;
            Entity.SuperTrainSupremelyTrained &= CHK_SecretComplete.IsChecked == true;
        }
        Close();
    }

    private void B_All_Click()
    {
        if (CHK_SecretUnlocked.IsChecked == true) // only give dist if Secret is Unlocked (None -> All -> All*)
        {
            foreach (var c in Distribution)
                c.IsChecked = true;
        }

        if (Entity is PK6)
        {
            CHK_SecretUnlocked.IsChecked = true;
            CHK_SecretComplete.IsChecked = true;
        }
        foreach (var c in Regular)
            c.IsChecked = true;
    }

    private void B_None_Click()
    {
        CHK_SecretUnlocked.IsChecked = false;
        CHK_SecretComplete.IsChecked = false;
        foreach (var c in Regular)
            c.IsChecked = false;
        foreach (var c in Distribution)
            c.IsChecked = false;
    }

    private void CHK_Secret_CheckedChanged()
    {
        if (Entity is not PK6)
            return;
        bool unlocked = CHK_SecretUnlocked.IsChecked == true;
        CHK_SecretComplete.IsChecked &= unlocked;
        CHK_SecretComplete.IsEnabled = unlocked;
        foreach (var c in Regular)
        {
            // CHK_SuperTrainN_...: rank digit at index 14
            var name = c.Name ?? string.Empty;
            if (name.Length <= 14 || name[14] < '4')
                continue;
            c.IsEnabled = unlocked;
            if (!unlocked)
                c.IsChecked = false;
        }
    }
}
