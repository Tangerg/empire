import type { Objective, ObjectiveRuntime } from './types';

function withChildren(objective: Objective, id: string): Objective {
  if (objective.type === 'all' || objective.type === 'any' || objective.type === 'sequence') {
    return {
      ...objective,
      id,
      objectives: objective.objectives.map((child, index) =>
        withChildren(child, child.id ?? `${id}.${index + 1}`),
      ),
    };
  }
  if (objective.type === 'optional' || objective.type === 'failOn') {
    return {
      ...objective,
      id,
      objective: withChildren(objective.objective, objective.objective.id ?? `${id}.1`),
    };
  }
  return { ...objective, id };
}

/** Copies definitions and supplies deterministic ids for save/replay references. */
export function assignObjectiveIds(objectives: Objective[], prefix: string): Objective[] {
  return objectives.map((objective, index) =>
    withChildren(objective, objective.id ?? `${prefix}.${index + 1}`),
  );
}

export function walkObjectives(objectives: Objective[]): Objective[] {
  const out: Objective[] = [];
  const visit = (objective: Objective) => {
    out.push(objective);
    if (objective.type === 'all' || objective.type === 'any' || objective.type === 'sequence') {
      objective.objectives.forEach(visit);
    } else if (objective.type === 'optional' || objective.type === 'failOn') {
      visit(objective.objective);
    }
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
