import { saveCampaignState } from '../application/campaign-storage';
import {
  CampaignBattleBridge,
  CampaignRuntime,
  type BattleRequest,
  type CampaignState,
  type BattleResult,
  type CampaignDefinition,
} from '@empire/campaign-engine';
import {
  scenarioSignalsOf,
  strikeCount,
  type GameEvent,
  type GameState,
  type ContentCatalog,
  type LevelData,
  player,
} from '@empire/battle-engine';
import {
  GameController,
  type BattleCompletionSnapshot,
  type GameControllerOptions,
} from './game';
import { escapeHtml } from './html';
import { portraitSvg } from '../art/portraits';
import { PAL } from '../art/palette';

interface CampaignBattleSummary {
  title: string;
  chapter: number;
  outcome: string;
  turns: number;
  alliesRemaining: number;
  enemiesRemaining: number;
  fallen: string[];
  signals: string[];
  events: GameEvent[];
}

/** What the shell is showing: prose to page, a decision, or a battle to stage. */
type CampaignScreen =
  | { readonly kind: 'story'; readonly presentation: string; readonly ending: boolean }
  | { readonly kind: 'choice'; readonly presentation: string }
  | { readonly kind: 'battle'; readonly level: string };

export interface StoryBeatView {
  speaker: string;
  text: string;
}

export interface StoryPresentationView {
  scene: string;
  chapter: number;
  kicker: string;
  title: string;
  date: string;
  location: string;
  summary: string;
  beats: readonly StoryBeatView[];
  mission?: { objective: string; danger: string; lesson: string } | undefined;
}

export interface StoryChoiceView {
  chapter: number;
  prompt: string;
  context: string;
  options: readonly { id: string; label: string; detail: string; consequence: string }[];
}

/** Story-facing port. The campaign shell owns flow, never story IDs or prose. */
export interface StoryCampaignAdapter {
  title: string;
  definition: CampaignDefinition;
  completionLabel: string;
  portraits: Readonly<Record<string, string>>;
  joinAfter?: Readonly<Record<string, number>>;
  relationLabels?: Readonly<Record<string, string>>;
  resourceLabels?: Readonly<Record<string, string>>;
  chapterTitle(chapter: number): string;
  /**
   * The place a chapter happens, for every screen in it that has no scene of
   * its own.
   *
   * A story beat names its own scene; a choice, a staging brief and a result do
   * not, and they used to be a card on a flat gradient — so the campaign
   * alternated between a painted place and a bare dialog about it. Every screen
   * knows its chapter, because `shell` is handed one.
   */
  chapterArt(chapter: number): string;
  chapterOf(level: LevelData): number;
  levelOrder(level: LevelData): number;
  briefingId(level: LevelData): string;
  storyArt(topicId: string): string;
  speakerNames: Readonly<Record<string, string>>;
  level(id: string): LevelData;
  story(id: string): StoryPresentationView;
  choice(id: string): StoryChoiceView;
  applyBattleContext(request: BattleRequest, state: CampaignState): BattleRequest;
  applyBattleResultPolicy(result: BattleResult, state: CampaignState): BattleResult;
}

/** Battle presentation selected by the application root for this campaign. */
export type StoryCampaignControllerOptions =
  Pick<GameControllerOptions, 'engine' | 'renderer' | 'eventPresenters'> &
  Required<Pick<GameControllerOptions, 'art'>>;

export class StoryCampaignController {
  readonly root = document.createElement('div');
  /** Ruleset of the campaign; every roster label resolves through it. */
  private readonly content: ContentCatalog;
  private readonly runtime: CampaignRuntime;
  private readonly bridge: CampaignBattleBridge;
  private readonly battleTotal: number;
  private game: GameController | null = null;
  private pendingRequest: BattleRequest | null = null;
  private beat = 0;
  private lastBattle: CampaignBattleSummary | null = null;
  private disposed = false;

