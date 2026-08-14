import { describe, expect, it } from 'vitest';
import { idx } from '../grid';
import { cloneState } from '../state';
import { damageStructure } from '../structures';
import { ScenarioTriggerEntity, TriggerOccurrence } from '../domain/scenario-trigger';
import type { Action, ScenarioTrigger } from '../types';
import { makeLevel, testApply, testState, TEST_CONTENT, u } from './fixtures';

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
    const events = testApply(s, wait(s.units[0].id, 0, 0));
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
    const events = testApply(s, {
      kind: 'command',
      unit: s.units[0].id,
      path: [{ x: 0, y: 0 }, { x: 1, y: 0 }],
      command: { ability: 'wait' },
    });
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
    testApply(s, { kind: 'endTurn' });
    const events = testApply(s, { kind: 'endTurn' });
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

/**
 * A trigger's own bookkeeping.
 *
 * The sweep used to hold six inline conditions and two copies of the same
 * defaulted ledger read; asking the trigger instead means these are answerable
 * without running a battle.
 */
describe('when a trigger is due', () => {
  const scenario = () => ({
    variables: {}, zones: {}, overlays: [], triggers: [],
    firedTriggerIds: [] as string[],
    triggerRuntime: {} as Record<string, { count: number; lastOccurrence: string }>,
    eventCounts: {}, zoneTags: {}, engagementRules: [],
  });
  const at = (turn: number) => new TriggerOccurrence(turn, 1, 'turnStart');
  const declare = (repeat?: ScenarioTrigger['repeat']): ScenarioTrigger => ({
    id: 'tick',
    timing: 'turnStart',
    condition: { type: 'turnAtLeast', turn: 1 },
    effects: [],
    ...(repeat ? { repeat } : {}),
  });

  it('fires a one-shot trigger once for the whole battle', () => {
    const state = scenario();
    const trigger = () => new ScenarioTriggerEntity(state, declare());
    expect(trigger().dueAt(at(1))).toBe(true);
    trigger().recordFiring(at(1));
    expect(state.firedTriggerIds).toEqual(['tick']);
    expect(trigger().dueAt(at(2))).toBe(false);
  });

  it('ignores an occurrence of another timing', () => {
    const trigger = new ScenarioTriggerEntity(scenario(), declare());
    expect(trigger.dueAt(new TriggerOccurrence(1, 1, 'turnEnd'))).toBe(false);
  });

  it('honours cadence, window and firing limit together', () => {
    const state = scenario();
    const trigger = () =>
      new ScenarioTriggerEntity(state, declare({ everyRounds: 2, startTurn: 3, endTurn: 7, maxFirings: 2 }));
    expect([1, 2, 3, 4, 5, 6, 7, 8, 9].map((turn) => trigger().dueAt(at(turn))))
      .toEqual([false, false, true, false, true, false, true, false, false]);

    trigger().recordFiring(at(3));
    trigger().recordFiring(at(5));
    expect(state.triggerRuntime.tick.count).toBe(2);
    expect(trigger().dueAt(at(7))).toBe(false);
  });

  it('fires a repeating trigger at most once per occurrence', () => {
    const state = scenario();
    const trigger = () => new ScenarioTriggerEntity(state, declare({ everyRounds: 1 }));
    trigger().recordFiring(at(4));
    expect(trigger().dueAt(at(4))).toBe(false);
    expect(trigger().dueAt(new TriggerOccurrence(4, 2, 'turnStart'))).toBe(true);
  });

  it('never fires a cadence that is not a whole number of rounds', () => {
    const trigger = new ScenarioTriggerEntity(scenario(), declare({ everyRounds: 0 }));
    expect([1, 2, 3].every((turn) => !trigger.dueAt(at(turn)))).toBe(true);
  });
});
