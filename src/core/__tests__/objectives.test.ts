import { describe, expect, it } from 'vitest';
import { applyScenarioEffect } from '../scenario';
import { createState } from '../state';
import { damageStructure } from '../structures';
import { evaluateVictory, objectiveOutcome, refreshObjectiveStates } from '../victory';
import type { GameEvent, Objective } from '../types';
import { makeLevel, u } from './fixtures';

function withEnemyObjectives(level: ReturnType<typeof makeLevel>) {
  level.players[1].objectives = [{ type: 'routEnemies' }];
  return level;
}

describe('composable mission objectives', () => {
  it('evaluates escort, structure, score and zone primitives without story code', () => {
    const objectives: Objective[] = [
      { id: 'escort', type: 'escort', selector: { owner: 1, anyTags: ['infantry'] }, zone: 'exit', count: 1 },
      { id: 'gate', type: 'destroy', structures: ['gate'] },
      { id: 'rescues', type: 'score', variable: 'rescued', atLeast: 2 },
      { id: 'yard', type: 'control', zone: 'yard' },
    ];
    const state = createState(
      withEnemyObjectives(
        makeLevel(['.v.'], {
          units: [u(0, 0, 'soldier', 1), u(2, 0, 'ogre', 2)],
          owners: [{ x: 1, y: 0, owner: 1 }],
          structures: [{ id: 'gate', type: 'gate', x: 2, y: 0, owner: 2 }],
          scenario: {
            variables: { rescued: 2 },
            zones: [
              { id: 'exit', cells: [{ x: 0, y: 0 }] },
              { id: 'yard', cells: [{ x: 1, y: 0 }] },
            ],
          },
          victory: objectives,
        }),
      ),
    );
    expect(objectiveOutcome(state, 1, state.players[0].objectives[0])).toBe('success');
    expect(objectiveOutcome(state, 1, state.players[0].objectives[1])).toBe('pending');
    expect(objectiveOutcome(state, 1, state.players[0].objectives[2])).toBe('success');
    expect(objectiveOutcome(state, 1, state.players[0].objectives[3])).toBe('success');
    damageStructure(state, 'gate', 999, () => {});
    expect(objectiveOutcome(state, 1, state.players[0].objectives[1])).toBe('success');
  });

  it('keeps sequence stages ordered even if a later condition is already true', () => {
    const level = withEnemyObjectives(
      makeLevel(['..'], {
        units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
        scenario: { variables: { breach: false, beacon: true } },
        victory: [
          {
            id: 'operation',
            type: 'sequence',
            objectives: [
              { id: 'breach', type: 'interact', variable: 'breach', equals: true },
              { id: 'beacon', type: 'interact', variable: 'beacon', equals: true },
            ],
          },
        ],
      }),
    );
    const state = createState(level);
    refreshObjectiveStates(state);
    expect(state.players[0].objectiveStates.breach.status).toBe('active');
    expect(state.players[0].objectiveStates.beacon.status).toBe('active');
    expect(state.players[0].objectiveStates.operation.status).toBe('active');

    state.scenario.variables.breach = true;
    const events: GameEvent[] = [];
    refreshObjectiveStates(state, (event) => events.push(event));
    expect(state.players[0].objectiveStates.breach.status).toBe('completed');
    expect(state.players[0].objectiveStates.beacon.status).toBe('completed');
    expect(state.players[0].objectiveStates.operation.status).toBe('completed');
    expect(events.filter((event) => event.type === 'objectiveChanged')).toHaveLength(3);
  });

  it('activates and reveals a hidden objective through the scenario DSL', () => {
    const state = createState(
      withEnemyObjectives(
        makeLevel(['..'], {
          units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
          scenario: { variables: { console: false } },
          victory: [
            {
              id: 'secret-console',
              type: 'interact',
              variable: 'console',
              equals: true,
              active: false,
              hidden: true,
            },
          ],
        }),
      ),
    );
    const events: GameEvent[] = [];
    const emit = (event: GameEvent) => events.push(event);
    applyScenarioEffect(state, { type: 'revealObjective', player: 1, id: 'secret-console' }, emit);
    applyScenarioEffect(state, { type: 'activateObjective', player: 1, id: 'secret-console' }, emit);
    applyScenarioEffect(state, { type: 'setVariable', key: 'console', value: true }, emit);
    const result = evaluateVictory(state, emit);
    expect(state.players[0].objectiveStates['secret-console']).toMatchObject({
      hidden: false,
      status: 'completed',
    });
    expect(result.team).toBe(1);
  });

  it('uses failOn as a critical loss condition', () => {
    const state = createState(
      withEnemyObjectives(
        makeLevel(['..'], {
          units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
          scenario: { variables: { convoyLost: true } },
          victory: [
            {
              id: 'protect-convoy',
              type: 'failOn',
              condition: { type: 'variable', key: 'convoyLost', op: 'eq', value: true },
              objective: { type: 'surviveTurns', turns: 5 },
            },
          ],
        }),
      ),
    );
    const result = evaluateVictory(state);
    expect(state.players[0].alive).toBe(false);
    expect(result.team).toBe(2);
  });

  it('does not let an optional objective end the battle by itself', () => {
    const state = createState(
      withEnemyObjectives(
        makeLevel(['..'], {
          units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
          scenario: { variables: { chest: true } },
          victory: [
            {
              id: 'bonus',
              type: 'optional',
              objective: { type: 'interact', variable: 'chest', equals: true },
            },
            { id: 'main', type: 'surviveTurns', turns: 4 },
          ],
        }),
      ),
    );
    const result = evaluateVictory(state);
    expect(state.players[0].objectiveStates.bonus.status).toBe('completed');
    expect(result.team).toBeNull();
  });
});
