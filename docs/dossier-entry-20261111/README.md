# Open the information named by a dossier entry

## Actual observation

At C05's 960×640 battle opening, select Mirelle and click “兵种与能力”. Before this change the popup showed only collapsed headings; the player had to click the same label again to see any unit information. This was reproduced with native mouse input, not inferred only from code.

## Repair

The entry label and automatic disclosure now use the same current `DossierSection` projection. A retained opener resolves its live section after stamp, subject and lifecycle checks, rather than capturing its initial target. Only the exact disclosure control is toggled, never a gameplay button. Ordinary site/structure sections need no artificial disclosure. The forecast-details entry retains its explicit target.

Opening a collapsed named section reveals its contents. Reopening an already expanded ordinary dossier preserves the player's reading offset; explicit forecast navigation still starts at the top. Native close focus, keyboard reading and user collapse/re-expand remain available. No domain rule, save, replay, art or localization data changed.

## Evidence and limits

- Native build: zero warnings/errors; native format check and diff whitespace check passed.
- Strict native process gates passed: DossierFocus 165 assertions, TacticalHud 1,246, CameraIntegration 370, and BattleScreen real-session integration. Existing duplicate mouse-enter notification warnings remain, without errors or leak diagnostics.
- Negative control: original production `TacticalHud.cs` with the new tests exits 1 at the required named-section disclosure. Production restored afterward.
- Dossier tests cover live retained opener across roster/unit/site/structure, multiple ordinary action buttons with zero dispatched intents, real C01 details, collapse/re-expand, reading-offset preservation and safe close focus.
- C05 actual 960×640 mouse input: one click now reveals Mirelle's information. Close button, scroll thumb and reading hint fit the popup. Controlled chapter-entry fixture, no battle action issued. The final reading-offset-preservation adjustment is covered by native tests; the initial collapsed-opening behavior is unchanged from the GUI-reviewed build.
- This narrow UI change uses targeted regression checks. The immediately preceding full 16-chapter gate on 99326db0 is documented separately; it is not represented as a full gate on this change.
- Automatic scrolling to an arbitrary later section behind other expanded long sections is not introduced or claimed here. The fix removes the extra disclosure click, not all possible dossier navigation improvements.

Only text logs are included; no previously withheld evidence or credentials.
