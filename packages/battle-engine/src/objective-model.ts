import type { Objective, ObjectiveRuntime } from './types';

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

function withChildren(objective: Objective, id: string): Objective {
  const copy = withDeclaredChildren(objective, (child, index) =>
    withChildren(child, child.id ?? `${id}.${index + 1}`));
  return { ...copy, id };
}

/** Copies definitions and supplies deterministic ids for save/replay references. */
export function assignObjectiveIds(objectives: Objective[], prefix: string): Objective[] {
  return objectives.map((objective, index) =>
    withChildren(objective, objective.id ?? `${prefix}.${index + 1}`),
  );
}

export function walkObjectives(objectives: Objective[]): Objective[] {
  const out: Objective[] = [];
  const visit = (objective: Objective): void => {
    out.push(objective);
    declaredChildObjectives(objective).forEach(visit);
  };
  objectives.forEach(visit);
  return out;
}

export function createObjectiveStates(objectives: Objective[]): Record<string, ObjectiveRuntime> {
  return Object.fromEntries(
    walkObjectives(objectives).map((objective) => {
      const id = objective.id!;
      return [
        id,
        {
          id,
          status: objective.active === false ? 'inactive' : 'active',
          hidden: objective.hidden ?? false,
        },
      ];
    }),
  );
}
