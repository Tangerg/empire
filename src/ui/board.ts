import { unitDef } from '../core/data/units';
import { idx } from '../core/grid';
import type { Coord, GameState, Unit } from '../core/types';
import { PAL } from '../art/palette';
import { battlefieldFeatureMarkup, battlefieldRenderKey } from '../art/battlefield-layer';
import { TILE, terrainLayerMarkup } from '../art/terrain';
import { unitSpriteMarkup } from '../art/units';
import { clear, fromMarkup, setAttrs, svg } from '../art/svg';

export interface BoardOverlay {
  move: Set<number>;
  attack: Set<number>;
  heal: Set<number>;
  threat: Set<number>;
  path: Coord[];
  selected: Coord | null;
  cursor: Coord | null;
  /** null = no fog. */
  visible: Set<number> | null;
  hiddenUnits: Set<number>;
}

export const emptyOverlay = (): BoardOverlay => ({
  move: new Set(),
  attack: new Set(),
  heal: new Set(),
  threat: new Set(),
  path: [],
  selected: null,
  cursor: null,
  visible: null,
  hiddenUnits: new Set(),
});

export interface BoardHandlers {
  onTileClick(c: Coord, ev: PointerEvent): void;
  onTileEnter(c: Coord): void;
  onLeave(): void;
  onSecondary(c: Coord): void;
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
 * The board. Owns the SVG tree and all animation; knows nothing about rules or
 * selection state beyond the overlay it is handed.
 */
export class BoardView {
  readonly el: SVGSVGElement;
  private readonly layers: Record<string, SVGGElement>;
  private readonly unitEls = new Map<number, SVGGElement>();
  private zoom = 1;
  private mapSignature = '';
  private hovered: string | null = null;

  constructor(
    private state: GameState,
    private readonly handlers: BoardHandlers,
  ) {
    const w = state.map.width * TILE;
    const h = state.map.height * TILE;
    this.el = svg('svg', {
      viewBox: `0 0 ${w} ${h}`,
      class: 'board',
      'shape-rendering': 'crispEdges',
      'text-rendering': 'optimizeLegibility',
    });

    const names = ['terrain', 'spatial', 'grid', 'range', 'path', 'units', 'effects', 'cursor'];
    this.layers = {};
    for (const n of names) {
      const g = svg('g', { class: `layer layer-${n}` });
      this.layers[n] = g;
      this.el.append(g);
    }
    this.buildGrid();
    this.bindPointer();
    this.setZoom(1.25);
  }

  /* ------------------------------------------------------------------ setup */

  private buildGrid(): void {
    const { width, height } = this.state.map;
    const parts: string[] = [];
    for (let x = 1; x < width; x++) {
      parts.push(
        `<line x1="${x * TILE}" y1="0" x2="${x * TILE}" y2="${height * TILE}" stroke="${PAL.ink}" stroke-width="0.4" opacity="0.12"/>`,
      );
    }
    for (let y = 1; y < height; y++) {
      parts.push(
        `<line x1="0" y1="${y * TILE}" x2="${width * TILE}" y2="${y * TILE}" stroke="${PAL.ink}" stroke-width="0.4" opacity="0.12"/>`,
      );
    }
    this.layers.grid.append(fromMarkup(parts.join('')));
  }

  private bindPointer(): void {
    const toCoord = (ev: PointerEvent | MouseEvent): Coord | null => {
      const rect = this.el.getBoundingClientRect();
      const scale = rect.width / (this.state.map.width * TILE);
      const x = Math.floor((ev.clientX - rect.left) / scale / TILE);
      const y = Math.floor((ev.clientY - rect.top) / scale / TILE);
      if (x < 0 || y < 0 || x >= this.state.map.width || y >= this.state.map.height) return null;
      return { x, y };
    };

    this.el.addEventListener('pointerdown', (ev) => {
      const c = toCoord(ev);
      if (!c) return;
      if (ev.button === 2) {
        this.handlers.onSecondary(c);
        return;
      }
      if (ev.button === 0) this.handlers.onTileClick(c, ev);
    });
    this.el.addEventListener('contextmenu', (ev) => ev.preventDefault());
    this.el.addEventListener('pointermove', (ev) => {
      const c = toCoord(ev);
      if (!c) {
        if (this.hovered) {
          this.hovered = null;
          this.handlers.onLeave();
        }
        return;
      }
      const key = `${c.x},${c.y}`;
      if (key === this.hovered) return;
      this.hovered = key;
      this.handlers.onTileEnter(c);
    });
    this.el.addEventListener('pointerleave', () => {
      this.hovered = null;
      this.handlers.onLeave();
    });
  }

