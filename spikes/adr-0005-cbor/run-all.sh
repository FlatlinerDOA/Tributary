#!/usr/bin/env bash
# Runs every harness and regenerates results/report.md.
# Needs: .NET 10 SDK, Rust toolchain, Python 3.11+ with python/requirements.txt installed.
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p results
dotnet run -c Release --project csharp -- "$PWD"
cargo run -q --release --manifest-path rust/Cargo.toml -- "$PWD"
python3 python/harness_cbor2.py results
python3 python/cddl_check.py results
python3 compare.py
echo "wrote results/report.md"
