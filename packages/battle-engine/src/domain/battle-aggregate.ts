import { idx } from '../grid';
import type { BattlefieldMarker, Coord, GameState, PlayerId, StructureId, Unit } from '../types';
import { DomainInvariantError } from './errors';
import { PlayerEntity } from './player-entity';
import { StructureEntity } from './structure-entity';
import { UnitEntity } from './unit-entity';
import { GlobalContentCatalog, type ContentCatalog } from '../content-pack';
import { cloneUnitState } from '../unit-state';
import { extractLostTransportPassengers } from '../transports';

/** Aggregate root for every mutation that changes battle ownership or life. */
export class BattleAggregate {
  constructor(
    readonly state: GameState,
    readonly content: ContentCatalog = GlobalContentCatalog,
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
    const index = idx(this.state.map, at.x, at.y);
    this.state.map.captureProgress[index] = 0;
  }

  moveUnit(id: number, destination: Coord): UnitEntity {
    const unit = this.unit(id);
    this.clearCaptureAt(unit.position);
    unit.moveTo(destination);
    return unit;
  }

  damageUnit(id: number, requested: number): {
    amount: number;
    hpAfter: number;
    killed: boolean;
    at: Coord;
    marker: BattlefieldMarker | null;
    passengerMarkers: BattlefieldMarker[];
  } {
    const unit = this.unit(id);
    const at = unit.position;
    const result = unit.takeDamage(requested);
    let marker: BattlefieldMarker | null = null;
    let passengerMarkers: BattlefieldMarker[] = [];
    if (result.killed) {
      this.clearCaptureAt(at);
      marker = this.createCorpse(unit.state);
      this.removeUnit(id);
      passengerMarkers = extractLostTransportPassengers(this.state, id, at);
    }
    return { ...result, at, marker, passengerMarkers };
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
