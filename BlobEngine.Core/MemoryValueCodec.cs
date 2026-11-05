using System.Globalization;

namespace BlobEngine.Core;

public enum MemoryDataType { Byte, Int16, Int32, Int64, Single, Double, String, ArrayOfBytes }
public enum ScanMode { Exact, GreaterThan, LessThan, Between, UnknownInitial, Increased, Decreased, Changed, Unchanged }

public static class MemoryValueCodec
{
    public static byte[] Parse(string input, MemoryDataType type)
    {
        ArgumentNullException.ThrowIfNull(input);
        return type switch
        {
            MemoryDataType.Byte => new[] { byte.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture) },
            MemoryDataType.Int16 => BitConverter.GetBytes(short.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture)),
            MemoryDataType.Int32 => BitConverter.GetBytes(int.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture)),
            MemoryDataType.Int64 => BitConverter.GetBytes(long.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture)),
            MemoryDataType.Single => BitConverter.GetBytes(float.Parse(input, NumberStyles.Float, CultureInfo.InvariantCulture)),
            MemoryDataType.Double => BitConverter.GetBytes(double.Parse(input, NumberStyles.Float, CultureInfo.InvariantCulture)),
            MemoryDataType.String => System.Text.Encoding.UTF8.GetBytes(input),
            MemoryDataType.ArrayOfBytes => ParseBytes(input),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    public static string Format(ReadOnlySpan<byte> bytes, MemoryDataType type)
    {
        return type switch
        {
            MemoryDataType.Byte when bytes.Length >= 1 => bytes[0].ToString(CultureInfo.InvariantCulture),
            MemoryDataType.Int16 when bytes.Length >= 2 => BitConverter.ToInt16(bytes).ToString(CultureInfo.InvariantCulture),
            MemoryDataType.Int32 when bytes.Length >= 4 => BitConverter.ToInt32(bytes).ToString(CultureInfo.InvariantCulture),
            MemoryDataType.Int64 when bytes.Length >= 8 => BitConverter.ToInt64(bytes).ToString(CultureInfo.InvariantCulture),
            MemoryDataType.Single when bytes.Length >= 4 => BitConverter.ToSingle(bytes).ToString("G", CultureInfo.InvariantCulture),
            MemoryDataType.Double when bytes.Length >= 8 => BitConverter.ToDouble(bytes).ToString("G", CultureInfo.InvariantCulture),
            MemoryDataType.String => System.Text.Encoding.UTF8.GetString(bytes),
            MemoryDataType.ArrayOfBytes => Convert.ToHexString(bytes),
            _ => throw new ArgumentException("Buffer is too small for the selected data type.", nameof(bytes))
        };
    }

    public static int GetSize(MemoryDataType type, string? stringValue = null) => type switch
    {
        MemoryDataType.Byte => 1, MemoryDataType.Int16 => 2, MemoryDataType.Int32 or MemoryDataType.Single => 4,
        MemoryDataType.Int64 or MemoryDataType.Double => 8,
        MemoryDataType.String => System.Text.Encoding.UTF8.GetByteCount(stringValue ?? ""),
        MemoryDataType.ArrayOfBytes => ParseBytes(stringValue ?? "").Length,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static byte[] ParseBytes(string input)
    {
        var normalized = input.Replace("0x", "", StringComparison.OrdinalIgnoreCase)
            .Replace(",", " ").Replace("-", " ");
        var parts = normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new FormatException("Enter hexadecimal bytes, e.g. 'DE AD BE EF'.");
        return parts.Select(p => Convert.ToByte(p, 16)).ToArray();
    }
}
