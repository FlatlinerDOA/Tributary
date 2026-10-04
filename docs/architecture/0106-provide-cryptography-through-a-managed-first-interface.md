# 0106. Provide cryptography through a managed-first interface

- Layer: L0 Primitives
- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0100](0100-keep-the-core-managed-with-optional-native-providers.md): the
    managed-first provider model.
  - [ADR-0101](0101-target-the-latest-dotnet-release.md): which BCL primitives
    (including `MLDsa` and `MLKem`) are available.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Tributary needs hashing, signatures, key agreement and authenticated encryption.
Native crypto libraries cause packaging trouble on iOS and under AOT.

Facts to verify:

- The BCL provides SHA-256 and AES-GCM. `ChaCha20Poly1305.IsSupported` depends on
  the platform. Check it on iOS, macOS and Android.
- Ed25519 and X25519 are not in the BCL as far as is known. BouncyCastle provides
  managed implementations.
- .NET 10 adds post-quantum `MLDsa` and `MLKem`, which also depend on the platform.
  Signatures that must stay meaningful for 20 years may want a post-quantum or
  hybrid option.
- Managed implementations need checking for constant-time behaviour.

## Decision

Expose cryptography only through Tributary-owned interfaces (hash, sign/verify,
key agreement, AEAD, key derivation). Ship a managed provider (BCL + BouncyCastle)
as the default. Allow native providers (libsodium, HACL*) as optional faster
implementations that must pass the same test vectors. Tag every key and signature
with its algorithm so that algorithms can be added later.

## Alternatives considered

### Native libsodium only (for example via NSec)

It is fast and well audited. It adds native packaging on every platform and does
not work everywhere AOT runs.

### BCL only

There is no third-party dependency, but Ed25519 and X25519 are missing.

### BouncyCastle directly, with no interface

It is less work now. Switching providers or adding post-quantum algorithms later
would touch every call site.

## Consequences

### Positive

- It works on every .NET target, including iOS AOT.
- Providers and algorithms can be swapped without touching domain code.

### Negative and trade-offs

- Managed signing may be slower, which matters if every change is signed.
- Implementations need constant-time review.

## Evidence

Required before acceptance:

- [ ] BenchmarkDotNet: Ed25519 sign and verify operations per second, BouncyCastle
      vs. libsodium.
- [ ] Pass RFC 8032, RFC 7748 and [Wycheproof](https://github.com/C2SP/wycheproof)
      vectors for every provider.
- [ ] Platform support matrix for ChaCha20-Poly1305, AES-GCM, ML-DSA and ML-KEM.
- [ ] Trimming and NativeAOT test of the managed provider on iOS.
- [ ] Decide whether signatures are hybrid (Ed25519 + ML-DSA) from the start.

## Fitness functions

- The same test-vector suite runs against every registered provider in CI.
- Architecture test: no assembly outside the crypto library references
  BouncyCastle or `System.Security.Cryptography` primitives directly.

## Review triggers

- A provider has a security advisory.
- The BCL adds Ed25519 or X25519.
- Post-quantum signatures become mandatory for a target market.

## Notes

- 2026-10-04: Renumbered from the former ADR-0008 when ADRs were grouped by layer.
