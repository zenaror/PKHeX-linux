using System;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Avalonia.Localization;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using static PKHeX.Core.MessageStrings;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// Builds a single batch instruction line (port of the WinForms <c>EntityInstructionBuilder</c>).
/// </summary>
public sealed class EntityInstructionBuilderView : UserControl
{
    private readonly Func<PKM> Getter;
    private PKM Entity => Getter();

    private static ReadOnlySpan<char> Prefixes => StringInstruction.Prefixes;

    private int currentFormat = -1;
    private int requirementIndex;
    private bool readOnlyMode;
    private readonly MenuItem[] requireMenuItems = new MenuItem[Prefixes.Length];
    private readonly MenuFlyout requireMenu = new();

    private readonly ComboBox CB_Format = UiFactory.StringCombo("CB_Format", 110);
    private readonly ComboBox CB_Property = UiFactory.StringCombo("CB_Property", 220);
    private readonly Button B_Require = UiFactory.Button("B_Require", "_Set");
    private readonly TextBlock L_PropType = UiFactory.Label("L_PropType", "PropertyType");
    private readonly TextBlock L_PropValue = UiFactory.Label("L_PropValue", "PropertyValue");

    public EntityInstructionBuilderView(Func<PKM> pk)
    {
        Getter = pk;
        for (int i = 0; i < Prefixes.Length; i++)
        {
            var prefix = Prefixes[i];
            var name = i == 0 ? "Set" : prefix.ToString();
            var text = i == 0 ? "_Set" : prefix.ToString();
            var item = new MenuItem { Name = $"mnu_{name}", Header = text };
            if (StringInstruction.IsMutationInstruction(prefix))
                item.Foreground = ColorUtilAvalonia.ColorWarn.ToBrush();
            var index = i;
            item.Click += (_, _) => SetRequirementIndex(index);
            requireMenu.Items.Add(item);
            requireMenuItems[i] = item;
        }

        // Allow translation of the menu item (flyout items are outside the logical tree).
        var lang = MainWindow.CurrentLanguage;
        foreach (var item in requireMenuItems)
            item.Header = Translator.ConvertAccessKeys(Translator.TranslateText(Translator.GetKey("BatchEdit", item.Name!), (string)item.Header!, lang));

        B_Require.Flyout = requireMenu;
        requireMenu.Placement = PlacementMode.Bottom;

        CB_Format.Items.Clear();
        CB_Format.Items.Add(MsgAny);
        foreach (Type t in EntityBatchEditor.Instance.Types)
            CB_Format.Items.Add(t.Name.ToLowerInvariant());
        CB_Format.Items.Add(MsgAll);

        CB_Format.SelectionChanged += (_, _) => CB_Format_SelectedIndexChanged();
        CB_Property.SelectionChanged += (_, _) => CB_Property_SelectedIndexChanged();

        ToolTip.SetTip(CB_Property, MsgBEToolTipPropName);
        ToolTip.SetTip(L_PropType, MsgBEToolTipPropType);
        ToolTip.SetTip(L_PropValue, MsgBEToolTipPropValue);

        var row = UiFactory.Row(B_Require, CB_Format, CB_Property, L_PropType, L_PropValue);
        row.HorizontalAlignment = HorizontalAlignment.Left;
        Content = row;

        CB_Format.SelectedIndex = 0;
        SetRequirementIndex(0);
        UpdateRequireMenuVisibility();
    }

    private void CB_Format_SelectedIndexChanged()
    {
        if (currentFormat == CB_Format.SelectedIndex)
            return;

        byte format = (byte)Math.Max(0, CB_Format.SelectedIndex);
        CB_Property.Items.Clear();
        foreach (var p in EntityBatchEditor.Instance.Properties[format])
            CB_Property.Items.Add(p);
        CB_Property.SelectedIndex = 0;
        currentFormat = format;
    }

    private void CB_Property_SelectedIndexChanged()
    {
        var property = CB_Property.SelectedItem as string ?? string.Empty;
        if (!EntityBatchEditor.Instance.TryGetPropertyType(property, out var type, CB_Format.SelectedIndex))
            type = "Unknown";
        L_PropType.Text = type;

        if (EntityBatchEditor.Instance.TryGetHasProperty(Entity, property, out var pi))
        {
            L_PropType.ResetForeColor();

            bool hasValue = GetPropertyDisplayText(pi, Entity, out var display);
            L_PropValue.Text = display;
            if (hasValue)
                L_PropValue.ResetForeColor();
            else
                L_PropValue.SetForeColor(ColorUtilAvalonia.ColorWarn);
        }
        else // no property, flag
        {
            L_PropValue.Text = string.Empty;
            L_PropType.SetForeColor(ColorUtilAvalonia.ColorWarn);
        }
    }

    private void SetRequirementIndex(int index)
    {
        if ((uint)index >= Prefixes.Length)
            return;

        requirementIndex = index;
        B_Require.Content = requireMenuItems[index].Header;
    }

    private void UpdateRequireMenuVisibility()
    {
        requireMenuItems[0].IsVisible = !readOnlyMode;

        if (readOnlyMode && requirementIndex == 0)
            SetRequirementIndex(1);
    }

    private static bool GetPropertyDisplayText(PropertyInfo pi, PKM pk, out string display)
    {
        var type = pi.PropertyType;
        if (type.IsGenericType)
        {
            if (type.GetGenericTypeDefinition().IsByRefLike) // Span, ReadOnlySpan
            {
                display = pi.PropertyType.ToString();
                return false;
            }
        }

        var value = pi.GetValue(pk);
        if (value?.ToString() is not { } x)
        {
            display = "null";
            return false;
        }

        display = x;
        return true;
    }

    /// <summary>
    /// Creates the instruction line for the current selection.
    /// </summary>
    public string Create()
    {
        if (CB_Property.SelectedIndex < 0)
            return string.Empty;

        var property = CB_Property.SelectedItem;
        var prefix = Prefixes[requirementIndex];
        const char equals = StringInstruction.SplitInstruction;
        return $"{prefix}{property}{equals}";
    }

    /// <summary>Hides the "Set" instruction prefix (filter-only mode).</summary>
    public bool ReadOnly
    {
        set
        {
            readOnlyMode = value;
            UpdateRequireMenuVisibility();
        }
    }
}
