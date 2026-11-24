#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
cache="$PWD/.artifacts/native"
patchfile="$PWD/native/macos/SDL-3.4.12-pr16658.patch"
archive="$cache/downloads/SDL3-3.4.12.tar.gz"
sha=f07b958a9ac5020fb7a44cadb957f658b2149c3c8abb4f63145fac9303249db7
if [ ! -f "$archive" ] && [ "${MONOON_ALLOW_SDL_DOWNLOAD:-}" != 1 ]; then
  echo "error: $archive is missing and the patched libSDL3.dylib must be rebuilt." >&2
  echo "  In a git worktree run engine/scripts/bootstrap-worktree.sh (copies the library from the main checkout)." >&2
  echo "  To download SDL 3.4.12 from GitHub instead, set MONOON_ALLOW_SDL_DOWNLOAD=1." >&2
  exit 1
fi
mkdir -p "$cache/downloads"
stage=$(mktemp -d "$cache/stage.XXXXXX")
trap 'rm -rf "$stage"' EXIT
if [ ! -f "$archive" ]; then
  curl -fLsS --retry 2 https://github.com/libsdl-org/SDL/releases/download/release-3.4.12/SDL3-3.4.12.tar.gz -o "$stage/source.tar.gz"
  printf '%s  %s\n' "$sha" "$stage/source.tar.gz" | shasum -a 256 -c -
  mv "$stage/source.tar.gz" "$archive"
fi
printf '%s  %s\n' "$sha" "$archive" | shasum -a 256 -c -
mkdir "$stage/source"
tar -xzf "$archive" -C "$stage/source" --strip-components=1
patch -d "$stage/source" -p1 -F 0 < "$patchfile"
cmake_cmd=${CMAKE:-cmake}
if ! command -v "$cmake_cmd" >/dev/null && [ -x "$cache/tools/cmake/data/bin/cmake" ]; then
  cmake_cmd="$cache/tools/cmake/data/bin/cmake"
fi
"$cmake_cmd" -S "$stage/source" -B "$stage/obj" -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_OSX_ARCHITECTURES=arm64 -DCMAKE_OSX_DEPLOYMENT_TARGET=12.0 \
  -DSDL_VENDOR_INFO=BrutalEditor-PR16658 \
  -DSDL_FRAMEWORK=OFF -DSDL_SHARED=ON -DSDL_STATIC=OFF -DSDL_TEST_LIBRARY=OFF -DSDL_TESTS=OFF -DSDL_EXAMPLES=OFF
"$cmake_cmd" --build "$stage/obj" --target SDL3-shared --parallel 8
mkdir -p "$cache/osx-arm64"
cp -L "$stage/obj/libSDL3.0.dylib" "$stage/libSDL3.dylib"
mv "$stage/libSDL3.dylib" "$cache/osx-arm64/libSDL3.dylib"
