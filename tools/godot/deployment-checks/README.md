# Deployment parity checks

Run from the repository root:

```sh
./node_modules/.bin/vite-node tools/godot/deployment-checks/export-vectors.ts
./node_modules/.bin/tsc --noEmit --skipLibCheck --moduleResolution bundler --module esnext --target es2022 tools/godot/deployment-checks/export-vectors.ts
source tools/godot/env.sh
dotnet run --project tools/godot/deployment-checks/Empire.Deployment.Checks.csproj -- .
```

The exporter calls the original TypeScript factory, deployment queries and transactional dispatcher. Eighteen fixture families cover assignment selectors, declared order and inferred order, duplicate order normalization, empty assignments, overlapping zones, allies/enemies, swaps in both directions, impassable terrain, structures/overlays, square4/square8/hex, initiative, no-op placement, capture reset, missing zones/units, phase gates, and final confirmation without post-action triggers.

The native runner compares 1,204 action outcomes, including complete state hashes, exact events and refusal text. Ordinary unmodified fixture levels exercise the production engine. Only deliberately edited/malformed states use an isolated transactional envelope around the actual rule owners, without weakening the validated load API. Queries are checked for mutation and failures for rollback. Seven invalid level declarations test canonical factory validation.

Additional production-engine checks save and resume each valid deployment, save again at the side handoff, and confirm into normal play. A swap case preserves initiative, done flags, directives and capture fields through save/load. Malformed persisted deployment aggregates are refused without mutating their input. Oracle data is test-only and never loaded by production rules.

Current result: **2,519 checks passed**, zero failures. See `godot/docs/COMPATIBILITY.md` for deliberate malformed-document hardening.
