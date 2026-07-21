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

public enum Endianness
{
    LittleEndian,
    BigEndian,
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
    public Endianness Endianness { get; set; }
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

    internal static byte[] Parse(string text, ValueTypeKind type, Endianness endianness)
    {
        var bytes = type switch
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

        ApplyEndianness(bytes, endianness);
        return bytes;
    }

    internal static string Format(ReadOnlySpan<byte> bytes, ValueTypeKind type, Endianness endianness)
    {
        var nativeBytes = bytes.ToArray();
        ApplyEndianness(nativeBytes, endianness);

        return type switch
        {
            ValueTypeKind.uByte => nativeBytes[0].ToString(CultureInfo.InvariantCulture),
            ValueTypeKind.Byte => nativeBytes[0].ToString(CultureInfo.InvariantCulture),
            ValueTypeKind.UShort => BitConverter.ToUInt16(nativeBytes).ToString(CultureInfo.InvariantCulture),
            ValueTypeKind.Short => BitConverter.ToInt16(nativeBytes).ToString(CultureInfo.InvariantCulture),
            ValueTypeKind.UInt32 => BitConverter.ToUInt32(nativeBytes).ToString(CultureInfo.InvariantCulture),
            ValueTypeKind.Int32 => BitConverter.ToInt32(nativeBytes).ToString(CultureInfo.InvariantCulture),
            ValueTypeKind.ULong => BitConverter.ToUInt64(nativeBytes).ToString(CultureInfo.InvariantCulture),
            ValueTypeKind.Int64 => BitConverter.ToInt64(nativeBytes).ToString(CultureInfo.InvariantCulture),
            ValueTypeKind.Float => BitConverter.ToSingle(nativeBytes).ToString("G9", CultureInfo.InvariantCulture),
            ValueTypeKind.Double => BitConverter.ToDouble(nativeBytes).ToString("G17", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    private static void ApplyEndianness(Span<byte> bytes, Endianness endianness)
    {
        var requestedLittleEndian = endianness == Endianness.LittleEndian;
        if (bytes.Length > 1 && BitConverter.IsLittleEndian != requestedLittleEndian)
            bytes.Reverse();
    }
}
