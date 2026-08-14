import { KeyedRegistry, type ContentRegistry } from './registry';
import type { AbilityDef } from './abilities';
import type { WeaponHitEffectHandlerRegistry } from './hit-effects';
import type { ObjectiveHandlerRegistry } from './objective-system';
import type { ScenarioConditionHandlerRegistry, ScenarioEffectHandlerRegistry } from './scenario';
import type { ReactionBehavior } from './reactions';
import type { TurnOrderPolicy } from './turn-order';
import type { WeaponAreaShapeRegistry } from './weapon-area';
import type { UnitDirectiveBehavior } from './unit-directive';
import type { ContentCatalog } from './content-pack';
import { objectivesOf } from './level/declarations';
import type { LevelData, Objective, ScenarioCondition } from './types';

/**
 * Everything a reference check may consult.
 *
 * A consumer port, and open on purpose: a plugin that adds an extension point
 * declaration-merges the capability it needs here, then registers the check that
 * guards it. `BattleRuleServices` satisfies this structurally.
 */
export interface RuleReferenceRules {
  readonly content: ContentCatalog;
  readonly abilities: ContentRegistry<AbilityDef>;
  readonly hitEffects: WeaponHitEffectHandlerRegistry;
  readonly objectives: ObjectiveHandlerRegistry;
  readonly scenarioConditions: ScenarioConditionHandlerRegistry;
  readonly scenarioEffects: ScenarioEffectHandlerRegistry;
  readonly reactions: ContentRegistry<ReactionBehavior>;
  readonly turnOrders: ContentRegistry<TurnOrderPolicy>;
  readonly areaShapes: WeaponAreaShapeRegistry;
  readonly directives: ContentRegistry<UnitDirectiveBehavior>;
}

/** A name something wrote down, and who wrote it. */
export interface RuleReference {
  /** For the message: `weapon "mage_meteor"`, `unit "north-guard"`, `关卡规则`. */
  readonly by: string;
  readonly name: string;
}

/**
 * One extension point, and how to find the names written against it.
 *
 * Referential integrity between what content says and what a ruleset actually
 * implements had no owner. Three of the twenty-odd extension points were
 * cross-checked by hand in the engine's constructor, three more by a hand-written
 * traversal beside it, and every point added since — blast shapes, standing
 * orders, reaction stances, turn-order policies — by nobody at all. A level
 * naming an unregistered blast shape did not fail as a bad level; it failed
 * mid-battle, as a registry lookup, from wherever the rule happened to run.
 *
 * A check lives next to nothing and belongs to no phase: it says which names its
 * point knows, and where in a catalog or a level document names appear.
 */
export interface RuleReferenceCheck {
  readonly id: string;
  /** What the name points at, in the reader's language: 「爆炸形状」. */
  readonly subject: string;
  /** Names this ruleset implements. */
  known(rules: RuleReferenceRules): Iterable<string>;
  /** References the catalog makes. Checked once, when a ruleset is composed. */
  inContent?(content: ContentCatalog): Iterable<RuleReference>;
  /** References one level document makes. Checked when that level is loaded. */
  inLevel?(level: LevelData, rules: RuleReferenceRules): Iterable<RuleReference>;
}

export class RuleReferenceCheckRegistry extends KeyedRegistry<string, RuleReferenceCheck> {
  constructor() {
    super('rule reference check');
  }

  protected keyOf(check: RuleReferenceCheck): string {
    return check.id;
  }

  /** What the composed ruleset fails to implement for its own catalog. */
  contentIssues(rules: RuleReferenceRules): string[] {
    return this.issues(rules, (check) => check.inContent?.(rules.content));
  }

  /** What it fails to implement for one level written against it. */
  levelIssues(rules: RuleReferenceRules, level: LevelData): string[] {
    return this.issues(rules, (check) => check.inLevel?.(level, rules));
  }

  clone(): RuleReferenceCheckRegistry {
    return this.copyInto(new RuleReferenceCheckRegistry());
  }

  private issues(
    rules: RuleReferenceRules,
    referencesOf: (check: RuleReferenceCheck) => Iterable<RuleReference> | undefined,
  ): string[] {
    const found: string[] = [];
    for (const check of this.all()) {
      const references = referencesOf(check);
      if (!references) continue;
      const known = new Set(check.known(rules));
      for (const reference of references) {
        if (!known.has(reference.name)) {
          found.push(`${reference.by} 引用了未注册的${check.subject}「${reference.name}」`);
        }
      }
    }
    return [...new Set(found)];
  }
}

