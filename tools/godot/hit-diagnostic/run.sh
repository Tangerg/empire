#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$root"
if [[ $# -ne 4 ]]; then
  echo "Usage: $0 OUTPUT_DIRECTORY cached|uncached shipped|markers fixed60|realtime|movie60" >&2
  exit 2
fi
output="$(realpath -m "$1")"
geometry="$2"; clip="$3"; mode="$4"
case "$geometry:$clip:$mode" in
  cached:shipped:fixed60|cached:shipped:realtime|cached:shipped:movie60|cached:markers:fixed60|cached:markers:realtime|cached:markers:movie60|uncached:shipped:fixed60|uncached:shipped:realtime|uncached:shipped:movie60|uncached:markers:fixed60|uncached:markers:realtime|uncached:markers:movie60) ;;
  *) echo "Invalid experiment options" >&2; exit 2 ;;
esac
if [[ "$mode" == movie60 && -z "${DISPLAY:-}" ]]; then
  echo "movie60 requires a working graphical DISPLAY; headless cannot provide rendered evidence." >&2
  exit 2
fi
[[ ! -e "$output" ]] || { echo "Output already exists; refusing to overwrite: $output" >&2; exit 2; }
mkdir -p "$output"
source tools/godot/env.sh
stage="$(mktemp -d "$output/stage.XXXXXX")"
printf '%s\n' "$stage" > "$output/staging-path.txt"
# Keep the diagnostic, generated assets, temporary saves, and baseline edits out of the checkout.
cp -a --reflink=auto godot "$stage/godot"
cp tools/godot/hit-diagnostic/HitDiagnosticChecks.cs "$stage/godot/Presentation/"
printf '[gd_scene load_steps=2 format=3]\n[ext_resource type="Script" path="res://Presentation/HitDiagnosticChecks.cs" id="1"]\n[node name="HitDiagnosticChecks" type="Node"]\nscript = ExtResource("1")\n' > "$stage/godot/Scenes/HitDiagnosticChecks.tscn"
if [[ "$geometry" == uncached ]]; then
  git show 1d5cbd0a4f0adc84ce71d9fbe05ae8775197bf2a:godot/Presentation/PaintedSurface.cs > "$stage/godot/Presentation/PaintedSurface.cs"
fi
sha256sum "$stage/godot/Presentation/PaintedSurface.cs" godot/tests/native/c01-04-facing.json tools/godot/hit-diagnostic/HitDiagnosticChecks.cs > "$output/inputs.sha256"
git rev-parse HEAD > "$output/source-commit.txt"
timeout 180 "$DOTNET_ROOT/dotnet" build "$stage/godot/Empire.csproj" --disable-build-servers -m:1 -p:UseSharedCompilation=false > "$output/build.log" 2>&1
if [[ ! -d "$stage/godot/.godot/imported" ]]; then
  bash tools/godot/native-process.sh "$output/import.log" "Hit diagnostic import" \
    "$GODOT_NET" --headless --editor --path "$stage/godot" --import
fi
args=(--headless)
case "$mode" in
 fixed60) args+=(--fixed-fps 60) ;;
 realtime) [[ -z "${DISPLAY:-}" ]] || args=(--audio-driver Dummy) ;;
 movie60)
  [[ -n "${DISPLAY:-}" ]] || { echo "movie60 requires a working graphical DISPLAY; headless cannot provide rendered evidence." >&2; exit 2; }
  args=(--audio-driver Dummy --fixed-fps 60 --write-movie "$output/movie.png") ;;
esac
export EMPIRE_HIT_OUTPUT="$output" EMPIRE_HIT_MODE="$mode"
export EMPIRE_HIT_CANDIDATE=0 EMPIRE_HIT_CAPTURE=0
[[ "$clip" != markers ]] || export EMPIRE_HIT_CANDIDATE=1
[[ "$mode" != movie60 ]] || export EMPIRE_HIT_CAPTURE=1
XDG_DATA_HOME="$stage/user-data" bash tools/godot/native-process.sh "$output/native.log" "Hit diagnostic" \
 "$GODOT_NET" "${args[@]}" --path "$stage/godot" res://Scenes/HitDiagnosticChecks.tscn
[[ -s "$output/trace.json" ]]
