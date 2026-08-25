import { icon } from '../art/icons';
import type { ArtDirection } from '../art/direction';
import { portraitSvg } from '../art/portraits';
import { unitIcon } from '../art/units';
import { PAL } from '../art/palette';
import {
  type BattleRuleServices,
  type CommandOption,
  type CareerOption,
  type FormationOption,
  type CarrierOption,
  type PassengerOption,
  type TacticOption,
  type CombatForecast,
  type CombatModifier,
  type CombatPlan,
  activeCommanderFor,
  commanderUnit,
  idx,
  player,
  recruitOptions,
  type RecruitOption,
  describeObjective,
  objectiveProgress,
  type Coord,
  type Direction,
  type GameState,
  type PendingCast,
  type ReactionStance,
  type ResourceAmount,
  type Unit,
  type UnitDef,
  SpellCastEntity,
  gaugeRatio,
  sameCoord,
} from '@empire/battle-engine';
import {
  type BattleResourceSystem,
  type ResourceSubject,
  playerResource,
  unitResource,
  weaponResource,
} from '@empire/battle-engine';
import { gaugeColor, gaugeFill } from '../art/gauges';
import { escapeHtml } from './html';

export interface HudView {
  state: GameState;
  /** Ruleset this view was projected from; every label resolves through it. */
  rules: BattleRuleServices;
  resources: BattleResourceSystem;
  /** Unit under the cursor or currently selected. */
  inspect: Unit | null;
  tile: Coord | null;
  /**
   * The exchange the player is currently aiming at: the whole area plan, plus
   * the predicted blow between attacker and whoever ends up taking it.
   */
  forecast: {
    plan: CombatPlan;
    exchange: CombatForecast;
    attacker: Unit;
    defender: Unit;
    recipient: Unit;
  } | null;
  commands: CommandOption[] | null;
  tactics: Array<TacticOption & { key: string; commander: string }>;
  reactionUnit: number | null;
  /** Upcoming actor turns for per-unit orders; empty for side turns. */
  turnOrder: { units: Unit[]; activeUnit: number | null };
  /** Strikes still charging, newest last. Empty when nothing is being cast. */
  casts: PendingCast[];
  rankNextThreshold: number | null;
  careerOptions: CareerOption[];
  formationOptions: FormationOption[];
  /** Carriers this unit is standing beside, eligible or with a reason. */
  carrierOptions: CarrierOption[];
  /** Who is aboard this unit when it is a carrier, and where each may land. */
  passengerOptions: PassengerOption[];
  /**
   * The pre-battle arrangement this side is making, or null once playing.
   *
   * Deployment replaces the orders column rather than adding a screen: the
   * player is still picking a unit and clicking a cell, so the same region says
   * so with a different list, and the dock confirms instead of ending a turn.
   */
  deployment: {
    units: Unit[];
    selected: number | null;
    /** Whom placing the picked unit under the cursor would send back, if anyone. */
    swaps: Unit | null;
  } | null;
  /** Ability whose target we are picking, if any. */
  targeting: string | null;
  recruitAt: Coord | null;
  hint: string;
  busy: boolean;
  /**
   * Whether this sitting can be put down and picked up again.
   *
   * Null when the shell keeps no slot — a campaign battle is resumed through the
   * campaign's own save, not through this one — and the entry stays off screen.
   */
  saves: { canSave: boolean; canResume: boolean } | null;
  canUndo: boolean;
  messages: string[];
  /** Application-owned labels keep campaign/navigation wording out of battle rules. */
  exitLabel?: string | undefined;
  completionLabel?: string | undefined;
}

export interface HudHandlers {
  onCommand(ability: string): void;
  onTactic(key: string): void;
  onReaction(stance: ReactionStance): void;
  onFacing(facing: Direction): void;
  onCareer(career: string): void;
  /** An empty id means "stand in no formation at all". */
  onFormation(formation: string | null): void;
  /** Board the named carrier with the unit now under command. */
  onEmbark(carrier: number): void;
  /** Start choosing where the named passenger steps off. */
  onDisembark(passenger: number): void;
  /** Take up one of the units being arranged before the battle. */
  onDeployPick(unit: number): void;
  /** Confirm the arrangement; the battle begins when every side has. */
  onConfirmDeployment(): void;
  onCancel(): void;
  onEndTurn(): void;
  onUndo(): void;
  onRestart(): void;
  onSave(): void;
  onResume(): void;
  onRecruit(unit: string): void;
  onExit(): void;
  onContinue(): void;
  onZoom(delta: number): void;
}

const pct = (n: number) => `${Math.round(n * 100)}%`;

function resourceName(resources: BattleResourceSystem, id: string): string {
  return resources.adapters.tryGet(id)?.name ?? id;
}

function formatAmounts(resources: BattleResourceSystem, amounts: readonly ResourceAmount[]): string {
  return amounts.length === 0
    ? '无'
    : amounts.map((amount) => `${resourceName(resources, amount.resource)} ${amount.amount}`).join(' · ');
}

function accountSummary(resources: BattleResourceSystem, subject: ResourceSubject): string[] {
  return resources.adapters.keys().flatMap((id) => {
    if (!resources.hasAccount(id, subject)) return [];
    const account = resources.inspect(id, subject);
    const value = account.current === null
      ? '∞'
      : account.capacity === null
        ? String(account.current)
        : `${account.current}/${account.capacity}`;
    return [`${resourceName(resources, id)} ${value}`];
  });
}

/**
 * Chip wording for the categories this game ships. A rule plugin may contribute
 * a category nobody translated, which reads as its own id rather than as a
 * blank chip — the chain stays honest about where a number came from.
 */
const MODIFIER_SOURCE_LABEL: Record<string, string> = {
  weapon: '武器',
  matchup: '克制',
  unit: '单位',
  status: '状态',
  commander: '指挥',
  terrain: '地形',
  reaction: '反应',
  elevation: '高低差',
  position: '方位',
  cover: '掩体',
  formation: '阵形',
  extension: '扩展',
};

