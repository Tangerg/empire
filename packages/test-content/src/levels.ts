import {
  COMMAND_POINTS_RESOURCE,
  FUNDS_RESOURCE,
  normaliseLevel,
  type LevelCliffEdge,
  type LevelCommander,
  type LevelComposite,
  type LevelData,
  type LevelDeployment,
  type LevelDirectionalCover,
  type LevelScenario,
  type LevelStructure,
  type LevelUnit,
  type Objective,
  type PlayerId,
  type RuleSet,
} from '@empire/battle-engine';

/**
 * Level sketches for tests, in the package that exists to compose test content.
 *
 * They used to live inside the engine's own `__tests__` folder, which meant the
 * one suite outside that package reached them through `@empire/battle-engine/
 * __tests__/fixtures` — a wildcard subpath export that published the engine's
 * test folder as part of its public API. A helper two packages share is a
 * shared helper; this is where the shared test-only things live.
 */

/** Build a level from an ASCII sketch — keeps the tests readable. */
export function makeLevel(
  terrain: string[],
  opts: {
    /**
     * What this level is called, when a test cares.
     *
     * Two suites wrote a whole `normaliseLevel({…})` by hand — the two-player
     * array, the resources, the victory list — because they needed a level whose
     * id was theirs and this builder's was `test`. Everything else they wrote was
     * what this builder already produces.
     */
    id?: string;
    units?: LevelUnit[];
    commanders?: LevelCommander[];
    owners?: { x: number; y: number; owner: PlayerId }[];
    rules?: Partial<RuleSet>;
    victory?: Objective[] | undefined;
    funds?: [number, number];
    structures?: LevelStructure[] | undefined;
    composites?: LevelComposite[] | undefined;
    scenario?: LevelScenario | undefined;
    deployment?: LevelDeployment | undefined;
    elevation?: number[] | undefined;
    cliffs?: LevelCliffEdge[] | undefined;
    directionalCover?: LevelDirectionalCover[];
  } = {},
): LevelData {
  return normaliseLevel({
    schema: 2,
    id: opts.id ?? 'test',
    name: opts.id ?? 'test',
    width: terrain[0].length,
    height: terrain.length,
    terrain,
    owners: opts.owners ?? [],
    units: opts.units ?? [],
    commanders: opts.commanders ?? [],
    structures: opts.structures ?? [],
    composites: opts.composites ?? [],
    players: [
      {
        id: 1,
        name: 'P1',
        team: 1,
        color: '#3f7fd8',
        controller: 'human',
        resources: {
          [FUNDS_RESOURCE]: { current: opts.funds?.[0] ?? 0, capacity: null },
          [COMMAND_POINTS_RESOURCE]: { current: 0, capacity: 5 },
        },
      },
      {
        id: 2,
        name: 'P2',
        team: 2,
        color: '#d8483f',
        controller: 'ai',
        resources: {
          [FUNDS_RESOURCE]: { current: opts.funds?.[1] ?? 0, capacity: null },
          [COMMAND_POINTS_RESOURCE]: { current: 0, capacity: 5 },
        },
        ai: { aggression: 0.5 },
      },
    ],
    rules: opts.rules ?? {},
    victory: opts.victory ?? [{ type: 'routEnemies' }, { type: 'captureHQ' }],
    scenario: opts.scenario,
    deployment: opts.deployment,
    elevation: opts.elevation,
    cliffs: opts.cliffs,
    directionalCover: opts.directionalCover,
  } satisfies Partial<LevelData>);
}

export const u = (x: number, y: number, unit: string, owner: PlayerId, hp?: number): LevelUnit => ({
  x,
  y,
  unit,
  owner,
  ...(hp === undefined ? {} : { hp }),
});
