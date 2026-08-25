import { Rectangle, Texture } from 'pixi.js';
import { hasRelief, RELIEF_SPILL, type BoardStrip } from './board-surface';

/**
 * Turning a picture into textures.
 *
 * A picture reaches a renderer as two different things, and a GPU backend needs
 * both as textures.
 *
 * Its body is markup, which has to be baked — once per distinct picture, which is
 * the whole reason a texture cache is worth anything here: `tools/board-scale.ts`
 * measures 4,131 terrain tiles across four distinct pictures on a painted field.
 *
 * Its strip is an image of frames, which needs no baking at all. Cutting it into
 * one texture per frame is what a spritesheet is for, and it is why the frames of a
 * walk cycle cost nothing beyond the image every unit of that type already shares.
 *
 * A port because both are browser-only and the display list is not. A test can
 * assert what is drawn, where and in what order with a rasteriser that answers
 * instantly; nothing can assert that a texture came out looking right.
 */
export interface PictureTextures {
  /** How many texture pixels one scene unit is baked at. */
  readonly resolution: number;
  /**
   * The CSS every baked picture is drawn under, set once per board.
   *
   * A picture is baked from its own markup, so a stylesheet in the page — or a
   * `<style>` somewhere else in the board's tree — is not in the room. Whatever the
   * scene declares as its style is written into each baked document instead, which
   * is what lets a prop's shadow and a sprite's rim light exist on this backend at
   * all. Setting it clears what has already been baked, because the same markup
   * under different CSS is a different picture.
   */
  style: string;
  /** The same markup is baked once. */
  bake(markup: string): Promise<BakedPicture>;
  /** One texture per frame of a strip, in order, cut without resampling. */
  frames(strip: BoardStrip): Promise<readonly Texture[]>;
  dispose(): void;
}

/** A baked picture, and where its ink sits relative to the markup's own origin. */
export interface BakedPicture {
  readonly texture: Texture;
  readonly left: number;
  readonly top: number;
}

const SVG_NS = 'http://www.w3.org/2000/svg';

/**
 * Both answers, from the browser this is running in.
 *
 * Bakes markup by asking the browser to rasterise it as an SVG image.
 *
 * Two things about that route are not obvious, and both are load-bearing.
 *
 * An SVG rasterised as an image is sandboxed: it may not fetch anything. This
 * campaign's tiles are `<image href="…/terrain-graveyard-1.png">`, so every asset a
 * picture points at has to be *inside* it before it can be baked. There are 62
 * distinct ones across the whole campaign, each fetched once.
 *
 * And a picture does not say how big it is. `BoardPiece` carries markup and a
 * place, which is all a vector renderer needs. Rather than make every producer
 * declare a box — including content packs, whose scene layers this module does not
 * control — the extent is measured from the picture itself, once per distinct
 * markup, with the same `getBBox` the browser would use to lay it out.
 */
export class BrowserPictureTextures implements PictureTextures {
  private css = '';
  private readonly baked = new Map<string, Promise<BakedPicture>>();
  private readonly cut = new Map<string, Promise<readonly Texture[]>>();
  private readonly assets = new Map<string, Promise<string>>();
  /** One hidden document to measure in, rather than one per measurement. */
  private ruler: SVGSVGElement | null = null;

  constructor(readonly resolution = 2) {
    if (!Number.isFinite(resolution) || resolution <= 0) {
      throw new Error(`texture resolution must be greater than zero, got ${resolution}`);
    }
  }

  get style(): string {
    return this.css;
  }

  set style(css: string) {
    if (css === this.css) return;
    this.css = css;
    // The same markup under different CSS is a different picture.
    this.baked.clear();
  }

  bake(markup: string): Promise<BakedPicture> {
    const found = this.baked.get(markup);
    if (found) return found;
    const pending = this.raster(markup);
    this.baked.set(markup, pending);
    return pending;
  }

  frames(strip: BoardStrip): Promise<readonly Texture[]> {
    // The image and how it is divided, because one sheet may be read as either.
    const key = `${strip.href}|${strip.frameCount}`;
    const found = this.cut.get(key);
    if (found) return found;
    const pending = this.slice(strip);
    this.cut.set(key, pending);
    return pending;
  }

