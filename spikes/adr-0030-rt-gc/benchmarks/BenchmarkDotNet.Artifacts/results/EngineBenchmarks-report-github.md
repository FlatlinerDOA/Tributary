```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Xeon Processor 2.80GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.112
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method       | Tracks | Stages | Mean        | Error     | StdDev    | Allocated |
|------------- |------- |------- |------------:|----------:|----------:|----------:|
| **ProcessBlock** | **200**    | **1**      |    **81.33 μs** |  **80.22 μs** |  **4.397 μs** |         **-** |
| **ProcessBlock** | **200**    | **22**     |   **770.43 μs** | **235.50 μs** | **12.908 μs** |         **-** |
| **ProcessBlock** | **200**    | **50**     | **1,835.55 μs** | **633.18 μs** | **34.707 μs** |         **-** |