const RANK_LABEL = ['新兵', '老兵', '精英'] as const;
/**
 * The arrow beside a facing's name.
 *
 * Names come from the tiling — it owns which facings exist — so this table only
 * decorates the ones it recognises. A tiling with facings nobody drew an arrow
 * for still gets a working row of buttons.
 */
const FACING_ARROW: Readonly<Record<string, string>> = {
  north: '↑', east: '→', south: '↓', west: '←',
  northeast: '↗', southeast: '↘', southwest: '↙', northwest: '↖',
  hexEast: '→', hexWest: '←',
  hexNortheast: '↗', hexNorthwest: '↖', hexSoutheast: '↘', hexSouthwest: '↙',
};

const facingLabel = (view: HudView, facing: Direction): string => {
  const named = view.rules.grids.get(view.state.rules.grid).directions
    .find((direction) => direction.id === facing);
  return `${named?.name ?? facing}${FACING_ARROW[facing] ? ` ${FACING_ARROW[facing]}` : ''}`;
};

/**
 * Where a unit stands with one career.
 *
 * `CareerOption.mastered` — mastery at or past that career's threshold, which
 * brings its `masteryAbilities` back with it — was computed by the rules and read
 * by nobody. The panel showed mastery of the career a unit is *in*, so a branch
 * already mastered looked exactly like one never entered.
 */
const careerStanding = (option: CareerOption): string => {
  if (option.mastered) return '已精通，转职后可用该职业的精通技能';
  if (option.unlocked) return '已解锁，可自由切换';
  return '满足进阶条件';
};

function modifierClass(modifier: CombatModifier): string {
  if (modifier.stage === 'mitigation') return modifier.value > 0 ? 'bad' : modifier.value < 0 ? 'good' : '';
  if (modifier.operation === 'multiply') return modifier.value > 1 ? 'good' : modifier.value < 1 ? 'bad' : '';
  return modifier.value > 0 ? 'good' : modifier.value < 0 ? 'bad' : '';
}

function modifierValue(modifier: CombatModifier): string {
  if (modifier.stage === 'mitigation') {
    return `${modifier.value >= 0 ? '+' : ''}${pct(modifier.value)} 减伤`;
  }
  if (modifier.operation === 'multiply') return `×${modifier.value.toFixed(2)}`;
  return `${modifier.value >= 0 ? '+' : ''}${modifier.value.toFixed(2)}`;
}

function modifierList(modifiers: readonly CombatModifier[]): string {
  return modifiers.map((modifier) =>
    `<li class="${modifierClass(modifier)}"><span class="modifier-source">${escapeHtml(MODIFIER_SOURCE_LABEL[modifier.source] ?? modifier.source)}</span> ${escapeHtml(modifier.label)} <b>${modifierValue(modifier)}</b></li>`,
  ).join('');
}

function hpBar(ratio: number, width = 96): string {
  const color = gaugeColor(ratio);
  return `<span class="bar" style="--w:${width}px">
    <i style="width:${gaugeFill(ratio) * 100}%;background:${color}"></i>
  </span>`;
}

/**
 * Where the HUD lays a thing over the battlefield, and the question it answers.
 *
 * The HUD used to be a `<header>` and an `<aside>` that the controller arranged
 * into a page: a title bar above the content and a sidebar beside it. Those are
 * the shapes of a document, and they read as one — the battlefield became a
 * picture embedded in an editor rather than the thing being played. The regions
 * below sit *over* the field instead, and each is named for what it tells the
 * player, so a new panel has to say which of these questions it answers before
 * it can be placed at all.
 *
 * The strings are the accessible names, and they are the same sentences the
 * layout is explained with.
 */
const HUD_REGIONS = {
  crown: '谁在行动',
  flank: '这一战要打成什么',
  aside: '眼下能下什么令',
  dispatch: '刚刚发生了什么',
  ledger: '光标底下是什么地方',
  hint: '下一步该怎么做',
  dock: '这一手到此为止',
  veil: '需要先回答的事',
} as const;

type HudRegion = keyof typeof HUD_REGIONS;

/** Everything the screen can be asking for, in the order the answers are read. */
type BattleMode = 'over' | 'recruiting' | 'targeting' | 'deploying' | 'waiting' | 'commanding';

/**
 * What the player is being asked for, as one word the whole screen reacts to.
 *
 * Declared rather than sniffed: the battlefield's wash, the HUD's tint and the
 * accent on the committing control are one mood, and four stylesheets each
 * guessing at it from a different class is how they drift apart.
 */
function battleMode(view: HudView): BattleMode {
  if (view.state.phase === 'over') return 'over';
  if (view.recruitAt) return 'recruiting';
  if (view.targeting) return 'targeting';
  if (view.deployment) return 'deploying';
  if (view.busy) return 'waiting';
  return 'commanding';
}

/** The HUD: one overlay of named regions laid over the battlefield. */
export class Hud {
  /** The overlay itself. The battle shell lays it over the field and nothing else. */
  readonly el = document.createElement('div');

  private readonly regions: Record<HudRegion, HTMLElement>;
  /**
   * What each region currently holds, so a region is only rewritten when it has
   * something different to say.
   *
   * One `innerHTML` for the whole panel meant every pointer move rebuilt the
   * objectives, the roster and the battle log — so nothing in the HUD could
   * animate, hold a scroll position, or keep an image from reloading, and the
   * whole overlay had the stillness of a re-rendered page.
   */
  private readonly written = new Map<HudRegion, string>();

