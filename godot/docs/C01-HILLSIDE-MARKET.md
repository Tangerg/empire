# C01: Hillside market

## Presentation and rules

The first battle projects the existing tactical board into a fixed-angle
orthographic 3D scene. The combat rules, input controller and campaign still
own legal actions and progress. The new scene is selected by the C01 content
manifest; later levels retain their current presentation. Buildings in the
miniature scene are art on canonical blocked terrain, not a second source of
movement or line-of-sight rules. See [art direction](c01-miniature-art-direction.md).

The choice at the end of the briefing sets the opening deployment. Advancing
from the workshop preserves formation and climbs the west slope. Sending an
advance guard to the north means Torren starts injured closer to the ridge
scouts. Both converge on the market. The east service lane is a longer
approach that remains connected across terraces. Controlling the market and
defeating the three vanguard units completes the primary objective while
Laiya survives; pursuing the rest still is not required.

## Save boundary

The replaced terrain yields a different battle-source hash. The application
rejects a mid-battle or suspended old-map C01 save without overwriting it or
loading mismatched state. To continue from an earlier build, finish C01 there
before updating, or use a checkpoint before C01 begins. Current C01 saves and
campaigns that have already entered C02 are separate from that breaking
boundary. No old C01 renderer or save adapter is selected in the new battle.

## Verification

Run `bash tools/godot/verify-native.sh` from the repository root with the
project toolchain. The [route witnesses](../tests/native/c01-steady-victory.md)
exercise both opening choices through the real campaign, a midbattle
save/reload, the normal AI, objective completion and entry to C02. These are
behavior checks. Inspect visual composition and tactical clicking independently
in a window at normal HUD scale.
