import '@empire/game-ui/styles/app.css';
import '@empire/game-ui/styles/battle.css';
import '@empire/story-candidate-01/styles/candidate-01.css';
import '@empire/game-ui/styles/campaign.css';
import {
  ContentPackInstaller,
  createBattleEngine,
  createContentCatalog,
  errorMessage,
  mapFromLevel,
  resolveRules,
  type LevelData,
  type UnitDef,
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
  takePlaytest,
  boardPiecesMarkup,
  type BoardSurfaceFactory,
  browserBattleSaves,
  deleteCampaignState,
  deleteCustomLevel,
  GameController,
  icon,
  loadCampaignState,
  loadCustomLevels,
  portraitSvg,
  readCustomLevels,
  requireMountPoint,
  amountsLabel,
  mapScenePieces,
  terrainLayerPieces,
  escapeHtml,
  squareLayout,
  StoryCampaignController,
  TILE,
  TEAM_COLORS,
} from '@empire/game-ui';
import type { ManagedBoardSurfaceFactory } from '@empire/game-ui/pixi';

/** Composition root: content and ruleset are built here, never reached for. */
const content = createContentCatalog();
new ContentPackInstaller(content).install(
  COMMON_CONTENT_PACK,
  ANCIENT_EMPIRES_CONTENT_PACK,
  CANDIDATE_01_CONTENT_PACK,
);
const engine = createBattleEngine({ content });

/**
 * The tiling one level is laid out on, which is the level's own answer.
 *
 * A card used to be measured on a hardcoded `square4`. Every shipped level happens
 * to be square, so it looked right — and a hex level saved from the editor would
 * have had its ground composed at square coordinates, or, since a painted scene
 * refuses a tiling its sheets cannot paint, crashed the menu.
 */
const tilingOf = (level: LevelData) => engine.rules.grids.get(resolveRules(level).grid);
// Composition root: art is composed here beside the catalog and the engine, so a
// second story pack would be another entry rather than another import side effect.
const art = CANDIDATE_01_ART;
const campaignAdapter = candidate01CampaignAdapter();

// What a unit costs, in the words this engine's resources go by.
const recruitCost = (unit: UnitDef) =>
  amountsLabel(engine.rules.resources, unit.recruitCosts, '不可招募');

const app = requireMountPoint('app');
let active: GameController | StoryCampaignController | null = null;

/** Static minimap for a level card. */
function thumbnail(level: LevelData): string {
  const map = mapFromLevel(content, level);
  const colorOf = (id: number) => level.players.find((p) => p.id === id)?.color;
  // The scene the board would paint, under and over the tiles the per-cell
  // painters draw. A card that showed only the tiles showed buildings on nothing.
  const scene = mapScenePieces({ art, content, grid: tilingOf(level) }, level.id, map);
  const units = level.units
    .map((u) => {
      const color = level.players.find((p) => p.id === u.owner)?.color ?? '#aaa';
      return `<circle cx="${u.x * TILE + TILE / 2}" cy="${u.y * TILE + TILE / 2}" r="${TILE * 0.3}"
        fill="${color}" stroke="#0b0e14" stroke-width="2"/>`;
    })
    .join('');
  return `<svg viewBox="0 0 ${map.width * TILE} ${map.height * TILE}" preserveAspectRatio="xMidYMid slice">
    ${boardPiecesMarkup(scene.ground)}${boardPiecesMarkup(terrainLayerPieces({ art, layout: squareLayout, content }, map, colorOf))}${boardPiecesMarkup([...scene.underUnits, ...scene.overUnits])}${units}
  </svg>`;
}

/** `custom` says whose level this is: one the player saved, or a built-in. */
function levelCard(level: LevelData, { custom }: { custom: boolean }): string {
  const size = `${level.width}×${level.height}`;
  return `<div class="level-card">
    <div class="level-thumb" data-act="play" data-arg="${escapeHtml(level.id)}">${thumbnail(level)}</div>
    <div class="level-body">
      <div class="level-title">${escapeHtml(level.name)} <small>${size}</small></div>
      <p class="level-desc">${escapeHtml(level.description ?? '')}</p>
      <div class="level-foot">
        <button class="btn primary" data-act="play" data-arg="${escapeHtml(level.id)}">${icon('play')} 开始</button>
        <button class="btn ghost" data-act="edit" data-arg="${escapeHtml(level.id)}">${icon('grid')} 在编辑器打开</button>
        ${custom ? `<button class="btn ghost danger" data-act="delete" data-arg="${escapeHtml(level.id)}" title="删除这张自制关卡">${icon('trash')}</button>` : ''}
      </div>
    </div>
  </div>`;
}