  /**
   * Every intent a rendered control can declare, and who answers it.
   *
   * A thirteen-case switch that did nothing but forward each case to a handler
   * of the same name is a table written the long way — and one nothing could
   * compare against the markup, so a control naming an intent nobody answered
   * looked alive and did nothing.
   */
  private readonly intents: Record<string, (arg: string) => void> = {
    command: (arg) => this.handlers.onCommand(arg),
    tactic: (arg) => this.handlers.onTactic(arg),
    reaction: (arg) => this.handlers.onReaction(arg as ReactionStance),
    facing: (arg) => this.handlers.onFacing(arg as Direction),
    career: (arg) => this.handlers.onCareer(arg),
    formation: (arg) => this.handlers.onFormation(arg || null),
    embark: (arg) => this.handlers.onEmbark(Number(arg)),
    disembark: (arg) => this.handlers.onDisembark(Number(arg)),
    'deploy-pick': (arg) => this.handlers.onDeployPick(Number(arg)),
    'deploy-done': () => this.handlers.onConfirmDeployment(),
    recruit: (arg) => this.handlers.onRecruit(arg),
    zoom: (arg) => this.handlers.onZoom(Number(arg)),
    cancel: () => this.handlers.onCancel(),
    end: () => this.handlers.onEndTurn(),
    undo: () => this.handlers.onUndo(),
    restart: () => this.handlers.onRestart(),
    save: () => this.handlers.onSave(),
    resume: () => this.handlers.onResume(),
    exit: () => this.handlers.onExit(),
    continue: () => this.handlers.onContinue(),
  };

  constructor(
    /** The art this overlay draws with; composed by the application root. */
    private readonly art: ArtDirection,
    private readonly handlers: HudHandlers,
  ) {
    this.el.className = 'battle-hud';
    const host = (region: HudRegion): HTMLElement => {
      const element = document.createElement('div');
      element.className = `hud-region hud-${region}`;
      // A landmark, so the label is an accessible name a reader can jump to
      // rather than an attribute on an anonymous box.
      element.setAttribute('role', 'region');
      element.setAttribute('aria-label', HUD_REGIONS[region]);
      return element;
    };
    // Written out rather than mapped over the table, so `Record<HudRegion, …>`
    // refuses a region that has no host and a host that names no region. The
    // order is the reading order, which is also the tab order.
    this.regions = {
      crown: host('crown'),
      flank: host('flank'),
      aside: host('aside'),
      dispatch: host('dispatch'),
      ledger: host('ledger'),
      hint: host('hint'),
      dock: host('dock'),
      veil: host('veil'),
    };
    this.el.append(...Object.values(this.regions));

    this.el.addEventListener('click', (event) => {
      const control = (event.target as HTMLElement).closest('[data-act]') as HTMLElement | null;
      if (!control) return;
      this.intents[control.dataset.act ?? '']?.(control.dataset.arg ?? '');
    });
  }

  /** The intents this HUD answers, so a test can compare them with the markup. */
  get handledIntents(): string[] {
    return Object.keys(this.intents);
  }

  render(view: HudView): void {
    this.el.dataset.mode = battleMode(view);
    this.write('crown', this.renderCrown(view));
    this.write('flank', this.renderObjectives(view));
    this.write('aside', this.renderOrders(view));
    this.write('dispatch', this.renderLog(view));
    this.write('ledger', this.renderTile(view));
    this.write('hint', this.renderHint(view));
    this.write('dock', this.renderDock(view));
    this.write('veil', this.renderRecruit(view) + this.renderGameOver(view));
  }

  /** Rewrites a region only when it has something different to say. */
  private write(region: HudRegion, markup: string): void {
    if (this.written.get(region) === markup) return;
    this.written.set(region, markup);
    this.regions[region].innerHTML = markup;
  }

  /* ------------------------------------------------------------------ crown */

  /**
   * Initiative strip. Deliberately absent under side turns, where "who acts
   * next" is the whole army and a strip would say nothing.
   */
  private renderTurnOrder(view: HudView): string {
    const upcoming = view.turnOrder.units;
    if (upcoming.length === 0) return '';
    const content = view.rules.content;
    return `<div class="order-strip" title="行动序">
      ${upcoming
        .map((unit, index) => {
          const owner = view.state.players.find((entry) => entry.id === unit.owner);
          const definition = content.units.get(unit.type);
          const active = unit.id === view.turnOrder.activeUnit && index === 0;
          return `<span class="order-slot ${active ? 'is-active' : ''}" style="--team:${owner?.color ?? PAL.neutral}"
            title="${escapeHtml(definition.name)} · ${escapeHtml(owner?.name ?? '')}">
            ${unitIcon(this.art, definition, owner?.color ?? PAL.neutral, active ? 26 : 20)}
          </span>`;
        })
        .join('')}
    </div>`;
  }

  /**
   * Charging strikes. A charge that nobody can see is a trap, so both sides'
   * casts are shown: the tile is public information the moment it is marked.
   */
  private renderCasts(view: HudView): string {
    if (view.casts.length === 0) return '';
    const content = view.rules.content;
    return `<div class="cast-strip" title="咏唱中">
      ${view.casts
        .map((cast) => {
          const owner = view.state.players.find((entry) => entry.id === cast.owner);
          const entity = new SpellCastEntity(cast);
          const remaining = entity.remainingAt(view.state.actorTurns);
          return `<span class="cast-slot" style="--team:${owner?.color ?? PAL.neutral}"
            title="${escapeHtml(content.weapons.get(cast.weapon).name)} · 目标 ${cast.target.x},${cast.target.y} · 还有 ${remaining} 个行动轮">
            ${icon('crosshair')}
            <b>${remaining}</b>
            <i style="--fill:${Math.round(entity.progressAt(view.state.actorTurns) * 100)}%"></i>
          </span>`;
        })
        .join('')}
    </div>`;
  }

