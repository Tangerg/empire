import { describe, expect, it } from 'vitest';
import { commandOptions, CoreActionHandlers, applyActionWith } from '../actions';
import { createDefaultBattleRuleServices } from '../action-system';
import { compositeStatus, moveComposite } from '../composites';
import { activeFormation } from '../formations';
import { changeMorale } from '../morale';
import { objectiveOutcome } from '../victory';
import { conditionMet, runScenarioTriggers } from '../scenario';
import { createState } from '../state';
import { BattleAggregate } from '../domain/battle-aggregate';
import { embarkUnit } from '../transports';
import { chooseAction } from '../ai';
import type { GameEvent } from '../types';
import { makeLevel, u } from './fixtures';

describe('campaign-grade battle primitives', () => {
  it('runs bounded cyclic scenario triggers once per timing occurrence', () => {
    const state = createState(makeLevel(['..'], {
      units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
      scenario: {
        variables: { pulses: 0 },
        triggers: [{
          id: 'environment-pulse',
          timing: 'turnStart',
          condition: { type: 'turnCycle', every: 1 },
          effects: [{ type: 'addVariable', key: 'pulses', amount: 1 }],
          repeat: { everyRounds: 1, maxFirings: 2 },
        }],
      },
    }));
    runScenarioTriggers(state, 'turnStart', () => {});
    runScenarioTriggers(state, 'turnStart', () => {});
    expect(state.scenario.variables.pulses).toBe(1);
    state.turn = 2;
    runScenarioTriggers(state, 'turnStart', () => {});
    state.turn = 3;
    runScenarioTriggers(state, 'turnStart', () => {});
    expect(state.scenario.variables.pulses).toBe(2);
    expect(state.scenario.triggerRuntime['environment-pulse'].count).toBe(2);
  });

  it('counts semantic events for declarative scenario predicates', () => {
    const state = createState(makeLevel(['..'], {
      units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
    }));
    const rules = createDefaultBattleRuleServices();
    applyActionWith(state, { kind: 'endTurn' }, CoreActionHandlers, rules);
    expect(conditionMet(state, { type: 'eventCount', event: 'turnEnd', op: 'gte', value: 1 })).toBe(true);
  });

  it('enforces dynamic no-combat zones through shared attack legality', () => {
    const state = createState(makeLevel(['...'], {
      units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
      scenario: {
        zones: [{ id: 'truce', cells: [{ x: 0, y: 0 }, { x: 1, y: 0 }] }],
        engagementRules: [{ id: 'bridge-truce', zone: 'truce', mode: 'no-attacks' }],
      },
    }));
    const options = commandOptions(state, state.units[0], { x: 0, y: 0 });
    expect(options.some((option) => option.ability === 'attack')).toBe(false);
  });

  it('keeps formations data-defined and active only while spatially supported', () => {
    const rules = createDefaultBattleRuleServices();
    rules.content.units.override('soldier', {
      formations: ['formation-defensive', 'formation-loose'],
    });
    const state = createState(makeLevel(['...'], {
      units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 1), u(2, 0, 'soldier', 2)],
    }), rules.content);
    const unit = state.units[0];
    const events = applyActionWith(
      state,
      { kind: 'changeFormation', unit: unit.id, formation: 'formation-defensive' },
      CoreActionHandlers,
      rules,
    );
    expect(activeFormation(state, unit, rules.content)?.id).toBe('formation-defensive');
    expect(events).toContainEqual({
      type: 'formationChanged', unit: unit.id, from: null, to: 'formation-defensive',
    });
  });

  it('embarks and disembarks identity-preserving units through formal actions', () => {
    const rules = createDefaultBattleRuleServices();
    rules.content.units.override('knight', { transport: { capacity: 2, allowedTags: ['infantry'] } });
    const state = createState(makeLevel(['.....'], {
      units: [u(0, 0, 'soldier', 1), u(1, 0, 'knight', 1), u(4, 0, 'soldier', 2)],
    }), rules.content);
    const passenger = state.units[0];
    const carrier = state.units[1];
    applyActionWith(state, { kind: 'embark', unit: passenger.id, carrier: carrier.id }, CoreActionHandlers, rules);
    expect(state.units.some((unit) => unit.id === passenger.id)).toBe(false);
    expect(state.embarkedUnits[0].unit.id).toBe(passenger.id);
    const events = applyActionWith(
      state,
      { kind: 'disembark', carrier: carrier.id, unit: passenger.id, at: { x: 2, y: 0 } },
      CoreActionHandlers,
      rules,
    );
    expect(state.units.find((unit) => unit.id === passenger.id)).toMatchObject({ x: 2, y: 0, done: true });
    expect(events.some((event) => event.type === 'unitDisembarked')).toBe(true);
  });

  it('keeps transport-loss invariants inside the battle aggregate', () => {
    const rules = createDefaultBattleRuleServices();
    rules.content.units.override('knight', { transport: { capacity: 1 } });
    const state = createState(makeLevel(['....'], {
      units: [u(0, 0, 'soldier', 1), u(1, 0, 'knight', 1), u(3, 0, 'soldier', 2)],
    }), rules.content);
    const passenger = state.units[0];
    const carrier = state.units[1];
    embarkUnit(state, passenger.id, carrier.id, () => {}, rules.content);
    const result = new BattleAggregate(state, rules.content).damageUnit(carrier.id, 999);
    expect(result.passengerMarkers).toHaveLength(1);
    expect(state.embarkedUnits).toHaveLength(0);
    expect(state.markers.some((marker) =>
      marker.kind === 'transport-loss' && marker.fallenUnit?.id === passenger.id)).toBe(true);
  });

  it('routes zero-morale units into recoverable battlefield markers', () => {
    const state = createState(makeLevel(['..'], {
      units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
      rules: { moraleEnabled: true },
    }));
    const id = state.units[0].id;
    const events: GameEvent[] = [];
    changeMorale(state, id, -999, 'test-shock', (event) => events.push(event));
    expect(state.units.some((unit) => unit.id === id)).toBe(false);
    expect(state.markers[0]).toMatchObject({ kind: 'routed', fallenUnit: { id } });
    expect(events.some((event) => event.type === 'unitRouted')).toBe(true);
  });

  it('moves and evaluates multi-part battlefield targets as one aggregate', () => {
    const level = makeLevel(['.....'], {
      units: [u(0, 0, 'soldier', 1), u(4, 0, 'soldier', 2)],
      structures: [
        { id: 'left-engine', type: 'boss_part', x: 1, y: 0, owner: 2 },
        { id: 'right-engine', type: 'boss_part', x: 2, y: 0, owner: 2, disabled: true },
      ],
      composites: [{ id: 'moving-fortress', parts: ['left-engine', 'right-engine'], minimumNeutralized: 1 }],
      victory: [{ type: 'neutralizeComposite', composite: 'moving-fortress' }],
    });
    const state = createState(level);
    expect(compositeStatus(state, 'moving-fortress').state).toBe('neutralized');
    expect(objectiveOutcome(state, 1, state.players[0].objectives[0])).toBe('success');
    const events: GameEvent[] = [];
    moveComposite(state, 'moving-fortress', { x: 1, y: 0 }, (event) => events.push(event));
    expect(state.structures.map((structure) => structure.x)).toEqual([2, 3]);
    expect(events.filter((event) => event.type === 'structureMoved')).toHaveLength(2);
  });

  it('lets retreat directives override opportunistic AI attacks', () => {
    const state = createState(makeLevel(['.....'], {
      units: [
        { ...u(3, 0, 'soldier', 1), directive: { mode: 'retreat', zone: 'exit' } },
        u(4, 0, 'soldier', 2),
      ],
      scenario: { zones: [{ id: 'exit', cells: [{ x: 0, y: 0 }] }] },
    }));
    const action = chooseAction(state, { aggression: 1 });
    expect(action.kind).toBe('command');
    if (action.kind === 'command') {
      expect(action.path.at(-1)).toEqual({ x: 0, y: 0 });
      expect(action.command.ability).toBe('wait');
    }
  });
});
