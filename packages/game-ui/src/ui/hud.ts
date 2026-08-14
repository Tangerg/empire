import { icon } from '../art/icons';
import type { ArtDirection } from '../art/direction';
import { portraitSvg } from '../art/portraits';
import { unitIcon } from '../art/units';
import { PAL } from '../art/palette';
import type { BattleRuleServices } from '@empire/battle-engine/action-system';
import type { CommandOption } from '@empire/battle-engine/actions';
import type { CareerOption } from '@empire/battle-engine/careers';
import type { TacticOption } from '@empire/battle-engine/commanders';
import type { CombatForecast } from '@empire/battle-engine/combat';
import type { CombatModifier } from '@empire/battle-engine/combat-modifiers';
import type { CombatPlan } from '@empire/battle-engine/combat-plan';
import { activeCommanderFor, commanderUnit } from '@empire/battle-engine/commanders';
import { idx } from '@empire/battle-engine/grid';
import { player, recruitOptions, type RecruitOption } from '@empire/battle-engine/state';
import { describeObjective, objectiveProgress } from '@empire/battle-engine/victory';
import type { Coord, Direction, GameState, PendingCast, ReactionStance, ResourceAmount, Unit, UnitDef } from '@empire/battle-engine/types';
import { SpellCastEntity } from '@empire/battle-engine/domain/spell-cast';
import {
  type BattleResourceSystem,
  type ResourceSubject,
  playerResource,
  unitResource,
  weaponResource,
} from '@empire/battle-engine/resources';
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
  exitLabel?: string;
  completionLabel?: string;
}

export interface HudHandlers {
  onCommand(ability: string): void;
  onTactic(key: string): void;
  onReaction(stance: ReactionStance): void;
  onFacing(facing: Direction): void;
  onCareer(career: string): void;
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
  const color = ratio > 0.6 ? PAL.hpGood : ratio > 0.3 ? PAL.hpMid : PAL.hpLow;
  return `<span class="bar" style="--w:${width}px">
    <i style="width:${Math.max(0, Math.min(1, ratio)) * 100}%;background:${color}"></i>
  </span>`;
}

