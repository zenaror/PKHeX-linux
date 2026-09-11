using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Views;

/// <summary>
/// Box search filter popout (port of the WinForms <c>EntitySearchSetup</c>).
/// </summary>
public sealed class EntitySearchWindow : Window
{
    private SaveFile CurrentSave;

    /// <summary>Filter built by the last search; null when reset.</summary>
    public Func<PKM, bool>? SearchFilter { get; private set; }

    /// <summary>Occurs when the Search action is requested.</summary>
    public event EventHandler? SearchRequested;

    /// <summary>Occurs when the Reset action is requested.</summary>
    public event EventHandler? ResetRequested;

    /// <summary>Occurs when the next item in a sequence is sought.</summary>
    public event EventHandler? SeekNext;

    /// <summary>Occurs when the Seek Previous action is requested.</summary>
    public event EventHandler? SeekPrevious;

    private readonly EntitySearchView UC_EntitySearch = new() { Name = "UC_EntitySearch" };
    private readonly EntityInstructionBuilderView UC_Builder;
    private readonly TextBox RTB_Instructions = new() { Name = "RTB_Instructions", AcceptsReturn = true, MinHeight = 120, TextWrapping = global::Avalonia.Media.TextWrapping.NoWrap };
    private readonly Button B_Add = UiFactory.Button("B_Add", "Add");
    private readonly Button B_Search = UiFactory.Button("B_Search", "Search!");
    private readonly Button B_Reset = UiFactory.Button("B_Reset", "Reset Filters");
    private readonly Button B_Next = UiFactory.Button("B_Next", "Next");
    private readonly Button B_Previous = UiFactory.Button("B_Previous", "Previous");

    public EntitySearchWindow(IPKMView edit, SaveFile sav)
    {
        CurrentSave = sav;
        UC_Builder = new EntityInstructionBuilderView(() => edit.PreparePKM()) { ReadOnly = true };

        Name = "EntitySearchSetup";
        Title = "Search";
        Icon = AppIcon.Get();
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;

        var advanced = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 6,
            Margin = new global::Avalonia.Thickness(6),
            Children = { UC_Builder, B_Add, RTB_Instructions },
        };

        var tabs = new TabControl { Name = "TC_SearchSettings" };
        tabs.Items.Add(new TabItem { Name = "Tab_General", Header = "General", Content = UC_EntitySearch });
        tabs.Items.Add(new TabItem { Name = "Tab_Advanced", Header = "Advanced", Content = advanced });
        tabs.SelectedIndex = 0;

        B_Next.IsVisible = B_Previous.IsVisible = false;
        var navigate = new StackPanel
        {
            Name = "TLP_SearchNavigate",
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 6,
            Children = { B_Previous, B_Search, B_Next, B_Reset },
        };

        Content = new StackPanel
        {
            Name = "TLP_Main",
            Orientation = Orientation.Vertical,
            Margin = new global::Avalonia.Thickness(8),
            Spacing = 8,
            Children = { tabs, navigate },
        };

        Initialize(sav);

        B_Search.Click += (_, _) => ClickSearch();
        B_Reset.Click += (_, _) => ForceReset();
        B_Add.Click += (_, _) => ClickAdd();
        B_Next.Click += (_, _) => SeekNext?.Invoke(this, EventArgs.Empty);
        B_Previous.Click += (_, _) => SeekPrevious?.Invoke(this, EventArgs.Empty);

        KeyDown += OnKeyDown;
        // Closing the window keeps the instance alive so the filter and the seek buttons survive (WinForms hides it).
        Closing += (_, e) =>
        {
            if (IsClosingForReal)
                return;
            e.Cancel = true;
            Hide();
        };

        Translator.TranslateInterface(this, MainWindow.CurrentLanguage);
    }

    private bool IsClosingForReal;

    private void Initialize(SaveFile sav)
    {
        UC_EntitySearch.PopulateComboBoxes(GameInfo.FilteredSources);
        UC_EntitySearch.SetFormatAnyText(MsgAny);
        UC_EntitySearch.InitializeSelections(sav, showContext: false);
        CurrentSave = sav;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (RTB_Instructions.IsFocused)
                return;
            ClickSearch();
            e.Handled = true;
        }

        // Quick close with Ctrl+W
        if (e.Key == Key.W && e.KeyModifiers == KeyModifiers.Control)
            Hide();
    }

    private void ClickSearch()
    {
        SearchFilter = UC_EntitySearch.GetFilter(RTB_Instructions.Text ?? string.Empty);
        SearchRequested?.Invoke(this, EventArgs.Empty);
        B_Next.IsVisible = B_Previous.IsVisible = true;
    }

    private async void ClickAdd()
    {
        var s = UC_Builder.Create();
        if (s.Length == 0)
        {
            await AppDialogs.Alert(this, MsgBEPropertyInvalid);
            return;
        }

        var text = RTB_Instructions.Text ?? string.Empty;
        if (text.Length != 0 && !text.EndsWith('\n'))
            text += Environment.NewLine;
        RTB_Instructions.Text = text + s;
    }

    public bool IsSameSaveFile(SaveFile sav) => CurrentSave == sav;

    public void ForceReset()
    {
        SearchFilter = null;
        UC_EntitySearch.ResetFilters();
        RTB_Instructions.Text = string.Empty;
        B_Next.IsVisible = B_Previous.IsVisible = false;
        ResetRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Closes the window for good (the close button only hides it).</summary>
    public void CloseForReal()
    {
        SearchFilter = null;
        IsClosingForReal = true;
        Close();
    }
}
