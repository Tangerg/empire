# Default action refusals and resource boundaries

Run `bash tools/godot/action-refusal-checks/run.sh --verify-guard` from the repository root using the existing provisioned toolchain.

The live TypeScript oracle supplies 27 action outcomes covering ability ownership, unknown abilities, unoffered weapons, missing and illegal targets, unavailable/cooling/empty/status-blocked weapons, moved attacks, capture refusal, and successful wait/heal/capture. C# compares typed error messages, ordered events, complete state and digest through real dispatch and persistence. Failure fixtures must actually fail in the original engine; success fixtures must actually succeed.

Another 96 queries cover all four resource adapters, finite and unlimited balances, fractional/negative/oversized amounts, and NaN/positive/negative infinity. Query results/errors and non-mutation match the source. A further 62 announcement cases check zero/nonzero amounts, finite/unlimited balances, unknown adapters, wrong holder kinds and absent accounts, including exact event references. Non-finite values are represented as explicit test descriptors because JSON cannot carry them as numbers.

`--verify-guard` makes three isolated temporary source copies. One loses the source missing-target distinction; another restores the unlimited-account short circuit before finite normalization; the third skips account lookup for zero announcements. Each mutant must fail its specific assertion, not an unrelated build or runtime error. No production file is mutated, software installed, or test artifact deleted. Generated oracle data and binaries remain in the printed temporary directory. TypeScript is an oracle only, not a native runtime dependency.
