import { KeyedRegistry } from '@empire/battle-engine';
import type { ContentCatalog } from '@empire/battle-engine/content-pack';
import type { Coord, GameEvent, GameEventKindMap, GameState, StructureState, Unit } from '@empire/battle-engine/types';
import type { GameSession } from '@empire/battle-engine/session';
import type { BoardView } from './board';

export type GameEventKind = Extract<keyof GameEventKindMap, string>;

/** What an event's animation is allowed to reach. */
export interface BattleStage {
  readonly board: BoardView;
  readonly state: GameState;
  unit(id: number): Unit | null;
  structure(id: string): StructureState | null;
  /**
   * Where a unit was last seen. A killed unit is gone by the time its death is
   * animated, and its damage number still has to land somewhere.
   */
  lastSeen(unitId: number): Coord | null;
  /** Redraw the board between animations — a spawn, a captured tile. */
  repaint(): void;
}

/** What a battle-log line is allowed to read. */
export interface BattleLogContext {
  readonly state: GameState;
  readonly content: ContentCatalog;
  /** Display name of a unit still on the field, or a neutral fallback. */
  unitName(id: number): string;
  playerName(id: number): string;
}

/**
 * How one kind of event looks and reads.
 *
 * Both halves live together because they answer the same question about the
 * same event, and they were previously two `switch` statements a hundred lines
 * apart in the controller — so a new event kind had to be remembered twice, and
 * an event added by a content pack was invisible to both. Either half may be
 * omitted: a capture that only advances progress is worth a log line and no
 * animation, and a turn boundary is the reverse.
 */
export interface BattleEventPresenter<K extends GameEventKind = GameEventKind> {
  readonly type: K;
  animate?(stage: BattleStage, event: GameEventKindMap[K]): Promise<void>;
  describe?(context: BattleLogContext, event: GameEventKindMap[K]): string;
}

export class BattleEventPresenterRegistry extends KeyedRegistry<GameEventKind, BattleEventPresenter> {
  constructor() {
    super('battle event presenter');
  }

  protected keyOf(presenter: BattleEventPresenter): GameEventKind {
    return presenter.type;
  }

  override register<K extends GameEventKind>(presenter: BattleEventPresenter<K>): this {
    return super.register(presenter as BattleEventPresenter);
  }

  override replace<K extends GameEventKind>(presenter: BattleEventPresenter<K>): this {
    return super.replace(presenter as BattleEventPresenter);
  }

  /** An event nobody presents simply passes in silence. */
  async animate(stage: BattleStage, event: GameEvent): Promise<void> {
    await this.tryGet(event.type)?.animate?.(stage, event as never);
  }

  describe(context: BattleLogContext, event: GameEvent): string {
    return this.tryGet(event.type)?.describe?.(context, event as never) ?? '';
  }

  clone(): BattleEventPresenterRegistry {
    return this.copyInto(new BattleEventPresenterRegistry());
  }
}

/**
 * The stage an animation actually gets: a board, the live state, and three
 * lookups. Deliberately narrower than the controller — an animation that could
 * reach the controller could dispatch an action mid-playback.
 */
export class SessionBattleStage implements BattleStage {
  constructor(
    readonly board: BoardView,
    private readonly session: GameSession,
    private readonly lastSeenPositions: ReadonlyMap<number, Coord>,
    private readonly redraw: () => void,
  ) {}

  get state(): GameState {
    return this.session.state;
  }

  unit(id: number): Unit | null {
    return this.session.unit(id) ?? null;
  }

  structure(id: string): StructureState | null {
    return this.state.structures.find((candidate) => candidate.id === id) ?? null;
  }

  lastSeen(unitId: number): Coord | null {
    return this.lastSeenPositions.get(unitId) ?? null;
  }

  repaint(): void {
    this.redraw();
  }
}

const presenter = <K extends GameEventKind>(entry: BattleEventPresenter<K>): BattleEventPresenter<K> => entry;

/** Where the blow lands: on the defender, or on the tile it just died in. */
const impactOn = (stage: BattleStage, unitId: number): Coord | null => {
  const target = stage.unit(unitId);
  return target ? { x: target.x, y: target.y } : stage.lastSeen(unitId);
};

/** Wind up, land, and report — the shape every direct strike shares. */
async function playStrike(
  stage: BattleStage,
  strike: { attacker?: number; at: Coord | null; damage: number; fatal: boolean; weapon?: string },
): Promise<void> {
  const { at } = strike;
  const attacker = strike.attacker === undefined ? null : stage.unit(strike.attacker);
  if (attacker) {
    await stage.board.animateStrike(attacker, at ?? { x: attacker.x, y: attacker.y });
  }
  if (at) await stage.board.animateHit(at, strike.damage, strike.fatal, strike.weapon);
}

const pause = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

const casualty = (killed: boolean, phrasing = '，目标阵亡') => (killed ? phrasing : '');

const RANK_NAMES = ['新兵', '老兵', '精英'] as const;

const RESOURCE_NAMES: Record<string, string> = {
  funds: '资金',
  command_points: '指挥点',
  momentum: '气势',
};

