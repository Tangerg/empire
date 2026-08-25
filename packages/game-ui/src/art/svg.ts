const SVG_NS = 'http://www.w3.org/2000/svg';

type Attrs = Record<string, string | number | boolean | null | undefined>;

/** Minimal SVG element helper — the whole renderer is built on this. */
export function svg<K extends keyof SVGElementTagNameMap>(
  tag: K,
  attrs: Attrs = {},
  children: (SVGElement | string)[] = [],
): SVGElementTagNameMap[K] {
  const el = document.createElementNS(SVG_NS, tag);
  setAttrs(el, attrs);
  for (const c of children) {
    el.append(typeof c === 'string' ? c : c);
  }
  return el;
}

export function setAttrs(el: Element, attrs: Attrs): void {
  for (const [k, v] of Object.entries(attrs)) {
    if (v === null || v === undefined || v === false) {
      el.removeAttribute(k);
    } else {
      el.setAttribute(k, String(v));
    }
  }
}

/**
 * A value safe to put between the quotes of an attribute.
 *
 * Here rather than in each module that writes markup: there were three private
 * copies of this four-line function, and a fourth call site that had none.
 */
export const escapeAttr = (value: string): string =>
  value.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;');

/** Parse an SVG markup string into elements (used by the sprite library). */
export function fromMarkup(markup: string): SVGGElement {
  const doc = new DOMParser().parseFromString(
    `<svg xmlns="${SVG_NS}"><g>${markup}</g></svg>`,
    'image/svg+xml',
  );
  const g = doc.documentElement.firstElementChild as SVGGElement;
  return document.importNode(g, true);
}

export function clear(el: Element): void {
  while (el.firstChild) el.removeChild(el.firstChild);
}
