import { describe, expect, it } from 'vitest';
import { moveCostOf } from '../movement';
import { makeLevel, testApply, testForecastStructure, testMoveField, testState, u } from './fixtures';

describe('cross-theme engine contracts', () => {
  it('fantasy: a commanded siege unit breaks a gate and completes the assault objective', () => {
    const level = makeLevel(['.....'], {
      units: [
        { ...u(0, 0, 'soldier', 1), key: 'banner' },
        { ...u(1, 0, 'ballista', 1), commander: 'oath-banner' },
        u(4, 0, 'ogre', 2),
      ],
      commanders: [
        {
          id: 'oath-banner',
          unitKey: 'banner',
          radius: 2,
          aura: { attackMultiplier: 1.2 },
        },
      ],
      structures: [{ id: 'black-gate', type: 'gate', owner: 2, x: 3, y: 0, hp: 25 }],
      victory: [{ id: 'breach', type: 'destroy', structures: ['black-gate'] }],
    });
    level.players[1].objectives = [{ type: 'routEnemies' }];
    const state = testState(level);
    expect(testForecastStructure(state, state.units[1], state.structures[0]).commanderAttackMultiplier).toBe(1.2);
    testApply(state, {
      kind: 'command',
      unit: state.units[1].id,
      path: [{ x: 1, y: 0 }],
      command: { ability: 'attack', weapon: 'ballista_bolt', target: { x: 3, y: 0 } },
    });
    expect(state.players[0].objectiveStates.breach.status).toBe('completed');
  });

  it('stellar: vacuum, a destructible node and a scenario signal use generic primitives', () => {
    const level = makeLevel(['....'], {
      units: [u(0, 0, 'mage', 1), u(3, 0, 'ogre', 2)],
      structures: [{ id: 'relay', type: 'command_node', owner: 2, x: 2, y: 0, hp: 30 }],
      scenario: {
        variables: { networkDown: false },
        zones: [{ id: 'breached-hull', cells: [{ x: 0, y: 0 }] }],
        overlays: [{ id: 'vacuum-a', type: 'vacuum', zone: 'breached-hull' }],
        triggers: [
          {
            id: 'relay-offline',
            timing: 'afterAction',
            condition: { type: 'structure', id: 'relay', state: 'destroyed' },
            effects: [
              { type: 'setVariable', key: 'networkDown', value: true },
              { type: 'emitSignal', signal: 'network.offline' },
            ],
          },
        ],
      },
      victory: [
        {
          id: 'disable-network',
          type: 'all',
          objectives: [
            { type: 'destroy', structures: ['relay'] },
            { type: 'interact', variable: 'networkDown', equals: true },
          ],
        },
      ],
    });
    level.players[1].objectives = [{ type: 'routEnemies' }];
    const state = testState(level);
    const events = testApply(state, {
      kind: 'command',
      unit: state.units[0].id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', weapon: 'mage_overcharge', target: { x: 2, y: 0 } },
    });
    expect(state.scenario.overlays[0].type).toBe('vacuum');
    expect(events).toContainEqual({ type: 'scenarioSignal', signal: 'network.offline' });
    expect(state.players[0].objectiveStates['disable-network'].status).toBe('completed');
  });

  it('history: flood modifies movement while escort and territorial control compose a victory', () => {
    const level = makeLevel(['..v.'], {
      units: [u(0, 0, 'soldier', 1), u(3, 0, 'ogre', 2)],
      owners: [{ x: 2, y: 0, owner: 1 }],
      scenario: {
        zones: [
          { id: 'river-road', cells: [{ x: 1, y: 0 }] },
          { id: 'escort-end', cells: [{ x: 1, y: 0 }] },
          { id: 'granary', cells: [{ x: 2, y: 0 }] },
        ],
        overlays: [{ id: 'seasonal-flood', type: 'flooded', zone: 'river-road' }],
      },
      victory: [
        {
          id: 'secure-supplies',
          type: 'all',
          objectives: [
            { type: 'escort', selector: { owner: 1, anyTags: ['infantry'] }, zone: 'escort-end', count: 1 },
            { type: 'control', zone: 'granary' },
          ],
        },
      ],
    });
    level.players[1].objectives = [{ type: 'routEnemies' }];
    const state = testState(level);
    const field = testMoveField(state, state.units[0]);
    expect(moveCostOf(field, state.map, { x: 1, y: 0 })).toBe(2);
    testApply(state, {
      kind: 'command',
      unit: state.units[0].id,
      path: [{ x: 0, y: 0 }, { x: 1, y: 0 }],
      command: { ability: 'wait' },
    });
    expect(state.players[0].objectiveStates['secure-supplies'].status).toBe('completed');
    expect(state.winnerTeam).toBe(1);
  });
});
