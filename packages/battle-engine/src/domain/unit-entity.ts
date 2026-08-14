import type { CareerId, CommanderId, Coord, Direction, FormationId, PlayerId, ReactionStance, Unit, UnitDirectiveState, UnitTypeId, UnitWeaponState, WeaponDef, WeaponId } from '../types';
import { DomainInvariantError } from './errors';

/**
 * Rich runtime façade over serialisable Unit state.
 *
 * The data object remains the save/replay format. All state-changing business
 * rules should go through this façade so callers cannot forget clamping,
 * resource consumption, or lifecycle invariants.
 */
export class UnitEntity {
  constructor(readonly state: Unit) {}

  get id(): number {
    return this.state.id;
  }

  get owner(): PlayerId {
    return this.state.owner;
  }

  get position(): Coord {
    return { x: this.state.x, y: this.state.y };
  }

  isOwnedBy(owner: PlayerId): boolean {
    return this.state.owner === owner;
  }

  /**
   * Has this unit spent its action this turn?
   *
   * Deliberately *not* a `canAct(player, phase)` predicate: whether a unit is
   * entitled to act depends on the battle's turn-order policy, which a single
   * unit cannot see. Answering that here produced a plausible-looking method
   * that was silently wrong under any per-unit ordering — see `mayAct`.
   */
  get hasActed(): boolean {
    return this.state.done;
  }

  moveTo(destination: Coord): void {
    this.state.x = destination.x;
    this.state.y = destination.y;
    this.clearCapture();
  }

  /**
   * Capture progress belongs to the tile you are standing on, so stopping
   * standing on it voids the progress — whether you walked, were deployed
   * elsewhere, or boarded a transport.
   */
  clearCapture(): void {
    this.state.capture = 0;
  }

  /**
   * Health, in a change that is neither a wound nor a cure: a career that
   * raises the maximum, a scenario that puts a unit back at a stated strength.
   * The upper bound is the caller's, because a unit's maximum lives in the
   * catalog and an entity cannot see it.
   */
  restoreHealth(hp: number): void {
    this.state.hp = Math.max(1, Math.round(hp));
  }

  /** Keeps the same fraction of health across a change of maximum. */
  rescaleHealth(fromMaximum: number, toMaximum: number): void {
    if (fromMaximum < 1 || toMaximum < 1) {
      throw new DomainInvariantError('health maxima must be positive');
    }
    const scaled = Math.round(toMaximum * (this.state.hp / fromMaximum));
    this.state.hp = Math.max(1, Math.min(toMaximum, scaled));
  }

  /** Morale, clamped to this unit's own band. Returns what actually changed. */
  changeMorale(delta: number): number {
    const before = this.state.morale.current;
    this.state.morale.current = Math.max(0, Math.min(this.state.morale.maximum, before + delta));
    return this.state.morale.current - before;
  }

  /** Back in the fight — leaving the field ended the panic, not just the poison. */
  restoreMorale(): void {
    this.state.morale.current = this.state.morale.maximum;
  }

  /** Whatever was clinging to this unit stopped when it left the field. */
  clearStatuses(): void {
    this.state.statuses = [];
  }

  changeCommander(commander: CommanderId | null): CommanderId | null {
    const previous = this.state.commanderId;
    this.state.commanderId = commander;
    return previous;
  }

  changeDirective(directive: UnitDirectiveState): UnitDirectiveState {
    const previous = this.state.directive;
    this.state.directive = directive;
    return previous;
  }

  finishAction(): void {
    this.state.done = true;
  }

  readyForTurn(): void {
    this.state.done = false;
  }

  changeOwner(owner: PlayerId): PlayerId {
    const previous = this.state.owner;
    this.state.owner = owner;
    this.state.done = true;
    return previous;
  }

  changeReaction(stance: ReactionStance): void {
    this.state.reaction = stance;
  }

  changeFacing(facing: Direction): Direction {
    const previous = this.state.facing;
    this.state.facing = facing;
    return previous;
  }

  changeFormation(formation: FormationId | null): FormationId | null {
    const previous = this.state.formation;
    this.state.formation = formation;
    return previous;
  }

  changeCareer(career: CareerId, unitType: UnitTypeId, weaponState: Record<WeaponId, UnitWeaponState>): CareerId | null {
    const previous = this.state.career.current;
    this.state.career.current = career;
    this.state.type = unitType;
    this.state.weaponState = weaponState;
    return previous;
  }

  /**
   * One reaction per round, whatever it is spent on — a riposte, an intercept,
   * a parting shot. Asked here rather than compared by hand at each site so a
   * new kind of reaction cannot quietly get a budget of its own.
   */
  canReact(round: number): boolean {
    return this.state.reactionUsedRound !== round;
  }

  consumeReaction(round: number): void {
    this.state.reactionUsedRound = round;
  }

  /** A unit that has just arrived has not reacted to anything yet. */
  restoreReaction(): void {
    this.state.reactionUsedRound = -1;
  }

  takeDamage(requested: number): { amount: number; hpAfter: number; killed: boolean } {
    if (!Number.isFinite(requested) || requested < 0) {
      throw new DomainInvariantError(`damage must be a finite non-negative number, got ${requested}`);
    }
    const before = this.state.hp;
    const amount = Math.min(before, Math.max(0, Math.round(requested)));
    this.state.hp = Math.max(0, before - amount);
    return { amount, hpAfter: this.state.hp, killed: this.state.hp <= 0 };
  }

  heal(requested: number, maxHp: number): number {
    if (!Number.isFinite(requested) || requested < 0 || maxHp < 1) {
      throw new DomainInvariantError('heal and maximum HP must be valid positive-domain values');
    }
    const amount = Math.min(Math.max(0, Math.round(requested)), Math.max(0, maxHp - this.state.hp));
    this.state.hp += amount;
    return amount;
  }

  weaponState(id: WeaponId) {
    const runtime = this.state.weaponState[id];
    if (!runtime) throw new DomainInvariantError(`unit ${this.id} has no weapon state "${id}"`);
    return runtime;
  }

  canUseWeapon(weapon: WeaponDef): boolean {
    const runtime = this.state.weaponState[weapon.id];
    return !!runtime && runtime.cooldownRemaining <= 0;
  }

  commitWeaponCooldown(weapon: WeaponDef): void {
    const runtime = this.weaponState(weapon.id);
    if (!this.canUseWeapon(weapon)) {
      throw new DomainInvariantError(`weapon "${weapon.id}" is not ready for unit ${this.id}`);
    }
    runtime.cooldownRemaining = Math.max(runtime.cooldownRemaining, weapon.cooldown);
  }

  advanceWeaponCooldowns(): void {
    for (const runtime of Object.values(this.state.weaponState)) {
      runtime.cooldownRemaining = Math.max(0, runtime.cooldownRemaining - 1);
    }
  }

  addRankProgress(amount: number): number {
    const gained = Math.max(0, Math.round(amount));
    this.state.rankProgress += gained;
    return gained;
  }

  changeRank(rank: Unit['rank']): Unit['rank'] {
    const previous = this.state.rank;
    this.state.rank = rank;
    return previous;
  }

}
