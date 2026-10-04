# 0701. Make API serialization pluggable

- Layer: L6 Host and API
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0102](0102-encode-persisted-data-as-deterministic-cbor.md): the CBOR codec
    reuses the persisted encoding.
  - [ADR-0103](0103-evolve-schemas-additively.md): the evolution rules every codec
    follows.
  - [ADR-0105](0105-generate-schemas-from-csharp-types.md): field IDs and names are
    declared with the same attributes as persisted types.
  - [ADR-0700](0700-drive-the-engine-through-one-typed-api.md): the API whose
    contract this defines.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

The typed API is used by very different clients:

- The GUI and the meter, waveform and playhead streams need compact binary
  encodings.
- Scripts and third-party controllers benefit from mature code generation in many
  languages (Protobuf).
- AI agents and MCP use JSON-RPC.
- Some consumers share code with the persisted CBOR model.

API messages are never persisted, so canonical encoding does not matter. What
matters is that every encoding carries the same contract and evolves under the
same rules. Each encoding identifies fields differently: Protobuf by field number,
CBOR by integer key, JSON by name.

## Decision

Define the API contract independently of any serialization format, and provide
encodings as codec plugins.

1. **Contract.** Commands, queries, events and stream messages are defined once,
   as C# types. Every field has a permanent numeric ID and a permanent name,
   declared by attributes. Both evolve under the schema rules. Codec artefacts
   for other languages (`.proto` files, JSON schemas) are generated from these
   types.
2. **Codecs.** A codec maps the contract to one wire encoding: Protobuf uses the
   numeric IDs as field numbers, CBOR uses them as integer keys, and JSON uses the
   names. Codecs are plugins behind an `IApiCodec` interface, and the initial set
   is CBOR, Protobuf and JSON.
3. **Negotiation.** Client and engine agree on a codec when a session opens. They
   may choose a different codec per stream within the session, for example binary
   for meters and JSON for commands.
4. **Framing is separate.** Message framing, request correlation, cancellation and
   stream multiplexing belong to the transport and are the same under every codec.
5. **Conformance.** Every codec must pass a shared conformance suite: each contract
   fixture round-trips, unknown fields are kept, and messages written under an
   older contract version still decode.

## Alternatives considered

### Protobuf only

It has the best multi-language tooling. Agents and MCP still need a JSON adapter,
and a second schema language (`.proto`) must be kept in step with CDDL.

### JSON only

It is easy to debug and matches MCP. High-rate streams are larger and slower to
parse.

### CBOR only

There is one encoding and one schema language shared with persisted data. RPC
tooling and code generation in other languages are much less mature.

### Cap'n Proto or FlatBuffers

Both avoid copying, which suits high-rate streams. They impose their own schema
language and layout rules, which a single neutral contract would have to fit
around.

## Consequences

### Positive

- Each client uses the encoding that suits it, without a separate API.
- One contract and one set of evolution rules for every encoding.
- New encodings can be added without changing the contract.

### Negative and trade-offs

- Each codec must be maintained and tested against the conformance suite.
- Codec-specific features (Protobuf `oneof`, JSON schemas for agents) must be
  derived from the neutral contract, not written by hand.
- Negotiation adds a step to opening a session.

## Evidence

Required before acceptance:

- [ ] Prototype generating `.proto` files and JSON schemas from annotated C# types.
- [ ] Benchmark serializing a meter frame with each codec (CBOR, Protobuf, JSON)
      using BenchmarkDotNet.
- [ ] Check transports for each target: gRPC on iOS and in the browser, Connect,
      WebSocket, or custom framing over the peer transport.

## Fitness functions

- Shared codec conformance suite runs against every registered codec in CI.
- CI check: no field ID or name in the contract is reused or changed.
- Architecture test: domain and engine code never reference a codec library
  directly, only the contract types.

## Review triggers

- A codec cannot express part of the contract.
- One codec becomes the only one in practical use.

## Notes

- 2026-10-04: Renumbered from the former ADR-0007 when ADRs were grouped by layer.
