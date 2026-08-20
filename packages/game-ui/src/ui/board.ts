import {
  idx,
  type ContentCatalog,
  type Coord,
  type GameState,
  type Unit,
  type WeaponId,
  type TacticalGrid,
} from '@empire/battle-engine';
import { PAL } from '../art/palette';
import { decorationsFor, type BattlePresentation } from '../art/battle-presentation';
import { markerFromRules, structureFromRules } from '../art/field-objects-from-rules';
import type { ArtDirection } from '../art/direction';
import type { BoardDecorations, BoardLayout, DecorationTint } from '../art/board-decorations';
import { battlefieldFeaturePieces, battlefieldRenderKey } from '../art/battlefield-layer';
import { TILE, terrainLayerPieces } from '../art/terrain';
import {
  cellCenter,
  cellOrigin,
  cellShape,
  createSceneViewport,
  scenePointToCell,
  type SceneViewport,
} from '../art/scene-viewport';
import { unitSpriteMarkup } from '../art/units';
import { escapeHtml } from './html';
import {
  wholeField,
  type BoardDrawing,
  type BoardPiece,
  type BoardSurface,
  type BoardSurfaceFactory,
} from '../art/board-surface';

export interface BoardOverlay {
  move: Set<number>;
  attack: Set<number>;
  heal: Set<number>;
  threat: Set<number>;
  /** Ground the enemy holds: a move that enters one of these tiles stops there. */
  controlled: Set<number>;
  path: Coord[];
  selected: Coord | null;
  cursor: Coord | null;
  /** Tiles a charging strike is already aimed at, with turns remaining. */
  incoming: Map<number, number>;
  /** null = no fog. */
  visible: Set<number> | null;
  hiddenUnits: Set<number>;
}

export const emptyOverlay = (): BoardOverlay => ({
  move: new Set(),
  attack: new Set(),
  heal: new Set(),
  threat: new Set(),
  controlled: new Set(),
  incoming: new Map(),
  path: [],
  selected: null,
  cursor: null,
  visible: null,
  hiddenUnits: new Set(),
});

/**
 * What a battle is drawn with, composed by the application root.
 *
 * Declared here because this is the consumer that needs it, and gathered into one
 * named port because the call was `f(state, handlers, content, grid, art)` — three
 * dependencies threaded behind the two subjects, which is the reverse of the shape
 * `AGENTS.md` asks for. The renderer joined them rather than becoming a sixth
 * positional parameter: it is a choice, and it belongs with the other three.
 */
export interface BoardComposition {
  readonly content: ContentCatalog;
  /**
   * The tiling this battle is fought on; it decides where every cell sits.
   *
   * Handed in rather than looked up: the picture has to be drawn under the same
   * tiling the rules are measured with, including a replaced one.
   */
  readonly grid: TacticalGrid;
  readonly art: ArtDirection;
  /**
   * Which renderer draws it.
   *
   * The board used to write `new SvgBoardSurface(…)` here, which made the port a
   * seam with only one possible other side — and made the board the thing that
   * chose, rather than the application root that composes everything else.
   */
  readonly renderer: BoardSurfaceFactory;
}

export interface BoardHandlers {
  onTileClick(at: Coord): void;
  /** The player asked for a different scale, in notches. */
  onScale(notches: number): void;
  onTileEnter(at: Coord): void;
  onLeave(): void;
  onSecondary(at: Coord): void;
}

const frame = () => new Promise<number>((r) => requestAnimationFrame(r));
const wait = (ms: number) => new Promise<void>((r) => setTimeout(r, ms));

async function tween(ms: number, step: (t: number) => void): Promise<void> {
  const start = performance.now();
  for (;;) {
    const now = await frame();
    const t = Math.min(1, (now - start) / ms);
    step(t);
    if (t >= 1) return;
  }
}

const easeInOut = (t: number) => (t < 0.5 ? 2 * t * t : 1 - 2 * (1 - t) ** 2);

