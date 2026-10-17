# 0039. Generate schemas from C# types

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0003](0003-target-the-latest-dotnet-release.md): the language features
    (including union types) available to persisted types.
  - [ADR-0005](0005-encode-persisted-data-as-deterministic-cbor.md): the encoding and
    the archived CDDL.
  - [ADR-0006](0006-evolve-schemas-additively.md): generated schemas evolve under its
    rules.
  - [ADR-0007](0007-make-api-serialization-pluggable.md): the same field-ID and name
    annotations serve the API contract.
  - [ADR-0016](0016-identify-entities-with-canonical-string-ids.md): the segment
    grammar emitted as `.regexp`.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Persisted data must stay readable for decades, including by readers that are not
written in .NET. CDDL ([RFC 8610](https://www.rfc-editor.org/rfc/rfc8610)) schemas
archived with the data describe its structure. The domain, though, is modelled in
C#, and modelling should not be limited by what CDDL can express.

Two directions are possible:

- **CDDL → C#** needs a CDDL parser in .NET (no mature one is known) and an
  annotation convention for C#-specific details (type names, namespaces, strongly
  typed IDs, absent-vs-null, collection types). CDDL constructs such as group
  choices and unwrap have no natural C# form.
- **C# → CDDL** needs only an emitter, because Roslyn already parses C#. Some C#
  constructs have no CDDL form and must be refused for persisted types.

A generated schema is only trustworthy if independent implementations agree with
it. Rust and Python have maintained CBOR and CDDL libraries.

## Decision

Make C# types the only source of truth for persisted formats. A source generator
emits, from the same types, the canonical CBOR mappers and the CDDL schemas that
are archived with the data.

1. **Mapping:**

   | C# | CDDL |
   |---|---|
   | `record` with `[Key(n)]` properties | `{ k-name => type, … }` with `k-name = n` constants |
   | `int`/`long`, `uint`/`ulong` | `int`, `uint` |
   | `Half`/`float`/`double` | `float16/32/64` |
   | `bool`, `string`, `ReadOnlyMemory<byte>` | `bool`, `tstr`, `bstr` |
   | `enum` | `&( name: n, … )` |
   | Strongly typed ID | Named rule (for example `track-id = segment`) |
   | `ImmutableArray<T>` | `[* T]` |
   | `ImmutableDictionary<K,V>` | `{ * K => V }` |
   | Closed hierarchy or union type | Type choice with a discriminator, or a `$socket` |
   | Generic record | CDDL generic rule |
   | Recursive type | Recursive rule |
   | `[Range]`, `[Length]`, `[Pattern]` | `.ge`/`.le`, `.size`, `.regexp` |
   | Optional property | `? key => T` (absent), never `T / null`, unless null is a distinct value |

2. **Meaning CDDL cannot express** (constraints across fields, uniqueness,
   references, invariants, merge behaviour, units) stays in domain code. The
   generator copies the types' XML documentation into the CDDL as comments.
3. **Refused constructs.** On a persisted type, each of these is a build error:
   - `object`, `dynamic`, and inheritance without a discriminator
   - `DateTime` or `DateTimeOffset`, unless mapped to an explicit epoch integer or
     tagged `tdate`
   - `decimal`, unless mapped to tag 4 (decimal fraction)
   - negative or duplicate keys
   - `[Pattern]` regexes outside the subset shared by .NET and W3C XML Schema
     regular expressions (anchored, no lookarounds, no backreferences)

   API-only types are not restricted.
4. **Cross-language conformance suite.** CI runs a fixture corpus produced from the
   C# types through independent Rust and Python implementations:
   - Validate every fixture against the generated CDDL (Rust `cddl` crate; Python
     through `pycddl` or an equivalent).
   - Decode every fixture and re-encode it deterministically (Rust `ciborium`,
     Python `cbor2`). The bytes must be identical.
   - The Rust and Python harnesses encode their own fixtures from
     language-neutral descriptions. C# must decode them to the same values.
   - Non-canonical fixtures crafted by the independent libraries (unsorted keys,
     duplicate keys, indefinite lengths, non-preferred floats) must be rejected by
     the C# decoder.

   Tool versions are pinned, and the suite runs in a container.

## Alternatives considered

### CDDL → C# (CDDL as the source of truth)

The archived schema is hand-written and as readable as its author makes it. It
needs a CDDL parser in .NET, an annotation convention for C# details, and C# forms
for CDDL constructs that have none. Modelling would be limited by CDDL.

### CDDL first for persisted formats, C# first for the API

Persisted formats would be kept deliberately simple. There would be two schema
sources, two generators to maintain, and types shared between the API and
persistence would sit on a boundary.

### Hand-maintained C# and CDDL, checked against each other

No generator is needed. The two drift apart, and the check only finds problems
after they have happened.

## Consequences

### Positive

- Domain modelling uses full C#. Only persisted types carry the mapping
  restrictions, and violations show up at compile time.
- One source produces the mappers, the archived schemas and the API codec
  artefacts.
- Independent implementations confirm the archived format, not just the generator.

### Negative and trade-offs

- The generated CDDL is only as readable as the generator makes it. Naming and
  comment output need care.
- CI needs Rust and Python toolchains.
- Union types are only usable under the runtime and boxing rules of
  [ADR-0003](0003-target-the-latest-dotnet-release.md). Until then, closed
  hierarchies are used instead.

## Evidence

Available:

- Spike: [`spikes/adr-0005-cbor/`](../../spikes/adr-0005-cbor/) (fixtures, harnesses
  for `ciborium`, `cbor2`, `dcbor`, `pycddl` and `cddl`, and
  [`results/report.md`](../../spikes/adr-0005-cbor/results/report.md)).

Required before acceptance:

- [ ] Generate CDDL for the event envelope and three event types (including a type
      choice), and review it for readability without the C# source.
- [x] Confirm the deterministic-encoding modes of `ciborium` and `cbor2` (key
      ordering, float handling). With only non-negative integer keys, RFC 8949
      bytewise and RFC 7049 length-first ordering agree.
      - `ciborium` 0.2.2 has no canonical serializer. Its `CanonicalValue` sorts
        length-first. With keys sorted bytewise by the harness, its output matches
        CDE on all 89 fixtures, including NaN payloads.
      - `cbor2` 6.1.5 `canonical=True` sorts length-first and writes every NaN as
        `f97e00`. It is otherwise CDE.
      - Neither decoder checks determinism. `cbor2` can reject duplicates and
        indefinite lengths on request, and accepts trailing bytes.
      - The orders agree for any map whose keys all share one major type, which
        includes text-keyed dictionaries.
- [ ] Confirm that a maintained Python CDDL validator exists (`pycddl` or an
      equivalent). Findings:
      - `pycddl` 0.6.4 (last release 2025-06-11) wraps the Rust `cddl` crate 0.9.5,
        so it is not independent of the Rust validator.
      - `pycddl` and `cddl` 0.10.7 both treat `.regexp` as an unanchored search, so
        `tstr .regexp "[a-z]+"` accepts `"aBC"`. RFC 8610 specifies XSD regular
        expressions, which match the whole string. Generated patterns need explicit
        `^…$`.
      - Neither validator checks float widths. Under preferred serialization, a C#
        `double` holding 0.5 is encoded as binary16, so a `float64` rule would be
        wrong even if it were checked. `float` is the rule that matches the data.
      - `cddl` 0.10.0–0.10.7 mis-validate map keys given as named constants
        (`k-name = n`, the style in the mapping table above). 0.9.5 handles them,
        and literal integer keys work in both.
- [ ] Define the format of the language-neutral fixture descriptions. Proposed in
      the spike: one-key JSON nodes, with integers as decimal strings, floats as
      binary64 bit patterns in hex, and map entries as ordered pairs. See
      [`fixtures/encode.json`](../../spikes/adr-0005-cbor/fixtures/encode.json).

## Fitness functions

- The cross-language conformance suite runs in CI on every change to a persisted
  type.
- Generator tests: each refused construct produces a build error.
- CI check: the generated CDDL for a released schema version never changes.

## Review triggers

- Rust or Python lose a maintained CDDL validator.
- A needed persisted construct cannot be mapped to CDDL.
