import document from '../../assets/final-fantasy-v1/edge-decoration-v1/manifest-edge-decoration-v1.json';
import { type GameMap, idx } from '@empire/battle-engine';
import { escapeAttr, TILE, type BoardPiece } from '@empire/game-ui';
import { fixedNumbers } from './candidate-01-documents';

const root = '../../assets/final-fantasy-v1/edge-decoration-v1/';
const urls = import.meta.glob<string>(
  '../../assets/final-fantasy-v1/edge-decoration-v1/runtime/*.png',
  { eager: true, import: 'default', query: '?url' },
);

function readDecorations() {
  if (document.schemaVersion !== 1 || document.meaningfulAlphaMin !== 2) {
    throw new Error('candidate-01: unsupported authored edge decoration contract');
  }
  const assets = document.assets.map((asset) => {
    const size = fixedNumbers(2, asset.sourceSize, 'sourceSize', asset.id);
    const anchor = fixedNumbers(2, asset.sourceAnchor, 'sourceAnchor', asset.id);
    const bounds = fixedNumbers(4, asset.meaningfulSourceBounds, 'meaningfulSourceBounds', asset.id);
    const scale = asset.logicalPerSourcePixel;
    const padding = asset.sourcePadding;
    if (!Number.isFinite(scale) || scale <= 0 || !Number.isInteger(padding) || padding < 0 || padding * scale > 4
      || size.some((n) => !Number.isInteger(n) || n <= 0)
      || anchor.some((n, i) => n < 0 || n > size[i])
      || bounds[0] < 0 || bounds[1] < 0 || bounds[2] > size[0] || bounds[3] > size[1]
      || bounds[0] >= bounds[2] || bounds[1] >= bounds[3]) {
      throw new Error(`candidate-01: invalid edge registration ${asset.id}`);
    }
    const href = urls[`${root}${asset.png2x}`];
    if (!href || !urls[`${root}${asset.png}`]) throw new Error(`candidate-01: missing edge export ${asset.id}`);
    return { ...asset, sourceSize: size, sourceAnchor: anchor, meaningfulSourceBounds: bounds, href,
      logicalSize: fixedNumbers(2, size.map((n) => (n + padding * 2) * scale), 'logicalSize', asset.id),
      logicalAnchor: fixedNumbers(2, anchor.map((n) => (n + padding) * scale), 'logicalAnchor', asset.id) };
  });
  if (new Set(assets.map((a) => a.id)).size !== assets.length) throw new Error('candidate-01: duplicate edge asset');
  const scenes = document.scenes.map((scene) => {
    const mapSize = fixedNumbers(2, scene.mapSize, 'mapSize', scene.levelId);
    if (mapSize.some((n) => !Number.isInteger(n) || n <= 0)) throw new Error('candidate-01: invalid edge scene extent');
    const placements = scene.placements.map((placement) => {
      const asset = assets.find((a) => a.id === placement.assetId);
      if (!asset) throw new Error(`candidate-01: unknown edge asset ${placement.assetId}`);
      const center = fixedNumbers(2, placement.center, 'center', placement.id);
      const origin = fixedNumbers(2, placement.terrainSupport.origin, 'terrain support origin', placement.id);
      const rows = placement.terrainSupport.rows;
      const width = rows[0]?.length ?? 0;
      if (!Number.isFinite(placement.scale) || placement.scale <= 0 || placement.scale > 1
        || typeof placement.flipX !== 'boolean' || typeof placement.flipY !== 'boolean'
        || origin.some((n) => !Number.isInteger(n) || n < 0)
        || !width || !rows.length || rows.some((row) => row.length !== width || row.some((id) => !id))
        || origin[0] + width > mapSize[0] || origin[1] + rows.length > mapSize[1]) {
        throw new Error(`candidate-01: invalid edge placement ${placement.id}`);
      }
      // The authored local precondition includes the complete export plus one
      // neighbour cell, so a new route port, bridge or changed bank disables it.
      for (const axis of [0, 1]) {
        const sign = (axis === 0 ? placement.flipX : placement.flipY) ? -1 : 1;
        const ends = [0, asset.logicalSize[axis]].map((n) => center[axis]
          + (n - asset.logicalAnchor[axis]) * placement.scale * sign);
        const minimum = Math.min(...ends), maximum = Math.max(...ends);
        const supportEnd = origin[axis] + (axis === 0 ? width : rows.length);
        if (minimum < 0 || maximum > mapSize[axis] * TILE
          || origin[axis] > Math.max(0, Math.floor(minimum / TILE) - 1)
          || supportEnd < Math.min(mapSize[axis], Math.ceil(maximum / TILE) + 1)) {
          throw new Error(`candidate-01: incomplete edge terrain support ${placement.id}`);
        }
      }
      return { ...placement, center, asset, terrainSupport: { origin, rows } };
    });
    if (new Set(placements.map((p) => p.id)).size !== placements.length) throw new Error('candidate-01: duplicate edge placement');
    return { ...scene, mapSize, placements };
  });
  if (new Set(scenes.map((s) => `${s.levelId}:${s.mapSize.join('x')}`)).size !== scenes.length) throw new Error('candidate-01: duplicate edge scene');
  return { ...document, assets, scenes };
}

export const CANDIDATE_01_EDGE_DECORATIONS = readDecorations();

/** Sparse authored ground art. Local terrain assumptions are guards, never map writes. */
export function candidate01EdgeDecorationPieces(levelId: string, map: GameMap): BoardPiece[] {
  const scene = CANDIDATE_01_EDGE_DECORATIONS.scenes.find((s) => s.levelId === levelId && map.width === s.mapSize[0] && map.height === s.mapSize[1]);
  if (!scene) return [];
  return scene.placements.flatMap((placement) => {
    const { origin, rows } = placement.terrainSupport;
    if (!rows.every((row, y) => row.every((terrain, x) => map.tiles[idx(map, x + origin[0], y + origin[1])] === terrain))) return [];
    const { asset, scale, flipX, flipY, center } = placement;
    const markup = `<g class="candidate-ground-edge" data-edge-decoration="${escapeAttr(asset.id)}"`
      + ` transform="scale(${scale * (flipX ? -1 : 1)} ${scale * (flipY ? -1 : 1)})">`
      + `<image href="${escapeAttr(asset.href)}" x="${-asset.logicalAnchor[0]}" y="${-asset.logicalAnchor[1]}"`
      + ` width="${asset.logicalSize[0]}" height="${asset.logicalSize[1]}" preserveAspectRatio="none"/></g>`;
    return [{ x: center[0], y: center[1], markup }];
  });
}