/**
 * How long after the last scale change the board considers itself settled.
 *
 * Long enough to cover the gap between two wheel notches on a mouse, short enough
 * that letting go feels like an immediate return of the shadows.
 */
const SETTLE_MS = 140;

/**
 * The board. Owns the SVG tree and all animation; knows nothing about rules or
 * selection state beyond the overlay it is handed.
 */
export class BoardView {
  /**
   * Where this battle is drawn.
   *
   * A port. The board decides what the field looks like and the surface puts it
   * somewhere — an SVG tree today, and the seam exists so that it need not be.
   */
  private readonly surface: BoardSurface;
  private readonly viewport: SceneViewport;
  private readonly presentation: BattlePresentation;
  private readonly decor: BoardDecorations;
  private zoom = 1;
  private mapSignature = '';
  private objectSignature = '';
  private hovered: string | null = null;
  private layoutCache: BoardLayout | null = null;
  /** Only for making a turn banner's gradient id unique. */
  private banners = 0;
  /**
   * Units whose facing `animateMove` is currently driving, step by step.
   *
   * The board's own bookkeeping, not a look. It used to be a `moving` state told to
   * the drawing and read back with `inState` — the board keeping a fact about its
   * animations inside the renderer, where a second backend would have had to store
   * something nothing ever draws.
   */
  private readonly walking = new Set<number>();
  private settleTimer: ReturnType<typeof setTimeout> | null = null;

  private readonly content: ContentCatalog;
  private readonly art: ArtDirection;

  constructor(
    composition: BoardComposition,
    private state: GameState,
    private readonly handlers: BoardHandlers,
  ) {
    this.content = composition.content;
    this.art = composition.art;
    this.presentation = this.art.presentationFor(state.levelId);
    this.decor = decorationsFor(this.presentation);
    this.viewport = createSceneViewport(
      composition.grid,
      state.map.width,
      state.map.height,
      TILE,
      this.presentation.sceneProfile(state.levelId),
    );
    const sceneFrame = this.presentation.sceneFrame(state.levelId, state.map, this.viewport);
    this.surface = composition.renderer({
      width: this.viewport.sceneWidth,
      height: this.viewport.sceneHeight,
      originX: this.viewport.originX,
      originY: this.viewport.originY,
      themeClass: this.presentation.boardClass,
      shapeRendering: this.decor.shapeRendering,
      backdrop: sceneFrame.backdrop,
      foreground: sceneFrame.foreground,
    });
    this.buildGrid();
    this.bindPointer();
    this.setZoom(1.25);
  }

  /** What gets mounted, and what a pointer position is measured against. */
  get el(): SVGElement | HTMLElement {
    return this.surface.element;
  }

  /* -------------------------------------------------------------- placement */

  /** Where this board's cells are, for whoever draws on top of them. */
  private get layout(): BoardLayout {
    return this.layoutCache ??= {
      tileSize: TILE,
      corners: this.viewport.grid.outline().length,
      origin: (at) => cellOrigin(this.viewport, at),
      center: (at) => cellCenter(this.viewport, at),
      shape: () => cellShape(this.viewport),
      neighbour: (at, direction) => cellCenter(this.viewport, this.viewport.grid.step(at, direction)),
    };
  }

  /**
   * Which way a unit is looking, as one arrow turned to point at the cell it
   * would step into.
   *
   * This used to be a four-entry table of glyphs — `north: '↑'` — which is the
   * square board's facing set written into the sprite badge: on a hex or
   * eight-way board every unit wore an empty badge. The tiling knows where its
   * neighbour sits, and that is the whole answer.
   */
  private facingBadge(unit: Unit): string {
    const grid = this.viewport.grid;
    const dot = `<circle cx="5" cy="6" r="4.8" fill="${PAL.ink}" opacity="0.78"/>`;
    if (!grid.directions.some((facing) => facing.id === unit.facing)) return dot;
    const from = cellCenter(this.viewport, unit);
    const to = cellCenter(this.viewport, grid.step(unit, unit.facing));
    const turn = Math.atan2(to.y - from.y, to.x - from.x) * 180 / Math.PI + 90;
    return `${dot}<text x="5" y="8.8" text-anchor="middle" font-size="7.5" fill="#fff" transform="rotate(${turn.toFixed(1)} 5 6)">↑</text>`;
  }

