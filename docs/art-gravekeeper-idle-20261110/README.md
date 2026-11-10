# Shared C01 gravekeeper breathing idle

This slice changes only the generic `c01.gravekeeper` idle binding. The type
appears in 14 of 16 starting campaign rosters; that is reuse evidence, not a
measurement of player time. Named keepers such as field-cleric and bridge-cleric
inherit this type. Mirelle retains her separate appearance and explicit static
idle. Other types, the canonical gait sheet, weapon policies and all finite
action bindings remain unchanged.

Four independent paintings show rest, inhale, high breath and exhale. Their
700/500/500/700 ms intervals form a direct 2400 ms cycle, 0/1/2/3. The final pose
already exhales, so reversing it through a ping-pong sequence would introduce an
extra rebound. This is four original drawings, not transformed copies of one
sprite. The shoulders and chest move subtly while the boots stay planted.
The right hand holds the pointed shovel and the left keeps the oil lantern;
the equipment identity is taken from the actual official artwork, not the
internal weapon identifier `c01.lantern-staff`.

## Source and registration

The built-in image tool used the official gravekeeper gait and accepted casting
art. `source-row.png` and `prompt-v1.txt` preserve the untouched generated image
and exact prompt. Four connected original silhouettes are retained. Extraction
preserves all 488,890 retained original RGBA pixels, with twelve transparent
padding pixels around each crop. Only 27,804 detached near-transparent pixels
are removed, with alpha at most 9/255. There is no repaint, per-pose rescaling,
rotation or transformed-sprite anatomy.

The 1709×502 atlas uses one scale, 0.0792, to match the larger generated source
to the established body height. Recorded boot-contact anchors, not independent
frame fitting, register the four figures. The coat/stance is modestly narrower
than the old painting at large source size; independent native-size comparison
finds no identity change or visible foot/size jump at cast-rest and loop-wrap
handoffs. Both-facing contact sheets and the nonlooping GIF are offline
registration diagnostics, not gameplay or real-time performance evidence.

`verify-extraction.py` reproduces the exact retained pixels and rejects a
one-channel mutation. `verify-registration.py` compares actual runtime bytes,
geometry, anchors, scale and clip intervals with that source contract.
`verify-data-scope.py` proves the two production JSON documents equal baseline
`f358d5bc` after removing only the new appearance, clip and keeper idle binding.
The extraction script uses a repository-relative reference path in this archive.

## Ownership and regression contracts

Production integration is data-only. The existing `BoardView.Idle` owns phase,
membership, texture changes, shadow/depth refresh, hidden freeze, reduced motion
and finite-action suspension/resumption. `ActorClip` remains a finite sampler.
There is no new clock, input gate, receipt, rule or persistence state.

The keeper's cast still releases at 300 ms and restores idle at 600 ms. Damage
and healing feedback still end at 720/800 ms. Updated cast tests distinguish the
canonical base sheet from the explicitly declared idle and sample its elapsed
phase after cancel/rebind. No valid timing assertion is weakened. A lawful C04
move/cast test now checks the declared recovery appearance while retaining its
exact movement, damage, state hashes and receipt sequence.

Focused coverage adds both-facing exact frame boundaries, wrap/large advances,
planted contact/root scale, cached held textures, no state/events/effects during
idle, visibility/reduced-motion behavior, same-type bystander phase independence,
cast/heal recovery, five-cell movement across the first breathing boundary,
static hit/death ownership and stale registration retirement after tree exit.
Other explicit static idle bindings remain guarded.

## Verification status

Source pixels, offline both-facing visual review, registration and semantic data
scope checks pass. The native build has zero warnings/errors; editor import,
formatter verification and 214 indexed script UIDs pass. The complete focused
AnimationChecks suite passes 6,876 assertions with strict exit 0. An initial
formatter check found two initializer whitespace issues, now corrected; its
output is retained separately from the successful final check.

Four isolated data mutations are rejected at their intended first assertion;
byte-exact restoration passes all 6,876 AnimationChecks assertions. The final
frozen build also replays 491 legal orders across three historical routes, with
independent state/event comparison, official save reloads and unchanged final
actions. These routes share prefixes and are not 491 unique scenarios.

Actual native UI recordings contain 103 right-facing and 103 left-facing idle
observations, plus a 48-frame stationary heal. Each idle recording spans 5.1
simulation seconds from first to last observation, including more than two
2400 ms cycles. The heal returns to the new idle at observed frame 36 (600 ms)
and finishes at frame 48 (800 ms). Complete F5 saves match their lawful expected
states except both changed writer timestamps. The final build and source trees
are identical across these recordings and the completed aggregate. Lossless
packaging and independent visual/evidence review pass all 254 native frames,
traces, selected screenshots and diagnostic crops. The portable native verifier
also passes 23 isolated controls.

The unchanged full native script passes all 45 stages with strict exit 0 and its
terminal success marker. The successful repeat ran from clean tested source
`869078976dd21f5d82932dc4151bdd03fa973c1a`, from 23:13:09 to 23:47:52 UTC on
2026-11-10. All three runtime DLLs still equal the exact native-recording build.
Independent comparison preserves all 16 AI traces and their 3,425 legal orders,
six played-flow markers, and the same 41-warning multiset as the accepted cheer
aggregate. The eight comparator controls pass.

The actual played-flow run reports 22,394 assertions, two carried victories
across 415 legal orders and a 21-order third-battle opening. Its assertion total
includes per-process-frame deadline checks and is not a scenario count; it does
not replace exact action/event/state comparisons. The earlier full attempt lost
its process during environment replacement and has no completion record. Its
partial log is retained separately from the successful complete repeat.

See [native evidence](verification/native/README.md),
[legal replay](verification/legal/README.md),
[negative controls](verification/negative-controls/README.md), and
[full aggregate review](verification/full-native/COMPARISON.md).
Full-character completion, all-campaign playthrough, standalone export and
real-time performance remain outside this slice.
