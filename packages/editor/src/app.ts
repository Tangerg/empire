import { icon } from '@empire/game-ui/art/icons';
import { loadCustomLevels, saveCustomLevel, stashPlaytest } from '@empire/game-ui/application/level-storage';
import { terrainSwatch } from '@empire/game-ui/art/terrain';
import { unitIcon } from '@empire/game-ui/art/units';
import { TEAM_COLORS } from '@empire/game-ui/art/palette';
import { ANCIENT_EMPIRES_LEVELS as BUILTIN_LEVELS } from '@empire/content-ancient-empires/levels';
import { Terrains } from '@empire/battle-engine/data/terrain';
import { UnitTypes } from '@empire/battle-engine/data/units';
import { idx } from '@empire/battle-engine/grid';
import {
  emptyLevel,
  normaliseLevel,
  terrainCharacter,
  validateLevel,
  type LevelIssue,
} from '@empire/battle-engine/mapio';
import { mapFromLevel } from '@empire/battle-engine/mapio';
import type {
  Coord,
  CoverLevel,
  Direction,
  LevelData,
  Objective,
  PlayerConfig,
  RuleSet,
  TerrainId,
} from '@empire/battle-engine/types';
import { escapeHtml } from '@empire/game-ui/ui/html';
import { EditorBoard } from './board';
import { EditorDocument } from './document';
import { COMMAND_POINTS_RESOURCE, FUNDS_RESOURCE } from '@empire/battle-engine/resources';

type Tool = 'terrain' | 'rect' | 'fill' | 'elevation' | 'cliff' | 'cover' | 'unit' | 'owner' | 'erase' | 'pick';

const DRAFT_KEY = 'empire.editorDraft';

const playerFunds = (player: PlayerConfig) => player.resources[FUNDS_RESOURCE]?.current ?? 0;
const unitRecruitCost = (id: string) => UnitTypes.get(id).recruitCosts
  .map((cost) => `${cost.resource} ${cost.amount}`)
  .join(' · ') || '不可招募';
const fundsGrant = (rules: Partial<RuleSet>) =>
  rules.baseResourceGrants?.find((grant) => grant.resource === FUNDS_RESOURCE)?.amount ?? 0;

const TOOL_LABEL: Record<Tool, { name: string; key: string; icon: string }> = {
  terrain: { name: '笔刷', key: 'B', icon: 'grid' },
  rect: { name: '矩形', key: 'R', icon: 'save' },
  fill: { name: '填充', key: 'F', icon: 'flag' },
  elevation: { name: '海拔', key: 'H', icon: 'grid' },
  cliff: { name: '悬崖边', key: 'J', icon: 'shield' },
  cover: { name: '方向掩体', key: 'K', icon: 'shield' },
  unit: { name: '放置单位', key: 'U', icon: 'sword' },
  owner: { name: '归属', key: 'O', icon: 'shield' },
  erase: { name: '擦除单位', key: 'E', icon: 'trash' },
  pick: { name: '吸取', key: 'I', icon: 'crosshair' },
};

const OBJECTIVE_TYPES: { type: Objective['type']; label: string }[] = [
  { type: 'routEnemies', label: '歼灭敌军' },
  { type: 'captureHQ', label: '攻占城堡' },
  { type: 'holdAllVillages', label: '控制全部据点' },
  { type: 'surviveTurns', label: '坚守回合' },
];

export class EditorApp {
  private doc: EditorDocument;
  private board: EditorBoard;
  private undoStack: string[] = [];
  private redoStack: string[] = [];

  private tool: Tool = 'terrain';
  private terrain: TerrainId = 'plain';
  private unitType = 'soldier';
  private owner = 1;
  private brushSize = 1;
  private elevation = 0;
  private coverLevel: Exclude<CoverLevel, 'none'> = 'half';
  private coverSide: Direction = 'north';
  private rectAnchor: Coord | null = null;
  private cursor: Coord | null = null;
  private showCoords = false;
  private showOwners = true;
  private strokeOpen = false;
  private status = '';

  private readonly root = document.createElement('div');
  private readonly topEl = document.createElement('header');
  private readonly leftEl = document.createElement('aside');
  private readonly rightEl = document.createElement('aside');
  private readonly scroller = document.createElement('div');

