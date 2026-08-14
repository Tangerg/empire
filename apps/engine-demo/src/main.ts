import '@empire/game-ui/styles/app.css';
import '@empire/game-ui/styles/demo.css';
import { ContentPackInstaller, createContentCatalog } from '@empire/battle-engine';
import { COMMON_CONTENT_PACK } from '@empire/content-common';
import { ANCIENT_EMPIRES_CONTENT_PACK } from '@empire/content-ancient-empires';
import { GENERIC_ART, icon, terrainSwatch, unitIcon } from '@empire/game-ui';
import { buildBattleEngine, createDefaultMicrokernel } from '@empire/battle-engine/plugins/default';
import {
  type ResourceSubject,
  playerResource,
  unitResource,
  weaponResource,
} from '@empire/battle-engine/resources';
import { GameSession } from '@empire/battle-engine/session';
import type { CombatPlan } from '@empire/battle-engine/combat-plan';
import type { GameEvent, LevelData, Unit } from '@empire/battle-engine/types';

const content = createContentCatalog();
new ContentPackInstaller(content).install(COMMON_CONTENT_PACK, ANCIENT_EMPIRES_CONTENT_PACK);

const DEMO_LEVEL: LevelData = {
  schema: 2,
  id: 'engine-capability-demo',
  name: '微内核演示场',
  description: '同一套引擎展示预测、实体资源、范围攻击、有限武器与招募。',
  width: 7,
  height: 5,
  terrain: [
    '.......',
    '...T...',
    '.......',
    '...h...',
    '......C',
  ],
  owners: [{ x: 6, y: 4, owner: 1 }],
  units: [
    { key: 'javelin', x: 0, y: 0, unit: 'soldier', owner: 1 },
    {
      key: 'hero',
      x: 2,
      y: 2,
      unit: 'knight',
      owner: 1,
      resources: { momentum: { current: 135, capacity: 150 } },
    },
    { key: 'javelin-target', x: 2, y: 0, unit: 'archer', owner: 2 },
    { key: 'hero-target', x: 3, y: 2, unit: 'ogre', owner: 2 },
    { key: 'area-north', x: 3, y: 1, unit: 'soldier', owner: 2 },
    { key: 'area-south', x: 3, y: 3, unit: 'mage', owner: 2 },
  ],
  players: [
    {
      id: 1,
      name: '演示蓝军',
      team: 1,
      color: '#4c8dff',
      controller: 'human',
      resources: {
        funds: { current: 450, capacity: null },
        command_points: { current: 3, capacity: 5 },
      },
    },
    {
      id: 2,
      name: '演示红军',
      team: 2,
      color: '#e0604f',
      controller: 'ai',
      resources: {
        funds: { current: 0, capacity: null },
        command_points: { current: 0, capacity: 5 },
      },
      ai: { aggression: 0.5 },
    },
  ],
  rules: {},
  victory: [{ type: 'routEnemies' }],
};

const appElement = document.getElementById('app');
if (!appElement) throw new Error('missing #app');
const app: HTMLElement = appElement;

const kernel = createDefaultMicrokernel(content);
const engine = buildBattleEngine(kernel.compose());
let session = new GameSession(DEMO_LEVEL, engine);
let preview: CombatPlan | null = null;
let events: GameEvent[] = [];
let headline = '先点“预测”，确认资源与血量都不会变化。';

const html = (value: string): string => value.replace(/[&<>"']/g, (character) => ({
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;',
})[character]!);

function byKey(key: string): Unit | undefined {
  return session.state.units.find((unit) => unit.key === key);
}

function resourceName(id: string): string {
  // Asked, not attempted: a registry answers `tryGet` for a name it does not
  // know, and wrapping the *committing* lookup in a catch answered "there is no
  // such resource" and "the registry threw for some other reason" alike.
  return engine.rules.resources.adapters.tryGet(id)?.name ?? id;
}

