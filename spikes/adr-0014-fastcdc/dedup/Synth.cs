/// <summary>
/// Builds a sample library with known duplication: each base sample also appears re-tagged, trimmed, extended,
/// with an insert, and gain-changed, and groups of bases are bounced into one file.
/// </summary>
static class Synth
{
    const int Rate = 48000, Channels = 2;

    public static IReadOnlyList<string> Build(string dir, int bases, int seed)
    {
        Directory.CreateDirectory(dir);
        var rng = new Random(seed);
        var samples = Enumerable.Range(0, bases).Select(_ => Noise(rng, rng.Next(2 * Rate, 20 * Rate))).ToArray();
        var files = samples.SelectMany((s, i) => new SynthFile[]
        {
            new($"base-{i:D2}.wav", s, null),
            new($"base-{i:D2}-retagged.wav", s, $"Kit {i} renamed"),
            new($"base-{i:D2}-trimmed.wav", s[(Channels * rng.Next(500, 5000))..], null),
            new($"base-{i:D2}-extended.wav", [.. s, .. Noise(rng, Rate / 2)], null),
            new($"base-{i:D2}-insert.wav", Insert(s, Noise(rng, Rate / 4), Channels * (s.Length / Channels / 2)), null),
            new($"base-{i:D2}-gain.wav", s.Select(v => v / 2).ToArray(), null),
        })
        .Concat(samples.Chunk(3).Select((group, g) => new SynthFile($"bounce-{g:D2}.wav", group.SelectMany(x => x).ToArray(), null)))
        .ToList();
        foreach (var f in files) Wav.Write(Path.Combine(dir, f.Name), f.Frames, Channels, Rate, f.Title);
        return files.Select(f => f.Name).ToList();
    }

    static int[] Insert(int[] s, int[] insert, int at) => [.. s[..at], .. insert, .. s[at..]];

    static int[] Noise(Random rng, int frames)
    {
        var data = new int[frames * Channels];
        double state = 0;
        for (var i = 0; i < data.Length; i++)
        {
            state = 0.97 * state + 0.03 * (rng.NextDouble() * 2 - 1);
            data[i] = (int)(state * 4_000_000);
        }
        return data;
    }
}

sealed record SynthFile(string Name, int[] Frames, string? Title);
