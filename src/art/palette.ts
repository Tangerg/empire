/** Single source of truth for every colour the art layer uses. */
export const PAL = {
  grass: '#7cae54',
  grassDark: '#6b9c47',
  grassLight: '#8fbf63',
  dirt: '#c9a978',
  dirtDark: '#a98a5c',
  water: '#4a90c4',
  waterDark: '#3a7aa8',
  waterLight: '#7cbde8',
  leaf: '#3f7d3f',
  leafDark: '#2f6330',
  leafLight: '#559751',
  trunk: '#6b4a2f',
  rock: '#9b9188',
  rockDark: '#7a7168',
  rockLight: '#c4bcb2',
  stone: '#b0a89c',
  stoneDark: '#8a8378',
  stoneLight: '#d2cbc0',
  wood: '#8a6239',
  woodDark: '#6a4a2a',
  roof: '#b3563f',
  roofDark: '#8f4231',
  plaster: '#e2d5b8',
  ink: '#2b2b33',
  inkSoft: '#3d3d47',
  skin: '#e8b98f',
  skinDark: '#c99a72',
  cloth: '#d8d2c4',
  steel: '#c3cbd6',
  steelDark: '#8d97a5',
  gold: '#e8c35a',
  neutral: '#9aa3ad',
  hpGood: '#5fd07a',
  hpMid: '#e8c35a',
  hpLow: '#e0604f',
} as const;

/** Faction colours, deliberately readable against grass. */
export const TEAM_COLORS = ['#3f7fd8', '#d8483f', '#54a860', '#a45fc0'];

export function shade(hex: string, amount: number): string {
  const n = parseInt(hex.slice(1), 16);
  const r = (n >> 16) & 255;
  const g = (n >> 8) & 255;
  const b = n & 255;
  const mix = (c: number) =>
    Math.max(0, Math.min(255, Math.round(amount < 0 ? c * (1 + amount) : c + (255 - c) * amount)));
  return `#${((mix(r) << 16) | (mix(g) << 8) | mix(b)).toString(16).padStart(6, '0')}`;
}
