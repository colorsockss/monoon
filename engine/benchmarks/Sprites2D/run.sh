#!/bin/sh
set -eu

cd "$(dirname "$0")/../.."
output="$PWD/.artifacts/sprite-benchmark"
case "${1:-}" in
    "") projects="Brutal MonoGame" ;;
    vulkan-metal|batching) projects="Brutal MonoGameVK" ;;
    *) echo "Usage: sh run.sh [vulkan-metal | batching [pre-change-Brutal.dll]]" >&2; exit 2 ;;
esac
for project in $projects; do
    dotnet build "benchmarks/Sprites2D/$project/$project.csproj" -c Release --artifacts-path "$output" -m:1
done

run_round() {
    for backend in "$@"; do
        case "$backend" in
            default) dotnet "$output/bin/Brutal/release/Brutal.dll" ;;
            opengl)  dotnet "$output/bin/Brutal/release/Brutal.dll" opengl ;;
            monogame) dotnet "$output/bin/MonoGame/release/MonoGame.dll" ;;
            metal)   dotnet "$output/bin/Brutal/release/Brutal.dll" metal ;;
            vulkan)  dotnet "$output/bin/MonoGameVK/release/MonoGameVK.dll" ;;
            original)
                if [ -n "$original_binary" ]; then
                    dotnet "$original_binary" metal
                else
                    dotnet "$output/bin/Brutal/release/Brutal.dll" metal --unbatched
                fi ;;
        esac
    done
}

if [ "${1:-}" = batching ]; then
    original_binary=${2:-}
    dotnet "$output/bin/Brutal/release/Brutal.dll" metal --verify-batching
    # All six orders, so each backend occupies each position twice.
    run_round original metal vulkan
    run_round metal vulkan original
    run_round vulkan original metal
    run_round vulkan metal original
    run_round metal original vulkan
    run_round original vulkan metal
    exit 0
fi

if [ "${1:-}" = vulkan-metal ]; then
    # Each backend goes first in two of the four pairs.
    run_round vulkan metal
    run_round metal vulkan
    run_round vulkan metal
    run_round metal vulkan
    exit 0
fi

# Rotate the order so each renderer runs first, second and third once.
run_round default opengl monogame
run_round opengl monogame default
run_round monogame default opengl
