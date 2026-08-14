import { describe, expect, it } from 'vitest';
import { Abilities, defineAbility } from '../abilities';
import { DefaultAbilityAiEvaluators } from '../ai';

import { defineCareer } from '../content-builders';
import { createBattleEngine } from '../plugins/default';
import { idx } from '../grid';
import { areAllies, cloneState } from '../state';
import type { GameEvent } from '../types';
import { makeLevel, testApply, testScenarioEffect, testState, TEST_CONTENT, TEST_ENGINE, TEST_RULES, u } from './fixtures';
import { createTestCatalog } from '@empire/test-content';

const collect = () => {
  const events: GameEvent[] = [];
  return { events, emit: (event: GameEvent) => events.push(event) };
};

describe('advanced battle-local primitives', () => {
  it('owns reinforcement, withdrawal, corpse, revival and team changes in the battle state', () => {
    const state = testState(makeLevel(['....'], {
      units: [u(0, 0, 'soldier', 1), u(3, 0, 'soldier', 2)],
      scenario: { zones: [{ id: 'reserve', cells: [{ x: 1, y: 0 }] }] },
    }));
    const log = collect();

    testScenarioEffect(state, {
      type: 'spawnUnits',
      reason: 'summon',
      ready: true,
      units: [{ key: 'summoned', x: 1, y: 0, unit: 'rogue', owner: 1 }],
    }, log.emit);
    const summoned = state.units.find((unit) => unit.key === 'summoned')!;
    expect(summoned.done).toBe(false);
    expect(log.events).toContainEqual(expect.objectContaining({ type: 'unitSpawned', reason: 'summon' }));

    testScenarioEffect(state, {
      type: 'withdrawUnits', selector: { ids: [summoned.id] }, leaveCorpse: true,
    }, log.emit);
    const corpse = state.markers[0];
    expect(state.units.some((unit) => unit.id === summoned.id)).toBe(false);
    expect(corpse).toMatchObject({ kind: 'corpse', owner: 1, at: { x: 1, y: 0 } });

    testScenarioEffect(state, {
      type: 'reviveMarkers', selector: { ids: [corpse.id] }, hpPercent: 0.25,
    }, log.emit);
    const revived = state.units.find((unit) => unit.id === summoned.id)!;
    expect(revived.hp).toBe(20);
    expect(revived.done).toBe(true);
    expect(state.markers).toEqual([]);

    testScenarioEffect(state, { type: 'setPlayerTeam', player: 2, team: 1 }, log.emit);
    expect(areAllies(state, 1, 2)).toBe(true);

    const cloned = cloneState(state);
    cloned.units.find((unit) => unit.id === revived.id)!.hp = 1;
    expect(revived.hp).toBe(20);
  });

  it('changes elevation, cliffs and directional cover as first-class spatial layers', () => {
    const state = testState(makeLevel(['...'], {
      units: [u(0, 0, 'soldier', 1), u(2, 0, 'soldier', 2)],
      scenario: { zones: [{ id: 'ridge', cells: [{ x: 1, y: 0 }] }] },
    }));
    const log = collect();

    testScenarioEffect(state, { type: 'setElevation', zone: 'ridge', value: 2 }, log.emit);
    testScenarioEffect(state, {
      type: 'setCliffs', blocked: true, edges: [{ from: { x: 0, y: 0 }, to: { x: 1, y: 0 } }],
    }, log.emit);
    testScenarioEffect(state, {
      type: 'setDirectionalCover', covers: [{ at: { x: 1, y: 0 }, sides: { west: 'full' } }],
    }, log.emit);

    expect(state.map.elevation[idx(state.map, 1, 0)]).toBe(2);
    expect(state.map.cliffs).toHaveLength(1);
    expect(state.map.directionalCover[0].sides.west).toBe('full');
    expect(TEST_RULES.space.moveField(state, state.units[0]).stops.has(idx(state.map, 1, 0))).toBe(false);
    expect(log.events.some((event) => event.type === 'elevationChanged')).toBe(true);
    expect(log.events.some((event) => event.type === 'cliffChanged')).toBe(true);
  });

  it('uses one forced-movement service for push, teleport and collision death', () => {
    const state = testState(makeLevel(['....'], {
      units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
      scenario: { zones: [{ id: 'portal', cells: [{ x: 2, y: 0 }] }] },
    }));
    const target = state.units[1];
    const log = collect();

    testScenarioEffect(state, {
      type: 'forceMove', selector: { ids: [target.id] }, mode: 'push', source: { x: 0, y: 0 }, distance: 1,
    }, log.emit);
    expect(target.x).toBe(2);

    testScenarioEffect(state, { type: 'teleportUnits', selector: { ids: [target.id] }, zone: 'portal' }, log.emit);
    expect(target.x).toBe(2);

    testScenarioEffect(state, {
      type: 'forceMove',
      selector: { ids: [target.id] },
      mode: 'push',
      source: { x: 1, y: 0 },
      distance: 3,
      collisionDamage: 999,
    }, log.emit);
    expect(state.units.some((unit) => unit.id === target.id)).toBe(false);
    expect(state.markers.some((marker) => marker.fallenUnit?.id === target.id)).toBe(true);
    expect(log.events).toContainEqual(expect.objectContaining({ type: 'collisionDamage', killed: true }));
  });

  it('models pre-battle deployment as authoritative actions before turn one', () => {
    const state = testState(makeLevel(['....'], {
      units: [
        { ...u(0, 0, 'soldier', 1), key: 'left' },
        { ...u(1, 0, 'rogue', 1), key: 'right' },
        u(3, 0, 'soldier', 2),
      ],
      scenario: { zones: [{ id: 'blue-front', cells: [{ x: 0, y: 0 }, { x: 1, y: 0 }] }] },
      deployment: { order: [1], zones: [{ player: 1, zone: 'blue-front' }] },
    }));
    expect(state.phase).toBe('deployment');
    expect(() => testApply(state, { kind: 'endTurn' })).toThrow(/部署/);

    const events = testApply(state, { kind: 'deployUnit', unit: state.units[0].id, at: { x: 1, y: 0 } });
    expect(state.units.find((unit) => unit.key === 'left')?.x).toBe(1);
    expect(state.units.find((unit) => unit.key === 'right')?.x).toBe(0);
    expect(events[0]?.type).toBe('unitDeployed');

    const started = testApply(state, { kind: 'finishDeployment' });
    expect(state.phase).toBe('playing');
    expect(state.turn).toBe(1);
    expect(started.some((event) => event.type === 'battleStarted')).toBe(true);
  });

  it('offers the deploying side exactly the placements the deploy action accepts', () => {
    // `^` is mountain: a foot soldier holds it, a mounted knight cannot.
    const state = testState(makeLevel(['..^.'], {
      units: [
        { ...u(0, 0, 'soldier', 1), key: 'foot' },
        { ...u(1, 0, 'knight', 1), key: 'horse' },
        u(3, 0, 'soldier', 2),
      ],
      scenario: {
        zones: [{ id: 'blue-left', cells: [{ x: 0, y: 0 }, { x: 1, y: 0 }] },
          { id: 'blue-ridge', cells: [{ x: 2, y: 0 }] }],
      },
      deployment: {
        order: [1],
        zones: [
          { player: 1, zone: 'blue-left', unitKeys: ['horse'] },
          { player: 1, zone: 'blue-ridge', unitKeys: ['foot'] },
        ],
      },
    }));
    const foot = state.units.find((unit) => unit.key === 'foot')!;
    const horse = state.units.find((unit) => unit.key === 'horse')!;

    const roster = TEST_ENGINE.deploymentRoster(state)!;
    expect(roster.player).toBe(1);
    // Both zones this player was given, and both units across them.
    expect(roster.units.map((unit) => unit.key).sort()).toEqual(['foot', 'horse']);
    expect(roster.zone).toHaveLength(3);

    // A unit is confined to its own zone, not to the roster's union.
    expect(TEST_ENGINE.deploymentSpots(state, foot).map((spot) => spot.at))
      .toEqual([{ x: 2, y: 0 }]);
    // The knight's zone is the flat pair, but taking x=0 would swap the soldier
    // into a cell the ridge assignment does not contain — so only its own is left.
    expect(TEST_ENGINE.deploymentSpots(state, horse).map((spot) => spot.at.x)).toEqual([1]);

    // Every cell of the board is either offered or refused, and the action must
    // agree with the menu on all of them — that is what one rule owner buys.
    for (const unit of [foot, horse]) {
      const offered = TEST_ENGINE.deploymentSpots(state, unit);
      for (let x = 0; x < 4; x++) {
        const at = { x, y: 0 };
        const listed = offered.some((spot) => spot.at.x === x);
        const probe = cloneState(state);
        const mine = probe.units.find((candidate) => candidate.key === unit.key)!;
        const attempt = () => testApply(probe, { kind: 'deployUnit', unit: mine.id, at });
        if (listed) expect(attempt).not.toThrow();
        else expect(attempt).toThrow();
      }
    }
  });

  it('refuses a deployment swap that would strand the other unit on ground it cannot hold', () => {
    const state = testState(makeLevel(['^..'], {
      units: [
        { ...u(0, 0, 'soldier', 1), key: 'foot' },
        { ...u(1, 0, 'knight', 1), key: 'horse' },
        u(2, 0, 'soldier', 2),
      ],
      scenario: { zones: [{ id: 'blue', cells: [{ x: 0, y: 0 }, { x: 1, y: 0 }] }] },
      deployment: { order: [1], zones: [{ player: 1, zone: 'blue' }] },
    }));
    const foot = state.units.find((unit) => unit.key === 'foot')!;

    // The soldier holds the mountain and wants the knight's cell. Placement was
    // always checked for the unit moving; the unit displaced by the swap was not,
    // so this used to put a horse on a mountain it can never leave.
    expect(TEST_ENGINE.deploymentSpots(state, foot).map((spot) => spot.at))
      .toEqual([{ x: 0, y: 0 }]);
    expect(() => testApply(state, { kind: 'deployUnit', unit: foot.id, at: { x: 1, y: 0 } }))
      .toThrow(/腾出的格子/);
    expect(foot.x).toBe(0);
  });
});

