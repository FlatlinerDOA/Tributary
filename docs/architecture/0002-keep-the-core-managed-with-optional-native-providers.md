# 0002. Keep the core managed, with optional native providers

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on: None
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Native dependencies make iOS and AOT packaging hard and bring memory-safety risk.
Several required capabilities are mostly available as native code:

- time-stretching (Rubber Band, which is GPL or commercial; signalsmith-stretch,
  which is MIT and C++)
- ML inference (ONNX Runtime)
- zstd
- plugin hosting (CLAP is a C ABI)
- audio device I/O

Later decisions on the engine, time-stretching and ML inference must work within
this principle or record why they cannot.

## Decision

Implement the domain model, storage, event log, sync and engine scheduling in
managed .NET code. Allow native code only behind Tributary interfaces, either as an
optional faster provider with a managed fallback, or for a platform boundary that
cannot be avoided (audio devices, plugin ABIs, GPU inference). Native code must
have a documented reason.

## Alternatives considered

### Native wherever it is faster

This gives the best performance, but packaging burden and memory-safety exposure
grow with every dependency.

### Strictly managed with no exceptions

This is impossible: audio devices and plugin ABIs are native.

## Consequences

### Positive

- The core stays portable, AOT-friendly and memory-safe.

### Negative and trade-offs

- Some managed algorithms will be slower, and some (such as a high-quality
  time-stretch) must be written in-house.

## Evidence

Required before acceptance:

- [ ] List every native dependency the architecture needs, with the managed fallback for
      each or a reason none is possible.
- [ ] Check the licences of candidate time-stretch libraries.

## Fitness functions

- Architecture test: P/Invoke (`DllImport` or `LibraryImport`) appears only in
  assemblies that implement a provider or platform adapter.

## Review triggers

- A needed capability has no viable managed implementation.
