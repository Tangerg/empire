import { idx } from '@empire/battle-engine/grid';
import type { ContentCatalog } from '@empire/battle-engine';
import type { Coord, GameState, Unit, WeaponId } from '@empire/battle-engine/types';
import { PAL } from '../art/palette';
import { FrameAnimationSystem, registerSvgStrip } from '../art/frame-animation';
import { battlePresentation, decorationsFor, type BattlePresentation } from '../art/battle-presentation';
import type { BoardDecorations } from '../art/board-decorations';
import { battlefieldFeatureMarkup, battlefieldRenderKey } from '../art/battlefield-layer';
import { TILE, terrainLayerMarkup } from '../art/terrain';
import { createSceneViewport, scenePointToCell, type SceneViewport } from '../art/scene-viewport';
import { unitSpriteMarkup } from '../art/units';
import { clear, fromMarkup, setAttrs, svg } from '../art/svg';

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

export interface BoardHandlers {
  onTileClick(at: Coord, event: PointerEvent): void;
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
 * The board. Owns the SVG tree and all animation; knows nothing about rules or
 * selection state beyond the overlay it is handed.
 */
export class BoardView {
  readonly el: SVGSVGElement;
  private readonly layers: Record<string, SVGGElement>;
  private readonly viewport: SceneViewport;
  private readonly presentation: BattlePresentation;
  private readonly decor: BoardDecorations;
  private readonly unitEls = new Map<number, SVGGElement>();
  private readonly frameAnimations = new FrameAnimationSystem();
  private sceneryAnimationIds: string[] = [];
  private zoom = 1;
  private mapSignature = '';
  private objectSignature = '';
  private hovered: string | null = null;
  private effectSerial = 0;

  constructor(
    private state: GameState,
    private readonly handlers: BoardHandlers,
    private readonly content: ContentCatalog,
  ) {
    this.presentation = battlePresentation(state.levelId);
    this.decor = decorationsFor(this.presentation);
    this.viewport = createSceneViewport(
      state.map.width,
      state.map.height,
      TILE,
      this.presentation.sceneProfile(state.levelId),
    );
    const presentationClass = this.presentation.boardClass ? ` ${this.presentation.boardClass}` : '';
    this.el = svg('svg', {
      viewBox: `0 0 ${this.viewport.sceneWidth} ${this.viewport.sceneHeight}`,
      class: `board${presentationClass}`,
      'shape-rendering': this.decor.shapeRendering,
      'text-rendering': 'optimizeLegibility',
      'data-scene-layout': this.viewport.originX || this.viewport.originY ? 'authored' : 'grid',
    });

    const sceneFrame = this.presentation.sceneFrame(state.levelId, state.map, this.viewport);
    if (sceneFrame.backdrop) this.el.append(fromMarkup(sceneFrame.backdrop));
    const world = svg('g', {
      class: 'board-world',
      transform: `translate(${this.viewport.originX} ${this.viewport.originY})`,
    });
    // DOM order is the depth contract. In particular, ground/roads must stay
    // below every actor even when a campaign replaces all tactical artwork.
    const names = [
      'ground',
      'terrain',
      'scenery',
      'spatial',
      'grid',
      'range',
      'path',
      'structures',
      'markers',
      'units',
      'foreground',
      'effects',
      'cursor',
    ];
    this.layers = {};
    for (const n of names) {
      const g = svg('g', { class: `layer layer-${n}` });
      this.layers[n] = g;
      world.append(g);
    }
    this.el.append(world);
    if (sceneFrame.foreground) this.el.append(fromMarkup(sceneFrame.foreground));
    this.buildGrid();
    this.bindPointer();
    this.setZoom(1.25);
  }

  /* ------------------------------------------------------------------ setup */

  private buildGrid(): void {
    const markup = this.decor.gridLines(this.state.map);
    if (markup) this.layers.grid.append(fromMarkup(markup));
  }