  dispose(): void {
    this.ruler?.remove();
    this.ruler = null;
    this.baked.clear();
    this.cut.clear();
    this.assets.clear();
  }

  /**
   * The frames of one strip, sharing the image's single texture.
   *
   * Cut in the image's own pixels rather than in scene units: a sheet is drawn into
   * a frame-sized box with `preserveAspectRatio="none"`, so nothing guarantees that
   * one source pixel is one scene unit, and the sprite is sized by whoever draws it.
   */
  private async slice(strip: BoardStrip): Promise<readonly Texture[]> {
    const image = new Image();
    image.src = strip.href;
    await image.decode();
    const source = Texture.from(image).source;
    // Pixel art, presented at whatever size the board is: no smoothing between frames.
    source.scaleMode = 'nearest';
    const width = image.naturalWidth / strip.frameCount;
    return Array.from({ length: strip.frameCount }, (_, frame) => new Texture({
      source,
      frame: new Rectangle(frame * width, 0, width, image.naturalHeight),
    }));
  }

  private async raster(markup: string): Promise<BakedPicture> {
    const measured = this.measure(markup);
    // `getBBox` answers the geometry, and a filter draws outside it. A relief
    // spills two down and one across, so a texture cut to the geometry would clip
    // exactly the part that makes the sprite read over terrain.
    const room = hasRelief(markup) ? RELIEF_SPILL : 0;
    const box = {
      left: measured.left - room,
      top: measured.top - room,
      width: measured.width + room * 2,
      height: measured.height + room * 2,
    };
    const width = Math.max(1, Math.ceil(box.width));
    const height = Math.max(1, Math.ceil(box.height));
    const document = `<svg xmlns="${SVG_NS}" width="${width * this.resolution}" height="${height * this.resolution}"`
      + ` viewBox="${box.left} ${box.top} ${width} ${height}">${this.css}${await this.inlined(markup)}</svg>`;

    const image = new Image();
    image.src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(document)}`;
    await image.decode();
    return { texture: Texture.from(image), left: box.left, top: box.top };
  }

  /** What part of the plane this markup actually draws on. */
  private measure(markup: string): { left: number; top: number; width: number; height: number } {
    const ruler = this.measuringDocument();
    const host = globalThis.document.createElementNS(SVG_NS, 'g');
    ruler.append(host);
    host.innerHTML = markup;
    const box = host.getBBox();
    host.remove();
    // An empty picture still needs a texture a sprite can hold, so it gets one pixel.
    return {
      left: Number.isFinite(box.x) ? box.x : 0,
      top: Number.isFinite(box.y) ? box.y : 0,
      width: box.width || 1,
      height: box.height || 1,
    };
  }

  private measuringDocument(): SVGSVGElement {
    if (this.ruler) return this.ruler;
    const ruler = globalThis.document.createElementNS(SVG_NS, 'svg');
    // Out of the way but still laid out: `getBBox` on a `display: none` subtree
    // throws in some engines and answers zero in others.
    ruler.setAttribute('aria-hidden', 'true');
    ruler.setAttribute('style', 'position:absolute;width:0;height:0;overflow:hidden;pointer-events:none');
    globalThis.document.body.append(ruler);
    this.ruler = ruler;
    return ruler;
  }

  /** The same markup with every external reference carried inside it. */
  private async inlined(markup: string): Promise<string> {
    const urls = [...new Set([...markup.matchAll(/href="([^"]+)"/g)].map(([, url]) => url))]
      .filter((url) => !url.startsWith('data:'));
    if (!urls.length) return markup;

    const datas = await Promise.all(urls.map((url) => this.asData(url)));
    let inlined = markup;
    urls.forEach((url, index) => {
      inlined = inlined.replaceAll(`href="${url}"`, `href="${datas[index]}"`);
    });
    return inlined;
  }

  private asData(url: string): Promise<string> {
    const found = this.assets.get(url);
    if (found) return found;
    const pending = fetch(url)
      .then((response) => {
        if (!response.ok) throw new Error(`cannot bake ${url}: ${response.status}`);
        return response.blob();
      })
      .then((blob) => new Promise<string>((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result));
        reader.onerror = () => reject(new Error(`cannot read ${url}`));
        reader.readAsDataURL(blob);
      }));
    this.assets.set(url, pending);
    return pending;
  }
}
