import { idx, inBounds, type ContentCatalog, type GameMap } from '@empire/battle-engine';
import { GROUND_TAGS, taggedGround, TILE, type BoardPiece } from '@empire/game-ui';

/** The terrain document, not a second art-only outline, owns irregular boundaries. */
export function candidate01PlayableShape(content: ContentCatalog, map: GameMap): { path: string; signature: string } | null {
  const outside = map.tiles.flatMap((terrain, index) => taggedGround(content.terrains.get(terrain), GROUND_TAGS.outside) ? [index] : []);
  if (!outside.length) return null;
  const empty = new Set(outside);
  const rows: string[] = [];
  const exposed = (x: number, y: number): boolean => !inBounds(map, x, y) || empty.has(idx(map, x, y));
  for (let y = 0; y < map.height; y++) {
    let start: number | null = null;
    for (let x = 0; x <= map.width; x++) {
      const filled = x < map.width && !empty.has(idx(map, x, y));
      if (filled && start === null) start = x;
      if (!filled && start !== null) {
        const left = start * TILE, right = x * TILE, top = y * TILE, bottom = (y + 1) * TILE;
        const tl = exposed(start, y - 1) ? 12 : 0, tr = exposed(x - 1, y - 1) ? 12 : 0;
        const bl = exposed(start, y + 1) ? 12 : 0, br = exposed(x - 1, y + 1) ? 12 : 0;
        rows.push(`M${left + tl} ${top}H${right - tr}Q${right} ${top} ${right} ${top + tr}`
          + `V${bottom - br}Q${right} ${bottom} ${right - br} ${bottom}`
          + `H${left + bl}Q${left} ${bottom} ${left} ${bottom - bl}`
          + `V${top + tl}Q${left} ${top} ${left + tl} ${top}Z`);
        start = null;
      }
    }
  }
  return { path: rows.join(''), signature: `${map.width}x${map.height}-${outside.join('_')}` };
}

/** Each piece keeps local definitions so the same shape survives isolated GPU baking. */
export function candidate01ClipGround(pieces: readonly BoardPiece[], shape: ReturnType<typeof candidate01PlayableShape>, prefix: string): BoardPiece[] {
  if (!shape) return [...pieces];
  return pieces.map((piece, index) => {
    const id = `c01-playable-${shape.signature}-${prefix}-${index}`;
    return { ...piece, markup: `<defs><clipPath id="${id}"><path d="${shape.path}" transform="translate(${-piece.x} ${-piece.y})"/></clipPath></defs><g clip-path="url(#${id})">${piece.markup}</g>` };
  });
}
