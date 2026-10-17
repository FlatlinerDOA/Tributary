# ADR-0014 spike: FastCDC chunking and deduplication

Evidence for [ADR-0014](../../docs/architecture/0014-store-blobs-as-content-defined-chunks.md). Throwaway code.

| Path | Content |
|---|---|
| `src/FastCdc.cs` | Managed FastCDC 2020 over `ReadOnlySpan<byte>`, with optional frame alignment |
| `rust/` | Reference CLI around the Rust `fastcdc` crate 3.2.1 (`v2020`) |
| `verify-against-rust.sh` | Compares cut points with the crate on random, all-zero, short and audio inputs at five size settings |
| `benchmarks/` | BenchmarkDotNet: chunking throughput (target size × alignment) and SHA-256 for comparison |
| `dedup/` | `scan` a directory and compare chunking strategies; `synth` a test library; `verify` prints cut points |

## Running

```
./verify-against-rust.sh                                    # must print 20 "match" lines
dotnet run -c Release --project benchmarks -- --filter '*'  # throughput; MiB/s = 64 / Mean in seconds
dotnet run -c Release --project dedup -- scan <sample library or projects dir> --sizes 8,16,32,64,128,256
```

`scan` reads files memory-mapped and in parallel and never writes. WAV files are split into the PCM `data`
chunk, which is chunked with cuts on frame boundaries (`blockAlign`), and the rest of the file (header and
metadata chunks), which is hashed as one piece. Every other file is chunked as raw bytes. Chunks are keyed by
the first 16 bytes of their SHA-256. "index MiB" estimates the chunk index at 48 bytes per unique chunk
(34-byte multihash plus pack offset and length).

## Implementation notes

- With alignment 1, cut points match the Rust crate exactly (20 of 20 combinations). The gear table is derived
  as the crate derives it: the first 8 bytes, big-endian, of MD5 over 64 copies of each byte value.
- Frame alignment: accepting cut candidates only at frame boundaries cut the per-byte cut probability by the
  frame size, which inflated the mean chunk to 2–2.3× the target on random data (alignment 4 and 6). Instead,
  each content-defined cut is moved forward to the next frame boundary. The mean then matches unaligned FastCDC
  (19.99 KB vs 19.99 KB at a 16 KiB target), and boundaries still move with the content when whole frames are
  inserted or removed.

## Results in this container

`results/benchmarks-container.md` (BenchmarkDotNet short job, 4 vCPU Xeon without SHA extensions):
chunking runs at about 1.3–1.9 GB/s at every target size, with or without alignment, and allocates nothing.
SHA-256 runs at about 0.37 GB/s here, matching `openssl speed`; CPUs with SHA extensions hash about 5× faster,
so the chunking-to-hashing ratio has to be measured on target hardware.

`results/synthetic-library-scan.md`: 12 synthetic 24-bit stereo samples, each also stored re-tagged, trimmed by
a few thousand frames, extended, with an insert, and at half gain, plus bounces of three samples each
(286 MiB). The library is built from edits that content-defined chunking handles, so it shows the mechanism,
not a realistic ratio:

| Strategy | saved |
|---|---:|
| whole file (raw bytes) | 0 % |
| whole content (PCM data only) | 14 % (re-tagged copies) |
| fixed 16 / 64 / 256 KiB | 40–41 % (copies, appends, the part before an insert) |
| FastCDC 16 / 64 / 256 KiB | 70 / 69 / 66 % (also trims, inserts and bounces) |

Gain changes and any re-encoding produce new sample bytes and deduplicate under no strategy.
