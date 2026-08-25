import type { ArtDirection } from './direction';
import type { ContentCatalog, GameMap, TacticalGrid, TerrainDef } from '@empire/battle-engine';
import { boardPiecesMarkup, wholeField, type BoardPiece } from './board-surface';
import { edgeLine, type BoardLayout } from './board-decorations';
import { createSceneViewport, type SceneLayers } from './scene-viewport';
import { TILE, terrainMarkup } from './terrain';

const featureColor = {
  elevationBackground: '#2a211a',
  elevationText: '#f2c76a',
  cliff: '#f0b24f',
  halfCover: '#4f9bc7',
  fullCover: '#d85c4c',
} as const;

/** Stable cache key for everything painted by the terrain and feature layers. */
export function battlefieldRenderKey(map: GameMap): string {
  const cliffs = map.cliffs
    .map(({ from, to }) => `${from.x},${from.y}>${to.x},${to.y}`)
    .join(';');
  // Whatever sides are actually written, sorted — not four fixed names, which
  // hashed nothing at all for a board whose facings have other names, so moving
  // a hex cover left the cached picture on screen.
  const cover = map.directionalCover
    .map(({ at, sides }) => {
      const written = Object.entries(sides)
        .filter(([, level]) => level)
        .map(([side, level]) => `${side}=${String(level)}`)
        .sort();
      return `${at.x},${at.y}:${written.join(',')}`;
    })
    .join(';');
  return [
    `${map.width}x${map.height}`,
    map.tiles.join(','),
    map.owners.join(','),
    map.elevation.join(','),
    cliffs,
    cover,
  ].join('|');
}

/** Everything a picture of a map needs: the art, the catalog, and the tiling. */
export interface MapCanvas {
  readonly art: ArtDirection;
  readonly content: ContentCatalog;
  readonly grid: TacticalGrid;
}

/**
 * What a map looks like, for a canvas that is not a battle.
 *
 * Three of them: the editor's canvas, a level card's thumbnail, and a terrain
 * swatch in the editor's palette. All three drew a map by asking each terrain's
 * painter for one tile. That worked while the shipped art answered with a tile; it
 * stopped the moment the campaign's ground moved into the scene, where it belongs
 * — a surface that knows its neighbours cannot be one cell's answer — and the
 * three of them went blank in three different ways.
 *
 * The scene's own three layers, because the ground alone is not what a map looks
 * like: a wood is a transition patch *and* the trees standing on it, a city wall is
 * paving *and* the wall. Pieces are placed in field coordinates, so a canvas that
 * draws the field at its own origin needs nothing from the viewport. The viewport
 * is built here because `sceneProfile` may claim a margin, and a scene asked for
 * its layers under a viewport it did not choose is a scene being lied to.
 */
export function mapScenePieces(canvas: MapCanvas, levelId: string, map: GameMap): SceneLayers {
  const { presentation } = canvas.art;
  const viewport = createSceneViewport(
    canvas.grid,
    map.width,
    map.height,
    TILE,
    presentation.sceneProfile(levelId),
  );
  return presentation.sceneLayers({ content: canvas.content, levelId, map, viewport });
}

/**
 * One cell of one terrain, as the palette shows it.
 *
 * A map of a single cell, put through the same scene as every other picture of a
 * map. It used to be `terrainMarkup` alone, which is why after the ground moved
 * into the scene the editor's palette showed nine empty squares: 平原, 道路, 森林,
 * 丘陵, 山地, 水域, 城墙 and the rest are ground, and ground is not one cell's
 * answer any more.
 *
 * The box is taller than a cell because what stands on a cell is taller than one:
 * a tree, a cliff pillar, a length of city wall. Without the extra room above, 城墙
 * and 王都石街 are both a square of paving.
 */
