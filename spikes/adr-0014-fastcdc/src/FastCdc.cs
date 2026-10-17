using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

/// <param name="Alignment">Each content-defined cut point is moved forward to the next multiple of this many bytes
/// (the audio frame size). Inserting or removing whole frames still shifts boundaries with the content, and the
/// chunk size distribution stays that of unaligned FastCDC.</param>
public sealed record FastCdcOptions(int MinSize, int AvgSize, int MaxSize, int Normalization = 1, int Alignment = 1)
{
    public static FastCdcOptions ForAverage(int avgSize, int alignment = 1) => new(avgSize / 4, avgSize, avgSize * 4, 1, alignment);
}

/// <summary>
/// FastCDC 2020 (Xia et al.), matching the cut points of the Rust <c>fastcdc</c> crate's v2020 chunker when
/// <see cref="FastCdcOptions.Alignment"/> is 1.
/// </summary>
public sealed class FastCdc
{
    static readonly ulong[] Masks =
    [
        0, 0, 0, 0, 0,
        0x0000000001804110, 0x0000000001803110, 0x0000000018035100, 0x0000001800035300, 0x0000019000353000,
        0x0000590003530000, 0x0000d90003530000, 0x0000d90103530000, 0x0000d90303530000, 0x0000d90313530000,
        0x0000d90f03530000, 0x0000d90303537000, 0x0000d90703537000, 0x0000d90707537000, 0x0000d91707537000,
        0x0000d91747537000, 0x0000d91767537000, 0x0000d93767537000, 0x0000d93777537000, 0x0000d93777577000,
        0x0000db3777577000,
    ];

    static readonly ulong[] Gear = Enumerable.Range(0, 256)
        .Select(i => BinaryPrimitives.ReadUInt64BigEndian(MD5.HashData(Enumerable.Repeat((byte)i, 64).ToArray())))
        .ToArray();

    readonly int _min, _avg, _max, _alignment;
    readonly ulong _maskS, _maskL;

    public FastCdc(FastCdcOptions options)
    {
        var bits = (int)Math.Round(Math.Log2(options.AvgSize));
        _alignment = options.Alignment;
        _min = options.MinSize;
        _avg = options.AvgSize;
        _max = options.MaxSize - options.MaxSize % options.Alignment;
        _maskS = Masks[bits + options.Normalization];
        _maskL = Masks[bits - options.Normalization];
    }

    public static ulong GearValue(int index) => Gear[index];

    /// <summary>Length of the next chunk at the start of <paramref name="source"/>.</summary>
    public int NextCut(ReadOnlySpan<byte> source) => _alignment == 1 ? NextCutUnaligned(source) : NextCutAligned(source);

    int NextCutUnaligned(ReadOnlySpan<byte> source)
    {
        var remaining = source.Length;
        if (remaining <= _min) return remaining;
        var center = _avg;
        if (remaining > _max) remaining = _max;
        else if (remaining < center) center = remaining;
        var gear = Gear;
        ulong hash = 0;
        int i = _min & ~1, centerEnd = center & ~1, end = remaining & ~1;
        for (; i < centerEnd; i++)
        {
            hash = (hash << 1) + gear[source[i]];
            if ((hash & _maskS) == 0) return i;
        }
        for (; i < end; i++)
        {
            hash = (hash << 1) + gear[source[i]];
            if ((hash & _maskL) == 0) return i;
        }
        return remaining;
    }

    int NextCutAligned(ReadOnlySpan<byte> source)
    {
        var cut = NextCutUnaligned(source);
        var aligned = (cut + _alignment - 1) / _alignment * _alignment;
        return Math.Min(aligned, source.Length);
    }
}

public static class FastCdcExtensions
{
    /// <summary>Chunk lengths covering the whole of <paramref name="data"/>.</summary>
    public static IEnumerable<int> ChunkLengths(this FastCdc cdc, ReadOnlyMemory<byte> data)
    {
        for (var offset = 0; offset < data.Length;)
        {
            var length = cdc.NextCut(data.Span[offset..]);
            yield return length;
            offset += length;
        }
    }
}
