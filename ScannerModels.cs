using System.Globalization;

namespace MemoryScanner;

internal readonly record struct ScanProgress(int MatchCount, double Percentage);

public enum ValueTypeKind
{
    uByte,
    Byte,
    UShort,
    Short,
    UInt32,
    Int32,
    ULong,
    Int64,
    Float,
    Double,
}

public sealed record ProcessItem(int Id, string Name)
{
    public string DisplayName => $"{Name} ({Id})";
}

public sealed class ScanResult
{
    public required long Address { get; init; }
    public required string Value { get; set; }
    public string AddressText => $"0x{Address:X}";
}

public sealed class SavedAddress
{
    public string Description { get; set; } = "Saved value";
    public string ProcessName { get; set; } = string.Empty;
    public long Address { get; set; }
    public ValueTypeKind ValueType { get; set; }
    public string AddressText => $"0x{Address:X}";
}

internal static class ValueConverter
{
    internal static int SizeOf(ValueTypeKind type) => type switch
    {
        ValueTypeKind.uByte => sizeof(byte),
        ValueTypeKind.Byte => sizeof(byte),
        ValueTypeKind.UShort => sizeof(short),
        ValueTypeKind.Short => sizeof(short),
        ValueTypeKind.UInt32 => sizeof(int),
        ValueTypeKind.Int32 => sizeof(int),
        ValueTypeKind.ULong => sizeof(long),
        ValueTypeKind.Int64 => sizeof(long),
        ValueTypeKind.Float => sizeof(float),
        ValueTypeKind.Double => sizeof(double),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    internal static byte[] Parse(string text, ValueTypeKind type) => type switch
    {
        ValueTypeKind.uByte => [byte.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture)],
        ValueTypeKind.Byte => [byte.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture)],
        ValueTypeKind.UShort => BitConverter.GetBytes(ushort.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture)),
        ValueTypeKind.Short => BitConverter.GetBytes(short.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture)),
        ValueTypeKind.UInt32 => BitConverter.GetBytes(uint.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture)),
        ValueTypeKind.Int32 => BitConverter.GetBytes(int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture)),
        ValueTypeKind.ULong => BitConverter.GetBytes(ulong.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture)),
        ValueTypeKind.Int64 => BitConverter.GetBytes(long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture)),
        ValueTypeKind.Float => BitConverter.GetBytes(float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)),
        ValueTypeKind.Double => BitConverter.GetBytes(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    internal static string Format(ReadOnlySpan<byte> bytes, ValueTypeKind type) => type switch
    {
        ValueTypeKind.uByte => bytes[0].ToString(CultureInfo.InvariantCulture),
        ValueTypeKind.Byte => bytes[0].ToString(CultureInfo.InvariantCulture),
        ValueTypeKind.UShort => BitConverter.ToUInt16(bytes).ToString(CultureInfo.InvariantCulture),
        ValueTypeKind.Short => BitConverter.ToInt16(bytes).ToString(CultureInfo.InvariantCulture),
        ValueTypeKind.UInt32 => BitConverter.ToUInt32(bytes).ToString(CultureInfo.InvariantCulture),
        ValueTypeKind.Int32 => BitConverter.ToInt32(bytes).ToString(CultureInfo.InvariantCulture),
        ValueTypeKind.ULong => BitConverter.ToUInt64(bytes).ToString(CultureInfo.InvariantCulture),
        ValueTypeKind.Int64 => BitConverter.ToInt64(bytes).ToString(CultureInfo.InvariantCulture),
        ValueTypeKind.Float => BitConverter.ToSingle(bytes).ToString("G9", CultureInfo.InvariantCulture),
        ValueTypeKind.Double => BitConverter.ToDouble(bytes).ToString("G17", CultureInfo.InvariantCulture),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
