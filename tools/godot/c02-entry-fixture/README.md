# Explicit C02 entry fixture

This generates a current-schema application save for visual/input acceptance of
formal C02. **It synthesizes the earlier C01 conclusion. It is not a continuous
campaign victory, a C02 outcome or normal-AI play evidence.** C02 rules and its
opening state are canonical; no C02 action or enemy turn is skipped.

From repository root, with the official toolchain available:

```bash
source tools/godot/env.sh
scratch=$(mktemp -d /tmp/empire-c02-entry.XXXXXX)
python3 tools/godot/localization-checks/validate_catalog.py \
  godot/Localization/zh_CN.po --output "$scratch/catalog.json"
dotnet run --project tools/godot/c02-entry-fixture/C02EntryFixture.csproj -- \
  "$PWD" "$scratch/catalog.json" "$scratch/c02-entry-FIXTURE.json"
```

Use only a fresh isolated `XDG_DATA_HOME` when opening this fixture. Copy it to
that namespace's manual save path, launch NativeCapture with an absolute capture
directory and **`--level-entry-fixture`**, then load with normal F9. The capture
flag labels both native title and viewport; do not omit it from screenshots.
Never overwrite a user's real manual/campaign save. No persistent campaign
progress or production asset is changed by this tool.
