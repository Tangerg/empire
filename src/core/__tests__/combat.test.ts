import { describe, expect, it } from 'vitest';
import { applyAction } from '../actions';
import { computeDamage, forecast } from '../combat';
import { createState } from '../state';
import { makeLevel, u } from './fixtures';

describe('damage model', () => {
  it('is deterministic and matches the forecast exactly', () => {
    const s = createState(makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }));
    const [a, b] = s.units;
    const fc = forecast(s, a, b);
    const events = applyAction(s, {
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
    const s = createState(
      makeLevel(['.^'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    const flat = createState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    const onMountain = computeDamage(s, s.units[0], s.units[1]);
    const onPlain = computeDamage(flat, flat.units[0], flat.units[1]);
    expect(onMountain.terrainDefense).toBeCloseTo(0.4);
    expect(onMountain.damage).toBeLessThan(onPlain.damage);
  });

  it('scales attacker output with its remaining HP', () => {
    const s = createState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1, 50), u(1, 0, 'soldier', 2)] }),
    );
    const full = createState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    expect(computeDamage(s, s.units[0], s.units[1]).damage).toBeLessThan(
      computeDamage(full, full.units[0], full.units[1]).damage,
    );
  });

  it('uses the damage-type chart: pierce shreds flyers, blunt beats armour', () => {
    const vsDragon = createState(
      makeLevel(['..'], { units: [u(0, 0, 'archer', 1), u(1, 0, 'dragon', 2)] }),
    );
    expect(computeDamage(vsDragon, vsDragon.units[0], vsDragon.units[1]).effectiveness).toBeCloseTo(1.4);

    const vsOgre = createState(
      makeLevel(['..'], { units: [u(0, 0, 'cleric', 1), u(1, 0, 'ogre', 2)] }),
    );
    expect(computeDamage(vsOgre, vsOgre.units[0], vsOgre.units[1]).effectiveness).toBeCloseTo(1.3);
  });
});

describe('retaliation', () => {
  it('happens when the defender can reach back', () => {
    const s = createState(makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }));
    expect(forecast(s, s.units[0], s.units[1]).counter).not.toBeNull();
  });

  it('does not happen when the archer shoots from range 2', () => {
    const s = createState(
      makeLevel(['...'], { units: [u(0, 0, 'archer', 1), u(2, 0, 'soldier', 2)] }),
    );
    expect(forecast(s, s.units[0], s.units[1]).counter).toBeNull();
  });

  it('does happen when the archer is adjacent', () => {
    const s = createState(
      makeLevel(['...'], { units: [u(1, 0, 'archer', 1), u(2, 0, 'soldier', 2)] }),
    );
    expect(forecast(s, s.units[0], s.units[1]).counter).not.toBeNull();
  });

  it('never happens against siege at minimum range 2', () => {
    const s = createState(
      makeLevel(['...'], { units: [u(0, 0, 'ballista', 1), u(2, 0, 'knight', 2)] }),
    );
    expect(forecast(s, s.units[0], s.units[1]).counter).toBeNull();
  });

  it('is skipped when the defender dies', () => {
    const s = createState(
      makeLevel(['..'], { units: [u(0, 0, 'knight', 1), u(1, 0, 'mage', 2, 10)] }),
    );
    const fc = forecast(s, s.units[0], s.units[1]);
    expect(fc.defenderDies).toBe(true);
    expect(fc.counter).toBeNull();
  });
});
