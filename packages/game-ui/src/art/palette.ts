/** Single source of truth for every colour the art layer uses. */
export const PAL = {
  grass: '#739d45',
  grassDark: '#507738',
  grassLight: '#91b858',
  dirt: '#b98a58',
  dirtDark: '#875f3d',
  water: '#447f96',
  waterDark: '#2f6178',
  waterLight: '#7fb4bb',
  leaf: '#315f38',
  leafDark: '#203f2e',
  leafLight: '#4f7c43',
  trunk: '#61432b',
  rock: '#85837d',
  rockDark: '#585b59',
  rockLight: '#b8b5ac',
  stone: '#999080',
  stoneDark: '#625c55',
  stoneLight: '#c5bca9',
  wood: '#8c6037',
  woodDark: '#513622',
  roof: '#a84d35',
  roofDark: '#713224',
  plaster: '#d8c5a1',
  ink: '#28231f',
  inkSoft: '#3d342e',
  skin: '#e0aa7f',
  skinDark: '#b97d5b',
  /*
   * The green hide of the big ones, three tones of it.
   *
   * Written out in both `units.ts` (the sprite on the board) and `portraits.ts`
   * (the same creature's plate), which is one creature painted twice from two
   * copies of the same three numbers.
   */
  hide: '#8fa06a',
  hideDark: '#7f9160',
  hideLight: '#9cae76',
  /** Light behind a window, which is what makes a house look lived in. */
  lamp: '#d5ad62',
  cloth: '#d7cbb1',
  steel: '#bcc3bf',
  steelDark: '#737b79',
  gold: '#e4b84f',
  neutral: '#918b7d',
  /** Nobody's colour, for a side that has none: a slate grey, not the earthy one. */
  unowned: '#9aa3ad',
  hpGood: '#5fd07a',
  hpMid: '#e8c35a',
  hpLow: '#e0604f',
} as const;

/** Faction colours, deliberately readable against grass. */
export const TEAM_COLORS = ['#3f7fd8', '#d8483f', '#54a860', '#a45fc0'];

/**
 * Ground tones a tile is drawn in when nobody drew it.
 *
 * Four families, one per thing the rules can say about standing on a tile, and
 * five well-separated tones inside each so two terrains a content pack invented
 * on the same afternoon do not come out the same colour. They are muted on
 * purpose: these sit under hand-painted tiles in the same field, and a tile that
 * shouts is worse than a tile that is merely unfamiliar.
 */
export const GROUND_TONES = {
  /** Anything may walk it. */
  open: ['#739d45', '#84964a', '#5f8f56', '#8fa052', '#6b8f3e'],
  /** Passable but slow. */
  broken: ['#a08a56', '#8a7c50', '#b09468', '#94804e', '#7d7350'],
  /** Nothing may enter. */
  stone: ['#85837d', '#77786f', '#948d80', '#6f7370', '#9c948a'],
  /** Only some ways of moving may cross it. */
  liquid: ['#447f96', '#3a6f8b', '#4f8aa0', '#356b7a', '#568fa2'],
} as const;

/** The three tones a side's colour gives a figure. */
export interface SpriteColors {
  team: string;
  dark: string;
  light: string;
}

export const spriteColors = (team: string): SpriteColors => ({
  team,
  dark: shade(team, -0.35),
  light: shade(team, 0.28),
});

export function shade(hex: string, amount: number): string {
  const n = parseInt(hex.slice(1), 16);
  const r = (n >> 16) & 255;
  const g = (n >> 8) & 255;
  const b = n & 255;
  const mix = (c: number) =>
    Math.max(0, Math.min(255, Math.round(amount < 0 ? c * (1 + amount) : c + (255 - c) * amount)));
  return `#${((mix(r) << 16) | (mix(g) << 8) | mix(b)).toString(16).padStart(6, '0')}`;
}
