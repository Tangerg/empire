import '@empire/game-ui/styles/app.css';
import '@empire/game-ui/styles/campaign.css';
import {
  ContentPackInstaller,
  createBattleEngine,
  createContentCatalog,
  mapFromLevel,
  type LevelData,
} from '@empire/battle-engine';
import { COMMON_CONTENT_PACK } from '@empire/content-common';
import {
  ANCIENT_EMPIRES_CONTENT_PACK,
  ANCIENT_EMPIRES_LEVELS as BUILTIN_LEVELS,
} from '@empire/content-ancient-empires';
import {
  CANDIDATE_01_CONTENT_PACK,
  CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN,
} from '@empire/story-candidate-01';
import type { CampaignState } from '@empire/campaign-engine';
import {
  CANDIDATE_01_MENU_ART,
  candidate01CampaignAdapter,
  CANDIDATE_01_ART,
} from '@empire/story-candidate-01/presentation';
import {
  browserBattleSaves,
  deleteCampaignState,
  deleteCustomLevel,
  GameController,
  icon,
  loadCampaignState,
  loadCustomLevels,
  squareLayout,
  readCustomLevels,
  portraitSvg,
  StoryCampaignController,
  takePlaytest,
  terrainLayerMarkup,
  TILE,
  escapeHtml,
} from '@empire/game-ui';

/** Composition root: content and ruleset are built here, never reached for. */
const content = createContentCatalog();
new ContentPackInstaller(content).install(
  COMMON_CONTENT_PACK,
  ANCIENT_EMPIRES_CONTENT_PACK,
  CANDIDATE_01_CONTENT_PACK,
);
const engine = createBattleEngine({ content });
// Composition root: art is composed here beside the catalog and the engine, so a
// second story pack would be another entry rather than another import side effect.
const art = CANDIDATE_01_ART;
const campaignAdapter = candidate01CampaignAdapter();

const recruitCost = (unit: { recruitCosts: { resource: string; amount: number }[] }) =>
  unit.recruitCosts.map((cost) => `${cost.resource} ${cost.amount}`).join(' · ') || '不可招募';

const app = document.getElementById('app')!;
let active: GameController | StoryCampaignController | null = null;

/** Static minimap for a level card. */
function thumbnail(level: LevelData): string {
  const map = mapFromLevel(content, level);
  const colorOf = (id: number) => level.players.find((p) => p.id === id)?.color;
  const units = level.units
    .map((u) => {
      const color = level.players.find((p) => p.id === u.owner)?.color ?? '#aaa';
      return `<circle cx="${u.x * TILE + TILE / 2}" cy="${u.y * TILE + TILE / 2}" r="${TILE * 0.3}"
        fill="${color}" stroke="#0b0e14" stroke-width="2"/>`;
    })
    .join('');
  return `<svg viewBox="0 0 ${map.width * TILE} ${map.height * TILE}" preserveAspectRatio="xMidYMid slice">
    ${terrainLayerMarkup({ art, layout: squareLayout, content }, map, colorOf)}${units}
  </svg>`;
}

