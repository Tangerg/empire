# Bounded surface interval reconstruction

## Ownership and scope

BoardProjection remains the sole owner of depth cuts. Each interval is clipped
independently from the original triangles, so exact unchanged interval endpoints
produce the same ordered geometry for a fixed projection and registration.
Bounded PaintedSurface instances retain only the last successful version's
managed points, piece indices and UVs, including empty results. Changed intervals
use the original clipping path. Preparation stages the next cache; an exception
must leave the previous cache and mounted polygons intact. Native polygons are
still synchronously recreated/freed. Unbounded floors and cliffs do not cache.
No map artwork, game clock, gameplay, saves or animation binding is changed.

## Recovery status

The earlier unpublished commits and local recovery archive disappeared after
another environment reset. This is a fresh reconstruction from remote baseline
1d5cbd0a4f0adc84ce71d9fbe05ae8775197bf2a, not recovery of the lost Git objects.
Historical timing suggested repeated clipping during depth changes was costly,
but those lost measurements are not fresh verification or a new FPS claim.

This checkpoint preserves the implementation while regression guards and the
full native gate are pending. It is not release, visual or performance acceptance.
Subsequent checkpoints will record actual test results and retained evidence.

## Focused reconstruction checkpoint

Fresh build completed with zero warnings and errors. Strict editor import,
PaintedSurfaceChecks, the expanded full-clip/cache oracle (26,452 assertions),
and AssembledGroundChecks (18,032 assertions) returned exit 0. The guards cover
unchanged/empty reuse, changed endpoints, split/merge/withdrawal, repeated cuts,
synchronous node freeing and direct recovery after failed preparation.
Independent code inspection found no introduced correctness defect. Initial
whitespace and a test-local variable shadowing error were corrected before the
successful build/run. Deliberate fault controls and the full native gate remain
pending; these focused results are not a full-suite or performance claim.
