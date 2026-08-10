import { normaliseLevel } from '../mapio';
import type { LevelData, LevelUnit, Objective, PlayerId, RuleSet } from '../types';

/** Build a level from an ASCII sketch — keeps the tests readable. */
export function makeLevel(
  terrain: string[],
  opts: {
    units?: LevelUnit[];
    owners?: { x: number; y: number; owner: PlayerId }[];
    rules?: Partial<RuleSet>;
    victory?: Objective[];
    funds?: [number, number];
  } = {},
): LevelData {
  return normaliseLevel({
    schema: 1,
    id: 'test',
    name: 'test',
    width: terrain[0].length,
    height: terrain.length,
    terrain,
    owners: opts.owners ?? [],
    units: opts.units ?? [],
    players: [
      {
        id: 1,
        name: 'P1',
        team: 1,
        color: '#3f7fd8',
        controller: 'human',
        funds: opts.funds?.[0] ?? 0,
      },
      {
        id: 2,
        name: 'P2',
        team: 2,
        color: '#d8483f',
        controller: 'ai',
        funds: opts.funds?.[1] ?? 0,
        ai: { aggression: 0.5 },
      },
    ],
    rules: opts.rules ?? {},
    victory: opts.victory ?? [{ type: 'routEnemies' }, { type: 'captureHQ' }],
  } satisfies Partial<LevelData>);
}

export const u = (x: number, y: number, unit: string, owner: PlayerId, hp?: number): LevelUnit => ({
  x,
  y,
  unit,
  owner,
  ...(hp === undefined ? {} : { hp }),
});
