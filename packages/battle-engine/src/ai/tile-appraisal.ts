import { Battlefield, type BattlefieldCell } from '../domain/battlefield';
import { dist, idx } from '../grid';
import { enemyUnitsOf } from '../state';
import type { ContentCatalog } from '../content-pack';
import type { Coord, GameState, TerrainDef, Unit, UnitDef } from '../types';
import type { AiAgenda } from './agenda';
import { hpRatio, nearestDistance, preferredEngagementRange } from './measures';
import { directiveOf, directivePull, type UnitDirectiveRules } from '../unit-directive';
import type { AiOptions } from './rules';

/** What appraising a tile needs. `AiTurnContext` satisfies it structurally. */
export interface TileAppraisalContext {
  readonly state: GameState;
  /** The ruleset this appraisal is made under; standing orders are content. */
  readonly rules: UnitDirectiveRules;
  readonly agenda: AiAgenda;
  /** Damage the enemy could land per tile, read once for the whole turn. */
  readonly threat: Map<number, number>;
  readonly options: AiOptions;
  readonly battlefield: Battlefield;
  readonly content: ContentCatalog;
}

/**
 * What standing on one tile is worth, before deciding what to do from there.
 *
 * This was an eighty-line accumulator behind eight parameters — the same eight
 * the turn context already held, taken apart at the call site and passed back
 * one by one. Worse, every term was anonymous: a tile scored 214 and nothing
 * said whether that came from the ground, the mission, or fear of the archer
 * two tiles east, which made every balance question a bisect.
 *
 * Each consideration is now a named question with its own answer. They are
 * summed in the order they are asked, and that order is deliberate — it is the
 * order the hand-written accumulator used, so the arithmetic is unchanged.
 */
export class TileAppraisal {
  private readonly definition: UnitDef;
  private readonly terrain: TerrainDef;
  private readonly cell: BattlefieldCell;

  constructor(
    private readonly context: TileAppraisalContext,
    private readonly unit: Unit,
    private readonly at: Coord,
  ) {
    const { state, content } = context;
    this.definition = content.units.get(unit.type);
    this.terrain = content.terrains.get(state.map.tiles[this.tile]);
    this.cell = context.battlefield.cell(at);
  }

  get value(): number {
    let total = 0;
    total += this.directivePull();
    total += this.defensibleGround();
    total += this.highGround();
    total += this.captureProspect();
    total += this.missionPull();
    total += this.escortPull();
    total += this.engagementRange();
    total += this.coverFromNearestFoe();
    total += this.enemyKeepPull();
    total += this.homeDefencePull();
    total += this.exposure();
    total += this.recovery();
    total += this.commandLink();
    return total;
  }

  private get tile(): number {
    return idx(this.context.state.map, this.at.x, this.at.y);
  }

  private get agenda(): AiAgenda {
    return this.context.agenda;
  }

  private get aggression(): number {
    return this.context.options.aggression;
  }

  private get canCapture(): boolean {
    return this.definition.abilities.includes('capture');
  }

  /** How badly the mission wants this unit kept alive; 0 for everyone else. */
  private get protectedWeight(): number {
    return this.agenda.mission.protectedUnits.get(this.unit.id) ?? 0;
  }

  private foesCache: Unit[] | null = null;

  private get foes(): Unit[] {
    return this.foesCache ??= enemyUnitsOf(this.context.state, this.unit.owner);
  }

  /** A standing order outranks tactics: a guard that wanders is not guarding. */
  private directivePull(): number {
    return directivePull(this.context.rules, this.context.state, this.unit, this.at);
  }

  private defensibleGround(): number {
    return this.terrain.defense * 90;
  }

  private highGround(): number {
    return this.cell.elevation * 3;
  }

  /** Only a unit that can plant a flag is drawn by a flag worth planting. */
  private captureProspect(): number {
    if (!this.canCapture || this.agenda.captureTargets.length === 0) return 0;
    let best = 0;
    for (const target of this.agenda.captureTargets) {
      best = Math.max(best, (target.weight * 120) / (1 + dist(this.at, target.at)));
    }
    return best * (0.6 + 0.4 * this.aggression);
  }

