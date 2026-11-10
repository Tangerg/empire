# Legal C01 archer death witness

The official campaign begins through normal story operations and steady-advance.
Its first 151 actions exactly reproduce the accepted southern-bows settled replay,
including every action, before/after hash and event. Thereafter the human only
ends turns and the regular opponent AI acts. No battle, position, HP, facing,
camera, controller, random seed or receipt is edited.

At zero-based step 167, enemy archer 18 at (23,21) shoots east-facing Elin archer
3 at (22,20), killing her with 4 damage (4 -> 0 HP). No movement precedes this
shot. The normal sequence is attackActivated, attack(killed=true), death,
markerAdded, rankProgressChanged, attackCompleted. State hashes are 489512a2 ->
329dacf4. Receipt event SHA256 is
2357b977746dd9296c91b0d11d69a5f6e871d29c6874466d2a40061099f98996.

Both participants are engine-visible to player 1, on the southern ground used by
the previous accepted native recoil recording. Pixel visibility and authored
fall quality still require native inspection. Only east-facing death is covered;
this is not a native or two-facing visual acceptance claim.

## Verified checks

The scratch-only harness builds with zero warnings/errors. Every committed
order is dispatched independently through a core loaded from the preceding
official save and compared for complete state and receipt equality. All four
checkpoints roundtrip through GrayFlagCampaign.Load without changes except the
two writer-generated timestamps. Freshly loading before.json and running normal
AI yields the same two actions and full settled save. The first AI command is
the lethal shot, and the second is endTurn. Settled hash: 0d654a89. The production
selection controller E from ui-trigger-before.json emits the exact preceding
human endTurn (step166). No real player save is opened or written.

## Final implementation provenance

This archived derivation ran against implementation commit
90952ac7a0d74f3f43b7dbeb0971a27df29b6dac, Git tree
8d213612c8067bbc73a49a079414430bac2bce84. No production source was changed
after that commit for this verification. Documentation updates were pending
when archived. The copied executed harness is byte-identical to harness/Program.cs;
its unused third argument has been removed. See provenance.json for source and
assembly SHA256 values. The three frozen assemblies were compared byte-for-byte
with the actual copies loaded by the final harness.

The native F5 output in native/native-output.json matches ui-settled.json in its
entirety, excluding only campaign.savedAt and battle.savedAt in detached copies.
The native input is byte-identical to before.json. native-save-comparison.json
preserves the successful comparison and exact original save hashes. Neither
archived save is normalized or rewritten. This proves complete canonical state
and event preservation through the actual native run, not visual quality.
Native animation frames and visual review are maintained separately by the
parent verification package. Only the east-facing death is witnessed here.

## Reproduce

    EMPIRE_TOOLCHAIN_ROOT=/path/to/official-toolchain bash /path/to/package/derive.sh --repo-root /path/to/repo --assembly-dir /path/to/final-assemblies --output /new/empty/output

Only the harness is compiled. Output must be outside the repository and new or
empty. Existing game assemblies are read only. No game build occurs here.

## Exact native plan

Use the actual desktop terminal with a real display:

    EMPIRE_TOOLCHAIN_ROOT=/path/to/official-toolchain bash /path/to/package/launch-native.sh /path/to/repo /path/to/final-verified-output /path/to/final-assemblies /new/qa-directory

This launch verifies all three live assemblies byte-for-byte against the frozen
final assemblies. It places before.json only in a fresh isolated XDG save folder,
then launches the existing NativeCapture scene at 960x640, fixed 60 fps. On the
home screen press F8 once to arm next-transition capture, then F9 to load. Do not
inject any order or camera movement. Ordinary AI performs the lethal shot and
endTurn. Wait until the native motion capture has completed and human turn 15
is settled. Press F5 once to save, then quit normally so the launch script copies
the output. Compare the complete official save:

    python3 /path/to/package/compare-native.py /path/to/final-verified-output/ui-settled.json /new/qa-directory/native-output.json

Rerun the archived comparison from this directory:

    python3 compare-native.py ui-settled.json native/native-output.json

Only campaign.savedAt and battle.savedAt may be omitted in detached comparison
copies. The immediate after.json is NOT the expected F5 output after normal AI
settles. Inspect frames, timing trace, body/bow/feet, contact, disappearance and
corpse, overlap, terrain and visibility independently; deterministic equality
does not prove pixels. Do not force facing, pause AI, patch HP or alter the camera.

## Scope

During witness preparation and archival, production files were not changed and
no commit or push was performed by the witness worker. No real user
save was touched. Earlier exploration directories outside this package are not
acceptance artifacts. The prior prefix contains another lawful east-facing
archer death at step122 (unit6 at16,6), but it was not selected for visibility.
