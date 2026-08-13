import { icon } from '../art/icons';
import { resolveArt } from '../art/ports';
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
import { recruitOptions } from '@empire/battle-engine/state';
import { describeObjective, objectiveProgress } from '@empire/battle-engine/victory';
import type { Coord, Direction, GameState, ReactionStance, ResourceAmount, Unit } from '@empire/battle-engine/types';
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
  forecast: { plan: CombatPlan; fc: CombatForecast; attacker: Unit; defender: Unit; recipient: Unit } | null;
  commands: CommandOption[] | null;
  tactics: Array<TacticOption & { key: string; commander: string }>;
  reactionUnit: number | null;
  /** Upcoming actor turns for per-unit orders; empty for side turns. */
  turnOrder: { units: Unit[]; activeUnit: number | null };
  rankNextThreshold: number | null;
  careerOptions: CareerOption[];
  /** Ability whose target we are picking, if any. */
  targeting: string | null;
  recruitAt: Coord | null;
  hint: string;
  busy: boolean;
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
  onRecruit(unit: string): void;
  onExit(): void;
  onContinue(): void;
  onZoom(delta: number): void;
}

const pct = (n: number) => `${Math.round(n * 100)}%`;

function resourceName(resources: BattleResourceSystem, id: string): string {
  try {
    return resources.adapters.get(id).name;
  } catch {
    return id;
  }
}

function formatAmounts(resources: BattleResourceSystem, amounts: readonly ResourceAmount[]): string {
  return amounts.length === 0
    ? '无'
    : amounts.map((amount) => `${resourceName(resources, amount.resource)} ${amount.amount}`).join(' · ');
}

