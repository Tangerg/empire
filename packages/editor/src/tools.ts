import { idx } from '@empire/battle-engine/grid';
import type { ContentCatalog } from '@empire/battle-engine/content-pack';
import type { Coord, CoverLevel, Direction, TerrainId, UnitTypeId } from '@empire/battle-engine/types';
import type { EditorDocument } from './document';

/**
 * Editing tools as strategies.
 *
 * A tool used to be a `Tool` union member whose behaviour was scattered over
 * four places: a label map, a *second* hotkey map, an `if` chain for the
 * two-phase tools, and a `switch` for the rest. Adding one meant touching all
 * four, and the two maps had already drifted — the elevation, cliff and
 * directional-cover tools advertised hotkeys in their tooltips that no key
 * handler listened for.
 *
 * One object per tool now declares its identity, its highlight, and how it
 * responds to a stroke. This mirrors the engine's action handlers, for the same
 * reason: a tool set is meant to grow.
 */

/** What the palette holds: the settings a tool paints *with*. */
export class BrushSettings {
  terrain: TerrainId;
  unitType: UnitTypeId;
  owner = 1;
  /** Square brush width in tiles; only continuous terrain-like tools use it. */
  size = 1;
  elevation = 0;
  coverSide: Direction = 'north';
  coverLevel: Exclude<CoverLevel, 'none'> = 'half';

  constructor(terrain: TerrainId, unitType: UnitTypeId) {
    this.terrain = terrain;
    this.unitType = unitType;
  }

  /** Adopts whatever is already on a tile — the eyedropper's whole behaviour. */
  sampleFrom(document: EditorDocument, at: Coord, content: ContentCatalog): void {
    const index = idx(document.map, at.x, at.y);
    this.terrain = document.map.tiles[index];
    const unit = document.units.find((candidate) => candidate.x === at.x && candidate.y === at.y);
    if (unit) {
      this.unitType = unit.unit;
      this.owner = unit.owner;
      return;
    }
    if (content.terrains.get(this.terrain).capturable) {
      this.owner = document.map.owners[index] || this.owner;
    }
  }

  /** Tiles a square brush covers, clipped to the map. */
  square(document: EditorDocument, centre: Coord): Coord[] {
    if (this.size <= 1) return [centre];
    const reach = Math.floor(this.size / 2);
    const tiles: Coord[] = [];
    for (let dy = -reach; dy <= reach; dy++) {
      for (let dx = -reach; dx <= reach; dx++) {
        const tile = { x: centre.x + dx, y: centre.y + dy };
        if (document.inBounds(tile)) tiles.push(tile);
      }
    }
    return tiles;
  }
}

export interface EditorToolContext {
  readonly document: EditorDocument;
  readonly brush: BrushSettings;
  readonly content: ContentCatalog;
}

export interface EditorTool {
  readonly id: string;
  readonly name: string;
  /** Single-character shortcut, and the only place it is declared. */
  readonly hotkey: string;
  readonly icon: string;
  /**
   * Two-phase tools anchor on press and commit on release; continuous tools
   * paint on every pointer sample. A tool is one or the other.
   */
  readonly twoPhase?: boolean;
  /** Tiles this tool would affect, for the board's brush overlay. */
  highlight(context: EditorToolContext, cursor: Coord, anchor: Coord | null): Coord[];
  /** Continuous tools do their work here; `erasing` is the secondary button. */
  paint?(context: EditorToolContext, at: Coord, erasing: boolean): void;
  /** Two-phase tools commit from anchor to release point. */
  commit?(context: EditorToolContext, anchor: Coord, at: Coord, erasing: boolean): void;
}

/** Inclusive rectangle between two corners. */
export function rectTiles(a: Coord, b: Coord): Coord[] {
  const tiles: Coord[] = [];
  for (let y = Math.min(a.y, b.y); y <= Math.max(a.y, b.y); y++) {
    for (let x = Math.min(a.x, b.x); x <= Math.max(a.x, b.x); x++) tiles.push({ x, y });
  }
  return tiles;
}

const single = (cursor: Coord): Coord[] => [cursor];

const TerrainBrushTool: EditorTool = {
  id: 'terrain',
  name: '笔刷',
  hotkey: 'b',
  icon: 'grid',
  highlight: ({ document, brush }, cursor) => brush.square(document, cursor),
  paint: ({ document, brush }, at, erasing) => {
    for (const tile of brush.square(document, at)) {
      document.setTerrain(tile, erasing ? 'plain' : brush.terrain);
    }
  },
};

