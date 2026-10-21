# Actual campaign route differential test

From the repository root, run `bash tools/godot/campaign-route-checks/run.sh first`
or `last`. These choose the first or last available authored choice; both sides
use the original default AI policy without changing human controller flags.

The TypeScript oracle executes real policy-transformed battles and reconstructs
its campaign and battle readers at checkpoints. C# independently chooses every
action, dispatches through the application session, compares ordered events and
state digests, then reloads its own native checkpoint before continuing.
The schema-2 native envelope intentionally omits the source mutable request;
request projections still match exactly, but source wrappers are not imported.
Campaign requests, choices, carryover states and terminal outcomes are compared.
No injected victory or synthetic campaign transition is used.

Verified `first-default`: c01-01 victory after 142 actions, c01-02 defeat after
234 actions, 376 actions total and 14 save/resume checkpoints, terminal `failed`.
This proves one real loss route and carried state, not complete winning coverage
or every campaign branch. Generated JSONL stays in a temporary evidence directory.

Also verified `last-default`: c01-01 victory after 126 actions and c01-02 defeat
after 288, totaling 414 independently selected native actions with 14 source
checkpoints/native resumes. Both authored choice routes therefore exercise real roster and
fund carryover and natural campaign failure, not a winning full campaign.
