using System.Buffers.Binary;
using System.Text;

/// <summary>Location of PCM sample data inside a file; non-WAV files are one opaque region.</summary>
sealed record ContentRegion(long Offset, long Length, int Alignment, bool IsPcm);

static class Wav
{
    public static ContentRegion Locate(ReadOnlySpan<byte> head, long fileLength, string path)
    {
        var opaque = new ContentRegion(0, fileLength, 1, false);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is not (".wav" or ".wave") || head.Length < 12 || !head[..4].SequenceEqual("RIFF"u8) || !head[8..12].SequenceEqual("WAVE"u8))
            return opaque;
        int? blockAlign = null;
        for (long pos = 12; pos + 8 <= Math.Min(head.Length, fileLength);)
        {
            var id = head.Slice((int)pos, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(head.Slice((int)pos + 4, 4));
            if (id.SequenceEqual("fmt "u8) && pos + 22 <= head.Length) blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(head.Slice((int)pos + 20, 2));
            if (id.SequenceEqual("data"u8))
                return blockAlign is > 0 ? new ContentRegion(pos + 8, Math.Min(size, fileLength - pos - 8), blockAlign.Value, true) : opaque;
            pos += 8 + size + (size & 1);
        }
        return opaque;
    }

    /// <summary>Writes 24-bit PCM; <paramref name="frames"/> holds interleaved samples as ints.</summary>
    public static void Write(string path, ReadOnlySpan<int> frames, int channels, int sampleRate, string? title = null)
    {
        const int bytesPerSample = 3;
        var info = title is null ? [] : InfoChunk(title);
        var dataBytes = frames.Length * bytesPerSample;
        using var w = new BinaryWriter(File.Create(path));
        w.Write("RIFF"u8); w.Write(4 + 24 + info.Length + 8 + dataBytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)channels); w.Write(sampleRate);
        w.Write(sampleRate * channels * bytesPerSample); w.Write((short)(channels * bytesPerSample)); w.Write((short)(bytesPerSample * 8));
        w.Write(info);
        w.Write("data"u8); w.Write(dataBytes);
        Span<byte> s = stackalloc byte[3];
        foreach (var v in frames) { s[0] = (byte)v; s[1] = (byte)(v >> 8); s[2] = (byte)(v >> 16); w.Write(s); }
    }

    static byte[] InfoChunk(string title)
    {
        var text = Encoding.ASCII.GetBytes(title + "\0");
        var padded = text.Length + (text.Length & 1);
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("LIST"u8); w.Write(4 + 8 + padded); w.Write("INFO"u8);
        w.Write("INAM"u8); w.Write(text.Length); w.Write(text); if ((text.Length & 1) == 1) w.Write((byte)0);
        return ms.ToArray();
    }
}