function accountSummary(resources: BattleResourceSystem, subject: ResourceSubject): string[] {
  return resources.adapters.ids().flatMap((id) => {
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

const MODIFIER_SOURCE_LABEL: Record<CombatModifier['source'], string> = {
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
  extension: '扩展',
};

const REACTION_LABEL: Record<ReactionStance, string> = {
  counter: '反击',
  guard: '防御',
  support: '援护',
  conserve: '节制',
};

const RANK_LABEL = ['新兵', '老兵', '精英'] as const;
const FACING_LABEL: Record<Direction, string> = { north: '北 ↑', east: '东 →', south: '南 ↓', west: '西 ←' };

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

function modifierList(modifiers: CombatModifier[]): string {
  return modifiers.map((modifier) =>
    `<li class="${modifierClass(modifier)}"><span class="modifier-source">${MODIFIER_SOURCE_LABEL[modifier.source]}</span> ${escapeHtml(modifier.label)} <b>${modifierValue(modifier)}</b></li>`,
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

  constructor(private readonly handlers: HudHandlers) {
    this.topEl.className = 'topbar';
    this.panelEl.className = 'panel';
    this.modalEl.className = 'modal-root';
    const delegate = (root: HTMLElement) => {
      root.addEventListener('click', (ev) => {
        const el = (ev.target as HTMLElement).closest('[data-act]') as HTMLElement | null;
        if (!el) return;
        const act = el.dataset.act!;
        const arg = el.dataset.arg ?? '';
        switch (act) {
          case 'command':
            this.handlers.onCommand(arg);
            break;
          case 'tactic':
            this.handlers.onTactic(arg);
            break;
          case 'reaction':
            this.handlers.onReaction(arg as ReactionStance);
            break;
          case 'facing':
            this.handlers.onFacing(arg as Direction);
            break;
          case 'career':
            this.handlers.onCareer(arg);
            break;
          case 'cancel':
            this.handlers.onCancel();
            break;
          case 'end':
            this.handlers.onEndTurn();
            break;
          case 'undo':
            this.handlers.onUndo();
            break;
          case 'restart':
            this.handlers.onRestart();
            break;
          case 'recruit':
            this.handlers.onRecruit(arg);
            break;
          case 'exit':
            this.handlers.onExit();
            break;
          case 'continue':
            this.handlers.onContinue();
            break;
          case 'zoom':
            this.handlers.onZoom(Number(arg));
            break;
        }
      });
    };
    delegate(this.topEl);
    delegate(this.panelEl);
    delegate(this.modalEl);
  }

  render(v: HudView): void {
    this.topEl.innerHTML = this.renderTop(v);
    this.panelEl.innerHTML = [
      this.renderCommands(v),
      this.renderTactics(v),
      this.renderForecast(v),
      this.renderUnit(v),
      this.renderTile(v),
      this.renderObjectives(v),
      this.renderLog(v),
    ].join('');
    this.modalEl.innerHTML = this.renderRecruit(v) + this.renderGameOver(v);
  }

  /* -------------------------------------------------------------------- top */

  /**
   * Initiative strip. Deliberately absent under side turns, where "who acts
   * next" is the whole army and a strip would say nothing.
   */
  private renderTurnOrder(v: HudView): string {
    const upcoming = v.turnOrder.units;
    if (upcoming.length === 0) return '';
    const content = v.rules.content;
    return `<div class="order-strip" title="行动序">
      ${upcoming
        .map((unit, index) => {
          const owner = v.state.players.find((entry) => entry.id === unit.owner);
          const definition = content.units.get(unit.type);
          const active = unit.id === v.turnOrder.activeUnit && index === 0;
          return `<span class="order-slot ${active ? 'is-active' : ''}" style="--team:${owner?.color ?? PAL.neutral}"
            title="${escapeHtml(definition.name)} · ${escapeHtml(owner?.name ?? '')}">
            ${unitIcon(unit.type, owner?.color ?? PAL.neutral, active ? 26 : 20)}
          </span>`;
        })
        .join('')}
    </div>`;
  }

  private renderTop(v: HudView): string {
    const s = v.state;
    const p = s.players.find((x) => x.id === s.currentPlayer)!;
    const accounts = accountSummary(v.resources, playerResource(p));
    const turnLimit = s.rules.turnLimit ? ` / ${s.rules.turnLimit}` : '';
    return `
      <div class="topbar-left">
        <button class="btn ghost" data-act="exit" title="${escapeHtml(v.exitLabel ?? '返回关卡列表')}">${icon('grid')}</button>
        <div class="level-name">${escapeHtml(s.levelName)}</div>
        <div class="turn-chip">第 <b>${s.turn}</b>${turnLimit} 回合</div>
      </div>
      <div class="topbar-center">
        ${this.renderTurnOrder(v)}
        <div class="player-chip" style="--team:${p.color}">
          <span class="dot"></span>
          <b>${escapeHtml(p.name)}</b>
          <span class="sub">${p.controller === 'human' ? '你的回合' : 'AI 行动中'}</span>
        </div>
        ${accounts.map((account) => `<div class="funds">${icon('coin')}<b>${escapeHtml(account)}</b></div>`).join('')}
      </div>
      <div class="topbar-right">
        <button class="btn ghost" data-act="zoom" data-arg="-0.15" title="缩小">−</button>
        <button class="btn ghost" data-act="zoom" data-arg="0.15" title="放大">+</button>
        <button class="btn ghost" data-act="undo" ${v.canUndo && !v.busy ? '' : 'disabled'} title="撤销 (U)">${icon('undo')}</button>
        <button class="btn ghost" data-act="restart" title="重新开始">${icon('play')}</button>
        <button class="btn primary" data-act="end" ${v.busy || p.controller !== 'human' ? 'disabled' : ''}>
          ${icon('hourglass')} 结束回合 <kbd>E</kbd>
        </button>
      </div>`;
  }

  /* --------------------------------------------------------------- commands */

  private renderCommands(v: HudView): string {
    if (v.targeting) {
      return `<section class="card accent">
        <h3>选择目标</h3>
        <p class="hint">点击高亮格中的目标，或右键 / Esc 取消。</p>
        <button class="btn wide" data-act="cancel">取消</button>
      </section>`;
    }
    if (!v.commands || v.commands.length === 0) {
      return `<section class="card">
        <h3>指令</h3>
        <p class="hint">${escapeHtml(v.hint)}</p>
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
      return (command.weapon
        ? resolveArt((provider) => provider.weaponIcon?.(command.weapon!))
        : resolveArt((provider) => provider.abilityIcon?.(command.ability)))
        ?? icon(iconOf[command.ability] ?? 'crosshair');
    };
    return `<section class="card accent">
      <h3>指令</h3>
      <div class="cmd-list">
        ${v.commands
          .map(
            (c) => `<button class="btn cmd" data-act="command" data-arg="${escapeHtml(c.key)}" title="${escapeHtml(c.hint)}">
              ${commandIcon(c)}<span>${escapeHtml(c.name)}</span>
              ${keyOf[c.ability] ? `<kbd>${keyOf[c.ability]}</kbd>` : ''}
            </button>`,
          )
          .join('')}
      </div>
      <button class="btn wide ghost" data-act="cancel">取消 <kbd>Esc</kbd></button>
    </section>`;
  }

  private renderTactics(v: HudView): string {
    if (v.targeting || v.commands || v.tactics.length === 0) return '';
    return `<section class="card accent">
      <h3>指挥战术</h3>
      <div class="cmd-list">
        ${v.tactics
          .map(
            (tactic) => `<button class="btn cmd" data-act="tactic" data-arg="${escapeHtml(tactic.key)}">
              ${resolveArt((provider) => provider.abilityIcon?.(tactic.id)) ?? icon('flag')}<span>${escapeHtml(tactic.name)}</span><em>${escapeHtml(formatAmounts(v.resources, tactic.costs))}</em>
            </button>`,
          )
          .join('')}
      </div>
    </section>`;
  }

  /* --------------------------------------------------------------- forecast */

  private renderForecast(v: HudView): string {
    if (!v.forecast) return '';
    const { plan, fc, attacker, defender, recipient } = v.forecast;
    const content = v.rules.content;
    const aDef = content.units.get(attacker.type);
    const dDef = content.units.get(recipient.type);
    return `<section class="card forecast">
      <h3>战斗预测</h3>
      <div class="fc-row">
        <span class="fc-name">${escapeHtml(aDef.name)}</span>
        <span class="fc-arrow">${icon('sword')}</span>
        <span class="fc-name">${escapeHtml(content.units.get(defender.type).name)}</span>
      </div>
      ${fc.interceptor ? `<div class="hint">${escapeHtml(dDef.name)} 将进行援护并承受伤害</div>` : ''}
      ${fc.reaction?.stance === 'guard' ? '<div class="hint">目标将触发防御姿态</div>' : ''}
      ${plan.unitHits.length + plan.structureHits.length > 1
        ? `<div class="hint">范围攻击还将波及 ${plan.unitHits.length - 1} 个单位、${plan.structureHits.length} 个结构</div>`
        : ''}
      ${plan.supportAttack
        ? `<div class="hint">援护攻击预计追加 ${plan.supportAttack.damage.damage} 点伤害</div>`
        : ''}
      <div class="fc-line">
        <span>造成伤害</span>
        <b class="dmg">${fc.strike.damage}</b>
        <span class="fc-hp">${recipient.hp} → ${fc.recipientHpAfter}</span>
      </div>
      ${hpBar(fc.recipientHpAfter / dDef.maxHp)}
      <div class="fc-line">
        <span>遭到反击</span>
        <b class="${fc.counter ? 'dmg' : 'none'}">${fc.counter ? fc.counter.damage : '无'}</b>
        <span class="fc-hp">${attacker.hp} → ${fc.attackerHpAfter}</span>
      </div>
      ${hpBar(fc.attackerHpAfter / aDef.maxHp)}
      <div class="fc-chain-title">攻击修正链 · ${content.damageTypes.get(fc.strike.damageType).name} → ${content.armorClasses.get(dDef.armorClass).name}</div>
      <ul class="fc-detail">
        ${modifierList(fc.strike.modifiers)}
        <li>最终减伤上限后 <b>${pct(fc.strike.mitigation)}</b></li>
        ${fc.recipientDies ? '<li class="good">可以击杀伤害承担者</li>' : ''}
        ${fc.attackerDies ? '<li class="bad">反击会导致我方阵亡</li>' : ''}
      </ul>
      ${fc.counter ? `<div class="fc-chain-title">反击修正链</div><ul class="fc-detail">${modifierList(fc.counter.modifiers)}</ul>` : ''}
    </section>`;
  }

  /* ------------------------------------------------------------------- unit */

  private renderUnit(v: HudView): string {
    const u = v.inspect;
    if (!u) return '';
    const content = v.rules.content;
    const def = content.units.get(u.type);
    const weapons = def.weapons.map((id) => content.weapons.get(id));
    const maximumPower = Math.max(...weapons.map((weapon) => weapon.power));
    const minimumRange = Math.min(...weapons.map((weapon) => weapon.minRange));
    const maximumRange = Math.max(...weapons.map((weapon) => weapon.maxRange));
    const damageTypes = [...new Set(weapons.map((weapon) => content.damageTypes.get(weapon.damageType).name))].join(' / ');
    const owner = v.state.players.find((p) => p.id === u.owner);
    const ratio = u.hp / def.maxHp;
    const commander = u.commanderId
      ? v.state.commanders.find((candidate) => candidate.id === u.commanderId)
      : null;
    const leader = commander ? commanderUnit(v.state, commander) : null;
    const commandActive = Boolean(activeCommanderFor(v.state, u));
    return `<section class="card unit-card">
      <div class="unit-head">
        ${portraitSvg(u.type, owner?.color ?? PAL.neutral, 84)}
        <div class="unit-meta">
          <div class="unit-name">${escapeHtml(def.name)}
            <span class="team-tag" style="--team:${owner?.color}">${escapeHtml(owner?.name ?? '中立')}</span>
          </div>
          <div class="unit-hp">${icon('heart')} ${u.hp} / ${def.maxHp} ${hpBar(ratio, 72)}</div>
          <div class="unit-blurb">${escapeHtml(def.blurb)}</div>
        </div>
      </div>
      <div class="stat-grid">
        <div><span>最高威力</span><b>${maximumPower}</b></div>
        <div><span>减伤</span><b>${pct(def.defense)}</b></div>
        <div><span>移动</span><b>${def.movement}</b></div>
        <div><span>射程</span><b>${minimumRange === maximumRange ? minimumRange : `${minimumRange}-${maximumRange}`}</b></div>
        <div><span>伤害</span><b>${damageTypes}</b></div>
        <div><span>护甲</span><b>${content.armorClasses.get(def.armorClass).name}</b></div>
        <div><span>移动型</span><b>${content.movementProfiles.get(def.movementClass).name}</b></div>
        <div><span>战力价值</span><b>${def.value}</b></div>
      </div>
      <div class="tag-row">
        ${weapons.some((weapon) => weapon.moveAndAttack) ? '' : '<span class="tag warn">移动后无法攻击</span>'}
        ${def.abilities.includes('capture') ? '<span class="tag">可占领</span>' : '<span class="tag dim">不可占领</span>'}
        ${def.abilities.includes('heal') ? '<span class="tag good">可治疗</span>' : ''}
        <span class="tag">反应：${REACTION_LABEL[u.reaction]}</span>
        ${u.done ? '<span class="tag dim">已行动</span>' : ''}
      </div>
      <div class="unit-section">
        <h4>武器与资源</h4>
        ${def.weapons.map((id) => {
          const weapon = content.weapons.get(id);
          const runtime = u.weaponState[id];
          const range = weapon.minRange === weapon.maxRange
            ? String(weapon.minRange)
            : `${weapon.minRange}-${weapon.maxRange}`;
          const cooldown = runtime.cooldownRemaining > 0 ? ` · 冷却 ${runtime.cooldownRemaining}` : '';
          const accounts = accountSummary(v.resources, weaponResource(u, id));
          const requirements = weapon.resourceRequirements.length > 0
            ? ` · 需要 ${formatAmounts(v.resources, weapon.resourceRequirements)}`
            : '';
          const costs = weapon.resourceCosts.length > 0
            ? ` · 消耗 ${formatAmounts(v.resources, weapon.resourceCosts)}`
            : '';
          const state = accounts.length > 0 ? accounts.join(' · ') : '无限制';
          const art = resolveArt((provider) => provider.weaponIcon?.(id)) ?? '';
          return `<div class="kv wrap art-kv"><span>${art}<i>${escapeHtml(weapon.name)} · ${content.damageTypes.get(weapon.damageType).name} ${weapon.power} · 射程 ${range}</i></span><b>${escapeHtml(state)}${cooldown}${escapeHtml(requirements)}${escapeHtml(costs)}</b></div>`;
        }).join('')}
      </div>
      <div class="unit-section">
        <h4>战场状态</h4>
        ${u.statuses.length > 0
          ? u.statuses.map((status) => {
            const art = resolveArt((provider) => provider.statusIcon?.(status.id)) ?? '';
            return `<div class="kv art-kv"><span>${art}<i>${escapeHtml(content.statuses.get(status.id).name)}</i></span><b>${status.remaining} 回合${status.stacks > 1 ? ` · ${status.stacks} 层` : ''}</b></div>`;
          }).join('')
          : '<div class="hint">无状态效果</div>'}
        <div class="kv"><span>军衔</span><b>${RANK_LABEL[u.rank]}${v.rankNextThreshold === null ? '' : ` · ${u.rankProgress}/${v.rankNextThreshold}`}</b></div>
        <div class="kv"><span>朝向</span><b>${FACING_LABEL[u.facing]}</b></div>
        ${u.career.current ? `<div class="kv"><span>职业</span><b>${escapeHtml(content.careers.get(u.career.current).name)} · 熟练度 ${u.career.mastery[u.career.current] ?? 0}/${content.careers.get(u.career.current).masteryThreshold}</b></div>` : ''}
        ${accountSummary(v.resources, unitResource(u)).map((account) => `<div class="kv"><span>单位资源</span><b>${escapeHtml(account)}</b></div>`).join('')}
        ${commander
          ? `<div class="kv"><span>编队 ${escapeHtml(commander.id)}</span><b class="${commandActive ? 'good' : 'bad'}">${leader ? (commandActive ? '光环生效' : '超出指挥范围') : '指挥官已离场'}</b></div>`
          : ''}
      </div>
      ${v.reactionUnit === u.id ? `<div class="cmd-list">
        ${([
          ['counter', '反击'],
          ['guard', '防御'],
          ['support', '援护'],
          ['conserve', '节制'],
        ] as const)
          .map(([stance, label]) => `<button class="btn ${u.reaction === stance ? 'primary' : 'ghost'}" data-act="reaction" data-arg="${stance}">${label}</button>`)
          .join('')}
        ${(['north', 'east', 'south', 'west'] as const)
          .map((facing) => `<button class="btn ${u.facing === facing ? 'primary' : 'ghost'}" data-act="facing" data-arg="${facing}">${FACING_LABEL[facing]}</button>`)
          .join('')}
      </div>` : ''}
      ${v.reactionUnit === u.id && v.careerOptions.length > 0 ? `<div class="unit-section">
        <h4>职业树与转职</h4>
        <div class="cmd-list">${v.careerOptions.map((option) => `<button class="btn ${option.eligible ? 'ghost' : 'disabled'}" ${option.eligible ? '' : 'disabled'}
          data-act="${option.eligible ? 'career' : 'noop'}" data-arg="${escapeHtml(option.career.id)}"
          title="${escapeHtml(option.reasons.join('；') || (option.unlocked ? '已解锁，可自由切换' : '满足进阶条件'))}">
          ${escapeHtml(option.career.name)} · T${option.career.tier}${option.unlocked ? ' · 已解锁' : ''}
        </button>`).join('')}</div>
      </div>` : ''}
    </section>`;
  }

  /* ------------------------------------------------------------------- tile */

  private renderTile(v: HudView): string {
    if (!v.tile) return '';
    const s = v.state;
    const i = idx(s.map, v.tile.x, v.tile.y);
    const t = v.rules.content.terrains.get(s.map.tiles[i]);
    const owner = s.players.find((p) => p.id === s.map.owners[i]);
    const costs = v.rules.content.movementProfiles.all()
      .map((profile) => {
        const cost = t.cost[profile.id];
        return `${profile.name} ${cost == null ? '—' : cost}`;
      })
      .join(' · ');
    return `<section class="card tile-card">
      <h3>${escapeHtml(t.name)} <span class="coord">(${v.tile.x}, ${v.tile.y})</span></h3>
      <div class="kv"><span>防御加成</span><b>${pct(t.defense)}</b></div>
      <div class="kv"><span>海拔</span><b>${s.map.elevation[i]}</b></div>
      <div class="kv"><span>基础掩体</span><b>${t.cover === 'full' ? '全掩体' : t.cover === 'half' ? '半掩体' : '无'}</b></div>
      ${t.capturable ? `<div class="kv"><span>归属</span><b style="color:${owner?.color ?? PAL.neutral}">${escapeHtml(owner?.name ?? '中立')}</b></div>` : ''}
      ${t.ownerTurnGrants.length > 0 ? `<div class="kv"><span>回合产出</span><b>${escapeHtml(formatAmounts(v.resources, t.ownerTurnGrants))}</b></div>` : ''}
      ${t.heal ? `<div class="kv"><span>治疗</span><b>${t.heal}/回合</b></div>` : ''}
      <div class="kv wrap"><span>移动消耗</span><b>${costs}</b></div>
    </section>`;
  }

  /* ------------------------------------------------------------- objectives */

  private renderObjectives(v: HudView): string {
    const s = v.state;
    const me = s.players.find((p) => p.controller === 'human') ?? s.players[0];
    return `<section class="card">
      <h3 class="art-heading">${resolveArt((provider) => provider.iconMarkup?.('C01-HUD-05')) ?? icon('flag')}<span>作战目标</span></h3>
      <ul class="obj-list">
        ${me.objectives
          .filter((objective) => !me.objectiveStates[objective.id!]?.hidden)
          .map(
            (o) =>
              `<li>${icon('flag')}<span>${escapeHtml(o.label ?? describeObjective(o, v.rules.objectives))}</span><em>${escapeHtml(
                objectiveProgress(v.rules, s, me.id, o),
              )}</em></li>`,
          )
          .join('')}
      </ul>
      <div class="roster">
        ${s.players
          .map((p) => {
            const n = s.units.filter((u) => u.owner === p.id).length;
            return `<div class="roster-row" style="--team:${p.color}">
              <span class="dot"></span>${escapeHtml(p.name)}
              <em>${n} 单位 · ${escapeHtml(accountSummary(v.resources, playerResource(p)).join(' · '))}</em>
            </div>`;
          })
          .join('')}
      </div>
    </section>`;
  }

  private renderLog(v: HudView): string {
    if (v.messages.length === 0) return '';
    return `<section class="card log">
      <h3>战报</h3>
      <ul>${v.messages.slice(-7).map((m) => `<li>${escapeHtml(m)}</li>`).join('')}</ul>
    </section>`;
  }

  /* ---------------------------------------------------------------- modals */

  private renderRecruit(v: HudView): string {
    if (!v.recruitAt) return '';
    const s = v.state;
    const p = s.players.find((x) => x.id === s.currentPlayer)!;
    const options = recruitOptions(s, v.recruitAt, v.resources, v.rules.content);
    const accounts = accountSummary(v.resources, playerResource(p));
    return `<div class="modal">
      <div class="modal-box">
        <div class="modal-head">
          <h2>征募单位</h2>
          <div class="funds">${icon('coin')}<b>${escapeHtml(accounts.join(' · '))}</b></div>
          <button class="btn ghost" data-act="cancel">✕</button>
        </div>
        <div class="recruit-grid">
          ${options
            .map((o) => {
              const def = v.rules.content.units.get(o.unit);
              const weapons = def.weapons.map((id) => v.rules.content.weapons.get(id));
              const maximumPower = Math.max(...weapons.map((weapon) => weapon.power));
              const minimumRange = Math.min(...weapons.map((weapon) => weapon.minRange));
              const maximumRange = Math.max(...weapons.map((weapon) => weapon.maxRange));
              return `<button class="recruit-card ${o.affordable ? '' : 'disabled'}"
                data-act="${o.affordable ? 'recruit' : 'noop'}" data-arg="${o.unit}">
                <div class="rc-art">${unitIcon(o.unit, p.color, 46)}</div>
                <div class="rc-body">
                  <div class="rc-name">${escapeHtml(def.name)}<span class="rc-cost">${icon('coin')}${escapeHtml(formatAmounts(v.resources, o.costs))}</span></div>
                  <div class="rc-stats">
                    ${icon('sword')}${maximumPower} · ${icon('heart')}${def.maxHp} · ${icon('boot')}${def.movement}
                    · ${minimumRange === maximumRange ? `射程 ${maximumRange}` : `射程 ${minimumRange}-${maximumRange}`}
                  </div>
                  <div class="rc-blurb">${escapeHtml(def.blurb)}</div>
                </div>
              </button>`;
            })
            .join('')}
        </div>
      </div>
    </div>`;
  }

  private renderGameOver(v: HudView): string {
    const s = v.state;
    if (s.phase !== 'over') return '';
    const me = s.players.find((p) => p.controller === 'human');
    const won = me ? s.winnerTeam === me.team : false;
    const continueButton = won && v.completionLabel
      ? `<button class="btn primary" data-act="continue">${icon('flag')} ${escapeHtml(v.completionLabel)}</button>`
      : '';
    return `<div class="modal">
      <div class="modal-box narrow ${won ? 'win' : 'lose'}">
        <h2>${s.winnerTeam === null ? '平局' : won ? '胜利' : '战败'}</h2>
        <p>${escapeHtml(s.endReason)}</p>
        <div class="modal-actions">
          ${continueButton}
          <button class="btn ${continueButton ? '' : 'primary'}" data-act="restart">${icon('play')} 再来一次</button>
          <button class="btn" data-act="exit">${icon('grid')} ${escapeHtml(v.exitLabel ?? '关卡列表')}</button>
        </div>
      </div>
    </div>`;
  }
}
