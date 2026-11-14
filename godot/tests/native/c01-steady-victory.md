# C01 steady campaign victory witness

`c01-steady-victory.json` records one real new campaign: authored opening,
`steady-advance`, then `BeginBattle`. Its 430 starting funds are campaign context,
not the independent level's 300 funds. The 95 orders are the previously played
C01 steady route, readmitted through the current Application runtime. All 34
opponent orders match the live AI and are still executed by the normal AI owner.

The final human Torren attack kills the last required vanguard and naturally
completes the already-captured village objective: `25320161` to `f03ecd7d`.
The initial state is `79c9ead8`. No terminal snapshot, units, stats, map or
production AI are edited to produce the victory.

CampaignNavigation and CampaignPlayedFlow share this single route record.
Their input, animation ordering, once-only completion, F9, storage and carried
roster assertions remain independent. The old AI-as-player driver could fail
to capture the village within its limit after the C01 mission changed; the
original order bounds remain, rather than enlarging them to hide the failure.
C02 keeps its existing live test-player policy and all carried-state assertions.

Focused native checks passed on 2026-11-14: Navigation 9,330 assertions;
PlayedFlow 17,733 assertions, including C01 and carried C02 victories, then
22 legal C03 opening orders. This is headless behavior evidence, not visual,
exported-package or full-campaign acceptance. The original aggregate gate must
still pass separately.
