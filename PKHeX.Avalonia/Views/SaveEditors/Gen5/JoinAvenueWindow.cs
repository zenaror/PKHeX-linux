using Avalonia.Controls;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Views.SaveEditors.Gen5.JoinAvenue;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen5;

/// <summary>
/// Join Avenue editor for Black 2 / White 2 (port of the WinForms <c>SAV_JoinAvenue</c>).
/// </summary>
/// <remarks>
/// Four entity lists (visitors, fans, occupants and assistants) share one general editor and get their own
/// per-kind page; the avenue's own settings and the player's entry are separate tabs.
/// </remarks>
public sealed class JoinAvenueWindow : SaveEditorWindow
{
    private readonly SAV5B2W2 Origin;
    private readonly SAV5B2W2 SAV;
    private readonly JoinAvenue5 Avenue;

    private readonly JoinAvenueListView<JoinAvenueVisitor5, JoinAvenueVisitorSpecificView> VisitorsEditor;
    private readonly JoinAvenueListView<JoinAvenueFan5, JoinAvenueFanSpecificView> FansEditor;
    private readonly JoinAvenueListView<JoinAvenueVisitor5, JoinAvenueVisitorSpecificView> OccupantsEditor;
    private readonly JoinAvenueListView<JoinAvenueAssistant5, JoinAvenueAssistantSpecificView> AssistantsEditor;

    private readonly JoinAvenueSettingsView UC_Settings = new();
    private readonly JoinAvenueEntityGeneralView UC_SelfGeneral = new();
    private readonly JoinAvenueVisitorSpecificView UC_SelfSpecific = new();

    private readonly CheckBox CHK_ScriptFlag = UiFactory.Check("CHK_ScriptFlag", "Script Flag");
    private readonly NumericUpDown NUD_VisitorCount = UiFactory.NumericUpDown("NUD_VisitorCount", 0, uint.MaxValue, 140);
    private readonly NumericUpDown NUD_FanCount = UiFactory.NumericUpDown("NUD_FanCount", 0, uint.MaxValue, 140);

    public JoinAvenueWindow(SAV5B2W2 sav) : base("SAV_JoinAvenue", "Join Avenue Editor")
    {
        Origin = sav;
        SAV = (SAV5B2W2)sav.Clone();
        Avenue = SAV.JoinAvenue;

        VisitorsEditor = new(JoinAvenue5.VisitorCount, Avenue.GetVisitor, new JoinAvenueVisitorSpecificView());
        FansEditor = new(JoinAvenue5.FanCount, Avenue.GetFan, new JoinAvenueFanSpecificView());
        OccupantsEditor = new(JoinAvenue5.OccupantCount, Avenue.GetOccupant, new JoinAvenueVisitorSpecificView());
        AssistantsEditor = new(JoinAvenue5.AssistantCount, Avenue.GetAssistant, new JoinAvenueAssistantSpecificView());

        BuildLayout();
        LoadData();
    }

    private void BuildLayout()
    {
        var settings = UiFactory.Column(
            CHK_ScriptFlag,
            UiFactory.Row(UiFactory.Label("L_VisitorCount", "Visitor Count:"), NUD_VisitorCount),
            UiFactory.Row(UiFactory.Label("L_FanCount", "Fan Count:"), NUD_FanCount),
            UC_Settings);

        var self = new TabControl { Name = "TC_Self" };
        self.Items.Add(new TabItem { Name = "Tab_SelfGeneral", Header = "General", Content = new ScrollViewer { Content = UC_SelfGeneral, MaxHeight = 540 } });
        self.Items.Add(new TabItem { Name = "Tab_SelfSpecific", Header = "Specific", Content = new ScrollViewer { Content = UC_SelfSpecific, MaxHeight = 540 } });

        var tabs = new TabControl { Name = "TC_JoinAvenue" };
        tabs.Items.Add(new TabItem { Name = "Tab_Settings", Header = "Settings", Content = new ScrollViewer { Content = settings, MaxHeight = 620 } });
        tabs.Items.Add(new TabItem { Name = "Tab_Visitors", Header = "Visitors", Content = VisitorsEditor });
        tabs.Items.Add(new TabItem { Name = "Tab_Fans", Header = "Fans", Content = FansEditor });
        tabs.Items.Add(new TabItem { Name = "Tab_Occupants", Header = "Occupants", Content = OccupantsEditor });
        tabs.Items.Add(new TabItem { Name = "Tab_Assistants", Header = "Assistants", Content = AssistantsEditor });
        tabs.Items.Add(new TabItem { Name = "Tab_Self", Header = "Self", Content = self });
        SetBody(tabs);
    }

    private void LoadData()
    {
        CHK_ScriptFlag.IsChecked = Avenue.ScriptFlag;
        NUD_VisitorCount.SetValueClamped(Avenue.CountVisitor);
        NUD_FanCount.SetValueClamped(Avenue.CountFan);
        UC_Settings.LoadObject(Avenue.Settings);
        VisitorsEditor.LoadAll();
        FansEditor.LoadAll();
        OccupantsEditor.LoadAll();
        AssistantsEditor.LoadAll();
        UC_SelfGeneral.LoadObject(Avenue.Self);
        UC_SelfSpecific.LoadObject(Avenue.Self);
    }

    protected override void OnSave()
    {
        Avenue.ScriptFlag = CHK_ScriptFlag.IsChecked == true;
        Avenue.CountVisitor = (uint)(NUD_VisitorCount.Value ?? 0);
        Avenue.CountFan = (uint)(NUD_FanCount.Value ?? 0);
        UC_Settings.SaveObject(Avenue.Settings);
        VisitorsEditor.SaveAll();
        FansEditor.SaveAll();
        OccupantsEditor.SaveAll();
        AssistantsEditor.SaveAll();
        UC_SelfGeneral.SaveObject(Avenue.Self);
        UC_SelfSpecific.SaveObject(Avenue.Self);
        Origin.CopyChangesFrom(SAV);
        Close();
    }
}
