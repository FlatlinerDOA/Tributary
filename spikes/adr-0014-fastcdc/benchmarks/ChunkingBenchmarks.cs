using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;

/// <summary>Throughput over a 64 MiB random buffer: MiB/s = 64 / (Mean in seconds).</summary>
[MemoryDiagnoser]
public class ChunkingBenchmarks
{
    const int BufferBytes = 64 << 20;
    byte[] _data = [];
    FastCdc _cdc = null!;

    [Params(16, 64, 256)]
    public int AvgKiB { get; set; }

    [Params(1, 6)]
    public int Alignment { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[BufferBytes];
        new Random(1).NextBytes(_data);
        _cdc = new FastCdc(FastCdcOptions.ForAverage(AvgKiB * 1024, Alignment));
    }

    [Benchmark]
    public int Chunk()
    {
        var data = _data.AsSpan();
        var chunks = 0;
        for (var offset = 0; offset < data.Length; chunks++) offset += _cdc.NextCut(data[offset..]);
        return chunks;
    }
}

/// <summary>SHA-256 over the same 64 MiB in 64 KiB pieces, the per-chunk hashing cost ADR-0009 adds.</summary>
public class HashingBenchmarks
{
    readonly byte[] _data = new byte[64 << 20];
    readonly byte[] _digest = new byte[32];

    [GlobalSetup]
    public void Setup() => new Random(1).NextBytes(_data);

    [Benchmark]
    public void Sha256()
    {
        for (var offset = 0; offset < _data.Length; offset += 65536) SHA256.HashData(_data.AsSpan(offset, 65536), _digest);
    }
}
