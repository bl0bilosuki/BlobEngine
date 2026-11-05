using BlobEngine.Core;
using BlobEngine.Memory;

namespace BlobEngine.Scanner;

public sealed class MemoryScanner
{
    private readonly ProcessMemory _memory;
    public MemoryScanner(ProcessMemory memory) => _memory = memory;

    public async Task<IReadOnlyList<ScanCandidate>> FirstScanAsync(byte[] value, int maxResults = 5000,
        ulong maxBytes = 512UL * 1024 * 1024, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (value.Length == 0) throw new ArgumentException("Search value cannot be empty.", nameof(value));
        if (maxResults < 1) throw new ArgumentOutOfRangeException(nameof(maxResults));
        return await Task.Run(() =>
        {
            var results = new List<ScanCandidate>();
            var buffer = new byte[1024 * 1024];
            ulong scanned = 0;
            foreach (var region in _memory.EnumerateReadableRegions(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ulong offset = 0;
                while (offset < region.Size && scanned < maxBytes && results.Count < maxResults)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var request = (int)Math.Min((ulong)buffer.Length, Math.Min(region.Size - offset, maxBytes - scanned));
                    if (request <= 0) break;
                    if (!_memory.TryRead(checked(region.BaseAddress + (long)offset), buffer, out var read) || read <= 0)
                    { offset += (ulong)request; scanned += (ulong)request; continue; }
                    var limit = read - value.Length;
                    for (var i = 0; i <= limit && results.Count < maxResults; i++)
                    {
                        if (buffer.AsSpan(i, value.Length).SequenceEqual(value))
                        {
                            var found = buffer.AsSpan(i, value.Length).ToArray();
                            results.Add(new ScanCandidate(checked(region.BaseAddress + (long)offset + i), found, found));
                        }
                    }
                    offset += (ulong)read; scanned += (ulong)read;
                    progress?.Report(Math.Min(1d, (double)scanned / maxBytes));
                }
                if (scanned >= maxBytes || results.Count >= maxResults) break;
            }
            return (IReadOnlyList<ScanCandidate>)results;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ScanCandidate>> NextScanAsync(IEnumerable<ScanCandidate> previous, ScanMode mode,
        byte[]? exactValue = null, byte[]? upperValue = null, int maxResults = 5000,
        CancellationToken cancellationToken = default)
    {
        var snapshot = previous.ToArray();
        return await Task.Run(() =>
        {
            var results = new List<ScanCandidate>();
            foreach (var candidate in snapshot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = new byte[candidate.PreviousValue.Length];
                if (!_memory.TryRead(candidate.Address, current, out var read) || read != current.Length) continue;
                if (Matches(candidate.PreviousValue, current, mode, exactValue, upperValue))
                    results.Add(new ScanCandidate(candidate.Address, current, current));
                if (results.Count >= maxResults) break;
            }
            return (IReadOnlyList<ScanCandidate>)results;
        }, cancellationToken);
    }

    private static bool Matches(byte[] previous, byte[] current, ScanMode mode, byte[]? exact, byte[]? upper)
    {
        if (mode == ScanMode.Changed) return !previous.AsSpan().SequenceEqual(current);
        if (mode == ScanMode.Unchanged) return previous.AsSpan().SequenceEqual(current);
        if (mode == ScanMode.Exact) return exact is not null && current.AsSpan().SequenceEqual(exact);
        if (previous.Length is not (1 or 2 or 4 or 8) || current.Length != previous.Length) return false;
        var p = ToNumber(previous); var c = ToNumber(current);
        return mode switch
        {
            ScanMode.GreaterThan => exact is not null && c > ToNumber(exact),
            ScanMode.LessThan => exact is not null && c < ToNumber(exact),
            ScanMode.Between => exact is not null && upper is not null && c >= ToNumber(exact) && c <= ToNumber(upper),
            ScanMode.Increased => c > p,
            ScanMode.Decreased => c < p,
            ScanMode.UnknownInitial => true,
            _ => false
        };
    }

    private static double ToNumber(byte[] bytes) => bytes.Length switch
    {
        1 => bytes[0], 2 => BitConverter.ToInt16(bytes), 4 => BitConverter.ToInt32(bytes),
        8 => BitConverter.ToInt64(bytes), _ => double.NaN
    };
}
