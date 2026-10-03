# Tributary — Project Plan

A ground-up DAW (Digital Audio Workstation) built around a headless engine, an immutable event-sourced project model, first-class ML/agent collaboration, and a promise that projects remain playable and editable 20 years later.

---

## 1. Principles

1. **Headless engine, UI is a client.** GUI, CLI, scripts, controllers and AI agents all drive the engine through the same typed API.
2. **Sources are immutable.** Audio is never rewritten. All edits (warp, stretch, consolidate) are transforms resolved at render time.
3. **The op log is the source of truth.** Everything else (indexes, render caches, analysis, UI state) is a projection and can be rebuilt.
4. **Degrade gracefully over time.** A project must always open; losing a plugin costs editability of that effect, never the song.
5. **Local-first, no central server.** Peers and pinning nodes are equal. No service in the critical path.
6. **Pure managed core where practical.** Avoid native dependencies in the core (iOS/AOT pain, memory safety). Native code only behind interfaces as optional fast paths.
7. **Boring, documented formats for anything persisted.**

---

## 2. Scope and Repository Boundaries

**This repo (Tributary):** DAW domain model, engine, project schema, ML/agent integration, UI clients.

**External common libraries (separate projects, consumed as packages):**

| Library | Purpose | Notes |
|---|---|---|
| P2P networking | Transport, NAT traversal, discovery, connections | Existing pure C# project |
| Crypto | Hashing, signatures, AEAD, key recovery behind a thin interface | Managed-first (BouncyCastle / BCL); optional native providers |
| CBOR source generator | Canonical CBOR mappers over `System.Formats.Cbor` | Integer keys in ascending order → canonical by construction |
| Data layer *(assumed common)* | Content-addressed store, signed op log, sync | Reusable across other apps; Tributary is the first consumer |

Tributary depends on these via interfaces only; no Tributary types leak into them.

---

## 3. Architecture

```
┌──────────────────────────────────────────────────────────┐
│ Clients: GUI · CLI · scripts · controllers · AI agents   │
└───────────────────────┬──────────────────────────────────┘
                        │ typed API (RPC; protobuf OK here)
┌───────────────────────▼──────────────────────────────────┐
│ Command layer (CQRS write side)                          │
│  aggregates: track · bus · section · tempo/key map ·     │
│  plugin instance — invariants, permissions, versions     │
└───────────────────────┬──────────────────────────────────┘
                        │ events (canonical CBOR, signed)
┌───────────────────────▼──────────────────────────────────┐
│ Data layer (external)                                    │
│  op log · content-addressed blob store · sync            │
└───────────────────────┬──────────────────────────────────┘
                        │ projections
┌──────────┬────────────┼─────────────┬────────────────────┐
│ Audio    │ Semantic   │ Render /    │ UI / agent views   │
│ graph    │ layer      │ analysis    │                    │
│ (RT swap)│ (key,      │ cache       │                    │
│          │ chords…)   │ (evictable) │                    │
└──────────┴────────────┴─────────────┴────────────────────┘
```

### 3.1 Project model
- A project is like a git repository: content-addressed objects, Merkle DAG, named refs (branches), full history.
- **Unit of history is an operation**, not a snapshot. Snapshots are derived.
- **Hybrid consistency:**
  - CRDT for fine-grained content (notes, automation points, clip positions).
  - Aggregate commands with optimistic concurrency (expected version) for structural/semantic ops (routing, tempo, key, plugin chains).
  - Cross-aggregate effects (e.g. key change → re-transpose) via sagas/process managers.
- **Real-time fast path** for gestures (knobs, performance input) straight to the engine, coalesced into events afterwards.
- Every op is signed and attributed (human or agent, model, licence).

### 3.2 Storage
- Blobs: chunked (content-defined chunking), hashed, deduped across all projects on a device. Stored as files or packfiles — never in a database.
- Index: disposable, rebuildable from the log. Start with append-only segments + in-memory index + checkpoints behind `IIndex`; swap to ZoneTree/SQLite only if profiling or querying demands it.
- Users never see the hash store. Sharing is via:
  - **Reference:** root hash + capability token; recipient syncs missing blobs only.
  - **Bundle:** single `.trib` file (manifest + blobs).
  - **Plain export:** named WAV/FLAC stems, MIDI, DAWproject.
  - **Optional virtual filesystem:** ProjFS / FileProvider / FUSE.

### 3.3 Audio and time-stretching
- Analysis (transients, onsets, pitch, spectral frames) computed once at import, tempo-independent.
- Warp is a **single composed time-mapping function** per clip, not a stack of renders — prevents compounding artefacts.
- Tiered render: real-time stretch for playback → background HQ render swapped in.
- Render cache keyed by `hash(source, warp params, tempo-map segment, algorithm version)`; LRU on disk; range-scoped invalidation.
- "Consolidate" is a view operation; clips always resolve to original source + transforms.

### 3.4 Engine
- Lock-free, multi-core, dependency-scheduled graph; sample-accurate automation; full delay compensation.
- Graph compiled from events into an immutable snapshot, atomically swapped to the RT thread.
- CLAP-first hosting (VST3/AU/LV2 secondary); plugins sandboxed out of process.
- Native MIDI 2.0 / MPE; universal modulation; deterministic offline render.