  /** Top-left of a cell, for a group that draws a tile-sized picture. */
  private origin(at: Coord): { x: number; y: number } {
    return cellOrigin(this.viewport, at);
  }

  /** Centre of a cell, for anything drawn around a point. */
  private centre(at: Coord): { x: number; y: number } {
    return cellCenter(this.viewport, at);
  }

  /** A picture, at the origin of the cell it belongs to. */
  private at(cell: Coord, markup: string): BoardPiece {
    return { markup, ...this.origin(cell) };
  }

  /** The cell a flat map index refers to. */
  private cellOf(index: number): Coord {
    return { x: index % this.state.map.width, y: Math.floor(index / this.state.map.width) };
  }

  /* ------------------------------------------------------------------ setup */

  private buildGrid(): void {
    this.surface.setLayer('grid', this.decor.gridLines(this.layout, this.state.map));
  }

  /**
   * Which cell the pointer is over, which is the tiling's answer.
   *
   * The surface reports scene coordinates; turning those into a cell is the only
   * half of this the board owns. It used to own both, and therefore held a
   * `getBoundingClientRect` and a letterbox calculation.
   */
  private bindPointer(): void {
    const cellAt = (at: { x: number; y: number }) => scenePointToCell(this.viewport, at.x, at.y);
    this.surface.listen({
      press: (at, button) => {
        const cell = cellAt(at);
        if (!cell) return;
        if (button === 2) this.handlers.onSecondary(cell);
        else if (button === 0) this.handlers.onTileClick(cell);
      },
      move: (at) => {
        const cell = at ? cellAt(at) : null;
        if (!cell) {
          if (!this.hovered) return;
          this.hovered = null;
          this.handlers.onLeave();
          return;
        }
        const key = `${cell.x},${cell.y}`;
        if (key === this.hovered) return;
        this.hovered = key;
        this.handlers.onTileEnter(cell);
      },
      leave: () => {
        this.hovered = null;
        this.handlers.onLeave();
      },
      scale: (notches) => this.handlers.onScale(notches),
    });
  }

  setZoom(z: number): void {
    const zoom = Math.max(0.5, Math.min(2.2, z));
    if (zoom === this.zoom) return;
    this.zoom = zoom;
    this.surface.resize(this.viewport.sceneWidth * zoom, this.viewport.sceneHeight * zoom);

    this.surface.rescaling(true);
    if (this.settleTimer !== null) clearTimeout(this.settleTimer);
    this.settleTimer = setTimeout(() => {
      this.settleTimer = null;
      this.surface.rescaling(false);
    }, SETTLE_MS);
  }

  /**
   * Fills the given box with the whole tactical field, without cropping it.
   *
   * "Fills", not "fits inside": this used to cap the scale at 1.25, so a 20×14
   * field on any ordinary screen was a 800×560 rectangle floating in the middle
   * of a much larger gradient — the single clearest reason the battle read as a
   * figure on a page rather than the thing being played. The zoom clamp still
   * bounds how large a tile may get; a tiny field simply stops growing there.
   */
  fitWithin(width: number, height: number): void {
    if (width <= 0 || height <= 0) return;
    this.setZoom(Math.min(width / this.viewport.sceneWidth, height / this.viewport.sceneHeight));
  }

  get zoomLevel(): number {
    return this.zoom;
  }

  /* ----------------------------------------------------------------- render */

  setState(state: GameState): void {
    this.state = state;
  }

  dispose(): void {
    this.surface.dispose();
    if (this.settleTimer !== null) clearTimeout(this.settleTimer);
  }