const TerrainRectTool: EditorTool = {
  id: 'rect',
  name: '矩形',
  hotkey: 'r',
  icon: 'save',
  twoPhase: true,
  highlight: (_context, cursor, anchor) => (anchor ? rectTiles(anchor, cursor) : [cursor]),
  commit: ({ document, brush }, anchor, at, erasing) => {
    for (const tile of rectTiles(anchor, at)) document.setTerrain(tile, erasing ? 'plain' : brush.terrain);
  },
};

const TerrainFillTool: EditorTool = {
  id: 'fill',
  name: '填充',
  hotkey: 'f',
  icon: 'flag',
  highlight: (_context, cursor) => single(cursor),
  paint: ({ document, brush }, at, erasing) => document.floodFill(at, erasing ? 'plain' : brush.terrain),
};

const ElevationTool: EditorTool = {
  id: 'elevation',
  name: '海拔',
  hotkey: 'h',
  icon: 'grid',
  highlight: ({ document, brush }, cursor) => brush.square(document, cursor),
  paint: ({ document, brush }, at, erasing) => {
    for (const tile of brush.square(document, at)) document.setElevation(tile, erasing ? 0 : brush.elevation);
  },
};

const CliffEdgeTool: EditorTool = {
  id: 'cliff',
  name: '悬崖边',
  hotkey: 'j',
  icon: 'shield',
  twoPhase: true,
  highlight: (_context, cursor, anchor) => (anchor ? [anchor, cursor] : [cursor]),
  commit: ({ document }, anchor, at) => {
    // A cliff sits on the edge between two neighbours, so only an orthogonal
    // drag of exactly one tile describes one.
    if (Math.abs(anchor.x - at.x) + Math.abs(anchor.y - at.y) === 1) document.toggleCliff(anchor, at);
  },
};

const DirectionalCoverTool: EditorTool = {
  id: 'cover',
  name: '方向掩体',
  hotkey: 'k',
  icon: 'shield',
  highlight: (_context, cursor) => single(cursor),
  paint: ({ document, brush }, at, erasing) =>
    document.setDirectionalCover(at, brush.coverSide, erasing ? null : brush.coverLevel),
};

const PlaceUnitTool: EditorTool = {
  id: 'unit',
  name: '放置单位',
  hotkey: 'u',
  icon: 'sword',
  highlight: (_context, cursor) => single(cursor),
  paint: ({ document, brush }, at, erasing) => {
    if (erasing) document.removeUnitAt(at);
    else document.placeUnit(at, brush.unitType, brush.owner);
  },
};

const OwnerTool: EditorTool = {
  id: 'owner',
  name: '归属',
  hotkey: 'o',
  icon: 'shield',
  highlight: (_context, cursor) => single(cursor),
  paint: ({ document, brush }, at, erasing) => document.setOwner(at, erasing ? 0 : brush.owner),
};

const EraseUnitTool: EditorTool = {
  id: 'erase',
  name: '擦除单位',
  hotkey: 'e',
  icon: 'trash',
  highlight: (_context, cursor) => single(cursor),
  paint: ({ document }, at) => document.removeUnitAt(at),
};

const EyedropperTool: EditorTool = {
  id: 'pick',
  name: '吸取',
  hotkey: 'i',
  icon: 'crosshair',
  highlight: (_context, cursor) => single(cursor),
  paint: ({ document, brush, content }, at) => brush.sampleFrom(document, at, content),
};

/** Palette order is display order. */
export class EditorToolbox {
  private readonly byId = new Map<string, EditorTool>();

  constructor(readonly tools: readonly EditorTool[]) {
    for (const tool of tools) {
      if (this.byId.has(tool.id)) throw new Error(`duplicate editor tool "${tool.id}"`);
      const clash = tools.filter((candidate) => candidate.hotkey === tool.hotkey);
      if (clash.length > 1) throw new Error(`editor tools ${clash.map((t) => t.id).join(', ')} share hotkey "${tool.hotkey}"`);
      this.byId.set(tool.id, tool);
    }
  }

  get default(): EditorTool {
    return this.tools[0];
  }

  get(id: string): EditorTool | undefined {
    return this.byId.get(id);
  }

  forHotkey(key: string): EditorTool | undefined {
    return this.tools.find((tool) => tool.hotkey === key.toLowerCase());
  }
}

export const EDITOR_TOOLS = new EditorToolbox([
  TerrainBrushTool,
  TerrainRectTool,
  TerrainFillTool,
  ElevationTool,
  CliffEdgeTool,
  DirectionalCoverTool,
  PlaceUnitTool,
  OwnerTool,
  EraseUnitTool,
  EyedropperTool,
]);
