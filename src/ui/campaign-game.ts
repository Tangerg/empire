import '../styles/campaign.css';
import { CANDIDATE_01_PORTRAITS, candidate01StoryArt } from '../art/candidate-01-story';
import { saveCampaignState } from '../application/campaign-storage';
import { CampaignBattleBridge, CampaignRuntime, type BattleRequest, type CampaignState } from '../campaign';
import { unitDef } from '../core/data/units';
import type { GameEvent, GameState } from '../core/types';
import {
  CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN,
  CANDIDATE_01_LEVELS,
  CANDIDATE_01_SPEAKER_NAMES,
  applyCandidate01BattleContext,
  applyCandidate01BattleResultPolicy,
  candidate01Choice,
  candidate01Level,
  candidate01Story,
} from '../content/candidate-01';
import { GameController, type BattleCompletionSnapshot } from './game';
import { escapeHtml } from './html';

interface CampaignBattleSummary {
  title: string;
  outcome: string;
  turns: number;
  alliesRemaining: number;
  enemiesRemaining: number;
  fallen: string[];
  signals: string[];
  events: GameEvent[];
}

const JOIN_AFTER: Readonly<Record<string, number>> = { mirelle: 3, bran: 4, tasha: 7, ivra: 9 };
const RELATION_LABELS: Readonly<Record<string, string>> = {
  cain: '凯恩', mirelle: '米蕾尔', tasha: '塔莎', refugees: '灰境难民', silverwood: '银林',
  'mountain-forge': '山炉氏族', 'named-dead': '归名者',
};

function chapterTitle(chapter: number): string {
  return chapter === 1 ? '边境之火' : chapter === 2 ? '灰旗流亡' : '古老诸族';
}

function levelOrder(levelId: string): number {
  return Number(levelId.slice(-2));
}

/** Application shell for story-one; campaign state and battle state remain separate aggregates. */
export class Candidate01CampaignController {
  readonly root = document.createElement('div');
  private readonly runtime: CampaignRuntime;
  private readonly bridge = new CampaignBattleBridge(candidate01Level);
  private game: GameController | null = null;
  private pendingRequest: BattleRequest | null = null;
  private beat = 0;
  private lastBattle: CampaignBattleSummary | null = null;
  private disposed = false;

  constructor(state: CampaignState | null, private readonly onExit: () => void) {
    this.runtime = new CampaignRuntime(CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN, state ?? undefined);
    this.root.className = 'campaign-root';
    this.root.addEventListener('click', this.onClick);
    this.render();
  }

  dispose(): void {
    this.disposed = true;
    this.game?.dispose();
    this.root.removeEventListener('click', this.onClick);
  }

  private onClick = (event: Event): void => {
    const target = (event.target as HTMLElement).closest<HTMLElement>('[data-campaign-act]');
    if (!target || this.disposed) return;
    const action = target.dataset.campaignAct;
    if (action === 'exit') return this.exit();
    if (action === 'nextBeat') return this.nextBeat();
    if (action === 'choose') return this.choose(target.dataset.choice ?? '');
    if (action === 'battle') return this.startBattle();
    if (action === 'aftermath') {
      this.lastBattle = null;
      return this.render();
    }
    if (action === 'finish') return this.finishCampaign();
  };

  private exit(): void {
    this.game?.dispose();
    this.game = null;
    this.dispose();
    this.onExit();
  }

  private nextBeat(): void {
    const node = this.runtime.node();
    if (!node.presentation || node.type === 'choice' || node.type === 'battle' || node.type === 'ending') return;
    const story = candidate01Story(node.presentation);
    if (this.beat < story.beats.length - 1) {
      this.beat++;
      this.render();
      return;
    }
    this.runtime.advance();
    this.beat = 0;
    this.persist();
    this.render();
  }

  private choose(id: string): void {
    if (!id) return;
    this.runtime.choose(id);
    this.beat = 0;
    this.persist();
    this.render();
  }

  private startBattle(): void {
    const node = this.runtime.node();
    if (node.type !== 'battle') return;
    if (!this.pendingRequest) {
      // Save the clean battle node. Action-level mid-battle saves are outside
      // this campaign shell and never pretend to be resumable.
      this.persist();
      this.pendingRequest = applyCandidate01BattleContext(this.runtime.beginBattle(this.bridge), this.runtime.state);
    }
    const request = this.pendingRequest;
    const game = new GameController(request.level, () => {
      game.dispose();
      this.game = null;
      this.render();
    }, {
      exitLabel: '战役营地',
      completionLabel: '结算战果',
      onComplete: (snapshot) => this.completeBattle(request, snapshot),
    });
    this.game = game;
    this.root.replaceChildren(game.root);
  }

