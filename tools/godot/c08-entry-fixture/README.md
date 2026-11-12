# C08 entry from a verified C07 victory

This is an isolated visual/input acceptance entry. It starts detached at C07,
replays all 263 recorded legal orders with normal-AI choices and every state hash
checked, and settles that one real victory through the production campaign.
**It is not a continuous C01–C08 playthrough.** No C01–C06 victories are invented.
Torren's recorded casualty and the surviving roster's injuries/progression remain.

The existing `LuojiaChecks` owns the replay and both C08 contract-choice checks;
this export does not introduce a second replay implementation. Only after those
checks pass are briefing, pre-choice and both branches' staging saves exported.
Each save is admitted by the unchanged production loader and read back from disk.

```bash
source tools/godot/env.sh
dotnet build godot/Empire.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false
output=$(mktemp -d /tmp/empire-c08-entry.XXXXXX)
checks_data=$(mktemp -d /tmp/empire-c08-check-data.XXXXXX)
XDG_DATA_HOME="$checks_data" EMPIRE_CHECK_TIMEOUT_SECONDS=900 \
  bash tools/godot/native-process.sh "$output/checks.log" 'C07 verified C08 entry' \
  "$GODOT_NET" --headless --path godot res://Scenes/LuojiaChecks.tscn -- \
  "--export-c08-gui-dir=$output"
```

Use a new GUI namespace, never a user's existing slots:

```bash
gui_data=$(mktemp -d /tmp/empire-c08-gui-data.XXXXXX)
slots="$gui_data/godot/app_userdata/Empire GrayFlag Migration"
mkdir -p "$slots"
cp "$output/c08-carried-briefing-campaign.json" "$slots/grayflag-campaign-autosave.json"
XDG_DATA_HOME="$gui_data" "$GODOT_NET" --path godot --resolution 1180x812 \
  res://Scenes/NativeCapture.tscn -- "--capture-dir=$output/native" --level-entry-fixture
```

NativeCapture wraps the default formal GrayFlagGame, not a replacement battle
screen. Choose Continue from its normal menu, then advance the C08 briefing,
choose a contract and enter battle from staging. Keep the fixture label visible
and disclose the detached C07 start alongside screenshots. This generic label
indicates noncontinuous entry; the C07 outcome here is replayed, not synthesized.

`c08-carried-choice-campaign.json` starts immediately before choosing. The
`c08-ledger-staging-campaign.json` and `c08-paid-staging-campaign.json` files retain
the respective authored choice. Files ending `-application.json` are the separate
manual F9 slot format (`grayflag-campaign-save.json`), not campaign-slot files.
Do not interchange them. No generated fixture is a new source of game rules.
