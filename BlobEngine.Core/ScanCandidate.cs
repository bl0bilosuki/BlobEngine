namespace BlobEngine.Core;

public sealed record ScanCandidate(long Address, byte[] PreviousValue, byte[] CurrentValue);
