import { loadCustomLevels, saveCustomLevel, stashPlaytest } from '@empire/game-ui/application/level-storage';
import type { ContentCatalog } from '@empire/battle-engine/content-pack';
import type { BattleRuleServices } from '@empire/battle-engine/action-system';
import { TEAM_COLORS } from '@empire/game-ui/art/palette';
import { ANCIENT_EMPIRES_LEVELS as BUILTIN_LEVELS } from '@empire/content-ancient-empires/levels';
import { validateLevel } from '@empire/battle-engine/level-validation';
import { DEFAULT_RULES } from '@empire/battle-engine/types';
import type { DirectionDef } from '@empire/battle-engine/tactical-grid';
import {
  emptyLevel,
  mapFromLevel,
  normaliseLevel,
  type LevelIssue,
} from '@empire/battle-engine/level';
import type {
  Coord,
  CoverLevel,
  Direction,
  LevelData,
  Objective,
  PlayerConfig,
  RuleSet,
} from '@empire/battle-engine/types';
import { COMMAND_POINTS_RESOURCE, FUNDS_RESOURCE } from '@empire/battle-engine/resources';
import { EditorBoard } from './board';
import { EditorDocument } from './document';
import { EditorHistory } from './history';
import { EditorPanels, type EditorPanelView } from './panels';
import { BrushSettings, EDITOR_TOOLS, type EditorTool, type EditorToolContext } from './tools';

const DRAFT_KEY = 'empire.editorDraft';

/**
 * Writing one player field. Pure: a player's own settings need nothing but the
 * player, so these are stated as a table rather than as a run of `if (key ===)`.
 */
type PlayerFieldWriter = (player: PlayerConfig, value: string | boolean) => void;

const PLAYER_FIELDS: Record<string, PlayerFieldWriter> = {
  name: (player, value) => { player.name = String(value); },
  color: (player, value) => { player.color = String(value); },
  team: (player, value) => { player.team = Math.max(1, Number(value) || 1); },
  funds: (player, value) => {
    player.resources[FUNDS_RESOURCE] = {
      current: Math.max(0, Number(value) || 0),
      capacity: player.resources[FUNDS_RESOURCE]?.capacity ?? null,
    };
  },
  controller: (player, value) => { player.controller = value === 'ai' ? 'ai' : 'human'; },
  aggression: (player, value) => {
    player.ai = { aggression: Math.max(0, Math.min(1, Number(value) || 0)) };
  },
};

/**
 * Rule inputs that do not map one-to-one onto a `RuleSet` field: the editor
 * offers "funds per turn" as a number, while the ruleset stores a list of
 * resource grants. Everything else is written by name.
 */
const COMPOUND_RULE_FIELDS: Record<string, (rules: Partial<RuleSet>, value: string | boolean) => void> = {
  baseFundsGrant: (rules, value) => {
    const amount = Math.max(0, Number(value) || 0);
    const others = (rules.baseResourceGrants ?? []).filter((grant) => grant.resource !== FUNDS_RESOURCE);
    rules.baseResourceGrants = amount > 0 ? [...others, { resource: FUNDS_RESOURCE, amount }] : others;
  },
  siteFundsOverride: (rules, value) => {
    const overrides = { ...(rules.siteResourceOverrides ?? {}) };
    if (value === '') delete overrides[FUNDS_RESOURCE];
    else overrides[FUNDS_RESOURCE] = Math.max(0, Number(value) || 0);
    rules.siteResourceOverrides = overrides;
  },
};

/** Rule fields whose value is a string rather than a number or a flag. */
const NAMED_RULE_FIELDS: ReadonlySet<string> = new Set(['captureMode', 'turnOrder']);

const numberOrNull = (value: string | boolean) => (value === '' ? null : Number(value));

export class EditorApp {
  private doc: EditorDocument;
  private board: EditorBoard;
  private readonly panels = new EditorPanels();
  private readonly history = new EditorHistory();

