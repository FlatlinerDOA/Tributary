#!/usr/bin/env bash
# Usage: run-matrix.sh [SECONDS]  -> results/<utc-timestamp>-matrix.txt
# Env: LOADS="0.3 0.7"  ALLOC_MBPS="50 200 1000"  ALLOC_RETAIN="256 4096"
#      SATORI=1 repeats every C# row on a self-contained build using the Satori GC (PublishWithSatoriGC)
set -euo pipefail
cd "$(dirname "$0")"
S=${1:-300}
LOADS=${LOADS:-"0.3 0.7"}
ALLOC_MBPS=${ALLOC_MBPS:-"50 200 1000"}
ALLOC_RETAIN=${ALLOC_RETAIN:-"256 4096"}
case "$(uname -s)" in MINGW*|MSYS*|CYGWIN*) os=win ;; Darwin) os=osx ;; *) os=linux ;; esac
case "$(uname -m)" in arm64|aarch64) arch=arm64 ;; *) arch=x64 ;; esac
runtimes="stock"
if [ "${SATORI:-0}" = 1 ]; then
  dotnet publish csharp -c Release -r "$os-$arch" --self-contained -p:UseSatoriGC=true -o csharp/out-satori -v quiet -nologo
  runtimes="stock satori"
fi
cs_for() { [ "$1" = satori ] && echo "./csharp/out-satori/Spike" || echo "dotnet csharp/bin/Release/net10.0/Spike.dll"; }
mkdir -p results
out=results/$(date -u +%Y%m%dT%H%M%SZ)-matrix.txt
rs=./rust/target/release/spike

host_info() {
  case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*)
      powershell.exe -NoProfile -Command \
        '$os = Get-CimInstance Win32_OperatingSystem; $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1; "$($os.Caption) $($os.Version), $($cpu.Name), $($cpu.NumberOfCores) cores / $($cpu.NumberOfLogicalProcessors) threads, power plan: $((powercfg /getactivescheme) -replace ''.*\((.*)\).*'',''$1'')"' | tr -d '\r' ;;
    Darwin) echo "$(sw_vers -productName) $(sw_vers -productVersion), $(sysctl -n machdep.cpu.brand_string), $(sysctl -n hw.ncpu) threads" ;;
    *) echo "$(. /etc/os-release && echo "$PRETTY_NAME") $(uname -r), $(grep -m1 'model name' /proc/cpuinfo | cut -d: -f2 | xargs), $(nproc) threads" ;;
  esac
}

steal() { [ -r /proc/stat ] && awk '/^cpu /{print $9}' /proc/stat || echo n/a; }

{
  echo "host: $(host_info)"
  echo "seconds-per-run=$S loads=\"$LOADS\" alloc-mbps=\"$ALLOC_MBPS\" alloc-retain=\"$ALLOC_RETAIN\" steal_ticks_at_start=$(steal)"
  for load in $LOADS; do
    echo; echo "## load=$load rust baseline";   $rs --seconds "$S" --load "$load"
    for rt in $runtimes; do
      cs=$(cs_for "$rt")
      echo; echo "## load=$load csharp[$rt] isolated"; $cs --seconds "$S" --load "$load"
      for retain in $ALLOC_RETAIN; do
        for mbps in $ALLOC_MBPS; do
          echo; echo "## load=$load csharp[$rt] alloc mbps=$mbps retain=$retain default"
          $cs --seconds "$S" --load "$load" --alloc-threads 2 --alloc-mbps "$mbps" --alloc-retain "$retain"
        done
      done
      for mode in lowlatency lowlatency-strict; do
        echo; echo "## load=$load csharp[$rt] alloc mbps=200 retain=4096 $mode"
        $cs --seconds "$S" --load "$load" --alloc-threads 2 --alloc-mbps 200 --alloc-retain 4096 --gc "$mode"
      done
    done
  done
  echo; echo "steal_ticks_at_end=$(steal) (USER_HZ ticks, all CPUs; Linux only, non-zero only on virtualised hosts)"
} | tee "$out"
