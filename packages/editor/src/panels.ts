import { icon } from '@empire/game-ui/art/icons';
import { terrainSwatch } from '@empire/game-ui/art/terrain';
import { unitIcon } from '@empire/game-ui/art/units';
import { escapeHtml } from '@empire/game-ui/ui/html';
import type { ContentCatalog } from '@empire/battle-engine/content-pack';
import { terrainCharacter, type LevelIssue } from '@empire/battle-engine/level';
import { FUNDS_RESOURCE } from '@empire/battle-engine/resources';
import type { Objective, PlayerConfig, RuleSet } from '@empire/battle-engine/types';
import type { EditorDocument } from './document';
import { EDITOR_TOOLS, type BrushSettings, type EditorTool } from './tools';

/**
 * Everything the editor's three panels draw from.
 *
 * The panels used to read fifteen private fields of `EditorApp` directly, which
 * is what made an 869-line class out of a document editor: rendering, input
 * dispatch, history and file IO all had to live together because the templates
 * reached into all of it. Stating the inputs is what let them separate — the
 * same shape `HudView` already gives the in-battle UI.
 */
export interface EditorPanelView {
  readonly document: EditorDocument;
  readonly content: ContentCatalog;
  readonly tool: EditorTool;
  readonly brush: BrushSettings;
  readonly status: string;
  readonly showCoords: boolean;
  readonly showOwners: boolean;
  readonly canUndo: boolean;
  readonly canRedo: boolean;
  /** Lint findings for the document as it currently stands. */
  readonly issues: readonly LevelIssue[];
  /** Levels offered by the load-a-preset picker, already labelled. */
  readonly presets: readonly { value: string; label: string }[];
}

const OBJECTIVE_TYPES: { type: Objective['type']; label: string }[] = [
  { type: 'routEnemies', label: '歼灭敌军' },
  { type: 'captureHQ', label: '攻占城堡' },
  { type: 'holdAllVillages', label: '控制全部据点' },
  { type: 'surviveTurns', label: '坚守回合' },
];

const SIDE_LABEL = { north: '北', east: '东', south: '南', west: '西' } as const;

const playerFunds = (player: PlayerConfig) => player.resources[FUNDS_RESOURCE]?.current ?? 0;

const unitRecruitCost = (content: ContentCatalog, id: string) => content.units.get(id).recruitCosts
  .map((cost) => `${cost.resource} ${cost.amount}`)
  .join(' · ') || '不可招募';

const fundsGrant = (rules: Partial<RuleSet>) =>
  rules.baseResourceGrants?.find((grant) => grant.resource === FUNDS_RESOURCE)?.amount ?? 0;

const issueLine = (issue: LevelIssue) =>
  `<li class="${issue.severity}">${issue.severity === 'error' ? '✕' : '!'} ${escapeHtml(issue.message)}</li>`;

/**
 * The editor chrome: a toolbar, a palette, and an inspector.
 *
 * Every interactive element declares an intent in `data-act` (a command) or
 * `data-field` (a value to write). The app owns both tables; a fitness test
 * checks that nothing rendered here names an intent nobody handles.
 */
export class EditorPanels {
  readonly topEl = document.createElement('header');
  readonly paletteEl = document.createElement('aside');
  readonly propertiesEl = document.createElement('aside');

  constructor() {
    this.topEl.className = 'topbar editor-top';
    this.paletteEl.className = 'panel palette';
    this.propertiesEl.className = 'panel props';
  }

  renderAll(view: EditorPanelView): void {
    this.renderTop(view);
    this.renderPalette(view);
    this.renderProperties(view);
  }