  constructor(level: LevelData) {
    this.doc = EditorDocument.fromLevel(level);
    this.ensureOwnerSelection();
    this.root.className = 'editor-root';
    this.topEl.className = 'topbar editor-top';
    this.leftEl.className = 'panel palette';
    this.rightEl.className = 'panel props';
    this.scroller.className = 'board-scroll';

    this.board = new EditorBoard(this.doc.map, this.doc.units, this.doc.players, {
      onStroke: (c, phase, button) => this.onStroke(c, phase, button),
      onHover: (c) => {
        this.cursor = c;
        this.paintBoard();
      },
    });
    this.scroller.append(this.board.el);

    const stage = document.createElement('div');
    stage.className = 'stage';
    stage.append(this.leftEl, this.scroller, this.rightEl);
    this.root.append(this.topEl, stage);

    this.bindDelegates();
    document.addEventListener('keydown', (ev) => this.onKey(ev));
    this.board.el.addEventListener(
      'wheel',
      (ev) => {
        if (!ev.ctrlKey && !ev.metaKey) return;
        ev.preventDefault();
        this.board.setZoom(this.board.zoomLevel - Math.sign(ev.deltaY) * 0.1);
      },
      { passive: false },
    );

    this.renderAll();
  }

  mount(host: HTMLElement): void {
    host.replaceChildren(this.root);
  }

  /* ------------------------------------------------------------- doc history */

  private snapshot(): void {
    this.undoStack.push(this.doc.serialize());
    if (this.undoStack.length > 80) this.undoStack.shift();
    this.redoStack.length = 0;
  }

  /** Public so the editor host (and tests) can drive history. */
  undo(): void {
    const prev = this.undoStack.pop();
    if (!prev) return;
    this.redoStack.push(this.doc.serialize());
    this.replaceDocument(EditorDocument.deserialize(prev));
    this.renderAll();
  }

  redo(): void {
    const next = this.redoStack.pop();
    if (!next) return;
    this.undoStack.push(this.doc.serialize());
    this.replaceDocument(EditorDocument.deserialize(next));
    this.renderAll();
  }

  private autosave(): void {
    try {
      localStorage.setItem(DRAFT_KEY, JSON.stringify(this.exportLevel()));
    } catch {
      /* storage full or unavailable — drafts are a convenience, not a contract */
    }
  }

  private replaceDocument(document: EditorDocument): void {
    this.doc = document;
    this.ensureOwnerSelection();
    this.board.resize(document.map);
  }

  private ensureOwnerSelection(): void {
    if (!this.doc.players.some((player) => player.id === this.owner)) {
      this.owner = this.doc.players[0]?.id ?? 0;
    }
  }

  /* ------------------------------------------------------------------ tools */

  private brushTiles(): Coord[] {
    if (!this.cursor) return [];
    if (this.tool === 'rect' && this.rectAnchor) return rectTiles(this.rectAnchor, this.cursor);
    if (this.tool === 'terrain' && this.brushSize > 1) {
      const out: Coord[] = [];
      const r = Math.floor(this.brushSize / 2);
      for (let dy = -r; dy <= r; dy++) {
        for (let dx = -r; dx <= r; dx++) {
          const x = this.cursor.x + dx;
          const y = this.cursor.y + dy;
          if (this.doc.inBounds({ x, y })) out.push({ x, y });
        }
      }
      return out;
    }
    return [this.cursor];
  }

  private onStroke(c: Coord, phase: 'start' | 'move' | 'end', button: number): void {
    const erasing = button === 2;

    if (phase === 'start') {
      this.snapshot();
      this.strokeOpen = true;
      if (this.tool === 'rect') {
        this.rectAnchor = c;
        this.paintBoard();
        return;
      }
      if (this.tool === 'cliff') {
        this.rectAnchor = c;
        this.paintBoard();
        return;
      }
    }

    if (this.tool === 'rect') {
      if (phase === 'end' && this.rectAnchor) {
        for (const t of rectTiles(this.rectAnchor, c)) this.setTerrain(t, this.terrain);
        this.rectAnchor = null;
        this.finishStroke();
      } else {
        this.cursor = c;
        this.paintBoard();
      }
      return;
    }

    if (this.tool === 'cliff') {
      if (phase === 'end' && this.rectAnchor) {
        const anchor = this.rectAnchor;
        if (Math.abs(anchor.x - c.x) + Math.abs(anchor.y - c.y) === 1) this.toggleCliff(anchor, c);
        this.rectAnchor = null;
        this.finishStroke();
      } else {
        this.cursor = c;
        this.paintBoard();
      }
      return;
    }

    if (phase === 'end') {
      this.finishStroke();
      return;
    }

    switch (this.tool) {
      case 'terrain':
        for (const t of this.brushTilesAt(c)) this.setTerrain(t, erasing ? 'plain' : this.terrain);
        break;
      case 'fill':
        this.floodFill(c, this.terrain);
        break;
      case 'elevation':
        for (const tile of this.brushTilesAt(c)) {
          this.doc.setElevation(tile, erasing ? 0 : this.elevation);
        }
        break;
      case 'cover':
        this.setDirectionalCover(c, erasing ? null : this.coverLevel);
        break;
      case 'unit':
        if (erasing) this.removeUnitAt(c);
        else this.placeUnit(c);
        break;
      case 'owner':
        this.setOwner(c, erasing ? 0 : this.owner);
        break;
      case 'erase':
        this.removeUnitAt(c);
        break;
      case 'pick':
        this.pick(c);
        break;
    }
    this.paintBoard();
    this.renderRight();
  }