  setZoom(z: number): void {
    this.zoom = Math.max(0.5, Math.min(2.2, z));
    this.el.style.width = `${this.state.map.width * TILE * this.zoom}px`;
    this.el.style.height = `${this.state.map.height * TILE * this.zoom}px`;
  }

  get zoomLevel(): number {
    return this.zoom;
  }

  /* ----------------------------------------------------------------- render */

  setState(state: GameState): void {
    this.state = state;
  }

  /** Terrain only changes when ownership does, so it is cheap to diff by hash. */
  syncTerrain(): void {
    const s = this.state;
    const signature = battlefieldRenderKey(s.map);
    if (signature === this.mapSignature) return;
    this.mapSignature = signature;
    clear(this.layers.terrain);
    const colorOf = (id: number) => s.players.find((p) => p.id === id)?.color;
    this.layers.terrain.append(fromMarkup(terrainLayerMarkup(s.map, colorOf)));
    clear(this.layers.spatial);
    const spatial = battlefieldFeatureMarkup(s.map);
    if (spatial) this.layers.spatial.append(fromMarkup(spatial));
  }

  render(overlay: BoardOverlay): void {
    this.syncTerrain();
    this.renderRanges(overlay);
    this.renderPath(overlay.path);
    this.renderUnits(overlay);
    this.renderCursor(overlay);
  }

  private renderRanges(o: BoardOverlay): void {
    const s = this.state;
    const parts: string[] = [];
    const cell = (i: number, fill: string, opacity: number, stroke?: string) => {
      const x = (i % s.map.width) * TILE;
      const y = Math.floor(i / s.map.width) * TILE;
      parts.push(
        `<rect x="${x}" y="${y}" width="${TILE}" height="${TILE}" fill="${fill}" opacity="${opacity}"${
          stroke ? ` stroke="${stroke}" stroke-width="1"` : ''
        }/>`,
      );
    };
    for (const i of o.threat) cell(i, '#ff3b30', 0.16);
    for (const i of o.move) if (!o.attack.has(i)) cell(i, '#3f9fff', 0.3);
    for (const i of o.heal) cell(i, '#5fd07a', 0.4);
    for (const i of o.attack) cell(i, '#ff4436', 0.42);

    if (o.visible) {
      for (let i = 0; i < s.map.tiles.length; i++) {
        if (!o.visible.has(i)) cell(i, '#0b1020', 0.55);
      }
    }
    clear(this.layers.range);
    if (parts.length) this.layers.range.append(fromMarkup(parts.join('')));
  }

  private renderPath(path: Coord[]): void {
    clear(this.layers.path);
    if (path.length < 2) return;
    const d = path
      .map((c, i) => `${i === 0 ? 'M' : 'L'}${c.x * TILE + TILE / 2} ${c.y * TILE + TILE / 2}`)
      .join(' ');
    const last = path[path.length - 1];
    const prev = path[path.length - 2];
    const angle = (Math.atan2(last.y - prev.y, last.x - prev.x) * 180) / Math.PI;
    this.layers.path.append(
      fromMarkup(
        `<path d="${d}" fill="none" stroke="#ffffff" stroke-width="5" stroke-linejoin="round" stroke-linecap="round" opacity="0.85"/>
         <path d="${d}" fill="none" stroke="#2f6fd0" stroke-width="2.6" stroke-linejoin="round" stroke-linecap="round"/>
         <g transform="translate(${last.x * TILE + TILE / 2} ${last.y * TILE + TILE / 2}) rotate(${angle})">
           <path d="M-4 -5 4 0 -4 5z" fill="#2f6fd0" stroke="#ffffff" stroke-width="1.2"/>
         </g>`,
      ),
    );
  }