  renderTop(view: EditorPanelView): void {
    const { document: doc, tool, brush } = view;
    this.topEl.innerHTML = `
      <div class="topbar-left">
        <a class="btn ghost" href="./index.html" title="返回游戏">${icon('play')}</a>
        <input class="name-input" data-field="name" value="${escapeHtml(doc.name)}" placeholder="关卡名称" />
        <input class="id-input" data-field="id" value="${escapeHtml(doc.id)}" placeholder="id" />
      </div>
      <div class="topbar-center tool-row">
        ${EDITOR_TOOLS.tools
          .map(
            (candidate) => `<button class="btn tool ${tool === candidate ? 'active' : ''}" data-act="tool" data-arg="${candidate.id}"
              title="${candidate.name} (${candidate.hotkey.toUpperCase()})">${icon(candidate.icon)}<span>${candidate.name}</span></button>`,
          )
          .join('')}
        <span class="sep"></span>
        <button class="btn ghost ${brush.size === 1 ? 'active' : ''}" data-act="brush" data-arg="1">1×1</button>
        <button class="btn ghost ${brush.size === 3 ? 'active' : ''}" data-act="brush" data-arg="3">3×3</button>
      </div>
      <div class="topbar-right">
        <button class="btn ghost" data-act="undo" ${view.canUndo ? '' : 'disabled'}>${icon('undo')}</button>
        <button class="btn ghost" data-act="redo" ${view.canRedo ? '' : 'disabled'}>${icon('play')}</button>
        <button class="btn ghost" data-act="zoom" data-arg="-0.15">−</button>
        <button class="btn ghost" data-act="zoom" data-arg="0.15">+</button>
        <button class="btn" data-act="save">${icon('save')} 保存</button>
        <button class="btn primary" data-act="playtest">${icon('play')} 试玩</button>
      </div>
      ${view.status ? `<div class="status-toast">${escapeHtml(view.status)}</div>` : ''}`;
  }

  renderPalette(view: EditorPanelView): void {
    const { content, brush } = view;
    const ownerColor = this.colorOf(view, brush.owner);
    this.paletteEl.innerHTML = `
      <section class="card">
        <h3>地形</h3>
        <div class="swatch-grid">
          ${content.terrains.all()
            .map(
              (terrain, index) => `<button class="swatch ${brush.terrain === terrain.id ? 'active' : ''}"
                data-act="terrain" data-arg="${terrain.id}" title="${escapeHtml(terrain.name)} · 字符 ${terrainCharacter(content, terrain.id) ?? '?'}${index < 9 ? ` · 快捷键 ${index + 1}` : ''}">
                ${terrainSwatch(terrain.id, terrain.capturable ? ownerColor : undefined)}
                <span>${escapeHtml(terrain.name)}</span>
              </button>`,
            )
            .join('')}
        </div>
      </section>

      <section class="card">
        <h3>空间规则</h3>
        <div class="row"><label>海拔<input type="number" min="-9" max="20" data-field="elevation" value="${brush.elevation}"/></label></div>
        <div class="owner-row">
          ${(['north', 'east', 'south', 'west'] as const)
            .map((side) => `<button class="owner-chip ${brush.coverSide === side ? 'active' : ''}" data-act="cover-side" data-arg="${side}">${SIDE_LABEL[side]}</button>`)
            .join('')}
        </div>
        <div class="owner-row">
          <button class="owner-chip ${brush.coverLevel === 'half' ? 'active' : ''}" data-act="cover-level" data-arg="half">半掩体</button>
          <button class="owner-chip ${brush.coverLevel === 'full' ? 'active' : ''}" data-act="cover-level" data-arg="full">全掩体</button>
        </div>
        <p class="hint">海拔工具直接绘制高度；悬崖工具从一个格拖到相邻格；方向掩体按来袭方向保护单位。</p>
      </section>

      <section class="card">
        <h3>归属</h3>
        <div class="owner-row">
          <button class="owner-chip ${brush.owner === 0 ? 'active' : ''}" data-act="owner" data-arg="0"
            style="--team:#9aa3ad">中立</button>
          ${view.document.players
            .map(
              (player) => `<button class="owner-chip ${brush.owner === player.id ? 'active' : ''}" data-act="owner" data-arg="${player.id}"
                style="--team:${player.color}">${escapeHtml(player.name)}</button>`,
            )
            .join('')}
        </div>
      </section>

      <section class="card">
        <h3>单位</h3>
        <div class="unit-grid">
          ${content.units.all()
            .map(
              (definition) => `<button class="unit-chip ${brush.unitType === definition.id ? 'active' : ''}"
                data-act="unit" data-arg="${definition.id}" title="${escapeHtml(definition.name)} · ${escapeHtml(unitRecruitCost(content, definition.id))}">
                ${unitIcon(definition.id, ownerColor, 30)}
                <span>${escapeHtml(definition.name)}</span>
              </button>`,
            )
            .join('')}
        </div>
      </section>

      <section class="card">
        <h3>显示</h3>
        <label class="check"><input type="checkbox" data-field="showOwners" ${view.showOwners ? 'checked' : ''}/> 归属描边</label>
        <label class="check"><input type="checkbox" data-field="showCoords" ${view.showCoords ? 'checked' : ''}/> 坐标</label>
        <p class="hint">左键绘制 · 右键擦除 / 置为平原 · Ctrl+滚轮缩放</p>
      </section>`;
  }

