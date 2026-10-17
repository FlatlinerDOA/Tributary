#!/usr/bin/env bash
# NativeAOT isolation test: does the host process's GC suspend a capture thread owned by a NativeAOT shim?
# Usage: run-capture.sh [SECONDS]  -> results/<utc-timestamp>-capture.txt
# Env: LOADS="0:4096 200:256 200:4096 1000:4096"  (alloc-mbps:alloc-retain; 0 = no allocation threads)
set -euo pipefail
cd "$(dirname "$0")"
S=${1:-300}
LOADS=${LOADS:-"0:4096 200:256 200:4096 1000:4096"}
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) os=win ;;
  Darwin) os=osx ;;
  *) os=linux ;;
esac
case "$(uname -m)" in
  arm64|aarch64) arch=arm64 ;;
  *) arch=x64 ;;
esac
dotnet publish capture-shim -c Release -r "$os-$arch" -o capture-shim/out -v quiet -nologo
dotnet build capture-host -c Release -v quiet -nologo
mkdir -p results
out=results/$(date -u +%Y%m%dT%H%M%SZ)-capture.txt
host="dotnet capture-host/bin/Release/net10.0/CaptureHost.dll"
steal() { [ -r /proc/stat ] && awk '/^cpu /{print $9}' /proc/stat || echo n/a; }
{
  echo "seconds-per-run=$S rid=$os-$arch steal_ticks_at_start=$(steal)"
  for load in $LOADS; do
    mbps=${load%%:*}; retain=${load##*:}; threads=2; [ "$mbps" = 0 ] && threads=0
    for capture in managed shim; do
      echo; echo "## capture=$capture alloc-mbps=$mbps alloc-retain=$retain"
      $host --capture "$capture" --seconds "$S" --alloc-threads "$threads" --alloc-mbps "$mbps" --alloc-retain "$retain"
    done
  done
  echo; echo "steal_ticks_at_end=$(steal)"
} | tee "$out"