  /** Whose turn it is, how far in, and the controls that are always to hand. */
  private renderCrown(view: HudView): string {
    const state = view.state;
    const active = player(state, state.currentPlayer);
    const accounts = accountSummary(view.resources, playerResource(active));
    const turnLimit = state.rules.turnLimit ? `<i>/ ${state.rules.turnLimit}</i>` : '';
    return `
      <div class="crown-group is-leading">
        <button class="btn glyph" data-act="exit" title="${escapeHtml(view.exitLabel ?? '返回关卡列表')}">${icon('grid')}</button>
        <div class="standard">
          <b>${escapeHtml(state.levelName)}</b>
          <span>第 <em>${state.turn}</em> ${turnLimit} 回合</span>
        </div>
      </div>
      <div class="crown-group is-centre">
        ${this.renderTurnOrder(view)}
        ${this.renderCasts(view)}
      </div>
      <div class="crown-group is-trailing">
        <div class="player-chip" style="--team:${active.color}">
          <span class="dot"></span>
          <b>${escapeHtml(active.name)}</b>
          <span class="sub">${active.controller === 'human' ? '你的回合' : 'AI 行动中'}</span>
        </div>
        ${accounts.map((account) => `<div class="funds">${icon('coin')}<b>${escapeHtml(account)}</b></div>`).join('')}
        <div class="glyph-cluster">
          <button class="btn glyph" data-act="zoom" data-arg="-0.15" title="缩小">−</button>
          <button class="btn glyph" data-act="zoom" data-arg="0.15" title="放大">+</button>
          <button class="btn glyph" data-act="undo" ${view.canUndo && !view.busy ? '' : 'disabled'} title="撤销 (U)">${icon('undo')}</button>
          ${this.renderSaveSlot(view)}
          <button class="btn glyph" data-act="restart" title="重新开始">${icon('play')}</button>
        </div>
      </div>`;
  }

  /** Put the battle down, or pick it up where it was left. */
  private renderSaveSlot(view: HudView): string {
    if (!view.saves) return '';
    return `
      <button class="btn glyph" data-act="save" ${view.saves.canSave ? '' : 'disabled'} title="保存战斗进度">${icon('save')}</button>
      <button class="btn glyph" data-act="resume" ${view.saves.canResume ? '' : 'disabled'} title="读取战斗进度">${icon('flag')}</button>`;
  }

  /* ------------------------------------------------------------------- dock */

  /**
   * The one control that ends the player's turn in this phase.
   *
   * It sits apart from the rest because it is the only irreversible thing on
   * screen: it used to be the last button in a row of six system glyphs, where
   * "confirm the whole arrangement" looked exactly like "zoom out".
   */
  private renderDock(view: HudView): string {
    if (view.deployment) {
      return `<button class="btn commit" data-act="deploy-done" ${view.busy ? 'disabled' : ''}>
        ${icon('flag')} 确认部署
      </button>`;
    }
    const active = player(view.state, view.state.currentPlayer);
    return `<button class="btn commit" data-act="end" ${view.busy || active.controller !== 'human' ? 'disabled' : ''}>
      ${icon('hourglass')} 结束回合 <kbd>E</kbd>
    </button>`;
  }

  /**
   * What to do next, in one line at the foot of the field.
   *
   * The selection owns this sentence — it is the state machine the player is
   * standing in — and this is the only place it is shown. It used to be printed
   * inside whichever panel happened to be up, with the target-picking sentence
   * written a second time here in the HUD, so the two could and did disagree.
   */
  private renderHint(view: HudView): string {
    if (!view.hint) return '';
    return `<p class="crier">${escapeHtml(view.hint)}</p>`;
  }

  /* ----------------------------------------------------------------- orders */

  /**
   * The orders column: what may be commanded, and what it would cost.
   *
   * Assembled here rather than by the shell so the column is one region with one
   * reading order — the order being composed, then the exchange it would cause,
   * then whoever is under the cursor.
   */
  private renderOrders(view: HudView): string {
    return this.renderCommands(view)
      + this.renderTactics(view)
      + this.renderForecast(view)
      + this.renderUnit(view);
  }

  /** The line waiting to be arranged; clicking a name takes that unit up. */
  private renderDeployment(view: HudView): string {
    const deployment = view.deployment;
    if (!deployment) return '';
    // The swap is legal in both directions or it is not offered at all, so naming
    // the comrade is the whole of what the board cannot already show.
    const swap = deployment.swaps
      ? `<p class="hint tiny">落在此格：与${escapeHtml(view.rules.content.units.get(deployment.swaps.type).name)}换位</p>`
      : '';
    return `<section class="plaque is-live">
      <h3>战前部署</h3>
      <div class="cmd-list">${deployment.units.map((unit) => `<button
        class="btn ${unit.id === deployment.selected ? 'primary' : 'ghost'}"
        data-act="deploy-pick" data-arg="${unit.id}" ${view.busy ? 'disabled' : ''}
        >${escapeHtml(view.rules.content.units.get(unit.type).name)} <span class="sub">${unit.x},${unit.y}</span></button>`).join('')}</div>
      ${swap}
    </section>`;
  }

  private renderCommands(view: HudView): string {
    if (view.deployment) return this.renderDeployment(view);
    if (view.targeting) {
      return `<section class="plaque is-live">
        <h3>选择目标</h3>
        <button class="btn wide" data-act="cancel">取消 <kbd>Esc</kbd></button>
      </section>`;
    }
    if (!view.commands || view.commands.length === 0) return '';
    const keyOf: Record<string, string> = { attack: 'A', capture: 'C', heal: 'H', wait: 'W' };
    const iconOf: Record<string, string> = {
      attack: 'sword',
      capture: 'flag',
      heal: 'cross',
      wait: 'hourglass',
    };
    const commandIcon = (command: CommandOption): string => {
      const weapon = command.weapon;
      return (weapon
        ? this.art.resolve((provider) => provider.weaponIcon?.(weapon))
        : this.art.resolve((provider) => provider.abilityIcon?.(command.ability)))
        ?? icon(iconOf[command.ability] ?? 'crosshair');
    };
    return `<section class="plaque is-live">
      <h3>指令</h3>
      <div class="cmd-list">
        ${view.commands
          .map(
            (command) => `<button class="btn cmd" data-act="command" data-arg="${escapeHtml(command.key)}" title="${escapeHtml(command.hint)}">
              ${commandIcon(command)}<span>${escapeHtml(command.name)}</span>
              ${keyOf[command.ability] ? `<kbd>${keyOf[command.ability]}</kbd>` : ''}
            </button>`,
          )
          .join('')}
      </div>
      <button class="btn wide ghost" data-act="cancel">取消 <kbd>Esc</kbd></button>
    </section>`;
  }