  /** Terrain only changes when ownership does, so it is cheap to diff by hash. */
  syncTerrain(): void {
    const s = this.state;
    const signature = battlefieldRenderKey(s.map);
    if (signature === this.mapSignature) return;
    this.mapSignature = signature;
    const colorOf = (id: number) => s.players.find((p) => p.id === id)?.color;
    this.surface.setLayer('terrain', terrainLayerPieces(
      { art: this.art, layout: this.layout, content: this.content }, s.map, colorOf, s.levelId,
    ));
    // An authored scene is one painting, not a picture per cell, so it crosses
    // the seam as the whole field. Which is why the layer it lands in is the
    // second-largest on a big board and the one still worth breaking up.
    const sceneLayers = this.presentation.sceneLayers(s.levelId, s.map, this.viewport);
    this.surface.setLayer('ground', wholeField(sceneLayers.ground));
    this.surface.setLayer('scenery', wholeField(sceneLayers.underUnits));
    this.surface.setLayer('foreground', wholeField(sceneLayers.overUnits));
    this.surface.setLayer('spatial', battlefieldFeaturePieces({ art: this.art, layout: this.layout }, s.map));
  }

  render(overlay: BoardOverlay): void {
    this.surface.tactical(
      !!overlay.selected || overlay.move.size > 0 || overlay.attack.size > 0
      || overlay.heal.size > 0 || overlay.path.length > 0,
    );
    this.syncTerrain();
    this.syncBattlefieldObjects();
    this.renderRanges(overlay);
    this.renderPath(overlay.path);
    this.renderUnits(overlay);
    this.renderCursor(overlay);
  }

  private syncBattlefieldObjects(): void {
    const s = this.state;
    const signature = [
      ...s.structures.map((item) => `${item.id}:${item.type}:${item.owner}:${item.x},${item.y}:${item.hp}:${item.disabled}`),
      ...s.markers.map((item) => `${item.id}:${item.kind}:${item.owner}:${item.at.x},${item.at.y}`),
    ].join('|');
    if (signature === this.objectSignature) return;
    this.objectSignature = signature;

    // The presentation answers first and the generic look is the floor beneath
    // it, so declining to draw a structure is no longer the same as hiding one.
    // Every one of the six structure types and five marker kinds this repository
    // ships was invisible on a board with no painted scene — and one of them is a
    // shipped chapter's victory condition.
    this.surface.setLayer('structures', s.structures.map((state) => {
      const ownerColor = s.players.find((player) => player.id === state.owner)?.color;
      const definition = this.content.structures.get(state.type);
      return this.at(state, this.presentation.structure(state, definition, ownerColor)
        ?? structureFromRules(state, definition, ownerColor));
    }));

    this.surface.setLayer('markers', s.markers.map((marker) => {
      const ownerColor = s.players.find((player) => player.id === marker.owner)?.color;
      return this.at(marker.at, this.presentation.marker(marker, ownerColor)
        ?? markerFromRules(marker, ownerColor));
    }));
  }

  /**
   * The tactical overlay: one tinted spot per tile, at every tile that wants it.
   *
   * `actionSpot` used to be handed the cell and return a picture already at it, so
   * a fog of war over a large field was four thousand differently-worded copies of
   * the same dark shape. It is one shape now, placed four thousand times.
   */
  private renderRanges(o: BoardOverlay): void {
    const s = this.state;
    const pieces: BoardPiece[] = [];
    const spread = (cells: Iterable<number>, tint: DecorationTint) => {
      const spot = this.decor.actionSpot(this.layout, tint);
      for (const i of cells) pieces.push(this.at(this.cellOf(i), spot));
    };
    spread(o.threat, { fill: '#ff3b30', opacity: 0.1 });
    // Held ground is drawn under the move range: the player needs to see why a
    // path stops short, not merely that it does.
    spread(o.controlled, { fill: '#8c6bd8', opacity: 0.18, stroke: '#b9a3f0' });
    // A marked tile is committed damage, so it reads stronger than mere threat
    // and carries its own countdown.
    const marked = this.decor.actionSpot(this.layout, { fill: '#ffb020', opacity: 0.3, stroke: '#ffd479' });
    const middle = TILE / 2;
    for (const [i, remaining] of o.incoming) {
      pieces.push(this.at(this.cellOf(i), `${marked}<text class="incoming-count" x="${middle}" y="${middle + 4}"
        text-anchor="middle" font-size="11" font-weight="700" fill="#2a1a00">${remaining}</text>`));
    }
    spread([...o.move].filter((i) => !o.attack.has(i)), { fill: '#3f9fff', opacity: 0.22 });
    spread(o.heal, { fill: '#5fd07a', opacity: 0.28, stroke: '#8ef7a5' });
    spread(o.attack, { fill: '#ff4436', opacity: 0.16, stroke: '#ff7468' });

    if (o.visible) {
      const unseen: number[] = [];
      for (let i = 0; i < s.map.tiles.length; i++) if (!o.visible.has(i)) unseen.push(i);
      spread(unseen, { fill: '#0b1020', opacity: 0.55 });
    }
    this.surface.setLayer('range', pieces);
  }

