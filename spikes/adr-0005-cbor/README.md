# ADR-0005 / ADR-0039 spike: deterministic CBOR across implementations

Evidence for [ADR-0005](../../docs/architecture/0005-encode-persisted-data-as-deterministic-cbor.md) and
[ADR-0039](../../docs/architecture/0039-generate-schemas-from-csharp-types.md). Throwaway harness code.

```
pip install -r python/requirements.txt
./run-all.sh          # .NET 10 SDK, Rust, Python 3.11+; regenerates results/
```

Results are deterministic and committed; `results/report.md` is the full comparison.

## Layout

| Path | Content |
|---|---|
| `fixtures/encode.json` | 89 values in a language-neutral description format, with expected bytes where a published source gives them (RFC 8949 Appendix A, draft-mcnally-deterministic-cbor-12 Table 3) |
| `fixtures/decode.json` | 29 inputs a checking decoder must accept or reject, per profile |
| `fixtures/cddl.json` | CDDL schemas for an event envelope, instances, and minimal probes |
| `python/ref_encoder.py` | Reference CDE and dCBOR encoder written from the drafts; reproduces all 143 published expectations |
| `csharp/` | `System.Formats.Cbor` 10.0.0, every conformance mode, `WriteDouble` vs. shortest-float helper |
| `python/` | `cbor2` 6.1.5, `pycddl` 0.6.4 |
| `rust/` | `ciborium` 0.2.2, `dcbor` 0.25.2, `cddl` 0.10.7 |

Fixture description format (proposal for ADR-0039): each node is a one-key object, `{"int": "<decimal>"}`,
`{"float": "<16 hex digits of the IEEE 754 binary64 bits>"}`, `{"text": ...}`, `{"bytes": "<hex>"}`, `{"bool": ...}`,
`{"null": true}`, `{"array": [...]}`, `{"map": [[key, value], ...]}` in deliberately non-canonical order, and
`{"tag": "<decimal>", "item": node}`. Integers are strings so that 64-bit values survive every JSON parser;
floats are bit patterns so that NaN payloads and -0.0 are exact.

## Findings

Profiles: CDE = draft-ietf-cbor-cde-09, dCBOR = draft-mcnally-deterministic-cbor-12. Both sort map keys
bytewise (RFC 8949 §4.2.1); dCBOR adds numeric reduction, a single NaN, NFC text and a narrower integer range.

### System.Formats.Cbor 10.0.0

- **`Canonical` orders keys length-first (RFC 7049 §3.9), not bytewise.** `{24, -1}` encodes as `a22000181800`;
  the reader rejects the RFC 8949 order. `Ctap2Canonical` orders bytewise but rejects tags when writing and reading,
  and `WriteDouble` there writes binary64 without shortening.
- Bytewise and length-first agree whenever every key in a map has the same major type (all unsigned, all text, …).
  The two orders differ only across major types, for example `24` vs `-1` or `256` vs `"a"`.
- In `Lax`, `Strict` and `Canonical`, `WriteDouble` already writes the shortest exact float, and every NaN is written
  as `f97e00` (sign and payload dropped). That matches CDE except for NaN payloads.
- **No reader mode checks floats.** `Canonical` accepts 1.5 as binary64 or binary32, and Infinity and NaN as binary64.
  It does reject unsorted keys (by its own order), duplicates, non-shortest integers and lengths, indefinite lengths
  and invalid UTF-8. `Strict` checks duplicates and UTF-8 only. A Tributary decoder needs its own float check.

### Independent implementations

- **ciborium 0.2.2** has no canonical serializer. `CanonicalValue` sorts length-first. With keys sorted bytewise
  by hand, it matches CDE on all 89 fixtures, NaN payloads included. Its decoder checks nothing beyond well-formedness.
- **cbor2 6.1.5** `canonical=True` sorts length-first and writes every NaN as `f97e00`. Otherwise it matches CDE.
  `loads` checks only duplicates and indefinite lengths, and only when asked to (`allow_duplicate_keys=False`,
  `allow_indefinite=False`). It accepts trailing bytes.
- **dcbor 0.25.2** is the only dCBOR implementation tested, and the only checking decoder (28/29 cases).
  It departs from the draft-12 copy it bundles: it reduces `-18446742974197924000.0` to an integer below -2^63
  (Table 3 expects `fadf7fffff`), and its decoder accepts `-2^64` (Table 4 lists it as invalid). Its encoder
  normalizes text to NFC silently instead of refusing.

### CDDL validators

- **pycddl 0.6.4** (last release 2025-06-11) wraps the Rust `cddl` crate 0.9.5, so a Python plus Rust validator pair is
  one codebase at two versions, not two independent validators.
- Both `pycddl` and `cddl` 0.10.7 treat `.regexp` as an unanchored search: `tstr .regexp "[a-z]+"` accepts `"aBC"`.
  RFC 8610 specifies XSD regular expressions, which match the whole string. Explicit `^…$` works in both.
- Neither checks float widths: `float64` accepts binary16 and `float16` accepts binary64.
- `cddl` 0.10.0–0.10.7 mis-validates map keys given as named constants (`k = 1`, `{ k => bstr }`), the style
  ADR-0039's mapping table prescribes; 0.9.5 handles it. Literal integer keys work in both.
- CDDL maps are closed: a schema rejects a key added later unless it declares an extension point.
