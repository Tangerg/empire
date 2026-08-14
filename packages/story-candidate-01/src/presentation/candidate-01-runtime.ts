import type {
  BattlefieldMarker,
  StructureDef,
  StructureState,
  TerrainId,
  UnitTypeId,
  WeaponId,
} from '@empire/battle-engine/types';
import {
  candidate01Asset,
  candidate01AssetUrl,
  candidate01TryAsset,
  type Candidate01RuntimeAsset,
} from './candidate-01-assets';
import {
  CANDIDATE_01_MAP_STRUCTURE_ART,
  CANDIDATE_01_STRUCTURE_ART,
  CANDIDATE_01_TERRAIN_ART,
  CANDIDATE_01_UNIT_ART,
  CANDIDATE_01_WEAPON_FX,
} from './candidate-01-bindings';
import {
  runtimeAtlasCellMarkup,
  runtimeFrameStripMarkup,
  runtimeUnitMarkup,
  type RuntimeCellAtlas,
  type RuntimeUnitSheet,
} from '@empire/game-ui';

const attr = (value: string): string =>
  value.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;');

function unitSheet(record: Candidate01RuntimeAsset): RuntimeUnitSheet {
  const frameWidth = record.frameWidth;
  const frameHeight = record.frameHeight;
  const frameCount = record.frames;
  if (!frameWidth || !frameHeight || !frameCount || !record.anchor) {
    throw new Error(`candidate-01 unit metadata is incomplete: ${record.topicId}`);
  }
  const frameIndex = (role: string, fallback: number): number => {
    const index = record.frameOrder?.indexOf(role) ?? -1;
    return index >= 0 ? index : fallback;
  };
  return {
    href: record.url,
    frameWidth,
    frameHeight,
    frameCount,
    anchor: { x: record.anchor[0], y: record.anchor[1] },
    idleFrame: frameIndex('idle', 0),
    walkFrames: [frameIndex('walk-a', Math.min(1, frameCount - 1)), frameIndex('walk-b', frameCount - 1)],
    attackFrame: frameIndex('attack', Math.min(2, frameCount - 1)),
  };
}

function cellAtlas(record: Candidate01RuntimeAsset): RuntimeCellAtlas {
  const cellWidth = record.tileWidth ?? record.frameWidth;
  const cellHeight = record.tileHeight ?? record.frameHeight;
  const cells = record.variants ?? record.connectionMasks ?? record.frames;
  if (!cellWidth || !cellHeight || !cells) {
    throw new Error(`candidate-01 atlas metadata is incomplete: ${record.topicId}`);
  }
  return { href: record.url, cellWidth, cellHeight, columns: cells, rows: 1 };
}

interface RuntimeTerrainContext {
  x: number;
  y: number;
  theme?: string;
  ownerColor?: string;
  linked: { n: boolean; e: boolean; s: boolean; w: boolean };
}

const connectionMask = (linked: RuntimeTerrainContext['linked']): number =>
  (linked.n ? 1 : 0) | (linked.e ? 2 : 0) | (linked.s ? 4 : 0) | (linked.w ? 8 : 0);

const baseTerrain = (theme?: string): RuntimeCellAtlas => cellAtlas(candidate01Asset(
  theme === 'c01-01' ? 'C01-TERRAIN-SILVERWOOD-1' : 'C01-TERRAIN-BORDER-1',
));

function structureFrame(record: Candidate01RuntimeAsset, state: 'normal' | 'damaged' | 'captured'): number {
  const index = record.stateOrder?.indexOf(state) ?? -1;
  return index >= 0 ? index : 0;
}

function structureAssetMarkup(record: Candidate01RuntimeAsset, frame: number, ownerColor?: string): string {
  const atlas = cellAtlas(record);
  const anchor = record.anchor ?? [atlas.cellWidth / 2, atlas.cellHeight - 1];
  const x = 16 - anchor[0];
  const y = 31 - anchor[1];
  const owner = ownerColor
    ? `<ellipse cx="16" cy="29" rx="13" ry="2.5" fill="none" stroke="${attr(ownerColor)}" stroke-width="1.5" opacity="0.95"/>`
    : '';
  return `<g data-candidate-art="structure" transform="translate(${x} ${y})">${runtimeAtlasCellMarkup(atlas, frame)}</g>${owner}`;
}

export function candidate01UnitMarkup(type: UnitTypeId, team: string): string | null {
  const topic = CANDIDATE_01_UNIT_ART[type];
  return topic ? runtimeUnitMarkup(unitSheet(candidate01Asset(topic)), team) : null;
}

export function candidate01UnitIcon(type: UnitTypeId, team: string, size: number): string | null {
  const markup = candidate01UnitMarkup(type, team);
  if (!markup) return null;
  return `<svg viewBox="0 -16 32 48" width="${size}" height="${size}" class="candidate-unit-icon">${markup}</svg>`;
}