  constructor(
    private readonly adapter: StoryCampaignAdapter,
    state: CampaignState | null,
    private readonly onExit: () => void,
    private readonly options: StoryCampaignControllerOptions,
  ) {
    this.content = options.engine.content;
    this.bridge = new CampaignBattleBridge(adapter.level, this.content);
    this.runtime = new CampaignRuntime(adapter.definition, state ?? undefined);
    this.battleTotal = adapter.definition.nodes.filter((node) => node.type === 'battle').length;
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
    const screen = this.screen();
    if (screen.kind !== 'story' || screen.ending) return;
    const story = this.adapter.story(screen.presentation);
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
    if (this.screen().kind !== 'battle') return;
    if (!this.pendingRequest) {
      // Save the clean battle node. Action-level mid-battle saves are outside
      // this campaign shell and never pretend to be resumable.
      this.persist();
      this.pendingRequest = this.adapter.applyBattleContext(this.runtime.beginBattle(this.bridge), this.runtime.state);
    }
    const request = this.pendingRequest;
    const game = new GameController(request.level, () => {
      game.dispose();
      this.game = null;
      this.render();
    }, {
      engine: this.options.engine,
      art: this.options.art,
      renderer: this.options.renderer,
      eventPresenters: this.options.eventPresenters,
      exitLabel: '战役营地',
      completionLabel: '结算战果',
      onComplete: (snapshot) => this.completeBattle(request, snapshot),
    });
    this.game = game;
    this.root.replaceChildren(game.root);
  }

  private completeBattle(request: BattleRequest, snapshot: BattleCompletionSnapshot): void {
    const result = this.adapter.applyBattleResultPolicy(
      this.bridge.result(request, snapshot.state, snapshot.events),
      this.runtime.state,
    );
    if (result.outcome !== 'victory') return;
    this.lastBattle = this.summarizeBattle(request, snapshot.state, snapshot.events);
    this.runtime.completeBattle(result);
    this.pendingRequest = null;
    this.game = null;
    this.beat = 0;
    this.persist();
    this.render();
  }

  /**
   * Whose battle it was is the campaign's fact, carried on the request.
   *
   * This used to look for the side with a human controller and assert that it
   * found one — which is a different question with a coincidentally equal
   * answer, and no answer at all for a battle watched rather than played.
   */
  private summarizeBattle(request: BattleRequest, state: GameState, events: GameEvent[]): CampaignBattleSummary {
    const level = this.adapter.level(request.levelId);
    const ours = player(state, request.perspectivePlayer);
    const fallen = state.markers.flatMap((marker) => marker.fallenUnit?.owner === ours.id
      ? [this.content.units.get(marker.fallenUnit.type).name]
      : []);
    return {
      title: level.name,
      chapter: this.adapter.chapterOf(level),
      outcome: state.endReason,
      turns: state.turn,
      alliesRemaining: state.units.filter((unit) => unit.owner === ours.id).length,
      enemiesRemaining: state.units.filter((unit) => state.players.find((side) => side.id === unit.owner)?.team !== ours.team).length,
      fallen,
      signals: scenarioSignalsOf(events),
      events,
    };
  }

  private finishCampaign(): void {
    const screen = this.screen();
    if (screen.kind === 'story' && screen.ending && this.runtime.state.status === 'active') {
      this.runtime.advance();
      this.persist();
      this.render();
      return;
    }
    this.exit();
  }

  private persist(): void {
    saveCampaignState(this.adapter.definition, this.runtime.snapshot());
  }

  /**
   * Which screen the campaign is on, and the one place that reads a node's kind.
   *
   * Four methods used to ask the node what kind it was — to page prose, to stage
   * a battle, to finish, and to render — so the shell held four copies of a
   * vocabulary the engine deliberately left open, and two of them guessed that a
   * node with prose to show had prose to show.
   */
  private screen(): CampaignScreen {
    const node = this.runtime.node();
    if (node.type === 'battle') return { kind: 'battle', level: node.level };
    if (node.type === 'choice') return { kind: 'choice', presentation: node.presentation };
    if (node.type === 'ending') return { kind: 'story', presentation: node.presentation, ending: true };
    return { kind: 'story', presentation: node.presentation, ending: false };
  }

