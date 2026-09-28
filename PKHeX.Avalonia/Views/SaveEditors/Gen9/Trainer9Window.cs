using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Drawing;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views.EntityEditors;
using PKHeX.Core;
using PKHeX.Drawing;
using SkiaSharp;
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

    private readonly RenderedString TB_OTName = UiFactory.Name("TB_OTName", 12, 140);
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
    private readonly Button B_UnlockFlyLocations = UiFactory.Button("B_UnlockFlyLocations", "Unlock All Fly Locations");
    private readonly Button B_CollectAllStakes = UiFactory.Button("B_CollectAllStakes", "Collect All Stakes");
    private readonly Button B_UnlockBikeUpgrades = UiFactory.Button("B_UnlockBikeUpgrades", "Unlock All Bike Upgrades");
    private readonly Button B_UnlockTMRecipes = UiFactory.Button("B_UnlockTMRecipes", "Unlock All TM Recipes");
    private readonly Button B_UnlockClothing = UiFactory.Button("B_UnlockClothing", "Unlock All Fashion");
    private readonly Button B_ActivateSnacksworthLegendaries = UiFactory.Button("B_ActivateSnacksworthLegendaries", "Activate Legendaries");
    private readonly Button B_UnlockCoaches = UiFactory.Button("B_UnlockCoaches", "Unlock All Coaches");
    private readonly Button B_UnlockThrowStyles = UiFactory.Button("B_UnlockThrowStyles", "Unlock All Throw Styles");

    // WinForms picture boxes: 362x210 and 90x90, both PictureBoxSizeMode.Zoom.
    private readonly Image P_CurrPhoto = Picture("P_CurrPhoto", 362, 210);
    private readonly Image P_CurrIcon = Picture("P_CurrIcon", 90, 90);
    private readonly Image P_InitialIcon = Picture("P_InitialIcon", 90, 90);
    /// <summary>Decoded pictures, kept so clicking one can write it to a file as WinForms does.</summary>
    private readonly SKBitmap?[] Pictures = new SKBitmap?[3];

    private readonly NumericUpDown NUD_BBQSolo = UiFactory.NumericUpDown("NUD_BBQSolo", 0, uint.MaxValue, 130);
    private readonly NumericUpDown NUD_BBQGroup = UiFactory.NumericUpDown("NUD_BBQGroup", 0, uint.MaxValue, 130);
    private readonly ComboBox CB_ThrowStyle = UiFactory.StringCombo("CB_ThrowStyle", 170);

    public Trainer9Window(SAV9SV sav) : base("SAV_Trainer9", "Trainer Data Editor")
    {
        SAV = (SAV9SV)(Origin = sav).Clone();

        if (!MainWindow.Unicode)
            TB_OTName.DisableInGameFont = true;
        Loading = true;

        var games = GameInfo.Strings.gamelist;
        CB_Game.Items.Add(games[(int)GameVersion.SL]);
        CB_Game.Items.Add(games[(int)GameVersion.VL]);
        foreach (var s in MainWindow.GenderSymbols.Take(2)) // m/f depending on unicode selection
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
        // Overview: the WinForms page is one column of rows (SAV_Trainer9.Designer.cs:666-694), with the gender
        // next to the name and the game combo carrying no label.
        var main = UiFactory.FormGrid(9);
        UiFactory.AddFormRow(main, 0, UiFactory.Label("L_TrainerName", "Trainer Name:"), UiFactory.Row(TB_OTName, CB_Gender));
        UiFactory.AddFormRow(main, 1, null, trainerID1);
        UiFactory.AddFormRow(main, 2, UiFactory.Label("L_Money", "$:"), UiFactory.Row(MT_Money, B_MaxCash));
        UiFactory.AddFormRow(main, 3, UiFactory.Label("L_LP", "LP:"), UiFactory.Row(MT_LP, B_MaxLP));
        UiFactory.AddFormRow(main, 4, null, CB_Game);
        UiFactory.AddFormRow(main, 5, UiFactory.Label("L_Language", "Language:"), CB_Language);
        UiFactory.AddFormRow(main, 6, UiFactory.Label("L_Hours", "Hrs:"), UiFactory.Row(
            MT_Hours, UiFactory.Label("L_Minutes", "Min:"), MT_Minutes, UiFactory.Label("L_Seconds", "Sec:"), MT_Seconds));
        UiFactory.AddFormRow(main, 7, UiFactory.Label("L_Started", "Game Started:"), CAL_AdventureStartDate);
        UiFactory.AddFormRow(main, 8, UiFactory.Label("L_LastSaved", "Last Saved:"), UiFactory.Row(CAL_LastSavedDate, CAL_LastSavedTime));
        var overview = UiFactory.Column(main);

        // Misc tab: the map group on the left, the unlock buttons on the right (Designer:796-801).
        var map = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(map, 0, UiFactory.Label("L_X", "X Coordinate:"), NUD_X);
        UiFactory.AddFormRow(map, 1, UiFactory.Label("L_Z", "Z Coordinate:"), NUD_Z);
        UiFactory.AddFormRow(map, 2, UiFactory.Label("L_Y", "Y Coordinate:"), NUD_Y);
        UiFactory.AddFormRow(map, 3, UiFactory.Label("L_R", "Rotation:"), NUD_R);
        GB_Map = new GroupBoxView("GB_Map", "Map Position", map);
        GB_Map.VerticalAlignment = VerticalAlignment.Top;
        var unlocks = UiFactory.Column(B_UnlockFlyLocations, B_CollectAllStakes, B_UnlockBikeUpgrades, B_UnlockTMRecipes, B_UnlockClothing);
        unlocks.VerticalAlignment = VerticalAlignment.Top;
        var misc = UiFactory.Row(GB_Map, unlocks);
        misc.Spacing = 10;

        // Images tab: the profile photo, and the two icons beside it. Clicking one writes it to a file.
        var photos = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        photos.Children.Add(P_CurrPhoto);
        photos.Children.Add(UiFactory.Column(P_CurrIcon, P_InitialIcon));

        var bbq = UiFactory.FormGrid(3);
        UiFactory.AddFormRow(bbq, 0, UiFactory.Label("L_BP", "BP:"), UiFactory.Row(MT_BP, B_MaxBP));
        UiFactory.AddFormRow(bbq, 1, UiFactory.Label("L_BBQSolo", "Solo Quests:"), NUD_BBQSolo);
        UiFactory.AddFormRow(bbq, 2, UiFactory.Label("L_BBQGroup", "Group Quests:"), NUD_BBQGroup);
        var bbqLeft = UiFactory.Column(
            new GroupBoxView("GB_BBQ", "BBQ", bbq),
            UiFactory.Row(UiFactory.Label("L_ThrowStyle", "Throw Style:"), CB_ThrowStyle));
        var bbqRight = UiFactory.Column(B_ActivateSnacksworthLegendaries, B_UnlockCoaches, B_UnlockThrowStyles);
        bbqLeft.VerticalAlignment = bbqRight.VerticalAlignment = VerticalAlignment.Top;
        var blueberry = UiFactory.Row(bbqLeft, bbqRight);
        blueberry.Spacing = 10;

        var tabs = new TabControl { Name = "TC_Editor" };
        tabs.Items.Add(new TabItem { Name = "Tab_Overview", Header = "Overview", Content = new ScrollViewer { Content = overview, MaxHeight = 560 } });
        tabs.Items.Add(new TabItem { Name = "Tab_MiscValues", Header = "Misc", Content = new ScrollViewer { Content = misc, MaxHeight = 560 } });
        tabs.Items.Add(new TabItem { Name = "Tab_Images", Header = "Images", Content = new ScrollViewer { Content = photos, MaxHeight = 560 } });
        tabs.Items.Add(new TabItem { Name = "Tab_Blueberry", Header = "Blueberry", Content = blueberry });
        SetBody(tabs);

        B_MaxCash.Click += (_, _) => MT_Money.Text = SAV.MaxMoney.ToString();
        B_MaxLP.Click += (_, _) => MT_LP.Text = SAV.MaxMoney.ToString();
        B_MaxBP.Click += (_, _) => MT_BP.Text = SAV.MaxMoney.ToString();
        B_UnlockFlyLocations.Click += (_, _) => UnlockFlyLocations();
        B_CollectAllStakes.Click += (_, _) => { SAV.CollectAllStakes(); B_CollectAllStakes.IsEnabled = false; };
        B_UnlockTMRecipes.Click += (_, _) => { SAV.UnlockAllTMRecipes(); B_UnlockTMRecipes.IsEnabled = false; };
        B_UnlockBikeUpgrades.Click += (_, _) => UnlockBikeUpgrades();
        B_UnlockClothing.Click += (_, _) => { PlayerFashionUnlock9.UnlockBase(SAV.Accessor, SAV.Gender); B_UnlockClothing.IsEnabled = false; };
        B_ActivateSnacksworthLegendaries.Click += (_, _) => { SAV.ActivateSnacksworthLegendaries(); B_ActivateSnacksworthLegendaries.IsEnabled = false; };
        B_UnlockCoaches.Click += (_, _) => { SAV.UnlockAllCoaches(); B_UnlockCoaches.IsEnabled = false; };
        B_UnlockThrowStyles.Click += (_, _) => { SAV.UnlockAllThrowStyles(); B_UnlockThrowStyles.IsEnabled = false; };
        P_CurrPhoto.AttachClick(async _ => await SaveImage(0, "current_photo"));
        P_CurrIcon.AttachClick(async _ => await SaveImage(1, "current_icon"));
        P_InitialIcon.AttachClick(async _ => await SaveImage(2, "initial_icon"));
        Closed += (_, _) =>
        {
            foreach (var img in Pictures)
                img?.Dispose();
        };
        foreach (var nud in new[] { NUD_X, NUD_Y, NUD_Z, NUD_R })
            nud.ValueChanged += (_, _) => { if (!Loading) MapUpdated = true; };
        TB_OTName.AttachClick(async mods =>
        {
            if (mods == KeyModifiers.Control)
            {
                var trash = await TrashEditorWindow.ShowAsync(this, TB_OTName, SAV, SAV.MyStatus.OriginalTrainerTrash.ToArray());
                trash?.CopyTo(SAV.MyStatus.OriginalTrainerTrash); // WinForms writes the edited bytes back into the save
            }
        });
    }

    private static Image Picture(string name, double width, double height) => new()
    {
        Name = name,
        Width = width,
        Height = height,
        Stretch = global::Avalonia.Media.Stretch.Uniform, // WinForms PictureBoxSizeMode.Zoom
        Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand),
    };

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
        Pictures[0] = Decode(blocks, KPictureProfileCurrent, KPictureProfileCurrentWidth, KPictureProfileCurrentHeight);
        Pictures[1] = Decode(blocks, KPictureIconCurrent, KPictureIconCurrentWidth, KPictureIconCurrentHeight);
        Pictures[2] = Decode(blocks, KPictureIconInitial, KPictureIconInitialWidth, KPictureIconInitialHeight);
        P_CurrPhoto.Source = Pictures[0]?.ToAvaloniaBitmap();
        P_CurrIcon.Source = Pictures[1]?.ToAvaloniaBitmap();
        P_InitialIcon.Source = Pictures[2]?.ToAvaloniaBitmap();

        static SKBitmap? Decode(SCBlockAccessor blocks, uint kd, uint kw, uint kh)
        {
            try
            {
                var data = blocks.GetBlock(kd).Data;
                var width = (int)blocks.GetBlockValue<uint>(kw);
                var height = (int)blocks.GetBlockValue<uint>(kh);
                if (width <= 0 || height <= 0)
                    return null;
                var pixels = DXT1.Decompress(data, width, height);
                return ImageUtil.GetBitmap(pixels, width, height);
            }
            catch (Exception)
            {
                return null; // a save without a stored picture leaves the block empty
            }
        }
    }

    /// <summary>
    /// Writes one of the stored pictures to a file, as clicking it does in WinForms.
    /// </summary>
    private async Task SaveImage(int index, string name)
    {
        if (Pictures[index] is { } img)
            await ImageExport.SaveDialog(this, img, name);
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

    /// <summary>The ride upgrades WinForms unlocks; the flight block is missing from base and DLC1 saves.</summary>
    private void UnlockBikeUpgrades()
    {
        ReadOnlySpan<string> blocks =
        [
            "FSYS_RIDE_DASH_ENABLE",
            "FSYS_RIDE_SWIM_ENABLE",
            "FSYS_RIDE_HIJUMP_ENABLE",
            "FSYS_RIDE_GLIDE_ENABLE",
            "FSYS_RIDE_CLIMB_ENABLE",
        ];

        var accessor = SAV.Accessor;
        foreach (var block in blocks)
            accessor.GetBlock(block).ChangeBooleanType(SCTypeCode.Bool2);
        if (accessor.TryGetBlock("FSYS_RIDE_FLIGHT_ENABLE", out var fly))
            fly.ChangeBooleanType(SCTypeCode.Bool2);
        B_UnlockBikeUpgrades.IsEnabled = false;
    }

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
