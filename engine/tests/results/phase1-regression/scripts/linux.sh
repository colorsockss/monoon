#!/bin/sh
# Run from the main checkout root with the Debian guest booted by .test-env/work/ch11/boot.sh (work disk at /run/keys96).
#   linux.sh fresh            : copy p1reg.zip, new run folder, run guest.py
#   linux.sh resume [steps]   : newer guest.py in the same run folder, optional steps
#   linux.sh fetch            : copy results back to the Mac (before shutdown: the run folder is on tmpfs)
set -eu
G=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/p1reg/guest
ssh=".test-env/linux/ssh.conf"
# The work disk has ~376 MB free: the run folder lives on a RAM tmpfs that stays mounted until the guest shuts down.
ssh -F $ssh monoon-qa 'mountpoint -q /run/p1reg || { sudo mkdir -p /run/p1reg && sudo mount -t tmpfs -o size=2048M,mode=1777 tmpfs /run/p1reg; }'
case "$1" in
  fresh) scp -F $ssh "$G/p1reg.zip" monoon-qa:/run/p1reg/ ;;
  fetch) rm -rf "$G/linux-results"; scp -r -F $ssh monoon-qa:/run/p1reg/run/results "$G/linux-results"; exit 0 ;;
  resume) scp -F $ssh "$G/guest.py" monoon-qa:/run/p1reg/run/guest.py ;;
esac
ssh -F $ssh monoon-qa "sudo unshare -m --propagation private sh -s $1 \"${2:-}\"" <<'GUEST'
set -eu
mount -t tmpfs -o size=2048M,mode=1777 tmpfs /tmp  # work disk is nearly full: temp files and game builds live here
mkdir -m 1777 /tmp/.X11-unix
dpkg-deb -x /run/keys96/v102/libosmesa6_*.deb /tmp/mesa
export LD_LIBRARY_PATH=/tmp/mesa/usr/lib/x86_64-linux-gnu
exec runuser -u debian -- bash -c '
set -eu
cd /run/p1reg
if [ "$1" = fresh ]; then rm -rf run && mkdir run && cd run && python3 -m zipfile -e ../p1reg.zip . && rm ../p1reg.zip; else cd run; fi
mkdir -p cache config home /tmp/t /tmp/products && ln -sfn /tmp/products products
export DOTNET_ROOT=/home/debian/monoon-buildtools/dotnet
export PATH="$DOTNET_ROOT:$PATH" RestoreSources=/home/debian/monoon-buildtools/feed
export TMPDIR=/tmp/t XDG_CACHE_HOME=$PWD/cache XDG_CONFIG_HOME=$PWD/config DOTNET_CLI_HOME=$PWD/home NUGET_PACKAGES=/run/keys96/nuget
export NuGetAudit=false DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 SDL_AUDIODRIVER=dummy SDL_AUDIO_DRIVER=dummy LANG=C.UTF-8
rm -f steps.txt; if [ -n "${2:-}" ]; then echo "$2" > steps.txt; fi
xvfb-run -a -s "-screen 0 2560x1440x24" python3 -u guest.py' _ "$@"
GUEST
