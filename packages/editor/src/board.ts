import { clear, fromMarkup, setAttrs, svg } from '@empire/game-ui/art/svg';
import { PAL } from '@empire/game-ui/art/palette';
import { battlefieldFeatureMarkup, battlefieldRenderKey } from '@empire/game-ui/art/battlefield-layer';
import { TILE, terrainLayerMarkup } from '@empire/game-ui/art/terrain';
import { squareLayout } from '@empire/game-ui/art/board-decorations';
import { GENERIC_ART } from '@empire/game-ui/art/direction';
import type { ContentCatalog } from '@empire/battle-engine/content-pack';
import { unitSpriteMarkup } from '@empire/game-ui/art/units';
import { idx } from '@empire/battle-engine/grid';
import type { Coord, GameMap, LevelUnit, PlayerConfig } from '@empire/battle-engine/types';

export interface EditorBoardHandlers {
  onStroke(at: Coord, phase: 'start' | 'move' | 'end', button: number): void;
  onHover(at: Coord | null): void;
}

/**
 * Editor canvas. Deliberately separate from the in-game BoardView: it renders a
 * document (which may be temporarily invalid) rather than a GameState, and it
 * reports drag strokes instead of discrete clicks.
 */
export class EditorBoard {
  readonly el: SVGSVGElement;
  private readonly layers: Record<string, SVGGElement>;
  private zoom = 1;
  private drawing = false;
  private last: string | null = null;
  private signature = '';

  constructor(
    private map: GameMap,
    private units: LevelUnit[],
    private players: PlayerConfig[],
    private readonly handlers: EditorBoardHandlers,
    private readonly content: ContentCatalog,
  ) {
    this.el = svg('svg', {
      class: 'board editor-board',
      'shape-rendering': 'crispEdges',
      'text-rendering': 'optimizeLegibility',
    });
    this.layers = {};
    for (const n of ['terrain', 'grid', 'units', 'marks', 'cursor']) {
      const g = svg('g', { class: `layer layer-${n}` });
      this.layers[n] = g;
      this.el.append(g);
    }
    this.bind();
    this.resize(map);
  }

  resize(map: GameMap): void {
    this.map = map;
    this.signature = '';
    setAttrs(this.el, { viewBox: `0 0 ${map.width * TILE} ${map.height * TILE}` });
    clear(this.layers.grid);
    const parts: string[] = [];
    for (let x = 0; x <= map.width; x++) {
      parts.push(
        `<line x1="${x * TILE}" y1="0" x2="${x * TILE}" y2="${map.height * TILE}" stroke="${PAL.ink}" stroke-width="0.5" opacity="0.2"/>`,
      );
    }
    for (let y = 0; y <= map.height; y++) {
      parts.push(
        `<line x1="0" y1="${y * TILE}" x2="${map.width * TILE}" y2="${y * TILE}" stroke="${PAL.ink}" stroke-width="0.5" opacity="0.2"/>`,
      );
    }
    this.layers.grid.append(fromMarkup(parts.join('')));
    this.setZoom(this.zoom);
  }

  setZoom(z: number): void {
    this.zoom = Math.max(0.4, Math.min(2.4, z));
    this.el.style.width = `${this.map.width * TILE * this.zoom}px`;
    this.el.style.height = `${this.map.height * TILE * this.zoom}px`;
  }

  get zoomLevel(): number {
    return this.zoom;
  }

  private toCoord(ev: PointerEvent): Coord | null {
    const rect = this.el.getBoundingClientRect();
    const scale = rect.width / (this.map.width * TILE);
    const x = Math.floor((ev.clientX - rect.left) / scale / TILE);
    const y = Math.floor((ev.clientY - rect.top) / scale / TILE);
    if (x < 0 || y < 0 || x >= this.map.width || y >= this.map.height) return null;
    return { x, y };
  }

  private bind(): void {
    this.el.addEventListener('contextmenu', (e) => e.preventDefault());
    this.el.addEventListener('pointerdown', (ev) => {
      const c = this.toCoord(ev);
      if (!c) return;
      this.el.setPointerCapture(ev.pointerId);
      this.drawing = true;
      this.last = `${c.x},${c.y}`;
      this.handlers.onStroke(c, 'start', ev.button);
    });
    this.el.addEventListener('pointermove', (ev) => {
      const c = this.toCoord(ev);
      this.handlers.onHover(c);
      if (!this.drawing || !c) return;
      const key = `${c.x},${c.y}`;
      if (key === this.last) return;
      this.last = key;
      this.handlers.onStroke(c, 'move', ev.buttons & 2 ? 2 : 0);
    });
    const stop = (ev: PointerEvent) => {
      if (!this.drawing) return;
      this.drawing = false;
      const c = this.toCoord(ev);
      if (c) this.handlers.onStroke(c, 'end', ev.button);
    };
    this.el.addEventListener('pointerup', stop);
    this.el.addEventListener('pointercancel', stop);
    this.el.addEventListener('pointerleave', () => this.handlers.onHover(null));
  }