export const DefaultBattleEventPresenters = new BattleEventPresenterRegistry()
  .register(presenter({
    type: 'attack',
    animate: (stage, event) => playStrike(stage, {
      attacker: event.attacker,
      at: impactOn(stage, event.defender),
      damage: event.damage,
      fatal: event.killed,
      weapon: event.weapon,
    }),
    describe: ({ unitName }, event) =>
      `${unitName(event.attacker)} 造成 ${event.damage} 点伤害${casualty(event.killed)}`,
  }))
  .register(presenter({
    type: 'counter',
    animate: (stage, event) => playStrike(stage, {
      attacker: event.attacker,
      at: impactOn(stage, event.defender),
      damage: event.damage,
      fatal: event.killed,
      weapon: event.weapon,
    }),
    describe: (_context, event) => `反击造成 ${event.damage} 点伤害${casualty(event.killed, '，我方阵亡')}`,
  }))
  .register(presenter({
    type: 'supportAttack',
    animate: (stage, event) => playStrike(stage, {
      attacker: event.attacker,
      at: impactOn(stage, event.defender),
      damage: event.damage,
      fatal: event.killed,
      weapon: event.weapon,
    }),
    describe: ({ unitName }, event) =>
      `${unitName(event.attacker)} 援护攻击造成 ${event.damage} 点伤害${casualty(event.killed)}`,
  }))
  .register(presenter({
    type: 'partingShot',
    // The defender has already moved on; the blow lands on the tile it left.
    animate: (stage, event) => playStrike(stage, {
      attacker: event.attacker,
      at: event.at,
      damage: event.damage,
      fatal: event.killed,
      weapon: event.weapon,
    }),
    describe: ({ unitName }, event) =>
      `${unitName(event.attacker)} 借机攻击造成 ${event.damage} 点伤害${casualty(event.killed)}`,
  }))
  .register(presenter({
    type: 'areaAttack',
    // Splash has no wind-up of its own: the aimed strike already played one.
    animate: (stage, event) => playStrike(stage, {
      at: impactOn(stage, event.defender),
      damage: event.damage,
      fatal: event.killed,
      weapon: event.weapon,
    }),
    describe: ({ unitName }, event) =>
      `范围攻击对 ${unitName(event.defender)} 造成 ${event.damage} 点伤害${casualty(event.killed)}`,
  }))
  .register(presenter({
    type: 'attackStructure',
    animate: (stage, event) => {
      const structure = stage.structure(event.structure);
      return playStrike(stage, {
        attacker: event.attacker,
        at: structure ? { x: structure.x, y: structure.y } : null,
        damage: event.damage,
        fatal: event.destroyed,
        weapon: event.weapon,
      });
    },
    describe: (_context, event) =>
      `对结构造成 ${event.damage} 点伤害${casualty(event.destroyed, '，结构被摧毁')}`,
  }))
  .register(presenter({
    type: 'areaAttackStructure',
    animate: (stage, event) => {
      const structure = stage.structure(event.structure);
      return playStrike(stage, {
        at: structure ? { x: structure.x, y: structure.y } : null,
        damage: event.damage,
        fatal: event.destroyed,
        weapon: event.weapon,
      });
    },
    describe: (_context, event) =>
      `范围攻击对结构造成 ${event.damage} 点伤害${casualty(event.destroyed, '，结构被摧毁')}`,
  }))
  .register(presenter({
    type: 'death',
    animate: (stage, event) => stage.board.animateDeath(event.unit),
  }))
  .register(presenter({
    type: 'heal',
    animate: async (stage, event) => {
      const target = stage.unit(event.target);
      if (target) await stage.board.animateHeal({ x: target.x, y: target.y }, event.amount);
    },
    describe: ({ unitName }, event) => `${unitName(event.source)} 治疗了 ${event.amount} 点生命`,
  }))
  .register(presenter({
    type: 'recruit',
    animate: async (stage, event) => {
      stage.repaint();
      await stage.board.animateSpawn(event.unit);
    },
    describe: ({ unitName, playerName, state }, event) =>
      `${playerName(state.currentPlayer)} 征募了 ${unitName(event.unit)}`,
  }))
  .register(presenter({
    type: 'capture',
    animate: async (stage, event) => {
      if (event.captured) stage.board.syncTerrain();
    },
    describe: ({ playerName }, event) =>
      event.captured ? `${playerName(event.player)} 占领了 (${event.at.x}, ${event.at.y})` : '占领进度提升',
  }))
  .register(presenter({
    type: 'turnStart',
    animate: async (stage, event) => {
      const player = stage.state.players.find((candidate) => candidate.id === event.player);
      if (!player) return;
      stage.repaint();
      await stage.board.announce(`${player.name} · 第 ${event.turn} 回合`, player.color);
    },
    describe: ({ playerName }, event) => `第 ${event.turn} 回合 · ${playerName(event.player)}`,
  }))
  .register(presenter({
    type: 'resourceChanged',
    describe: ({ unitName, playerName }, event) => {
      const resource = RESOURCE_NAMES[event.resource] ?? event.resource;
      const subject = event.subject.kind === 'player'
        ? playerName(event.subject.id)
        : event.subject.kind === 'unit'
          ? unitName(event.subject.id)
          : unitName(event.subject.unit);
      return `${subject} ${resource} ${event.amount >= 0 ? '+' : ''}${event.amount}（${event.current}）`;
    },
  }))
  .register(presenter({
    type: 'rankChanged',
    describe: ({ unitName }, event) => `${unitName(event.unit)} 晋升为 ${RANK_NAMES[event.to]}`,
  }))
  .register(presenter({
    type: 'careerChanged',
    describe: ({ unitName, content }, event) =>
      `${unitName(event.unit)} 转职为 ${content.careers.get(event.to).name}`,
  }))
  .register(presenter({
    type: 'facingChanged',
    describe: ({ unitName }, event) => `${unitName(event.unit)} 调整了朝向`,
  }))
  .register(presenter({
    type: 'defeat',
    describe: ({ playerName }, event) => `${playerName(event.player)} 已被击败`,
  }))
  .register(presenter({
    type: 'gameOver',
    // A beat before the modal, so the last blow is still on screen when it opens.
    animate: () => pause(220),
    describe: (_context, event) => event.reason,
  }));