  private completeBattle(request: BattleRequest, snapshot: BattleCompletionSnapshot): void {
    const result = applyCandidate01BattleResultPolicy(
      this.bridge.result(request, snapshot.state, snapshot.events),
      this.runtime.state,
    );
    if (result.outcome !== 'victory') return;
    const level = CANDIDATE_01_LEVELS.find((entry) => entry.id === request.levelId)!;
    this.lastBattle = this.summarizeBattle(level.name, snapshot.state, snapshot.events);
    this.runtime.completeBattle(result);
    this.pendingRequest = null;
    this.game = null;
    this.beat = 0;
    this.persist();
    this.render();
  }

  private summarizeBattle(title: string, state: GameState, events: GameEvent[]): CampaignBattleSummary {
    const human = state.players.find((player) => player.controller === 'human')!;
    const fallen = state.markers
      .filter((marker) => marker.fallenUnit?.owner === human.id)
      .map((marker) => unitDef(marker.fallenUnit!.type).name);
    return {
      title,
      outcome: state.endReason,
      turns: state.turn,
      alliesRemaining: state.units.filter((unit) => unit.owner === human.id).length,
      enemiesRemaining: state.units.filter((unit) => state.players.find((player) => player.id === unit.owner)?.team !== human.team).length,
      fallen,
      signals: events.filter((event) => event.type === 'scenarioSignal').map((event) => event.signal),
      events,
    };
  }

  private finishCampaign(): void {
    if (this.runtime.node().type === 'ending' && this.runtime.state.status === 'active') {
      this.runtime.advance();
      this.persist();
      this.render();
      return;
    }
    this.exit();
  }

  private persist(): void {
    saveCampaignState(CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN, this.runtime.snapshot());
  }

  private render(): void {
    if (this.disposed || this.game) return;
    if (this.lastBattle) return this.renderBattleResult(this.lastBattle);
    const node = this.runtime.node();
    if (node.type === 'battle') return this.renderBattleStaging(node.level);
    if (node.type === 'choice') return this.renderChoice(node.presentation!);
    if (!node.presentation) throw new Error(`campaign node ${node.id} has no presentation`);
    this.renderStory(node.presentation, node.type === 'ending');
  }

  private shell(content: string, chapter: number): void {
    const completed = this.runtime.state.battleHistory.length;
    this.root.innerHTML = `<header class="campaign-topbar">
      <button class="campaign-icon-button" data-campaign-act="exit" aria-label="返回主菜单">‹</button>
      <div><span>断冠之誓</span><strong>第 ${chapter} 章 · ${escapeHtml(chapterTitle(chapter))}</strong></div>
      <div class="campaign-progress" aria-label="战役进度"><i style="--progress:${completed / 16}"></i><b>${completed}/16</b></div>
    </header>${content}`;
  }

  private renderStory(presentationId: string, ending: boolean): void {
    const story = candidate01Story(presentationId);
    const scene = candidate01StoryArt(story.scene);
    const beat = story.beats[Math.min(this.beat, story.beats.length - 1)];
    const portrait = CANDIDATE_01_PORTRAITS[beat.speaker];
    const done = this.runtime.state.status !== 'active';
    const lastBeat = this.beat >= story.beats.length - 1;
    const button = ending
      ? `<button class="campaign-primary" data-campaign-act="finish">${done ? '返回主菜单' : '完成前三章'}</button>`
      : `<button class="campaign-primary" data-campaign-act="nextBeat">${lastBeat ? '继续' : '下一段'} <span>→</span></button>`;
    this.shell(`<main class="story-screen">
      <div class="story-backdrop">
        <img class="story-backdrop-wash" src="${scene}" alt=""/>
        <img class="story-backdrop-art" src="${scene}" alt=""/>
      </div>
      <section class="story-card" style="--beat:${this.beat}">
        <div class="story-meta"><span>${escapeHtml(story.kicker)}</span><span>${escapeHtml(story.date)}</span><span>${escapeHtml(story.location)}</span></div>
        <h1>${escapeHtml(story.title)}</h1>
        <p class="story-summary">${escapeHtml(story.summary)}</p>
        ${story.mission ? `<div class="mission-brief">
          <div><small>胜利目标</small><b>${escapeHtml(story.mission.objective)}</b></div>
          <div><small>战场风险</small><span>${escapeHtml(story.mission.danger)}</span></div>
          <div><small>机制主题</small><span>${escapeHtml(story.mission.lesson)}</span></div>
        </div>` : ''}
        <div class="dialogue-row ${portrait ? '' : 'narration'}">
          ${portrait ? `<img class="dialogue-portrait" src="${portrait}" alt="${escapeHtml(CANDIDATE_01_SPEAKER_NAMES[beat.speaker])}"/>` : ''}
          <div class="dialogue-copy"><small>${escapeHtml(CANDIDATE_01_SPEAKER_NAMES[beat.speaker])}</small><p>${escapeHtml(beat.text)}</p></div>
        </div>
        <div class="story-actions"><div class="beat-dots">${story.beats.map((_, index) => `<i class="${index <= this.beat ? 'active' : ''}"></i>`).join('')}</div>${button}</div>
      </section>
    </main>`, story.chapter);
  }