  private brushTilesAt(c: Coord): Coord[] {
    const saved = this.cursor;
    this.cursor = c;
    const tiles = this.brushTiles();
    this.cursor = saved;
    return tiles;
  }

  private finishStroke(): void {
    if (!this.strokeOpen) return;
    this.strokeOpen = false;
    this.autosave();
    this.renderAll();
  }

  private setTerrain(c: Coord, id: TerrainId): void {
    this.doc.setTerrain(c, id);
  }

  private floodFill(from: Coord, id: TerrainId): void {
    this.doc.floodFill(from, id);
  }

  private placeUnit(c: Coord): void {
    this.doc.placeUnit(c, this.unitType, this.owner);
  }

  private removeUnitAt(c: Coord): void {
    this.doc.removeUnitAt(c);
  }

  private setOwner(c: Coord, owner: number): void {
    this.doc.setOwner(c, owner);
  }

  private toggleCliff(from: Coord, to: Coord): void {
    this.doc.toggleCliff(from, to);
  }

  private setDirectionalCover(at: Coord, level: Exclude<CoverLevel, 'none'> | null): void {
    this.doc.setDirectionalCover(at, this.coverSide, level);
  }

  private pick(c: Coord): void {
    const i = idx(this.doc.map, c.x, c.y);
    this.terrain = this.doc.map.tiles[i];
    const u = this.doc.units.find((x) => x.x === c.x && x.y === c.y);
    if (u) {
      this.unitType = u.unit;
      this.owner = u.owner;
    } else if (Terrains.get(this.terrain).capturable) {
      this.owner = this.doc.map.owners[i] || this.owner;
    }
    this.renderLeft();
  }

  resize(width: number, height: number): void {
    width = Math.max(4, Math.min(64, Math.round(width)));
    height = Math.max(4, Math.min(64, Math.round(height)));
    if (width === this.doc.map.width && height === this.doc.map.height) return;
    this.snapshot();
    this.doc.resize(width, height);
    this.board.resize(this.doc.map);
    this.autosave();
    this.renderAll();
  }

  /* ---------------------------------------------------------------- keyboard */

  private onKey(ev: KeyboardEvent): void {
    const target = ev.target as HTMLElement;
    if (target && /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName)) return;