  private renderPath(path: Coord[]): void {
    if (path.length < 2) {
      this.surface.setLayer('path', []);
      return;
    }
    const d = this.decor.movePath(this.layout, path);
    const last = path[path.length - 1];
    const prev = path[path.length - 2];
    const angle = (Math.atan2(last.y - prev.y, last.x - prev.x) * 180) / Math.PI;
    // The line runs through many cells, so it is field-wide; the arrowhead is a
    // picture at the cell the march ends on.
    this.surface.setLayer('path', [
      ...wholeField(
        `<path d="${d}" fill="none" stroke="#ffffff" stroke-width="5" stroke-linejoin="round" stroke-linecap="round" opacity="0.85"/>
         <path d="${d}" fill="none" stroke="#2f6fd0" stroke-width="2.6" stroke-linejoin="round" stroke-linecap="round"/>`,
      ),
      this.at(last, `<g transform="translate(${TILE / 2} ${TILE / 2}) rotate(${angle})">
           <path d="M-4 -5 4 0 -4 5z" fill="#2f6fd0" stroke="#ffffff" stroke-width="1.2"/>
         </g>`),
    ]);
  }

  /** The drawing for a unit, made on first sight and kept until it leaves. */
  private drawingFor(unit: Unit): BoardDrawing {
    const drawn = this.surface.unit(unit.id, () => {
      const color = this.state.players.find((p) => p.id === unit.owner)?.color ?? PAL.neutral;
      return unitSpriteMarkup(this.art, this.content.units.get(unit.type), color);
    });
    if (drawn.fresh) drawn.play('idle');
    return drawn.drawing;
  }

  private renderUnits(o: BoardOverlay): void {
    const s = this.state;
    const alive = new Set(s.units.map((u) => u.id));
    for (const id of this.surface.drawnUnits()) {
      if (alive.has(id)) continue;
      this.surface.removeUnit(id);
    }

    for (const u of s.units) {
      const drawing = this.drawingFor(u);
      if (!this.walking.has(u.id)) drawing.say('facingLeft', u.facing === 'west');
      const hidden = o.hiddenUnits.has(u.id);
      drawing.say('hidden', hidden);
      if (hidden) continue;
      const origin = this.origin(u);
      drawing.place(origin.x, origin.y);
      drawing.say('selected', !!o.selected && o.selected.x === u.x && o.selected.y === u.y);

      const def = this.content.units.get(u.type);
      const ratio = u.hp / def.maxHp;
      const parts: string[] = [this.facingBadge(u)];
      if (ratio < 1) {
        const color = ratio > 0.6 ? PAL.hpGood : ratio > 0.3 ? PAL.hpMid : PAL.hpLow;
        parts.push(
          `<rect x="5" y="27.6" width="22" height="3.6" rx="1.8" fill="${PAL.ink}" opacity="0.65"/>
           <rect x="5.6" y="28.2" width="${(20.8 * ratio).toFixed(2)}" height="2.4" rx="1.2" fill="${color}"/>`,
        );
      }
      const capture = s.map.captureProgress[idx(s.map, u.x, u.y)];
      if (capture > 0) {
        const pct = Math.min(1, capture / s.rules.captureThreshold);
        parts.push(
          `<circle cx="27" cy="6" r="4.4" fill="${PAL.ink}" opacity="0.6"/>
           <circle cx="27" cy="6" r="3.2" fill="none" stroke="${PAL.gold}" stroke-width="2"
             stroke-dasharray="${(pct * 20.1).toFixed(2)} 20.1" transform="rotate(-90 27 6)"/>`,
        );
      }
      if (u.done) {
        parts.push(`<rect width="32" height="32" fill="${PAL.ink}" opacity="0.35"/>`);
      }
      drawing.fill('badges', parts.join(''));
    }
  }

