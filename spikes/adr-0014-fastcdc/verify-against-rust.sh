#!/usr/bin/env bash
# Checks that the C# FastCDC produces exactly the cut points of the Rust fastcdc crate (v2020) at alignment 1.
set -euo pipefail
cd "$(dirname "$0")"
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
cargo build -q --release --manifest-path rust/Cargo.toml
dotnet build -c Release -v quiet -nologo dedup >/dev/null
dedup="dotnet dedup/bin/Release/net10.0/Dedup.dll"
head -c 67108864 /dev/urandom > "$work/random.bin"
head -c 4194304 /dev/zero > "$work/zeros.bin"
head -c 5000 /dev/urandom > "$work/short.bin"
$dedup synth "$work/lib" 3 >/dev/null && cp "$work/lib/bounce-00.wav" "$work/audio.bin"
fail=0
for f in random zeros short audio; do
  for p in "4096 16384 65536" "16384 65536 262144" "65536 262144 1048576" "1001 8192 30001" "64 256 1024"; do
    a=$(./rust/target/release/cdc-ref "$work/$f.bin" $p | md5sum)
    b=$($dedup verify "$work/$f.bin" $p | md5sum)
    n=$(./rust/target/release/cdc-ref "$work/$f.bin" $p | wc -l)
    if [ "$a" = "$b" ]; then echo "match  $f [$p] ($n chunks)"; else echo "DIFFER $f [$p]"; fail=1; fi
  done
done
exit $fail