describe('instance-isolated content and ability-aware AI', () => {
  it('resolves every runtime definition through the engine content catalog', () => {
    const low = createTestCatalog();
    const high = createTestCatalog();
    const globalPower = TEST_CONTENT.weapons.get('soldier_sword').power;
    low.weapons.override('soldier_sword', { power: 5 });
    high.weapons.override('soldier_sword', { power: 90 });
    low.units.get('soldier').tags.push('low-sandbox-only');
    const lowEngine = createBattleEngine({ content: low });
    const highEngine = createBattleEngine({ content: high });
    const level = makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] });
    const lowState = lowEngine.createState(level);
    const highState = highEngine.createState(level);

    expect(lowEngine.forecast(lowState, lowState.units[0], lowState.units[1]).strike.base).toBe(5);
    expect(highEngine.forecast(highState, highState.units[0], highState.units[1]).strike.base).toBe(90);
    expect(TEST_CONTENT.weapons.get('soldier_sword').power).toBe(globalPower);
    expect(TEST_CONTENT.units.get('soldier').tags).not.toContain('low-sandbox-only');
    expect('attack' in TEST_CONTENT.units.get('soldier')).toBe(false);
  });

  it('enumerates isolated weapons and progression without falling back to global content', () => {
    const content = createTestCatalog();
    const baseWeapon = content.weapons.get('soldier_sword');
    const sandboxWeapon = { ...structuredClone(baseWeapon), id: 'sandbox_sword', name: 'Sandbox Sword' };
    content.weapons.define(sandboxWeapon);
    content.units.override('soldier', { weapons: [sandboxWeapon.id] });
    content.careers.define(defineCareer({
      id: 'sandbox-cleric',
      name: 'Sandbox Cleric',
      unitType: 'cleric',
    }));
    const engine = createBattleEngine({ content });
    const state = engine.createState(makeLevel(['.....'], {
      units: [
        { ...u(0, 0, 'soldier', 1), key: 'fighter' },
        u(1, 0, 'soldier', 2),
        { ...u(3, 0, 'cleric', 1), key: 'healer', career: 'sandbox-cleric' },
        { ...u(4, 0, 'soldier', 1, 40), key: 'patient' },
      ],
    }));
    const fighter = state.units.find((unit) => unit.key === 'fighter')!;
    const healer = state.units.find((unit) => unit.key === 'healer')!;
    const patient = state.units.find((unit) => unit.key === 'patient')!;

    expect(engine.commandsAt(state, fighter, fighter).map((option) => option.weapon)).toContain(sandboxWeapon.id);
    engine.dispatch(state, {
      kind: 'command',
      unit: healer.id,
      path: [{ x: healer.x, y: healer.y }],
      command: { ability: 'heal', target: { x: patient.x, y: patient.y } },
    });
    expect(healer.career.mastery['sandbox-cleric']).toBeGreaterThan(0);
  });

  it('applies data-defined forced movement through an isolated weapon catalog', () => {
    const content = createTestCatalog();
    const sword = content.weapons.get('soldier_sword');
    content.weapons.override(sword.id, {
      hitEffects: [...sword.hitEffects, { type: 'forcedMove', mode: 'push', distance: 1 }],
    });
    const engine = createBattleEngine({ content });
    const state = engine.createState(makeLevel(['....'], {
      units: [u(0, 0, 'soldier', 1), u(1, 0, 'ogre', 2)],
    }));

    engine.dispatch(state, {
      kind: 'command', unit: state.units[0].id, path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', weapon: sword.id, target: { x: 1, y: 0 } },
    });
    expect(state.units.find((unit) => unit.owner === 2)?.x).toBe(2);
  });

  it('requires an explicit AI evaluator for a custom ability and then uses it', () => {
    const rally = defineAbility({
      id: 'test-rally',
      name: 'Rally',
      priority: 1,
      tags: ['support'],
      execute: (_rules, { state }, _target, emit) => {
        state.scenario.variables.rallied = true;
        emit({ type: 'scenarioSignal', signal: 'rallied' });
      },
    });
    const abilities = Abilities.clone();
    abilities.define(rally);
    const evaluators = DefaultAbilityAiEvaluators.clone().register({
      ability: rally.id,
      score: () => 100_000,
    });
    const engine = createBattleEngine({ content: TEST_CONTENT, abilities, abilityAiEvaluators: evaluators });
    const state = engine.createState(makeLevel(['...'], {
      units: [u(0, 0, 'soldier', 1), u(2, 0, 'soldier', 2)],
    }));
    state.units[0].learnedAbilities.push(rally.id);

    const action = engine.chooseAiAction(state);
    expect(action).toMatchObject({ kind: 'command', command: { ability: rally.id } });
    engine.dispatch(state, action);
    expect(state.scenario.variables.rallied).toBe(true);
  });
});
