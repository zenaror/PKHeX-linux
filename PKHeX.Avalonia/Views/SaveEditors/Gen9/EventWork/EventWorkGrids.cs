using System;
using System.Globalization;
using Avalonia.Controls;
using PKHeX.Core;

namespace PKHeX.Avalonia.Views.SaveEditors.Gen9.EventWork;

/// <summary>
/// Grid over an <see cref="EventWorkStorage64{T}"/> block: index, value, name
/// (port of the WinForms <c>EventWorkGrid64&lt;T&gt;</c>).
/// </summary>
public sealed class EventWorkGrid64<T> : EventWorkGridBase<EventWorkGrid64<T>.Row> where T : struct, IEquatable<T>
{
    private readonly EventWorkStorage64<T> Storage;
    private readonly EventWorkLookup Names;

    public sealed class Row : EventWorkRow
    {
        private string _name = string.Empty;
        private bool _flag;
        private string _value = "0";

        public string Name { get => _name; set => Set(ref _name, value, nameof(Name)); }
        public bool Flag { get => _flag; set => Set(ref _flag, value, nameof(Flag)); }
        public string Value { get => _value; set => Set(ref _value, value, nameof(Value)); }
    }

    private EventWorkGrid64(ContentControl host, EventWorkStorage64<T> storage, EventWorkLookup names) : base(host)
    {
        Storage = storage;
        Names = names;

        Grid.Columns.Add(IndexColumn());
        if (typeof(T) == typeof(bool))
            Grid.Columns.Add(BoolColumn("Value", nameof(Row.Flag), 80));
        else
            Grid.Columns.Add(TextColumn("Value", nameof(Row.Value), 140));
        Grid.Columns.Add(TextColumn("Name", nameof(Row.Name), 0));
    }

    public static EventWorkGrid64<bool> CreateFlags(ContentControl host, EventWorkFlagStorage storage, EventWorkLookup names)
        => new(host, storage, names);

    public static EventWorkGrid64<ulong> CreateValues(ContentControl host, EventWorkValueStorage storage, EventWorkLookup names)
        => new(host, storage, names);

    protected override bool Matches(Row row, string text) => row.Name.Contains(text, StringComparison.OrdinalIgnoreCase);

    public override void Load()
    {
        Rows.Clear();
        var count = Storage.Count;
        for (int i = 0; i < count; i++)
        {
            var value = Storage.GetValue(i);
            var row = new Row { Index = i, Name = Names.GetName(Storage.GetKey(i)) };
            if (value is bool b)
                row.Flag = b;
            else
                row.Value = value.ToString() ?? "0";
            Rows.Add(row);
        }
        Rebind();
    }

    public override void Save()
    {
        var count = Math.Min(Storage.Count, Rows.Count);
        for (int i = 0; i < count; i++)
        {
            var row = Rows[i];
            if (typeof(T) == typeof(bool))
            {
                Storage.SetValue(i, (T)(object)row.Flag);
            }
            else
            {
                // Reject a bad edit by keeping what is already stored, as the WinForms validator does.
                if (!ulong.TryParse(row.Value, CultureInfo.InvariantCulture, out var v))
                    continue;
                Storage.SetValue(i, (T)(object)v);
            }
            Storage.SetKey(i, Names.GetHash(row.Name.Trim()));
        }
        Storage.Compress();
    }
}

/// <summary>
/// Grid over a 128-bit-key block shown as one key and two values (port of <c>EventWorkGridTuple</c>).
/// </summary>
public sealed class EventWorkGridTuple : EventWorkGridBase<EventWorkGridTuple.Row>
{
    private readonly EventWorkValueStorageKey128 Storage;
    private readonly EventWorkLookup Names;

    public sealed class Row : EventWorkRow
    {
        private string _keyA = string.Empty;
        private string _value1 = "0";
        private string _value2 = "0";

