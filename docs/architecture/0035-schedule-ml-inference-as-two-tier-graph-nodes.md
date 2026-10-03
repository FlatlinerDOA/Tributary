# 0035. Schedule ML inference as two-tier graph nodes

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0002](0002-keep-the-core-managed-with-optional-native-providers.md):
    inference runtimes are a native provider.
  - [ADR-0014](0014-store-blobs-as-content-defined-chunks.md): frozen outputs are
    stored as chunks.
  - [ADR-0019](0019-authorize-actors-with-path-scoped-capabilities.md): compute
    budgets are capability caveats.
  - [ADR-0031](0031-swap-immutable-graph-snapshots-to-the-realtime-thread.md): ML
    nodes are scheduled in the graph.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Models range from tiny real-time effects to heavy separation and generation models
that take seconds per bar on a GPU.

Points to consider:

- GPU inference is often non-deterministic, so the same input can give different
  outputs. Storing the outputs (automatic freezing) is the only way to reproduce a
  result.
- Inference runtimes such as ONNX Runtime are native.
- Look-ahead nodes add latency that delay compensation has to absorb.

## Decision

Treat ML inference as a graph node type with two tiers. Tier 1 runs small,
RT-safe models inline on the CPU within the buffer deadline. Tier 2 runs heavy
models as look-ahead or background jobs on the GPU or NPU, and their output is
automatically frozen and stored as content-addressed chunks. Background jobs are
limited by the compute budget in the requesting actor's capability, and RT audio
always takes priority.

## Alternatives considered

### All ML offline only

This is simple and deterministic. It rules out real-time ML effects and live agent
performance.

### All ML inline

The model is uniform. Heavy models cannot meet RT deadlines.

## Consequences

### Positive

- Results can be reproduced through frozen outputs. RT safety is preserved.

### Negative and trade-offs

- Frozen outputs use storage. A native inference runtime is needed.

## Evidence

Required before acceptance:

- [ ] Measure inference latency of a representative tier-1 model on the CPU at
      64-sample buffers.
- [ ] Choose an inference runtime and check its AOT and iOS support.

## Fitness functions

- Test: rendering a project with tier-2 nodes twice gives identical output
  (because outputs are frozen).

## Review triggers

- Hardware makes heavy models RT-capable.
