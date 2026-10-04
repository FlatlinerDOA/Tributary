# 0101. Target the latest .NET release

- Layer: L0 Primitives
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0100](0100-keep-the-core-managed-with-optional-native-providers.md): the
    managed core is bound to the .NET runtime.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

The managed core is bound to the .NET runtime, and the choice of release determines
which language features and BCL APIs every later decision can use. Examples:

- `Guid.CreateVersion7()` (.NET 9 and later)
- post-quantum `MLDsa`/`MLKem` (.NET 10, platform dependent)
- C# 15 union types (.NET 11), for modelling closed sets of alternatives such as
  event bodies

Facts:

- .NET 10 is an LTS release, supported until November 2028.
- .NET 11 is an STS release, with general availability due in November 2026.
  Since September 2025, STS releases are supported for 24 months, so .NET 11's
  support also ends around November 2028.
- .NET 12 (November 2027) is expected to be the next LTS.
- C# 15 union types reached the runtime support types (`UnionAttribute`, `IUnion`)
  in .NET 11 Preview 5. One analysis reports that union cases that are value types
  are boxed, which matters for hot paths.
- Some Tributary libraries are meant to be reused by other applications, which may
  be on an older LTS.
- iOS, Android and NativeAOT support must be confirmed for each new release.

## Decision

1. **Applications** (engine, CLI, GUI) target the latest generally available .NET
   release: `net11.0` from its GA in November 2026. They move to each new major
   release within three months of its GA, after the platform checks below pass.
2. **Reusable libraries** target the latest release and the current LTS (`net11.0`
   and `net10.0` until .NET 12). Features unavailable on the LTS target are kept
   behind `#if` or confined to application code.
3. **No preview features on the main branch.** `LangVersion` is never `preview`.
   Preview SDKs are used only on spike branches.
4. **Union types** may be used once .NET 11 is generally available. Hot paths
   (engine messages, high-rate streams and decoding) use them only after the
   boxing behaviour is measured.
5. **Never ship on a release with less than six months of support left.**

## Alternatives considered

### Stay on .NET 10 LTS until .NET 12

It is the most conservative choice. There are no union types until late 2027, so
closed sets of alternatives would be modelled as class hierarchies and later
migrated.

### Target .NET 11 previews now

Unions would be available immediately. The syntax can still change before GA, and
code would be built on an unstable compiler.

### Target the latest release for libraries too

There is one target framework. Consumers still on the LTS cannot use the
libraries.

## Consequences

### Positive

- Union types and current BCL APIs are available to the code being written now.
- With 24-month STS support, following each release costs no more support time
  than staying on the LTS.

### Negative and trade-offs

- An upgrade every year, each needing platform checks (iOS, Android, NativeAOT).
- Libraries carry two target frameworks and conditional code.

## Evidence

Available:

- [.NET support policy and downloads](https://dotnet.microsoft.com/en-us/download/dotnet)
- [Explore union types in C# 15 – .NET Blog](https://devblogs.microsoft.com/dotnet/csharp-15-union-types/)

Required before acceptance:

- [ ] Confirm .NET 11 GA date and support end date once it is released.
- [ ] Check iOS and Android workloads and NativeAOT with .NET 11: startup and
      trimming.
- [ ] Measure boxing for union cases that are value types under the final .NET 11
      compiler.
- [ ] Confirm that third-party dependencies support `net11.0` and `net10.0`.

## Fitness functions

- CI check: application projects target the latest GA framework, and library
  projects target that framework plus the current LTS.
- CI check: no project sets `LangVersion` to `preview` on the main branch.
- Build matrix: libraries build and pass their tests on every targeted framework.

## Review triggers

- A new .NET major release reaches GA.
- Microsoft changes the STS or LTS support periods.
- A target platform drops support for a .NET release.

## Notes

- 2026-10-04: Renumbered from the former ADR-0003 when ADRs were grouped by layer.
