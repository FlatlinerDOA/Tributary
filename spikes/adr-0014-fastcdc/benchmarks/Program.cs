using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(ChunkingBenchmarks).Assembly).Run(args);