  private unitElement(u: Unit): SVGGElement {
    let el = this.unitEls.get(u.id);
    if (el) return el;
    const color = this.state.players.find((p) => p.id === u.owner)?.color ?? PAL.neutral;
    el = svg('g', { class: 'unit', 'data-unit': u.id });
    el.append(fromMarkup(unitSpriteMarkup(u.type, color)));
    const badges = svg('g', { class: 'badges' });
    el.append(badges);
    this.layers.units.append(el);
    this.unitEls.set(u.id, el);
    return el;
  }

  private renderUnits(o: BoardOverlay): void {
    const s = this.state;
    const alive = new Set(s.units.map((u) => u.id));
    for (const [id, el] of this.unitEls) {
      if (!alive.has(id)) {
        el.remove();
        this.unitEls.delete(id);
      }
    }

    for (const u of s.units) {
      const el = this.unitElement(u);
      if (!el.classList.contains('is-moving')) el.classList.toggle('face-left', u.facing === 'west');
      const hidden = o.hiddenUnits.has(u.id);
      el.style.display = hidden ? 'none' : '';
      if (hidden) continue;
      setAttrs(el, { transform: `translate(${u.x * TILE},${u.y * TILE})` });
      el.classList.toggle('is-done', u.done);
      el.classList.toggle('is-selected', !!o.selected && o.selected.x === u.x && o.selected.y === u.y);

      const badges = el.querySelector('.badges')!;
      clear(badges);
      const def = unitDef(u.type);
      const ratio = u.hp / def.maxHp;
      const parts: string[] = [];
      const arrow = ({ north: '↑', east: '→', south: '↓', west: '←' } as const)[u.facing];
      parts.push(`<circle cx="5" cy="6" r="4.8" fill="${PAL.ink}" opacity="0.78"/><text x="5" y="8.8" text-anchor="middle" font-size="7.5" fill="#fff">${arrow}</text>`);
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
      if (parts.length) badges.append(fromMarkup(parts.join('')));
    }
  }

  private renderCursor(o: BoardOverlay): void {
    clear(this.layers.cursor);
    const parts: string[] = [];
    if (o.selected) {
      parts.push(
        `<rect x="${o.selected.x * TILE + 1}" y="${o.selected.y * TILE + 1}" width="${TILE - 2}" height="${TILE - 2}"
          fill="none" stroke="#ffffff" stroke-width="2" rx="3" opacity="0.95"/>`,
      );
    }
    if (o.cursor) {
      parts.push(
        `<rect x="${o.cursor.x * TILE + 0.5}" y="${o.cursor.y * TILE + 0.5}" width="${TILE - 1}" height="${TILE - 1}"
          fill="none" stroke="${PAL.gold}" stroke-width="1.6" rx="2"/>`,
      );
    }
    if (parts.length) this.layers.cursor.append(fromMarkup(parts.join('')));
  }

  /* -------------------------------------------------------------- animation */

  async animateMove(unit: Unit, path: Coord[], msPerTile = 85): Promise<void> {
    const el = this.unitEls.get(unit.id);
    if (!el || path.length < 2) return;
    el.classList.add('is-moving');
    for (let i = 1; i < path.length; i++) {
      const a = path[i - 1];
      const b = path[i];
      if (b.x > a.x) el.classList.remove('face-left');
      if (b.x < a.x) el.classList.add('face-left');
      await tween(msPerTile, (t) => {
        const e = easeInOut(t);
        const x = (a.x + (b.x - a.x) * e) * TILE;
        const y = (a.y + (b.y - a.y) * e) * TILE - Math.sin(Math.PI * t) * 2.5;
        setAttrs(el, { transform: `translate(${x.toFixed(2)},${y.toFixed(2)})` });
      });
    }
    el.classList.remove('is-moving');
  }

  async animateStrike(attacker: Unit, target: Coord): Promise<void> {
    const el = this.unitEls.get(attacker.id);
    if (!el) return;
    const dx = Math.sign(target.x - attacker.x);
    const dy = Math.sign(target.y - attacker.y);
    if (dx > 0) el.classList.remove('face-left');
    if (dx < 0) el.classList.add('face-left');
    const base = `translate(${attacker.x * TILE},${attacker.y * TILE})`;
    await tween(150, (t) => {
      const push = Math.sin(Math.PI * t) * 7;
      setAttrs(el, {
        transform: `translate(${attacker.x * TILE + dx * push},${attacker.y * TILE + dy * push})`,
      });
    });
    setAttrs(el, { transform: base });
  }

