# 0018. Select paths with one pattern language

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0016](0016-identify-entities-with-canonical-string-ids.md): the segment
    grammar that keeps wildcards unambiguous.
  - [ADR-0017](0017-key-aggregates-by-hierarchical-paths.md): the paths being
    selected and the reason ancestors match.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

With path-keyed aggregates, many subsystems need to select a set of paths:
authorisation scopes, subscriptions, replication scopes and projection inputs. If
each defines its own selector language, they drift apart, and a grant may cover
something different from what a subscription with the same text shows.

Changes recorded at an ancestor path affect its descendants. A change recorded at
`/` may change `/tracks/3`, but a plain match of its path against `/tracks/3/**`
misses it.

## Decision

Use one path-pattern language everywhere a set of paths is selected.

1. **Grammar:**
   - A literal segment (any valid identifier segment).
   - `*` matches exactly one segment.
   - `**` matches zero or more segments, and is allowed only as the last segment.
2. **Selector:** an OR of conjunctions, `any of [ pattern AND attribute filters? ]`,
   where attribute filters (record type, actor, branch) are exact-match sets.
   There is no negation.
3. **Matching a record against a selector:** a record at path P matches a pattern
   if the pattern matches P, **or** if P is an ancestor of some path the pattern can
   match (because a change at P can affect that subtree).
4. **Containment:** pattern A is contained in pattern B if every path A matches is
   also matched by B. The algorithm is specified and tested, so narrowing a scope
   can be checked mechanically.
5. **Implementation:** selectors are compiled into a trie keyed by segment, so
   finding every match for a record costs one walk down its path. C# code builds
   patterns through strongly typed builders (`Paths.Tracks.Any.Clips.All` →
   `/tracks/*/clips/**`) that cannot produce an invalid pattern.

## Alternatives considered

### A separate selector language for each subsystem

Each can be tuned for its use. They drift apart, and security reviews must reason
about several languages.

### Full glob or regular-expression patterns

They are more expressive. Matching is no longer linear in path depth, containment
between patterns is expensive or undecidable, and the trie index no longer works.

### General query language (Datalog, as in Biscuit)

It is very expressive. It is too heavy for matching on the hot path.

## Consequences

### Positive

- One matcher, one specification and one test suite.
- Containment checks are cheap.
- Wildcards can never collide with identifiers, because the segment grammar
  excludes them.

### Negative and trade-offs

- Some selections can't be expressed: "every track except 3" has to be written
  out explicitly or derived elsewhere.
- Ancestor matching means a broad change at `/` matches every selector.

## Evidence

Required before acceptance:

- [ ] BenchmarkDotNet: trie matching with 10,000 selectors at a record rate typical
      of live co-editing.
- [ ] Specify the containment algorithm and test it with properties.
- [ ] Check that the patterns map onto UCAN resource URIs and Willow-style path
      ranges.

## Fitness functions

- A shared conformance suite of pattern-matching test vectors, run by every
  consumer of the language.
- Property test: if A ⊆ B by the containment algorithm, every path matched by A is
  matched by B.
- Test: a selector for `/tracks/3/**` matches a record at `/`.

## Review triggers

- A real use case needs negation or `**` in the middle of a pattern.
- The matcher shows up in hot-path profiles.