    if ((ev.metaKey || ev.ctrlKey) && ev.key.toLowerCase() === 'z') {
      ev.preventDefault();
      if (ev.shiftKey) this.redo();
      else this.undo();
      return;
    }
    if ((ev.metaKey || ev.ctrlKey) && ev.key.toLowerCase() === 's') {
      ev.preventDefault();
      this.save();
      return;
    }
    const toolKeys: Record<string, Tool> = {
      b: 'terrain',
      r: 'rect',
      f: 'fill',
      u: 'unit',
      o: 'owner',
      e: 'erase',
      i: 'pick',
    };
    const k = ev.key.toLowerCase();
    if (toolKeys[k]) {
      this.tool = toolKeys[k];
      this.renderAll();
      return;
    }
    if (ev.key === 'Escape') {
      this.rectAnchor = null;
      this.paintBoard();
    }
    const n = Number(ev.key);
    if (n >= 1 && n <= 9) {
      const ids = Terrains.ids();
      if (ids[n - 1]) {
        this.terrain = ids[n - 1];
        this.tool = 'terrain';
        this.renderAll();
      }
    }
  }

  /* ------------------------------------------------------------------- io */

  /** Serialise the document to the on-disk level format. */
  exportLevel(): LevelData {
    return this.doc.toLevel();
  }

  private save(): void {
    const level = this.exportLevel();
    const errors = validateLevel(level).filter((i) => i.severity === 'error');
    saveCustomLevel(level);
    this.autosave();
    this.status = errors.length
      ? `已保存（仍有 ${errors.length} 个错误，游戏中可能无法开始）`
      : '已保存到「我的关卡」';
    this.renderAll();
  }

  private exportFile(): void {
    const level = this.exportLevel();
    const blob = new Blob([JSON.stringify(level, null, 2)], { type: 'application/json' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `${level.id || 'level'}.json`;
    a.click();
    URL.revokeObjectURL(a.href);
    this.status = '已导出 JSON';
    this.renderTop();
  }

  private async copyJson(): Promise<void> {
    try {
      await navigator.clipboard.writeText(JSON.stringify(this.exportLevel(), null, 2));
      this.status = 'JSON 已复制到剪贴板';
    } catch {
      this.status = '复制失败（浏览器拒绝了剪贴板权限）';
    }
    this.renderTop();
  }

  private importFile(): void {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json,application/json';
    input.onchange = async () => {
      const file = input.files?.[0];
      if (!file) return;
      try {
        const level = normaliseLevel(JSON.parse(await file.text()));
        mapFromLevel(level); // fail fast on a broken terrain grid
        this.snapshot();
        this.replaceDocument(EditorDocument.fromLevel(level));
        this.status = `已载入 ${level.name}`;
        this.renderAll();
      } catch (e) {
        this.status = `载入失败：${(e as Error).message}`;
        this.renderTop();
      }
    };
    input.click();
  }

  private playtest(): void {
    const level = this.exportLevel();
    const errors = validateLevel(level).filter((i) => i.severity === 'error');
    if (errors.length) {
      this.status = `无法试玩：${errors[0].message}`;
      this.renderAll();
      return;
    }
    stashPlaytest(level);
    location.href = './index.html';
  }

  /* --------------------------------------------------------------- rendering */

  private renderAll(): void {
    this.renderTop();
    this.renderLeft();
    this.renderRight();
    this.paintBoard();
  }

  private paintBoard(): void {
    this.board.render(this.doc.map, this.doc.units, this.doc.players, {
      cursor: this.cursor,
      brush: this.brushTiles(),
      showCoords: this.showCoords,
      showOwners: this.showOwners,
    });
  }

  private renderTop(): void {
    this.topEl.innerHTML = `
      <div class="topbar-left">
        <a class="btn ghost" href="./index.html" title="返回游戏">${icon('play')}</a>
        <input class="name-input" data-field="name" value="${escapeHtml(this.doc.name)}" placeholder="关卡名称" />
        <input class="id-input" data-field="id" value="${escapeHtml(this.doc.id)}" placeholder="id" />
      </div>
      <div class="topbar-center tool-row">
        ${(Object.keys(TOOL_LABEL) as Tool[])
          .map(
            (t) => `<button class="btn tool ${this.tool === t ? 'active' : ''}" data-act="tool" data-arg="${t}"
              title="${TOOL_LABEL[t].name} (${TOOL_LABEL[t].key})">${icon(TOOL_LABEL[t].icon)}<span>${TOOL_LABEL[t].name}</span></button>`,
          )
          .join('')}
        <span class="sep"></span>
        <button class="btn ghost ${this.brushSize === 1 ? 'active' : ''}" data-act="brush" data-arg="1">1×1</button>
        <button class="btn ghost ${this.brushSize === 3 ? 'active' : ''}" data-act="brush" data-arg="3">3×3</button>
      </div>
      <div class="topbar-right">
        <button class="btn ghost" data-act="undo" ${this.undoStack.length ? '' : 'disabled'}>${icon('undo')}</button>
        <button class="btn ghost" data-act="redo" ${this.redoStack.length ? '' : 'disabled'}>${icon('play')}</button>
        <button class="btn ghost" data-act="zoom" data-arg="-0.15">−</button>
        <button class="btn ghost" data-act="zoom" data-arg="0.15">+</button>
        <button class="btn" data-act="save">${icon('save')} 保存</button>
        <button class="btn primary" data-act="playtest">${icon('play')} 试玩</button>
      </div>
      ${this.status ? `<div class="status-toast">${escapeHtml(this.status)}</div>` : ''}`;
  }

  private renderLeft(): void {
    const terrainList = Terrains.all();
    this.leftEl.innerHTML = `
      <section class="card">
        <h3>地形</h3>
        <div class="swatch-grid">
          ${terrainList
            .map(
              (t, i) => `<button class="swatch ${this.terrain === t.id ? 'active' : ''}"
                data-act="terrain" data-arg="${t.id}" title="${escapeHtml(t.name)} · 字符 ${terrainCharacter(t.id) ?? '?'}${i < 9 ? ` · 快捷键 ${i + 1}` : ''}">
                ${terrainSwatch(t.id, t.capturable ? this.colorOf(this.owner) : undefined)}
                <span>${escapeHtml(t.name)}</span>
              </button>`,
            )
            .join('')}
        </div>
      </section>

      <section class="card">
        <h3>空间规则</h3>
        <div class="row"><label>海拔<input type="number" min="-9" max="20" data-field="elevation" value="${this.elevation}"/></label></div>
        <div class="owner-row">
          ${(['north', 'east', 'south', 'west'] as const).map((side) => `<button class="owner-chip ${this.coverSide === side ? 'active' : ''}" data-act="cover-side" data-arg="${side}">${({ north: '北', east: '东', south: '南', west: '西' })[side]}</button>`).join('')}
        </div>
        <div class="owner-row">
          <button class="owner-chip ${this.coverLevel === 'half' ? 'active' : ''}" data-act="cover-level" data-arg="half">半掩体</button>
          <button class="owner-chip ${this.coverLevel === 'full' ? 'active' : ''}" data-act="cover-level" data-arg="full">全掩体</button>
        </div>
        <p class="hint">海拔工具直接绘制高度；悬崖工具从一个格拖到相邻格；方向掩体按来袭方向保护单位。</p>
      </section>

      <section class="card">
        <h3>归属</h3>
        <div class="owner-row">
          <button class="owner-chip ${this.owner === 0 ? 'active' : ''}" data-act="owner" data-arg="0"
            style="--team:#9aa3ad">中立</button>
          ${this.doc.players
            .map(
              (p) => `<button class="owner-chip ${this.owner === p.id ? 'active' : ''}" data-act="owner" data-arg="${p.id}"
                style="--team:${p.color}">${escapeHtml(p.name)}</button>`,
            )
            .join('')}
        </div>
      </section>

      <section class="card">
        <h3>单位</h3>
        <div class="unit-grid">
          ${UnitTypes.all()
            .map(
              (d) => `<button class="unit-chip ${this.unitType === d.id ? 'active' : ''}"
                data-act="unit" data-arg="${d.id}" title="${escapeHtml(d.name)} · ${escapeHtml(unitRecruitCost(d.id))}">
                ${unitIcon(d.id, this.colorOf(this.owner), 30)}
                <span>${escapeHtml(d.name)}</span>
              </button>`,
            )
            .join('')}
        </div>
      </section>

      <section class="card">
        <h3>显示</h3>
        <label class="check"><input type="checkbox" data-field="showOwners" ${this.showOwners ? 'checked' : ''}/> 归属描边</label>
        <label class="check"><input type="checkbox" data-field="showCoords" ${this.showCoords ? 'checked' : ''}/> 坐标</label>
        <p class="hint">左键绘制 · 右键擦除 / 置为平原 · Ctrl+滚轮缩放</p>
      </section>`;
  }

  private colorOf(id: number): string {
    return this.doc.players.find((p) => p.id === id)?.color ?? '#9aa3ad';
  }

  private renderRight(): void {
    const level = this.exportLevel();
    const issues = validateLevel(level);
    const rules = this.doc.rules;
    this.rightEl.innerHTML = `
      <section class="card">
        <h3>尺寸</h3>
        <div class="row">
          <label>宽<input type="number" min="4" max="64" data-field="width" value="${this.doc.map.width}"/></label>
          <label>高<input type="number" min="4" max="64" data-field="height" value="${this.doc.map.height}"/></label>
        </div>
        <p class="hint">缩小地图会裁掉超出范围的内容。</p>
      </section>

      <section class="card">
        <h3>说明</h3>
        <textarea data-field="description" rows="3" placeholder="关卡简介">${escapeHtml(this.doc.description)}</textarea>
        <label class="stack">作者<input data-field="author" value="${escapeHtml(this.doc.author)}"/></label>
      </section>

      <section class="card">
        <h3>玩家</h3>
        ${this.doc.players
          .map(
            (p) => `<div class="player-edit" style="--team:${p.color}">
              <div class="row">
                <span class="dot"></span>
                <input data-field="p.name" data-id="${p.id}" value="${escapeHtml(p.name)}"/>
                <input type="color" data-field="p.color" data-id="${p.id}" value="${p.color}"/>
                ${this.doc.players.length > 2 ? `<button class="btn ghost danger tiny" data-act="delPlayer" data-arg="${p.id}">${icon('trash')}</button>` : ''}
              </div>
              <div class="row">
                <label class="tiny">阵营<input type="number" min="1" max="8" data-field="p.team" data-id="${p.id}" value="${p.team}"/></label>
                <label class="tiny">资金<input type="number" min="0" step="50" data-field="p.funds" data-id="${p.id}" value="${playerFunds(p)}"/></label>
                <label class="tiny">控制
                  <select data-field="p.controller" data-id="${p.id}">
                    <option value="human" ${p.controller === 'human' ? 'selected' : ''}>玩家</option>
                    <option value="ai" ${p.controller === 'ai' ? 'selected' : ''}>AI</option>
                  </select>
                </label>
                <label class="tiny">激进<input type="number" min="0" max="1" step="0.05" data-field="p.aggression" data-id="${p.id}" value="${p.ai?.aggression ?? 0.5}"/></label>
              </div>
              <div class="chip-row">
                ${OBJECTIVE_TYPES.map((o) => {
                  const on = (p.objectives ?? []).some((x) => x.type === o.type);
                  return `<button class="chip ${on ? 'on' : ''}" data-act="pObj" data-arg="${p.id}:${o.type}">${o.label}</button>`;
                }).join('')}
              </div>
              ${(p.objectives ?? []).length === 0 ? '<p class="hint tiny">未选择时使用下方通用胜利条件</p>' : ''}
            </div>`,
          )
          .join('')}
        <button class="btn wide ghost" data-act="addPlayer">+ 添加玩家</button>
      </section>

      <section class="card">
        <h3>通用胜利条件</h3>
        <div class="chip-row">
          ${OBJECTIVE_TYPES.map((o) => {
            const on = this.doc.victory.some((x) => x.type === o.type);
            return `<button class="chip ${on ? 'on' : ''}" data-act="vObj" data-arg="${o.type}">${o.label}</button>`;
          }).join('')}
        </div>
      </section>

      <section class="card">
        <h3>规则</h3>
        <label class="check"><input type="checkbox" data-field="r.fog" ${rules.fog ? 'checked' : ''}/> 战争迷雾</label>
        <label class="check"><input type="checkbox" data-field="r.counterAttack" ${rules.counterAttack ?? true ? 'checked' : ''}/> 允许反击</label>
        <label class="check"><input type="checkbox" data-field="r.recruitsActImmediately" ${rules.recruitsActImmediately ? 'checked' : ''}/> 新单位当回合可行动</label>
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

      <section class="card ${issues.some((i) => i.severity === 'error') ? 'has-error' : ''}">
        <h3>检查 ${issues.length ? `(${issues.length})` : ''}</h3>
        ${
          issues.length === 0
            ? '<p class="hint good">没有发现问题，可以试玩。</p>'
            : `<ul class="issue-list">${issues.map(issueLine).join('')}</ul>`
        }
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
            ${BUILTIN_LEVELS.map((l) => `<option value="b:${l.id}">内置 · ${escapeHtml(l.name)}</option>`).join('')}
            ${loadCustomLevels()
              .map((s) => `<option value="c:${s.level.id}">我的 · ${escapeHtml(s.level.name)}</option>`)
              .join('')}
          </select>
        </label>
        <button class="btn wide ghost danger" data-act="clear">清空为空白地图</button>
      </section>`;
  }

  /* ------------------------------------------------------------- delegation */

  private bindDelegates(): void {
    const onClick = (ev: Event) => {
      const el = (ev.target as HTMLElement).closest('[data-act]') as HTMLElement | null;
      if (!el) return;
      const arg = el.dataset.arg ?? '';
      switch (el.dataset.act) {
        case 'tool':
          this.tool = arg as Tool;
          this.rectAnchor = null;
          this.renderAll();
          break;
        case 'brush':
          this.brushSize = Number(arg);
          this.renderTop();
          this.paintBoard();
          break;
        case 'terrain':
          this.terrain = arg;
          if (this.tool !== 'rect' && this.tool !== 'fill') this.tool = 'terrain';
          this.renderAll();
          break;
        case 'cover-side':
          this.coverSide = arg as Direction;
          this.renderLeft();
          break;
        case 'cover-level':
          this.coverLevel = arg as Exclude<CoverLevel, 'none'>;
          this.renderLeft();
          break;
        case 'unit':
          this.unitType = arg;
          this.tool = 'unit';
          this.renderAll();
          break;
        case 'owner':
          this.owner = Number(arg);
          this.renderLeft();
          this.renderTop();
          break;
        case 'undo':
          this.undo();
          break;
        case 'redo':
          this.redo();
          break;
        case 'zoom':
          this.board.setZoom(this.board.zoomLevel + Number(arg));
          break;
        case 'save':
          this.save();
          break;
        case 'playtest':
          this.playtest();
          break;
        case 'export':
          this.exportFile();
          break;
        case 'copy':
          void this.copyJson();
          break;
        case 'import':
          this.importFile();
          break;
        case 'clear':
          if (confirm('清空当前地图？')) {
            this.snapshot();
            this.replaceDocument(EditorDocument.fromLevel(emptyLevel(this.doc.map.width, this.doc.map.height)));
            this.renderAll();
          }
          break;
        case 'addPlayer': {
          this.snapshot();
          const id = Math.max(0, ...this.doc.players.map((p) => p.id)) + 1;
          this.doc.players.push({
            id,
            name: `玩家 ${id}`,
            team: id,
            color: TEAM_COLORS[(id - 1) % TEAM_COLORS.length],
            controller: 'ai',
            resources: {
              [FUNDS_RESOURCE]: { current: 0, capacity: null },
              [COMMAND_POINTS_RESOURCE]: { current: 0, capacity: 5 },
            },
            ai: { aggression: 0.5 },
          });
          this.renderAll();
          break;
        }
        case 'delPlayer': {
          this.snapshot();
          const id = Number(arg);
          this.doc.players = this.doc.players.filter((p) => p.id !== id);
          this.doc.units = this.doc.units.filter((u) => u.owner !== id);
          for (let i = 0; i < this.doc.map.owners.length; i++) {
            if (this.doc.map.owners[i] === id) this.doc.map.owners[i] = 0;
          }
          if (this.owner === id) this.owner = this.doc.players[0]?.id ?? 0;
          this.renderAll();
          break;
        }
        case 'vObj': {
          this.snapshot();
          this.doc.victory = toggleObjective(this.doc.victory, arg as Objective['type'], this.doc.rules);
          this.renderRight();
          break;
        }
        case 'pObj': {
          this.snapshot();
          const [pid, type] = arg.split(':');
          const p = this.doc.players.find((x) => x.id === Number(pid));
          if (p) {
            p.objectives = toggleObjective(p.objectives ?? [], type as Objective['type'], this.doc.rules);
            if (p.objectives.length === 0) delete p.objectives;
          }
          this.renderRight();
          break;
        }
      }
      this.autosave();
    };

    const onChange = (ev: Event) => {
      const el = ev.target as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;
      const field = el.dataset.field;
      if (!field) return;
      const value = el instanceof HTMLInputElement && el.type === 'checkbox' ? el.checked : el.value;
      this.applyField(field, value, el.dataset.id);
    };

    for (const host of [this.topEl, this.leftEl, this.rightEl]) {
      host.addEventListener('click', onClick);
      host.addEventListener('change', onChange);
    }
  }

  private applyField(field: string, value: string | boolean, id?: string): void {
    const num = (v: string | boolean) => (v === '' ? null : Number(v));

    if (field.startsWith('p.')) {
      this.snapshot();
      const p = this.doc.players.find((x) => x.id === Number(id));
      if (!p) return;
      const key = field.slice(2);
      if (key === 'name') p.name = String(value);
      if (key === 'color') p.color = String(value);
      if (key === 'team') p.team = Math.max(1, Number(value) || 1);
      if (key === 'funds') {
        p.resources[FUNDS_RESOURCE] = {
          current: Math.max(0, Number(value) || 0),
          capacity: p.resources[FUNDS_RESOURCE]?.capacity ?? null,
        };
      }
      if (key === 'controller') p.controller = value === 'ai' ? 'ai' : 'human';
      if (key === 'aggression') p.ai = { aggression: Math.max(0, Math.min(1, Number(value) || 0)) };
      this.renderAll();
      return;
    }

    if (field.startsWith('r.')) {
      this.snapshot();
      const fieldName = field.slice(2);
      if (fieldName === 'baseFundsGrant') {
        const amount = Math.max(0, Number(value) || 0);
        const other = (this.doc.rules.baseResourceGrants ?? [])
          .filter((grant) => grant.resource !== FUNDS_RESOURCE);
        this.doc.rules.baseResourceGrants = amount > 0
          ? [...other, { resource: FUNDS_RESOURCE, amount }]
          : other;
        this.renderRight();
        this.autosave();
        return;
      }
      if (fieldName === 'siteFundsOverride') {
        const overrides = { ...(this.doc.rules.siteResourceOverrides ?? {}) };
        if (value === '') delete overrides[FUNDS_RESOURCE];
        else overrides[FUNDS_RESOURCE] = Math.max(0, Number(value) || 0);
        this.doc.rules.siteResourceOverrides = overrides;
        this.renderRight();
        this.autosave();
        return;
      }
      const key = fieldName as keyof RuleSet;
      const rules = this.doc.rules as Record<string, unknown>;
      if (typeof value === 'boolean') rules[key] = value;
      else if (key === 'captureMode') rules[key] = value;
      else rules[key] = num(value);
      this.renderRight();
      this.autosave();
      return;
    }

    switch (field) {
      case 'name':
        this.doc.name = String(value);
        break;
      case 'id':
        this.doc.id = String(value).trim().replace(/\s+/g, '-') || 'untitled';
        break;
      case 'description':
        this.doc.description = String(value);
        break;
      case 'author':
        this.doc.author = String(value);
        break;
      case 'elevation':
        this.elevation = Math.max(-9, Math.min(20, Math.round(Number(value) || 0)));
        this.renderLeft();
        return;
      case 'width':
        this.resize(Number(value), this.doc.map.height);
        return;
      case 'height':
        this.resize(this.doc.map.width, Number(value));
        return;
      case 'showCoords':
        this.showCoords = Boolean(value);
        this.paintBoard();
        return;
      case 'showOwners':
        this.showOwners = Boolean(value);
        this.paintBoard();
        return;
      case 'loadPreset': {
        const v = String(value);
        if (!v) return;
        const [kind, levelId] = [v.slice(0, 1), v.slice(2)];
        const level =
          kind === 'b'
            ? BUILTIN_LEVELS.find((l) => l.id === levelId)
            : loadCustomLevels().find((s) => s.level.id === levelId)?.level;
        if (level) {
          this.snapshot();
          this.replaceDocument(EditorDocument.fromLevel(level));
          this.status = `已载入 ${level.name}`;
          this.renderAll();
        }
        return;
      }
    }
    this.autosave();
    this.renderTop();
  }
}

/* ------------------------------------------------------------------ helpers */

function issueLine(i: LevelIssue): string {
  return `<li class="${i.severity}">${i.severity === 'error' ? '✕' : '!'} ${escapeHtml(i.message)}</li>`;
}

function toggleObjective(
  list: Objective[],
  type: Objective['type'],
  rules: Partial<RuleSet>,
): Objective[] {
  const has = list.some((o) => o.type === type);
  if (has) return list.filter((o) => o.type !== type);
  const next: Objective =
    type === 'surviveTurns' ? { type, turns: rules.turnLimit ?? 12 } : ({ type } as Objective);
  return [...list, next];
}

function rectTiles(a: Coord, b: Coord): Coord[] {
  const out: Coord[] = [];
  for (let y = Math.min(a.y, b.y); y <= Math.max(a.y, b.y); y++) {
    for (let x = Math.min(a.x, b.x); x <= Math.max(a.x, b.x); x++) out.push({ x, y });
  }
  return out;
}

/* ------------------------------------------------------------ level loading */

export function initialLevel(): LevelData {
  const params = new URLSearchParams(location.search);
  const wanted = params.get('level');
  if (wanted) {
    const found =
      BUILTIN_LEVELS.find((l) => l.id === wanted) ??
      loadCustomLevels().find((s) => s.level.id === wanted)?.level;
    if (found) return found;
  }
  const draft = localStorage.getItem(DRAFT_KEY);
  if (draft) {
    try {
      const level = normaliseLevel(JSON.parse(draft));
      mapFromLevel(level);
      return level;
    } catch {
      /* fall through to a blank map */
    }
  }
  return emptyLevel(20, 14);
}