  async animateHit(at: Coord, damage: number, killed: boolean): Promise<void> {
    const cx = at.x * TILE + TILE / 2;
    const cy = at.y * TILE + TILE / 2;
    const g = svg('g', { class: 'fx' });
    g.append(
      fromMarkup(
        `<circle cx="${cx}" cy="${cy}" r="12" fill="#ffffff" opacity="0.85"/>
         <text x="${cx}" y="${cy - 8}" text-anchor="middle" class="fx-damage">-${damage}</text>`,
      ),
    );
    this.layers.effects.append(g);
    const text = g.querySelector('text') as SVGTextElement;
    const circle = g.querySelector('circle') as SVGCircleElement;
    await tween(420, (t) => {
      setAttrs(circle, { r: `${(12 + t * 10).toFixed(1)}`, opacity: `${(0.8 * (1 - t)).toFixed(2)}` });
      setAttrs(text, { transform: `translate(0,${(-14 * t).toFixed(1)})`, opacity: `${(1 - t ** 2).toFixed(2)}` });
    });
    g.remove();
    if (killed) await wait(40);
  }

  async animateDeath(unitId: number): Promise<void> {
    const el = this.unitEls.get(unitId);
    if (!el) return;
    const transform = el.getAttribute('transform') ?? '';
    await tween(220, (t) => {
      el.style.opacity = String(1 - t);
      setAttrs(el, { transform: `${transform} translate(16,16) scale(${1 - 0.35 * t}) translate(-16,-16)` });
    });
    el.remove();
    this.unitEls.delete(unitId);
  }

  async animateHeal(at: Coord, amount: number): Promise<void> {
    const cx = at.x * TILE + TILE / 2;
    const cy = at.y * TILE + TILE / 2;
    const g = svg('g', { class: 'fx' });
    g.append(
      fromMarkup(
        `<text x="${cx}" y="${cy - 6}" text-anchor="middle" class="fx-heal">+${amount}</text>`,
      ),
    );
    this.layers.effects.append(g);
    const text = g.querySelector('text') as SVGTextElement;
    await tween(500, (t) => {
      setAttrs(text, { transform: `translate(0,${(-16 * t).toFixed(1)})`, opacity: `${(1 - t ** 2).toFixed(2)}` });
    });
    g.remove();
  }

  async animateSpawn(unitId: number): Promise<void> {
    const el = this.unitEls.get(unitId);
    if (!el) return;
    const transform = el.getAttribute('transform') ?? '';
    await tween(240, (t) => {
      el.style.opacity = String(t);
      setAttrs(el, {
        transform: `${transform} translate(16,16) scale(${0.6 + 0.4 * t}) translate(-16,-16)`,
      });
    });
    el.style.opacity = '1';
    setAttrs(el, { transform });
  }

  /** Banner that sweeps across the board on turn change. */
  async announce(text: string, color: string): Promise<void> {
    const w = this.state.map.width * TILE;
    const h = this.state.map.height * TILE;
    const g = svg('g', { class: 'fx' });
    g.append(
      fromMarkup(
        `<rect x="0" y="${h / 2 - 16}" width="${w}" height="32" fill="${color}" opacity="0.9"/>
         <text x="${w / 2}" y="${h / 2 + 7}" text-anchor="middle" class="fx-banner">${text}</text>`,
      ),
    );
    this.layers.effects.append(g);
    const rect = g.querySelector('rect') as SVGRectElement;
    await tween(180, (t) => setAttrs(rect, { x: `${(-w * (1 - t)).toFixed(0)}` }));
    await wait(420);
    await tween(200, (t) => {
      g.style.opacity = String(1 - t);
    });
    g.remove();
  }

  centerOn(c: Coord, container: HTMLElement): void {
    const px = (c.x + 0.5) * TILE * this.zoom;
    const py = (c.y + 0.5) * TILE * this.zoom;
    container.scrollTo({
      left: px - container.clientWidth / 2,
      top: py - container.clientHeight / 2,
      behavior: 'smooth',
    });
  }
}
