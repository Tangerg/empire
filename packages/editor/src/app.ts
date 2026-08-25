import {
  copyLevelJson,
  downloadLevel,
  loadCustomLevels,
  pickJsonFile,
  readEditorDraft,
  saveEditorDraft,
  saveCustomLevel,
  stashPlaytest,
  TEAM_COLORS,
  type ArtDirection,
} from '@empire/game-ui';
import {
  type ContentCatalog,
  type BattleRuleServices,
  errorMessage,
  validateLevel,
  DEFAULT_GRID,
  DEFAULT_TURN_ORDER,
  type DirectionDef,
  type TacticalGrid,
  defaultPlayer,
  FUNDS_RESOURCE,
} from '@empire/battle-engine';
import {
  emptyLevel,
  mapFromLevel,
  normaliseLevel,
  type LevelIssue,
} from '@empire/battle-engine';
import type {
  Coord,
  CoverLevel,
  Direction,
  LevelData,
  Objective,
  PlayerConfig,
  RuleSet,
} from '@empire/battle-engine';
import { EditorBoard } from './board';
import { EditorDocument } from './document';
import { EditorHistory } from './history';
import { EditorPanels, type EditorPanelView } from './panels';
import {
  BrushSettings,
  EDITOR_TOOLS,
  type EditorTool,
  type EditorToolContext,
  type EditorToolRegistry,
} from './tools';


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

/**
 * The game this editor was opened for.
 *
 * A level editor is generic; the level it edits is not. This package used to
 * import one campaign's levels directly, so the "general" editor could only
 * ever offer ancient-empires maps to open — a second story with its own catalog
 * had no way in. The ruleset already carries the catalog, and the levels come
 * from whoever composed the ruleset, which is the application root.
 */
export interface EditorSetup {
  /**
   * The ruleset this level is being written for.
   *
   * It used to be the content catalog alone, which is why the editor could tell
   * you that a unit type did not exist but not that an objective kind, a
   * standing order or a turn-order policy was one nobody had registered: those
   * live in the composed rules, and the editor could not see them. A level is
   * authored *against a ruleset*; that is the dependency.
   */
  readonly rules: BattleRuleServices;
  /**
   * The art this level is drawn with, composed beside the ruleset.
   *
   * The editor used to draw with `GENERIC_ART` no matter what it had been
   * opened for, so an author working on the shipped campaign saw thirty-one of
   * its forty unit types as the same soldier and eleven of its terrains as the
   * same meadow — the pack's own art was installed in the application and the
   * editor never asked for it.
   */
  readonly art: ArtDirection;
  /** Toolbox selected by the host; cloned and sealed when the editor opens. */
  readonly tools?: EditorToolRegistry;
  /** Levels offered in the open menu, beside the author's own saves. */
  readonly presets: readonly LevelData[];
}

export class EditorApp {
  private doc: EditorDocument;
  private board: EditorBoard;
  private readonly panels = new EditorPanels();
  private readonly history = new EditorHistory();

  private tool: EditorTool;
  private readonly tools: EditorToolRegistry;
  private readonly brush: BrushSettings;
  private strokeAnchor: Coord | null = null;
  private cursor: Coord | null = null;
  private showCoords = false;
  private showOwners = true;
  private strokeOpen = false;
  private status = '';
  private disposed = false;

  private readonly root = document.createElement('div');
  private readonly scroller = document.createElement('div');

  private readonly content: ContentCatalog;

