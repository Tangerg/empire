import { Texture } from 'pixi.js';

/**
 * Turning a picture's markup into a texture.
 *
 * The board hands every renderer the same thing: markup. A DOM backend appends it
 * and is done; a GPU backend has to bake it, once per distinct picture, and that is
 * the whole reason a texture cache is worth anything here — `tools/board-scale.ts`
 * measures 4,131 terrain tiles across four distinct pictures on a painted field.
 *
 * A port because the baking is browser-only and the display list is not. A test can
 * assert what is drawn, where and in what order with a rasteriser that answers
 * instantly; nothing can assert that a texture came out looking right.
 */
export interface MarkupTextures {
  /** How many texture pixels one scene unit is baked at. */
  readonly resolution: number;
  /** The same markup is baked once. */
  bake(markup: string): Promise<BakedPicture>;
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
export class SvgMarkupTextures implements MarkupTextures {
  private readonly baked = new Map<string, Promise<BakedPicture>>();
  private readonly assets = new Map<string, Promise<string>>();
  /** One hidden document to measure in, rather than one per measurement. */
  private ruler: SVGSVGElement | null = null;

  constructor(readonly resolution = 2) {
    if (!Number.isFinite(resolution) || resolution <= 0) {
      throw new Error(`texture resolution must be greater than zero, got ${resolution}`);
    }
  }

  bake(markup: string): Promise<BakedPicture> {
    const found = this.baked.get(markup);
    if (found) return found;
    const pending = this.raster(markup);
    this.baked.set(markup, pending);
    return pending;
  }

  dispose(): void {
    this.ruler?.remove();
    this.ruler = null;
    this.baked.clear();
    this.assets.clear();
  }

  private async raster(markup: string): Promise<BakedPicture> {
    const box = this.measure(markup);
    const width = Math.max(1, Math.ceil(box.width));
    const height = Math.max(1, Math.ceil(box.height));
    const document = `<svg xmlns="${SVG_NS}" width="${width * this.resolution}" height="${height * this.resolution}"`
      + ` viewBox="${box.left} ${box.top} ${width} ${height}">${await this.inlined(markup)}</svg>`;

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
