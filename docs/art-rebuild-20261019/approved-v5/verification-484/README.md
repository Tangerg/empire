# 484-topic checkpoint

Nine reviewed topics were formally registered after the complete475-snapshot test finished: FX13 scorched earth and eight C02 skills (snipe, suppress, boarding, quick repair, deploy turret, brace platform, heat zone, emergency water).

FX13 preserves one cracked-earth registration (2-source-pixel contact range) across ignition, peak and residue. It remains nonloop12fps with consumer-owned lifetime; no420ms hold or campaign consumer is invented. Independent phase sources, native32/64 alpha, exact single-play APNG decode/timing and static terminal progression are preserved. Live playback is unobserved.

C02 icons retain original board meaning, exact reference provenance and transparent exterior. Native48 white/black/olive checks pass; isolated low-alpha internal arc/tool details and reduction of fine original details are disclosed. They bind to the C02 catalog, not an invented playable campaign.

Verification:59 focused TypeScript tests,47 Python exporter/ledger tests, and typecheck passed. The earlier475 complete-suite result remains1734/1737 with three timeout failures; unchanged isolated rerun10/10 passed. Both logs remain in verification-475. This checkpoint does not claim a passing complete suite or browser/gameplay acceptance.

## Application build check

The normal game, editor and engine-demo builds passed. The experience-lab single-file build was killed with exit137 after902 modules transformed. An isolated unchanged build reproduced this at max RSS3812064KiB; a process-local2048MiB V8 heap cap also failed at3350724KiB. No compiler diagnostic or kernel OOM event was available, so resource pressure is a supported hypothesis, not a proven kernel cause. The normal game external-asset directory is about170MiB; experience-lab deliberately inlines those resources. Its single-file contract has not been removed. A minify-disabled diagnostic is pending. An attempted /usr/bin/time wrapper failed because that executable is absent; no build was started by that wrapper, and later measurements use Python resource.

The minify-disabled diagnostic also exited137 (max RSS2953072KiB). Thus minification alone is not established as the cause. Normal builds of game/editor/engine-demo remain valid. A separate engineering change will test late resource embedding while preserving the offline single-HTML contract. No successful experience-lab build is claimed for this snapshot.
