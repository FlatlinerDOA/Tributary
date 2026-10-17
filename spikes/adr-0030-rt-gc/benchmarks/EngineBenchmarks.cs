using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class EngineBenchmarks
{
    Engine _engine = null!;

    [Params(200)]
    public int Tracks { get; set; }

    [Params(1, 22, 50)]
    public int Stages { get; set; }

    [GlobalSetup]
    public void Setup() => _engine = new Engine(Tracks, Stages);

    [Benchmark]
    public void ProcessBlock() => _engine.Process();
}