  private renderCursor(o: BoardOverlay): void {
    const pieces: BoardPiece[] = [];
    if (o.selected) pieces.push(this.at(o.selected, this.decor.ring(this.layout, 'selection')));
    if (o.cursor) pieces.push(this.at(o.cursor, this.decor.ring(this.layout, 'cursor')));
    this.surface.setLayer('cursor', pieces);
  }

  /* -------------------------------------------------------------- animation */

  async animateMove(unit: Unit, path: Coord[], msPerTile = 85): Promise<void> {
    const drawn = this.surface.drawnUnit(unit.id);
    if (!drawn || path.length < 2) return;
    const { drawing } = drawn;
    drawn.play('walk');
    // Cleared however this ends. It used to be set and unset around the loop, so a
    // tween that threw left the unit's facing frozen for the rest of the battle.
    this.walking.add(unit.id);
    try {
      for (let i = 1; i < path.length; i++) {
        const a = path[i - 1];
        const b = path[i];
        if (b.x !== a.x) drawing.say('facingLeft', b.x < a.x);
        await tween(msPerTile, (t) => {
          const e = easeInOut(t);
          const from = this.origin(a);
          const to = this.origin(b);
          drawing.place(
            from.x + (to.x - from.x) * e,
            from.y + (to.y - from.y) * e - Math.sin(Math.PI * t) * 2.5,
          );
        });
      }
    } finally {
      this.walking.delete(unit.id);
    }
    drawn.play('idle');
  }

  async animateStrike(attacker: Unit, target: Coord): Promise<void> {
    const drawn = this.surface.drawnUnit(attacker.id);
    if (!drawn) return;
    const { drawing } = drawn;
    const dx = Math.sign(target.x - attacker.x);
    const dy = Math.sign(target.y - attacker.y);
    if (dx !== 0) drawing.say('facingLeft', dx < 0);
    const anchor = this.origin(attacker);
    drawing.place(anchor.x, anchor.y);
    drawn.play('attack');
    await tween(150, (t) => {
      const push = Math.sin(Math.PI * t) * 7;
      drawing.nudge(dx * push, dy * push);
    });
    drawn.play('idle');
    drawing.nudge(0, 0);
  }

  async animateHit(at: Coord, damage: number, killed: boolean, weapon?: WeaponId): Promise<void> {
    const fx = weapon ? this.presentation.weaponFx(weapon) : null;
    // Drawn about its own origin and then placed, like everything else on the board.
    // These used to bake the cell's scene coordinates into the markup, which is the
    // one thing left that a renderer could not treat as a picture at a position: it
    // would have had to rasterise a field-sized image to hold a damage number.
    const drawing = this.surface.effect(
      `${fx ? this.presentation.effect(fx) : ''}
         <g data-part="burst"><circle r="12" fill="#ffffff" opacity="0.65"/></g>
         <g data-part="number"><text y="-8" text-anchor="middle" class="fx-damage">-${damage}</text></g>`,
    );
    const { x: cx, y: cy } = this.centre(at);
    drawing.place(cx, cy);
    const burst = drawing.part('burst');
    const number = drawing.part('number');
    await tween(420, (t) => {
      // 12 → 22 was written as a radius; as a swell it is the same picture and the
      // one property a backend without shapes can also honour.
      burst?.swell(1 + t * 0.83);
      burst?.opacity(0.8 * (1 - t));
      number?.nudge(0, -14 * t);
      number?.opacity(1 - t ** 2);
    });
    drawing.remove();
    if (killed) await wait(40);
  }

