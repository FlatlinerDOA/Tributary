# 0029. Develop the data layer as a separable library

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0003](0003-target-the-latest-dotnet-release.md): reusable libraries also
    target the current LTS.
  - [ADR-0020](0020-make-a-dag-of-signed-events-the-source-of-truth.md) and
    [ADR-0026](0026-sync-peers-by-range-based-set-reconciliation.md): the event DAG
    and sync are the main content of the library.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

The content-addressed store, the signed event DAG and sync are meant to be reused
by other applications, and Tributary is their first consumer. Extracting a library
too early freezes an API before it has been tested in practice. Extracting it too
late lets Tributary-specific types leak into it.

## Decision

Build the data layer (storage pipeline, chunk store, event DAG, path model,
capabilities and sync) as separate assemblies and packages with no reference to
Tributary domain types. Develop them inside this repository until a second
consumer exists, then move them to their own repository.

## Alternatives considered

### Separate repository from day one

The boundary is clean, but every change crosses repositories and needs a version
bump while the API is still unstable.

### Start inside Tributary with no enforced boundary

This is fastest at first. Extraction later becomes expensive.

## Consequences

### Positive

- The API can change quickly while the boundary stays enforced.

### Negative and trade-offs

- A later move to another repository is still needed.

## Evidence

Required before acceptance:

- [ ] Identify the likely second consumer and when it will arrive.

## Fitness functions

- Architecture test: data-layer assemblies do not reference any Tributary domain
  namespace.

## Review triggers

- A second consumer starts development.
