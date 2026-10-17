using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;

sealed record Strategy(string Name, Func<ContentRegion, Func<ReadOnlyMemory<byte>, IEnumerable<int>>> Chunker);

sealed record StrategyTotals(string Name, long TotalBytes, long UniqueBytes, long Chunks, long UniqueChunks);

sealed record ScanReport(int Files, long Bytes, long PcmBytes, IReadOnlyList<StrategyTotals> Totals);

/// <summary>Counts unique bytes per chunking strategy, keyed by truncated SHA-256 of each chunk.</summary>
sealed class Scanner(IReadOnlyList<Strategy> strategies)
{
    const long Window = 1L << 30;

    readonly HashSet<UInt128>[] _seen = strategies.Select(_ => new HashSet<UInt128>()).ToArray();
    readonly long[] _total = new long[strategies.Count], _unique = new long[strategies.Count], _chunks = new long[strategies.Count], _uniqueChunks = new long[strategies.Count];
    int _files;
    long _bytes, _pcmBytes;

    public static IReadOnlyList<Strategy> Standard(IEnumerable<int> sizesKiB) =>
    [
        new("whole file (raw bytes)", _ => m => [m.Length]),
        new("whole content (PCM data or file)", _ => m => [m.Length]),
        .. sizesKiB.SelectMany(k => new Strategy[]
        {
            new($"fixed {k} KiB", r => m => Fixed(m.Length, k * 1024 - k * 1024 % r.Alignment)),
            new($"FastCDC avg {k} KiB", r => { var cdc = new FastCdc(FastCdcOptions.ForAverage(k * 1024, r.Alignment)); return m => cdc.ChunkLengths(m); }),
        }),
    ];

    static IEnumerable<int> Fixed(int length, int size)
    {
        for (var o = 0; o < length; o += size) yield return Math.Min(size, length - o);
    }

    public unsafe void Add(string path)
    {
        var length = new FileInfo(path).Length;
        if (length == 0) return;
        using var mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        using var view = mmf.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);
        byte* basePtr = null;
        view.SafeMemoryMappedViewHandle.AcquirePointer(ref basePtr);
        try
        {
            var memory = new UnmanagedMemory(basePtr, length);
            var region = Wav.Locate(memory.Slice(0, Math.Min(length, 1 << 16)).Span, length, path);
            var results = strategies.Select((s, i) => Chunks(memory, i == 0 ? new ContentRegion(0, length, 1, false) : region, s)).ToArray();
            lock (_seen)
            {
                _files++;
                _bytes += length;
                if (region.IsPcm) _pcmBytes += region.Length;
                for (var i = 0; i < results.Length; i++)
                    foreach (var (key, size) in results[i])
                    {
                        _total[i] += size;
                        _chunks[i]++;
                        if (_seen[i].Add(key)) { _unique[i] += size; _uniqueChunks[i]++; }
                    }
            }
        }
        finally
        {
            view.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    static List<(UInt128 Key, int Size)> Chunks(UnmanagedMemory memory, ContentRegion region, Strategy strategy)
    {
        var chunker = strategy.Chunker(region);
        var outside = new[] { (0L, region.Offset), (region.Offset + region.Length, memory.Length - region.Offset - region.Length) }
            .Where(p => p.Item2 > 0)
            .Select(p => Key(memory.Slice(p.Item1, p.Item2)));
        var inside = Enumerable.Range(0, (int)((region.Length + Window - 1) / Window))
            .Select(w => memory.Slice(region.Offset + w * Window, Math.Min(Window, region.Length - w * Window)))
            .SelectMany(window => HashChunks(window, chunker(window.Memory)));
        return outside.Concat(inside).ToList();
    }

    static IEnumerable<(UInt128, int)> HashChunks(UnmanagedMemory window, IEnumerable<int> lengths)
    {
        long offset = 0;
        foreach (var length in lengths)
        {
            yield return (Hash(window.Slice(offset, length).Span), length);
            offset += length;
        }
    }

    static (UInt128, int) Key(UnmanagedMemory m) => (Hash(m.Span), (int)Math.Min(int.MaxValue, m.Length));

    static UInt128 Hash(ReadOnlySpan<byte> data)
    {
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(data, digest);
        return new UInt128(BitConverter.ToUInt64(digest), BitConverter.ToUInt64(digest[8..]));
    }

    public ScanReport Report() => new(_files, _bytes, _pcmBytes,
        strategies.Select((s, i) => new StrategyTotals(s.Name, _total[i], _unique[i], _chunks[i], _uniqueChunks[i])).ToList());
}

/// <summary>A window over memory-mapped file bytes; slices must stay below 2 GiB to be used as spans.</summary>
readonly unsafe struct UnmanagedMemory(byte* pointer, long length)
{
    public long Length => length;
    public UnmanagedMemory Slice(long offset, long count) => new(pointer + offset, count);
    public ReadOnlySpan<byte> Span => new(pointer, checked((int)length));
    public ReadOnlyMemory<byte> Memory => new UnmanagedMemoryManager(pointer, checked((int)length)).Memory;
}

sealed unsafe class UnmanagedMemoryManager(byte* pointer, int length) : System.Buffers.MemoryManager<byte>
{
    public override Span<byte> GetSpan() => new(pointer, length);
    public override System.Buffers.MemoryHandle Pin(int elementIndex = 0) => new(pointer + elementIndex);
    public override void Unpin() { }
    protected override void Dispose(bool disposing) { }
}
