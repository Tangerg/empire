import { describe, expect, it } from 'vitest';
import { BattleEngineConfigurationError, BattleLevelError } from '../engine';
import { createBattleEngine } from '../plugins/default';
import { DefaultRuleReferenceChecks, type RuleReferenceCheck } from '../rule-references';
import { UnitDirectives } from '../unit-directive';
import { ScenarioConditionHandlers } from '../scenario';
import { cloneContentCatalog } from '../content-pack';
import { TEST_CONTENT, makeLevel, u } from './fixtures';
import type { LevelData } from '../types';

/**
 * Does this ruleset implement every rule the content and the level name?
 *
 * Three of the extension points were cross-checked by hand in the engine's
 * constructor, three more by a traversal beside it, and every point added since
 * by nobody — so a level naming an unregistered blast shape failed mid-battle as
 * a registry lookup rather than as a bad level.
 */

const engine = () => createBattleEngine({ content: TEST_CONTENT });

const level = (patch: Partial<LevelData> = {}): LevelData => ({
  ...makeLevel(['...'], { units: [u(0, 0, 'soldier', 1), u(2, 0, 'soldier', 2)] }),
  ...patch,
});

describe('references a ruleset has to honour', () => {
  it('names the extension point, the name, and who wrote it down', () => {
    const broken = level();
    broken.units[0].directive = { mode: 'test.forage', waypoints: [], cursor: 0 };

    expect(() => engine().createState(broken)).toThrow(BattleLevelError);
    expect(() => engine().createState(broken)).toThrow(/未注册的常驻命令「test\.forage」/);
  });

  it('checks every point a level can name, not the three it used to', () => {
    const reject = (patch: (data: LevelData) => void, message: RegExp) => {
      const broken = level();
      patch(broken);
      expect(() => engine().createState(broken)).toThrow(message);
    };

    reject((data) => { data.units[0].reaction = 'test.parry'; }, /反应姿态「test\.parry」/);
    reject((data) => { data.rules = { turnOrder: 'test.initiative' }; }, /行动顺序策略「test\.initiative」/);
    reject((data) => { data.units[0].learnedAbilities = ['test.rally']; }, /能力「test\.rally」/);
    reject((data) => {
      data.scenario = {
        zones: [],
        triggers: [{
          id: 'first',
          timing: 'turnStart',
          condition: { type: 'test.omen' } as never,
          effects: [],
        }],
      };
    }, /场景条件「test\.omen」/);
  });

  it('refuses a catalog whose weapon names a blast shape nobody registered', () => {
    const content = cloneContentCatalog(TEST_CONTENT);
    content.weapons.override('soldier_sword', { area: 'test.cone' });

    expect(() => createBattleEngine({ content })).toThrow(BattleEngineConfigurationError);
    expect(() => createBattleEngine({ content })).toThrow(/未注册的爆炸形状「test\.cone」/);
  });

  it('walks a compound condition through the registry, not a hardcoded name list', () => {
    const nested = level();
    nested.scenario = {
      zones: [],
      triggers: [{
        id: 'first',
        timing: 'turnStart',
        condition: {
          type: 'all',
          conditions: [{ type: 'not', condition: { type: 'test.omen' } as never }],
        },
        effects: [],
      }],
    };

    expect(() => engine().createState(nested)).toThrow(/场景条件「test\.omen」/);
    // The children come from the handler, so a rule pack's own compound kind is
    // walked too — the two hardcoded traversals could only ever see three.
    expect(ScenarioConditionHandlers.children({ type: 'not', condition: { type: 'turnAtLeast', turn: 2 } }))
      .toEqual([{ type: 'turnAtLeast', turn: 2 }]);
  });

  it('lets a rule pack guard its own extension point', () => {
    const directives = UnitDirectives.clone();
    directives.define({ id: 'forage', pull: () => 0, engagement: 1, fightPenalty: 0 });
    const smell: RuleReferenceCheck = {
      id: 'test.no-foraging-heroes',
      subject: '可觅食命令',
      known: () => ['forage'],
      inLevel: (data) => data.units
        .filter((unit) => unit.key?.startsWith('hero'))
        .map((unit) => ({ by: `英雄 ${unit.key}`, name: 'scavenge' })),
    };
    const referenceChecks = DefaultRuleReferenceChecks.clone().register(smell);
    const heroic = level();
    heroic.units[0].key = 'hero-1';

    expect(() => createBattleEngine({ content: TEST_CONTENT, directives, referenceChecks })
      .createState(heroic)).toThrow(/英雄 hero-1 引用了未注册的可觅食命令「scavenge」/);
    // And the ruleset without that pack is untouched.
    expect(() => engine().createState(heroic)).not.toThrow();
  });
});
