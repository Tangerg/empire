import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { isStrike, strikeCount } from '../combat';
import type { GameEvent } from '../types';
import { makeLevel, testApply, testDamage, testForecast, testState, u } from './fixtures';

describe('damage model', () => {
  it('is deterministic and matches the forecast exactly', () => {
    const s = testState(makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }));
    const [a, b] = s.units;
    const fc = testForecast(s, a, b);
    const events = testApply(s, {
      kind: 'command',
      unit: a.id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', target: { x: 1, y: 0 } },
    });
    const attack = events.find((e) => e.type === 'attack');
    expect(attack).toMatchObject({ damage: fc.strike.damage });
    expect(b.hp).toBe(fc.defenderHpAfter);
    expect(a.hp).toBe(fc.attackerHpAfter);
  });

  it('applies terrain defense to the defender', () => {
    const s = testState(
      makeLevel(['.^'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    const flat = testState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    const onMountain = testDamage(s, s.units[0], s.units[1]);
    const onPlain = testDamage(flat, flat.units[0], flat.units[1]);
    expect(onMountain.factorOf('defense.terrain', 0)).toBeCloseTo(0.4);
    expect(onMountain.damage).toBeLessThan(onPlain.damage);
  });

  it('scales attacker output with its remaining HP', () => {
    const s = testState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1, 50), u(1, 0, 'soldier', 2)] }),
    );
    const full = testState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    expect(testDamage(s, s.units[0], s.units[1]).damage).toBeLessThan(
      testDamage(full, full.units[0], full.units[1]).damage,
    );
  });

  it('combines the generic damage-type chart with weapon-specific unit identity', () => {
    const vsDragon = testState(
      makeLevel(['..'], { units: [u(0, 0, 'archer', 1), u(1, 0, 'dragon', 2)] }),
    );
    const antiAir = testDamage(vsDragon, vsDragon.units[0], vsDragon.units[1]);
    expect(antiAir.factorOf('matchup.effectiveness')).toBeCloseTo(1.1);
    expect(antiAir.familyFactor('weapon.target-tag.')).toBeCloseTo(1.4);
    expect(antiAir.familyLabels('weapon.target-tag.')).toEqual(['弓箭对空']);

    const vsOgre = testState(
      makeLevel(['..'], { units: [u(0, 0, 'cleric', 1), u(1, 0, 'ogre', 2)] }),
    );
    expect(testDamage(vsOgre, vsOgre.units[0], vsOgre.units[1]).factorOf('matchup.effectiveness')).toBeCloseTo(1.3);
  });

  it('emits an attack before the death it causes', () => {
    const s = testState(
      makeLevel(['..'], { units: [u(0, 0, 'knight', 1), u(1, 0, 'mage', 2, 5)] }),
    );
    const events = testApply(s, {
      kind: 'command',
      unit: s.units[0].id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', target: { x: 1, y: 0 } },
    });
    const attackIndex = events.findIndex((event) => event.type === 'attack');
    const deathIndex = events.findIndex((event) => event.type === 'death');
    expect(attackIndex).toBeGreaterThanOrEqual(0);
    expect(deathIndex).toBeGreaterThan(attackIndex);
  });

  it('emits a counter before the death it causes', () => {
    const s = testState(
      makeLevel(['..'], { units: [u(0, 0, 'mage', 1, 5), u(1, 0, 'knight', 2)] }),
    );
    const events = testApply(s, {
      kind: 'command',
      unit: s.units[0].id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', target: { x: 1, y: 0 } },
    });
    const counterIndex = events.findIndex((event) => event.type === 'counter');
    const deathIndex = events.findIndex((event) => event.type === 'death' && event.unit === 1);
    expect(counterIndex).toBeGreaterThanOrEqual(0);
    expect(deathIndex).toBeGreaterThan(counterIndex);
  });
});

