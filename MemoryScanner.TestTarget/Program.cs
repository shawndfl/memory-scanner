using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

const int BlockSize = 288;
const int Spacing = 16;

var values = new TestValue[]
{
    new("ub", "uByte", 0 * Spacing, 1, false, "201", WriteByte, ReadByte),
    new("b", "Byte", 1 * Spacing, 1, false, "102", WriteByte, ReadByte),
    new("us", "UShort", 2 * Spacing, 2, false, "50001", WriteUInt16, ReadUInt16),
    new("s", "Short", 3 * Spacing, 2, false, "-12345", WriteInt16, ReadInt16),
    new("ui", "UInt32", 4 * Spacing, 4, false, "3000000001", WriteUInt32, ReadUInt32),
    new("i", "Int32", 5 * Spacing, 4, false, "-123456789", WriteInt32, ReadInt32),
    new("ul", "ULong", 6 * Spacing, 8, false, "12000000000000000001", WriteUInt64, ReadUInt64),
    new("l", "Int64", 7 * Spacing, 8, false, "-1234567890123456789", WriteInt64, ReadInt64),
    new("f", "Float", 8 * Spacing, 4, false, "123.25", WriteSingle, ReadSingle),
    new("d", "Double", 9 * Spacing, 8, false, "9876.125", WriteDouble, ReadDouble),
    new("usbe", "UShort", 10 * Spacing, 2, true, "54321", WriteUInt16, ReadUInt16),
    new("sbe", "Short", 11 * Spacing, 2, true, "-23456", WriteInt16, ReadInt16),
    new("uibe", "UInt32", 12 * Spacing, 4, true, "4000000001", WriteUInt32, ReadUInt32),
    new("ibe", "Int32", 13 * Spacing, 4, true, "-987654321", WriteInt32, ReadInt32),
    new("ulbe", "ULong", 14 * Spacing, 8, true, "15000000000000000001", WriteUInt64, ReadUInt64),
    new("lbe", "Int64", 15 * Spacing, 8, true, "-2234567890123456789", WriteInt64, ReadInt64),
    new("fbe", "Float", 16 * Spacing, 4, true, "456.75", WriteSingle, ReadSingle),
    new("dbe", "Double", 17 * Spacing, 8, true, "54321.625", WriteDouble, ReadDouble),
};

var block = Marshal.AllocHGlobal(BlockSize);
try
{
    for (var offset = 0; offset < BlockSize; offset++)
        Marshal.WriteByte(block, offset, 0xCC);

    foreach (var value in values)
        value.WriteValue(block + value.Offset, value.InitialValue);

    Console.WriteLine("MemoryScanner test target");
    Console.WriteLine($"Process: {Process.GetCurrentProcess().ProcessName}");
    Console.WriteLine($"PID:     {Environment.ProcessId}");
    Console.WriteLine("The values below are stored in persistent unmanaged memory.");

    while (true)
    {
        PrintValues();
        Console.Write("Command (<key> <value>, show, or q): ");
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input))
            continue;
        if (input.Equals("q", StringComparison.OrdinalIgnoreCase))
            break;
        if (input.Equals("show", StringComparison.OrdinalIgnoreCase))
            continue;

        var parts = input.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
        var selected = parts.Length == 2
            ? values.FirstOrDefault(value => value.Key.Equals(parts[0], StringComparison.OrdinalIgnoreCase))
            : null;

        if (selected is null)
        {
            Console.WriteLine("Unknown command. Example: i 250");
            continue;
        }

        try
        {
            selected.WriteValue(block + selected.Offset, parts[1]);
            Console.WriteLine($"Updated {selected.Name}.");
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            Console.WriteLine($"Invalid {selected.Name} value: {exception.Message}");
        }
    }

    void PrintValues()
    {
        Console.WriteLine();
        Console.WriteLine("Key   Type     Order   Address             Current value");
        Console.WriteLine("----  -------  ------  ------------------  --------------------");
        foreach (var value in values)
        {
            var address = block + value.Offset;
            var order = value.BigEndian ? "Big" : "Little";
            Console.WriteLine($"{value.Key,-4}  {value.Name,-7}  {order,-6}  0x{address.ToInt64():X16}  {value.ReadValue(address)}");
        }
        Console.WriteLine();
    }
}
finally
{
    Marshal.FreeHGlobal(block);
}

static void WriteByte(nint address, string text) =>
    Marshal.WriteByte(address, byte.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
static string ReadByte(nint address) => Marshal.ReadByte(address).ToString(CultureInfo.InvariantCulture);

static void WriteUInt16(nint address, string text) =>
    Marshal.WriteInt16(address, unchecked((short)ushort.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture)));
static string ReadUInt16(nint address) => unchecked((ushort)Marshal.ReadInt16(address)).ToString(CultureInfo.InvariantCulture);

static void WriteInt16(nint address, string text) =>
    Marshal.WriteInt16(address, short.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
static string ReadInt16(nint address) => Marshal.ReadInt16(address).ToString(CultureInfo.InvariantCulture);

static void WriteUInt32(nint address, string text) =>
    Marshal.WriteInt32(address, unchecked((int)uint.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture)));
static string ReadUInt32(nint address) => unchecked((uint)Marshal.ReadInt32(address)).ToString(CultureInfo.InvariantCulture);

static void WriteInt32(nint address, string text) =>
    Marshal.WriteInt32(address, int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
static string ReadInt32(nint address) => Marshal.ReadInt32(address).ToString(CultureInfo.InvariantCulture);

static void WriteUInt64(nint address, string text) =>
    Marshal.WriteInt64(address, unchecked((long)ulong.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture)));
static string ReadUInt64(nint address) => unchecked((ulong)Marshal.ReadInt64(address)).ToString(CultureInfo.InvariantCulture);

static void WriteInt64(nint address, string text) =>
    Marshal.WriteInt64(address, long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
static string ReadInt64(nint address) => Marshal.ReadInt64(address).ToString(CultureInfo.InvariantCulture);

static void WriteSingle(nint address, string text) =>
    Marshal.WriteInt32(address, BitConverter.SingleToInt32Bits(float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)));
static string ReadSingle(nint address) => BitConverter.Int32BitsToSingle(Marshal.ReadInt32(address)).ToString("G9", CultureInfo.InvariantCulture);

static void WriteDouble(nint address, string text) =>
    Marshal.WriteInt64(address, BitConverter.DoubleToInt64Bits(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)));
static string ReadDouble(nint address) => BitConverter.Int64BitsToDouble(Marshal.ReadInt64(address)).ToString("G17", CultureInfo.InvariantCulture);

internal sealed record TestValue(
    string Key,
    string Name,
    int Offset,
    int Size,
    bool BigEndian,
    string InitialValue,
    Action<nint, string> Write,
    Func<nint, string> Read)
{
    internal void WriteValue(nint address, string text)
    {
        Write(address, text);
        ReverseIfBigEndian(address);
    }

    internal string ReadValue(nint address)
    {
        ReverseIfBigEndian(address);
        try
        {
            return Read(address);
        }
        finally
        {
            ReverseIfBigEndian(address);
        }
    }

    private void ReverseIfBigEndian(nint address)
    {
        if (!BigEndian || Size <= 1)
            return;

        for (var left = 0; left < Size / 2; left++)
        {
            var right = Size - left - 1;
            var leftByte = Marshal.ReadByte(address, left);
            var rightByte = Marshal.ReadByte(address, right);
            Marshal.WriteByte(address, left, rightByte);
            Marshal.WriteByte(address, right, leftByte);
        }
    }
}
