import '@empire/game-ui/styles/app.css';
import '@empire/game-ui/styles/battle.css';
import '@empire/story-candidate-01/styles/candidate-01.css';
import './experience.css';
import {
  ContentPackInstaller,
  createBattleEngine,
  createContentCatalog,
} from '@empire/battle-engine';
import { COMMON_CONTENT_PACK } from '@empire/content-common';
import { ANCIENT_EMPIRES_CONTENT_PACK } from '@empire/content-ancient-empires';
import { CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';
import { CANDIDATE_01_ART, CANDIDATE_01_MENU_ART } from '@empire/story-candidate-01/presentation';
import { experienceLevel } from '@empire/experience-lab';
import {
  GameController,
  icon,
  requireMountPoint,
  escapeHtml,
} from '@empire/game-ui';

/** Composition root: this app declares its own content and ruleset. */
const content = createContentCatalog();
new ContentPackInstaller(content).install(
  COMMON_CONTENT_PACK,
  ANCIENT_EMPIRES_CONTENT_PACK,
  CANDIDATE_01_CONTENT_PACK,
);
const engine = createBattleEngine({ content });
// Composition root: this lab draws with the candidate pack's art.
const art = CANDIDATE_01_ART;

const app = requireMountPoint('app');

let game: GameController | null = null;

function renderLanding(): void {
  game?.dispose();
  game = null;
  const level = experienceLevel();
  app.innerHTML = `<main class="experience-entry">
    <img class="experience-art" src="${CANDIDATE_01_MENU_ART}" alt="灰旗军在边境集结"/>
    <div class="experience-shade"></div>
    <section class="experience-copy">
      <span class="experience-kicker">种子玩家体验版 · 单关完整战术切片</span>
      <h1>灰旗试炼</h1>
      <p>${escapeHtml(level.description ?? '')}</p>
      <div class="experience-pillars">
        <div><b>三条战线</b><small>高地奇袭、中央争夺、右路攻城</small></div>
        <div><b>九人联队</b><small>指挥、援护、治疗、工程、骑兵与幼龙</small></div>
        <div><b>动态战场</b><small>部署、烟尘、增援、士气与阶段信号</small></div>
      </div>
      <aside><b>给第一次玩的朋友</b><span>先选中单位，再点落点和行动。无需歼灭所有敌人：让莱娅存活、控制中央区域并击退凯恩即可。</span></aside>
      <button class="experience-start">${icon('play')} 开始试炼</button>
      <small class="experience-note">本体验版不写入战役存档，可随时重开。</small>
    </section>
  </main>`;
  app.querySelector('.experience-start')?.addEventListener('click', start);
}

function start(): void {
  const controller = new GameController(experienceLevel(), renderLanding, {
    art,
    engine,
    exitLabel: '退出试炼',
    completionLabel: '返回体验首页',
  });
  game = controller;
  app.replaceChildren(controller.root);
}

renderLanding();