  private render(): void {
    if (this.disposed || this.game) return;
    if (this.lastBattle) return this.renderBattleResult(this.lastBattle);
    const screen = this.screen();
    if (screen.kind === 'battle') return this.renderBattleStaging(screen.level);
    if (screen.kind === 'choice') return this.renderChoice(screen.presentation);
    this.renderStory(screen.presentation, screen.ending);
  }

  /**
   * Every campaign screen: the bar, the place it happens in, and the card.
   *
   * The backdrop is here rather than in each screen because *every* screen is
   * somewhere. It used to be drawn by the story screen alone, from the beat's own
   * scene, and the choice, the staging brief and the result were a card on a flat
   * gradient — so the campaign alternated between a painted place and a bare
   * dialog about it. A screen with a more specific scene passes it; the rest get
   * their chapter's.
   */
  private shell(content: string, chapter: number, scene = this.adapter.chapterArt(chapter)): void {
    const completed = this.runtime.state.battleHistory.length;
    const progress = this.battleTotal === 0 ? 0 : completed / this.battleTotal;
    this.root.innerHTML = `<header class="campaign-topbar">
      <button class="campaign-icon-button" data-campaign-act="exit" aria-label="返回主菜单">‹</button>
      <div><span>${escapeHtml(this.adapter.title)}</span><strong>第 ${chapter} 章 · ${escapeHtml(this.adapter.chapterTitle(chapter))}</strong></div>
      <div class="campaign-progress" aria-label="战役进度"><i style="--progress:${progress}"></i><b>${completed}/${this.battleTotal}</b></div>
    </header>
    <div class="campaign-backdrop">
      <img class="campaign-backdrop-wash" src="${scene}" alt=""/>
      <img class="campaign-backdrop-art" src="${scene}" alt=""/>
    </div>${content}`;
  }

  private renderStory(presentationId: string, ending: boolean): void {
    const story = this.adapter.story(presentationId);
    const scene = this.adapter.storyArt(story.scene);
    const beat = story.beats[Math.min(this.beat, story.beats.length - 1)];
    const portrait = this.adapter.portraits[beat.speaker];
    const done = this.runtime.state.status !== 'active';
    const lastBeat = this.beat >= story.beats.length - 1;
    const button = ending
      ? `<button class="campaign-primary" data-campaign-act="finish">${done ? '返回主菜单' : escapeHtml(this.adapter.completionLabel)}</button>`
      : `<button class="campaign-primary" data-campaign-act="nextBeat">${lastBeat ? '继续' : '下一段'} <span>→</span></button>`;
    this.shell(`<main class="story-screen">
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
          ${portrait ? `<img class="dialogue-portrait" src="${portrait}" alt="${escapeHtml(this.adapter.speakerNames[beat.speaker] ?? beat.speaker)}"/>` : ''}
          <div class="dialogue-copy"><small>${escapeHtml(this.adapter.speakerNames[beat.speaker] ?? beat.speaker)}</small><p>${escapeHtml(beat.text)}</p></div>
        </div>
        <div class="story-actions"><div class="beat-dots">${story.beats.map((_, index) => `<i class="${index <= this.beat ? 'active' : ''}"></i>`).join('')}</div>${button}</div>
      </section>
    </main>`, story.chapter, scene);
  }

