import { MapLayers } from './map-layers';
import type { BattlefieldMarker, Coord, GameState, PlayerId, StructureId, Unit } from '../types';
import type { UnitFall } from './unit-fall';
import { DomainInvariantError } from './errors';
import { PlayerEntity } from './player-entity';
import { StructureEntity } from './structure-entity';
import { UnitEntity } from './unit-entity';
import { type ContentCatalog } from '../content-pack';
import { cloneUnitState } from '../unit-state';
import { extractLostTransportPassengers } from '../transports';

export interface UnitDamageResult {
  amount: number;
  hpAfter: number;
  killed: boolean;
  at: Coord;
  /** Present exactly when the damage was lethal. */
  fall: UnitFall | null;
}

/** Aggregate root for every mutation that changes battle ownership or life. */
export class BattleAggregate {
  constructor(
    readonly state: GameState,
    readonly content: ContentCatalog,
  ) {}

  unit(id: number): UnitEntity {
    const state = this.state.units.find((candidate) => candidate.id === id);
    if (!state) throw new DomainInvariantError(`unknown unit ${id}`);
    return new UnitEntity(state);
  }

  findUnit(id: number): UnitEntity | null {
    const state = this.state.units.find((candidate) => candidate.id === id);
    return state ? new UnitEntity(state) : null;
  }

  player(id: PlayerId): PlayerEntity {
    const state = this.state.players.find((candidate) => candidate.id === id);
    if (!state) throw new DomainInvariantError(`unknown player ${id}`);
    return new PlayerEntity(state);
  }

  structure(id: StructureId): StructureEntity {
    const state = this.state.structures.find((candidate) => candidate.id === id);
    if (!state) throw new DomainInvariantError(`unknown structure "${id}"`);
    return new StructureEntity(state, this.content.structures.get(state.type));
  }

  clearCaptureAt(at: Coord): void {
    new MapLayers(this.state.map).changeCaptureProgress(at, 0);
  }

  moveUnit(id: number, destination: Coord): UnitEntity {
    const unit = this.unit(id);
    this.clearCaptureAt(unit.position);
    unit.moveTo(destination);
    return unit;
  }

  /**
   * Applies damage and, when it is lethal, completes the removal in one step.
   *
   * `fall` is non-null exactly when `killed` — it is everything a caller needs
   * to report and resolve the death, so no caller has to reassemble it from
   * the battlefield the unit has already left.
   */
  damageUnit(id: number, requested: number): UnitDamageResult {
    const unit = this.unit(id);
    const at = unit.position;
    const result = unit.takeDamage(requested);
    if (!result.killed) return { ...result, at, fall: null };

    const casualty = cloneUnitState(unit.state);
    this.clearCaptureAt(at);
    const marker = this.createCorpse(unit.state);
    this.removeUnit(id);
    const passengerMarkers = extractLostTransportPassengers(this.state, id, at);
    return { ...result, at, fall: { unit: casualty, at, marker, passengerMarkers } };
  }

  createCorpse(unit: Unit): BattlefieldMarker {
    return this.createUnitMarker(unit, 'corpse');
  }

  createUnitMarker(
    unit: Unit,
    kind: string,
    meta: BattlefieldMarker['meta'] = {},
  ): BattlefieldMarker {
    const marker: BattlefieldMarker = {
      id: this.state.nextMarkerId++,
      kind,
      at: { x: unit.x, y: unit.y },
      owner: unit.owner,
      fallenUnit: cloneUnitState(unit),
      meta: { ...meta },
    };
    this.state.markers.push(marker);
    return marker;
  }

  removeUnit(id: number): void {
    const index = this.state.units.findIndex((candidate) => candidate.id === id);
    if (index < 0) return;
    this.clearCaptureAt(this.state.units[index]);
    this.state.units.splice(index, 1);
  }
}
