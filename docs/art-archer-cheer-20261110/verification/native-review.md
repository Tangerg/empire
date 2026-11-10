# Independent native cheer review

## Verdict and evidence scope

No blocking visual defect found in accepted capture
`native-final/capture/motion-20261110-221509-002`: 100 contiguous actual
960×640 PNG frames at stride1. Reviewed full native frames040,041,064,080,095
and099, an all100-frame archer-region contact sheet at unchanged pixel scale,
and enlarged crops of040/041/047/054/064/080/090/095/098/099. Crops are inspection
views of captured pixels, not substitute game renders or generated artwork.
Failed capture001 is not the acceptance evidence. Its prior budget failure
remains an honestly retained limitation of that attempt.

## Visual findings

Elin3 and generic archer9 retain the accepted blue hood, equipment proportions,
quiver and wooden bow. Their free fists visibly rise, hold and lower. The bow
stays with the same central gripping hand, remains empty and does not acquire
a loose arrow, detached tip or extra hand. The fist/quiver overlap remains
readable rather than resembling an arrow-drawing attack. Native-size strokes
are necessarily small, so full anatomy/string detail is additionally supported
by the independent original-source review, not inferred from tiny pixels alone.

Both archers keep planted support and stable size through entry, all six poses
and return to breathing. Their nearby silhouettes remain separable. The dying
archer's earlier foreground overlap clears during the terminal fade and does
not leave a ghost across celebration. The command-completion toast lies below
the archers and does not hide their feet, fists or bows. The final result text
change likewise does not obscure the accepted action. No visible saturated
fringe, clipping or equipment pop appears.

## Independent frame/metadata cross-check

Read all100 frame JSON records and cross-checked the pose transitions against
actual pixels. Both archers enter cheer at041, then advance at047,054,064,080
and090, returning to breathing at099. Torren enters salute041, advances through
its own poses and returns to breathing095. Target18 is present through040 and
absent041, the same captured frame at which celebration begins. All four actors
retain one constant recorded root position during their observed lifetimes.
Frame099 reports presenting=false.

These frame observations support the intended earlier900ms Torren endpoint and
later960ms archer endpoint. Frame sampling is not an independent submillisecond
clock measurement; exact half-open boundaries and620+160+960 ordering are
established by the controlled-clock tests reviewed separately. This native
sequence starts during the dying actor's fall, so it is not a complete attack
record from initial command selection.

## Limits

Both captured winners are unmirrored. Native visual acceptance here applies to
that facing; opposite-facing evidence remains the separately reviewed source
contacts and controlled-clock sprite checks, not a second native capture.
Saved-state equivalence is being checked separately by the legal worker and
is not asserted solely from these images. The original full aggregate gate is
still pending at the time of this review. No full-gate, export, full-campaign or
real-time performance pass is inferred from this capture.

No production edits, build, game test run or commit performed for this review.

## Packaged-evidence verification

Independently reran native/verify-native-evidence.py: PASS for the decoded100
full RGB frames, eight mapped stills, every diagnostic-sheet crop tile,
contiguous observations and expected actor lifecycles. All three isolated
mutation controls passed by rejecting a bad RGB hash, repeated process frame
and shifted cheer frame. Additionally compared every packaged proof PNG/RGB
hash and JSONL record directly with the original accepted002 PNG/JSON files:
all100 match; all eight mapped still files are byte-exact originals. The
separate victory-result still was visually inspected and shows the victory
screen; it is not falsely included among the100 motion observations.

Reran legal/compare-native.py on legal/native/native-output.json and
legal/ui-settled.json: complete documents match after removing only
campaign.savedAt and battle.savedAt. Current HEAD and tested6fa6f8aa have the
same Godot tree5023aa4d61b633c0d59c2e15e0e4057505fcf557 and tools tree
afff6c24fcf143fdff3a5a4facf2f373d1ad3290. Independently hashed all three actual
frozen DLLs and matched the proof's recorded values. The11 relevant native and
negative-control log files are explicitly staged; no cache is included in
that staged log list. No package issue found. Full aggregate gate remains
pending at this addendum, and no aggregate success is inferred from these
separate passing evidence checks.
