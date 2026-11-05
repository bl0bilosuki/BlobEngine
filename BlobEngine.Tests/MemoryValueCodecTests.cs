using BlobEngine.Core;
using Xunit;

namespace BlobEngine.Tests;

public sealed class MemoryValueCodecTests
{
    [Fact] public void Int32_RoundTrips() { var bytes = MemoryValueCodec.Parse("123456", MemoryDataType.Int32); Assert.Equal("123456", MemoryValueCodec.Format(bytes, MemoryDataType.Int32)); }
    [Fact] public void Float_RoundTrips() { var bytes = MemoryValueCodec.Parse("12.5", MemoryDataType.Single); Assert.Equal("12.5", MemoryValueCodec.Format(bytes, MemoryDataType.Single)); }
    [Fact] public void HexBytes_Parse() { Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, MemoryValueCodec.Parse("DE AD BE EF", MemoryDataType.ArrayOfBytes)); }
    [Fact] public void Byte_RejectsOverflow() { Assert.Throws<OverflowException>(() => MemoryValueCodec.Parse("256", MemoryDataType.Byte)); }
}
