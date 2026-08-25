import type { ArtDirection } from './direction';
import type { ContentCatalog, GameMap, TacticalGrid } from '@empire/battle-engine';
import { wholeField, type BoardPiece } from './board-surface';
import { edgeLine, type BoardLayout } from './board-decorations';
import { createSceneViewport } from './scene-viewport';
import { TILE } from './terrain';

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

/**
 * The ground a map is painted on, for a canvas that is not a battle.
 *
 * The editor's canvas and a level card's thumbnail are pictures of a map, and they
 * drew the ground by asking each terrain's painter for a tile. That worked while
 * the shipped art answered with a tile; it stopped the moment the campaign's
 * ground moved into the scene, where it belongs — a surface that knows its
 * neighbours cannot be one cell's answer.
 *
 * Ground pieces are placed in field coordinates, so a canvas that draws the field
 * at its own origin needs nothing from the viewport. It is built here because
 * `sceneProfile` may claim a margin, and a scene asked for its layers under a
 * viewport it did not choose is a scene being lied to.
 */
export function mapGroundPieces(
  canvas: { readonly art: ArtDirection; readonly content: ContentCatalog; readonly grid: TacticalGrid },
  levelId: string,
  map: GameMap,
): readonly BoardPiece[] {
  const { presentation } = canvas.art;
  const viewport = createSceneViewport(
    canvas.grid,
    map.width,
    map.height,
    TILE,
    presentation.sceneProfile(levelId),
  );
  return presentation.sceneLayers({ content: canvas.content, levelId, map, viewport }).ground;
}

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
