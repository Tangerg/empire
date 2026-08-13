import { isCharging, resolveDueCasts, type CastingRules } from './casting';
import { UnitEntity } from './domain/unit-entity';
import { advanceTerrainOverlayRound, applyOverlayTurnStartEffects } from './overlays';
import { handleCommanderDefeat, refreshCommanderTurn } from './commanders';
import { playerResource, type BattleResourceSystem } from './resources';
import { runScenarioTriggers, type ScenarioRules } from './scenario';
import { player, unitsOf } from './state';
import { resolveTurnStartStatuses, type StatusRules } from './statuses';
import { activeTurnOrder, type TurnHandoff, type TurnOrderRules } from './turn-order';
import { evaluateVictory, healRateAt, turnResourceGrantsFor, type VictoryRules } from './victory';
import type { GameEvent, GameState, PlayerId, Unit } from './types';

/**
 * Port declared by this module: everything one battle's clock needs. The
 * composition-level `BattleRuleServices` satisfies it structurally, so neither
 * side needs to import the other.
 */
export interface TurnCycleRules
  extends StatusRules, VictoryRules, ScenarioRules, TurnOrderRules, CastingRules {
  readonly resources: BattleResourceSystem;
}

/**
 * The battle's own clock and phase, as an object.
 *
 * These transitions used to be free functions threading `(state, emit, rules)`
 * between each other, with `state.phase` assigned from three unrelated places.
 * Nobody owned the answer to "what may change the phase, and when does a round
 * end", so a policy that separated rounds from actor turns had nowhere to plug
 * in and the exhausted-battle path could leave a battle over without ever
 * saying so.
 *
 * One object now owns `state.phase` and `state.turn`. Two vocabularies stay
 * distinct inside it, because either turn-order family needs them to:
 *  - **round** — one lap of the battle clock: income, overlay decay, `everyRounds`.
 *  - **actor turn** — one entitlement to act: statuses, healing, cooldowns.
 */
export class BattleLifecycle {
  constructor(
    private readonly state: GameState,
    private readonly rules: TurnCycleRules,
    private readonly emit: (event: GameEvent) => void = () => {},
  ) {}

  /**
   * Seeds the ordering policy and claims the battle's first actor turn.
   *
   * The level declares which policy it runs under; from here on the battle's
   * own turn-order state is authoritative, so a saved battle keeps its order.
   */
  start(): void {
    const policy = this.rules.turnOrders.get(this.state.rules.turnOrder);
    this.state.turnOrder = policy.initialState(this.state, this.rules.content);
    this.claim(policy.begin(this.state, this.context()));
  }

  /** Deployment is settled: the battle proper begins. */
  beginPlaying(): void {
    this.state.deployment = null;
    this.state.phase = 'playing';
    for (const unit of this.state.units) new UnitEntity(unit).readyForTurn();
    this.start();
    this.emit({ type: 'battleStarted', player: this.state.currentPlayer, turn: this.state.turn });
  }

  /** The current actor turn is over; hand off through the ordering policy. */
  advanceTurn(): void {
    const state = this.state;
    this.emit({ type: 'turnEnd', player: state.currentPlayer });
    runScenarioTriggers(this.rules, state, 'turnEnd', this.emit);

    const handoff = activeTurnOrder(this.rules, state).advance(state, this.context());
    if (handoff.exhausted) {
      // Nobody is left to act. This used to set the phase and return, leaving a
      // battle "over" with no winner, no reason and no `gameOver` event, so any
      // shell waiting on that event simply hung.
      this.concludeIfDecided();
      if (state.phase !== 'over') this.conclude(null, '无人可行动');
      return;
    }
    if (handoff.roundAdvanced) {
      state.turn++;
      advanceTerrainOverlayRound(state, this.emit);
      this.emit({ type: 'roundStart', turn: state.turn });
    }
    this.claim(handoff);
    this.beginActorTurn(handoff.roundAdvanced);
  }

