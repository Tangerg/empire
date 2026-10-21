# First GrayFlag battlefield layout

## Authoring owner and intentional change

`data/grayflag-campaign-levels.json`, level `c01-01`, owns the playable outline,
terrain, elevation, ownership sites and initial unit coordinates. The 2026-10-21
revision deliberately changes the level design, not battle rules. It is not an
old-level hash-parity change. Native scenery projects this layout; decoration
cannot add or remove passability.

The storage rectangle is 36 × 24 instead of 27 × 17. Its irregular boundary uses
the existing `_` glyph (`c01.outside`), whose canonical terrain definition forbids
all movement profiles. Of 864 storage cells, 704 are playable and 160 are outside.
The playable cells form one connected region. Forest occupies 132 cells (18.75%
of playable area); 394 cells are open plain, 75 road, 57 low hill, 42 higher hill,
and four are capturable sites. The nineteen initial units and their types,
identities, teams and other properties are unchanged.

## Spatial composition

The west outpost is at (5,13), the main village at (17,11), the southern village
at (23,14), and the east outpost at (31,10). The main lane bends through these
settlements. A southern branch leaves the west camp, passes through open ground
around (12,20) and (18,20), then reconnects at the southern village. This is real
playable maneuver space rather than visual padding around an unchanged board.

The northern highland occupies roughly x10–18/y3–9, centered at (14,6). The
southern highland occupies roughly x21–29/y15–21, centered at (25,18). The exact
edge is defined by the terrain/elevation cells, not these descriptive bounds.
Groves occur in separated perimeter groups, leaving visible clearings and routes.

The closest opposing seeds are ten Manhattan cells apart (ridge guard to north
shield); the old closest pair was eight. Most additional area lies beside and
behind the fronts, not between armies. This spacing is an initial authored
candidate, not a claim that combat pacing or visual balance has been accepted.

## Camera inspection route

Inspect at the same normal play zoom, not a whole-map shrink-to-fit:

1. West camp around (7,14), including the outpost doorway and separated roster.
2. North ridge around (14,7), then central village around (18,12).
3. East camp around (30,11), with its approach and ranged support visible.
4. Southern plateau around (25,18), then the open bypass around (15,20).
5. Return to west camp; verify the irregular boundary and all sites can be reached
   by the camera without changing the click/selection mapping.

## Verification boundary

A bounded structural check verified row widths, elevation length and glyph
agreement, unique in-bounds unit coordinates, valid ownership-site glyphs, a
connected playable region, and unchanged non-layout fields. The other fifteen
levels are unchanged. No scenario/objective coordinate rewrites were needed:
this level's scenario emits a signal after a death and its objectives use stable
unit identities, not cells.

Actual engine admission, movement, save/load rejection for the old dimensions,
camera navigation and rendered spacing remain integration checks. The old battle
save is not converted to the new map, deleted or silently overwritten.

## Actual native checkpoint

The updated canonical level compiled and passed the existing presentation-resource
scene, including exact outside-cell surface coverage and all 16 level resource
projections. The presentation check now uses the current native opening level,
not the historical TypeScript map, and derives mask dimensions from its state.

In the real 1180 × 812 application, the camera was manually panned from the west
camp through the village to the east camp. Torren moved from (9,12) to (10,13),
then F5 saved and F9 restored the new 36 × 24 battle. An earlier 27 × 17 manual
save was rejected with the existing player-safe message, leaving the current
battle unchanged and preserving the old file bytes until an explicit F5 action.
These checks used an isolated test save directory, not user saves.

This is a domain-layout checkpoint, not final visual acceptance or a complete
combat-balance playthrough. Existing old-layout frozen action traces are historical
observations, not expected whole-state/hash outputs for this intentional redesign.
Other native interaction fixtures may require their deliberate coordinates to be
reauthored for this layout; the full historical verification matrix was not run.
Continuous highlands, natural road edges and coherent single-building art remain
open. The unchanged camera scale is not used to disguise these unfinished items.
