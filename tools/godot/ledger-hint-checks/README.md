# Portable ledger HUD checks

Run from the repository root after applying the ledger HUD implementation and reviewed C08 A2 level. No research directory, saved campaign checkpoint, external file argument or GUI is required.

```sh
source tools/godot/env.sh
dotnet build godot/Empire.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false
XDG_DATA_HOME=$(mktemp -d) bash tools/godot/native-process.sh /tmp/ledger-hint-checks.log ledger-hint-checks "$GODOT_NET" --headless --path godot res://Scenes/LedgerHintChecks.tscn
```

Add `LedgerHintChecks` to the normal native scene gate. Keep the existing `LuojiaChecks` gate for actual carried campaign choices: LuojiaContinuationChecks executes C07 production continuation, both contracts, fallen Torren exclusion, wounded Bran carry, treasury/relation/institution differences and the 40-fund starting delta. This new focused test does not claim to reproduce those campaign histories.

## Fixture provenance and behavior
`godot/data/checks/c08-ledger-prefix.json` is only 26 legal orders with before/after state hashes (4494 bytes), extracted from the retained original-AI A2 standalone opening. It contains no snapshot, credentials, absolute path or generated event. Each order executes through the production Battle engine from the current authored C08 level. A wrong input or changed rule fails the exact hash checks. It does not call the AI planner and is therefore not coupled to later planner-weight fixes.

After these commands the test uses production movement/path/command queries to select a live player unit that can stop on the authored ledger zone or pass through it. The same legal path prefix supplies the positive witness. All afterAction effects and signals are produced by the engine, not inserted by the test. Production Save/FromSave establishes the application session and checks the completed save roundtrip. No actor, HP, turn, zone, trigger or progress field is edited.

The current witness is turn 2 Bran, from (10,5) via (10,6) to (10,7), wait; coordinates are not copied into the test. The negative route passes through and ends outside. The test asserts pending/visible instructions, canonical target cells, busy suppression, foreign-owner filtering, projection purity, single original collection signal, completed state, serialized load, stale revision rejection, Undo, no collection on mere traversal, plus strict metadata rejection.

To intentionally regenerate after an authorized level/rule change, run a normal production C08 battle, retain its exact legal action receipts, and extract the shortest prefix reaching the witness. Store only action/beforeHash/stateHash. Independently replay from the authored initial level before replacing fixture hashes. Never update hashes merely to silence a regression, fabricate saves or edit state to make the path legal.

Validation: build 0 warnings/errors; native diagnostic gate exit 0, 77 assertions. An initial attempt using the opening without any commands had no legal traversal witness and failed; it was replaced with this small real command prefix rather than state manipulation. A runtime-number read was corrected to EcmaJson.ToElement(...).GetDouble(). Existing two-contract full-route research remains external evidence, not shipped fixtures.