  /** Where the formal objectives want somebody to be. */
  private missionPull(): number {
    let strongest = 0;
    for (const destination of this.agenda.mission.destinations) {
      if (destination.unitIds && !destination.unitIds.has(this.unit.id)) continue;
      if (destination.captureOnly && !this.canCapture) continue;
      strongest = Math.max(strongest, destination.weight * 180 / (1 + dist(this.at, destination.at)));
    }
    return strongest;
  }

  /** Escorts close on their charge. The charge itself is pulled by its own goal. */
  private escortPull(): number {
    const protectedUnits = this.agenda.mission.protectedUnits;
    if (this.protectedWeight !== 0 || protectedUnits.size === 0) return 0;
    let strongest = 0;
    for (const [protectedId, weight] of protectedUnits) {
      const charge = this.context.state.units.find((candidate) => candidate.id === protectedId);
      if (!charge) continue;
      strongest = Math.max(strongest, weight * 100 / (1 + dist(this.at, charge)));
    }
    return strongest;
  }

  /** Ranged units want to be near-but-not-adjacent; melee wants contact. */
  private engagementRange(): number {
    if (this.foes.length === 0) return 0;
    const toNearestFoe = nearestDistance(this.at, this.foes.map((foe) => ({ x: foe.x, y: foe.y })));
    const ideal = preferredEngagementRange(this.unit, this.context.content);
    const wants = directiveOf(this.context.rules, this.unit).engagement;
    return -Math.abs(toNearestFoe - ideal) * 16 * (0.5 + this.aggression) * wants;
  }

  /** Cover only counts against the shooter it is actually between you and. */
  private coverFromNearestFoe(): number {
    if (this.foes.length === 0) return 0;
    const nearestFoe = this.foes.slice()
      .sort((left, right) => dist(this.at, left) - dist(this.at, right))[0];
    const facing = this.cell.directionalCoverFrom(nearestFoe);
    if (facing === 'full') return 36;
    if (facing === 'half') return 18;
    if (this.cell.cover === 'full') return 28;
    if (this.cell.cover === 'half') return 14;
    return 0;
  }

  private enemyKeepPull(): number {
    if (this.agenda.enemyHqs.length === 0) return 0;
    return -nearestDistance(this.at, this.agenda.enemyHqs) * 8 * this.aggression;
  }

  /** Stay useful to our own keep, but only while it is actually threatened. */
  private homeDefencePull(): number {
    const myHqs = this.agenda.myHqs;
    if (myHqs.length === 0) return 0;
    const threatened = myHqs.some((keep) => this.foes.some((foe) => dist(foe, keep) <= 5));
    if (!threatened) return 0;
    return -nearestDistance(this.at, myHqs) * 14 * (1 - this.aggression * 0.5);
  }

  /** Fear of the tile itself, discounted by armour and by how needed we are. */
  private exposure(): number {
    const risk = this.context.threat.get(this.tile) ?? 0;
    return -risk * (0.55 - 0.25 * this.aggression) * (1 - this.definition.defense) *
      (1 + this.protectedWeight * 0.15);
  }

  /** Healing ground is only worth standing on when there is damage to heal. */
  private recovery(): number {
    const ours = this.context.state.map.owners[this.tile] === this.unit.owner;
    const rate = ours ? this.terrain.heal : 0;
    if (rate <= 0 || hpRatio(this.unit, this.context.content) >= 0.6) return 0;
    return rate * 4;
  }

  /** Staying inside the commander's reach is worth more than the tile itself. */
  private commandLink(): number {
    if (!this.unit.commanderId) return 0;
    const state = this.context.state;
    const commander = state.commanders.find((candidate) => candidate.id === this.unit.commanderId);
    if (!commander) return 0;
    const leader = state.units.find((candidate) => candidate.id === commander.unitId);
    if (!leader) return 0;
    const distance = dist(this.at, leader);
    return distance <= commander.radius ? 45 : -Math.max(0, distance - commander.radius) * 18;
  }
}
