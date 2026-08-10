import { icon } from '../art/icons';
import { portraitSvg } from '../art/portraits';
import { unitIcon } from '../art/units';
import { PAL } from '../art/palette';
import type { CommandOption } from '../core/actions';
import type { CombatForecast } from '../core/combat';
import { Terrains } from '../core/data/terrain';
import {
  ARMOR_LABEL,
  DAMAGE_TYPE_LABEL,
  MOVEMENT_LABEL,
  unitDef,
} from '../core/data/units';
import { idx } from '../core/grid';
import { recruitOptions } from '../core/state';
import { describeObjective, objectiveProgress } from '../core/victory';
import type { Coord, GameState, Unit } from '../core/types';

export interface HudView {
  state: GameState;
  /** Unit under the cursor or currently selected. */
  inspect: Unit | null;
  tile: Coord | null;
  forecast: { fc: CombatForecast; attacker: Unit; defender: Unit } | null;
  commands: CommandOption[] | null;
  /** Ability whose target we are picking, if any. */
  targeting: string | null;
  recruitAt: Coord | null;
  hint: string;
  busy: boolean;
  canUndo: boolean;
  messages: string[];
}

export interface HudHandlers {
  onCommand(ability: string): void;
  onCancel(): void;
  onEndTurn(): void;
  onUndo(): void;
  onRestart(): void;
  onRecruit(unit: string): void;
  onExit(): void;
  onZoom(delta: number): void;
}

