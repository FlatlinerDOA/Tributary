var command = args.FirstOrDefault() ?? "help";
switch (command)
{
    case "verify":
    {
        var data = File.ReadAllBytes(args[1]);
        var o = new FastCdcOptions(int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]), 1, args.Length > 5 ? int.Parse(args[5]) : 1);
        Console.WriteLine(string.Join("\n", new FastCdc(o).ChunkLengths(data)));
        break;
    }
    case "synth":
        Console.WriteLine($"wrote {Synth.Build(args[1], args.Length > 2 ? int.Parse(args[2]) : 12, 42).Count} files to {args[1]}");
        break;
    case "scan":
    {
        var sizes = Option("--sizes", "16,64,256").Split(',').Select(int.Parse).ToArray();
        var files = Directory.EnumerateFiles(args[1], "*", SearchOption.AllDirectories).ToList();
        var scanner = new Scanner(Scanner.Standard(sizes));
        var done = 0;
        Parallel.ForEach(files, f =>
        {
            try { scanner.Add(f); }
            catch (Exception e) { Console.Error.WriteLine($"skip {f}: {e.Message}"); }
            var n = Interlocked.Increment(ref done);
            if (n % 500 == 0) Console.Error.WriteLine($"{n}/{files.Count}");
        });
        Console.WriteLine(Format(scanner.Report(), args[1]));
        break;
    }
    default:
        Console.WriteLine("dedup verify <file> <min> <avg> <max> [alignment] | synth <dir> [bases] | scan <dir> [--sizes 16,64,256]");
        break;
}

string Option(string name, string fallback) { var i = Array.IndexOf(args, name); return i >= 0 ? args[i + 1] : fallback; }

static string Format(ScanReport r, string root)
{
    const int indexEntryBytes = 48;
    var mib = (long b) => $"{b / 1048576.0:F1}";
    var header = new[]
    {
        $"Scanned `{Path.GetFileName(Path.GetFullPath(root))}`: {r.Files} files, {mib(r.Bytes)} MiB, of which {mib(r.PcmBytes)} MiB WAV sample data (chunked on frame boundaries).",
        "",
        "| Strategy | unique MiB | dedup ratio | saved | chunks | unique chunks | mean chunk KiB | index MiB (48 B/entry) |",
        "|---|---:|---:|---:|---:|---:|---:|---:|",
    };
    var rows = r.Totals.Select(t =>
        $"| {t.Name} | {mib(t.UniqueBytes)} | {(double)t.TotalBytes / t.UniqueBytes:F3} | {1 - (double)t.UniqueBytes / t.TotalBytes:P1} | {t.Chunks} | {t.UniqueChunks} | {t.TotalBytes / 1024.0 / t.Chunks:F1} | {t.UniqueChunks * indexEntryBytes / 1048576.0:F2} |");
    return string.Join("\n", header.Concat(rows));
}