  private bindPointer(): void {
    const toCoord = (ev: PointerEvent | MouseEvent): Coord | null => {
      const rect = this.el.getBoundingClientRect();
      if (rect.width <= 0 || rect.height <= 0) return null;
      const scale = Math.min(rect.width / this.viewport.sceneWidth, rect.height / this.viewport.sceneHeight);
      const contentWidth = this.viewport.sceneWidth * scale;
      const contentHeight = this.viewport.sceneHeight * scale;
      const sceneX = (ev.clientX - rect.left - (rect.width - contentWidth) / 2) / scale;
      const sceneY = (ev.clientY - rect.top - (rect.height - contentHeight) / 2) / scale;
      return scenePointToCell(this.viewport, sceneX, sceneY);
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
    this.el.style.width = `${this.viewport.sceneWidth * this.zoom}px`;
    this.el.style.height = `${this.viewport.sceneHeight * this.zoom}px`;
  }

  /** Fits the whole tactical field into a viewport while preserving pixel scale. */
  fitWithin(width: number, height: number, padding = 32): void {
    const availableWidth = width - padding;
    const availableHeight = height - padding;
    if (availableWidth <= 0 || availableHeight <= 0) return;
    const horizontal = availableWidth / this.viewport.sceneWidth;
    const vertical = availableHeight / this.viewport.sceneHeight;
    this.setZoom(Math.min(1.25, horizontal, vertical));
  }

  get zoomLevel(): number {
    return this.zoom;
  }

  /* ----------------------------------------------------------------- render */

  setState(state: GameState): void {
    this.state = state;
  }

  dispose(): void {
    this.frameAnimations.dispose();
  }

  /** Terrain only changes when ownership does, so it is cheap to diff by hash. */
  syncTerrain(): void {
    const s = this.state;
    const signature = battlefieldRenderKey(s.map);
    if (signature === this.mapSignature) return;
    this.mapSignature = signature;
    clear(this.layers.terrain);
    const colorOf = (id: number) => s.players.find((p) => p.id === id)?.color;
    this.layers.terrain.append(fromMarkup(terrainLayerMarkup(this.content, s.map, colorOf, s.levelId)));
    for (const id of this.sceneryAnimationIds) this.frameAnimations.unregister(id);
    this.sceneryAnimationIds = [];
    clear(this.layers.ground);
    clear(this.layers.scenery);
    clear(this.layers.foreground);
    const sceneLayers = this.presentation.sceneLayers(s.levelId, s.map);
    if (sceneLayers.ground) this.layers.ground.append(fromMarkup(sceneLayers.ground));
    if (sceneLayers.underUnits) this.layers.scenery.append(fromMarkup(sceneLayers.underUnits));
    if (sceneLayers.overUnits) this.layers.foreground.append(fromMarkup(sceneLayers.overUnits));
    this.sceneryAnimationIds = [
      ...this.playEmbeddedAnimations(this.layers.ground),
      ...this.playEmbeddedAnimations(this.layers.scenery),
      ...this.playEmbeddedAnimations(this.layers.foreground),
    ];
    clear(this.layers.spatial);
    const spatial = battlefieldFeatureMarkup(s.map);
    if (spatial) this.layers.spatial.append(fromMarkup(spatial));
  }

  render(overlay: BoardOverlay): void {
    this.el.classList.toggle(
      'is-tactical',
      !!overlay.selected || overlay.move.size > 0 || overlay.attack.size > 0 || overlay.heal.size > 0 || overlay.path.length > 0,
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

    clear(this.layers.structures);
    const structures = s.structures.flatMap((state) => {
      const ownerColor = s.players.find((player) => player.id === state.owner)?.color;
      const markup = this.presentation.structure(state, this.content.structures.get(state.type), ownerColor);
      return markup
        ? [`<g transform="translate(${state.x * TILE} ${state.y * TILE})" data-structure="${state.id}">${markup}</g>`]
        : [];
    });
    if (structures.length) this.layers.structures.append(fromMarkup(structures.join('')));

    clear(this.layers.markers);
    const markers = s.markers.map((marker) =>
      `<g transform="translate(${marker.at.x * TILE} ${marker.at.y * TILE})" data-marker="${marker.id}">${this.presentation.marker(marker)}</g>`,
    );
    if (markers.length) this.layers.markers.append(fromMarkup(markers.join('')));
  }

  private renderRanges(o: BoardOverlay): void {
    const s = this.state;
    const parts: string[] = [];
    const cell = (i: number, fill: string, opacity: number, stroke?: string) => {
      parts.push(this.decor.actionSpot({
        x: i % s.map.width,
        y: Math.floor(i / s.map.width),
        fill,
        opacity,
        stroke,
      }));
    };
    for (const i of o.threat) cell(i, '#ff3b30', 0.1);
    // Held ground is drawn under the move range: the player needs to see why a
    // path stops short, not merely that it does.
    for (const i of o.controlled) cell(i, '#8c6bd8', 0.18, '#b9a3f0');
    // A marked tile is committed damage, so it reads stronger than mere threat
    // and carries its own countdown.
    for (const [i, remaining] of o.incoming) {
      cell(i, '#ffb020', 0.3, '#ffd479');
      const x = (i % s.map.width) * TILE;
      const y = Math.floor(i / s.map.width) * TILE;
      parts.push(`<text class="incoming-count" x="${x + TILE / 2}" y="${y + TILE / 2 + 4}"
        text-anchor="middle" font-size="11" font-weight="700" fill="#2a1a00">${remaining}</text>`);
    }
    for (const i of o.move) if (!o.attack.has(i)) cell(i, '#3f9fff', 0.22);
    for (const i of o.heal) cell(i, '#5fd07a', 0.28, '#8ef7a5');
    for (const i of o.attack) cell(i, '#ff4436', 0.16, '#ff7468');

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
    const d = this.decor.movePath(path);
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

  private unitElement(unit: Unit): SVGGElement {
    let el = this.unitEls.get(unit.id);
    if (el) return el;
    const color = this.state.players.find((p) => p.id === unit.owner)?.color ?? PAL.neutral;
    el = svg('g', { class: 'unit', 'data-unit': unit.id });
    el.append(fromMarkup(unitSpriteMarkup(unit.type, color)));
    const badges = svg('g', { class: 'badges' });
    el.append(badges);
    this.layers.units.append(el);
    this.unitEls.set(unit.id, el);
    const strip = el.querySelector('.runtime-frame-strip') as SVGImageElement | null;
    if (strip) {
      const animationId = this.unitAnimationId(unit.id);
      registerSvgStrip(this.frameAnimations, animationId, strip);
      this.frameAnimations.play(animationId, 'idle');
    }
    return el;
  }

  private renderUnits(o: BoardOverlay): void {
    const s = this.state;
    const alive = new Set(s.units.map((u) => u.id));
    for (const [id, el] of this.unitEls) {
      if (!alive.has(id)) {
        this.frameAnimations.unregister(this.unitAnimationId(id));
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
      const def = this.content.units.get(u.type);
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
    if (o.selected) parts.push(this.decor.ring(o.selected, 'selection'));
    if (o.cursor) parts.push(this.decor.ring(o.cursor, 'cursor'));
    if (parts.length) this.layers.cursor.append(fromMarkup(parts.join('')));
  }

  /* -------------------------------------------------------------- animation */

  async animateMove(unit: Unit, path: Coord[], msPerTile = 85): Promise<void> {
    const el = this.unitEls.get(unit.id);
    if (!el || path.length < 2) return;
    const animationId = this.unitAnimationId(unit.id);
    if (this.frameAnimations.has(animationId)) this.frameAnimations.play(animationId, 'walk');
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
    if (this.frameAnimations.has(animationId)) this.frameAnimations.play(animationId, 'idle');
  }

  async animateStrike(attacker: Unit, target: Coord): Promise<void> {
    const el = this.unitEls.get(attacker.id);
    if (!el) return;
    const dx = Math.sign(target.x - attacker.x);
    const dy = Math.sign(target.y - attacker.y);
    if (dx > 0) el.classList.remove('face-left');
    if (dx < 0) el.classList.add('face-left');
    const base = `translate(${attacker.x * TILE},${attacker.y * TILE})`;
    const animationId = this.unitAnimationId(attacker.id);
    if (this.frameAnimations.has(animationId)) this.frameAnimations.play(animationId, 'attack');
    el.classList.add('is-attacking');
    await tween(150, (t) => {
      const push = Math.sin(Math.PI * t) * 7;
      setAttrs(el, {
        transform: `translate(${attacker.x * TILE + dx * push},${attacker.y * TILE + dy * push})`,
      });
    });
    el.classList.remove('is-attacking');
    if (this.frameAnimations.has(animationId)) this.frameAnimations.play(animationId, 'idle');
    setAttrs(el, { transform: base });
  }

  async animateHit(at: Coord, damage: number, killed: boolean, weapon?: WeaponId): Promise<void> {
    const cx = at.x * TILE + TILE / 2;
    const cy = at.y * TILE + TILE / 2;
    const g = svg('g', { class: 'fx' });
    g.append(
      fromMarkup(
        `${weapon && this.presentation.weaponFx(weapon) ? this.presentation.effect(this.presentation.weaponFx(weapon)!, cx, cy) : ''}
         <circle cx="${cx}" cy="${cy}" r="12" fill="#ffffff" opacity="0.65"/>
         <text x="${cx}" y="${cy - 8}" text-anchor="middle" class="fx-damage">-${damage}</text>`,
      ),
    );
    this.layers.effects.append(g);
    const animationIds = this.playEmbeddedAnimations(g);
    const text = g.querySelector('text') as SVGTextElement;
    const circle = g.querySelector('circle') as SVGCircleElement;
    await tween(420, (t) => {
      setAttrs(circle, { r: `${(12 + t * 10).toFixed(1)}`, opacity: `${(0.8 * (1 - t)).toFixed(2)}` });
      setAttrs(text, { transform: `translate(0,${(-14 * t).toFixed(1)})`, opacity: `${(1 - t ** 2).toFixed(2)}` });
    });
    for (const id of animationIds) this.frameAnimations.unregister(id);
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
    this.frameAnimations.unregister(this.unitAnimationId(unitId));
    el.remove();
    this.unitEls.delete(unitId);
  }

  async animateHeal(at: Coord, amount: number): Promise<void> {
    const cx = at.x * TILE + TILE / 2;
    const cy = at.y * TILE + TILE / 2;
    const g = svg('g', { class: 'fx' });
    g.append(
      fromMarkup(
        `${this.presentation.healFx ? this.presentation.effect(this.presentation.healFx, cx, cy) : ''}
         <text x="${cx}" y="${cy - 6}" text-anchor="middle" class="fx-heal">+${amount}</text>`,
      ),
    );
    this.layers.effects.append(g);
    const animationIds = this.playEmbeddedAnimations(g);
    const text = g.querySelector('text') as SVGTextElement;
    await tween(500, (t) => {
      setAttrs(text, { transform: `translate(0,${(-16 * t).toFixed(1)})`, opacity: `${(1 - t ** 2).toFixed(2)}` });
    });
    for (const id of animationIds) this.frameAnimations.unregister(id);
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

  centerOn(at: Coord, container: HTMLElement): void {
    const px = (this.viewport.originX + (at.x + 0.5) * TILE) * this.zoom;
    const py = (this.viewport.originY + (at.y + 0.5) * TILE) * this.zoom;
    container.scrollTo({
      left: px - container.clientWidth / 2,
      top: py - container.clientHeight / 2,
      behavior: 'smooth',
    });
  }

  private unitAnimationId(unitId: number): string {
    return `unit:${unitId}`;
  }

  private playEmbeddedAnimations(root: Element): string[] {
    const ids: string[] = [];
    for (const strip of root.querySelectorAll<SVGImageElement>('.runtime-frame-strip')) {
      const id = `effect:${this.effectSerial++}`;
      registerSvgStrip(this.frameAnimations, id, strip);
      const clips = JSON.parse(strip.getAttribute('data-frame-clips') ?? '[]') as Array<{ id?: string }>;
      const clip = clips[0]?.id;
      if (clip) this.frameAnimations.play(id, clip);
      ids.push(id);
    }
    return ids;
  }
}