        public string KeyA { get => _keyA; set => Set(ref _keyA, value, nameof(KeyA)); }
        public string Value1 { get => _value1; set => Set(ref _value1, value, nameof(Value1)); }
        public string Value2 { get => _value2; set => Set(ref _value2, value, nameof(Value2)); }
    }

    private EventWorkGridTuple(ContentControl host, EventWorkValueStorageKey128 storage, EventWorkLookup names) : base(host)
    {
        Storage = storage;
        Names = names;

        Grid.Columns.Add(IndexColumn());
        Grid.Columns.Add(TextColumn("Key A", nameof(Row.KeyA), 0));
        Grid.Columns.Add(TextColumn("Value 1", nameof(Row.Value1), 130));
        Grid.Columns.Add(TextColumn("Value 2", nameof(Row.Value2), 130));
    }

    public static EventWorkGridTuple CreateValues(ContentControl host, EventWorkValueStorageKey128 storage, EventWorkLookup names)
        => new(host, storage, names);

    protected override bool Matches(Row row, string text) => row.KeyA.Contains(text, StringComparison.OrdinalIgnoreCase);

    public override void Load()
    {
        Rows.Clear();
        var count = Storage.Count;
        for (int i = 0; i < count; i++)
        {
            var (a, b) = Storage.GetKey(i);
            Rows.Add(new Row
            {
                Index = i,
                KeyA = Names.GetName(a),
                Value1 = ((long)b).ToString(CultureInfo.InvariantCulture),
                Value2 = Storage.GetValue(i).ToString(CultureInfo.InvariantCulture),
            });
        }
        Rebind();
    }

    public override void Save()
    {
        var count = Math.Min(Storage.Count, Rows.Count);
        for (int i = 0; i < count; i++)
        {
            var row = Rows[i];
            var a = Names.GetHash(row.KeyA.Trim());
            var b = long.TryParse(row.Value1, CultureInfo.InvariantCulture, out var bt) ? (ulong)bt : 0UL;
            var v = ulong.TryParse(row.Value2, CultureInfo.InvariantCulture, out var vt) ? vt : 0UL;
            Storage.SetValue(i, v);
            Storage.SetKey(i, a, b);
        }
        Storage.Compress();
    }
}

/// <summary>
/// Grid over a 128-bit-key block shown as two named keys and one value (port of <c>EventWorkGrid128</c>).
/// </summary>
public sealed class EventWorkGrid128 : EventWorkGridBase<EventWorkGrid128.Row>
{
    private readonly EventWorkValueStorageKey128 Storage;
    private readonly EventWorkLookup Names;

    public sealed class Row : EventWorkRow
    {
        private string _keyA = string.Empty;
        private string _keyB = string.Empty;
        private string _value = "0";

        public string KeyA { get => _keyA; set => Set(ref _keyA, value, nameof(KeyA)); }
        public string KeyB { get => _keyB; set => Set(ref _keyB, value, nameof(KeyB)); }
        public string Value { get => _value; set => Set(ref _value, value, nameof(Value)); }
    }

    private EventWorkGrid128(ContentControl host, EventWorkValueStorageKey128 storage, EventWorkLookup names) : base(host)
    {
        Storage = storage;
        Names = names;

        Grid.Columns.Add(IndexColumn());
        Grid.Columns.Add(TextColumn("Key A", nameof(Row.KeyA), 280));
        Grid.Columns.Add(TextColumn("Key B", nameof(Row.KeyB), 0));
        Grid.Columns.Add(TextColumn("Value", nameof(Row.Value), 130));
    }

    public static EventWorkGrid128 CreateValues(ContentControl host, EventWorkValueStorageKey128 storage, EventWorkLookup names)
        => new(host, storage, names);

    protected override bool Matches(Row row, string text)
        => row.KeyA.Contains(text, StringComparison.OrdinalIgnoreCase) || row.KeyB.Contains(text, StringComparison.OrdinalIgnoreCase);

