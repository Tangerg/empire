import { Registry } from '../registry';
import type {
  ArmorClass,
  ArmorClassDef,
  DamageMatchupDef,
  DamageType,
  DamageTypeDef,
} from '../types';

export const DamageTypes = new Registry<DamageTypeDef>('damage type');
export const ArmorClasses = new Registry<ArmorClassDef>('armor class');

const matchupKey = (damageType: DamageType, armorClass: ArmorClass): string =>
  `${damageType}\u0000${armorClass}`;

/** Content-owned matchup matrix. The combat pipeline only asks it for a multiplier. */
export class DamageMatchupRegistry {
  private readonly entries = new Map<string, DamageMatchupDef>();

  constructor(readonly neutralMultiplier = 1) {
    if (!Number.isFinite(neutralMultiplier) || neutralMultiplier <= 0) {
      throw new Error('neutral damage multiplier must be > 0');
    }
  }

  register(definitions: readonly DamageMatchupDef[]): void {
    const seen = new Set<string>();
    for (const definition of definitions) {
      if (!Number.isFinite(definition.multiplier) || definition.multiplier <= 0) {
        throw new Error(
          `damage matchup "${definition.damageType}" -> "${definition.armorClass}" must be > 0`,
        );
      }
      const key = matchupKey(definition.damageType, definition.armorClass);
      if (this.entries.has(key) || seen.has(key)) {
        throw new Error(
          `damage matchup already registered: "${definition.damageType}" -> "${definition.armorClass}"`,
        );
      }
      seen.add(key);
    }
    for (const definition of definitions) {
      this.entries.set(matchupKey(definition.damageType, definition.armorClass), { ...definition });
    }
  }

  effectiveness(damageType: DamageType, armorClass: ArmorClass): number {
    return this.entries.get(matchupKey(damageType, armorClass))?.multiplier ?? this.neutralMultiplier;
  }

  has(damageType: DamageType, armorClass: ArmorClass): boolean {
    return this.entries.has(matchupKey(damageType, armorClass));
  }

  all(): DamageMatchupDef[] {
    return [...this.entries.values()].map((definition) => ({ ...definition }));
  }

  clone(): DamageMatchupRegistry {
    const copy = new DamageMatchupRegistry(this.neutralMultiplier);
    copy.register(this.all());
    return copy;
  }
}

export const DamageMatchups = new DamageMatchupRegistry();

export const damageTypeDef = (id: DamageType): DamageTypeDef => DamageTypes.get(id);
export const armorClassDef = (id: ArmorClass): ArmorClassDef => ArmorClasses.get(id);
export const damageEffectiveness = (damageType: DamageType, armorClass: ArmorClass): number =>
  DamageMatchups.effectiveness(damageType, armorClass);