  private renderChoice(presentationId: string): void {
    const choice = candidate01Choice(presentationId);
    this.shell(`<main class="choice-screen"><section class="choice-card">
      <span class="campaign-eyebrow">需要作出决定</span>
      <h1>${escapeHtml(choice.prompt)}</h1>
      <p>${escapeHtml(choice.context)}</p>
      <div class="choice-grid">${choice.options.map((option, index) => `<button class="choice-option" data-campaign-act="choose" data-choice="${escapeHtml(option.id)}" style="--index:${index}">
        <span class="choice-number">0${index + 1}</span><strong>${escapeHtml(option.label)}</strong>
        <p>${escapeHtml(option.detail)}</p><small>${escapeHtml(option.consequence)}</small>
      </button>`).join('')}</div>
      <p class="choice-note">选择会写入战役存档，并在后续关卡、关系或资源中兑现。</p>
    </section></main>`, choice.chapter);
  }

  private renderBattleStaging(levelId: string): void {
    const level = CANDIDATE_01_LEVELS.find((entry) => entry.id === levelId)!;
    const chapter = Number(level.extra?.chapter);
    const order = levelOrder(levelId);
    const story = candidate01Story(`c01/brief-${String(order).padStart(2, '0')}`);
    const scene = candidate01StoryArt(story.scene);
    const completed = this.runtime.state.battleHistory.length;
    const roster = Object.values(this.runtime.state.roster).filter((unit) => (JOIN_AFTER[unit.id] ?? 0) <= completed);
    this.shell(`<main class="staging-screen">
      <section class="staging-hero"><img src="${scene}" alt=""/><div><span class="campaign-eyebrow">作战准备 · ${String(order).padStart(2, '0')}/16</span><h1>${escapeHtml(level.name)}</h1><p>${escapeHtml(level.description ?? '')}</p></div></section>
      <div class="staging-grid">
        <section class="campaign-panel"><h2>出战名册</h2><div class="roster-list">${roster.map((unit) => {
          const portrait = CANDIDATE_01_PORTRAITS[unit.id as keyof typeof CANDIDATE_01_PORTRAITS];
          return `<div class="campaign-unit">${portrait ? `<img src="${portrait}" alt=""/>` : '<span class="unit-fallback">◆</span>'}<div><b>${escapeHtml(unitDef(unit.unitType).name)}</b><small>${unit.disposition === 'available' ? `生命 ${Math.round(unit.hpRatio * 100)}% · 军衔 ${unit.rank ?? 0}` : escapeHtml(unit.disposition)}</small></div></div>`;
        }).join('')}</div></section>
        <section class="campaign-panel"><h2>战役状态</h2><div class="resource-row"><span>补给</span><b>${this.runtime.state.resources.supplies ?? 0}</b><span>国库</span><b>${this.runtime.state.resources.treasury ?? 0}</b></div>
          <div class="relation-list">${Object.entries(this.runtime.state.relations).filter(([, value]) => value !== 0).slice(0, 5).map(([id, value]) => `<div><span>${escapeHtml(RELATION_LABELS[id] ?? id)}</span><i style="--relation:${Math.max(0, Math.min(1, (value + 3) / 6))}"></i><b>${value > 0 ? '+' : ''}${value}</b></div>`).join('') || '<p>尚未形成明确阵营关系。</p>'}</div>
        </section>
      </div>
      <div class="staging-actions"><span>${this.pendingRequest ? '离开战场后将从本关开局重新开始。' : '进入战斗后自动保留关前存档。'}</span><button class="campaign-primary" data-campaign-act="battle">${this.pendingRequest ? '重新进入战斗' : '开始战斗'} →</button></div>
    </main>`, chapter);
  }

  private renderBattleResult(result: CampaignBattleSummary): void {
    const completed = this.runtime.state.battleHistory.length;
    const chapter = completed <= 5 ? 1 : completed <= 10 ? 2 : 3;
    const attackEvents = result.events.filter((event) => event.type === 'attack' || event.type === 'areaAttack' || event.type === 'counter').length;
    this.shell(`<main class="result-screen"><section class="result-card">
      <span class="campaign-eyebrow">战斗胜利</span><h1>${escapeHtml(result.title)}</h1><p>${escapeHtml(result.outcome)}</p>
      <div class="result-metrics"><div><b>${result.turns}</b><span>回合</span></div><div><b>${result.alliesRemaining}</b><span>我方存续</span></div><div><b>${result.enemiesRemaining}</b><span>敌方存续</span></div><div><b>${attackEvents}</b><span>交战次数</span></div></div>
      <div class="result-detail"><div><small>本关倒下</small><p>${result.fallen.length ? escapeHtml(result.fallen.join('、')) : '无人倒下'}</p></div><div><small>关键战场记录</small><p>${result.signals.length ? escapeHtml(result.signals.join(' · ')) : '目标按计划完成'}</p></div></div>
      <button class="campaign-primary" data-campaign-act="aftermath">查看战后剧情 →</button>
    </section></main>`, chapter);
  }
}