describe('retaliation', () => {
  it('happens when the defender can reach back', () => {
    const s = testState(makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }));
    expect(testForecast(s, s.units[0], s.units[1]).counter).not.toBeNull();
  });

  it('does not happen when the archer shoots from range 2', () => {
    const s = testState(
      makeLevel(['...'], { units: [u(0, 0, 'archer', 1), u(2, 0, 'soldier', 2)] }),
    );
    expect(testForecast(s, s.units[0], s.units[1]).counter).toBeNull();
  });

  it('does happen when the archer is adjacent', () => {
    const s = testState(
      makeLevel(['...'], { units: [u(1, 0, 'archer', 1), u(2, 0, 'soldier', 2)] }),
    );
    expect(testForecast(s, s.units[0], s.units[1]).counter).not.toBeNull();
  });

  it('never happens against siege at minimum range 2', () => {
    const s = testState(
      makeLevel(['...'], { units: [u(0, 0, 'ballista', 1), u(2, 0, 'knight', 2)] }),
    );
    expect(testForecast(s, s.units[0], s.units[1]).counter).toBeNull();
  });

  it('is skipped when the defender dies', () => {
    const s = testState(
      makeLevel(['..'], { units: [u(0, 0, 'knight', 1), u(1, 0, 'mage', 2, 10)] }),
    );
    const fc = testForecast(s, s.units[0], s.units[1]);
    expect(fc.defenderDies).toBe(true);
    expect(fc.counter).toBeNull();
  });
});

describe('how much fighting happened', () => {
  it('counts every event where a blow landed, not a list of three names', () => {
    // The campaign shell counted `attack`, `areaAttack` and `counter`, so a
    // battle decided by reaction fire or by knocking a gate down reported almost
    // no combat. Recognising a strike by its payload closes that permanently:
    // `attacker` and `damage` occur together in exactly the strike events, and
    // in nothing else the engine emits.
    const strikes: GameEvent[] = [
      { type: 'attack', attacker: 1, defender: 2, weapon: 'w', damage: 10, killed: false },
      { type: 'counter', attacker: 2, defender: 1, weapon: 'w', damage: 4, killed: false },
      { type: 'supportAttack', attacker: 3, defender: 2, weapon: 'w', damage: 6, killed: false },
      { type: 'partingShot', attacker: 4, defender: 1, weapon: 'w', at: { x: 0, y: 0 }, damage: 3, killed: false },
      { type: 'attackStructure', attacker: 1, structure: 'gate', weapon: 'w', damage: 20, destroyed: false },
    ];
    const quiet: GameEvent[] = [
      { type: 'turnEnd', player: 1 },
      { type: 'heal', source: 1, target: 2, amount: 10 },
      { type: 'scenarioSignal', signal: 'gate.open' },
    ];

    expect(strikes.every(isStrike)).toBe(true);
    expect(quiet.some(isStrike)).toBe(false);
    expect(strikeCount([...strikes, ...quiet])).toBe(strikes.length);

    // And the claim the predicate rests on, read off the event map itself: out
    // of sixty-seven kinds, `attacker` and `damage` occur together in exactly
    // these seven. If a kind ever carries both without being a strike, this
    // fails and the predicate needs rethinking rather than a quiet exception.
    const map = readFileSync(join(import.meta.dirname, '..', 'types.ts'), 'utf8');
    const body = /export interface GameEventKindMap \{([\s\S]*?)\n\}/.exec(map)![1];
    const kinds = [...body.matchAll(/^ {2}(\w+): \{([^}]*)\}/gm)];
    expect(kinds.length).toBeGreaterThan(60);
    const both = kinds
      .filter(([, , fields]) => /\battacker\b/.test(fields) && /\bdamage\b/.test(fields))
      .map(([, kind]) => kind)
      .sort();
    expect(both).toEqual([
      'areaAttack', 'areaAttackStructure', 'attack', 'attackStructure',
      'counter', 'partingShot', 'supportAttack',
    ]);
  });
});