  constructor(
    private readonly setup: EditorSetup,
    level: LevelData,
  ) {
    const content = setup.rules.content;
    this.content = content;
    this.tools = (setup.tools ?? EDITOR_TOOLS).clone().seal();
    this.tool = this.tools.default;
    // The document comes first: the brush opens on the tiling this level is
    // authored for, and the tiling is the document's to name.
    this.doc = EditorDocument.fromLevel(content, level);
    this.brush = new BrushSettings(content, this.grid);
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
    }, content, setup.art);
    this.scroller.append(this.board.el);

    const stage = document.createElement('div');
    stage.className = 'stage';
    stage.append(this.panels.paletteEl, this.scroller, this.panels.propertiesEl);
    this.root.append(this.panels.topEl, stage);

    this.bindDelegates();
    document.addEventListener('keydown', this.onKey);
    this.board.el.addEventListener(
      'wheel',
      this.onBoardWheel,
      { passive: false },
    );

    this.renderAll();
  }

  mount(host: HTMLElement): void {
    if (this.disposed) throw new Error('cannot mount a disposed editor');
    host.replaceChildren(this.root);
  }

  /** Releases every listener whose lifetime is wider than one DOM callback. */
  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    document.removeEventListener('keydown', this.onKey);
    this.board.el.removeEventListener('wheel', this.onBoardWheel);
    for (const host of this.delegateHosts()) {
      host.removeEventListener('click', this.onDelegateClick);
      host.removeEventListener('change', this.onDelegateChange);
    }
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
    saveEditorDraft(this.exportLevel());
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
    if (!facings.some((facing) => facing.id === this.brush.coverSide)) {
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

  private readonly onBoardWheel = (event: WheelEvent): void => {
    if (!event.ctrlKey && !event.metaKey) return;
    event.preventDefault();
    this.board.setZoom(this.board.zoomLevel - Math.sign(event.deltaY) * 0.1);
  };

  private readonly onKey = (event: KeyboardEvent): void => {
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
    const pressed = this.tools.forHotkey(event.key);
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
        this.selectTool(this.tools.default);
        this.renderAll();
      }
    }
  };

  /* ------------------------------------------------------------------- io */

  /** Serialise the document to the on-disk level format. */
  exportLevel(): LevelData {
    return this.doc.toLevel();
  }

  private errorsInDocument(): LevelIssue[] {
    return this.lint().filter((issue) => issue.severity === 'error');
  }

  private lint(): LevelIssue[] {
    return validateLevel(this.setup.rules, this.exportLevel());
  }

  private save(): void {
    const errors = this.errorsInDocument();
    try {
      saveCustomLevel(this.exportLevel());
    } catch (error) {
      // A refused save is reported, never silent: the alternative is telling the
      // author their level is saved when the slot could not be written.
      this.status = `保存失败：${errorMessage(error)}`;
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
    downloadLevel(this.exportLevel());
    this.status = '已导出 JSON';
    this.renderTop();
  }

  private async copyJson(): Promise<void> {
    try {
      await copyLevelJson(this.exportLevel());
      this.status = 'JSON 已复制到剪贴板';
    } catch (error) {
      // The reason comes from the error, not from a guess about it: this used to
      // report a refused clipboard permission for anything at all, including a
      // document that failed to serialise.
      this.status = `复制失败：${errorMessage(error)}`;
    }
    this.renderTop();
  }

  private importFile(): void {
    pickJsonFile((text) => {
      try {
        const level = normaliseLevel(JSON.parse(text));
        mapFromLevel(this.content, level); // fail fast on a broken terrain grid
        this.loadLevel(level, `已载入 ${level.name}`);
      } catch (error) {
        this.status = `载入失败：${errorMessage(error)}`;
        this.renderTop();
      }
    });
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
  /**
   * The tiling this document is authored for, or the default when it names one
   * this ruleset has never heard of.
   *
   * Total, where the facings getter used to answer `[]` — a canvas with no
   * facings at all offered no cover side to paint with, and the level validator
   * is the one that should be complaining about an unknown grid.
   */
  private get grid(): TacticalGrid {
    const named = this.doc.rules.grid ?? DEFAULT_GRID;
    return this.setup.rules.grids.tryGet(named) ?? this.setup.rules.grids.get(DEFAULT_GRID);
  }

  private get facings(): readonly DirectionDef[] {
    return this.grid.directions;
  }

  private panelView(issues: readonly LevelIssue[] = []): EditorPanelView {
    return {
      document: this.doc,
      content: this.content,
      art: this.setup.art,
      grid: this.grid,
      tool: this.tool,
      tools: this.tools.tools,
      brush: this.brush,
      status: this.status,
      showCoords: this.showCoords,
      showOwners: this.showOwners,
      canUndo: this.history.canUndo,
      canRedo: this.history.canRedo,
      issues,
      facings: this.facings,
      resources: this.setup.rules.resources,
      turnOrders: this.setup.rules.turnOrders.all().map(({ id, name }) => ({ id, name })),
      objectives: CHIP_OBJECTIVES.map((type) => ({
        type,
        label: this.setup.rules.objectives.get(type).label,
      })),
      defaultTurnOrder: DEFAULT_TURN_ORDER,
      presets: [
        ...this.setup.presets.map((level) => ({ value: `b:${level.id}`, label: `内置 · ${level.name}` })),
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
    this.board.render({
      levelId: this.doc.id,
      grid: this.grid,
      map: this.doc.map,
      units: this.doc.units,
      players: this.doc.players,
    }, {
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
      this.selectTool(this.tools.tryGet(arg) ?? this.tool);
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
      if (this.tool.id !== 'rect' && this.tool.id !== 'fill') this.selectTool(this.tools.default);
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
      this.selectTool(this.tools.get('unit'));
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
      this.loadLevel(emptyLevel(this.content, this.doc.map.width, this.doc.map.height), this.status);
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
    // What a side starts as is the engine's answer, not this editor's: it was
    // written out here, so the two accounts and the aggression were stated twice.
    return {
      ...defaultPlayer(id, `玩家 ${id}`, TEAM_COLORS[(id - 1) % TEAM_COLORS.length], 'ai'),
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
      ? this.setup.presets.find((level) => level.id === levelId)
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

  private readonly onDelegateClick = (event: Event): void => {
    const element = (event.target as HTMLElement).closest('[data-act]') as HTMLElement | null;
    if (!element) return;
    this.commands[element.dataset.act ?? '']?.(element.dataset.arg ?? '');
    this.autosave();
  };

  private readonly onDelegateChange = (event: Event): void => {
    const element = event.target as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;
    const field = element.dataset.field;
    if (!field) return;
    const value = element instanceof HTMLInputElement && element.type === 'checkbox'
      ? element.checked
      : element.value;
    this.applyField(field, value, element.dataset.id);
  };

  private delegateHosts(): HTMLElement[] {
    return [this.panels.topEl, this.panels.paletteEl, this.panels.propertiesEl];
  }

  private bindDelegates(): void {
    for (const host of this.delegateHosts()) {
      host.addEventListener('click', this.onDelegateClick);
      host.addEventListener('change', this.onDelegateChange);
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

/**
 * The objective kinds a chip can author.
 *
 * The editor's own judgment, and it belongs next to `toggleObjective` because
 * that is what limits it: a chip carries no payload, so a kind needing a
 * selector, a zone or a nested objective cannot be toggled into existence, and
 * `surviveTurns` is on the list only because the one field it needs is right
 * below. What each is *called* comes from the registry — nine hand-written labels
 * used to live in the properties panel, one of them a second copy of the
 * engine's own `控制全部据点`.
 */
const CHIP_OBJECTIVES: readonly Objective['type'][] = [
  'routEnemies',
  'captureHQ',
  'holdAllVillages',
  'surviveTurns',
];

function toggleObjective(
  list: Objective[],
  type: Objective['type'],
  ruleSet: Partial<RuleSet>,
): Objective[] {
  const has = list.some((objective) => objective.type === type);
  if (has) return list.filter((objective) => objective.type !== type);
  const next: Objective =
    type === 'surviveTurns' ? { type, turns: ruleSet.turnLimit ?? 12 } : ({ type } as Objective);
  return [...list, next];
}

/* ------------------------------------------------------------ level loading */

export function initialLevel(setup: EditorSetup): LevelData {
  const content = setup.rules.content;
  const params = new URLSearchParams(location.search);
  const wanted = params.get('level');
  if (wanted) {
    const found =
      setup.presets.find((level) => level.id === wanted) ??
      loadCustomLevels().find((saved) => saved.level.id === wanted)?.level;
    if (found) return found;
  }
  const draft = readEditorDraft();
  if (draft) {
    try {
      const level = normaliseLevel(JSON.parse(draft));
      mapFromLevel(content, level);
      return level;
    } catch (error) {
      // A stale draft must not prevent the editor from opening, but its loss is
      // still observable to the author and to diagnostics.
      console.warn('Editor draft was rejected; opening a blank map instead.', error);
    }
  }
  return emptyLevel(content);
}