    public override void Load()
    {
        Rows.Clear();
        var count = Storage.Count;
        for (int i = 0; i < count; i++)
        {
            var (a, b) = Storage.GetKey(i);
            Rows.Add(new Row
            {
                Index = i,
                KeyA = Names.GetName(a),
                KeyB = Names.GetName(b),
                Value = Storage.GetValue(i).ToString(CultureInfo.InvariantCulture),
            });
        }
        Rebind();
    }

    public override void Save()
    {
        var count = Math.Min(Storage.Count, Rows.Count);
        for (int i = 0; i < count; i++)
        {
            var row = Rows[i];
            var v = ulong.TryParse(row.Value, CultureInfo.InvariantCulture, out var vt) ? vt : 0UL;
            Storage.SetValue(i, v);
            Storage.SetKey(i, Names.GetHash(row.KeyA.Trim()), Names.GetHash(row.KeyB.Trim()));
        }
        Storage.Compress();
    }
}

/// <summary>
/// Grid over a 192-bit-key block shown as three named keys and one value (port of <c>EventWorkGrid192</c>).
/// </summary>
public sealed class EventWorkGrid192 : EventWorkGridBase<EventWorkGrid192.Row>
{
    private readonly EventWorkValueStorageKey192 Storage;
    private readonly EventWorkLookup Names;

    public sealed class Row : EventWorkRow
    {
        private string _keyA = string.Empty;
        private string _keyB = string.Empty;
        private string _keyC = string.Empty;
        private string _value = "0";

        public string KeyA { get => _keyA; set => Set(ref _keyA, value, nameof(KeyA)); }
        public string KeyB { get => _keyB; set => Set(ref _keyB, value, nameof(KeyB)); }
        public string KeyC { get => _keyC; set => Set(ref _keyC, value, nameof(KeyC)); }
        public string Value { get => _value; set => Set(ref _value, value, nameof(Value)); }
    }

    private EventWorkGrid192(ContentControl host, EventWorkValueStorageKey192 storage, EventWorkLookup names) : base(host)
    {
        Storage = storage;
        Names = names;

        Grid.Columns.Add(IndexColumn());
        Grid.Columns.Add(TextColumn("Key A", nameof(Row.KeyA), 220));
        Grid.Columns.Add(TextColumn("Key B", nameof(Row.KeyB), 220));
        Grid.Columns.Add(TextColumn("Key C", nameof(Row.KeyC), 0));
        Grid.Columns.Add(TextColumn("Value", nameof(Row.Value), 130));
    }

    public static EventWorkGrid192 CreateValues(ContentControl host, EventWorkValueStorageKey192 storage, EventWorkLookup names)
        => new(host, storage, names);

    protected override bool Matches(Row row, string text)
        => row.KeyA.Contains(text, StringComparison.OrdinalIgnoreCase)
        || row.KeyB.Contains(text, StringComparison.OrdinalIgnoreCase)
        || row.KeyC.Contains(text, StringComparison.OrdinalIgnoreCase);

    public override void Load()
    {
        Rows.Clear();
        var count = Storage.Count;
        for (int i = 0; i < count; i++)
        {
            var (a, b, c) = Storage.GetKey(i);
            Rows.Add(new Row
            {
                Index = i,
                KeyA = Names.GetName(a),
                KeyB = Names.GetName(b),
                KeyC = Names.GetName(c),
                Value = Storage.GetValue(i).ToString(CultureInfo.InvariantCulture),
            });
        }
        Rebind();
    }

    public override void Save()
    {
        var count = Math.Min(Storage.Count, Rows.Count);
        for (int i = 0; i < count; i++)
        {
            var row = Rows[i];
            var v = ulong.TryParse(row.Value, CultureInfo.InvariantCulture, out var vt) ? vt : 0UL;
            Storage.SetValue(i, v);
            Storage.SetKey(i, Names.GetHash(row.KeyA.Trim()), Names.GetHash(row.KeyB.Trim()), Names.GetHash(row.KeyC.Trim()));
        }
        Storage.Compress();
    }
}