  renderProperties(view: EditorPanelView): void {
    const { document: doc, issues } = view;
    const rules = doc.rules;
    this.propertiesEl.innerHTML = `
      <section class="card">
        <h3>尺寸</h3>
        <div class="row">
          <label>宽<input type="number" min="4" max="64" data-field="width" value="${doc.map.width}"/></label>
          <label>高<input type="number" min="4" max="64" data-field="height" value="${doc.map.height}"/></label>
        </div>
        <p class="hint">缩小地图会裁掉超出范围的内容。</p>
      </section>

      <section class="card">
        <h3>说明</h3>
        <textarea data-field="description" rows="3" placeholder="关卡简介">${escapeHtml(doc.description)}</textarea>
        <label class="stack">作者<input data-field="author" value="${escapeHtml(doc.author)}"/></label>
      </section>

      <section class="card">
        <h3>玩家</h3>
        ${doc.players.map((player) => this.playerCard(view, player)).join('')}
        <button class="btn wide ghost" data-act="addPlayer">+ 添加玩家</button>
      </section>

      <section class="card">
        <h3>通用胜利条件</h3>
        <div class="chip-row">
          ${OBJECTIVE_TYPES.map((objective) => {
            const on = doc.victory.some((candidate) => candidate.type === objective.type);
            return `<button class="chip ${on ? 'on' : ''}" data-act="vObj" data-arg="${objective.type}">${objective.label}</button>`;
          }).join('')}
        </div>
      </section>

      <section class="card">
        <h3>规则</h3>
        <label class="check"><input type="checkbox" data-field="r.fog" ${rules.fog ? 'checked' : ''}/> 战争迷雾</label>
        <label class="check"><input type="checkbox" data-field="r.counterAttack" ${rules.counterAttack ?? true ? 'checked' : ''}/> 允许反击</label>
        <label class="check"><input type="checkbox" data-field="r.recruitsActImmediately" ${rules.recruitsActImmediately ? 'checked' : ''}/> 新单位当回合可行动</label>
        <label class="stack">行动序
          <select data-field="r.turnOrder">
            <option value="side" ${(rules.turnOrder ?? 'side') === 'side' ? 'selected' : ''}>阵营回合（远古帝国 / AW）</option>
            <option value="initiative" ${rules.turnOrder === 'initiative' ? 'selected' : ''}>个体行动序（皇家骑士团 / FFT）</option>
          </select>
        </label>
        <label class="stack">占领方式
          <select data-field="r.captureMode">
            <option value="instant" ${(rules.captureMode ?? 'instant') === 'instant' ? 'selected' : ''}>踏入即占领（远古帝国）</option>
            <option value="progressive" ${rules.captureMode === 'progressive' ? 'selected' : ''}>按生命值累积</option>
          </select>
        </label>
        <div class="row">
          <label class="tiny">回合上限<input type="number" min="0" data-field="r.turnLimit" value="${rules.turnLimit ?? ''}" placeholder="无"/></label>
          <label class="tiny">基础资金产出<input type="number" min="0" step="50" data-field="r.baseFundsGrant" value="${fundsGrant(rules)}"/></label>
        </div>
        <div class="row">
          <label class="tiny">据点资金产出<input type="number" min="0" step="50" data-field="r.siteFundsOverride" value="${rules.siteResourceOverrides?.[FUNDS_RESOURCE] ?? ''}" placeholder="按地形"/></label>
          <label class="tiny">单位上限<input type="number" min="0" data-field="r.maxUnitsPerPlayer" value="${rules.maxUnitsPerPlayer ?? ''}" placeholder="无"/></label>
        </div>
      </section>

      <section class="card ${issues.some((issue) => issue.severity === 'error') ? 'has-error' : ''}">
        <h3>检查 ${issues.length ? `(${issues.length})` : ''}</h3>
        ${issues.length === 0
          ? '<p class="hint good">没有发现问题，可以试玩。</p>'
          : `<ul class="issue-list">${issues.map(issueLine).join('')}</ul>`}
      </section>

      <section class="card">
        <h3>文件</h3>
        <div class="row">
          <button class="btn" data-act="import">${icon('save')} 载入 JSON</button>
          <button class="btn" data-act="export">${icon('save')} 导出</button>
        </div>
        <button class="btn wide ghost" data-act="copy">复制 JSON</button>
        <label class="stack">载入内置 / 已保存关卡
          <select data-field="loadPreset">
            <option value="">选择…</option>
            ${view.presets.map((preset) => `<option value="${preset.value}">${escapeHtml(preset.label)}</option>`).join('')}
          </select>
        </label>
        <button class="btn wide ghost danger" data-act="clear">清空为空白地图</button>
      </section>`;
  }

