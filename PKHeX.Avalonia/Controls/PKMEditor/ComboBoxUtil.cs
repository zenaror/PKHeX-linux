using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>
/// <see cref="ComboItem"/> binding helpers for <see cref="ComboBox"/> (equivalent of the WinForms DisplayMember/ValueMember binding).
/// </summary>
public static class ComboBoxUtil
{
    /// <summary>
    /// Configures the combo box to display <see cref="ComboItem.Text"/>.
    /// </summary>
    public static void InitializeBinding(this ComboBox cb)
    {
        cb.DisplayMemberBinding = new Binding(nameof(ComboItem.Text));
    }

    /// <summary>
    /// Replaces the item list (like assigning a WinForms DataSource); the selection is cleared.
    /// </summary>
    public static void SetItems(this ComboBox cb, IReadOnlyList<ComboItem> items)
    {
        cb.SelectedIndex = -1;
        cb.ItemsSource = items;
    }

    /// <summary>
    /// Gets the selected <see cref="ComboItem.Value"/>. If no value is selected, will return 0.
    /// </summary>
    public static int GetValue(this ComboBox cb) => cb.SelectedItem is ComboItem c ? c.Value : 0;

    /// <summary>
    /// Gets the selected <see cref="ComboItem"/>, or null.
    /// </summary>
    public static ComboItem? GetSelectedItem(this ComboBox cb) => cb.SelectedItem as ComboItem;

    /// <summary>
    /// Selects the item with the requested <see cref="ComboItem.Value"/>; clears the selection if not present (same as WinForms SelectedValue).
    /// </summary>
    public static bool SetValue(this ComboBox cb, int value)
    {
        if (cb.ItemsSource is IReadOnlyList<ComboItem> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Value != value)
                    continue;
                cb.SelectedIndex = i;
                return true;
            }
        }
        cb.SelectedIndex = -1;
        return false;
    }

    /// <summary>
    /// Number of items in the list.
    /// </summary>
    public static int GetItemCount(this ComboBox cb) => cb.ItemsSource switch
    {
        IReadOnlyCollection<object> c => c.Count,
        System.Collections.ICollection c => c.Count,
        null => cb.ItemCount,
        _ => cb.ItemCount,
    };

    /// <summary>
    /// Text of the selected item.
    /// </summary>
    public static string GetText(this ComboBox cb) => cb.SelectedItem switch
    {
        ComboItem c => c.Text,
        string s => s,
        { } o => o.ToString() ?? string.Empty,
        _ => string.Empty,
    };

    /// <summary>
    /// Clamps the requested index to the available items and selects it (WinForms LoadClamp).
    /// </summary>
    public static void SetIndexClamped(this ComboBox cb, int value)
    {
        var max = cb.GetItemCount() - 1;
        if (value > max)
            value = max;
        else if (value < -1)
            value = 0;
        cb.SelectedIndex = value;
    }

    /// <summary>
    /// Marks the control as holding an invalid selection.
    /// </summary>
    /// <summary>
    /// Loads a country / sub-region list (port of <c>Main.SetCountrySubRegion</c>), retaining the previous index when possible.
    /// </summary>
    public static void SetCountrySubRegion(this ComboBox cb, string type)
    {
        // Try to retain previous selection index. If triggered by language change, the list will be reloaded.
        int index = cb.SelectedIndex;
        string cl = GameInfo.CurrentLanguage;
        cb.SetItems(Util.GetCountryRegionList(type, cl));

        if (index > 0 && index < cb.GetItemCount())
            cb.SelectedIndex = index;
    }

    public static void SetInvalid(this Control c, System.Drawing.Color color)
    {
        c.SetValue(TemplatedControl.BackgroundProperty, color.ToBrush());
        c.SetValue(TemplatedControl.ForegroundProperty, Brushes.Black);
    }

    /// <summary>
    /// Restores the default colors of the control.
    /// </summary>
    public static void ResetColors(this Control c)
    {
        c.ClearValue(TemplatedControl.BackgroundProperty);
        c.ClearValue(TemplatedControl.ForegroundProperty);
    }

    /// <summary>
    /// Assigns a value to a <see cref="NumericUpDown"/>, clamped to its range (port of the WinForms <c>SetValueClamped</c>).
    /// </summary>
    public static void SetValueClamped(this NumericUpDown nud, decimal value)
        => nud.Value = System.Math.Clamp(value, nud.Minimum, nud.Maximum);

    public static void SetBackColor(this Control c, System.Drawing.Color color) => c.SetValue(TemplatedControl.BackgroundProperty, color.ToBrush());
    public static void SetForeColor(this Control c, System.Drawing.Color color) => c.SetValue(TemplatedControl.ForegroundProperty, color.ToBrush());
    public static void ResetForeColor(this Control c) => c.ClearValue(TemplatedControl.ForegroundProperty);
    public static void ResetBackColor(this Control c) => c.ClearValue(TemplatedControl.BackgroundProperty);

    public static bool IsInvalidColor(this Control c, System.Drawing.Color color)
        => c.GetValue(TemplatedControl.BackgroundProperty) is ISolidColorBrush b && b.Color == color.ToAvalonia();
}

/// <summary>
/// Color conversion helpers between <see cref="System.Drawing.Color"/> (used by PKHeX.Drawing) and Avalonia.
/// </summary>
public static class ColorUtilAvalonia
{
    public static Color ToAvalonia(this System.Drawing.Color c) => Color.FromArgb(c.A, c.R, c.G, c.B);
    public static IBrush ToBrush(this System.Drawing.Color c) => new SolidColorBrush(c.ToAvalonia());

    // SystemColor equivalents for dark mode support (WinFormsUtil)
    public static System.Drawing.Color ColorWarn => App.IsDarkModeEnabled ? System.Drawing.Color.OrangeRed : System.Drawing.Color.Red;
    public static System.Drawing.Color ColorValid => App.IsDarkModeEnabled ? System.Drawing.Color.FromArgb(030, 070, 030) : System.Drawing.Color.FromArgb(200, 255, 200);
    public static System.Drawing.Color ColorHint => App.IsDarkModeEnabled ? System.Drawing.Color.DarkKhaki : System.Drawing.Color.LightYellow;
    public static System.Drawing.Color ColorSuspect => App.IsDarkModeEnabled ? System.Drawing.Color.LightCoral : System.Drawing.Color.LightSalmon;
    public static System.Drawing.Color ColorAlternate => App.IsDarkModeEnabled ? System.Drawing.Color.SlateGray : System.Drawing.Color.SeaShell;
    public static System.Drawing.Color ColorAccept => App.IsDarkModeEnabled ? System.Drawing.Color.DarkSlateBlue : System.Drawing.Color.LightBlue;
    public static System.Drawing.Color ColorPlus => App.IsDarkModeEnabled ? System.Drawing.Color.OrangeRed : System.Drawing.Color.Red;
    public static System.Drawing.Color ColorMinus => App.IsDarkModeEnabled ? System.Drawing.Color.MediumBlue : System.Drawing.Color.Blue;
    public static System.Drawing.Color ControlText => App.IsDarkModeEnabled ? System.Drawing.Color.White : System.Drawing.Color.Black;
}