/** Right-hand panel + top bar. Pure presentation, driven by HudView. */
export class Hud {
  readonly topEl = document.createElement('header');
  readonly panelEl = document.createElement('aside');
  readonly modalEl = document.createElement('div');

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
    /** The art this panel draws with; composed by the application root. */
    private readonly art: ArtDirection,
    private readonly handlers: HudHandlers,
  ) {
    this.topEl.className = 'topbar';
    this.panelEl.className = 'panel';
    this.modalEl.className = 'modal-root';
    for (const root of [this.topEl, this.panelEl, this.modalEl]) {
      root.addEventListener('click', (event) => {
        const control = (event.target as HTMLElement).closest('[data-act]') as HTMLElement | null;
        if (!control) return;
        this.intents[control.dataset.act ?? '']?.(control.dataset.arg ?? '');
      });
    }
  }

  /** The intents this HUD answers, so a test can compare them with the markup. */
  get handledIntents(): string[] {
    return Object.keys(this.intents);
  }

  render(view: HudView): void {
    this.topEl.innerHTML = this.renderTop(view);
    this.panelEl.innerHTML = [
      this.renderCommands(view),
      this.renderTactics(view),
      this.renderForecast(view),
      this.renderUnit(view),
      this.renderTile(view),
      this.renderObjectives(view),
      this.renderLog(view),
    ].join('');
    this.modalEl.innerHTML = this.renderRecruit(view) + this.renderGameOver(view);
  }

  /* -------------------------------------------------------------------- top */

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
            ${unitIcon(this.art, unit.type, owner?.color ?? PAL.neutral, active ? 26 : 20)}
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

  private renderTop(view: HudView): string {
    const state = view.state;
    const active = player(state, state.currentPlayer);
    const accounts = accountSummary(view.resources, playerResource(active));
    const turnLimit = state.rules.turnLimit ? ` / ${state.rules.turnLimit}` : '';
    return `
      <div class="topbar-left">
        <button class="btn ghost" data-act="exit" title="${escapeHtml(view.exitLabel ?? '返回关卡列表')}">${icon('grid')}</button>
        <div class="level-name">${escapeHtml(state.levelName)}</div>
        <div class="turn-chip">第 <b>${state.turn}</b>${turnLimit} 回合</div>
      </div>
      <div class="topbar-center">
        ${this.renderTurnOrder(view)}
        ${this.renderCasts(view)}
        <div class="player-chip" style="--team:${active.color}">
          <span class="dot"></span>
          <b>${escapeHtml(active.name)}</b>
          <span class="sub">${active.controller === 'human' ? '你的回合' : 'AI 行动中'}</span>
        </div>
        ${accounts.map((account) => `<div class="funds">${icon('coin')}<b>${escapeHtml(account)}</b></div>`).join('')}
      </div>
      <div class="topbar-right">
        <button class="btn ghost" data-act="zoom" data-arg="-0.15" title="缩小">−</button>
        <button class="btn ghost" data-act="zoom" data-arg="0.15" title="放大">+</button>
        <button class="btn ghost" data-act="undo" ${view.canUndo && !view.busy ? '' : 'disabled'} title="撤销 (U)">${icon('undo')}</button>
        ${this.renderSaveSlot(view)}
        <button class="btn ghost" data-act="restart" title="重新开始">${icon('play')}</button>
        <button class="btn primary" data-act="end" ${view.busy || active.controller !== 'human' ? 'disabled' : ''}>
          ${icon('hourglass')} 结束回合 <kbd>E</kbd>
        </button>
      </div>`;
  }

  /** Put the battle down, or pick it up where it was left. */
  private renderSaveSlot(view: HudView): string {
    if (!view.saves) return '';
    return `
      <button class="btn ghost" data-act="save" ${view.saves.canSave ? '' : 'disabled'} title="保存战斗进度">${icon('save')}</button>
      <button class="btn ghost" data-act="resume" ${view.saves.canResume ? '' : 'disabled'} title="读取战斗进度">${icon('flag')}</button>`;
  }

  /* --------------------------------------------------------------- commands */

  private renderCommands(view: HudView): string {
    if (view.targeting) {
      return `<section class="card accent">
        <h3>选择目标</h3>
        <p class="hint">点击高亮格中的目标，或右键 / Esc 取消。</p>
        <button class="btn wide" data-act="cancel">取消</button>
      </section>`;
    }
    if (!view.commands || view.commands.length === 0) {
      return `<section class="card">
        <h3>指令</h3>
        <p class="hint">${escapeHtml(view.hint)}</p>
      </section>`;
    }
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
    return `<section class="card accent">
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
    return `<section class="card accent">
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
    return `<section class="card forecast">
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
    return `<section class="card unit-card">
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
      ${portraitSvg(this.art, unit.type, owner?.color ?? PAL.neutral, 84)}
      <div class="unit-meta">
        <div class="unit-name">${escapeHtml(definition.name)}
          <span class="team-tag" style="--team:${owner?.color}">${escapeHtml(owner?.name ?? '中立')}</span>
        </div>
        <div class="unit-hp">${icon('heart')} ${unit.hp} / ${definition.maxHp} ${hpBar(unit.hp / definition.maxHp, 72)}</div>
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
      ${accountSummary(view.resources, unitResource(unit)).map((account) => `<div class="kv"><span>单位资源</span><b>${escapeHtml(account)}</b></div>`).join('')}
      ${commander
        ? `<div class="kv"><span>编队 ${escapeHtml(commander.id)}</span><b class="${inCommand ? 'good' : 'bad'}">${leader ? (inCommand ? '光环生效' : '超出指挥范围') : '指挥官已离场'}</b></div>`
        : ''}
    </div>`;
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
    if (view.careerOptions.length === 0) return stances;
    return `${stances}
      <div class="unit-section">
        <h4>职业树与转职</h4>
        <div class="cmd-list">${view.careerOptions.map((option) => `<button class="btn ${option.eligible ? 'ghost' : 'disabled'}" ${option.eligible ? '' : 'disabled'}
          ${option.eligible ? `data-act="career"` : ''} data-arg="${escapeHtml(option.career.id)}"
          title="${escapeHtml(option.reasons.join('；') || (option.unlocked ? '已解锁，可自由切换' : '满足进阶条件'))}">
          ${escapeHtml(option.career.name)} · T${option.career.tier}${option.unlocked ? ' · 已解锁' : ''}
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
    return `<section class="card tile-card">
      <h3>${escapeHtml(terrain.name)} <span class="coord">(${view.tile.x}, ${view.tile.y})</span></h3>
      <div class="kv"><span>防御加成</span><b>${pct(terrain.defense)}</b></div>
      <div class="kv"><span>海拔</span><b>${state.map.elevation[tile]}</b></div>
      <div class="kv"><span>基础掩体</span><b>${terrain.cover === 'full' ? '全掩体' : terrain.cover === 'half' ? '半掩体' : '无'}</b></div>
      ${terrain.capturable ? `<div class="kv"><span>归属</span><b style="color:${owner?.color ?? PAL.neutral}">${escapeHtml(owner?.name ?? '中立')}</b></div>` : ''}
      ${terrain.ownerTurnGrants.length > 0 ? `<div class="kv"><span>回合产出</span><b>${escapeHtml(formatAmounts(view.resources, terrain.ownerTurnGrants))}</b></div>` : ''}
      ${terrain.heal ? `<div class="kv"><span>治疗</span><b>${terrain.heal}/回合</b></div>` : ''}
      <div class="kv wrap"><span>移动消耗</span><b>${costs}</b></div>
    </section>`;
  }

  /* ------------------------------------------------------------- objectives */

  private renderObjectives(view: HudView): string {
    const state = view.state;
    const viewer = state.players.find((player) => player.controller === 'human') ?? state.players[0];
    return `<section class="card">
      <h3 class="art-heading">${this.art.resolve((provider) => provider.iconMarkup?.('C01-HUD-05')) ?? icon('flag')}<span>作战目标</span></h3>
      <ul class="obj-list">
        ${viewer.objectives
          .filter((objective) => !viewer.objectiveStates[objective.id!]?.hidden)
          .map(
            (objective) =>
              `<li>${icon('flag')}<span>${escapeHtml(objective.label ?? describeObjective(objective, view.rules.objectives))}</span><em>${escapeHtml(
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

  private renderLog(view: HudView): string {
    if (view.messages.length === 0) return '';
    return `<section class="card log">
      <h3>战报</h3>
      <ul>${view.messages.slice(-7).map((m) => `<li>${escapeHtml(m)}</li>`).join('')}</ul>
    </section>`;
  }

  /* ---------------------------------------------------------------- modals */

  private renderRecruit(view: HudView): string {
    if (!view.recruitAt) return '';
    const state = view.state;
    const buyer = player(state, state.currentPlayer);
    const options = recruitOptions(state, view.recruitAt, view.resources, view.rules.content);
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
      <div class="rc-art">${unitIcon(this.art, option.unit, teamColor, 46)}</div>
      <div class="rc-body">
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
