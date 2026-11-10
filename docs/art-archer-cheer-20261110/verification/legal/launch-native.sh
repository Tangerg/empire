#!/usr/bin/env bash
set -euo pipefail
[[ $# == 4 ]] || { echo 'Usage: launch-native.sh REPO VERIFIED_WITNESS EXPECTED_ASSEMBLIES NEW_QA_DIRECTORY' >&2; exit 2; }
repo=$(realpath "$1"); witness=$(realpath "$2"); assemblies=$(realpath "$3"); qa=$(realpath -m "$4")
[[ ! -e "$qa" ]] || { echo 'QA directory must be new' >&2; exit 2; }
cd "$repo"
source tools/godot/env.sh
for assembly in Empire.dll Empire.Core.dll GodotSharp.dll; do
    cmp "$repo/godot/.godot/mono/temp/bin/Debug/$assembly" "$assemblies/$assembly"
done
export XDG_DATA_HOME="$qa/xdg-data" XDG_CONFIG_HOME="$qa/xdg-config" XDG_CACHE_HOME="$qa/xdg-cache"
mkdir -p "$XDG_DATA_HOME/godot/app_userdata/Empire GrayFlag Migration" "$XDG_CONFIG_HOME" "$XDG_CACHE_HOME"
cp "$witness/before.json" "$XDG_DATA_HOME/godot/app_userdata/Empire GrayFlag Migration/grayflag-campaign-save.json"
cp "$witness/before.json" "$qa/native-input.json"
"$GODOT_NET" --path godot --resolution 960x640 --position 80,100 --audio-driver Dummy --fixed-fps 60 res://Scenes/NativeCapture.tscn -- --capture-dir="$qa/capture" --capture-size=960x640 2>&1 | tee "$qa/native.log"
cp "$XDG_DATA_HOME/godot/app_userdata/Empire GrayFlag Migration/grayflag-campaign-save.json" "$qa/native-output.json"