function codexMarkup(): string {
  const team = TEAM_COLORS[0];
  return `<div class="modal">
    <div class="modal-box">
      <div class="modal-head">
        <h2>兵种图鉴</h2>
        <button class="btn ghost" data-act="closeModal">✕</button>
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
              <div class="rc-art" style="width:64px;height:auto;background:none">${portraitSvg(art, d, team, 64)}</div>
              <div>
                <div class="rc-name"><span class="rc-title">${escapeHtml(d.name)}</span><span class="rc-cost">${icon('coin')}${escapeHtml(recruitCost(d))}</span></div>
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

/**
 * Every level this build can be played on, as one overlay over the title art.
 *
 * The picture is the screen and the interface lies over it — the same rule the
 * battle already follows. These used to be two sections of a scrolling document
 * below the fold, under headings, which is what made the entry to the game read
 * as a page about the game.
 */
function skirmishMarkup(
  custom: readonly { level: LevelData }[],
  notes: readonly string[],
): string {
  return `<div class="modal">
    <div class="modal-box">
      <div class="modal-head">
        <h2>单场战斗</h2>
        <button class="btn ghost" data-act="closeModal">✕</button>
      </div>
      ${notes.map((note) => `<div class="empty-note">${note}</div>`).join('')}
      <div class="level-grid">${BUILTIN_LEVELS.map((l) => levelCard(l, { custom: false })).join('')}</div>
      <h3 class="modal-section">我的关卡</h3>
      ${
        custom.length === 0
          ? `<div class="empty-note">还没有自制关卡。用地图编辑器画一张，保存后就会出现在这里。</div>`
          : `<div class="level-grid">${custom.map((s) => levelCard(s.level, { custom: true })).join('')}</div>`
      }
    </div>
  </div>`;
}

/** One line of the title screen's menu. A link when it leaves, a button when it acts. */
const titleItem = (
  label: string,
  { act, href, note, primary }: { act?: string; href?: string; note?: string; primary?: boolean },
): string => {
  const inner = `<span>${label}</span>${note ? `<small>${note}</small>` : ''}`;
  const cls = `title-item${primary ? ' primary' : ''}`;
  return href
    ? `<a class="${cls}" href="${href}">${inner}</a>`
    : `<button class="${cls}" data-act="${act ?? ''}">${inner}</button>`;
};

function renderMenu(): void {
  active?.dispose();
  active = null;

  const { levels: custom, rejected } = readCustomLevels();
  const campaign = loadCampaignState(CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN);
  const campaignSave = campaign.state;
  const campaignBattles = campaignSave?.battleHistory.length ?? 0;
  // A save that cannot be read is the player's progress. Say so on the screen it
  // belongs to rather than swallowing it — but not in the title's own plate.
  const notes = [
    ...(campaign.rejected === null
      ? []
      : [`战役存档无法读取，进度已保留但未载入：${escapeHtml(campaign.rejected)}`]),
    ...(rejected.length === 0
      ? []
      : [`有 ${rejected.length} 个自制关卡存档无法读取，已跳过（未删除）：${
          rejected.map((entry) => escapeHtml(entry.id)).join('、')
        }`]),
  ];
  const screen = document.createElement('div');
  screen.style.height = '100%';
  screen.innerHTML = `<div class="title">
    <img class="title-art" src="${CANDIDATE_01_MENU_ART}" alt="灰旗立在烧毁的边境村庄"/>
    <div class="title-shade"></div>
    <div class="title-plate">
      <span class="title-kicker">剧情战役型 SRPG · 确定性战斗预测</span>
      <h1>远古帝国</h1>
      <p class="title-sub">《断冠之誓》从十八岁的边境见习旗官开始，经历灰旗流亡与诸族远征。16 场连续战斗，选择、关系、补给与伤亡跨关保留。</p>
      <nav class="title-menu">
        ${campaignSave ? titleItem(`${icon('flag')} 继续战役`, { act: 'campaignContinue', note: `${campaignBattles}/16`, primary: true }) : ''}
        ${titleItem(`${icon('play')} ${campaignSave ? '重新开始战役' : '开始战役'}`, { act: 'campaignNew', primary: !campaignSave })}
        ${titleItem(`${icon('crosshair')} 单场战斗`, { act: 'skirmish', note: `${BUILTIN_LEVELS.length + custom.length} 关` })}
        ${titleItem(`${icon('shield')} 兵种图鉴`, { act: 'codex' })}
        ${titleItem(`${icon('grid')} 地图编辑器`, { href: '../editor/index.html' })}
        ${titleItem(`${icon('crosshair')} 引擎能力 Demo`, { href: '../engine-demo/index.html' })}
      </nav>
      ${notes.length === 0 ? '' : `<div class="title-note">${notes.length} 条存档提示，见「单场战斗」</div>`}
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
        if (level) play(level);
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
            alert(`删除失败：${errorMessage(error)}`);
          }
          renderMenu();
        }
        break;
      case 'codex':
        modal.innerHTML = codexMarkup();
        break;
      case 'skirmish':
        modal.innerHTML = skirmishMarkup(custom, notes);
        break;
      case 'campaignContinue':
        openCampaign(campaignSave);
        break;
      case 'campaignNew':
        if (!campaignSave || confirm('重新开始会覆盖当前《断冠之誓》战役进度。继续吗？')) {
          deleteCampaignState(CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN);
          openCampaign(null);
        }
        break;
      case 'closeModal':
        modal.innerHTML = '';
        break;
    }
  });
}