  private renderTactics(view: HudView): string {
    if (view.targeting || view.commands || view.tactics.length === 0) return '';
    return `<section class="plaque is-live">
      <h3>指挥战术</h3>
      <div class="cmd-list">
        ${view.tactics
          .map(
            (tactic) => `<button class="btn cmd" data-act="tactic" data-arg="${escapeHtml(tactic.key)}">
              ${this.art.resolve((provider) => provider.abilityIcon?.(tactic.id)) ?? icon('flag')}<span>${escapeHtml(tactic.name)}</span><em>${escapeHtml(formatAmounts(view.resources, tactic.costs))}</em>
            </button>`,
          )
          .join('')}
      </div>
    </section>`;
  }

  /* --------------------------------------------------------------- forecast */

  private renderForecast(view: HudView): string {
    if (!view.forecast) return '';
    const { plan, exchange, attacker, defender, recipient } = view.forecast;
    const content = view.rules.content;
    const attackerDef = content.units.get(attacker.type);
    const recipientDef = content.units.get(recipient.type);
    return `<section class="plaque forecast">
      <h3>战斗预测</h3>
      <div class="fc-row">
        <span class="fc-name">${escapeHtml(attackerDef.name)}</span>
        <span class="fc-arrow">${icon('sword')}</span>
        <span class="fc-name">${escapeHtml(content.units.get(defender.type).name)}</span>
      </div>
      ${exchange.interceptor ? `<div class="hint">${escapeHtml(recipientDef.name)} 将进行援护并承受伤害</div>` : ''}
      ${exchange.reaction && !exchange.interceptor
        ? `<div class="hint">目标将触发「${escapeHtml(view.rules.reactions.get(exchange.reaction.stance).name)}」姿态</div>`
        : ''}
      ${plan.unitHits.length + plan.structureHits.length > 1
        ? `<div class="hint">范围攻击还将波及 ${plan.unitHits.length - 1} 个单位、${plan.structureHits.length} 个结构</div>`
        : ''}
      ${plan.supportAttack
        ? `<div class="hint">援护攻击预计追加 ${plan.supportAttack.damage.damage} 点伤害</div>`
        : ''}
      <div class="fc-line">
        <span>造成伤害</span>
        <b class="dmg">${exchange.strike.damage}</b>
        <span class="fc-hp">${recipient.hp} → ${exchange.recipientHpAfter}</span>
      </div>
      ${hpBar(exchange.recipientHpAfter / recipientDef.maxHp)}
      <div class="fc-line">
        <span>遭到反击</span>
        <b class="${exchange.counter ? 'dmg' : 'none'}">${exchange.counter ? exchange.counter.damage : '无'}</b>
        <span class="fc-hp">${attacker.hp} → ${exchange.attackerHpAfter}</span>
      </div>
      ${hpBar(exchange.attackerHpAfter / attackerDef.maxHp)}
      <div class="fc-chain-title">攻击修正链 · ${content.damageTypes.get(exchange.strike.damageType).name} → ${content.armorClasses.get(recipientDef.armorClass).name}</div>
      <ul class="fc-detail">
        ${modifierList(exchange.strike.modifiers)}
        <li>最终减伤上限后 <b>${pct(exchange.strike.mitigation)}</b></li>
        ${exchange.recipientDies ? '<li class="good">可以击杀伤害承担者</li>' : ''}
        ${exchange.attackerDies ? '<li class="bad">反击会导致我方阵亡</li>' : ''}
      </ul>
      ${exchange.counter ? `<div class="fc-chain-title">反击修正链</div><ul class="fc-detail">${modifierList(exchange.counter.modifiers)}</ul>` : ''}
    </section>`;
  }

  /* ------------------------------------------------------------------- unit */

  private renderUnit(view: HudView): string {
    const unit = view.inspect;
    if (!unit) return '';
    const definition = view.rules.content.units.get(unit.type);
    return `<section class="plaque">
      ${this.unitHeader(view, unit, definition)}
      ${this.unitStats(view, definition)}
      ${this.unitTags(view, unit, definition)}
      ${this.unitArsenal(view, unit, definition)}
      ${this.unitCondition(view, unit)}
      ${this.unitControls(view, unit)}
    </section>`;
  }

  private unitHeader(view: HudView, unit: Unit, definition: UnitDef): string {
    const owner = view.state.players.find((player) => player.id === unit.owner);
    return `<div class="unit-head">
      ${portraitSvg(this.art, definition, owner?.color ?? PAL.neutral, 84)}
      <div class="unit-meta">
        <div class="unit-name">${escapeHtml(definition.name)}
          <span class="team-tag" style="--team:${owner?.color}">${escapeHtml(owner?.name ?? '中立')}</span>
        </div>
        <div class="unit-hp">${icon('heart')} ${unit.hp} / ${definition.maxHp} ${hpBar(gaugeRatio(unit.hp, definition.maxHp), 72)}</div>
        <div class="unit-blurb">${escapeHtml(definition.blurb)}</div>
      </div>
    </div>`;
  }

  private unitStats(view: HudView, definition: UnitDef): string {
    const content = view.rules.content;
    const weapons = definition.weapons.map((id) => content.weapons.get(id));
    const minimumRange = Math.min(...weapons.map((weapon) => weapon.minRange));
    const maximumRange = Math.max(...weapons.map((weapon) => weapon.maxRange));
    const damageTypes = [...new Set(weapons.map((weapon) => content.damageTypes.get(weapon.damageType).name))].join(' / ');
    return `<div class="stat-grid">
      <div><span>最高威力</span><b>${Math.max(...weapons.map((weapon) => weapon.power))}</b></div>
      <div><span>减伤</span><b>${pct(definition.defense)}</b></div>
      <div><span>移动</span><b>${definition.movement}</b></div>
      <div><span>射程</span><b>${minimumRange === maximumRange ? minimumRange : `${minimumRange}-${maximumRange}`}</b></div>
      <div><span>伤害</span><b>${damageTypes}</b></div>
      <div><span>护甲</span><b>${content.armorClasses.get(definition.armorClass).name}</b></div>
      <div><span>移动型</span><b>${content.movementProfiles.get(definition.movementClass).name}</b></div>
      <div><span>战力价值</span><b>${definition.value}</b></div>
    </div>`;
  }