  /* ----------------------------------------------------------------- render */

  render(
    map: GameMap,
    units: LevelUnit[],
    players: PlayerConfig[],
    opts: { cursor: Coord | null; brush: Coord[]; showCoords: boolean; showOwners: boolean },
  ): void {
    this.map = map;
    this.units = units;
    this.players = players;

    const sig = battlefieldRenderKey(map);
    if (sig !== this.signature) {
      this.signature = sig;
      clear(this.layers.terrain);
      const colorOf = (id: number) => players.find((p) => p.id === id)?.color;
      this.layers.terrain.append(fromMarkup(terrainLayerMarkup({ art: GENERIC_ART, layout: squareLayout, content: this.content }, map, colorOf)));
    }

    clear(this.layers.units);
    const unitParts = units
      .map((u) => {
        const color = players.find((p) => p.id === u.owner)?.color ?? PAL.neutral;
        const bad = this.isBadPlacement(u);
        return `<g class="unit${u.facing === 'west' ? ' face-left' : ''}" transform="translate(${u.x * TILE},${u.y * TILE})">
          ${unitSpriteMarkup(GENERIC_ART, u.unit, color)}
          ${bad ? `<rect width="32" height="32" fill="#ff2d1f" opacity="0.35"/>` : ''}
        </g>`;
      })
      .join('');
    if (unitParts) this.layers.units.append(fromMarkup(unitParts));

    clear(this.layers.marks);
    const marks: string[] = [];
    if (opts.showOwners) {
      for (let i = 0; i < map.tiles.length; i++) {
        if (!this.content.terrains.get(map.tiles[i]).capturable) continue;
        const owner = map.owners[i];
        const color = players.find((p) => p.id === owner)?.color ?? PAL.neutral;
        const x = (i % map.width) * TILE;
        const y = Math.floor(i / map.width) * TILE;
        marks.push(
          `<rect x="${x + 0.75}" y="${y + 0.75}" width="${TILE - 1.5}" height="${TILE - 1.5}" rx="2"
             fill="none" stroke="${color}" stroke-width="1.5" opacity="0.9"/>`,
        );
      }
    }
    if (opts.showCoords) {
      for (let y = 0; y < map.height; y++) {
        for (let x = 0; x < map.width; x++) {
          if ((x + y) % 4 !== 0) continue;
          marks.push(
            `<text x="${x * TILE + 2}" y="${y * TILE + 9}" class="tile-coord">${x},${y}</text>`,
          );
        }
      }
    }
    const features = battlefieldFeatureMarkup(GENERIC_ART, squareLayout, map);
    if (features) marks.push(features);
    if (marks.length) this.layers.marks.append(fromMarkup(marks.join('')));

    clear(this.layers.cursor);
    const cursorParts = opts.brush.map(
      (c) =>
        `<rect x="${c.x * TILE + 0.5}" y="${c.y * TILE + 0.5}" width="${TILE - 1}" height="${TILE - 1}"
          fill="#ffffff" fill-opacity="0.14" stroke="${PAL.gold}" stroke-width="1.4"/>`,
    );
    if (opts.cursor) {
      cursorParts.push(
        `<rect x="${opts.cursor.x * TILE}" y="${opts.cursor.y * TILE}" width="${TILE}" height="${TILE}"
          fill="none" stroke="#ffffff" stroke-width="1.6"/>`,
      );
    }
    if (cursorParts.length) this.layers.cursor.append(fromMarkup(cursorParts.join('')));
  }

  /** Red wash on units the engine would reject, so mistakes are visible. */
  private isBadPlacement(u: LevelUnit): boolean {
    if (u.x < 0 || u.y < 0 || u.x >= this.map.width || u.y >= this.map.height) return true;
    if (!this.players.some((p) => p.id === u.owner)) return true;
    if (this.units.filter((o) => o.x === u.x && o.y === u.y).length > 1) return true;
    const def = this.content.units.tryGet(u.unit);
    if (!def) return true;
    const terrain = this.content.terrains.get(this.map.tiles[idx(this.map, u.x, u.y)]);
    return terrain.cost[def.movementClass] == null;
  }
}