export function candidate01TerrainMarkup(id: TerrainId, ctx: RuntimeTerrainContext): string | null {
  const structureTopic = CANDIDATE_01_MAP_STRUCTURE_ART[id];
  if (structureTopic) {
    const record = candidate01Asset(structureTopic);
    const captured = ctx.ownerColor !== undefined;
    const frame = structureFrame(record, captured ? 'captured' : 'normal');
    if (ctx.theme === 'c01-01') return structureAssetMarkup(record, frame, ctx.ownerColor);
    const groundCell = Math.min(3, Math.floor(((ctx.x * 31 + ctx.y * 17) >>> 0) % 4));
    return `${runtimeAtlasCellMarkup(baseTerrain(ctx.theme), groundCell)}${structureAssetMarkup(record, frame, ctx.ownerColor)}`;
  }

  // Twin Hills owns its complete terrain surface in the scene ground pass.
  // The non-empty marker intentionally suppresses the generic terrain fallback
  // while remaining visually transparent. Roads therefore cannot cover units.
  if (ctx.theme === 'c01-01' || ctx.theme?.startsWith('experience-lab')) {
    return '<g data-candidate-terrain="scene-owned" pointer-events="none"/>';
  }

  const topic = CANDIDATE_01_TERRAIN_ART[id];
  if (!topic) return null;
  const record = candidate01Asset(topic);
  const atlas = cellAtlas(record);
  const cell = record.tileMode === 'nesw-16'
    ? connectionMask(ctx.linked)
    : Math.min(atlas.columns - 1, Math.floor((((ctx.x * 31 + ctx.y * 17) >>> 0) % 997) / 997 * atlas.columns));
  const ground = record.tileMode === 'nesw-16'
    ? runtimeAtlasCellMarkup(baseTerrain(ctx.theme), Math.floor((((ctx.x * 31 + ctx.y * 17) >>> 0) % 4)))
    : '';
  return `${ground}${runtimeAtlasCellMarkup(atlas, cell)}`;
}

/** Render a destructible battle structure without leaking campaign art into the engine model. */
export function candidate01StructureMarkup(
  state: StructureState,
  def: StructureDef,
  ownerColor?: string,
): string | null {
  const topic = CANDIDATE_01_STRUCTURE_ART[state.type];
  if (!topic) return null;
  const record = candidate01Asset(topic);
  const visualState = state.hp <= def.maxHp * 0.5 || state.disabled ? 'damaged' : ownerColor ? 'captured' : 'normal';
  const frame = structureFrame(record, visualState);
  const ratio = Math.max(0, Math.min(1, state.hp / def.maxHp));
  return `${structureAssetMarkup(record, frame, ownerColor)}
    <rect x="5" y="27" width="22" height="3" rx="1.5" fill="#201914" opacity="0.72"/>
    <rect x="5.5" y="27.5" width="${(21 * ratio).toFixed(2)}" height="2" rx="1" fill="${ratio > 0.5 ? '#66b873' : '#d85c4c'}"/>`;
}

/** Illustrated cover art complements, but never replaces, the directional rules overlay. */
export function candidate01CoverPropMarkup(level: 'half' | 'full'): string {
  const topic = level === 'full' ? 'C01-BPROP-COVER-2' : 'C01-BPROP-COVER-1';
  return `<image href="${attr(candidate01AssetUrl(topic))}" x="-16" y="-20" width="64" height="64" class="candidate-battle-prop" preserveAspectRatio="xMidYMid meet"/>`;
}

export function candidate01MarkerMarkup(marker: BattlefieldMarker): string {
  const topic = marker.kind.includes('corpse') || marker.fallenUnit
    ? 'C01-BPROP-AFTERMATH-2'
    : marker.kind.includes('transport')
      ? 'C01-BPROP-AFTERMATH-3'
      : 'C01-BPROP-EVIDENCE-1';
  return `<image href="${attr(candidate01AssetUrl(topic))}" x="-8" y="-8" width="48" height="48" class="candidate-battle-marker" preserveAspectRatio="xMidYMid meet"/>`;
}

export function candidate01WeaponFxTopic(weapon: WeaponId): string | null {
  return CANDIDATE_01_WEAPON_FX[weapon] ?? null;
}

/** A self-describing effect strip consumed by the shared frame animation system. */
export function candidate01FxMarkup(topic: string, cx = 0, cy = 0): string {
  const record = candidate01Asset(topic);
  const frameWidth = record.frameWidth ?? 32;
  const frameHeight = record.frameHeight ?? 32;
  const frames = record.frames ?? 1;
  return `<svg x="${cx - frameWidth / 2}" y="${cy - frameHeight / 2}" width="${frameWidth}" height="${frameHeight}" viewBox="0 0 ${frameWidth} ${frameHeight}" overflow="hidden" class="candidate-fx" aria-hidden="true">
    ${runtimeFrameStripMarkup({
      href: record.url,
      frameWidth,
      frameHeight,
      frameCount: frames,
      clips: [{
        id: 'effect',
        frames: Array.from({ length: frames }, (_, frame) => frame),
        fps: record.fps ?? 12,
        loop: record.loop ?? false,
      }],
    }, 0, 'candidate-fx-strip')}
  </svg>`;
}

/** HTML icon for HUD commands, equipment and state chips. */
export function candidate01IconMarkup(topic: string, size = 28, className = 'candidate-art-icon'): string {
  return iconMarkupFor(candidate01Asset(topic), size, className);
}

/**
 * The icon for a topic this pack may not draw.
 *
 * The provider used to call the throwing version inside `try { } catch { return
 * null }`, so a topic the pack simply has no art for and a defect in asset
 * resolution came back as the same quiet null.
 */
export function candidate01TryIconMarkup(topic: string, size = 28, className = 'candidate-art-icon'): string | null {
  const record = candidate01TryAsset(topic);
  return record ? iconMarkupFor(record, size, className) : null;
}

const iconMarkupFor = (record: Candidate01RuntimeAsset, size: number, className: string): string =>
  `<img src="${attr(record.url)}" width="${size}" height="${size}" class="${attr(className)}" alt="" aria-hidden="true"/>`;