/** Every objective a level puts a player under, composites included. */
function objectiveTree(level: LevelData, rules: RuleReferenceRules): Objective[] {
  const all: Objective[] = [];
  const visit = (objective: Objective): void => {
    all.push(objective);
    // Stop at a kind nobody registered: its shape is unknown, so its children
    // are unknowable, and the missing kind is the finding worth reporting.
    if (!rules.objectives.has(objective.type)) return;
    for (const child of rules.objectives.children(objective)) visit(child);
  };
  for (const player of level.players) objectivesOf(level, player).forEach(visit);
  return all;
}

/** Every condition a level's triggers evaluate, nested ones included. */
function conditionTree(level: LevelData, rules: RuleReferenceRules): ScenarioCondition[] {
  const all: ScenarioCondition[] = [];
  const visit = (condition: ScenarioCondition): void => {
    all.push(condition);
    if (!rules.scenarioConditions.has(condition.type)) return;
    for (const child of rules.scenarioConditions.children(condition)) visit(child);
  };
  for (const trigger of level.scenario?.triggers ?? []) visit(trigger.condition);
  return all;
}

const levelUnits = (level: LevelData) =>
  level.units.map((unit) => ({ unit, by: `单位 ${unit.key ?? unit.unit}` }));

export const DefaultRuleReferenceChecks = new RuleReferenceCheckRegistry()
  .register({
    id: 'abilities',
    subject: '能力',
    known: (rules) => rules.abilities.ids(),
    inContent: (content) => [
      ...content.units.all().flatMap((unit) =>
        unit.abilities.map((name) => ({ by: `兵种 ${unit.id}`, name }))),
      ...content.careers.all().flatMap((career) =>
        career.masteryAbilities.map((name) => ({ by: `职业 ${career.id}`, name }))),
    ],
    inLevel: (level) => levelUnits(level).flatMap(({ unit, by }) =>
      (unit.learnedAbilities ?? []).map((name) => ({ by, name }))),
  })
  .register({
    id: 'hitEffects',
    subject: '命中效果',
    known: (rules) => rules.hitEffects.keys(),
    inContent: (content) => content.weapons.all().flatMap((weapon) =>
      weapon.hitEffects.map((effect) => ({ by: `武器 ${weapon.id}`, name: effect.type }))),
  })
  .register({
    id: 'areaShapes',
    subject: '爆炸形状',
    known: (rules) => rules.areaShapes.keys(),
    inContent: (content) => content.weapons.all().map((weapon) =>
      ({ by: `武器 ${weapon.id}`, name: weapon.area })),
  })
  .register({
    id: 'reactions',
    subject: '反应姿态',
    known: (rules) => rules.reactions.ids(),
    inContent: (content) => content.units.all().map((unit) =>
      ({ by: `兵种 ${unit.id}`, name: unit.defaultReaction })),
    inLevel: (level) => levelUnits(level).flatMap(({ unit, by }) =>
      unit.reaction ? [{ by, name: unit.reaction }] : []),
  })
  .register({
    id: 'directives',
    subject: '常驻命令',
    known: (rules) => rules.directives.ids(),
    inLevel: (level) => [
      ...levelUnits(level).flatMap(({ unit, by }) =>
        unit.directive ? [{ by, name: unit.directive.mode }] : []),
      ...(level.scenario?.triggers ?? []).flatMap((trigger) =>
        trigger.effects.flatMap((effect) => effect.type === 'setUnitDirective'
          ? [{ by: `触发器 ${trigger.id}`, name: effect.directive.mode }]
          : [])),
    ],
  })
  .register({
    id: 'turnOrders',
    subject: '行动顺序策略',
    known: (rules) => rules.turnOrders.ids(),
    inLevel: (level) => level.rules?.turnOrder
      ? [{ by: '关卡规则', name: level.rules.turnOrder }]
      : [],
  })
  .register({
    id: 'objectives',
    subject: '目标类型',
    known: (rules) => rules.objectives.keys(),
    inLevel: (level, rules) => objectiveTree(level, rules).map((objective) =>
      ({ by: `作战目标`, name: objective.type })),
  })
  .register({
    id: 'scenarioConditions',
    subject: '场景条件',
    known: (rules) => rules.scenarioConditions.keys(),
    inLevel: (level, rules) => conditionTree(level, rules).map((condition) =>
      ({ by: '触发条件', name: condition.type })),
  })
  .register({
    id: 'scenarioEffects',
    subject: '场景效果',
    known: (rules) => rules.scenarioEffects.keys(),
    inLevel: (level) => (level.scenario?.triggers ?? []).flatMap((trigger) =>
      trigger.effects.map((effect) => ({ by: `触发器 ${trigger.id}`, name: effect.type }))),
  });
