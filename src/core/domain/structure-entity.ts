import type { StructureDef, StructureState } from '../types';
import { DomainInvariantError } from './errors';

export class StructureEntity {
  constructor(
    readonly state: StructureState,
    readonly definition: StructureDef,
  ) {}

  takeRawDamage(requested: number): { amount: number; hpAfter: number; destroyed: boolean } {
    if (!Number.isFinite(requested) || requested < 0) {
      throw new DomainInvariantError(`structure damage must be non-negative, got ${requested}`);
    }
    if (this.state.hp <= 0) return { amount: 0, hpAfter: 0, destroyed: true };
    const amount = Math.min(
      this.state.hp,
      Math.max(1, Math.round(requested * (1 - this.definition.defense))),
    );
    this.state.hp -= amount;
    if (this.state.hp <= 0) this.state.disabled = true;
    return { amount, hpAfter: this.state.hp, destroyed: this.state.hp <= 0 };
  }

  repair(requested: number): number {
    if (!this.definition.repairable || requested <= 0) return 0;
    const amount = Math.min(
      Math.round(requested),
      Math.max(0, this.definition.maxHp - this.state.hp),
    );
    this.state.hp += amount;
    if (this.state.hp > 0) this.state.disabled = false;
    return amount;
  }
}
