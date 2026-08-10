import '../styles/app.css';
import { icon } from '../art/icons';
import { portraitSvg } from '../art/portraits';
import { TILE, terrainLayerMarkup } from '../art/terrain';
import { UnitTypes, ARMOR_LABEL, DAMAGE_TYPE_LABEL, MOVEMENT_LABEL } from '../core/data/units';
import { mapFromLevel } from '../core/mapio';
import type { LevelData } from '../core/types';
import { BUILTIN_LEVELS, deleteCustomLevel, loadCustomLevels, takePlaytest } from '../levels';
import { GameController } from '../ui/game';
import { escapeHtml } from '../ui/hud';

const app = document.getElementById('app')!;
let active: GameController | null = null;

/** Static minimap for a level card. */
function thumbnail(level: LevelData): string {
  const map = mapFromLevel(level);
  const colorOf = (id: number) => level.players.find((p) => p.id === id)?.color;
  const units = level.units
    .map((u) => {
      const color = level.players.find((p) => p.id === u.owner)?.color ?? '#aaa';
      return `<circle cx="${u.x * TILE + TILE / 2}" cy="${u.y * TILE + TILE / 2}" r="${TILE * 0.3}"
        fill="${color}" stroke="#0b0e14" stroke-width="2"/>`;
    })
    .join('');
  return `<svg viewBox="0 0 ${map.width * TILE} ${map.height * TILE}" preserveAspectRatio="xMidYMid slice">
    ${terrainLayerMarkup(map, colorOf)}${units}
  </svg>`;
}

function levelCard(level: LevelData, custom: boolean): string {
  const size = `${level.width}×${level.height}`;
  return `<div class="level-card" data-level="${escapeHtml(level.id)}" data-custom="${custom}">
    <div class="level-thumb" data-act="play" data-arg="${escapeHtml(level.id)}">${thumbnail(level)}</div>
    <div class="level-body">
      <div class="level-title">${escapeHtml(level.name)} <small>${size}</small></div>
      <p class="level-desc">${escapeHtml(level.description ?? '')}</p>
      <div class="level-foot">
        <button class="btn primary" data-act="play" data-arg="${escapeHtml(level.id)}">${icon('play')} 开始</button>
        <button class="btn ghost" data-act="edit" data-arg="${escapeHtml(level.id)}">${icon('grid')} 在编辑器打开</button>
        ${custom ? `<button class="btn ghost danger" data-act="delete" data-arg="${escapeHtml(level.id)}">${icon('trash')}</button>` : ''}
      </div>
    </div>
  </div>`;
}

function codexMarkup(): string {
  const team = '#3f7fd8';
  return `<div class="modal">
    <div class="modal-box">
      <div class="modal-head">
        <h2>兵种图鉴</h2>
        <button class="btn ghost" data-act="closeCodex">✕</button>
      </div>
      <div class="recruit-grid">
        ${UnitTypes.all()
          .map(
            (d) => `<div class="recruit-card">
              <div class="rc-art" style="width:64px;height:auto;background:none">${portraitSvg(d.id, team, 64)}</div>
              <div class="rc-body">
                <div class="rc-name">${escapeHtml(d.name)}<span class="rc-cost">${icon('coin')}${d.cost}</span></div>
                <div class="rc-stats">${icon('sword')}${d.attack} · ${icon('heart')}${d.maxHp} · ${icon('boot')}${d.movement} · 射程 ${
                  d.minRange === d.maxRange ? d.maxRange : `${d.minRange}-${d.maxRange}`
                }</div>
                <div class="rc-stats">${DAMAGE_TYPE_LABEL[d.damageType]} / ${ARMOR_LABEL[d.armorClass]} / ${MOVEMENT_LABEL[d.movementClass]}</div>
                <div class="rc-blurb">${escapeHtml(d.blurb)}</div>
              </div>
            </div>`,
          )
          .join('')}
      </div>
    </div>
  </div>`;
}

function renderMenu(): void {
  active?.dispose();
  active = null;

  const custom = loadCustomLevels();
  const screen = document.createElement('div');
  screen.style.height = '100%';
  screen.innerHTML = `<div class="menu"><div class="menu-inner">
    <h1>远古帝国 · 战术复刻</h1>
    <p class="tagline">回合制战棋 · 确定性战斗预测 · 全部美术由 SVG 绘制</p>
    <div class="menu-actions">
      <a class="btn primary" href="./editor.html">${icon('grid')} 打开地图编辑器</a>
      <button class="btn" data-act="codex">${icon('shield')} 兵种图鉴</button>
    </div>

    <h2>内置关卡</h2>
    <div class="level-grid">${BUILTIN_LEVELS.map((l) => levelCard(l, false)).join('')}</div>

    <h2>我的关卡</h2>
    ${
      custom.length === 0
        ? `<div class="empty-note">还没有自制关卡。用地图编辑器画一张，保存后就会出现在这里。</div>`
        : `<div class="level-grid">${custom.map((s) => levelCard(s.level, true)).join('')}</div>`
    }
  </div>
  <div class="modal-root" id="menu-modal"></div>
  </div>`;
  app.replaceChildren(screen);

  const modal = screen.querySelector('#menu-modal') as HTMLElement;
  // Listener lives on the freshly created screen, so re-rendering cannot stack them.
  screen.addEventListener('click', (ev) => {
    const el = (ev.target as HTMLElement).closest('[data-act]') as HTMLElement | null;
    if (!el) return;
    const id = el.dataset.arg ?? '';
    switch (el.dataset.act) {
      case 'play': {
        const level = findLevel(id);
        if (level) startGame(level);
        break;
      }
      case 'edit':
        location.href = `./editor.html?level=${encodeURIComponent(id)}`;
        break;
      case 'delete':
        if (confirm('删除这个自制关卡？')) {
          deleteCustomLevel(id);
          renderMenu();
        }
        break;
      case 'codex':
        modal.innerHTML = codexMarkup();
        break;
      case 'closeCodex':
        modal.innerHTML = '';
        break;
    }
  });
}

function findLevel(id: string): LevelData | null {
  return (
    BUILTIN_LEVELS.find((l) => l.id === id) ??
    loadCustomLevels().find((s) => s.level.id === id)?.level ??
    null
  );
}

function startGame(level: LevelData): void {
  active?.dispose();
  const controller = new GameController(level, () => renderMenu());
  active = controller;
  app.replaceChildren(controller.root);
}

/* The editor hands a level over through sessionStorage when you hit 试玩. */
const pending = takePlaytest();
if (pending) startGame(pending);
else renderMenu();
