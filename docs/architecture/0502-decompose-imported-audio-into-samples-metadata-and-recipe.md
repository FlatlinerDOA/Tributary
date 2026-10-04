# 0502. Decompose imported audio into samples, metadata and a rebuild recipe

- Layer: L4 Domain model
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0102](0102-encode-persisted-data-as-deterministic-cbor.md): metadata and
    recipe encoding.
  - [ADR-0200](0200-never-modify-stored-objects.md): the decomposed parts are
    immutable sources.
  - [ADR-0201](0201-choose-storage-codecs-by-content-type.md): sample streams use the
    integer or float PCM codec.
  - [ADR-0203](0203-store-blobs-as-content-defined-chunks.md): sample streams are
    stored as chunks.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Encoding only the sample data of an imported WAV, BWF or AIFF file loses
everything else in it:

| Chunk | Contents | Why a DAW cares |
|---|---|---|
| `fmt ` | Rate, channels, bit depth, int or float, channel mask | Decoding at all |
| `bext` | Description, originator, **time reference**, UMID, loudness | Placing audio on the timeline |
| `iXML` | Scene, take, track names, timecode | Location recording workflows |
| `cue ` / `LIST adtl` | Markers and regions | Editing |
| `smpl` / `inst` | **Loop points, root note, tuning** | Samplers |
| `acid` | Tempo, beats, root key | Warping loops |
| `LIST INFO`, `id3 `, `cart`, vendor chunks | Tags and library metadata | Provenance and search |
| Chunk order, padding, `JUNK` | File layout | Rebuilding the original file byte for byte |

Facts:

- `flac --keep-foreign-metadata` can carry RIFF chunks, but most libraries ignore
  them.
- 8-bit WAV is unsigned, while FLAC is signed.
- Provenance and sample-pack licensing may require proving that an imported file
  was the exact original.

## Decision

At import, split each audio file into three content-addressed objects:

1. **Sample stream:** canonical interleaved PCM in a fixed byte order and the
   source sample format, stored as chunks with the codec for its content type.
2. **Metadata object:** a canonical CBOR record of every parsed chunk (`fmt `,
   `bext`, `iXML`, markers, `smpl`, `inst`, `acid`, tags). Unknown chunks are kept
   as raw byte strings tagged with their chunk ID.
3. **Rebuild recipe:** the container type, chunk order, padding and header fields
   needed to rebuild the original file byte for byte, plus the original file's
   multihash.

The source references all three. Metadata such as timeline position (`bext`) and
loop points (`smpl`) is read directly from the metadata object.

## Alternatives considered

### Store the original file as an opaque blob

Its identity and every byte are preserved trivially. Chunking and codecs see
container headers instead of samples, identical audio with different tags doesn't
deduplicate, and metadata needs parsing again on every read.

### Store FLAC with foreign-metadata blocks

It is a single standard file. Most readers drop the foreign blocks, and float
samples are not supported.

### Store only samples and the metadata Tributary understands

This is simpler, but unknown chunks are lost and the original file can't be
rebuilt.

## Consequences

### Positive

- Identical audio with different metadata deduplicates.
- Chunking and compression operate on clean, frame-aligned samples.
- Export can rebuild the exact original file, which preserves provenance.

### Negative and trade-offs

- Each container format (WAV/RF64/BWF, AIFF/AIFC, CAF) needs a parser and a
  writer.
- Every import must pass a round-trip check, which costs CPU at import.

## Evidence

Required before acceptance:

- [ ] Build a corpus of real-world files: BWF from field recorders, Acidized loops,
      sampler WAVs with `smpl`, RF64, AIFF and files with vendor chunks.
- [ ] Rebuild every file in the corpus byte for byte from its three parts.
- [ ] Measure deduplication gains on sample libraries that ship the same audio with
      different tags.

## Fitness functions

- Import round-trip test: every corpus file is rebuilt byte for byte and its hash
  matches the recorded original hash.
- Unknown-chunk test: a file with a made-up chunk keeps it through import and
  export.

## Review triggers

- A container format cannot be rebuilt byte for byte.
- Compressed audio imports (MP3, AAC, Ogg) need the same split. They are currently
  stored raw.

## Notes

- 2026-10-04: Renumbered from the former ADR-0015 when ADRs were grouped by layer.
