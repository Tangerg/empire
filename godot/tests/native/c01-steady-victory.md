# C01 hillside market campaign witnesses

These records start the authored first command choice in a fresh campaign, then
enter the battle through the application. They replace the paths over the
superseded flat C01 map; they do not change AI, combat, or victory rules.

| Choice | Campaign opening | Orders | Live AI orders | Conclusion |
|---|---|---:|---:|---|
| `steady-advance` | `bc840f7f` | 92 | 32 | Primary victory on turn 6, `edd93bc8` |
| `rush-north-hill` | `beade493` | 93 | 31 | Primary victory on turn 6, `2867015a` |

The steady route sends the main group up the western slope and opens space
for the ridge scouts. The rush route stabilizes the injured advance guard in
the north, uses the slope and the east side to converge, and does not require
eliminating every enemy. Both use real capture and a live computer planner.
Neither sets a result directly nor substitutes a stored opponent action for
the AI. The application harness checks checkpoint reload and the actual
carried C02 entry separately from visual input and presentation.

CampaignNavigation and CampaignPlayedFlow consume the steady record in their
existing native integration paths. Native headless playback, desktop visual
quality, mission difficulty and a complete campaign are distinct gates.