async function startCampaign(state: CampaignState | null): Promise<void> {
  const renderer = await chosenRenderer();
  active?.dispose();
  const controller = new StoryCampaignController(campaignAdapter, state, () => renderMenu(), {
    engine,
    art,
    renderer,
  });
  active = controller;
  app.replaceChildren(controller.root);
}

function openCampaign(state: CampaignState | null): void {
  void startCampaign(state).catch((cause) => alert(`无法开始战役：${errorMessage(cause)}`));
}

function findLevel(id: string): LevelData | null {
  return (
    BUILTIN_LEVELS.find((l) => l.id === id) ??
    loadCustomLevels().find((s) => s.level.id === id)?.level ??
    null
  );
}

/**
 * Which renderer this session draws battles with.
 *
 * `?renderer=pixi` picks the GPU backend. A choice made here rather than a default,
 * because the SVG one is what every shipped level has been drawn and reviewed with
 * — and because choosing is the application root's job, not the board's.
 *
 * Prepared once and kept: a Pixi renderer decides between WebGPU and WebGL and
 * builds a context, which is worth doing per session rather than per battle.
 */
const wantsPixi = new URLSearchParams(location.search).get('renderer') === 'pixi';
let pixi: ManagedBoardSurfaceFactory | null = null;

async function chosenRenderer(): Promise<BoardSurfaceFactory | undefined> {
  if (!wantsPixi) return undefined;
  if (!pixi) {
    // Imported here, not at the top: `pixi.js` is 492 KB, and a session that never
    // asks for it should not download it.
    const { preparePixiBoardSurface } = await import('@empire/game-ui/pixi');
    pixi = await preparePixiBoardSurface();
  }
  return pixi;
}

window.addEventListener('pagehide', () => {
  active?.dispose();
  pixi?.dispose();
}, { once: true });

async function startGame(level: LevelData): Promise<void> {
  const renderer = await chosenRenderer();
  active?.dispose();
  // One browser slot per level, so a battle can be put down and picked up.
  const controller = new GameController(level, () => renderMenu(), {
    engine,
    art,
    renderer,
    saves: browserBattleSaves(level.id),
  });
  active = controller;
  app.replaceChildren(controller.root);
}

/** A GPU that will not come up is worth saying out loud, not swallowing. */
const play = (level: LevelData): void => {
  startGame(level).catch((cause) => alert(`无法开始战斗：${errorMessage(cause)}`));
};

/* The editor hands a level over through sessionStorage when you hit 试玩. */
const playtest = takePlaytest();
if (playtest.level) play(playtest.level);
else {
  renderMenu();
  // Say so: a rejected playtest is otherwise a 试玩 button that does nothing.
  if (playtest.rejected !== null) alert(`试玩关卡无法载入：${playtest.rejected}`);
}