  private unitTags(view: HudView, unit: Unit, definition: UnitDef): string {
    const weapons = definition.weapons.map((id) => view.rules.content.weapons.get(id));
    const stance = view.rules.reactions.get(unit.reaction);
    return `<div class="tag-row">
      ${weapons.some((weapon) => weapon.moveAndAttack) ? '' : '<span class="tag warn">移动后无法攻击</span>'}
      ${definition.abilities.includes('capture') ? '<span class="tag">可占领</span>' : '<span class="tag dim">不可占领</span>'}
      ${definition.abilities.includes('heal') ? '<span class="tag good">可治疗</span>' : ''}
      <span class="tag" title="${escapeHtml(stance.hint)}">反应：${escapeHtml(stance.name)}</span>
      ${unit.done ? '<span class="tag dim">已行动</span>' : ''}
    </div>`;
  }

  private unitArsenal(view: HudView, unit: Unit, definition: UnitDef): string {
    const content = view.rules.content;
    return `<div class="unit-section">
      <h4>武器与资源</h4>
      ${definition.weapons.map((id) => {
        const weapon = content.weapons.get(id);
        const runtime = unit.weaponState[id];
        const range = weapon.minRange === weapon.maxRange
          ? String(weapon.minRange)
          : `${weapon.minRange}-${weapon.maxRange}`;
        const cooldown = runtime.cooldownRemaining > 0 ? ` · 冷却 ${runtime.cooldownRemaining}` : '';
        const accounts = accountSummary(view.resources, weaponResource(unit, id));
        const requirements = weapon.resourceRequirements.length > 0
          ? ` · 需要 ${formatAmounts(view.resources, weapon.resourceRequirements)}`
          : '';
        const costs = weapon.resourceCosts.length > 0
          ? ` · 消耗 ${formatAmounts(view.resources, weapon.resourceCosts)}`
          : '';
        const stock = accounts.length > 0 ? accounts.join(' · ') : '无限制';
        const art = this.art.resolve((provider) => provider.weaponIcon?.(id)) ?? '';
        return `<div class="kv wrap art-kv"><span>${art}<i>${escapeHtml(weapon.name)} · ${content.damageTypes.get(weapon.damageType).name} ${weapon.power} · 射程 ${range}</i></span><b>${escapeHtml(stock)}${cooldown}${escapeHtml(requirements)}${escapeHtml(costs)}</b></div>`;
      }).join('')}
    </div>`;
  }

  /** Statuses, rank, facing, career — what this unit is like right now. */
  private unitCondition(view: HudView, unit: Unit): string {
    const content = view.rules.content;
    const commander = unit.commanderId
      ? view.state.commanders.find((candidate) => candidate.id === unit.commanderId)
      : null;
    const leader = commander ? commanderUnit(view.state, commander) : null;
    const inCommand = Boolean(activeCommanderFor(view.rules, view.state, unit));
    const career = unit.career.current ? content.careers.get(unit.career.current) : null;
    // The shape a unit is in, and whether it is holding: a formation that has
    // lost its neighbours contributes nothing, and the player has no other way
    // to find that out.
    const formation = unit.formation ? content.formations.tryGet(unit.formation) ?? null : null;
    const formationHolds = view.formationOptions.some((option) => option.current && option.eligible);
    return `<div class="unit-section">
      <h4>战场状态</h4>
      ${unit.statuses.length > 0
        ? unit.statuses.map((status) => {
          const art = this.art.resolve((provider) => provider.statusIcon?.(status.id)) ?? '';
          return `<div class="kv art-kv"><span>${art}<i>${escapeHtml(content.statuses.get(status.id).name)}</i></span><b>${status.remaining} 回合${status.stacks > 1 ? ` · ${status.stacks} 层` : ''}</b></div>`;
        }).join('')
        : '<div class="hint">无状态效果</div>'}
      <div class="kv"><span>军衔</span><b>${RANK_LABEL[unit.rank]}${view.rankNextThreshold === null ? '' : ` · ${unit.rankProgress}/${view.rankNextThreshold}`}</b></div>
      <div class="kv"><span>朝向</span><b>${escapeHtml(facingLabel(view, unit.facing))}</b></div>
      ${career ? `<div class="kv"><span>职业</span><b>${escapeHtml(career.name)} · 熟练度 ${unit.career.mastery[career.id] ?? 0}/${career.masteryThreshold}</b></div>` : ''}
      ${formation
        ? `<div class="kv"><span>阵形</span><b class="${formationHolds ? 'good' : 'bad'}">${escapeHtml(formation.name)}${formationHolds ? '' : ' · 缺少相邻友军'}</b></div>`
        : ''}
      ${accountSummary(view.resources, unitResource(unit)).map((account) => `<div class="kv"><span>单位资源</span><b>${escapeHtml(account)}</b></div>`).join('')}
      ${commander
        ? `<div class="kv"><span>编队 ${escapeHtml(commander.id)}</span><b class="${inCommand ? 'good' : 'bad'}">${leader ? (inCommand ? '光环生效' : '超出指挥范围') : '指挥官已离场'}</b></div>`
        : ''}
    </div>`;
  }

