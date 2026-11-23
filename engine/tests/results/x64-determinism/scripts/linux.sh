#!/bin/sh
# Mac side (cwd: main checkout root, guest booted by .test-env/work/ch11/boot.sh).
#   linux.sh fresh            : copy x64det.zip, new run folder, run guest.py (Joints/Paths/Lines/TileCollision)
#   linux.sh codegen          : x64 JIT (DOTNET_JitDisasm) and Native AOT (ILC JitDisasm) listings of the Affine2D probe
#   linux.sh resume [steps]   : rerun guest.py steps in the same run folder
#   linux.sh replay <before|after> : engine/tests/InventoryLab.Replay/build_guest.py from that source snapshot
set -eu
here=$(cd "$(dirname "$0")" && pwd)
ssh=".test-env/linux/ssh.conf"
ssh -F $ssh monoon-qa 'sudo mkdir -p /run/keys96/x64det && sudo chown debian:debian /run/keys96/x64det'
case "$1" in
  fresh) scp -F $ssh "$here/x64det.zip" monoon-qa:/run/keys96/x64det/ ;;
  resume) scp -F $ssh "$here/guest.py" monoon-qa:/run/keys96/x64det/run/guest.py ;;
  bisect) ssh -F $ssh monoon-qa 'mkdir -p /run/keys96/x64det/run/variants'
          scp -F $ssh "$here/bisect.py" monoon-qa:/run/keys96/x64det/run/bisect.py
          scp -F $ssh "$here"/../variants/Engine.Core.v?.dll monoon-qa:/run/keys96/x64det/run/variants/ ;;
esac
ssh -F $ssh monoon-qa "sudo unshare -m --propagation private sh -s $1 \"${2:-}\"" <<'GUEST'
set -eu
mount -t tmpfs -o size=2048M,mode=1777 tmpfs /tmp  # work disk is nearly full: temp files and game builds live here
mkdir -m 1777 /tmp/.X11-unix
dpkg-deb -x /run/keys96/v102/libosmesa6_*.deb /tmp/mesa
export LD_LIBRARY_PATH=/tmp/mesa/usr/lib/x86_64-linux-gnu
exec runuser -u debian -- bash -c '
set -eu
cd /run/keys96/x64det
if [ "$1" = fresh ]; then rm -rf run && mkdir run && cd run && python3 -m zipfile -e ../x64det.zip . && rm ../x64det.zip; else cd run; fi
mkdir -p cache config home /tmp/t /tmp/products && ln -sfn /tmp/products products
export DOTNET_ROOT=/home/debian/monoon-buildtools/dotnet
export PATH="$DOTNET_ROOT:$PATH" RestoreSources=/home/debian/monoon-buildtools/feed
export TMPDIR=/tmp/t XDG_CACHE_HOME=$PWD/cache XDG_CONFIG_HOME=$PWD/config DOTNET_CLI_HOME=$PWD/home NUGET_PACKAGES=/run/keys96/nuget
export NuGetAudit=false DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 SDL_AUDIODRIVER=dummy SDL_AUDIO_DRIVER=dummy LANG=C.UTF-8
case "$1" in
  fresh|resume)
    rm -f steps.txt; if [ -n "${2:-}" ]; then echo "$2" > steps.txt; fi
    xvfb-run -a -s "-screen 0 2560x1440x24" python3 -u guest.py ;;
  bisect) python3 -u bisect.py ${2:-} ;;
  codegen)
    export RestoreSources="$RestoreSources;$PWD/feed"
    rm -rf src-after codegen/out && mkdir src-after codegen/out && (cd src-after && python3 -m zipfile -e ../src-after.zip .)
    core=$PWD/src-after/engine/src/Engine.Core/Engine.Core.csproj; o=$PWD/codegen/out
    dotnet build codegen/Probe.csproj -c Release -m:1 -p:EngineCore=$core -o /tmp/cg-jit > $o/jit-build.log 2>&1
    DOTNET_TieredCompilation=0 DOTNET_JitStdOutFile=$o/jit-x64.asm DOTNET_JitDisasm="Program:Call* Program:Loop*" dotnet /tmp/cg-jit/Probe.dll nobench > $o/jit-run.txt 2>&1
    DOTNET_EnableAVX2=0 dotnet /tmp/cg-jit/Probe.dll nobench >> $o/jit-run.txt 2>&1
    dotnet publish codegen/Probe.csproj -c Release -m:1 -r linux-x64 -p:PublishAot=true -p:EngineCore=$core -p:Disasm=$o/aot-x64.asm -p:DisasmPattern="*"       -p:IntermediateOutputPath=/tmp/cg-obj/ -o /tmp/cg-aot > $o/aot-build.log 2>&1 || echo "AOT exit $?"
    /tmp/cg-aot/Probe nobench >> $o/jit-run.txt 2>&1 || echo "AOT run exit $?"
    xz -T2 $o/aot-x64.asm; rm -rf src-after ;;
  replay)  # source, builds and results on tmpfs (work disk is nearly full); results copied back
    export RestoreSources="$RestoreSources;$PWD/feed"
    run=$PWD; rm -rf src-$2 replay-$2 /tmp/src-$2 /tmp/replay-$2 && mkdir /tmp/src-$2 && (cd /tmp/src-$2 && python3 -m zipfile -e $run/src-$2.zip .)
    (cd /tmp/src-$2 && python3 -u engine/tests/InventoryLab.Replay/build_guest.py /tmp/replay-$2/builds /tmp/replay-$2/results --expected-rid linux-x64) \
      || echo "REPLAY-$2 exit $?"
    mkdir -p replay-$2 && cp -r /tmp/replay-$2/results replay-$2/ ;;
esac' _ "$@"
GUEST
