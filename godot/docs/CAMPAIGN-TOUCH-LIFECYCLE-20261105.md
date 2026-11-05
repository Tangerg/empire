# Campaign touch navigation lifetime

## Reproduction

On `987841be3`, an unchanged fresh native campaign followed New Campaign, seven
original story beats, `rush-north-hill`, staging and Start Battle through actual
`Input.ParseInputEvent` input. Both a quick touch and a touch whose press/release
were separated by native frames produced ten `can_process` errors:
`Condition "!is_inside_tree()" is true`. The 32 behavior assertions succeeded, but
the strict process/diagnostic gate correctly failed both runs. The mouse control
passed with no such errors. Debug assembly hashes were unchanged across the
three isolated runs.

The campaign widgets synchronously invoked navigation or rebuilt their current
story beat from `Pressed`. `RemoveChild` detached the pressed control before
Godot finished visiting it during touch-emulated mouse dispatch. `QueueFree`
preserves the object until later, but does not keep that detached object inside
the scene tree.

## Ownership and repair

`CampaignButtonActivation` owns only native campaign-button publication timing:
queue once per input target, finish the current dispatch, then recheck target and
owner lifetime, binding validity, disabled/deletion state, and publish. A target
that leaves and re-enters the tree cannot execute an older queued press; fresh
presses after remount remain governed by the original binding contract.

Menu, story/choice, staging, campaign exit and settlement buttons compose that
same boundary. Their original generation and submitted/handled guards still own
one-time business submission. Campaign/session rules, story cursor ownership,
confirmation policy, saved data and artwork are unchanged. No synchronous testing
backdoor, input-mode exception, error suppression or disabled touch emulation is
introduced. The existing tactical HUD retains its separate revision/row admission
contract.

Native widget tests now await publication before observing the result. Repeated
and competing presses are still enqueued within the same dispatch; stale tests
queue while controls are alive, then rebind or unmount before draining the queue.
They do not invoke disposed controls or skip invalid instances. Immediate camera
activation is observed from the same deferred queue before layout, with a separate
settled-layout assertion.

This follows Godot's documented
[deferred call timing](https://docs.godotengine.org/en/stable/classes/class_callable.html#class-callable-method-call-deferred),
[native input propagation](https://docs.godotengine.org/en/stable/tutorials/inputs/inputevent.html)
and [explicit scene dependencies](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html).

## Verification at the implementation checkpoint

- Debug application build: zero warnings and errors; native editor import passed.
- Unchanged external quick-touch, split-touch and mouse routes all pass 32 checks
  and the strict diagnostic gate. Candidate DLL hashes remain equal before/after.
- A separate actual touch route through staged progress, menu restart request,
  cancel, re-request and confirmation passes 40 checks with strict exit 0. Cancel
  retains Continue and progress; confirmation restarts once.
- Added a permanent native route regression covering both touch timings, mouse,
  exact canonical choice/advance states, story paging, staging exit/Continue and
  entering the first battle without dispatching an order.
- Focused widget/navigation tests and the complete aggregate are pending at this
  checkpoint. A first supplemental menu test invocation omitted its required
  frozen-oracle argument and was rejected at setup; it is not a product failure
  or a passing check.

These are native headless interaction and lifecycle checks, not graphical visual
acceptance. Previous isolated HUD Mono shutdown and morale signal diagnostics
remain unexplained; this repair does not claim to solve those different failures.
