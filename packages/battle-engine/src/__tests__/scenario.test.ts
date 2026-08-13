import { describe, expect, it } from 'vitest';
import { applyAction } from '../actions';
import { idx } from '../grid';
import { cloneState } from '../state';
import { damageStructure } from '../structures';
import type { Action } from '../types';
import { TEST_CONTENT, TEST_RULES, makeLevel, testState, u } from './fixtures';

const wait = (unit: number, x: number, y: number): Action => ({
    kind: 'command',
    unit,
    path: [{ x, y }],
    command: { ability: 'wait' },
  });

describe('headless scenario primitives', () => {
  it('uses the same destroyed-structure trigger for a fantasy tower or stellar node', () => {
    const s = testState(
      makeLevel(['...'], {
        units: [u(0, 0, 'soldier', 1), u(2, 0, 'soldier', 2)],
        structures: [{ id: 'story-node', type: 'command_node', x: 1, y: 0 }],
        scenario: {
          variables: { nodeDown: false },
          triggers: [
            {
              id: 'node-destroyed',
              timing: 'afterAction',
              condition: { type: 'structure', id: 'story-node', state: 'destroyed' },
              effects: [
                { type: 'setVariable', key: 'nodeDown', value: true },
                { type: 'emitSignal', signal: 'node.destroyed' },
              ],
            },
          ],
        },
      }),
    );
    damageStructure(TEST_CONTENT, s, 'story-node', 1000);
    const events = applyAction(s, wait(s.units[0].id, 0, 0), TEST_RULES);
    expect(s.scenario.variables.nodeDown).toBe(true);
    expect(events).toContainEqual({ type: 'scenarioSignal', signal: 'node.destroyed' });
    expect(s.scenario.firedTriggerIds).toEqual(['node-destroyed']);
  });

  it('applies a zone status without knowing whether the fiction is vacuum, fear, or poison', () => {
    const s = testState(
      makeLevel(['....'], {
        units: [u(0, 0, 'soldier', 1), u(3, 0, 'soldier', 2)],
        scenario: {
          zones: [{ id: 'hazard', cells: [{ x: 1, y: 0 }] }],
          triggers: [
            {
              id: 'hazard-entered',
              timing: 'afterAction',
              condition: { type: 'unitInZone', zone: 'hazard', owner: 1 },
              effects: [
                { type: 'addStatus', selector: { owner: 1, zone: 'hazard' }, status: 'shaken', duration: 2 },
                { type: 'emitSignal', signal: 'hazard.entered' },
              ],
            },
          ],
        },
      }),
    );
    const events = applyAction(s, {
      kind: 'command',
      unit: s.units[0].id,
      path: [{ x: 0, y: 0 }, { x: 1, y: 0 }],
      command: { ability: 'wait' },
    }, TEST_RULES);
    expect(s.units[0].statuses[0]).toMatchObject({ id: 'shaken', remaining: 2 });
    expect(events.some((event) => event.type === 'statusApplied')).toBe(true);
  });

  it('uses a turn trigger and zone terrain replacement for flood, tide, or decompression', () => {
    const s = testState(
      makeLevel(['....'], {
        units: [u(0, 0, 'soldier', 1), u(3, 0, 'soldier', 2)],
        scenario: {
          zones: [{ id: 'floodplain', cells: [{ x: 1, y: 0 }, { x: 2, y: 0 }] }],
          triggers: [
            {
              id: 'flood-on-turn-two',
              timing: 'turnStart',
              condition: { type: 'turnAtLeast', turn: 2 },
              effects: [
                { type: 'replaceTerrain', zone: 'floodplain', terrain: 'water' },
                { type: 'emitSignal', signal: 'terrain.flooded' },
              ],
            },
          ],
        },
      }),
    );
    applyAction(s, { kind: 'endTurn' }, TEST_RULES);
    const events = applyAction(s, { kind: 'endTurn' }, TEST_RULES);
    expect(s.map.tiles[idx(s.map, 1, 0)]).toBe('water');
    expect(s.map.tiles[idx(s.map, 2, 0)]).toBe('water');
    expect(events.filter((event) => event.type === 'terrainChanged')).toHaveLength(2);
  });

  it('deep-clones structures, variables, and trigger history for AI simulation', () => {
    const s = testState(
      makeLevel(['..', '..'], {
        units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
        structures: [{ id: 'gate-a', type: 'gate', x: 0, y: 1 }],
        scenario: { variables: { supply: 3 } },
      }),
    );
    const clone = cloneState(s);
    clone.structures[0].hp = 1;
    clone.scenario.variables.supply = 0;
    clone.scenario.firedTriggerIds.push('x');
    expect(s.structures[0].hp).toBeGreaterThan(1);
    expect(s.scenario.variables.supply).toBe(3);
    expect(s.scenario.firedTriggerIds).toEqual([]);
  });
});
