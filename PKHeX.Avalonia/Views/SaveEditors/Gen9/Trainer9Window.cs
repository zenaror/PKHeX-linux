using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Views.EntityEditors;
using PKHeX.Core;
using PKHeX.Drawing;
using static PKHeX.Core.SaveBlockAccessor9SV;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen9;

/// <summary>
/// Trainer editor for Scarlet / Violet (port of the WinForms <c>SAV_Trainer9</c>).
/// </summary>
/// <remarks>
/// The profile picture and the two icons are DXT1 textures stored in their own blocks, so they are decoded
/// for display. The Blueberry tab only exists from save revision 2 (the Indigo Disk update) onwards.
/// </remarks>
public sealed class Trainer9Window : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV9SV SAV;
    private readonly bool Loading;
    private bool MapUpdated;

    private readonly TextBox TB_OTName = UiFactory.Text("TB_OTName", 12, 140);
    private readonly ComboBox CB_Gender = UiFactory.StringCombo("CB_Gender", 60);
    private readonly ComboBox CB_Game = UiFactory.StringCombo("CB_Game", 140);
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 140);
    private readonly TrainerIDView trainerID1 = new() { Name = "trainerID1" };
    private readonly NumericTextBox MT_Money = UiFactory.Numeric("MT_Money", 8, 110);
    private readonly Button B_MaxCash = UiFactory.Button("B_MaxCash", "Max");
    private readonly NumericTextBox MT_LP = UiFactory.Numeric("MT_LP", 8, 110);
    private readonly Button B_MaxLP = UiFactory.Button("B_MaxLP", "Max");
    private readonly NumericTextBox MT_BP = UiFactory.Numeric("MT_BP", 8, 110);
    private readonly Button B_MaxBP = UiFactory.Button("B_MaxBP", "Max");
    private readonly NumericTextBox MT_Hours = UiFactory.Numeric("MT_Hours", 5, 60);
    private readonly NumericTextBox MT_Minutes = UiFactory.Numeric("MT_Minutes", 2, 44);
    private readonly NumericTextBox MT_Seconds = UiFactory.Numeric("MT_Seconds", 2, 44);

    private readonly DatePicker CAL_AdventureStartDate = new() { Name = "CAL_AdventureStartDate" };
    private readonly DatePicker CAL_LastSavedDate = new() { Name = "CAL_LastSavedDate" };
    private readonly TimePicker CAL_LastSavedTime = new() { Name = "CAL_LastSavedTime" };

    private readonly NumericUpDown NUD_X = UiFactory.NumericUpDown("NUD_X", -100000, 100000, 110);
    private readonly NumericUpDown NUD_Y = UiFactory.NumericUpDown("NUD_Y", -100000, 100000, 110);
    private readonly NumericUpDown NUD_Z = UiFactory.NumericUpDown("NUD_Z", -100000, 100000, 110);
    private readonly NumericUpDown NUD_R = UiFactory.NumericUpDown("NUD_R", -360, 360, 110);
    private GroupBoxView GB_Map = null!;
    private readonly Button B_UnlockFlyLocations = UiFactory.Button("B_UnlockFlyLocations", "Unlock Fly Locations");

    private readonly Image P_CurrPhoto = UiFactory.Picture("P_CurrPhoto", 160);
    private readonly Image P_CurrIcon = UiFactory.Picture("P_CurrIcon", 96);
    private readonly Image P_InitialIcon = UiFactory.Picture("P_InitialIcon", 96);

    private readonly NumericUpDown NUD_BBQSolo = UiFactory.NumericUpDown("NUD_BBQSolo", 0, uint.MaxValue, 130);
    private readonly NumericUpDown NUD_BBQGroup = UiFactory.NumericUpDown("NUD_BBQGroup", 0, uint.MaxValue, 130);
    private readonly ComboBox CB_ThrowStyle = UiFactory.StringCombo("CB_ThrowStyle", 170);

    public Trainer9Window(SAV9SV sav) : base("SAV_Trainer9", "Trainer Data Editor")
    {
        SAV = (SAV9SV)(Origin = sav).Clone();
        Loading = true;

        var games = GameInfo.Strings.gamelist;
        CB_Game.Items.Add(games[(int)GameVersion.SL]);
        CB_Game.Items.Add(games[(int)GameVersion.VL]);
        foreach (var s in GameInfo.GenderSymbolUnicode.Take(2))
            CB_Gender.Items.Add(s);

        BuildLayout();
        GetImages();
        GetComboBoxes();
        GetTextBoxes();
        LoadMap();

        if (SAV.SaveRevision >= 2)
            LoadBlueberry();
        else
            RemoveTab("Tab_Blueberry");

        Loading = false;
    }

    private void BuildLayout()
    {
        var main = UiFactory.FormGrid(9);
        UiFactory.AddFormRow(main, 0, UiFactory.Label("L_TrainerName", "Trainer Name:"), UiFactory.Row(TB_OTName, CB_Gender));
        UiFactory.AddFormRow(main, 1, UiFactory.Label("L_Game", "Game:"), CB_Game);
        UiFactory.AddFormRow(main, 2, UiFactory.Label("L_TrainerID", "Trainer ID:"), trainerID1);
        UiFactory.AddFormRow(main, 3, UiFactory.Label("L_Money", "Money:"), UiFactory.Row(MT_Money, B_MaxCash));
        UiFactory.AddFormRow(main, 4, UiFactory.Label("L_LP", "League Points:"), UiFactory.Row(MT_LP, B_MaxLP));
        UiFactory.AddFormRow(main, 5, UiFactory.Label("L_Language", "Language:"), CB_Language);
        UiFactory.AddFormRow(main, 6, UiFactory.Label("L_PlayTime", "Play Time:"), UiFactory.Row(MT_Hours, MT_Minutes, MT_Seconds));
        UiFactory.AddFormRow(main, 7, UiFactory.Label("L_AdventureStart", "Adventure Started:"), CAL_AdventureStartDate);
        UiFactory.AddFormRow(main, 8, UiFactory.Label("L_LastSaved", "Last Saved:"), UiFactory.Row(CAL_LastSavedDate, CAL_LastSavedTime));

        GB_Map = new GroupBoxView("GB_Map", "Map Position", UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_X", "X:"), NUD_X, UiFactory.Label("L_Y", "Y:"), NUD_Y),
            UiFactory.Row(UiFactory.Label("L_Z", "Z:"), NUD_Z, UiFactory.Label("L_R", "R:"), NUD_R),
            B_UnlockFlyLocations));

        var photos = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        photos.Children.Add(UiFactory.Column(UiFactory.Label("L_CurrPhoto", "Profile"), P_CurrPhoto));
        photos.Children.Add(UiFactory.Column(UiFactory.Label("L_CurrIcon", "Icon"), P_CurrIcon));
        photos.Children.Add(UiFactory.Column(UiFactory.Label("L_InitialIcon", "Initial Icon"), P_InitialIcon));

        var overview = UiFactory.Column(main, GB_Map, photos);

        var blueberry = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(blueberry, 0, UiFactory.Label("L_BP", "Blueberry Points:"), UiFactory.Row(MT_BP, B_MaxBP));
        UiFactory.AddFormRow(blueberry, 1, UiFactory.Label("L_BBQSolo", "Quests (Solo):"), NUD_BBQSolo);
        UiFactory.AddFormRow(blueberry, 2, UiFactory.Label("L_BBQGroup", "Quests (Group):"), NUD_BBQGroup);
        UiFactory.AddFormRow(blueberry, 3, UiFactory.Label("L_ThrowStyle", "Throw Style:"), CB_ThrowStyle);

        var tabs = new TabControl { Name = "TC_Editor" };
        tabs.Items.Add(new TabItem { Name = "Tab_Overview", Header = "Overview", Content = new ScrollViewer { Content = overview, MaxHeight = 560 } });
        tabs.Items.Add(new TabItem { Name = "Tab_Blueberry", Header = "Blueberry", Content = blueberry });
        SetBody(tabs);

        B_MaxCash.Click += (_, _) => MT_Money.Text = SAV.MaxMoney.ToString();
        B_MaxLP.Click += (_, _) => MT_LP.Text = SAV.MaxMoney.ToString();
        B_MaxBP.Click += (_, _) => MT_BP.Text = SAV.MaxMoney.ToString();
        B_UnlockFlyLocations.Click += (_, _) => UnlockFlyLocations();
        foreach (var nud in new[] { NUD_X, NUD_Y, NUD_Z, NUD_R })
            nud.ValueChanged += (_, _) => { if (!Loading) MapUpdated = true; };
        TB_OTName.AttachClick(async mods =>
        {
            if (mods == KeyModifiers.Control)
                await TrashEditorWindow.ShowAsync(this, TB_OTName, SAV, SAV.MyStatus.OriginalTrainerTrash.ToArray());
        });
    }

    private void RemoveTab(string name)
    {
        if (Body is not TabControl tabs)
            return;
        var tab = tabs.Items.OfType<TabItem>().FirstOrDefault(z => z.Name == name);
        if (tab is not null)
            tabs.Items.Remove(tab);
    }

    #region Load

    /// <summary>Decodes the DXT1 textures the game stores for the profile picture and the two icons.</summary>
    private void GetImages()
    {
        var blocks = SAV.Blocks;
        P_CurrPhoto.Source = Decode(blocks, KPictureProfileCurrent, KPictureProfileCurrentWidth, KPictureProfileCurrentHeight);
        P_CurrIcon.Source = Decode(blocks, KPictureIconCurrent, KPictureIconCurrentWidth, KPictureIconCurrentHeight);
        P_InitialIcon.Source = Decode(blocks, KPictureIconInitial, KPictureIconInitialWidth, KPictureIconInitialHeight);

        static global::Avalonia.Media.Imaging.Bitmap? Decode(SCBlockAccessor blocks, uint kd, uint kw, uint kh)
        {
            try
            {
                var data = blocks.GetBlock(kd).Data;
                var width = (int)blocks.GetBlockValue<uint>(kw);
                var height = (int)blocks.GetBlockValue<uint>(kh);
                if (width <= 0 || height <= 0)
                    return null;
                var pixels = DXT1.Decompress(data, width, height);
                return ImageUtil.GetBitmap(pixels, width, height).ToAvaloniaBitmapAndDispose();
            }
            catch (Exception)
            {
                return null; // a save without a stored picture leaves the block empty
            }
        }
    }

    private void GetComboBoxes() => CB_Language.SetItems(GameInfo.LanguageDataSource(SAV.Generation, SAV.Context));

    private void GetTextBoxes()
    {
        CB_Game.SelectedIndex = Math.Clamp(SAV.Version - GameVersion.SL, 0, CB_Game.ItemCount - 1);
        CB_Gender.SelectedIndex = SAV.Gender;
        TB_OTName.Text = SAV.OT;
        trainerID1.LoadTrainer(SAV);
        MT_Money.Text = SAV.Money.ToString();
        MT_LP.Text = SAV.LeaguePoints.ToString();
        CB_Language.SetValue(SAV.Language);

        MT_Hours.Text = SAV.PlayedHours.ToString();
        MT_Minutes.Text = SAV.PlayedMinutes.ToString();
        MT_Seconds.Text = SAV.PlayedSeconds.ToString();

        CAL_AdventureStartDate.SelectedDate = UiFactory.ToOffset(SAV.EnrollmentDate.Timestamp);
        var saved = SAV.LastSaved.Timestamp;
        CAL_LastSavedDate.SelectedDate = UiFactory.ToOffset(saved);
        CAL_LastSavedTime.SelectedTime = saved.TimeOfDay;
    }

    private void LoadMap()
    {
        try
        {
            NUD_X.SetValueClamped((decimal)(double)SAV.X);
            NUD_Y.SetValueClamped((decimal)(double)SAV.Y);
            NUD_Z.SetValueClamped((decimal)(double)SAV.Z);
            NUD_R.SetValueClamped((decimal)(Math.Atan2(SAV.RZ, SAV.RW) * 360.0 / Math.PI));
        }
        catch (OverflowException)
        {
            GB_Map.IsEnabled = false; // coordinates outside the editable range
        }
    }

    private void LoadBlueberry()
    {
        var bbq = SAV.BlueberryQuestRecord;
        MT_BP.Text = SAV.BlueberryPoints.ToString();
        NUD_BBQSolo.SetValueClamped(bbq.QuestsDoneSolo);
        NUD_BBQGroup.SetValueClamped(bbq.QuestsDoneGroup);

        CB_ThrowStyle.Items.Clear();
        foreach (var s in Util.GetStringList("throw_styles", MainWindow.CurrentLanguage))
            CB_ThrowStyle.Items.Add(s);
        CB_ThrowStyle.SelectedIndex = Math.Clamp((int)SAV.ThrowStyle - 1, 0, Math.Max(0, CB_ThrowStyle.ItemCount - 1));
    }

    #endregion

    private void UnlockFlyLocations()
    {
        var accessor = SAV.Accessor;
        foreach (var hash in FlyHashes9.All)
        {
            if (accessor.TryGetBlock(hash, out var block))
                block.ChangeBooleanType(SCTypeCode.Bool2);
        }
        B_UnlockFlyLocations.IsEnabled = false;
    }

    #region Save

    private void SaveTrainerInfo()
    {
        SAV.Version = (GameVersion)(CB_Game.SelectedIndex + (byte)GameVersion.SL);
        SAV.Gender = (byte)Math.Max(0, CB_Gender.SelectedIndex);
        SAV.Money = Util.ToUInt32(MT_Money.Text ?? string.Empty);
        SAV.LeaguePoints = Util.ToUInt32(MT_LP.Text ?? string.Empty);
        SAV.Language = CB_Language.GetSelectedItem()?.Value ?? 0;
        trainerID1.SaveTrainer(SAV);

        if (SAV.OT != TB_OTName.Text) // only modify if changed, to preserve trash bytes
            SAV.OT = TB_OTName.Text ?? string.Empty;

        SAV.PlayedHours = (ushort)Util.ToUInt32(MT_Hours.Text ?? string.Empty);
        SAV.PlayedMinutes = (ushort)(Util.ToUInt32(MT_Minutes.Text ?? string.Empty) % 60);
        SAV.PlayedSeconds = (ushort)(Util.ToUInt32(MT_Seconds.Text ?? string.Empty) % 60);

        SAV.EnrollmentDate.Timestamp = CAL_AdventureStartDate.SelectedDate?.Date ?? SAV.EnrollmentDate.Timestamp;
        var savedDate = CAL_LastSavedDate.SelectedDate?.Date ?? SAV.LastSaved.Timestamp.Date;
        SAV.LastSaved.Timestamp = savedDate + (CAL_LastSavedTime.SelectedTime ?? TimeSpan.Zero);

        if (SAV.Blocks.TryGetBlock(KBlueberryPoints, out var block))
            block.SetValue(Util.ToUInt32(MT_BP.Text ?? string.Empty));
    }

    private void SaveMap()
    {
        if (!MapUpdated)
            return;
        SAV.SetCoordinates((float)(NUD_X.Value ?? 0), (float)(NUD_Y.Value ?? 0), (float)(NUD_Z.Value ?? 0));
        var angle = (double)(NUD_R.Value ?? 0) * Math.PI / 360.0;
        SAV.SetPlayerRotation(0, (float)Math.Sin(angle), 0, (float)Math.Cos(angle));
    }

    private void SaveBlueberry()
    {
        var bbq = SAV.BlueberryQuestRecord;
        SAV.BlueberryPoints = Util.ToUInt32(MT_BP.Text ?? string.Empty);
        bbq.QuestsDoneSolo = (uint)(NUD_BBQSolo.Value ?? 0);
        bbq.QuestsDoneGroup = (uint)(NUD_BBQGroup.Value ?? 0);
        SAV.ThrowStyle = (ThrowStyle9)(CB_ThrowStyle.SelectedIndex + 1);
    }

    #endregion

    protected override void OnSave()
    {
        SaveTrainerInfo();
        SaveMap();
        if (SAV.SaveRevision >= 2)
            SaveBlueberry();

        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