  /** Ends the battle when the victory rules have decided it. */
  concludeIfDecided(): void {
    const state = this.state;
    const contenders = state.players.filter((candidate) => candidate.alive).map((candidate) => candidate.id);
    const result = evaluateVictory(this.rules, state, this.emit);
    for (const id of contenders) {
      if (!player(state, id).alive) this.emit({ type: 'defeat', player: id });
    }
    if (result.team !== null || result.reason) this.conclude(result.team, result.reason);
  }

  private context() {
    return { content: this.rules.content, emit: this.emit };
  }

  /**
   * Hands the actor turn over and advances the actor-turn clock.
   *
   * That clock is the unit delays are measured in, so it must tick exactly once
   * per entitlement to act — under either turn-order family.
   */
  private claim(handoff: TurnHandoff): void {
    this.state.currentPlayer = handoff.player;
    this.state.turnOrder.activeUnit = handoff.activeUnit;
    this.state.actorTurns++;
  }

  private conclude(team: number | null, reason: string): void {
    this.state.phase = 'over';
    this.state.winnerTeam = team;
    this.state.endReason = reason;
    this.emit({ type: 'gameOver', team, reason });
  }

  /** Everything a newly entitled actor — a whole side, or one unit — receives. */
  private beginActorTurn(roundAdvanced: boolean): void {
    const state = this.state;
    // Charged strikes land before the incoming actor moves, so a unit can be
    // caught by a spell aimed at the tile it is standing on.
    resolveDueCasts(this.rules, state, this.emit);
    const owner = player(state, state.currentPlayer);
    const active = state.turnOrder.activeUnit;
    // Side turns refresh a whole army; per-unit orders refresh only the actor.
    const scope = active === null
      ? unitsOf(state, owner.id)
      : state.units.filter((candidate) => candidate.id === active);

    this.emit(active === null
      ? { type: 'turnStart', player: owner.id, turn: state.turn }
      : { type: 'turnStart', player: owner.id, turn: state.turn, activeUnit: active });

    applyOverlayTurnStartEffects(state, owner.id, this.emit, this.rules.content, scope);
    resolveTurnStartStatuses(this.rules, state, this.emit, scope, (unitId) =>
      handleCommanderDefeat(state, unitId, this.emit, this.rules.content));

    if (active === null) {
      // One income grant per player per round; side turns already give each
      // player exactly one actor turn per round.
      this.openRoundFor(owner.id);
    } else if (roundAdvanced) {
      for (const candidate of state.players.filter((entry) => entry.alive)) {
        this.openRoundFor(candidate.id);
      }
    }

    for (const unit of scope) this.refreshActor(unit, owner.id);
    runScenarioTriggers(this.rules, state, 'turnStart', this.emit);
  }

  /** A player's per-round upkeep: tactics recharge, income lands. */
  private openRoundFor(owner: PlayerId): void {
    refreshCommanderTurn(this.state, owner, this.emit, this.rules.resources);
    this.grantIncome(owner);
  }

  private grantIncome(owner: PlayerId): void {
    const subject = playerResource(player(this.state, owner));
    for (const grant of turnResourceGrantsFor(this.state, owner, this.rules.content)) {
      if (!this.rules.resources.hasAccount(grant.resource, subject)) continue;
      const amount = this.rules.resources.credit(grant.resource, subject, grant.amount);
      const current = this.rules.resources.balance(grant.resource, subject);
      if (amount > 0 && current !== null) {
        this.emit({
          type: 'resourceChanged',
          resource: grant.resource,
          subject: { kind: 'player', id: owner },
          amount,
          current,
        });
      }
    }
  }

  private refreshActor(unit: Unit, owner: PlayerId): void {
    const entity = new UnitEntity(unit);
    entity.advanceWeaponCooldowns();
    // A charging unit keeps its spent action: that lock is what makes charge
    // time a cost instead of a free delay.
    if (!isCharging(this.state, unit)) entity.readyForTurn();
    const rate = healRateAt(this.state, unit.x, unit.y, owner, this.rules.content);
    if (rate <= 0) return;
    const healed = entity.heal(rate, this.rules.content.units.get(unit.type).maxHp);
    if (healed > 0) this.emit({ type: 'regen', unit: unit.id, amount: healed });
  }
}