  private renderChoice(presentationId: string): void {
    const choice = this.adapter.choice(presentationId);
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
    const level = this.adapter.level(levelId);
    const chapter = this.adapter.chapterOf(level);
    const order = this.adapter.levelOrder(level);
    const story = this.adapter.story(this.adapter.briefingId(level));
    const scene = this.adapter.storyArt(story.scene);
    const completed = this.runtime.state.battleHistory.length;
    const roster = Object.values(this.runtime.state.roster).filter((unit) => (this.adapter.joinAfter?.[unit.id] ?? 0) <= completed);
    const resources = Object.entries(this.runtime.state.resources)
      .map(([id, amount]) => `<span>${escapeHtml(this.adapter.resourceLabels?.[id] ?? id)}</span><b>${amount}</b>`)
      .join('') || '<span>暂无战役资源</span>';
    this.shell(`<main class="staging-screen">
      <section class="staging-hero"><img src="${scene}" alt=""/><div><span class="campaign-eyebrow">作战准备 · ${String(order).padStart(2, '0')}/${this.battleTotal}</span><h1>${escapeHtml(level.name)}</h1><p>${escapeHtml(level.description ?? '')}</p></div></section>
      <div class="staging-grid">
        <section class="campaign-panel"><h2>出战名册</h2><div class="roster-list">${roster.map((unit) => {
          /*
           * A named character has an authored portrait; everybody else has a
           * sprite, and the roster used to show them a lozenge. `◆` for two of
           * every three names on the muster is a placeholder standing in for art
           * this pack ships — the same picture the HUD draws them with.
           */
          const definition = this.content.units.get(unit.unitType);
          const portrait = this.adapter.portraits[unit.id];
          const avatar = portrait
            ? `<img src="${portrait}" alt=""/>`
            : `<span class="campaign-unit-plate">${portraitSvg(this.options.art, definition, PAL.neutral, 40)}</span>`;
          return `<div class="campaign-unit">${avatar}<div><b>${escapeHtml(definition.name)}</b><small>${unit.disposition === 'available' ? `生命 ${Math.round(unit.hpRatio * 100)}% · 军衔 ${unit.rank ?? 0}` : escapeHtml(unit.disposition)}</small></div></div>`;
        }).join('')}</div></section>
        <section class="campaign-panel"><h2>战役状态</h2><div class="resource-row">${resources}</div>
          <div class="relation-list">${Object.entries(this.runtime.state.relations).filter(([, value]) => value !== 0).slice(0, 5).map(([id, value]) => `<div><span>${escapeHtml(this.adapter.relationLabels?.[id] ?? id)}</span><i style="--relation:${Math.max(0, Math.min(1, (value + 3) / 6))}"></i><b>${value > 0 ? '+' : ''}${value}</b></div>`).join('') || '<p>尚未形成明确阵营关系。</p>'}</div>
        </section>
      </div>
      <div class="staging-actions"><span>${this.pendingRequest ? '离开战场后将从本关开局重新开始。' : '进入战斗后自动保留关前存档。'}</span><button class="campaign-primary" data-campaign-act="battle">${this.pendingRequest ? '重新进入战斗' : '开始战斗'} →</button></div>
    </main>`, chapter);
  }

  private renderBattleResult(result: CampaignBattleSummary): void {
    const attackEvents = strikeCount(result.events);
    this.shell(`<main class="result-screen"><section class="result-card">
      <span class="campaign-eyebrow">战斗胜利</span><h1>${escapeHtml(result.title)}</h1><p>${escapeHtml(result.outcome)}</p>
      <div class="result-metrics"><div><b>${result.turns}</b><span>回合</span></div><div><b>${result.alliesRemaining}</b><span>我方存续</span></div><div><b>${result.enemiesRemaining}</b><span>敌方存续</span></div><div><b>${attackEvents}</b><span>交战次数</span></div></div>
      <div class="result-detail"><div><small>本关倒下</small><p>${result.fallen.length ? escapeHtml(result.fallen.join('、')) : '无人倒下'}</p></div><div><small>关键战场记录</small><p>${result.signals.length ? escapeHtml(result.signals.join(' · ')) : '目标按计划完成'}</p></div></div>
      <button class="campaign-primary" data-campaign-act="aftermath">查看战后剧情 →</button>
    </section></main>`, result.chapter);
  }
}