function accountRows(subject: ResourceSubject): string {
  const rows = engine.rules.resources.adapters.keys().flatMap((id) => {
    if (!engine.rules.resources.hasAccount(id, subject)) return [];
    const account = engine.rules.resources.inspect(id, subject);
    const current = account.current === null ? '∞' : account.current;
    const capacity = account.capacity === null ? '' : ` / ${account.capacity}`;
    return [`<div class="account-row"><span>${html(resourceName(id))}</span><b>${current}${capacity}</b></div>`];
  });
  return rows.join('') || '<div class="account-empty">无独立账户</div>';
}

function unitAt(x: number, y: number): Unit | undefined {
  return session.state.units.find((unit) => unit.x === x && unit.y === y);
}

function boardMarkup(): string {
  const state = session.state;
  const affected = new Set((preview?.affectedCells ?? []).map((cell) => `${cell.x},${cell.y}`));
  const aimed = preview ? `${preview.aimedAt.x},${preview.aimedAt.y}` : '';
  return Array.from({ length: state.map.width * state.map.height }, (_, index) => {
    const x = index % state.map.width;
    const y = Math.floor(index / state.map.width);
    const terrain = state.map.tiles[index];
    const owner = state.players.find((player) => player.id === state.map.owners[index]);
    const unit = unitAt(x, y);
    const unitOwner = unit && state.players.find((player) => player.id === unit.owner);
    const key = `${x},${y}`;
    return `<div class="demo-tile ${affected.has(key) ? 'affected' : ''} ${aimed === key ? 'aimed' : ''}"
      data-cell="${key}" title="(${key})">
      <div class="tile-art">${terrainSwatch(GENERIC_ART, terrain, owner?.color)}</div>
      ${unit ? `<div class="demo-unit" data-unit="${html(unit.key ?? String(unit.id))}">
        ${unitIcon(GENERIC_ART, unit.type, unitOwner?.color ?? '#9aa3ad', 46)}
        <span class="unit-label">${html(content.units.get(unit.type).name)}</span>
        <span class="hp-chip">${unit.hp}</span>
      </div>` : ''}
      <span class="cell-coord">${x},${y}</span>
    </div>`;
  }).join('');
}

function forecastMarkup(): string {
  if (!preview) {
    return `<div class="empty-state">
      ${icon('crosshair', 22)}
      <b>尚未生成预测</b>
      <span>预测只读取状态，不扣资源、不改变 HP。</span>
    </div>`;
  }
  const primary = preview.primaryUnit;
  const cost = content.weapons.get(preview.weapon).resourceCosts
    .map((entry) => `${resourceName(entry.resource)} −${entry.amount}`)
    .join(' · ');
  return `<div class="forecast-result">
    <div class="forecast-main"><span>主目标伤害</span><b>${primary?.strike.damage ?? 0}</b></div>
    <div class="forecast-main"><span>波及单位</span><b>${preview.unitHits.length}</b></div>
    <div class="forecast-main"><span>提交消耗</span><b>${html(cost || '无')}</b></div>
    <div class="forecast-main"><span>反击</span><b>${primary?.counter ? primary.counter.damage : '无'}</b></div>
    <p>高亮格来自同一份 <code>CombatPlan</code>；执行阶段不会重新选择目标。</p>
  </div>`;
}

function describeEvent(event: GameEvent): string {
  switch (event.type) {
    case 'resourceChanged':
      return `${resourceName(event.resource)} ${event.amount >= 0 ? '+' : ''}${event.amount} → ${event.current}`;
    case 'attack':
      return `主攻击：单位 #${event.defender} 受到 ${event.damage} 伤害`;
    case 'areaAttack':
      return `范围攻击：单位 #${event.defender} 受到 ${event.damage} 伤害`;
    case 'counter':
      return `反击：${event.damage} 伤害`;
    case 'recruit':
      return `招募单位 #${event.unit}`;
    case 'rankProgressChanged':
      return `单位 #${event.unit} 军衔进度 +${event.amount}`;
    case 'rankChanged':
      return `单位 #${event.unit} 晋升到军衔 ${event.to}`;
    default:
      return event.type;
  }
}

