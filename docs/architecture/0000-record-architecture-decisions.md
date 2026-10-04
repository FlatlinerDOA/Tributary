# 0000. Record architecture decisions

- Layer: Governance
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Implemented by this documentation set
- Depends on: None
- Supersedes: None
- Superseded by: None
- Provenance: New governance decision; acceptance is established by merging the documentation pull request.

## Context

Digital Audio Workstations can be some of the most complicated systems with strong 
contradictions pulling the architecture in many different directions.
The reasons behind the architectural choices need to remain reviewable with 
the code that implements them.

The reference ADR guidance recommends one version-controlled text file per significant decision, 
present-tense imperative filenames, explicit rationale, timestamps, consequences, and supersession rather than silent historical edits.

Architecture decisions also form a dependency graph. Foundational decisions
(encoding, addressing, identity) constrain the ones built on them (event model,
sync, engine), never the other way round. Linking records in both directions, or
restating a rule in several records, means every new record forces edits to older
ones, and duplicated rules drift apart.

## Decision

Record each architecturally significant decision as a numbered Markdown ADR under
`docs/architecture/`. Keep one decision per record. New decisions start
as `Proposed`, become `Accepted` through pull-request review, and are replaced by a
new linked ADR rather than rewriting history.

Retrospective records must identify themselves as retrospective and must not invent
an original date, decider, or rationale unsupported by repository evidence.

Group the records into layers, and order and link them by dependency:

- Each ADR belongs to exactly one layer, stated in a `Layer` entry in its header.
  The layer is also the first two digits of its number:

  | Numbers | Layer | Concerns |
  |---|---|---|
  | 0000–0099 | Governance | How decisions are recorded |
  | 0100–0199 | L0 Primitives | Platform, encoding, hashing, cryptography, identifiers |
  | 0200–0299 | L1 Storage | Stored objects, codecs, framing, chunks |
  | 0300–0399 | L2 Event model | Paths, aggregates, events, merging, conflicts, projections |
  | 0400–0499 | L3 Replication | Sync, encryption in transit, bundles, the data layer library |
  | 0500–0599 | L4 Domain model | The DAW's project model, independent of how it is played |
  | 0600–0699 | L5 Engine | Real-time audio, graph execution, plugins, inference |
  | 0700–0799 | L6 Host and API | The API, clients, processes and their coordination |
  | 0800–0899 | L7 Product features | Features built on everything below |

- An ADR depends only on ADRs in its own layer or a lower one. Within a layer, it
  depends only on lower-numbered ADRs. Layers L0 to L3 know nothing about audio or
  the DAW.
- Each rule has exactly one owning ADR. Other ADRs link to it and never restate it.
  A rule that spans layers is split so that each layer owns its part.
- Each ADR lists what it builds on in a `Depends on` entry in its header, linking
  only to ADRs it may depend on. There are no links to higher-numbered ADRs anywhere
  in a record. A later concept may be mentioned in general terms, without a link.
- A new decision takes the next free number in its layer's range. If an existing
  ADR would need to depend on it, the existing ADR is superseded by a new record
  that does.
- ADRs stand alone: they state their own context and never rely on planning
  documents outside this directory.

## Alternatives considered

### Keep only the existing narrative documents

This is concise, but it makes individual decisions difficult to supersede, trace,
or review independently.

### Put decisions only in issues and pull requests

Discussion history is useful but is distributed, can be closed or retitled, and is
not guaranteed to remain aligned with the released source tree.

### Adopt an external ADR management service

A service could add workflow features, but it would introduce a dependency and
access boundary that is unnecessary for a small open-source repository.

### Chronological numbering with two-way "Related decisions" links

This is common ADR practice. It produces dense two-way links, edits to older
records whenever a new one is added, and duplicated rules.

### A single sequence in dependency order, without layers

Dependency direction is still checked. Records about storage, the engine and the
API interleave, so nothing stops a low-level record from depending on a high-level
concern, and the cross-references grow tangled.

### Chronological numbering with an index grouped by area

This keeps the order in which decisions were made. A reader still cannot tell
from a number whether one record builds on another, and nothing prevents links
pointing forward.

## Consequences

### Positive

- Decisions are versioned and reviewed beside implementation changes.
- Each decision can link to evidence, tests, and later superseding records.
- Retrospective uncertainty is explicit instead of presented as fact.
- The set reads from the bottom up: each record only needs the records before it.
- Adding a decision never requires editing an accepted record.
- Dependency direction can be checked automatically.

### Negative and trade-offs

- Contributors must update the ADR log for architecture-affecting changes.
- Some overlap with concise user/developer documentation is intentional.
- Documentation review cannot by itself prove radio or hardware behavior.
- A foundational decision found late takes the next number in its layer, or
  supersedes records in its layer that would need to depend on it.
- Each layer's range holds at most 100 records.
- Numbers do not show the order in which decisions were made. The `Recorded`
  date does that.

## Evidence

- [Architecture documentation entry point](./README.md)

## Fitness functions

- Every ADR filename begins with a unique four-digit number.
- Every ADR contains status, recorded date, depends on, context, decision,
  alternatives, consequences, evidence, fitness functions, and review triggers.
- CI check: every link of the form `NNNN-*.md` inside an ADR points to a number
  lower than the ADR's own, and every link target exists.
- CI check: no `Depends on` entry points to a higher layer, and every `Layer` entry
  matches the first two digits of the ADR's number.
- New architecture-changing pull requests link an existing ADR or add a new one.

## Review triggers

- The project adopts a different architecture knowledge-management format.
- Repository governance moves authoritative documentation outside Git.
- ADR volume requires automated indexing or validation.
- The number of supersessions caused by dependency order becomes a burden.

## Notes

- 2026-10-03: Accepted as originally written. Merged on the same day with the
  proposed dependency-ordering rules (formerly ADR-0001), which returns the record
  to `Proposed` until a human re-accepts it.
- 2026-10-04: Returned to `Proposed`. The ordering rules now group ADRs into layers
  with their own number ranges, and every ADR was renumbered into its layer.
