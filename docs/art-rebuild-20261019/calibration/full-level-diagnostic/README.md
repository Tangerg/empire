# Whole-level composition reference, c01-02

This read-only view includes all22×14 cells,20 authored actors,9 buildings, and every extracted ground/under-unit/over-unit scenery layer. It uses the current branch's production asset references, including previously exported art, without any calibration sprite, ground, connector or camera substitutions. It is a static renderer-piece diagnostic, not a live gameplay screenshot. Normal building state, neutral unit tint and idle-frame extraction remain explicit limitations; CSS shadows, runtime ownership effects, facing/motion and interactive camera behavior are not reproduced here.

- `whole-level-current-art-2x.png`: full1408×896 image, no overscan margins, neutral backing
- `whole-level-positions-annotated.png`: same image with initial faction deployment bounds/points
- `whole-level-layout.json`: exact20 actor records and framing alternatives
- `registration-and-provenance.json`: source hashes and renderer caveats

Owner1 has12 actors at cells x1–5,y7–12; world cell bounds[32,224,192,416]. Owner2 has8 at x15–20,y2–12; bounds[480,64,672,416]. These are authored initial deployments, not actual combat-frontline observations. The river occupies columns10/11; crossings are rows3,6,9. The earlier selected study cropped to x0–13 and thus excluded all enemy actors. Its empty far bank and sparse scenery cannot support conclusions about full-level composition.

Recommended framing references:
1. Whole level[0,0,704,448] for complete deployment/context
2. Deployment overview[16,32,672,416] retains both complete deployment bounds with less margin, but can trim decorative edge art
3. Crossing detail[256,48,320,352] is suitable only for bridge/terrain inspection; it intentionally excludes most starting actors and must not be presented as the full battle composition

No production code, level data, sources or gameplay state were changed for this diagnostic.