function eventMarkup(): string {
  if (events.length === 0) return '<div class="event-empty">执行行动后，这里会出现引擎语义事件。</div>';
  return events.map((event, index) => `<li><span>${String(index + 1).padStart(2, '0')}</span><code>${html(event.type)}</code><b>${html(describeEvent(event))}</b></li>`).join('');
}

function canAttack(key: string, weapon: string): boolean {
  const unit = byKey(key);
  if (!unit || unit.done) return false;
  return session.commandsAt(unit, unit).some((option) => option.weapon === weapon);
}

function render(): void {
  const player = session.state.players[0];
  const hero = byKey('hero');
  const javelin = byKey('javelin');
  const castleOccupied = Boolean(unitAt(6, 4));
  const pluginCards = [...kernel.pluginManifest()].map(([id, version], index) =>
    `<div class="plugin-card"><span>0${index + 1}</span><b>${html(id.replace('engine.', ''))}</b><em>v${version}</em></div>`,
  ).join('');

  app.innerHTML = `<main class="demo-shell">
    <header class="demo-header">
      <div>
        <a class="back-link" href="./">${icon('undo')} 返回游戏</a>
        <p class="eyebrow">SRPG ENGINE / LIVE CAPABILITY DEMO</p>
        <h1>实体没有被拆散，规则可以被组合。</h1>
        <p class="lede">这不是静态原型。下面所有预测、伤害、资源扣减和事件都由正式 <code>GameSession → BattleEngine</code> 链路产生。</p>
      </div>
      <button class="btn ghost" data-act="reset">${icon('undo')} 重置演示</button>
    </header>

    <section class="plugin-strip" aria-label="已安装插件">${pluginCards}</section>

    <section class="demo-grid">
      <div class="battle-column">
        <div class="section-heading">
          <div><span>01</span><h2>战场与正式行动</h2></div>
          <p>蓝色十字是预测产生的范围，不是 UI 自己推算。</p>
        </div>
        <div class="battle-card">
          <div class="demo-board" style="--columns:${session.state.map.width}">${boardMarkup()}</div>
          <div class="legend"><span><i class="blue"></i>我方</span><span><i class="red"></i>敌方</span><span><i class="pulse"></i>预测波及</span></div>
        </div>

        <div class="action-deck">
          <article class="action-card signature">
            <div class="action-title"><span>${icon('sword', 20)}</span><div><b>英雄 · 战意裂阵</b><em>单位资源 + 范围 CombatPlan</em></div></div>
            <p>需要气势 120，提交消耗 20；一次攻击波及十字区域中的三个敌人。</p>
            <div class="action-buttons">
              <button class="btn" data-act="forecast-hero" ${canAttack('hero', 'heroic_breakthrough') ? '' : 'disabled'}>${icon('crosshair')} 预测</button>
              <button class="btn primary" data-act="execute-hero" ${canAttack('hero', 'heroic_breakthrough') ? '' : 'disabled'}>${icon('play')} 执行</button>
            </div>
          </article>
          <article class="action-card">
            <div class="action-title"><span>${icon('bow', 20)}</span><div><b>剑士 · 投枪</b><em>武器自身账户</em></div></div>
            <p>投枪次数存放在该单位的 WeaponState 上；执行一次只扣这一把武器。</p>
            <div class="action-buttons">
              <button class="btn" data-act="forecast-javelin" ${canAttack('javelin', 'soldier_javelin') ? '' : 'disabled'}>${icon('crosshair')} 预测</button>
              <button class="btn primary" data-act="execute-javelin" ${canAttack('javelin', 'soldier_javelin') ? '' : 'disabled'}>${icon('play')} 执行</button>
            </div>
          </article>
          <article class="action-card">
            <div class="action-title"><span>${icon('coin', 20)}</span><div><b>城堡 · 招募剑士</b><em>玩家账户</em></div></div>
            <p>从 Player 账户支付 100 资金，实体状态和事件流同步更新。</p>
            <button class="btn primary wide" data-act="recruit" ${castleOccupied ? 'disabled' : ''}>${icon('flag')} 支付并招募</button>
          </article>
        </div>
      </div>

      <aside class="inspector-column">
        <section class="inspector-card status-card">
          <div class="card-kicker">LIVE STATUS</div>
          <h2>${html(headline)}</h2>
          <div class="truth-pill"><i></i>所有数值来自当前 GameState</div>
        </section>

        <section class="inspector-card">
          <div class="card-title"><span>02</span><h3>实体自有账户</h3></div>
          <div class="entity-account">
            <div><b>${html(player.name)}</b><em>PlayerState.resources</em></div>
            ${accountRows(playerResource(player))}
          </div>
          ${hero ? `<div class="entity-account">
            <div><b>英雄骑士</b><em>Unit.resources</em></div>
            ${accountRows(unitResource(hero))}
          </div>` : ''}
          ${javelin ? `<div class="entity-account">
            <div><b>剑士投枪</b><em>UnitWeaponState.resources</em></div>
            ${accountRows(weaponResource(javelin, 'soldier_javelin'))}
          </div>` : ''}
        </section>

        <section class="inspector-card">
          <div class="card-title"><span>03</span><h3>预测真值</h3></div>
          ${forecastMarkup()}
        </section>

        <section class="inspector-card event-card" aria-live="polite">
          <div class="card-title"><span>04</span><h3>本次语义事件</h3></div>
          <ol>${eventMarkup()}</ol>
        </section>
      </aside>
    </section>
  </main>`;
}

