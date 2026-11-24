#!/bin/sh
# Run from the main checkout root with the Debian guest booted by .test-env/work/ch11/boot.sh (work disk at /run/keys96).
#   linux.sh fresh            : copy n1x64.zip, new run folder, run guest.py
#   linux.sh resume [steps]   : newer guest.py in the same run folder, optional steps
#   linux.sh extra <zip>      : unzip another zip (cli-speed/, base/) into the run folder, no run
#   linux.sh fetch            : copy results back to the Mac (before shutdown: the run folder is on tmpfs)
# Adapted from phase1-regression/scripts/linux.sh.
set -eu
S=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/n1x64
H=/Users/paare/codex_project/brutal_engine/.worktrees/x64reg/engine/tests/results/next1-x64/scripts
ssh=".test-env/linux/ssh.conf"
# The work disk has ~340 MB free: the run folder lives on a RAM tmpfs that stays mounted until the guest shuts down.
ssh -F $ssh monoon-qa 'mountpoint -q /run/n1x64 || { sudo mkdir -p /run/n1x64 && sudo mount -t tmpfs -o size=2560M,mode=1777 tmpfs /run/n1x64; }'
case "$1" in
  fresh) scp -F $ssh "$S/n1x64.zip" monoon-qa:/run/n1x64/ ;;
  fetch) rm -rf "$S/linux-results"; scp -r -F $ssh monoon-qa:/run/n1x64/run/results "$S/linux-results"; exit 0 ;;
  resume) scp -F $ssh "$H/guest.py" monoon-qa:/run/n1x64/run/guest.py; scp -F $ssh "$H/sitecustomize.py" monoon-qa:/run/n1x64/run/site/ ;;
  extra) scp -F $ssh "$2" monoon-qa:/run/n1x64/extra.zip
         ssh -F $ssh monoon-qa 'cd /run/n1x64/run && python3 -m zipfile -e ../extra.zip . && rm ../extra.zip'; exit 0 ;;
esac
ssh -F $ssh monoon-qa "sudo unshare -m --propagation private sh -s $1 \"${2:-}\"" <<'GUEST'
set -eu
mount -t tmpfs -o size=2048M,mode=1777 tmpfs /tmp  # work disk is nearly full: temp files and game builds live here
mkdir -m 1777 /tmp/.X11-unix
dpkg-deb -x /run/keys96/v102/libosmesa6_*.deb /tmp/mesa
export LD_LIBRARY_PATH=/tmp/mesa/usr/lib/x86_64-linux-gnu
exec runuser -u debian -- bash -c '
set -eu
cd /run/n1x64
if [ "$1" = fresh ]; then rm -rf run && mkdir run && cd run && python3 -m zipfile -e ../n1x64.zip . && rm ../n1x64.zip && chmod +x repo/monoon; else cd run; fi
find repo/player-templates/linux-x64 -maxdepth 1 -name MoNoonPlayer -exec chmod +x {} +
mkdir -p cache config home /tmp/t
export DOTNET_ROOT=/home/debian/monoon-buildtools/dotnet
export PATH="$DOTNET_ROOT:$PATH" RestoreSources=/home/debian/monoon-buildtools/feed
export TMPDIR=/tmp/t XDG_CACHE_HOME=$PWD/cache XDG_CONFIG_HOME=$PWD/config DOTNET_CLI_HOME=$PWD/home NUGET_PACKAGES=/run/keys96/nuget
export NuGetAudit=false DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 SDL_AUDIODRIVER=dummy SDL_AUDIO_DRIVER=dummy LANG=C.UTF-8
rm -f steps.txt; if [ -n "${2:-}" ]; then echo "$2" > steps.txt; fi
xvfb-run -a -s "-screen 0 2560x1440x24" python3 -u guest.py' _ "$@"
GUEST
