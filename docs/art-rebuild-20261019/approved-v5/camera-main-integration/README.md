# Independent camera on current compact campaign

Reviewed source commit f567b92958d0cb70c915105667a23657d035ba4e was cherry-picked as180dab512763340b9f1d2cb0c15bd594dd6824e6 after source629 checkpoint9ef23f72. This packet adds generic scroll-position camera ownership, bounded drag, click-versus-drag discrimination, preserved readable world scale, zoom-center preservation, initial human-unit focus, keyboard reveal and resize clamping. Generic outside-terrain support is exercised by a synthetic irregular fixture. No expanded C02 terrain, new map silhouette, AI or route scoring changes are included.

Exact compact-main verification passes145 files/2051 TypeScript tests, typecheck and experience-lab production build. Tests cover narrow/oversized/small board clamping, pointer capture/cancel, drag not selecting a unit, ordinary clicks, modifier-wheel zoom, keyboard cursor reveal, later resize and old campaign presentation consumers. Shared camera/input code was independently inspected by the integration owner. Prior source629 also passes361 Python art tests.

No browser session is claimed: shell and cloud CDP localhost access remain connection-refused in this environment; native desktop input was unavailable. DOM/renderer tests and build are not live input acceptance. The expanded river map and generic navigation changes remain separate and held for gameplay review.