function forecast(attackerKey: string, targetKey: string, weapon: string): void {
  const attacker = byKey(attackerKey);
  const target = byKey(targetKey);
  if (!attacker || !target) throw new Error('演示单位已离场，请重置');
  const before = JSON.stringify(session.state);
  preview = session.attackPlan(attacker, target, attacker, weapon);
  if (JSON.stringify(session.state) !== before) throw new Error('预测意外修改了状态');
  events = [];
  headline = `已预测 ${content.weapons.get(weapon).name}：状态未发生变化。`;
}

function execute(attackerKey: string, targetKey: string, weapon: string): void {
  const attacker = byKey(attackerKey);
  const target = byKey(targetKey);
  if (!attacker || !target) throw new Error('演示单位已离场，请重置');
  preview = session.attackPlan(attacker, target, attacker, weapon);
  events = session.dispatch({
    kind: 'command',
    unit: attacker.id,
    path: [{ x: attacker.x, y: attacker.y }],
    command: { ability: 'attack', weapon, target: { x: target.x, y: target.y } },
  });
  preview = null;
  headline = `${content.weapons.get(weapon).name}已由正式行动管线提交。`;
}

app.addEventListener('click', (click) => {
  const button = (click.target as HTMLElement).closest<HTMLElement>('[data-act]');
  if (!button || button.hasAttribute('disabled')) return;
  try {
    switch (button.dataset.act) {
      case 'forecast-hero':
        forecast('hero', 'hero-target', 'heroic_breakthrough');
        break;
      case 'execute-hero':
        execute('hero', 'hero-target', 'heroic_breakthrough');
        break;
      case 'forecast-javelin':
        forecast('javelin', 'javelin-target', 'soldier_javelin');
        break;
      case 'execute-javelin':
        execute('javelin', 'javelin-target', 'soldier_javelin');
        break;
      case 'recruit':
        preview = null;
        events = session.dispatch({ kind: 'recruit', at: { x: 6, y: 4 }, unit: 'soldier' });
        headline = '招募完成：资金仍属于 PlayerState，资源系统只执行规则。';
        break;
      case 'reset':
        session = new GameSession(DEMO_LEVEL, engine);
        preview = null;
        events = [];
        headline = '演示已重置，所有实体账户恢复到关卡初始值。';
        break;
    }
  } catch (error) {
    headline = `行动被拒绝：${(error as Error).message}`;
  }
  render();
});

render();