### 3.5 ML and agents
- **ML as a scheduled node type, two tiers:** RT-safe small models inline on CPU; heavy models (separation, generation, timbre transfer) as look-ahead/background jobs on GPU/NPU with auto-freeze and caching.
- **Semantic layer** (key, tempo, chords, sections, roles, loudness) as a continuously maintained projection — the interface agents reason over.
- **Agents issue commands, never events.** Scoped write permissions per aggregate (UCAN-style capabilities).
- **Generation as proposals:** results land on branches/take lanes as reviewable diffs with rendered before/after and rationale.
- Multi-agent patterns: role-specialised agents, parallel exploration with a critic, background maintainers, live "band member" agents.
- Compute budgeting per agent; RT path always wins.

---

## 4. Longevity (the 20-year promise)

| Tier | Guarantee | Mechanism |
|---|---|---|
| 1 | Fully editable, original sound | Plugins and models pinned by hash; archived binaries; VM/emulation last resort |
| 2 | Editable, dead plugins frozen | **Print insurance:** every plugin's output continuously printed and cached; plugin state stored as opaque blob + parameter snapshot + parameter metadata |
| 3 | Opens in any DAW | Canonical export bundle: BWF/FLAC stems, MIDI, tempo map, DAWproject/AAF |

Rules:
- Persisted formats: WAV/BWF/FLAC for audio; canonical CBOR (RFC 8949 §4.2) for events; CDDL schemas archived inside the bundle.
- Never rewrite events. Schema version in every envelope; upcasters on read; add fields, never repurpose; preserve unknown keys.
- Hash and sign the exact stored bytes; never re-encode before verifying. Compress (zstd) only after hashing.
- Multihash-style prefix on all addresses so the hash algorithm can change later.
- Self-describing archives: readable with only their own contents + a text spec.
- Integrity scrubbing on a schedule; repair from replicas.
- Key recovery planned from day one (SLIP-39 Shamir shares of a key-encryption key).

---

## 5. Decisions Log

| Decision | Choice | Rationale |
|---|---|---|
| Persisted encoding | Canonical CBOR | IETF standard, deterministic encoding defined, self-describing; required for hashing/signing |
| Live RPC encoding | Protobuf acceptable | Canonical form irrelevant; better tooling |
| Hash | SHA-256 (BCL) with own chunk Merkle tree; BLAKE3 optional later | Pure managed, HW-accelerated, conservative; chunking already provides tree benefits |
| Crypto library | Managed-first behind interface | Avoid native packaging/iOS issues; native libsodium/HACL* as optional providers |
| Index store | No DB initially; checkpointed in-memory index | Index is rebuildable, so archival properties don't matter; avoids SQLite-on-iOS issues |
| Consistency | CQRS + event sourcing for structure, CRDT for content | Explicit consistency boundaries for multi-agent edits |
| Plugin standard | CLAP first | Polyphonic modulation, host thread pools |

---

## 6. Phases

### Phase 0 — Foundations (external libs)
- [ ] CBOR source generator: canonical mappers, round-trip and canonical-bytes tests
- [ ] Crypto interface + managed provider (SHA-256, Ed25519, X25519, AEAD); test vectors
- [ ] (Optional) managed BLAKE3: scalar port of reference impl, official vectors, differential fuzzing

### Phase 1 — Local durable store (single device, no networking)
- [ ] Content-addressed blob store with CDC chunking and integrity scrubbing
- [ ] Signed op log with event envelope (schema version, author, causal metadata)
- [ ] Projection framework + checkpointed rebuildable index
- [ ] Bundle export/import (`.trib`)
- **Exit:** a project survives process crash, index deletion and re-import bit-identically.

### Phase 2 — Minimal engine and project model
- [ ] Aggregates and commands for tracks, clips, tempo map
- [ ] Audio graph compiled from projection; RT snapshot swap
- [ ] Immutable source import + analysis; RT time-stretch playback; render cache
- [ ] Deterministic offline render
- [ ] CLI client
- **Exit:** create, edit, play and render a multitrack project entirely from the CLI.

### Phase 3 — Plugins and longevity
- [ ] CLAP hosting, out-of-process sandbox
- [ ] Print insurance (continuous plugin output capture)
- [ ] Plain export (stems, MIDI, DAWproject)
- **Exit:** delete a plugin binary; project still opens and plays.

### Phase 4 — Sync and collaboration
- [ ] Integrate P2P library; set-reconciliation sync of log + blobs
- [ ] CRDT content types; optimistic concurrency on aggregates
- [ ] Identity, capabilities, key recovery
- **Exit:** two devices co-edit offline, reconnect, converge.

### Phase 5 — Semantic layer and agents
- [ ] Semantic projection (key, tempo, chords, sections, loudness)
- [ ] Agent API (MCP or similar) issuing commands with scoped capabilities
- [ ] Proposals on branches with rendered diffs; accept/reject/cherry-pick
- [ ] Two-tier ML node scheduling (CPU inline, GPU/NPU background)

### Phase 6 — GUI and beyond
- [ ] GPU-rendered UI client
- [ ] Stem separation / transcription as editing tools
- [ ] Continuous mix analysis annotations
- [ ] LAN offload of heavy plugins/models

---

## 7. Open Questions

- **Engine language.** C# with GC-free RT discipline vs. Rust core behind a C ABI. Decide before Phase 2.
- **Data layer boundary.** Confirm the store/log/sync lives in a common project vs. starting here and extracting later.
- **Sync protocol.** Range-based set reconciliation design (Willow/Negentropy-style) and how it interacts with the P2P transport.
- **Aggregate granularity.** Per-track/per-bus to start; revisit under multi-agent contention.
- **Plugin binary archiving.** Licensing constraints on storing third-party binaries.
- **Encryption at rest.** Scope and impact on long-term access.
- **Name clearance.** Trademark check for "Tributary" (classes 9/42); reserve GitHub org, NuGet prefix, domain.
