using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(EngineBenchmarks).Assembly).Run(args);
