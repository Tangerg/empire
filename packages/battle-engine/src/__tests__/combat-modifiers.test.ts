import { describe, expect, it } from 'vitest';
import { Battlefield } from '../domain/battlefield';
import {
  CombatModifierPipeline,
  CombatModifierProviderRegistry,
  type CombatModifierProvider,
} from '../combat-modifiers';
import { createBattleEngine } from '../plugins/default';
import { GameSession } from '../session';
import { TEST_CONTENT, TEST_RULES, makeLevel, testBoard, testState, u } from './fixtures';

const provider = (
  id: string,
  priority: number,
  stage: 'power' | 'mitigation' | 'final',
  operation: 'add' | 'multiply',
  value: number,
): CombatModifierProvider => ({
  id,
  priority,
  provide: () => [{ id, label: id, source: 'extension', stage, operation, value }],
});

describe('combat modifier pipeline', () => {
  it('resolves ordered power, capped mitigation and final phases with a trace', () => {
    const registry = new CombatModifierProviderRegistry()
      .register(provider('final.half', 300, 'final', 'multiply', 0.5))
      .register(provider('power.double', 100, 'power', 'multiply', 2))
      .register(provider('mitigation.cover', 200, 'mitigation', 'add', 0.25));
    const pipeline = new CombatModifierPipeline(registry);
    const state = testState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    const result = pipeline.evaluate(100, {
      rules: TEST_RULES,
      board: testBoard(state),
      content: TEST_CONTENT,
      battlefield: new Battlefield(state, TEST_CONTENT),
      state,
      attacker: state.units[0],
      attackerAt: state.units[0],
      defender: state.units[1],
      defenderAt: state.units[1],
      weapon: TEST_CONTENT.weapons.get('soldier_sword'),
    });
    expect(result.damage).toBe(75);
    expect(result.modifiers.map((modifier) => modifier.id)).toEqual([
      'power.double',
      'mitigation.cover',
      'final.half',
    ]);
  });

  it('injects an isolated ruleset through BattleEngine and GameSession', () => {
    const pipeline = new CombatModifierPipeline(
      new CombatModifierProviderRegistry().register(
        provider('mode.double-damage', 1, 'power', 'multiply', 2),
      ),
    );
    const engine = createBattleEngine({ content: TEST_CONTENT, combatModifiers: pipeline });
    const session = new GameSession(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
      engine,
    );
    const result = session.forecast(session.state.units[0], session.state.units[1]);
    expect(result.strike.damage).toBe(76);
    expect(result.strike.modifiers).toContainEqual(
      expect.objectContaining({ id: 'mode.double-damage', source: 'extension' }),
    );

    const events = session.dispatch({
      kind: 'command',
      unit: session.state.units[0].id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', target: { x: 1, y: 0 } },
    });
    expect(events.find((event) => event.type === 'attack')).toMatchObject({ damage: 76 });
    expect(session.state.units.find((unit) => unit.owner === 2)?.hp).toBe(24);
  });
});
