import { IllegalActionError } from './domain/errors';
import { UnitEntity } from './domain/unit-entity';
import { player } from './state';
import { playerResource, type BattleResourceSystem } from './resources';
import type {
  AbilityId,
  CareerDef,
  CareerId,
  GameEvent,
  GameState,
  ResourceAccounts,
  Unit,
  UnitWeaponState,
} from './types';
import { type ContentCatalog } from './content-pack';

export interface CareerOption {
  career: CareerDef;
  eligible: boolean;
  unlocked: boolean;
  mastered: boolean;
  reasons: string[];
}

export function unitAbilityIds(unit: Unit, content: ContentCatalog): AbilityId[] {
  return [...new Set([...content.units.get(unit.type).abilities, ...unit.learnedAbilities])];
}

export function careerMastery(unit: Unit, career: CareerId): number {
  return Math.max(0, unit.career.mastery[career] ?? 0);
}

/**
 * Port declared by this module. The composition-level `BattleRuleServices`
 * satisfies it structurally, so neither side needs to import the other.
 */
export interface CareerRules {
  readonly content: ContentCatalog;
  readonly resources: BattleResourceSystem;
}

export function careerOptions(
  rules: CareerRules,
  state: GameState,
  unit: Unit,
): CareerOption[] {
  const { content, resources } = rules;
  const current = unit.career.current;
  const currentMastery = current ? careerMastery(unit, current) : 0;
  const owner = playerResource(player(state, unit.owner));
  const rootsOf = (career: CareerDef, visiting = new Set<string>()): Set<string> => {
    if (career.from.length === 0 || visiting.has(career.id)) return new Set([career.id]);
    visiting.add(career.id);
    const roots = new Set<string>();
    for (const predecessor of career.from) {
      if (!content.careers.has(predecessor)) continue;
      for (const root of rootsOf(content.careers.get(predecessor), new Set(visiting))) roots.add(root);
    }
    return roots;
  };
  const currentRoots = current && content.careers.has(current) ? rootsOf(content.careers.get(current)) : new Set<string>();
  return content.careers.all()
    .filter((career) => career.id !== current && (
      unit.career.unlocked.includes(career.id) ||
      (currentRoots.size === 0
        ? career.unitType === unit.type
        : [...rootsOf(career)].some((root) => currentRoots.has(root)))
    ))
    .map((career) => {
      const unlocked = unit.career.unlocked.includes(career.id);
      const reasons: string[] = [];
      if (!unlocked) {
        if (career.from.length === 0 && (current !== null || career.unitType !== unit.type)) {
          reasons.push('不能跨越到另一棵职业树的根职业');
        } else if (career.from.length > 0 && (!current || !career.from.includes(current))) {
          reasons.push('当前职业不在可转职路径上');
        }
        if (unit.rank < career.minimumRank) reasons.push(`需要军衔 ${career.minimumRank}`);
        if (currentMastery < career.minimumMastery) reasons.push(`需要当前职业熟练度 ${career.minimumMastery}`);
      }
      if (!resources.canAfford(career.costs, owner)) reasons.push('转职资源不足');
      return {
        career,
        eligible: reasons.length === 0,
        unlocked,
        mastered: careerMastery(unit, career.id) >= career.masteryThreshold,
        reasons,
      };
    })
    .sort((left, right) => left.career.tier - right.career.tier || left.career.id.localeCompare(right.career.id));
}

const cloneAccounts = (accounts: ResourceAccounts): ResourceAccounts =>
  Object.fromEntries(Object.entries(accounts).map(([id, account]) => [id, { ...account }]));

function initialWeaponState(unit: Unit, career: CareerDef, content: ContentCatalog): Record<string, UnitWeaponState> {
  const next = content.units.get(career.unitType);
  return Object.fromEntries(next.weapons.map((weaponId) => {
    const existing = unit.weaponState[weaponId];
    if (existing) return [weaponId, { ...existing, resources: cloneAccounts(existing.resources) }];
    const weapon = content.weapons.get(weaponId);
    return [weaponId, { cooldownRemaining: 0, resources: cloneAccounts(weapon.resources) }];
  }));
}

export function changeCareer(
  rules: CareerRules,
  state: GameState,
  unit: Unit,
  requested: CareerId,
  emit: (event: GameEvent) => void,
): void {
  const { content, resources } = rules;
  const option = careerOptions(rules, state, unit).find((entry) => entry.career.id === requested);
  if (!option) throw new IllegalActionError(`无法转为当前职业或未知职业「${requested}」`);
  if (!option.eligible) throw new IllegalActionError(option.reasons.join('；'));

  const career = option.career;
  const owner = player(state, unit.owner);
  const subject = playerResource(owner);
  const spent = resources.spendAll(career.costs, subject);
  for (const cost of spent) resources.announce(subject, cost.resource, -cost.amount, emit);

  const from = unit.career.current;
  const hpRatio = unit.hp / content.units.get(unit.type).maxHp;
  const nextDef = content.units.get(career.unitType);
  const weaponState = initialWeaponState(unit, career, content);
  const entity = new UnitEntity(unit);
  entity.changeCareer(career.id, career.unitType, weaponState);
  unit.hp = Math.max(1, Math.min(nextDef.maxHp, Math.round(nextDef.maxHp * hpRatio)));
  for (const [resource, account] of Object.entries(nextDef.resources)) {
    if (!unit.resources[resource]) unit.resources[resource] = { ...account };
  }
  if (!unit.career.unlocked.includes(career.id)) unit.career.unlocked.push(career.id);
  if (unit.career.mastery[career.id] === undefined) unit.career.mastery[career.id] = 0;
  entity.finishAction();
  emit({ type: 'careerChanged', unit: unit.id, from, to: career.id, unitType: career.unitType });
}

export function awardCareerProgress(
  content: ContentCatalog,
  unit: Unit,
  amount: number,
  emit: (event: GameEvent) => void,
): void {
  const current = unit.career.current;
  if (!current || !content.careers.has(current)) return;
  const gained = Math.max(0, Math.round(amount));
  if (gained === 0) return;
  const definition = content.careers.get(current);
  const before = careerMastery(unit, current);
  const after = before + gained;
  unit.career.mastery[current] = after;
  emit({ type: 'careerProgressChanged', unit: unit.id, career: current, amount: gained, current: after });
  if (before < definition.masteryThreshold && after >= definition.masteryThreshold) {
    const learned = definition.masteryAbilities.filter((ability) => !unit.learnedAbilities.includes(ability));
    unit.learnedAbilities.push(...learned);
    emit({ type: 'careerMastered', unit: unit.id, career: current, abilities: learned });
  }
}
