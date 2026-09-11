using System;
using Avalonia.Controls;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen3;

/// <summary>
/// Real time clock editor for Ruby/Sapphire/Emerald (port of the WinForms <c>SAV_RTC3</c>).
/// </summary>
public sealed class RTC3Window : SaveEditorWindow
{
    private readonly SaveFile Origin;
    private readonly SAV3 SAV;
    private readonly ISaveBlock3SmallHoenn Small;
    private readonly RTC3 ClockInitial;
    private readonly RTC3 ClockElapsed;

    private readonly TextBlock L_IDay = UiFactory.Label("L_IDay", "Days");
    private readonly NumericUpDown NUD_IDay = UiFactory.NumericUpDown("NUD_IDay", 0, ushort.MaxValue, 110);
    private readonly TextBlock L_IHour = UiFactory.Label("L_IHour", "Hours");
    private readonly NumericUpDown NUD_IHour = UiFactory.NumericUpDown("NUD_IHour", 0, 23, 90);
    private readonly TextBlock L_IMinute = UiFactory.Label("L_IMinute", "Minutes");
    private readonly NumericUpDown NUD_IMinute = UiFactory.NumericUpDown("NUD_IMinute", 0, 59, 90);
    private readonly TextBlock L_ISecond = UiFactory.Label("L_ISecond", "Seconds");
    private readonly NumericUpDown NUD_ISecond = UiFactory.NumericUpDown("NUD_ISecond", 0, 59, 90);

    private readonly TextBlock L_EDay = UiFactory.Label("L_EDay", "Days");
    private readonly NumericUpDown NUD_EDay = UiFactory.NumericUpDown("NUD_EDay", 0, ushort.MaxValue, 110);
    private readonly TextBlock L_EHour = UiFactory.Label("L_EHour", "Hours");
    private readonly NumericUpDown NUD_EHour = UiFactory.NumericUpDown("NUD_EHour", 0, 23, 90);
    private readonly TextBlock L_EMinute = UiFactory.Label("L_EMinute", "Minutes");
    private readonly NumericUpDown NUD_EMinute = UiFactory.NumericUpDown("NUD_EMinute", 0, 59, 90);
    private readonly TextBlock L_ESecond = UiFactory.Label("L_ESecond", "Seconds");
    private readonly NumericUpDown NUD_ESecond = UiFactory.NumericUpDown("NUD_ESecond", 0, 59, 90);

    private readonly Button B_Reset = UiFactory.Button("B_Reset", "Reset RTC");
    private readonly Button B_BerryFix = UiFactory.Button("B_BerryFix", "Berry Fix");

    public RTC3Window(SaveFile sav) : base("SAV_RTC3", "Clock Editor")
    {
        SAV = (SAV3)(Origin = sav).Clone();
        Small = (ISaveBlock3SmallHoenn)SAV.SmallBlock;

        ClockInitial = Small.ClockInitial;
        ClockElapsed = Small.ClockElapsed;

        var initial = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(initial, 0, L_IDay, NUD_IDay);
        UiFactory.AddFormRow(initial, 1, L_IHour, NUD_IHour);
        UiFactory.AddFormRow(initial, 2, L_IMinute, NUD_IMinute);
        UiFactory.AddFormRow(initial, 3, L_ISecond, NUD_ISecond);

        var elapsed = UiFactory.FormGrid(4);
        UiFactory.AddFormRow(elapsed, 0, L_EDay, NUD_EDay);
        UiFactory.AddFormRow(elapsed, 1, L_EHour, NUD_EHour);
        UiFactory.AddFormRow(elapsed, 2, L_EMinute, NUD_EMinute);
        UiFactory.AddFormRow(elapsed, 3, L_ESecond, NUD_ESecond);

        var body = UiFactory.Column(
            UiFactory.Row(new GroupBoxView("GB_Initial", "Initial Time", initial), new GroupBoxView("GB_Passed", "Time Elapsed", elapsed)),
            UiFactory.Row(B_Reset, B_BerryFix));
        SetBody(body);

        B_Reset.Click += (_, _) =>
        {
            NUD_IDay.Value = NUD_IHour.Value = NUD_IMinute.Value = NUD_ISecond.Value = 0;
            NUD_EDay.Value = NUD_EHour.Value = NUD_EMinute.Value = NUD_ESecond.Value = 0;
        };
        B_BerryFix.Click += (_, _) => NUD_EDay.Value = Math.Max((2 * 366) + 2, NUD_EDay.Value ?? 0); // advance

        LoadData();
    }

    private void LoadData()
    {
        NUD_IDay.Value = ClockInitial.Day;
        NUD_IHour.Value = Math.Min(NUD_IHour.Maximum, ClockInitial.Hour);
        NUD_IMinute.Value = Math.Min(NUD_IMinute.Maximum, ClockInitial.Minute);
        NUD_ISecond.Value = Math.Min(NUD_ISecond.Maximum, ClockInitial.Second);

        NUD_EDay.Value = ClockElapsed.Day;
        NUD_EHour.Value = Math.Min(NUD_EHour.Maximum, ClockElapsed.Hour);
        NUD_EMinute.Value = Math.Min(NUD_EMinute.Maximum, ClockElapsed.Minute);
        NUD_ESecond.Value = Math.Min(NUD_ESecond.Maximum, ClockElapsed.Second);
    }

    private void SaveData()
    {
        ClockInitial.Day = (ushort)(NUD_IDay.Value ?? 0);
        ClockInitial.Hour = (byte)(NUD_IHour.Value ?? 0);
        ClockInitial.Minute = (byte)(NUD_IMinute.Value ?? 0);
        ClockInitial.Second = (byte)(NUD_ISecond.Value ?? 0);

        ClockElapsed.Day = (ushort)(NUD_EDay.Value ?? 0);
        ClockElapsed.Hour = (byte)(NUD_EHour.Value ?? 0);
        ClockElapsed.Minute = (byte)(NUD_EMinute.Value ?? 0);
        ClockElapsed.Second = (byte)(NUD_ESecond.Value ?? 0);
    }

    protected override void OnSave()
    {
        SaveData();

        Small.ClockInitial = ClockInitial;
        Small.ClockElapsed = ClockElapsed;

        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
