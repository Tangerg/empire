# Joined market street — C01 review

Original authored geometry from the included Blender generators. Reproduce in a
separate staging directory with Blender 4.3, then review the bounds and default
camera before replacing production binaries. The two market GLBs replace the
upper/lower N3 resources, the linked cottages replace the 3×6 N1 resource, and
the rain shelter replaces the 3×3 W1 resource, and the weighhouse replaces the 4×2 E1 resource. Scene anchors are unchanged. The
models are visual-only; the canonical C01 terrain still owns all movement.

The market and weighhouse use an opaque vertex-color matte finish. The cottage
generator includes its embedded palette. Roof remains a named independent mesh
for the existing visibility behavior. The visual vocabulary is shared, not the
floor plan: stone arches and a staggered exchange building, joined cottages to
the northwest, and a shorter timber loading canopy to the east. The slate court
continues to the actual upper and lower west slope gaps via the existing mesh
shader. Retaining masonry shades only the actual interior cliff mesh; the outside cutaway remains earth. No false stair or bridge is created. The orchard shows low staggered trees while keeping cell centres and edges open.

Only the five associated resources above are replaced. Previous source notes
below the current-status section of the parent README are historical.
