import { gaugeRatio } from '@empire/battle-engine';
import type {
  BattlefieldMarker,
  StructureDef,
  StructureState,
  TerrainId,
  UnitTypeId,
  WeaponId,
} from '@empire/battle-engine';
import {
  candidate01Asset,
  candidate01AssetUrl,
  candidate01TryAsset,
  type Candidate01RuntimeAsset,
} from './candidate-01-assets';
import {
  CANDIDATE_01_MAP_STRUCTURE_ART,
  CANDIDATE_01_STRUCTURE_ART,
  CANDIDATE_01_UNIT_ART,
  CANDIDATE_01_WEAPON_FX,
} from './candidate-01-bindings';
import {
  boardPictureMarkup,
  escapeAttr as attr,
  runtimeAtlasCellMarkup,
  runtimeUnitPicture,
  shade,
  tileGaugeBar,
  type BoardPicture,
  type RuntimeCellAtlas,
  type TileContext,
  type RuntimeUnitSheet,
} from '@empire/game-ui';

function unitSheet(record: Candidate01RuntimeAsset): RuntimeUnitSheet {
  const frameWidth = record.frameWidth;
  const frameHeight = record.frameHeight;
  const frameCount = record.frames;
  if (!frameWidth || !frameHeight || !frameCount || !record.anchor) {
    throw new Error(`candidate-01 unit metadata is incomplete: ${record.topicId}`);
  }
  const frameIndex = (role: string): number => {
    const index = record.frameOrder?.indexOf(role) ?? -1;
    if (index < 0 || index >= frameCount) {
      throw new Error(`candidate-01 unit ${record.topicId} has no valid "${role}" frame`);
    }
    return index;
  };
  return {
    href: record.url,
    frameWidth,
    frameHeight,
    frameCount,
    anchor: { x: record.anchor[0], y: record.anchor[1] },
    idleFrame: frameIndex('idle'),
    walkFrames: [frameIndex('walk-a'), frameIndex('walk-b')],
    // Mission sheets name their purposeful action rather than pretending a
    // civilian has a combat attack. This adapter is the explicit translation
    // into the board's three animation clips.
    attackFrame: frameIndex(record.category === 'mission-unit' ? 'carry-or-work' : 'attack'),
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

function structureFrame(record: Candidate01RuntimeAsset, state: 'normal' | 'damaged' | 'captured'): number {
  const index = record.stateOrder?.indexOf(state) ?? -1;
  if (index < 0) throw new Error(`candidate-01 structure ${record.topicId} has no "${state}" frame`);
  return index;
}

/**
 * Whose ground this is, read from across the board.
 *
 * A thin ellipse under the building, which is what this was, says "owned" only to
 * someone already looking at that tile. A painted map says it with a standard: a
 * pole and a pennant in the holder's colour, tall enough to clear the roof, plus
 * the faintest wash of that colour on the ground it stands on.
 *
 * Drawn rather than stamped, because the environment kit ships one banner in all
 * thirty-six atlases and it is a ruin (`wasteland-broken-standard`). Vector work is
 * already how this package draws what its sheets do not have.
 */
function ownerBanner(color: string): string {
  const pole = attr(color);
  const cloth = attr(shade(color, 0.1));
  const shadow = attr(shade(color, -0.45));
  return `<g class="candidate-owner-banner" pointer-events="none">`
    + `<ellipse cx="16" cy="29.2" rx="12" ry="2.6" fill="${pole}" opacity="0.16"/>`
    + `<path d="M7.4 20.5 L7.4 5.2" stroke="#241b14" stroke-width="1.3" stroke-linecap="round"/>`
    + `<path d="M7.4 5.2 L7.4 4.1" stroke="${cloth}" stroke-width="1.9" stroke-linecap="round"/>`
    + `<path d="M8.1 5.6 L16.6 8.1 L8.1 11.1 Z" fill="${cloth}" stroke="${shadow}" stroke-width="0.55"`
    + ` stroke-linejoin="round"/>`
    + `<path d="M8.1 5.6 L16.6 8.1 L8.1 11.1 Z" fill="none" stroke="rgb(255 255 255 / 22%)" stroke-width="0.35"/>`
    + `</g>`;
}

function structureAssetMarkup(record: Candidate01RuntimeAsset, frame: number, ownerColor?: string): string {
  const atlas = cellAtlas(record);
  const anchor = record.anchor ?? [atlas.cellWidth / 2, atlas.cellHeight - 1];
  const x = 16 - anchor[0];
  const y = 31 - anchor[1];
  const owner = ownerColor ? ownerBanner(ownerColor) : '';
  return `<g data-candidate-art="structure" transform="translate(${x} ${y})">${runtimeAtlasCellMarkup(atlas, frame)}</g>${owner}`;
}

export function candidate01UnitPicture(type: UnitTypeId, team: string): BoardPicture | null {
  const topic = CANDIDATE_01_UNIT_ART[type];
  return topic ? runtimeUnitPicture(unitSheet(candidate01Asset(topic)), team) : null;
}

export function candidate01UnitIcon(type: UnitTypeId, team: string, size: number): string | null {
  const picture = candidate01UnitPicture(type, team);
  if (!picture) return null;
  // An icon has no timeline, so its strip stands still on the first frame.
  return `<svg viewBox="0 -16 32 48" width="${size}" height="${size}">${boardPictureMarkup(picture)}</svg>`;
}

/**
 * A building on a cell — and nothing else, because the scene owns the ground.
 *
 * This used to answer two questions. It drew the structures, and it also drew the
 * *surface* of every cell from `CANDIDATE_01_TERRAIN_ART`: one four-variant tile
 * per terrain, no transitions and no connections, so a field of it came out as a
 * visible grid of stamps. That path ran on every level except chapter one and the
 * experience lab, which had the environment builder's composition instead.
 *
 * Two owners for one question, chosen by level id. The scene paints every level
 * now, so what is left here is the building, and the empty string is this
 * painter's way of saying the ground is somebody else's business. (Not `null`:
 * that means "no opinion" and lets the generic painter draw a tile underneath.)
 */
export function candidate01TerrainMarkup(id: TerrainId, ctx: TileContext): string {
  const structureTopic = CANDIDATE_01_MAP_STRUCTURE_ART[id];
  if (!structureTopic) return '';
  const record = candidate01Asset(structureTopic);
  const frame = structureFrame(record, ctx.ownerColor !== undefined ? 'captured' : 'normal');
  return structureAssetMarkup(record, frame, ctx.ownerColor);
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
  const ratio = gaugeRatio(state.hp, def.maxHp);
  return `${structureAssetMarkup(record, frame, ownerColor)}${tileGaugeBar(ratio)}`;
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

/**
 * One effect: a strip, drawn about its own origin, already running.
 *
 * The board plays this the moment it appears and takes it away when it is done, so
 * `playing` is the whole of its animation policy — there is nobody to tell it to
 * start. It used to be a self-describing `<image>` whose clips the renderer found
 * by class name and read out of a JSON attribute, then played whichever came first.
 */
export function candidate01FxPicture(topic: string): BoardPicture {
  const record = candidate01Asset(topic);
  const frameWidth = record.frameWidth ?? 32;
  const frameHeight = record.frameHeight ?? 32;
  const frames = record.frames ?? 1;
  return {
    body: '',
    strip: {
      href: record.url,
      frameWidth,
      frameHeight,
      frameCount: frames,
      x: -frameWidth / 2,
      y: -frameHeight / 2,
      clips: [{
        id: 'effect',
        frames: Array.from({ length: frames }, (_, frame) => frame),
        fps: record.fps ?? 12,
        loop: record.loop ?? false,
      }],
      playing: 'effect',
    },
  };
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