  /**
   * Boarding and stepping off, for the unit under command.
   *
   * Both lists show what is refused as well as what is offered, with the reason
   * on the button: "载具已满" and "该载具拒载这个兵种" are decisions the player
   * makes plans around, and hiding them makes a full transport look like a
   * transport that does not exist.
   */
  private renderTransport(view: HudView): string {
    const carriers = view.carrierOptions.length === 0 ? '' : `
      <div class="unit-section">
        <h4>登载</h4>
        <div class="cmd-list">${view.carrierOptions.map((option) => `<button
          class="btn ${option.eligible ? 'ghost' : 'disabled'}" ${option.eligible ? '' : 'disabled'}
          ${option.eligible ? `data-act="embark"` : ''} data-arg="${option.carrier.id}"
          title="${escapeHtml(option.reasons.join('；') || '登上这辆载具')}"
          >${escapeHtml(view.rules.content.units.get(option.carrier.type).name)}</button>`).join('')}</div>
      </div>`;
    const passengers = view.passengerOptions.length === 0 ? '' : `
      <div class="unit-section">
        <h4>卸载</h4>
        <div class="cmd-list">${view.passengerOptions.map((option) => `<button
          class="btn ${option.spots.length > 0 ? 'ghost' : 'disabled'}" ${option.spots.length > 0 ? '' : 'disabled'}
          ${option.spots.length > 0 ? `data-act="disembark"` : ''} data-arg="${option.unit.id}"
          title="${escapeHtml(option.spots.length > 0 ? `可在 ${option.spots.length} 格卸载` : '周围没有可以卸载的格子')}"
          >${escapeHtml(view.rules.content.units.get(option.unit.type).name)}</button>`).join('')}</div>
      </div>`;
    return `${carriers}${passengers}`;
  }

  /** Only offered for a unit the player may still give orders to. */
  private unitControls(view: HudView, unit: Unit): string {
    if (view.reactionUnit !== unit.id) return '';
    const stances = `<div class="cmd-list">
      ${view.rules.reactions.all()
        .map((stance) => `<button class="btn ${unit.reaction === stance.id ? 'primary' : 'ghost'}"
          data-act="reaction" data-arg="${escapeHtml(stance.id)}" title="${escapeHtml(stance.hint)}">${escapeHtml(stance.name)}</button>`)
        .join('')}
      ${view.rules.grids.get(view.state.rules.grid).directions
        .map(({ id }) => `<button class="btn ${unit.facing === id ? 'primary' : 'ghost'}" data-act="facing" data-arg="${escapeHtml(id)}">${escapeHtml(facingLabel(view, id))}</button>`)
        .join('')}
    </div>`;
    const formations = view.formationOptions.length === 0 ? '' : `
      <div class="unit-section">
        <h4>阵形</h4>
        <div class="cmd-list">${view.formationOptions.map((option) => `<button class="btn ${option.current ? 'primary' : option.eligible ? 'ghost' : 'disabled'}" ${option.eligible || option.current ? '' : 'disabled'}
          ${option.eligible || option.current ? `data-act="formation"` : ''} data-arg="${escapeHtml(option.current ? '' : option.formation.id)}"
          title="${escapeHtml(option.reasons.join('；') || `攻击 ×${option.formation.attackMultiplier} · 防御 ${option.formation.defenseDelta >= 0 ? '+' : ''}${option.formation.defenseDelta} · 移动 ${option.formation.movementDelta >= 0 ? '+' : ''}${option.formation.movementDelta}`)}">
          ${escapeHtml(option.formation.name)}${option.current ? ' · 解除' : ''}
        </button>`).join('')}</div>
      </div>`;
    const transport = this.renderTransport(view);
    if (view.careerOptions.length === 0) return `${stances}${formations}${transport}`;
    return `${stances}${formations}${transport}
      <div class="unit-section">
        <h4>职业树与转职</h4>
        <div class="cmd-list">${view.careerOptions.map((option) => `<button class="btn ${option.eligible ? 'ghost' : 'disabled'}" ${option.eligible ? '' : 'disabled'}
          ${option.eligible ? `data-act="career"` : ''} data-arg="${escapeHtml(option.career.id)}"
          title="${escapeHtml(option.reasons.join('；') || careerStanding(option))}">
          ${escapeHtml(option.career.name)} · T${option.career.tier}${option.mastered ? ' · 精通' : option.unlocked ? ' · 已解锁' : ''}
        </button>`).join('')}</div>
      </div>`;
  }

  /* ------------------------------------------------------------------- tile */

  private renderTile(view: HudView): string {
    if (!view.tile) return '';
    const state = view.state;
    const tile = idx(state.map, view.tile.x, view.tile.y);
    const terrain = view.rules.content.terrains.get(state.map.tiles[tile]);
    const owner = state.players.find((player) => player.id === state.map.owners[tile]);
    const costs = view.rules.content.movementProfiles.all()
      .map((profile) => {
        const cost = terrain.cost[profile.id];
        return `${profile.name} ${cost == null ? '—' : cost}`;
      })
      .join(' · ');
    // A row of facts rather than a column of labelled rows: this reads while the
    // cursor is moving, and a seven-row table at the foot of the field covered
    // the ground the player was pointing at.
    const facts = [
      `防御 ${pct(terrain.defense)}`,
      `海拔 ${state.map.elevation[tile]}`,
      terrain.cover === 'full' ? '全掩体' : terrain.cover === 'half' ? '半掩体' : '无掩体',
      terrain.heal ? `治疗 ${terrain.heal}/回合` : '',
      terrain.ownerTurnGrants.length > 0
        ? `产出 ${formatAmounts(view.resources, terrain.ownerTurnGrants)}`
        : '',
    ].filter(Boolean);
    return `<section class="plaque tile-plaque">
      <h3>${escapeHtml(terrain.name)} <span class="coord">${view.tile.x}, ${view.tile.y}</span>
        ${terrain.capturable
          ? `<em style="color:${owner?.color ?? PAL.neutral}">${escapeHtml(owner?.name ?? '中立')}</em>`
          : ''}</h3>
      <div class="fact-row">${facts.map((fact) => `<span>${escapeHtml(fact)}</span>`).join('')}</div>
      <div class="fact-row is-quiet"><span>${escapeHtml(costs)}</span></div>
      ${this.tileStructure(view, view.tile)}
    </section>`;
  }

