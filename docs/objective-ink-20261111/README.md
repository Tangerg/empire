# Objective paper interaction contrast

Base: `a644fe5d46bb3fb4ef722aeaba22dd4bf0cc381d`.

Actual 960×640 C05 review found the expanded objective heading switching to white
text on light parchment when hovered. The normal state was dark and readable;
Godot's separate `font_hover_pressed_color` for an expanded toggle was not styled.
The same incomplete paper-button styling existed on objective location buttons.

The correction uses one paper-button palette for disclosure and location controls:
dark text in all interaction states, a muted dark disabled color, and transparent
or lightly tinted backgrounds that keep the parchment visible. Dark compact-menu
backgrounds must not leak into collapsed hover or disabled states. Focus retains
its visible border. Fonts, sizes, objective definitions and actions are unchanged.

C05 briefing/staging/opening were entered from a detached fixture produced by
ReedVillageChecks (1,504 assertions passed). No previous campaign victories or C05
completion are claimed.

Verification:
- Old-production negative control failed exactly on the 960-wide objective
  heading hover font color; the restored implementation passes.
- TacticalHudChecks: 1,213 assertions across both paper controls, six font and
  background states, and 960/1180 widths.
- CameraIntegrationChecks: 370 assertions; DossierFocusChecks: 143 assertions.
- Format/build/diff checks passed, build zero warnings/errors.
- Real 960×640 C05: expanded hover remains dark/readable; collapsed hover retains
  the light paper; Enter reopens the focused disclosure without changing progress.

This is presentation styling only. No full 16-level gate was rerun in this slice,
and no complete-campaign, export or performance acceptance is claimed. Existing
resource readiness diagnostics and native mouse warnings remain. No binary GUI
evidence or previously denied artifact is included.
