#!/bin/sh
# Prepare a fresh git worktree without network access (docs/development.md#워크트리-준비):
# - macOS: copies the patched libSDL3.dylib and the Scene panel bridge from the main checkout and links its SDL tarball
#   and CMake, so an editor or probe build neither downloads nor rebuilds SDL;
# - links player-templates/ to <main checkout>/.monoon-cache/players/<hash of the player sources>, building that entry once.
set -eu
cd "$(dirname "$0")/../.."
repo=$PWD
main=$(cd "$(git rev-parse --path-format=absolute --git-common-dir)/.." && pwd)

if [ "$(uname -s)" = Darwin ] && [ "$main" != "$repo" ]; then
  from=$main/engine/.artifacts/native
  to=$repo/engine/.artifacts/native
  mkdir -p "$to/osx-arm64"
  # Copy a library only when this checkout's sources for it match the main checkout's; otherwise MSBuild rebuilds it.
  # cp (not cp -p) gives the copy a newer mtime than the freshly checked-out sources, so MSBuild treats it as current.
  copy() {
    lib=$1; shift
    [ -f "$from/osx-arm64/$lib" ] || { echo "bootstrap-worktree: $from/osx-arm64/$lib not found; build the editor once in the main checkout." >&2; return; }
    for src do cmp -s "$main/$src" "$repo/$src" || { echo "bootstrap-worktree: $src differs from the main checkout; $lib will be rebuilt." >&2; return; }; done
    # The main checkout's build can be older than its own sources (they changed after its last editor build): don't spread it.
    for src do [ "$from/osx-arm64/$lib" -nt "$main/$src" ] || { echo "bootstrap-worktree: the main checkout's $lib is older than $src; $lib will be rebuilt." >&2; return; }; done
    cp "$from/osx-arm64/$lib" "$to/osx-arm64/$lib"
    echo "bootstrap-worktree: copied $lib"
  }
  # shortcut: only the patch is compared, not build-scene-sdl-mac.sh's CMake flags; compare the script too if they start changing.
  copy libSDL3.dylib engine/native/macos/SDL-3.4.12-pr16658.patch
  copy libBrutalScenePanel.dylib engine/native/macos/ScenePanel.m engine/native/macos/ScriptEditorPicker.m engine/src/Engine.Editor/MacScene.targets
  for dir in downloads tools; do
    rmdir "$to/$dir" 2>/dev/null || true
    if [ ! -e "$to/$dir" ] && [ -d "$from/$dir" ]; then ln -s "$from/$dir" "$to/$dir"; fi
  done
fi

# Everything build-players.sh and build-native-aot-kit.sh read.
sources="engine/src/Engine.Core engine/src/Engine.Desktop engine/src/Engine.Player engine/native engine/Directory.Build.props
  engine/Directory.Build.targets engine/scripts/build-players.sh engine/scripts/build-native-aot-kit.sh"
link=$repo/player-templates
if [ -n "${MONOON_PLAYERS_DIR:-}" ]; then
  echo "bootstrap-worktree: MONOON_PLAYERS_DIR is set; player templates left alone."
elif [ -d "$link" ] && [ ! -L "$link" ]; then
  echo "bootstrap-worktree: $link already holds locally built templates; left alone."
elif [ -n "$(git status --porcelain -- $sources)" ]; then
  [ -L "$link" ] && rm "$link"
  echo "bootstrap-worktree: player sources have uncommitted changes; run engine/scripts/build-players.sh for local templates." >&2
else
  key=$(for path in $sources; do git rev-parse "HEAD:$path"; done | shasum -a 256 | cut -c1-16)
  cache=$main/.monoon-cache/players
  if [ ! -x "$cache/$key/osx-arm64/MoNoonPlayer" ]; then
    mkdir -p "$cache"
    work=$(mktemp -d "$cache/.build.XXXXXX")
    echo "bootstrap-worktree: building player templates into the shared cache ($key)"
    if ! MONOON_PLAYERS_DIR=$work engine/scripts/build-players.sh; then rm -rf "$work"; exit 1; fi
    # shortcut: two worktrees building the same key at once both build; the second result is discarded.
    if [ -e "$cache/$key" ]; then rm -rf "$work"; else mv "$work" "$cache/$key"; fi
  fi
  ln -sfn "$cache/$key" "$link"
  echo "bootstrap-worktree: player-templates -> $cache/$key"
fi
