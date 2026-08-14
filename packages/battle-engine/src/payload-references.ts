import type { Coord, LevelCliffEdge, MarkerSelector, ScenarioCondition, UnitSelector } from './types';

/** One player's objective, named by the id that player declared it under. */
export interface PlayerObjectiveReference {
  readonly player: number;
  readonly id: string;
}

/**
 * What one rule payload points at, and what only that payload can complain about.
 *
 * A scenario effect, a trigger condition and an objective are all declarations
 * that name things: a zone, a structure, a status a catalog defines, a cell that
 * must be on the map. Who knew which names? The level linter did — two hundred
 * lines of `effect.type === '…'` in a module that owns none of those payloads,
 * with the same enumeration re-derived a third time to find which effects name a
 * standing order. A rule pack that declaration-merged its own kind had its
 * references checked by nobody, which is how a closed union grows back inside an
 * open registry.
 *
 * The handler that runs a payload is the one thing that knows what the payload
 * points at, so it says so by filling one of these in, and the caller decides
 * what "unknown" means: the level linter resolves them against one document and
 * its catalog, the ruleset's reference checks against what a ruleset implements.
 *
 * Both a builder and the record it builds. The setters ignore `undefined` so an
 * optional field cites nothing without the handler branching on it.
 */
export class PayloadReferences {
  readonly zones: string[] = [];
  readonly players: number[] = [];
  readonly structures: string[] = [];
  readonly composites: string[] = [];
  readonly objectives: PlayerObjectiveReference[] = [];
  readonly statuses: string[] = [];
  readonly terrains: string[] = [];
  readonly overlays: string[] = [];
  readonly unitTypes: string[] = [];
  readonly directives: string[] = [];
  readonly cells: Coord[] = [];
  readonly edges: LevelCliffEdge[] = [];
  readonly conditions: ScenarioCondition[] = [];
  readonly faults: string[] = [];

  /** A scenario zone, declared by the level. */
  zone(id: string | undefined): this {
    return this.add(this.zones, id);
  }

  player(id: number | undefined): this {
    return this.add(this.players, id);
  }

  structure(id: string | undefined): this {
    return this.add(this.structures, id);
  }

  composite(id: string | undefined): this {
    return this.add(this.composites, id);
  }

  objective(player: number, id: string): this {
    this.objectives.push({ player, id });
    return this;
  }

  /** A content id: a status the catalog must define. */
  status(id: string | undefined): this {
    return this.add(this.statuses, id);
  }

  terrain(id: string | undefined): this {
    return this.add(this.terrains, id);
  }

  overlay(id: string | undefined): this {
    return this.add(this.overlays, id);
  }

  unitType(id: string | undefined): this {
    return this.add(this.unitTypes, id);
  }

  /** A rule id: a standing order the composed ruleset must implement. */
  directive(id: string | undefined): this {
    return this.add(this.directives, id);
  }

  /** A cell that must be on the map. */
  cell(at: Coord | undefined): this {
    return this.add(this.cells, at);
  }

  /** Two cells that must be neighbours under the board's tiling. */
  edge(edge: LevelCliffEdge): this {
    this.edges.push(edge);
    return this;
  }

  /** A condition this payload embeds, to be inspected in its own right. */
  condition(condition: ScenarioCondition): this {
    this.conditions.push(condition);
    return this;
  }

  /**
   * Something only this payload's own rules can judge, in its own words.
   *
   * A caller prefixes whose payload it was; the sentence itself belongs to the
   * handler, the same way a repeat schedule states its own faults.
   */
  fault(message: string): this {
    this.faults.push(message);
    return this;
  }

  /**
   * The zone a unit or marker selector filters by, when it filters by one.
   *
   * Not its `owner`: a selector may legitimately aim at neutral side 0, which no
   * level declares as a player, so an owner in a selector is not a reference to
   * a declared name.
   */
  selector(selector: UnitSelector | MarkerSelector): this {
    return this.zone(selector.zone);
  }

  private add<T>(into: T[], value: T | undefined): this {
    if (value !== undefined) into.push(value);
    return this;
  }
}
