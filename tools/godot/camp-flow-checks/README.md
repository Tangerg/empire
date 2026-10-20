# Native campaign camp/retry flow

Run without Godot or user-save writes:

    bash tools/godot/camp-flow-checks/run.sh

The runner follows the original opening story/choice path into the real first battle. It checks:

- Retry restores the seeded opening state without repeating campaign choices, request creation, or battle-node effects.
- Exit retains the pending request and prior campaign snapshot, clears played battle history, and marks camp navigation.
- Camp save/load/reentry preserves request identity, restores the same battle opening, and does not replay old presentation receipts.
- Malformed camp booleans, missing retained requests, and mismatched request identity are rejected without changing the current campaign, battle, or camp flag.
- A real defeat is reached using legal human end-turn orders and canonical enemy AI, not an injected outcome. Defeat retry and exit do not enter `campaign-failed` or append a battle-history outcome; save/load from that camp reenters the original request.

Save comparisons exclude only the two intentionally refreshed `savedAt` envelope timestamps. Every canonical campaign/battle/request/event/camp field remains compared. A mismatch writes `/tmp/empire-camp-flow-mismatch.json` for diagnosis. The action limit is a failing diagnostic guard, never a fabricated completion.