/** `custom` says whose level this is: one the player saved, or a built-in. */
function levelCard(level: LevelData, { custom }: { custom: boolean }): string {
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
        ${content.units.all()
          .map(
            (d) => {
              const weapons = d.weapons.map((id) => content.weapons.get(id));
              const power = Math.max(...weapons.map((weapon) => weapon.power));
              const minRange = Math.min(...weapons.map((weapon) => weapon.minRange));
              const maxRange = Math.max(...weapons.map((weapon) => weapon.maxRange));
              const damageTypes = [...new Set(weapons.map((weapon) => content.damageTypes.get(weapon.damageType).name))].join(' / ');
              return `<div class="recruit-card">
              <div class="rc-art" style="width:64px;height:auto;background:none">${portraitSvg(art, d.id, team, 64)}</div>
              <div class="rc-body">
                <div class="rc-name">${escapeHtml(d.name)}<span class="rc-cost">${icon('coin')}${escapeHtml(recruitCost(d))}</span></div>
                <div class="rc-stats">${icon('sword')}${power} · ${icon('heart')}${d.maxHp} · ${icon('boot')}${d.movement} · 射程 ${
                  minRange === maxRange ? maxRange : `${minRange}-${maxRange}`
                }</div>
                <div class="rc-stats">${damageTypes} / ${content.armorClasses.get(d.armorClass).name} / ${content.movementProfiles.get(d.movementClass).name}</div>
                <div class="rc-blurb">${escapeHtml(d.blurb)}</div>
              </div>
            </div>`;
            },
          )
          .join('')}
      </div>
    </div>
  </div>`;
}

function renderMenu(): void {
  active?.dispose();
  active = null;

  const { levels: custom, rejected } = readCustomLevels();
  const campaign = loadCampaignState(CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN);
  const campaignSave = campaign.state;
  const campaignBattles = campaignSave?.battleHistory.length ?? 0;
  const screen = document.createElement('div');
  screen.style.height = '100%';
  screen.innerHTML = `<div class="menu"><div class="menu-inner">
    <h1>远古帝国 · 战术复刻</h1>
    <p class="tagline">剧情战役型 SRPG · 确定性战斗预测 · 卡通奇幻视觉</p>
    <section class="campaign-feature">
      <img src="${CANDIDATE_01_MENU_ART}" alt="灰旗立在烧毁的边境村庄"/>
      <div class="campaign-feature-shade"></div>
      <div class="campaign-feature-copy">
        <span>完整战役 · 前三章现已可玩</span>
        <h2>断冠之誓</h2>
        <p>从十八岁的边境见习旗官开始，经历灰旗流亡与诸族远征。16 场连续战斗，选择、关系、补给与伤亡跨关保留。</p>
        <div class="campaign-feature-actions">
          ${campaignSave ? `<button class="btn primary" data-act="campaignContinue">${icon('flag')} 继续战役 · ${campaignBattles}/16</button>` : ''}
          <button class="btn ${campaignSave ? '' : 'primary'}" data-act="campaignNew">${icon('play')} ${campaignSave ? '重新开始' : '开始战役'}</button>
        </div>
      </div>
    </section>
    <div class="menu-actions">
      <a class="btn primary" href="../editor/index.html">${icon('grid')} 打开地图编辑器</a>
      <a class="btn" href="../engine-demo/index.html">${icon('crosshair')} 引擎能力 Demo</a>
      <button class="btn" data-act="codex">${icon('shield')} 兵种图鉴</button>
    </div>

    <h2>内置关卡</h2>
    <div class="level-grid">${BUILTIN_LEVELS.map((l) => levelCard(l, { custom: false })).join('')}</div>

    ${
      campaign.rejected === null
        ? ''
        : `<div class="empty-note">战役存档无法读取，进度已保留但未载入：${escapeHtml(campaign.rejected)}</div>`
    }

    <h2>我的关卡</h2>
    ${
      rejected.length === 0
        ? ''
        : `<div class="empty-note">有 ${rejected.length} 个存档无法读取，已跳过（未删除）：${
            rejected.map((entry) => escapeHtml(entry.id)).join('、')
          }</div>`
    }
    ${
      custom.length === 0
        ? `<div class="empty-note">还没有自制关卡。用地图编辑器画一张，保存后就会出现在这里。</div>`
        : `<div class="level-grid">${custom.map((s) => levelCard(s.level, { custom: true })).join('')}</div>`
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
        location.href = `../editor/index.html?level=${encodeURIComponent(id)}`;
        break;
      case 'delete':
        if (confirm('删除这个自制关卡？')) {
          try {
            deleteCustomLevel(id);
          } catch (error) {
            alert(`删除失败：${(error as Error).message}`);
          }
          renderMenu();
        }
        break;
      case 'codex':
        modal.innerHTML = codexMarkup();
        break;
      case 'campaignContinue':
        startCampaign(campaignSave);
        break;
      case 'campaignNew':
        if (!campaignSave || confirm('重新开始会覆盖当前《断冠之誓》战役进度。继续吗？')) {
          deleteCampaignState(CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN);
          startCampaign(null);
        }
        break;
      case 'closeCodex':
        modal.innerHTML = '';
        break;
    }
  });
}

function startCampaign(state: CampaignState | null): void {
  active?.dispose();
  const controller = new StoryCampaignController(campaignAdapter, state, () => renderMenu(), engine);
  active = controller;
  app.replaceChildren(controller.root);
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
  // One browser slot per level, so a battle can be put down and picked up.
  const controller = new GameController(level, () => renderMenu(), {
    engine,
    art,
    saves: browserBattleSaves(level.id),
  });
  active = controller;
  app.replaceChildren(controller.root);
}

/* The editor hands a level over through sessionStorage when you hit 试玩. */
const playtest = takePlaytest();
if (playtest.level) startGame(playtest.level);
else {
  renderMenu();
  // Say so: a rejected playtest is otherwise a 试玩 button that does nothing.
  if (playtest.rejected !== null) alert(`试玩关卡无法载入：${playtest.rejected}`);
}