  private tool: EditorTool = EDITOR_TOOLS.default;
  private readonly brush = new BrushSettings('plain', 'soldier');
  private strokeAnchor: Coord | null = null;
  private cursor: Coord | null = null;
  private showCoords = false;
  private showOwners = true;
  private strokeOpen = false;
  private status = '';

  private readonly root = document.createElement('div');
  private readonly scroller = document.createElement('div');

  private readonly content: ContentCatalog;

  constructor(
    /**
     * The ruleset this level is being written for.
     *
     * It used to be the content catalog alone, which is why the editor could
     * tell you that a unit type did not exist but not that an objective kind,
     * a standing order or a turn-order policy was one nobody had registered:
     * those live in the composed rules, and the editor could not see them. A
     * level is authored *against a ruleset*; that is the dependency.
     */
    private readonly rules: BattleRuleServices,
    level: LevelData,
  ) {
    const content = rules.content;
    this.content = content;
    this.doc = EditorDocument.fromLevel(content, level);
    this.ensureOwnerSelection();
    this.ensureCoverSide();
    this.root.className = 'editor-root';
    this.scroller.className = 'board-scroll';

    this.board = new EditorBoard(this.doc.map, this.doc.units, this.doc.players, {
      onStroke: (at, phase, button) => this.onStroke(at, phase, button),
      onHover: (at) => {
        this.cursor = at;
        this.paintBoard();
      },
    }, content);
    this.scroller.append(this.board.el);

    const stage = document.createElement('div');
    stage.className = 'stage';
    stage.append(this.panels.paletteEl, this.scroller, this.panels.propertiesEl);
    this.root.append(this.panels.topEl, stage);

    this.bindDelegates();
    document.addEventListener('keydown', (event) => this.onKey(event));
    this.board.el.addEventListener(
      'wheel',
      (event) => {
        if (!event.ctrlKey && !event.metaKey) return;
        event.preventDefault();
        this.board.setZoom(this.board.zoomLevel - Math.sign(event.deltaY) * 0.1);
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
    this.history.record(this.doc.serialize());
  }

  /** Public so the editor host (and tests) can drive history. */
  undo(): void {
    this.restore(this.history.undo(this.doc.serialize()));
  }

  redo(): void {
    this.restore(this.history.redo(this.doc.serialize()));
  }

  private restore(snapshot: string | null): void {
    if (snapshot === null) return;
    this.replaceDocument(EditorDocument.deserialize(this.content, snapshot));
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
    this.ensureCoverSide();
    this.board.resize(document.map);
  }

  private ensureOwnerSelection(): void {
    if (!this.doc.players.some((player) => player.id === this.brush.owner)) {
      this.brush.owner = this.doc.players[0]?.id ?? 0;
    }
  }

  /**
   * The cover brush starts on a facing the board actually has.
   *
   * `north` is the square board's first facing, not every board's, and a brush
   * pointing at a name the tiling does not know paints cover that protects
   * against nothing.
   */
  private ensureCoverSide(): void {
    const facings = this.facings;
    if (facings.length > 0 && !facings.some((facing) => facing.id === this.brush.coverSide)) {
      this.brush.coverSide = facings[0].id;
    }
  }

  /* ------------------------------------------------------------------ tools */

  /** What a tool is handed: the document it edits and the palette it paints with. */
  private toolContext(): EditorToolContext {
    return { document: this.doc, brush: this.brush, content: this.content };
  }

  private brushTiles(): Coord[] {
    if (!this.cursor) return [];
    return this.tool.highlight(this.toolContext(), this.cursor, this.strokeAnchor);
  }

  /**
   * One stroke protocol for every tool: press opens an undo step, drag paints,
   * release commits. Two-phase tools keep the press point and act on release.
   */
  private onStroke(at: Coord, phase: 'start' | 'move' | 'end', button: number): void {
    const erasing = button === 2;
    if (phase === 'start') {
      this.snapshot();
      this.strokeOpen = true;
      if (this.tool.twoPhase) this.strokeAnchor = at;
    }
    this.cursor = at;

    if (this.tool.twoPhase) {
      if (phase !== 'end') {
        this.paintBoard();
        return;
      }
      if (this.strokeAnchor) this.tool.commit?.(this.toolContext(), this.strokeAnchor, at, erasing);
      this.strokeAnchor = null;
      this.finishStroke();
      return;
    }

    if (phase === 'end') {
      this.finishStroke();
      return;
    }
    this.tool.paint?.(this.toolContext(), at, erasing);
    this.paintBoard();
    this.renderProperties();
    // A sampling tool changes the palette rather than the map.
    if (this.tool.samples) this.renderPalette();
  }

  private finishStroke(): void {
    if (!this.strokeOpen) return;
    this.strokeOpen = false;
    this.autosave();
    this.renderAll();
  }

  private selectTool(tool: EditorTool): void {
    this.tool = tool;
    this.strokeAnchor = null;
  }

  resize(width: number, height: number): void {
    const clampedWidth = Math.max(4, Math.min(64, Math.round(width)));
    const clampedHeight = Math.max(4, Math.min(64, Math.round(height)));
    if (clampedWidth === this.doc.map.width && clampedHeight === this.doc.map.height) return;
    this.snapshot();
    this.doc.resize(clampedWidth, clampedHeight);
    this.board.resize(this.doc.map);
    this.autosave();
    this.renderAll();
  }

  /* ---------------------------------------------------------------- keyboard */

  private onKey(event: KeyboardEvent): void {
    const target = event.target as HTMLElement;
    if (target && /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName)) return;

    if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'z') {
      event.preventDefault();
      if (event.shiftKey) this.redo();
      else this.undo();
      return;
    }
    if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 's') {
      event.preventDefault();
      this.save();
      return;
    }
    // Each tool declares its own hotkey, so the palette tooltip and the key
    // handler cannot disagree about what a key does.
    const pressed = EDITOR_TOOLS.forHotkey(event.key);
    if (pressed) {
      this.selectTool(pressed);
      this.renderAll();
      return;
    }
    if (event.key === 'Escape') {
      this.strokeAnchor = null;
      this.paintBoard();
    }
    const slot = Number(event.key);
    if (slot >= 1 && slot <= 9) {
      const ids = this.content.terrains.ids();
      if (ids[slot - 1]) {
        this.brush.terrain = ids[slot - 1];
        this.selectTool(EDITOR_TOOLS.default);
        this.renderAll();
      }
    }
  }

  /* ------------------------------------------------------------------- io */

  /** Serialise the document to the on-disk level format. */
  exportLevel(): LevelData {
    return this.doc.toLevel();
  }

  private errorsInDocument(): LevelIssue[] {
    return this.lint().filter((issue) => issue.severity === 'error');
  }

  private lint(): LevelIssue[] {
    return validateLevel(this.rules, this.exportLevel());
  }

  private save(): void {
    const errors = this.errorsInDocument();
    try {
      saveCustomLevel(this.exportLevel());
    } catch (error) {
      // A refused save is reported, never silent: the alternative is telling the
      // author their level is saved when the slot could not be written.
      this.status = `保存失败：${(error as Error).message}`;
      this.renderAll();
      return;
    }
    this.autosave();
    this.status = errors.length
      ? `已保存（仍有 ${errors.length} 个错误，游戏中可能无法开始）`
      : '已保存到「我的关卡」';
    this.renderAll();
  }

  private exportFile(): void {
    const level = this.exportLevel();
    const blob = new Blob([JSON.stringify(level, null, 2)], { type: 'application/json' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = `${level.id || 'level'}.json`;
    link.click();
    URL.revokeObjectURL(link.href);
    this.status = '已导出 JSON';
    this.renderTop();
  }

  private async copyJson(): Promise<void> {
    const json = JSON.stringify(this.exportLevel(), null, 2);
    try {
      await navigator.clipboard.writeText(json);
      this.status = 'JSON 已复制到剪贴板';
    } catch (error) {
      // The reason comes from the error, not from a guess about it: this used to
      // report a refused clipboard permission for anything at all, including a
      // document that failed to serialise.
      this.status = `复制失败：${(error as Error).message}`;
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
        mapFromLevel(this.content, level); // fail fast on a broken terrain grid
        this.loadLevel(level, `已载入 ${level.name}`);
      } catch (error) {
        this.status = `载入失败：${(error as Error).message}`;
        this.renderTop();
      }
    };
    input.click();
  }

  private playtest(): void {
    const level = this.exportLevel();
    const errors = this.errorsInDocument();
    if (errors.length) {
      this.status = `无法试玩：${errors[0].message}`;
      this.renderAll();
      return;
    }
    stashPlaytest(level);
    location.href = './index.html';
  }

  private loadLevel(level: LevelData, status: string): void {
    this.snapshot();
    this.replaceDocument(EditorDocument.fromLevel(this.content, level));
    this.status = status;
    this.renderAll();
  }

  /* --------------------------------------------------------------- rendering */

  /**
   * Facings the board being authored admits.
   *
   * Empty when the level names a tiling this ruleset does not implement, which
   * the lint reports as its own finding rather than this panel guessing one.
   */
  private get facings(): readonly DirectionDef[] {
    return this.rules.grids.tryGet(this.doc.rules.grid ?? DEFAULT_RULES.grid)?.directions ?? [];
  }

  private panelView(issues: readonly LevelIssue[] = []): EditorPanelView {
    return {
      document: this.doc,
      content: this.content,
      tool: this.tool,
      brush: this.brush,
      status: this.status,
      showCoords: this.showCoords,
      showOwners: this.showOwners,
      canUndo: this.history.canUndo,
      canRedo: this.history.canRedo,
      issues,
      facings: this.facings,
      presets: [
        ...BUILTIN_LEVELS.map((level) => ({ value: `b:${level.id}`, label: `内置 · ${level.name}` })),
        ...loadCustomLevels().map((saved) => ({
          value: `c:${saved.level.id}`,
          label: `我的 · ${saved.level.name}`,
        })),
      ],
    };
  }

  private renderAll(): void {
    this.renderTop();
    this.renderPalette();
    this.renderProperties();
    this.paintBoard();
  }

  private renderTop(): void {
    this.panels.renderTop(this.panelView());
  }

  private renderPalette(): void {
    this.panels.renderPalette(this.panelView());
  }

  /** The inspector is the only panel that shows lint, so it pays for it. */
  private renderProperties(): void {
    this.panels.renderProperties(this.panelView(this.lint()));
  }

  private paintBoard(): void {
    this.board.render(this.doc.map, this.doc.units, this.doc.players, {
      cursor: this.cursor,
      brush: this.brushTiles(),
      showCoords: this.showCoords,
      showOwners: this.showOwners,
    });
  }

  /* -------------------------------------------------------------- commands */

  /**
   * Every intent a rendered control can declare, and what it does.
   *
   * This was a hundred-line `switch` inside the click listener, so the fact that
   * a button existed and the fact that anything handled it lived in different
   * halves of the file, and nothing checked they agreed — a typo in `data-act`
   * produced a silently dead button. A fitness test now walks the rendered
   * markup and requires every declared intent to appear here.
   */
  private readonly commands: Record<string, (arg: string) => void> = {
    tool: (arg) => {
      this.selectTool(EDITOR_TOOLS.get(arg) ?? this.tool);
      this.renderAll();
    },
    brush: (arg) => {
      this.brush.size = Number(arg);
      this.renderTop();
      this.paintBoard();
    },
    terrain: (arg) => {
      this.brush.terrain = arg;
      // Picking a terrain means you want to paint it; the shape tools already do.
      if (this.tool.id !== 'rect' && this.tool.id !== 'fill') this.selectTool(EDITOR_TOOLS.default);
      this.renderAll();
    },
    'cover-side': (arg) => {
      this.brush.coverSide = arg as Direction;
      this.renderPalette();
    },
    'cover-level': (arg) => {
      this.brush.coverLevel = arg as Exclude<CoverLevel, 'none'>;
      this.renderPalette();
    },
    unit: (arg) => {
      this.brush.unitType = arg;
      this.selectTool(EDITOR_TOOLS.get('unit') ?? this.tool);
      this.renderAll();
    },
    owner: (arg) => {
      this.brush.owner = Number(arg);
      this.renderPalette();
      this.renderTop();
    },
    undo: () => this.undo(),
    redo: () => this.redo(),
    zoom: (arg) => this.board.setZoom(this.board.zoomLevel + Number(arg)),
    save: () => this.save(),
    playtest: () => this.playtest(),
    export: () => this.exportFile(),
    copy: () => void this.copyJson(),
    import: () => this.importFile(),
    clear: () => {
      if (!confirm('清空当前地图？')) return;
      this.loadLevel(emptyLevel(this.doc.map.width, this.doc.map.height), this.status);
    },
    addPlayer: () => {
      this.snapshot();
      this.doc.players.push(this.nextPlayer());
      this.renderAll();
    },
    delPlayer: (arg) => {
      this.snapshot();
      this.removePlayer(Number(arg));
      this.renderAll();
    },
    vObj: (arg) => {
      this.snapshot();
      this.doc.victory = toggleObjective(this.doc.victory, arg as Objective['type'], this.doc.rules);
      this.renderProperties();
    },
    pObj: (arg) => {
      this.snapshot();
      const [owner, type] = arg.split(':');
      this.togglePlayerObjective(Number(owner), type as Objective['type']);
      this.renderProperties();
    },
  };

  private nextPlayer(): PlayerConfig {
    const id = Math.max(0, ...this.doc.players.map((player) => player.id)) + 1;
    return {
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
    };
  }

  /** Removing a side also removes what belonged to it; a level cannot half-forget one. */
  private removePlayer(id: number): void {
    this.doc.removePlayer(id);
    if (this.brush.owner === id) this.brush.owner = this.doc.players[0]?.id ?? 0;
  }

  private togglePlayerObjective(owner: number, type: Objective['type']): void {
    const player = this.doc.players.find((candidate) => candidate.id === owner);
    if (!player) return;
    player.objectives = toggleObjective(player.objectives ?? [], type, this.doc.rules);
    // An empty list means "use the level's shared victory conditions".
    if (player.objectives.length === 0) delete player.objectives;
  }

  /* ----------------------------------------------------------------- fields */

  /**
   * Every value a rendered input can write, and where it goes.
   *
   * `p.` and `r.` are namespaces, not string prefixes to be sliced apart at the
   * top of a hundred-line function: one addresses a player, the other the
   * ruleset, and each has its own table.
   */
  private readonly documentFields: Record<string, (value: string | boolean) => void> = {
    name: (value) => this.writeAndRedrawTop(() => { this.doc.name = String(value); }),
    id: (value) => this.writeAndRedrawTop(() => {
      this.doc.id = String(value).trim().replace(/\s+/g, '-') || 'untitled';
    }),
    description: (value) => this.writeAndRedrawTop(() => { this.doc.description = String(value); }),
    author: (value) => this.writeAndRedrawTop(() => { this.doc.author = String(value); }),
    elevation: (value) => {
      this.brush.elevation = Math.max(-9, Math.min(20, Math.round(Number(value) || 0)));
      this.renderPalette();
    },
    width: (value) => this.resize(Number(value), this.doc.map.height),
    height: (value) => this.resize(this.doc.map.width, Number(value)),
    showCoords: (value) => {
      this.showCoords = Boolean(value);
      this.paintBoard();
    },
    showOwners: (value) => {
      this.showOwners = Boolean(value);
      this.paintBoard();
    },
    loadPreset: (value) => {
      const chosen = String(value);
      if (!chosen) return;
      const level = this.presetLevel(chosen);
      if (level) this.loadLevel(level, `已载入 ${level.name}`);
    },
  };

  private writeAndRedrawTop(write: () => void): void {
    write();
    this.autosave();
    this.renderTop();
  }

  private presetLevel(chosen: string): LevelData | undefined {
    const [kind, levelId] = [chosen.slice(0, 1), chosen.slice(2)];
    return kind === 'b'
      ? BUILTIN_LEVELS.find((level) => level.id === levelId)
      : loadCustomLevels().find((saved) => saved.level.id === levelId)?.level;
  }

  private applyField(field: string, value: string | boolean, id?: string): void {
    if (field.startsWith('p.')) {
      this.snapshot();
      const player = this.doc.players.find((candidate) => candidate.id === Number(id));
      if (!player) return;
      PLAYER_FIELDS[field.slice(2)]?.(player, value);
      this.renderAll();
      return;
    }

    if (field.startsWith('r.')) {
      this.snapshot();
      this.applyRuleField(field.slice(2), value);
      this.renderProperties();
      this.autosave();
      return;
    }

    this.documentFields[field]?.(value);
  }

  private applyRuleField(field: string, value: string | boolean): void {
    const compound = COMPOUND_RULE_FIELDS[field];
    if (compound) {
      compound(this.doc.rules, value);
      return;
    }
    const rules = this.doc.rules as Record<string, unknown>;
    if (typeof value === 'boolean') rules[field] = value;
    else if (NAMED_RULE_FIELDS.has(field)) rules[field] = value;
    else rules[field] = numberOrNull(value);
  }

  /* ------------------------------------------------------------- delegation */

  private bindDelegates(): void {
    const onClick = (event: Event) => {
      const element = (event.target as HTMLElement).closest('[data-act]') as HTMLElement | null;
      if (!element) return;
      this.commands[element.dataset.act ?? '']?.(element.dataset.arg ?? '');
      this.autosave();
    };

    const onChange = (event: Event) => {
      const element = event.target as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;
      const field = element.dataset.field;
      if (!field) return;
      const value = element instanceof HTMLInputElement && element.type === 'checkbox'
        ? element.checked
        : element.value;
      this.applyField(field, value, element.dataset.id);
    };

    for (const host of [this.panels.topEl, this.panels.paletteEl, this.panels.propertiesEl]) {
      host.addEventListener('click', onClick);
      host.addEventListener('change', onChange);
    }
  }

  /**
   * The intents this app answers, so a test can compare them with the markup.
   *
   * `r.` is generic on purpose: a rule input writes the `RuleSet` field of the
   * same name, so there is no table to compare against — only the compound ones
   * that do not map field-for-field are listed.
   */
  get handledIntents(): {
    commands: string[];
    fields: string[];
    genericFieldPrefixes: string[];
  } {
    return {
      commands: Object.keys(this.commands),
      fields: [
        ...Object.keys(this.documentFields),
        ...Object.keys(PLAYER_FIELDS).map((field) => `p.${field}`),
        ...Object.keys(COMPOUND_RULE_FIELDS).map((field) => `r.${field}`),
      ],
      genericFieldPrefixes: ['r.'],
    };
  }
}

/* ------------------------------------------------------------------ helpers */

function toggleObjective(
  list: Objective[],
  type: Objective['type'],
  rules: Partial<RuleSet>,
): Objective[] {
  const has = list.some((objective) => objective.type === type);
  if (has) return list.filter((objective) => objective.type !== type);
  const next: Objective =
    type === 'surviveTurns' ? { type, turns: rules.turnLimit ?? 12 } : ({ type } as Objective);
  return [...list, next];
}

/* ------------------------------------------------------------ level loading */

export function initialLevel(content: ContentCatalog): LevelData {
  const params = new URLSearchParams(location.search);
  const wanted = params.get('level');
  if (wanted) {
    const found =
      BUILTIN_LEVELS.find((level) => level.id === wanted) ??
      loadCustomLevels().find((saved) => saved.level.id === wanted)?.level;
    if (found) return found;
  }
  const draft = localStorage.getItem(DRAFT_KEY);
  if (draft) {
    try {
      const level = normaliseLevel(JSON.parse(draft));
      mapFromLevel(content, level);
      return level;
    } catch {
      /* fall through to a blank map */
    }
  }
  return emptyLevel(20, 14);
}
