#!/usr/bin/env bash
# Usage: run-matrix.sh SECONDS  -> results/<timestamp>-matrix.txt
set -euo pipefail
cd "$(dirname "$0")"
S=${1:-120}
out=results/$(date -u +%Y%m%dT%H%M%SZ)-matrix.txt
cs="dotnet csharp/bin/Release/net10.0/Spike.dll"
{
  echo "host: $(nproc) vCPU, $(uname -r), seconds-per-run=$S"
  for load in 0.3 0.7; do
    echo; echo "## load=$load rust baseline";                    ./rust/target/release/spike --seconds "$S" --load "$load"
    echo; echo "## load=$load csharp isolated";                  $cs --seconds "$S" --load "$load"
    echo; echo "## load=$load csharp alloc-load default";        $cs --seconds "$S" --load "$load" --alloc-threads 2
    echo; echo "## load=$load csharp alloc-load sustained-low";  $cs --seconds "$S" --load "$load" --alloc-threads 2 --gc lowlatency
  done
} | tee "$out"
