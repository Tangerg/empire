import type { Objective, ObjectiveRuntime, RunningObjective } from './types';

/**
 * The shape of an objective document, stated once.
 *
 * "Which objectives is this one built out of" was written four times — the id
 * assigner, the runtime-state walker, the level linter's declaration pass and
 * the linter's own recursion — each as its own list of composite kind names.
 * Four lists to extend, and a rule pack's composite kind was in none of them.
 *
 * Read structurally instead: a composite objective is one that *holds* others,
 * and the document says so by carrying them. That is true of a pack's own
 * composite kind without anybody adding its name anywhere.
 */
function heldObjectives(objective: Objective): Objective[] | null {
  const held = objective as { objectives?: Objective[]; objective?: Objective };
  if (Array.isArray(held.objectives)) return held.objectives;
  return held.objective ? [held.objective] : null;
}

/** Whether the document shape says this objective is built out of others. */
export function isCompositeObjective(objective: Objective): boolean {
  return heldObjectives(objective) !== null;
}

/** Children a composite objective declares; empty for a leaf. */
export function declaredChildObjectives(objective: Objective): Objective[] {
  return heldObjectives(objective) ?? [];
}

/** The same objective with each declared child replaced in place. */
function withDeclaredChildren(
  objective: Objective,
  replace: (child: Objective, index: number) => Objective,
): Objective {
  const held = objective as { objectives?: Objective[]; objective?: Objective };
  if (Array.isArray(held.objectives)) {
    return { ...objective, objectives: held.objectives.map(replace) } as Objective;
  }
  if (held.objective) {
    return { ...objective, objective: replace(held.objective, 0) } as Objective;
  }
  return { ...objective };
}

function withChildren(objective: Objective, id: string): RunningObjective {
  const copy = withDeclaredChildren(objective, (child, index) =>
    withChildren(child, child.id ?? `${id}.${index + 1}`));
  return { ...copy, id };
}

/**
 * Copies definitions and supplies deterministic ids for save/replay references.
 *
 * This is the line between a declared objective and a running one, so it is also
 * where the type stops being optional about the id.
 */
export function assignObjectiveIds(objectives: Objective[], prefix: string): RunningObjective[] {
  return objectives.map((objective, index) =>
    withChildren(objective, objective.id ?? `${prefix}.${index + 1}`),
  );
}

/**
 * The same objectives, seen as objectives in play.
 *
 * `assignObjectiveIds` names a whole tree, so every child of a running objective
 * is running too — but the two ways of asking for children, the document's shape
 * and the handler's declaration, both answer about documents, because that is
 * what an author writes. This is the one place that states the invariant, and the
 * only place that needs to.
 */
export const inPlay = (objectives: readonly Objective[]): RunningObjective[] =>
  objectives as RunningObjective[];

function walkObjectives(objectives: readonly RunningObjective[]): RunningObjective[] {
  const out: RunningObjective[] = [];
  const visit = (objective: RunningObjective): void => {
    out.push(objective);
    inPlay(declaredChildObjectives(objective)).forEach(visit);
  };
  objectives.forEach(visit);
  return out;
}

export function createObjectiveStates(
  objectives: readonly RunningObjective[],
): Record<string, ObjectiveRuntime> {
  return Object.fromEntries(
    walkObjectives(objectives).map((objective) => [
      objective.id,
      {
        id: objective.id,
        status: objective.active === false ? 'inactive' : 'active',
        hidden: objective.hidden ?? false,
      },
    ]),
  );
}
