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
- Focused native checks passed: menu 107, staging 157, narrative 3309, campaign UI
  flow 209, settlement 397, and campaign navigation 9598 assertions. The three
  native input routes reach the same initial battle digest `78e7c7b9` with no
  battle orders. A first supplemental menu invocation omitted its frozen-oracle
  argument and was rejected at setup; the corrected invocation passed without
  changing the test or fixture.
- The first complete aggregate exited 0 in 2359 seconds: 16 core groups, all 30
  native scenes, and 16 formal levels with 3425 ordered commands matched the
  baseline traces and final digests. Its independent warning audit failed on one
  added viewport-enter warning: the new native route left the shared pointer
  inside, then the existing pointer fixture notified entry again. The native
  route now retires its pointer state on exit; no production change or warning
  suppression was needed. The failed audit and original logs are retained.
- The corrected navigation scene passed again with exactly the previous warning
  multiset. A fresh complete aggregate on `8f368a94e` then passed from
  11:44:50 to 12:28:33 UTC (2623 seconds): all 16 core groups, 30 native scenes,
  and editor import passed their strict process/diagnostic gates. Navigation
  reported 9634 assertions and no failed groups; its total includes assertions
  within frame-wait loops, so it is not a fixed test-case count.
- Independent final audit passed: all 16 formal levels and 3425 commands retain
  identical ordered action/event/receipt traces and final digests; all 40 warning
  headers exactly match the previous full gate. All 31 native/editor raw logs,
  source commit and assembly hashes are retained. The additional pointer warning
  is absent rather than filtered or reclassified.
- `ExportRelease` application configuration compiled with zero warnings and
  errors. This is configuration compilation, not platform-package acceptance.

These are native headless interaction and lifecycle checks, not graphical visual
acceptance. Previous isolated HUD Mono shutdown and morale signal diagnostics
remain unexplained; this repair does not claim to solve those different failures.