  /**
   * The structure standing on this cell, if one does.
   *
   * A structure has hit points, an owner, cover and a reach, the rules let a
   * player shoot it, and a shipped chapter is won by destroying one — and the
   * interface said nothing about it anywhere. This is the only readout it has.
   */
  private tileStructure(view: HudView, at: Coord): string {
    const state = view.state.structures.find((entry) => sameCoord(entry, at));
    if (!state) return '';
    const definition = view.rules.content.structures.get(state.type);
    const holder = view.state.players.find((player) => player.id === state.owner);
    const facts = [
      definition.blocksMovement ? '阻挡通行' : '',
      definition.blocksVision ? '阻挡视线' : '',
      definition.cover === 'full' ? '全掩体' : definition.cover === 'half' ? '半掩体' : '',
      definition.repairable ? '可修复' : '',
      definition.targetable ? '' : '不可被攻击',
      state.disabled ? '已失效' : '',
    ].filter(Boolean);
    return `<div class="tile-structure">
      <span class="ts-name">${escapeHtml(definition.name)}</span>
      ${holder ? `<em style="color:${holder.color}">${escapeHtml(holder.name)}</em>` : ''}
      <b>${state.hp} / ${definition.maxHp}</b>
      ${hpBar(gaugeRatio(state.hp, definition.maxHp), 60)}
      ${facts.length > 0 ? `<div class="fact-row">${facts.map((fact) => `<span>${escapeHtml(fact)}</span>`).join('')}</div>` : ''}
    </div>`;
  }

  /* ------------------------------------------------------------- objectives */

  private renderObjectives(view: HudView): string {
    const state = view.state;
    const viewer = state.players.find((player) => player.controller === 'human') ?? state.players[0];
    return `<section class="plaque">
      <h3 class="art-heading">${this.art.resolve((provider) => provider.iconMarkup?.('C01-HUD-05')) ?? icon('flag')}<span>作战目标</span></h3>
      <ul class="obj-list">
        ${viewer.objectives
          .filter((objective) => !viewer.objectiveStates[objective.id]?.hidden)
          .map(
            (objective) =>
              `<li>${icon('flag')}<span>${escapeHtml(objective.label ?? describeObjective(view.rules.objectives, objective))}</span><em>${escapeHtml(
                objectiveProgress(view.rules, state, viewer.id, objective),
              )}</em></li>`,
          )
          .join('')}
      </ul>
      <div class="roster">
        ${state.players
          .map((player) => {
            const strength = state.units.filter((unit) => unit.owner === player.id).length;
            return `<div class="roster-row" style="--team:${player.color}">
              <span class="dot"></span>${escapeHtml(player.name)}
              <em>${strength} 单位 · ${escapeHtml(accountSummary(view.resources, playerResource(player)).join(' · '))}</em>
            </div>`;
          })
          .join('')}
      </div>
    </section>`;
  }

  /**
   * What just happened, as lines that arrive and settle back.
   *
   * Four rather than seven, and no heading: this is the field talking, not a
   * report to be read. It is its own region so that a pointer moving over the
   * ground does not rewrite it — which is what let the newest line animate in at
   * all, since the old panel rebuilt every message on every hover.
   */
  private renderLog(view: HudView): string {
    if (view.messages.length === 0) return '';
    return `<ul class="dispatch">${view.messages
      .slice(-4)
      .map((message) => `<li>${escapeHtml(message)}</li>`)
      .join('')}</ul>`;
  }

  /* ---------------------------------------------------------------- modals */

  private renderRecruit(view: HudView): string {
    if (!view.recruitAt) return '';
    const state = view.state;
    const buyer = player(state, state.currentPlayer);
    const options = recruitOptions(view.rules, state, view.recruitAt);
    const accounts = accountSummary(view.resources, playerResource(buyer));
    return `<div class="modal">
      <div class="modal-box">
        <div class="modal-head">
          <h2>征募单位</h2>
          <div class="funds">${icon('coin')}<b>${escapeHtml(accounts.join(' · '))}</b></div>
          <button class="btn ghost" data-act="cancel">✕</button>
        </div>
        <div class="recruit-grid">
          ${options.map((option) => this.recruitCard(view, option, buyer.color)).join('')}
        </div>
      </div>
    </div>`;
  }

  private recruitCard(view: HudView, option: RecruitOption, teamColor: string): string {
    const content = view.rules.content;
    const definition = content.units.get(option.unit);
    const weapons = definition.weapons.map((id) => content.weapons.get(id));
    const minimumRange = Math.min(...weapons.map((weapon) => weapon.minRange));
    const maximumRange = Math.max(...weapons.map((weapon) => weapon.maxRange));
    return `<button class="recruit-card ${option.affordable ? '' : 'disabled'}"
      ${option.affordable ? `data-act="recruit"` : ''} data-arg="${option.unit}">
      <div class="rc-art">${unitIcon(this.art, definition, teamColor, 46)}</div>
      <div>
        <div class="rc-name">${escapeHtml(definition.name)}<span class="rc-cost">${icon('coin')}${escapeHtml(formatAmounts(view.resources, option.costs))}</span></div>
        <div class="rc-stats">
          ${icon('sword')}${Math.max(...weapons.map((weapon) => weapon.power))} · ${icon('heart')}${definition.maxHp} · ${icon('boot')}${definition.movement}
          · ${minimumRange === maximumRange ? `射程 ${maximumRange}` : `射程 ${minimumRange}-${maximumRange}`}
        </div>
        <div class="rc-blurb">${escapeHtml(definition.blurb)}</div>
      </div>
    </button>`;
  }

  private renderGameOver(view: HudView): string {
    const state = view.state;
    if (state.phase !== 'over') return '';
    const viewer = state.players.find((player) => player.controller === 'human');
    const won = viewer ? state.winnerTeam === viewer.team : false;
    const continueButton = won && view.completionLabel
      ? `<button class="btn primary" data-act="continue">${icon('flag')} ${escapeHtml(view.completionLabel)}</button>`
      : '';
    return `<div class="modal">
      <div class="modal-box narrow ${won ? 'win' : 'lose'}">
        <h2>${state.winnerTeam === null ? '平局' : won ? '胜利' : '战败'}</h2>
        <p>${escapeHtml(state.endReason)}</p>
        <div class="modal-actions">
          ${continueButton}
          <button class="btn ${continueButton ? '' : 'primary'}" data-act="restart">${icon('play')} 再来一次</button>
          <button class="btn" data-act="exit">${icon('grid')} ${escapeHtml(view.exitLabel ?? '关卡列表')}</button>
        </div>
      </div>
    </div>`;
  }
}
