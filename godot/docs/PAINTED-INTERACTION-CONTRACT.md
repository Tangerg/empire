# Painted objects, depth and pointer subjects

## One fact, one owner

Canonical level terrain and ordinary blocking structures own traversal, cover and
opacity. A painted polygon never supplies collision. Closed foundations require
complete canonical blocked cells; an open canopy's floor may be traversable.
Neither is inferred from a roof, a cell center or a drawing contact.

The painted document owns source registration and presentation geometry. Each
`occluders` entry is one logical object: `id`, optional `selectionCell`, optional
`ownsMapStructureBody`, and a required nonempty `parts` array. The selection cell
is an explicitly authored canonical site, not a center-derived entrance.
`ownsMapStructureBody` suppresses the site's duplicate sprite, not its rules.

Every part declares `id`, `contactCell`, `groundFrontPixels`, `polygonPixels` and
`pointer`. A part is a real opaque material/depth section, such as a roof, curtain
or post. A mother image without alpha redraws every pixel inside its polygon;
therefore an open mouth or between-post floor must not be included in a coarse
foreground shell. Several simple polygons represent material around an opening.

`PaintedGroundView.Sites` exposes unique logical site bindings for banners and
body suppression. `DepthEntries` contains material pieces, not independent
buildings. No first-piece owner or duplicate-then-deduplicate behavior exists.
Every piece uses the existing `StandingDepth` and `OrderStanding` calculation;
rendering and pointer visibility have one depth authority.

## Explicit pointer geometry

Every material part declares one pointer behavior:

- `{"kind":"target","polygonPixels":[...]}` targets its enclosing object's
  selection cell through an independently authored visible front/body contour.
- `{"kind":"block","polygonPixels":[...]}` blocks pointer access within the
  explicitly authored contour.
- `{"kind":"pass-through"}` has no pointer polygon or target.

Drawing coverage and pointer coverage are different facts. Target/block contours
must lie within the part's drawable coverage. Outside that contour a visual part
can still occlude units, but does not redirect the pointer to its object. Such a
point can return projected Ground; it cannot expose a hidden Unit/Object below.
Opaque frame pixels, cliff faces and explicit blockers retain their meaning.

Missing/unknown pointer declarations, unknown painted fields, invalid polygons,
foreign site bindings and duplicate body owners fail admission. No old single
polygon fallback or scene-specific picking branch is retained. All shipped
painted documents and in-code fixtures use the same schema.

## Preserve the subject through input

`BoardView.Pick` returns an immutable `TacticalHit`:

- `Ground(cell)` is projected ground, not an implicit occupant lookup.
- `Unit(cell,id)` is the visible unit body actually hit.
- `Object(cell)` is an explicitly hit site or structure surface.

Press, hover and `TacticalSelection` preserve this type. Ground and Object never
become an occupant through `UnitAt`, including inspection, deployment and
automatic attack selection. A visible unit body can be selected in front of a
building; an independently visible building facade remains a site hit even when
its cell has an occupant. No fixed unit/site preference overrides actual depth.

Ability definitions explicitly own target-subject requirements; command options
and tactical targets only project them. A cell-targeted effect may use a clicked
cell, but a unit-targeted command requires the matching visible Unit hit. Mixed
unit/structure attack targets preserve canonical primary-target identity. Area
of effect is not the pointer subject. A cell-targeted tactic clicked on an Object
uses that object's explicit logical cell, preserving existing site-target
behavior; it does not mean the projected ground beneath its roof. Ground hits
alone describe that projected ground. The controller never reprojects a pointer
or substitutes a hidden ground cell. Coordinate legality and actual execution
remain canonical rules, not a second presentation combat implementation.

Keyboard focus/cycling and an explicit HUD command with a single legal target
retain their existing canonical behavior. They are not pixel-hit claims and do
not substitute for mouse acceptance. Native test helpers use `SelectHit` with an
explicit subject rather than ambiguous `SelectCell` inference.

A pointer hit belongs to the rendered revision and camera transform. A changed
revision clears the old hit; after the new board is drawn, the screen may pick
again at the last explicit map-local pointer position. Pan and zoom use the same
`BoardView.Pick`, not a coordinate-to-occupant substitute. Pointer leave, unbind
and focus loss clear that pointer source. Explicit keyboard direction focus owns
its logical subject; its `CursorFocused` event prevents a stationary mouse from
overwriting that focus during camera reveal. Cycling and roster selection retain
their original selection/reveal behavior without replacing the cursor: an active
pointer is re-picked after camera motion, while an existing logical keyboard
cursor is preserved. These are input-lifecycle corrections, not changes to legal
action targets or combat execution.

## Concrete regression and scope

In chapter03, SouthHouse's unchanged roof polygon covers legal road centers
(52,35) through (55,35), source pixels (853,656) through (901,656). Its independent
front contour excludes those roof points while retaining a clickable facade.
Ground selection must not redirect these road clicks to entrance (55,40), nor
reveal a unit concealed under the roof.

The natural south-route report concerns the different SoutheastHouse, with
entrance (88,39) and covered road (88,34), source center (1429,638). The same
native check also asserts Ground across that road's pointer footprint and Object
on its independent facade. The SouthHouse movement fixture is a same-class
regression, not a substitute for real-play acceptance at this reported site.

The shared-contract batch migrates presentation data and callers, not battle
terrain, units, stats, objectives or scene scale. Chapter04's enlarged domain,
full foundations, ten facilities and flat-height authoring are a separate
balance/art batch. Runtime checks, deliberate regression mutations and natural
GUI play are distinct acceptance evidence; passing one does not imply the others.
