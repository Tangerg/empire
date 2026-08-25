import type { RuntimeCellAtlas } from './runtime-raster';

/*
 * The pair fields below are `readonly number[]`, not `readonly [number, number]`.
 *
 * These interfaces describe a *document* — a manifest a build tool writes and a
 * pack imports — and JSON has no tuples, so a pack could only satisfy the tuple
 * form with `manifestJson as EnvironmentManifest`. That assertion was the only
 * thing standing between a generator that dropped a coordinate and an atlas cell
 * drawn at `NaN`. The arity is checked once, on the way in, by the catalog below.
 */
export interface EnvironmentCellRecord {
  readonly id: string;
  readonly label?: string;
  readonly index?: number;
  readonly footprint?: readonly number[];
  readonly anchor?: readonly number[];
  readonly tags?: readonly string[];
  readonly passable?: boolean;
  readonly heightDelta?: number;
  readonly obstructionHeight?: number;
}

export interface EnvironmentAtlasRecord {
  readonly id: string;
  readonly category: string;
  readonly png: string;
  readonly png2x?: string;
  readonly columns: number;
  readonly rows: number;
  readonly cellWidth: number;
  readonly cellHeight: number;
  readonly componentCount: number;
  readonly tileMode?: string;
  readonly anchor?: readonly number[];
  readonly maskOrder?: readonly number[];
  readonly variantsPerMask?: number;
  readonly cellIndex?: string;
  readonly cells?: readonly EnvironmentCellRecord[];
}

/*
 * What the catalog reads, not everything the file holds.
 *
 * `schemaVersion`, `renderLayer` and `ySort` were declared here and read by
 * nothing — and `ySort` was declared `boolean` while the shipped manifest says
 * `'bottom-anchor'` on ten atlases, which is a sorting *mode*. Both halves of that
 * were invisible because the one pack reading a manifest reached it through
 * `json as EnvironmentManifest`. A document may carry more than this; the extra
 * rides along in the record and nothing here pretends to know its type.
 */
export interface EnvironmentManifest {
  readonly runtimeReady?: boolean;
  readonly atlases: readonly EnvironmentAtlasRecord[];
}

export interface RuntimeEnvironmentAtlas extends EnvironmentAtlasRecord {
  /** Logical-density source, useful to tools and asset diagnostics. */
  readonly url: string;
  /** Preferred presentation source when the package supplies one. */
  readonly url2x?: string | undefined;
  readonly pixelDensity: 1 | 2;
  readonly raster: RuntimeCellAtlas;
}

export interface RuntimeEnvironmentCell {
  readonly atlas: RuntimeEnvironmentAtlas;
  readonly cell: EnvironmentCellRecord & { readonly index: number };
}

export type EnvironmentUrlResolver = (relativePath: string) => string | undefined;

/** A point or a size the manifest states, checked for being one. */
function checkPair(value: readonly number[] | undefined, field: string, subject: string): void {
  if (value && (value.length !== 2 || value.some((entry) => !Number.isFinite(entry)))) {
    throw new Error(`Environment ${field} of ${subject} must be two finite numbers`);
  }
}

/**
 * Runtime-facing, immutable view of an environment atlas package.
 *
 * The catalog deliberately knows nothing about a battle, story or renderer. It
 * owns the fiddly atlas conventions once, while scene renderers only ask for a
 * semantic cell or a connected-tile index.
 */
export class EnvironmentCatalog {
  private readonly atlasesById = new Map<string, RuntimeEnvironmentAtlas>();
  private readonly cellsById = new Map<string, RuntimeEnvironmentCell>();

  public constructor(manifest: EnvironmentManifest, resolveUrl: EnvironmentUrlResolver) {
    for (const record of manifest.atlases) {
      const url = resolveUrl(record.png);
      const url2x = record.png2x ? resolveUrl(record.png2x) : undefined;
      const rasterUrl = url2x ?? url;
      if (!url || !rasterUrl) {
        throw new Error(`Environment atlas file is missing: ${record.png}`);
      }
      if (record.columns <= 0 || record.rows <= 0 || record.cellWidth <= 0 || record.cellHeight <= 0) {
        throw new Error(`Environment atlas has invalid geometry: ${record.id}`);
      }

      checkPair(record.anchor, 'anchor', record.id);

      const atlas: RuntimeEnvironmentAtlas = Object.freeze({
        ...record,
        url,
        url2x,
        pixelDensity: url2x ? 2 : 1,
        raster: Object.freeze({
          href: rasterUrl,
          cellWidth: record.cellWidth,
          cellHeight: record.cellHeight,
          columns: record.columns,
          rows: record.rows,
        }),
      });
      this.atlasesById.set(record.id, atlas);

      for (const [declaredIndex, source] of (record.cells ?? []).entries()) {
        const cell = Object.freeze({ ...source, index: source.index ?? declaredIndex });
        checkPair(cell.anchor, 'anchor', cell.id);
        checkPair(cell.footprint, 'footprint', cell.id);
        if (this.cellsById.has(cell.id)) {
          throw new Error(`Duplicate environment cell id: ${cell.id}`);
        }
        this.cellsById.set(cell.id, Object.freeze({ atlas, cell }));
      }
    }
  }

  public atlas(id: string): RuntimeEnvironmentAtlas {
    const atlas = this.atlasesById.get(id);
    if (!atlas) throw new Error(`Unknown environment atlas: ${id}`);
    return atlas;
  }

  public cell(id: string): RuntimeEnvironmentCell {
    const cell = this.cellsById.get(id);
    if (!cell) throw new Error(`Unknown environment cell: ${id}`);
    return cell;
  }

  public connectedIndex(atlasId: string, mask: number, variant = 0): number {
    const atlas = this.atlas(atlasId);
    const normalizedMask = mask & 0x0f;
    const variants = Math.max(1, atlas.variantsPerMask ?? 1);
    const normalizedVariant = Math.abs(Math.trunc(variant)) % variants;
    if (atlas.cellIndex === 'mask * 4 + variant' || atlas.tileMode === 'nesw-16-variants') {
      return normalizedMask * variants + normalizedVariant;
    }
    return normalizedMask;
  }

  public blobIndex(atlasId: string, mask: number): number {
    const atlas = this.atlas(atlasId);
    const canonical = canonicalBlobMask(mask);
    const index = atlas.maskOrder?.indexOf(canonical) ?? -1;
    if (index < 0) {
      throw new Error(`Blob mask ${canonical} is not declared by atlas ${atlasId}`);
    }
    return index;
  }
}

/** Keep diagonal links only when both adjacent cardinal links exist. */
function canonicalBlobMask(mask: number): number {
  let canonical = mask & 0xff;
  if ((canonical & 0x05) !== 0x05) canonical &= ~0x02; // NE requires N + E
  if ((canonical & 0x14) !== 0x14) canonical &= ~0x08; // SE requires E + S
  if ((canonical & 0x50) !== 0x50) canonical &= ~0x20; // SW requires S + W
  if ((canonical & 0x41) !== 0x41) canonical &= ~0x80; // NW requires W + N
  return canonical;
}