export function terrainSwatch(canvas: MapCanvas, terrain: TerrainDef, ownerColor?: string): string {
  const map: GameMap = {
    width: 1,
    height: 1,
    tiles: [terrain.id],
    owners: [0],
    captureProgress: [0],
    elevation: [0],
    cliffs: [],
    directionalCover: [],
  };
  const scene = mapScenePieces(canvas, SWATCH_LEVEL, map);
  const tile = terrainMarkup(canvas.art, terrain, {
    x: 0,
    y: 0,
    ownerColor,
    linked: { n: false, e: false, s: false, w: false },
  });
  return `<svg viewBox="0 -14 32 46" width="32" height="46" shape-rendering="crispEdges">`
    + `${boardPiecesMarkup(scene.ground)}${tile}`
    + `${boardPiecesMarkup([...scene.underUnits, ...scene.overUnits])}</svg>`;
}

/**
 * The level id a swatch is drawn under.
 *
 * A swatch is not a level, and a scene may key authored dressing to a level id —
 * so it gets a name no level has, rather than borrowing one and inheriting
 * somebody's hand-placed props.
 */
const SWATCH_LEVEL = 'palette-swatch';

function cliffMarkup(layout: BoardLayout, map: GameMap): string[] {
  return map.cliffs.map((cliff) =>
    edgeLine(layout, cliff.from, layout.center(cliff.to), featureColor.cliff));
}

/**
 * Shared renderer for elevation badges, cliffs and directional cover.
 *
 * Takes its canvas — the art it was handed and where the cells are — for the
 * same reason every other decoration does: this was the third tactical layer and
 * the one that stayed square. On a hex board the
 * terrain, the units, the grid lines and the move range all moved to where the
 * cells are, while the height badges, cliff marks and cover edges kept drawing
 * at `x * TILE` — the right pictures in the wrong places.
 */
export interface BattlefieldCanvas {
  readonly art: ArtDirection;
  readonly layout: BoardLayout;
}

/**
 * Height badges, cliff marks and cover props, as pictures at places.
 *
 * A badge and a cover prop belong to one cell, so each is a piece there. An edge
 * line does not: it sits on the boundary between two cells and is drawn from both
 * their centres, so the lines are one piece of field-wide line work.
 */
export function battlefieldFeaturePieces(canvas: BattlefieldCanvas, map: GameMap): BoardPiece[] {
  const { art, layout } = canvas;
  const pieces: BoardPiece[] = [];
  const badge = layout.tileSize * 0.81;
  for (let i = 0; i < map.elevation.length; i++) {
    const value = map.elevation[i];
    if (value === 0) continue;
    const cellX = i % map.width;
    const cellY = Math.floor(i / map.width);
    const sameToWest = cellX > 0 && map.elevation[i - 1] === value;
    const sameToNorth = cellY > 0 && map.elevation[i - map.width] === value;
    // One label identifies a continuous plateau; repeating it in every cell
    // obscures both the authored terrain and the units standing on it.
    if (sameToWest || sameToNorth) continue;
    pieces.push({
      markup:
        `<g class="elevation-badge">` +
        `<circle cx="${badge}" cy="7" r="5" fill="${featureColor.elevationBackground}" opacity="0.8"/>` +
        `<text x="${badge}" y="9.5" text-anchor="middle" font-size="7" fill="${featureColor.elevationText}">${value}</text>` +
        `</g>`,
      ...layout.origin({ x: cellX, y: cellY }),
    });
  }

  const lines: string[] = [...cliffMarkup(layout, map)];
  for (const cover of map.directionalCover) {
    const written = Object.entries(cover.sides).filter(([, level]) => level);
    if (written.length > 0) {
      const strongest = written.some(([, level]) => level === 'full') ? 'full' : 'half';
      const prop = art.resolve((provider) => provider.coverMarkup?.(strongest));
      // Markup with nothing in it is not a piece — the rule `wholeField` states —
      // and that holds whether the pack had no opinion or asked for no prop. This
      // discards an empty answer rather than replacing it, so nothing is lost.
      if (prop) pieces.push({ markup: prop, ...layout.origin(cover.at) });
    }
    for (const [side, level] of written) {
      lines.push(edgeLine(
        layout,
        cover.at,
        layout.neighbour(cover.at, side),
        level === 'full' ? featureColor.fullCover : featureColor.halfCover,
        0.42,
      ));
    }
  }
  pieces.push(...wholeField(lines.join('')));
  return pieces;
}