  private playerCard(view: EditorPanelView, player: PlayerConfig): string {
    const removable = view.document.players.length > 2;
    return `<div class="player-edit" style="--team:${player.color}">
      <div class="row">
        <span class="dot"></span>
        <input data-field="p.name" data-id="${player.id}" value="${escapeHtml(player.name)}"/>
        <input type="color" data-field="p.color" data-id="${player.id}" value="${player.color}"/>
        ${removable ? `<button class="btn ghost danger tiny" data-act="delPlayer" data-arg="${player.id}">${icon('trash')}</button>` : ''}
      </div>
      <div class="row">
        <label class="tiny">阵营<input type="number" min="1" max="8" data-field="p.team" data-id="${player.id}" value="${player.team}"/></label>
        <label class="tiny">资金<input type="number" min="0" step="50" data-field="p.funds" data-id="${player.id}" value="${playerFunds(player)}"/></label>
        <label class="tiny">控制
          <select data-field="p.controller" data-id="${player.id}">
            <option value="human" ${player.controller === 'human' ? 'selected' : ''}>玩家</option>
            <option value="ai" ${player.controller === 'ai' ? 'selected' : ''}>AI</option>
          </select>
        </label>
        <label class="tiny">激进<input type="number" min="0" max="1" step="0.05" data-field="p.aggression" data-id="${player.id}" value="${player.ai?.aggression ?? 0.5}"/></label>
      </div>
      <div class="chip-row">
        ${OBJECTIVE_TYPES.map((objective) => {
          const on = (player.objectives ?? []).some((candidate) => candidate.type === objective.type);
          return `<button class="chip ${on ? 'on' : ''}" data-act="pObj" data-arg="${player.id}:${objective.type}">${objective.label}</button>`;
        }).join('')}
      </div>
      ${(player.objectives ?? []).length === 0 ? '<p class="hint tiny">未选择时使用下方通用胜利条件</p>' : ''}
    </div>`;
  }

  private colorOf(view: EditorPanelView, owner: number): string {
    return view.document.players.find((player) => player.id === owner)?.color ?? '#9aa3ad';
  }
}
