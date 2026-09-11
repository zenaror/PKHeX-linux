using System;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors;

/// <summary>
/// Seen/caught flag editor (port of the WinForms <c>SAV_SimplePokedex</c>).
/// </summary>
public sealed class SimplePokedexWindow : SaveEditorWindow
{
    private readonly SaveFile SAV;
    private readonly int MaxSpeciesID;

    private readonly TextBlock Label_Seen = UiFactory.Label("Label_Seen", "Seen:");
    private readonly TextBlock Label_Caught = UiFactory.Label("Label_Caught", "Caught:");
    private readonly CheckedListView CLB_Seen = new() { Name = "CLB_Seen", Width = 260, Height = 420 };
    private readonly CheckedListView CLB_Caught = new() { Name = "CLB_Caught", Width = 260, Height = 420 };
    private readonly Button B_SeenAll = UiFactory.Button("B_SeenAll", "Seen All");
    private readonly Button B_SeenNone = UiFactory.Button("B_SeenNone", "Seen None");
    private readonly Button B_CaughtAll = UiFactory.Button("B_CaughtAll", "Caught All");
    private readonly Button B_CaughtNone = UiFactory.Button("B_CaughtNone", "Caught None");

    public SimplePokedexWindow(SaveFile sav) : base("SAV_SimplePokedex", "Pokédex Editor")
    {
        SAV = sav;
        var count = MaxSpeciesID = SAV.MaxSpeciesID;

        var seen = UiFactory.Column(Label_Seen, CLB_Seen, UiFactory.Row(B_SeenAll, B_SeenNone));
        var caught = UiFactory.Column(Label_Caught, CLB_Caught, UiFactory.Row(B_CaughtAll, B_CaughtNone));
        var body = UiFactory.Row(seen, caught);
        body.Spacing = 12;
        seen.VerticalAlignment = caught.VerticalAlignment = VerticalAlignment.Top;
        SetBody(body);

        var speciesNames = GameInfo.Strings.specieslist.AsSpan(1, count);
        AddAllSpecies(speciesNames);
        LoadAllFlags(SAV, count);

        B_SeenAll.Click += (_, _) => CLB_Seen.SetAllChecked(true);
        B_SeenNone.Click += (_, _) => CLB_Seen.SetAllChecked(false);
        B_CaughtAll.Click += (_, _) => CLB_Caught.SetAllChecked(true);
        B_CaughtNone.Click += (_, _) => CLB_Caught.SetAllChecked(false);
    }

    private void AddAllSpecies(ReadOnlySpan<string> speciesNames)
    {
        for (var i = 0; i < speciesNames.Length; i++)
        {
            var text = $"{i + 1:000} - {speciesNames[i]}";
            CLB_Seen.Add(text);
            CLB_Caught.Add(text);
        }
    }

    private void LoadAllFlags(SaveFile sav, int count)
    {
        for (int i = 0; i < count; i++)
        {
            ushort species = (ushort)(i + 1);
            CLB_Seen.SetItemChecked(i, sav.GetSeen(species));
            CLB_Caught.SetItemChecked(i, sav.GetCaught(species));
        }
    }

    private void SaveAllFlags(SaveFile sav, int count)
    {
        for (int i = 0; i < count; i++)
        {
            ushort species = (ushort)(i + 1);
            sav.SetSeen(species, CLB_Seen.GetItemChecked(i));
            sav.SetCaught(species, CLB_Caught.GetItemChecked(i));
        }
    }

    protected override void OnSave()
    {
        SaveAllFlags(SAV, MaxSpeciesID);
        SanityCheck();
        SAV.State.Edited = true;
        Close();
    }

    private void SanityCheck()
    {
        // Remove any foreign species dex bits.
        if (SAV is SAV3FRLG { IsVirtualConsole: true })
        {
            for (int i = 150; i < MaxSpeciesID; i++)
            {
                ushort species = (ushort)(i + 1);
                if (!Legal.IsForeignFRLG(species))
                    continue;
                //SAV.SetSeen(species, false); // some can be seen via trainers; don't bother
                SAV.SetCaught(species, false);
            }
        }
        if (SAV is SAV3 s3)
            s3.MirrorSeenFlags();
    }
}
