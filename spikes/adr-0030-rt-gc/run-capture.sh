#!/usr/bin/env bash
# NativeAOT isolation test: does the host process's GC suspend a capture thread owned by a NativeAOT shim?
# Usage: run-capture.sh [SECONDS]  -> results/<utc-timestamp>-capture.txt
# Env: LOADS="0:4096 200:256 200:4096 1000:4096"  (alloc-mbps:alloc-retain; 0 = no allocation threads)
#      SATORI=1 also runs every row with the host built on the Satori GC (PublishWithSatoriGC)
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
hosts="stock"
if [ "${SATORI:-0}" = 1 ]; then
  dotnet publish capture-host -c Release -r "$os-$arch" --self-contained -p:UseSatoriGC=true -o capture-host/out-satori -v quiet -nologo
  hosts="stock satori"
fi
for shim in "$PWD"/capture-shim/out/capture_shim.{so,dll,dylib}; do [ -f "$shim" ] && break; done
host_for() { [ "$1" = satori ] && echo "./capture-host/out-satori/CaptureHost --shim $shim" || echo "dotnet capture-host/bin/Release/net10.0/CaptureHost.dll"; }
mkdir -p results
out=results/$(date -u +%Y%m%dT%H%M%SZ)-capture.txt
steal() { [ -r /proc/stat ] && awk '/^cpu /{print $9}' /proc/stat || echo n/a; }
{
  echo "seconds-per-run=$S rid=$os-$arch steal_ticks_at_start=$(steal)"
  for load in $LOADS; do
    mbps=${load%%:*}; retain=${load##*:}; threads=2; [ "$mbps" = 0 ] && threads=0
    for h in $hosts; do
      for capture in managed shim; do
        echo; echo "## host=$h capture=$capture alloc-mbps=$mbps alloc-retain=$retain"
        $(host_for "$h") --capture "$capture" --seconds "$S" --alloc-threads "$threads" --alloc-mbps "$mbps" --alloc-retain "$retain"
      done
    done
  done
  echo; echo "steal_ticks_at_end=$(steal)"
} | tee "$out"
