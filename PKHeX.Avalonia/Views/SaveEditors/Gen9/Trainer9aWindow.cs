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
using static PKHeX.Core.SaveBlockAccessor9ZA;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen9;

/// <summary>
/// Trainer editor for Legends: Z-A (port of the WinForms <c>SAV_Trainer9a</c>).
/// </summary>
/// <remarks>
/// Holds the Royale ticket points, the three stored pictures, and, from save revision 1 on, the DLC tab with
/// the Hyperspace survey points and street name. The two collect buttons walk the field item blocks.
/// </remarks>
public sealed class Trainer9aWindow : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV9ZA SAV;
    private readonly bool Loading;
    private bool MapUpdated;

    private readonly TextBox TB_OTName = UiFactory.Text("TB_OTName", 12, 140);
    private readonly GenderToggleView CB_Gender = new() { Name = "CB_Gender" };
    private readonly ComboBox CB_Language = UiFactory.Combo("CB_Language", 140);
    private readonly TrainerIDView trainerID1 = new() { Name = "trainerID1" };
    private readonly NumericTextBox MT_Money = UiFactory.Numeric("MT_Money", 8, 110);
    private readonly Button B_MaxCash = UiFactory.Button("B_MaxCash", "Max");
    private readonly NumericTextBox MT_Hours = UiFactory.Numeric("MT_Hours", 5, 60);
    private readonly NumericTextBox MT_Minutes = UiFactory.Numeric("MT_Minutes", 2, 44);
    private readonly NumericTextBox MT_Seconds = UiFactory.Numeric("MT_Seconds", 2, 44);
    private readonly DatePicker CAL_LastSavedDate = new() { Name = "CAL_LastSavedDate" };
    private readonly TimePicker CAL_LastSavedTime = new() { Name = "CAL_LastSavedTime" };

    private readonly NumericTextBox MT_RoyaleRegular = UiFactory.Numeric("MT_RoyaleRegular", 7, 110);
    private readonly Button B_RoyaleRegularMax = UiFactory.Button("B_RoyaleRegularMax", "Max");
    private readonly NumericTextBox MT_RoyaleInfinite = UiFactory.Numeric("MT_RoyaleInfinite", 7, 110);
    private readonly Button B_RoyaleInfiniteMax = UiFactory.Button("B_RoyaleInfiniteMax", "Max");

    private readonly TextBox TB_Map = UiFactory.Text("TB_Map", 40, 200);
    private readonly NumericUpDown NUD_X = UiFactory.NumericUpDown("NUD_X", -100000, 100000, 110);
    private readonly NumericUpDown NUD_Y = UiFactory.NumericUpDown("NUD_Y", -100000, 100000, 110);
    private readonly NumericUpDown NUD_Z = UiFactory.NumericUpDown("NUD_Z", -100000, 100000, 110);
    private readonly NumericUpDown NUD_R = UiFactory.NumericUpDown("NUD_R", -360, 360, 110);
    private GroupBoxView GB_Map = null!;

    private readonly Image P_Picture1 = UiFactory.Picture("P_Picture1", 160);
    private readonly Image P_Picture2 = UiFactory.Picture("P_Picture2", 160);
    private readonly Image P_Picture3 = UiFactory.Picture("P_Picture3", 160);

    private readonly NumericTextBox MT_HyperspaceSurveyPoints = UiFactory.Numeric("MT_HyperspaceSurveyPoints", 7, 110);
    private readonly Button B_HyperspaceSurveyPoints = UiFactory.Button("B_HyperspaceSurveyPoints", "Max");
    private readonly TextBox TB_StreetName = UiFactory.Text("TB_StreetName", 18, 220);

    private readonly Button B_CollectTechnicalMachines = UiFactory.Button("B_CollectTechnicalMachines", "Collect all TMs");
    private readonly Button B_CollectScrews = UiFactory.Button("B_CollectScrews", "Collect all Screws");

    public Trainer9aWindow(SAV9ZA sav) : base("SAV_Trainer9a", "Trainer Data Editor")
    {
        SAV = (SAV9ZA)(Origin = sav).Clone();
        Loading = true;

        BuildLayout();
        GetImages();
        CB_Language.SetItems(GameInfo.LanguageDataSource(SAV.Generation, SAV.Context));
        GetTextBoxes();
        GetDLC();
        LoadMap();

        Loading = false;

        B_MaxCash.Click += (_, _) => MT_Money.Text = SAV.MaxMoney.ToString();
        B_RoyaleRegularMax.Click += (_, _) => MT_RoyaleRegular.Text = 310_000.ToString();
        B_RoyaleInfiniteMax.Click += (_, _) => MT_RoyaleInfinite.Text = 50_000.ToString();
        B_HyperspaceSurveyPoints.Click += (_, _) => MT_HyperspaceSurveyPoints.Text = 100_000.ToString();
        B_CollectTechnicalMachines.Click += async (_, _) => await CollectTechnicalMachines();
        B_CollectScrews.Click += async (_, _) => await CollectScrews();
        foreach (var nud in new[] { NUD_X, NUD_Y, NUD_Z, NUD_R })
            nud.ValueChanged += (_, _) => { if (!Loading) MapUpdated = true; };
        TB_Map.OnTextChanged(_ => { if (!Loading) MapUpdated = true; });
        TB_OTName.AttachClick(async mods =>
        {
            if (mods == KeyModifiers.Control)
                await TrashEditorWindow.ShowAsync(this, TB_OTName, SAV, SAV.MyStatus.OriginalTrainerTrash.ToArray());
        });
    }

    #region Layout

    private void BuildLayout()
    {
        var main = UiFactory.FormGrid(8);
        UiFactory.AddFormRow(main, 0, UiFactory.Label("L_TrainerName", "Trainer Name:"), UiFactory.Row(TB_OTName, CB_Gender));
        UiFactory.AddFormRow(main, 1, UiFactory.Label("L_TrainerID", "Trainer ID:"), trainerID1);
        UiFactory.AddFormRow(main, 2, UiFactory.Label("L_Money", "Money:"), UiFactory.Row(MT_Money, B_MaxCash));
        UiFactory.AddFormRow(main, 3, UiFactory.Label("L_Language", "Language:"), CB_Language);
        UiFactory.AddFormRow(main, 4, UiFactory.Label("L_PlayTime", "Play Time:"), UiFactory.Row(MT_Hours, MT_Minutes, MT_Seconds));
        UiFactory.AddFormRow(main, 5, UiFactory.Label("L_LastSaved", "Last Saved:"), UiFactory.Row(CAL_LastSavedDate, CAL_LastSavedTime));
        UiFactory.AddFormRow(main, 6, UiFactory.Label("L_RoyaleRegular", "Royale Points:"), UiFactory.Row(MT_RoyaleRegular, B_RoyaleRegularMax));
        UiFactory.AddFormRow(main, 7, UiFactory.Label("L_RoyaleInfinite", "Royale (Infinite):"), UiFactory.Row(MT_RoyaleInfinite, B_RoyaleInfiniteMax));

        GB_Map = new GroupBoxView("GB_Map", "Map Position", UiFactory.Column(
            UiFactory.Row(UiFactory.Label("L_Map", "Map:"), TB_Map),
            UiFactory.Row(UiFactory.Label("L_X", "X:"), NUD_X, UiFactory.Label("L_Y", "Y:"), NUD_Y),
            UiFactory.Row(UiFactory.Label("L_Z", "Z:"), NUD_Z, UiFactory.Label("L_R", "R:"), NUD_R)));

        var overview = UiFactory.Column(main, GB_Map, UiFactory.Row(B_CollectTechnicalMachines, B_CollectScrews));

        var images = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        images.Children.Add(P_Picture1);
        images.Children.Add(P_Picture2);
        images.Children.Add(P_Picture3);

        var dlc = UiFactory.FormGrid(2);
        UiFactory.AddFormRow(dlc, 0, UiFactory.Label("L_HyperspaceSurveyPoints", "Survey Points:"), UiFactory.Row(MT_HyperspaceSurveyPoints, B_HyperspaceSurveyPoints));
        UiFactory.AddFormRow(dlc, 1, UiFactory.Label("L_StreetName", "Street Name:"), TB_StreetName);

        var tabs = new TabControl { Name = "TC_Editor" };
        tabs.Items.Add(new TabItem { Name = "Tab_Overview", Header = "Overview", Content = new ScrollViewer { Content = overview, MaxHeight = 540 } });
        tabs.Items.Add(new TabItem { Name = "Tab_Images", Header = "Images", Content = images });
        tabs.Items.Add(new TabItem { Name = "Tab_DLC", Header = "DLC", Content = dlc });
        SetBody(tabs);
    }

    private void RemoveTab(string name)
    {
        if (Body is not TabControl tabs)
            return;
        var tab = tabs.Items.OfType<TabItem>().FirstOrDefault(z => z.Name == name);
        if (tab is not null)
            tabs.Items.Remove(tab);
    }

    #endregion

    #region Load

    /// <summary>Decodes the three DXT1 pictures the save stores; the tab is dropped when none exist.</summary>
    private void GetImages()
    {
        var blocks = SAV.Blocks;
        var any = false;
        any |= SetImage(P_Picture1, Decode(blocks, KPictureCurrentData, KPictureCurrentWidth, KPictureCurrentHeight));
        any |= SetImage(P_Picture2, Decode(blocks, KPictureSBCData, KPictureSBCWidth, KPictureSBCHeight));
        any |= SetImage(P_Picture3, Decode(blocks, KPictureInitialData, KPictureInitialWidth, KPictureInitialHeight));
        if (!any)
            RemoveTab("Tab_Images");
        return;

        static global::Avalonia.Media.Imaging.Bitmap? Decode(SCBlockAccessor blocks, uint kd, uint kw, uint kh)
        {
            var width = (int)blocks.GetBlockValue<uint>(kw);
            var height = (int)blocks.GetBlockValue<uint>(kh);
            if (width == 0 && height == 0)
                return null; // no picture stored
            var data = blocks.GetBlock(kd).Data;
            var pixels = DXT1.Decompress(data, width, height);
            return ImageUtil.GetBitmap(pixels, width, height).ToAvaloniaBitmapAndDispose();
        }

        static bool SetImage(Image pb, global::Avalonia.Media.Imaging.Bitmap? img)
        {
            if (img is null)
            {
                pb.IsVisible = false;
                return false;
            }
            pb.Width = img.PixelSize.Width;
            pb.Height = img.PixelSize.Height;
            pb.Source = img;
            return true;
        }
    }

    private void GetTextBoxes()
    {
        CB_Gender.Gender = SAV.Gender;
        TB_OTName.Text = SAV.OT;
        trainerID1.LoadTrainer(SAV);
        MT_Money.Text = SAV.Money.ToString();
        CB_Language.SetValue(SAV.Language);

        MT_Hours.Text = SAV.PlayedHours.ToString();
        MT_Minutes.Text = SAV.PlayedMinutes.ToString();
        MT_Seconds.Text = SAV.PlayedSeconds.ToString();

        var saved = SAV.LastSaved.Timestamp;
        CAL_LastSavedDate.SelectedDate = UiFactory.ToOffset(saved);
        CAL_LastSavedTime.SelectedTime = saved.TimeOfDay;

        MT_RoyaleRegular.Text = SAV.TicketPointsRoyale.ToString();
        MT_RoyaleInfinite.Text = SAV.TicketPointsRoyaleInfinite.ToString();
    }

    private void GetDLC()
    {
        if (SAV.SaveRevision == 0)
        {
            RemoveTab("Tab_DLC");
            return;
        }
        MT_HyperspaceSurveyPoints.Text = SAV.GetValue<uint>(KHyperspaceSurveyPoints).ToString();
        TB_StreetName.Text = SAV.GetString(SAV.Blocks.GetBlock(KStreetName).Data);
    }

    private void LoadMap()
    {
        try
        {
            NUD_X.SetValueClamped((decimal)(double)SAV.Coordinates.X);
            NUD_Y.SetValueClamped((decimal)(double)SAV.Coordinates.Y);
            NUD_Z.SetValueClamped((decimal)(double)SAV.Coordinates.Z);
            NUD_R.SetValueClamped((decimal)SAV.Coordinates.Rotation);
            TB_Map.Text = SAV.Coordinates.Map;
        }
        catch (OverflowException)
        {
            GB_Map.IsEnabled = false; // coordinates outside the editable range
        }
    }

    #endregion

    #region Commands

    private async Task CollectTechnicalMachines()
    {
        var count = TechnicalMachine9a.SetAllTechnicalMachines(SAV, true);
        await AppDialogs.Alert(this, count == 0
            ? "All Technical Machines have already been collected."
            : $"Collected Technical Machines x{count}!");
    }

    private async Task CollectScrews()
    {
        var mods = MainWindow.CurrentModifiers;
        var itemName = GameInfo.Strings.Item[ColorfulScrew9a.ColorfulScrewItemIndex];

        if (mods == (KeyModifiers.Alt | KeyModifiers.Control | KeyModifiers.Shift))
        {
            ColorfulScrew9a.SetAllScrews(SAV);
            return;
        }

        // Alt copies the remaining locations, for collecting them manually in game.
        if (mods == KeyModifiers.Alt)
        {
            var uncollected = ColorfulScrew9a.GetScrewLocations(SAV.Blocks.FieldItems);
            var msg = string.Join(Environment.NewLine, uncollected.Select(s => $"{s.FieldItem} at ({s.Point.X}, {s.Point.Y}, {s.Point.Z})"));
            await ClipboardService.SetText(this, msg);
            await AppDialogs.Alert(this, $"Remaining {itemName} locations copied to clipboard.");
            return;
        }

        var count = ColorfulScrew9a.CollectScrews(SAV);
        await AppDialogs.Alert(this, count == 0
            ? $"Every {itemName} has already been collected."
            : $"Collected {itemName} x{count}!");
    }

    #endregion

    #region Save

    private void SaveTrainerInfo()
    {
        if (SAV.Gender != CB_Gender.Gender)
        {
            SAV.Gender = CB_Gender.Gender;
            SAV.PlayerFashion.Reset();
        }

        SAV.Money = Util.ToUInt32(MT_Money.Text ?? string.Empty);
        SAV.Language = CB_Language.GetSelectedItem()?.Value ?? 0;
        trainerID1.SaveTrainer(SAV);

        if (SAV.OT != TB_OTName.Text) // only modify if changed, to preserve trash bytes
            SAV.OT = TB_OTName.Text ?? string.Empty;

        SAV.PlayedHours = (ushort)Util.ToUInt32(MT_Hours.Text ?? string.Empty);
        SAV.PlayedMinutes = (ushort)(Util.ToUInt32(MT_Minutes.Text ?? string.Empty) % 60);
        SAV.PlayedSeconds = (ushort)(Util.ToUInt32(MT_Seconds.Text ?? string.Empty) % 60);

        var fallback = SAV.LastSaved.Timestamp;
        SAV.LastSaved.Timestamp = (CAL_LastSavedDate.SelectedDate?.Date ?? fallback.Date) + (CAL_LastSavedTime.SelectedTime ?? fallback.TimeOfDay);

        SAV.TicketPointsRoyale = Util.ToUInt32(MT_RoyaleRegular.Text ?? string.Empty);
        SAV.TicketPointsRoyaleInfinite = Util.ToUInt32(MT_RoyaleInfinite.Text ?? string.Empty);
    }

    private void SaveMap()
    {
        if (!MapUpdated)
            return;
        SAV.Coordinates.SetCoordinates((float)(NUD_X.Value ?? 0), (float)(NUD_Y.Value ?? 0), (float)(NUD_Z.Value ?? 0));
        SAV.Coordinates.SetPlayerRotation((double)(NUD_R.Value ?? 0));
        SAV.Coordinates.Map = TB_Map.Text ?? string.Empty;
    }

    private void SaveDLC()
    {
        if (SAV.SaveRevision == 0)
            return;
        SAV.SetValue(KHyperspaceSurveyPoints, Util.ToUInt32(MT_HyperspaceSurveyPoints.Text ?? string.Empty));
        SAV.SetString(SAV.Blocks.GetBlock(KStreetName).Data, TB_StreetName.Text ?? string.Empty, 18, StringConverterOption.ClearZero);
    }

    #endregion

    protected override void OnSave()
    {
        SaveTrainerInfo();
        SaveMap();
        SaveDLC();
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
