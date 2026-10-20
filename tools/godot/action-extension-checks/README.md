# Action execution policy contracts

The sole BattleActionRegistry owns handler definitions, deployment eligibility and handoff metadata. DefaultActionHandlers.Create returns unbound source-ordered definitions. ActionExecutionPipeline owns phase admission, counted event emission and after-action lifecycle; Battle remains the transaction, rollback and receipt owner. Engine-created battles always use the pipeline. Standalone transaction tests may use raw delegate adapters with a bare context; demanding composed rules/lifecycle there fails explicitly.

Run `bash tools/godot/action-extension-checks/run.sh --frozen` without Node, or omit --frozen to regenerate original TypeScript expectations. Add --verify-guard to deliberately break six isolated semantic controls.

Thirty source cases cover all three phases, metadata-selected custom deployment actions, unknown action precedence, partial-event rollback, exact event counts, after-action triggers, phase changes and handoff. Default registration order is compared. Existing Core tests separately verify 396 complete battle replay actions. This slice does not by itself establish plugin composition or custom save admission.
