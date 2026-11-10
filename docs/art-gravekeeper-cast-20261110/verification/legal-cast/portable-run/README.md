# Final three-route portable replay

The official runner was invoked from `/tmp` by absolute path against a frozen copy of the final presentation build. `Empire.dll` SHA256 is `23f3ea10fdbb1bc4f0329c9c995cfe32ff1545a412126b6c90671a0575d9cd9c`. No production build ran; only the copied scratch harness compiled, with zero warnings and zero errors.

Actual outcomes:

- New empty sibling scratch output: exit0; cast133, heal-right179, heal-left179 actions all pass.
- Repository-contained output: exit2 and no directory created.
- Nonempty sibling output: exit2 and existing sentinel preserved.

Each route passes complete original-campaign replay, every-step independent core state/event equality, exact preserved trace and save comparison, official before/after save roundtrips and final action replay after reload. Trace and receipt files match the preserved evidence bytes exactly. Preserved before/after files also match their accepted source bytes exactly; neither replay nor promotion rewrites them.

The actual command logs, exit codes, frozen DLL hashes, full per-route verification results and machine-readable summary are included here. `summary.json` also records each native input SHA256 for recording bindings. Scratch build outputs and regenerated saves remain outside the repository.

The final replay was repeated after applying the repository whitespace formatter solely to `harness/Program.cs`. The targeted `dotnet format whitespace --folder . --include docs/art-gravekeeper-cast-20261110/verification/legal-cast/harness/Program.cs --verify-no-changes` check returned exit0; its actual output and exit code are preserved in `formatter.txt` and `formatter.exit`. All three routes and both output guards passed again after formatting. `summary.json` binds this result to the formatted harness SHA256.