  async animateDeath(unitId: number): Promise<void> {
    const drawn = this.surface.drawnUnit(unitId);
    if (!drawn) return;
    await tween(220, (t) => {
      drawn.drawing.opacity(1 - t);
      drawn.drawing.swell(1 - 0.35 * t);
    });
    this.surface.removeUnit(unitId);
  }

  async animateHeal(at: Coord, amount: number): Promise<void> {
    const drawing = this.surface.effect(
      `${this.presentation.healFx ? this.presentation.effect(this.presentation.healFx) : ''}
         <g data-part="number"><text y="-6" text-anchor="middle" class="fx-heal">+${amount}</text></g>`,
    );
    const { x: cx, y: cy } = this.centre(at);
    drawing.place(cx, cy);
    const number = drawing.part('number');
    await tween(500, (t) => {
      number?.nudge(0, -16 * t);
      number?.opacity(1 - t ** 2);
    });
    drawing.remove();
  }

  async animateSpawn(unitId: number): Promise<void> {
    const drawn = this.surface.drawnUnit(unitId);
    if (!drawn) return;
    const { drawing } = drawn;
    await tween(240, (t) => {
      drawing.opacity(t);
      drawing.swell(0.6 + 0.4 * t);
    });
    drawing.opacity(1);
    drawing.swell(1);
  }

  /**
   * The turn changing, said on the field itself.
   *
   * The one piece of interface that has always been inside the picture, and it
   * looked like the only one that was not thought about: a flat rectangle of team
   * colour at full width and 90% opacity, sliding in from the left. It is a band
   * of light now — feathered at both ends so it belongs to the scene rather than
   * covering it, with the field still readable through the middle.
   */
  async announce(text: string, color: string): Promise<void> {
    const w = this.viewport.fieldWidth;
    const h = this.viewport.fieldHeight;
    const band = `turn-band-${this.banners++}`;
    const drawing = this.surface.effect(
      `<defs>
           <linearGradient id="${band}" x1="0" y1="0" x2="1" y2="0">
             <stop offset="0" stop-color="${color}" stop-opacity="0"/>
             <stop offset="0.22" stop-color="${color}" stop-opacity="0.88"/>
             <stop offset="0.78" stop-color="${color}" stop-opacity="0.88"/>
             <stop offset="1" stop-color="${color}" stop-opacity="0"/>
           </linearGradient>
         </defs>
         <g class="fx-turn-band" data-part="band">
           <rect x="0" y="0" width="${w}" height="34" fill="url(#${band})"/>
           <rect x="0" y="0" width="${w}" height="1" fill="#fff" opacity="0.4"/>
           <rect x="0" y="33" width="${w}" height="1" fill="#000" opacity="0.32"/>
           <text x="${w / 2}" y="24" text-anchor="middle" class="fx-banner">${escapeHtml(text)}</text>
         </g>`,
    );
    drawing.place(0, h / 2 - 17);
    const sweep = drawing.part('band');
    await tween(220, (t) => {
      const eased = easeInOut(t);
      sweep?.nudge(-w * 0.3 * (1 - eased), 0);
      sweep?.opacity(eased);
    });
    await wait(420);
    await tween(220, (t) => drawing.opacity(1 - t));
    drawing.remove();
  }

  centerOn(at: Coord, container: HTMLElement): void {
    const centre = this.centre(at);
    const px = (this.viewport.originX + centre.x) * this.zoom;
    const py = (this.viewport.originY + centre.y) * this.zoom;
    container.scrollTo({
      left: px - container.clientWidth / 2,
      top: py - container.clientHeight / 2,
      behavior: 'smooth',
    });
  }

}