const pct = (n: number) => `${Math.round(n * 100)}%`;

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
      this.renderForecast(v),
      this.renderUnit(v),
      this.renderTile(v),
      this.renderObjectives(v),
      this.renderLog(v),
    ].join('');
    this.modalEl.innerHTML = this.renderRecruit(v) + this.renderGameOver(v);
  }

  /* -------------------------------------------------------------------- top */

  private renderTop(v: HudView): string {
    const s = v.state;
    const p = s.players.find((x) => x.id === s.currentPlayer)!;
    const turnLimit = s.rules.turnLimit ? ` / ${s.rules.turnLimit}` : '';
    return `
      <div class="topbar-left">
        <button class="btn ghost" data-act="exit" title="返回关卡列表">${icon('grid')}</button>
        <div class="level-name">${escapeHtml(s.levelName)}</div>
        <div class="turn-chip">第 <b>${s.turn}</b>${turnLimit} 回合</div>
      </div>
      <div class="topbar-center">
        <div class="player-chip" style="--team:${p.color}">
          <span class="dot"></span>
          <b>${escapeHtml(p.name)}</b>
          <span class="sub">${p.controller === 'human' ? '你的回合' : 'AI 行动中'}</span>
        </div>
        <div class="funds">${icon('coin')}<b>${p.funds}</b></div>
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
    return `<section class="card accent">
      <h3>指令</h3>
      <div class="cmd-list">
        ${v.commands
          .map(
            (c) => `<button class="btn cmd" data-act="command" data-arg="${c.ability}" title="${escapeHtml(c.hint)}">
              ${icon(iconOf[c.ability] ?? 'crosshair')}<span>${escapeHtml(c.name)}</span>
              ${keyOf[c.ability] ? `<kbd>${keyOf[c.ability]}</kbd>` : ''}
            </button>`,
          )
          .join('')}
      </div>
      <button class="btn wide ghost" data-act="cancel">取消 <kbd>Esc</kbd></button>
    </section>`;
  }

  /* --------------------------------------------------------------- forecast */

  private renderForecast(v: HudView): string {
    if (!v.forecast) return '';
    const { fc, attacker, defender } = v.forecast;
    const aDef = unitDef(attacker.type);
    const dDef = unitDef(defender.type);
    const eff = fc.strike.effectiveness;
    const effClass = eff > 1.05 ? 'good' : eff < 0.95 ? 'bad' : '';
    return `<section class="card forecast">
      <h3>战斗预测</h3>
      <div class="fc-row">
        <span class="fc-name">${escapeHtml(aDef.name)}</span>
        <span class="fc-arrow">${icon('sword')}</span>
        <span class="fc-name">${escapeHtml(dDef.name)}</span>
      </div>
      <div class="fc-line">
        <span>造成伤害</span>
        <b class="dmg">${fc.strike.damage}</b>
        <span class="fc-hp">${defender.hp} → ${fc.defenderHpAfter}</span>
      </div>
      ${hpBar(fc.defenderHpAfter / dDef.maxHp)}
      <div class="fc-line">
        <span>遭到反击</span>
        <b class="${fc.counter ? 'dmg' : 'none'}">${fc.counter ? fc.counter.damage : '无'}</b>
        <span class="fc-hp">${attacker.hp} → ${fc.attackerHpAfter}</span>
      </div>
      ${hpBar(fc.attackerHpAfter / aDef.maxHp)}
      <ul class="fc-detail">
        <li class="${effClass}">属性克制 ×${eff.toFixed(2)}（${DAMAGE_TYPE_LABEL[aDef.damageType]} → ${ARMOR_LABEL[dDef.armorClass]}）</li>
        <li>攻击方状态 ×${fc.strike.strength.toFixed(2)}</li>
        <li>目标减伤 ${pct(fc.strike.mitigation)}（地形 ${pct(fc.strike.terrainDefense)} + 自身 ${pct(fc.strike.unitDefense)}）</li>
        ${fc.defenderDies ? '<li class="good">可以击杀</li>' : ''}
        ${fc.attackerDies ? '<li class="bad">反击会导致我方阵亡</li>' : ''}
      </ul>
    </section>`;
  }

  /* ------------------------------------------------------------------- unit */

  private renderUnit(v: HudView): string {
    const u = v.inspect;
    if (!u) return '';
    const def = unitDef(u.type);
    const owner = v.state.players.find((p) => p.id === u.owner);
    const ratio = u.hp / def.maxHp;
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
        <div><span>攻击</span><b>${def.attack}</b></div>
        <div><span>减伤</span><b>${pct(def.defense)}</b></div>
        <div><span>移动</span><b>${def.movement}</b></div>
        <div><span>射程</span><b>${def.minRange === def.maxRange ? def.minRange : `${def.minRange}-${def.maxRange}`}</b></div>
        <div><span>伤害</span><b>${DAMAGE_TYPE_LABEL[def.damageType]}</b></div>
        <div><span>护甲</span><b>${ARMOR_LABEL[def.armorClass]}</b></div>
        <div><span>移动型</span><b>${MOVEMENT_LABEL[def.movementClass]}</b></div>
        <div><span>造价</span><b>${def.cost}</b></div>
      </div>
      <div class="tag-row">
        ${def.attackAfterMove ? '' : '<span class="tag warn">移动后无法攻击</span>'}
        ${def.abilities.includes('capture') ? '<span class="tag">可占领</span>' : '<span class="tag dim">不可占领</span>'}
        ${def.abilities.includes('heal') ? '<span class="tag good">可治疗</span>' : ''}
        ${u.done ? '<span class="tag dim">已行动</span>' : ''}
      </div>
    </section>`;
  }

  /* ------------------------------------------------------------------- tile */

  private renderTile(v: HudView): string {
    if (!v.tile) return '';
    const s = v.state;
    const i = idx(s.map, v.tile.x, v.tile.y);
    const t = Terrains.get(s.map.tiles[i]);
    const owner = s.players.find((p) => p.id === s.map.owners[i]);
    const costs = (['foot', 'mounted', 'heavy', 'flying'] as const)
      .map((k) => `${MOVEMENT_LABEL[k]} ${t.cost[k] === null ? '—' : t.cost[k]}`)
      .join(' · ');
    return `<section class="card tile-card">
      <h3>${escapeHtml(t.name)} <span class="coord">(${v.tile.x}, ${v.tile.y})</span></h3>
      <div class="kv"><span>防御加成</span><b>${pct(t.defense)}</b></div>
      ${t.capturable ? `<div class="kv"><span>归属</span><b style="color:${owner?.color ?? PAL.neutral}">${escapeHtml(owner?.name ?? '中立')}</b></div>` : ''}
      ${t.income ? `<div class="kv"><span>收入</span><b>${t.income}/回合</b></div>` : ''}
      ${t.heal ? `<div class="kv"><span>治疗</span><b>${t.heal}/回合</b></div>` : ''}
      <div class="kv wrap"><span>移动消耗</span><b>${costs}</b></div>
    </section>`;
  }

  /* ------------------------------------------------------------- objectives */

  private renderObjectives(v: HudView): string {
    const s = v.state;
    const me = s.players.find((p) => p.controller === 'human') ?? s.players[0];
    return `<section class="card">
      <h3>作战目标</h3>
      <ul class="obj-list">
        ${me.objectives
          .map(
            (o) =>
              `<li>${icon('flag')}<span>${escapeHtml(describeObjective(o))}</span><em>${escapeHtml(
                objectiveProgress(s, me.id, o),
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
              <em>${n} 单位 · ${p.funds}${icon('coin')}</em>
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
    const options = recruitOptions(s, v.recruitAt);
    return `<div class="modal">
      <div class="modal-box">
        <div class="modal-head">
          <h2>征募单位</h2>
          <div class="funds">${icon('coin')}<b>${p.funds}</b></div>
          <button class="btn ghost" data-act="cancel">✕</button>
        </div>
        <div class="recruit-grid">
          ${options
            .map((o) => {
              const def = unitDef(o.unit);
              return `<button class="recruit-card ${o.affordable ? '' : 'disabled'}"
                data-act="${o.affordable ? 'recruit' : 'noop'}" data-arg="${o.unit}">
                <div class="rc-art">${unitIcon(o.unit, p.color, 46)}</div>
                <div class="rc-body">
                  <div class="rc-name">${escapeHtml(def.name)}<span class="rc-cost">${icon('coin')}${def.cost}</span></div>
                  <div class="rc-stats">
                    ${icon('sword')}${def.attack} · ${icon('heart')}${def.maxHp} · ${icon('boot')}${def.movement}
                    · ${def.minRange === def.maxRange ? `射程 ${def.maxRange}` : `射程 ${def.minRange}-${def.maxRange}`}
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
    return `<div class="modal">
      <div class="modal-box narrow ${won ? 'win' : 'lose'}">
        <h2>${s.winnerTeam === null ? '平局' : won ? '胜利' : '战败'}</h2>
        <p>${escapeHtml(s.endReason)}</p>
        <div class="modal-actions">
          <button class="btn primary" data-act="restart">${icon('play')} 再来一次</button>
          <button class="btn" data-act="exit">${icon('grid')} 关卡列表</button>
        </div>
      </div>
    </div>`;
  }
}

export function escapeHtml(s: string): string {
  return s.replace(/[&<>"']/g, (c) =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!,
  );
}
