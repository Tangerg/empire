import { boardPictureMarkup, type BoardPicture } from './board-surface';
import type { ArtDirection } from './direction';
import type { UnitDef, UnitTypeId } from '@empire/battle-engine';
import { PAL, shade, spriteColors, type SpriteColors } from './palette';
import { groundShadow } from './shading';
import { unitFromRules } from './unit-from-rules';

/**
 * Unit sprites, hand-drawn in a 32x32 box with the ground line at y=29 and the
 * figure facing right. Faction colour drives cloth/caparison/wings so both
 * armies read at a glance; class identity comes from silhouette and weapon.
 *
 * This is one game's roster. A type nobody drew is drawn from what the rules can
 * see about it — see `unit-from-rules.ts`.
 */
export { spriteColors, type SpriteColors } from './palette';

type Sprite = (c: SpriteColors) => string;

const shadow = groundShadow({ cx: 16, cy: 29 }, { rx: 8.8, ry: 2.6 }, 'heavy') + `
  <rect x="11" y="28" width="8" height="1" fill="#ffffff" opacity="0.08"/>`;
const legs = (color: string) =>
  `<rect x="12" y="21" width="3.2" height="7" rx="1.2" fill="${color}"/>
   <rect x="16.8" y="21" width="3.2" height="7" rx="1.2" fill="${color}"/>
   <rect x="11.4" y="27" width="4.4" height="1.8" rx="0.8" fill="${PAL.woodDark}"/>
   <rect x="16.2" y="27" width="4.4" height="1.8" rx="0.8" fill="${PAL.woodDark}"/>`;
const head = (cx = 16, cy = 10.5, r = 3.9) =>
  `<circle cx="${cx}" cy="${cy}" r="${r}" fill="${PAL.skin}"/>
   <path d="M${cx - r} ${cy}a${r} ${r} 0 0 0 ${r * 2} 0z" fill="${PAL.skinDark}" opacity="0.35"/>`;

const sprites: Record<UnitTypeId, Sprite> = {
  /* ------------------------------------------------------------- infantry */
  soldier: (c) => `
    ${shadow}
    ${legs(PAL.inkSoft)}
    <path d="M22.5 5.5 26 15l-1.8 0.9-3.9-9.6z" fill="${PAL.steel}"/>
    <path d="M22.5 5.5 26 15l-0.9 0.45-3.4-9.35z" fill="${PAL.steelDark}"/>
    <rect x="19.6" y="14.4" width="5.6" height="1.8" rx="0.8" fill="${PAL.gold}" transform="rotate(-22 22.4 15.3)"/>
    <path d="M11 13h10l1.4 9H9.6z" fill="${c.team}"/>
    <path d="M16 13h5l1.4 9H16z" fill="${c.dark}"/>
    <rect x="9.4" y="20.4" width="13.2" height="2" rx="0.8" fill="${PAL.woodDark}"/>
    ${head()}
    <path d="M11.6 10.4a4.6 4.6 0 0 1 8.8 0z" fill="${PAL.steel}"/>
    <path d="M11.6 10.4h8.8v1.5h-8.8z" fill="${PAL.steelDark}"/>
    <rect x="15.4" y="5.6" width="1.2" height="2.4" fill="${c.light}"/>
    <ellipse cx="8.6" cy="17.2" rx="3.4" ry="5" fill="${c.light}" stroke="${PAL.steelDark}" stroke-width="1"/>
    <ellipse cx="8.6" cy="17.2" rx="1.2" ry="1.8" fill="${PAL.gold}"/>`,

  archer: (c) => `
    ${shadow}
    ${legs(PAL.leafDark)}
    <path d="M21 6q6 6 0 18" stroke="${PAL.wood}" stroke-width="1.8" fill="none" stroke-linecap="round"/>
    <path d="M21 6q6 6 0 18" stroke="${PAL.cloth}" stroke-width="0.7" fill="none" opacity="0.5" transform="translate(1.6 0)"/>
    <path d="M11.6 13h9.4l1.2 8.6H10.4z" fill="${c.team}"/>
    <path d="M16.3 13h4.7l1.2 8.6h-5.9z" fill="${c.dark}"/>
    <path d="M9.2 12.6 13 21.4" stroke="${PAL.wood}" stroke-width="1.4" stroke-linecap="round"/>
    <path d="M8.2 11.4h4.4l0.8 2.6H8.6z" fill="${PAL.leafDark}"/>
    ${head(15.4, 10.6, 3.7)}
    <path d="M11.2 11.4q0.4-6 4.6-6t4.4 5.4l-1.6 1q-0.4-4-2.9-4t-3 4z" fill="${PAL.leaf}"/>
    <path d="M19.6 5.6q3.4 1 2.4 4.6l-3-1.6z" fill="${PAL.leafDark}"/>
    <path d="M13.4 15.8h9.6" stroke="${PAL.cloth}" stroke-width="1.1" stroke-linecap="round" opacity="0.9"/>
    <path d="M22.6 14.9l1.8 0.9-1.8 0.9z" fill="${PAL.steel}"/>`,

  rogue: (c) => `
    ${shadow}
    ${legs(PAL.ink)}
    <path d="M10.6 12.6q7-2 11 1.6-2.6 7.4-1.6 8.4H10z" fill="${c.dark}"/>
    <path d="M12 13.4h8.2l1 8.2H11.2z" fill="${c.team}"/>
    <path d="M22.6 12.4 27 8.6l1.2 1.4-4.4 3.8z" fill="${PAL.steel}"/>
    <path d="M22 13.6l1.6-0.4 0.5 1.6-1.7 0.3z" fill="${PAL.woodDark}"/>
    <path d="M9.6 20.6 5.4 24l-1.2-1.4 4.2-3.4z" fill="${PAL.steel}"/>
    ${head(16, 10.6, 3.6)}
    <path d="M11.8 12q0-6.4 4.4-6.4 4.6 0 4.6 5l-2 1.4q0.2-3.4-2.5-3.4-2.6 0-2.6 3.6z" fill="${c.dark}"/>
    <path d="M13.4 10.6h4.2v1.8h-4.2z" fill="${PAL.ink}" opacity="0.65"/>
    <circle cx="17.4" cy="10.8" r="0.7" fill="${PAL.gold}"/>`,

  cleric: (c) => `
    ${shadow}
    <path d="M11 14h10l2 14H9z" fill="${PAL.cloth}"/>
    <path d="M16 14h5l2 14h-7z" fill="${shade(PAL.cloth, -0.18)}"/>
    <path d="M9 26.8h14l0.6 1.4H8.4z" fill="${c.team}"/>
    <path d="M11 14h10l0.5 3.4h-11z" fill="${c.team}"/>
    <path d="M23.4 6v20" stroke="${PAL.wood}" stroke-width="1.7" stroke-linecap="round"/>
    <circle cx="23.4" cy="5.4" r="2.6" fill="${PAL.gold}"/>
    <circle cx="23.4" cy="5.4" r="1.1" fill="${PAL.plaster}"/>
    ${head(15.6, 10.4, 3.8)}
    <path d="M11.6 10.8q0-5.4 4-5.4t4 5.4z" fill="${PAL.cloth}"/>
    <path d="M12.2 12.6h6.8l-0.6 1.6h-5.6z" fill="${c.light}"/>
    <ellipse cx="15.6" cy="4.2" rx="4.6" ry="1.5" fill="none" stroke="${PAL.gold}" stroke-width="1.1" opacity="0.95"/>`,

  mage: (c) => `
    ${shadow}
    <path d="M10.4 15h11.2l2.2 13H8.2z" fill="${c.team}"/>
    <path d="M16 15h5.6l2.2 13H16z" fill="${c.dark}"/>
    <path d="M8.2 26.4h15.6l0.4 1.6H7.8z" fill="${c.light}"/>
    <path d="M24.6 7.4v19" stroke="${PAL.wood}" stroke-width="1.7" stroke-linecap="round"/>
    <circle cx="24.6" cy="6.2" r="3" fill="${PAL.waterLight}" opacity="0.95"/>
    <circle cx="23.6" cy="5.2" r="1" fill="#ffffff" opacity="0.9"/>
    ${head(15.4, 11, 3.6)}
    <path d="M12.2 13.6h6.6l-0.5 2.2h-5.6z" fill="${PAL.cloth}"/>
    <path d="M9.6 9.4h11.6l-0.8 1.6H10.4z" fill="${c.dark}"/>
    <path d="M15.4 0.8 20.6 9.6H10.2z" fill="${c.team}"/>
    <path d="M15.4 0.8 20.6 9.6h-5.2z" fill="${c.dark}"/>
    <circle cx="15.4" cy="6.4" r="1.1" fill="${PAL.gold}"/>`,

  /* -------------------------------------------------------------- cavalry */
  knight: (c) => `
    ${shadow}
    <path d="M6 17q3-4 9-4h8q3 0 4 3v4q0 2-3 2H9q-3 0-3-3z" fill="${shade(PAL.woodDark, 0.15)}"/>
    <path d="M23 13q3 0 4 3l-2 1-3-2z" fill="${PAL.woodDark}"/>
    <path d="M7.4 21.4v6.6M11.6 21.6v6.4M20 21.6v6.4M24.4 21.4v6.6" stroke="${PAL.woodDark}" stroke-width="2.2" stroke-linecap="round"/>
    <path d="M25.4 15.6q2-1.6 2.6-5l1.8 0.4q-0.6 4.6-3 6.6z" fill="${shade(PAL.woodDark, 0.15)}"/>
    <path d="M27.6 9.6q1.8-0.4 2.6 1.2-1.4 1-2.8 0.6z" fill="${PAL.woodDark}"/>
    <path d="M6.6 17q-3.4-1.6-4.4-5.4l1.6-0.8q1.4 3.4 3.8 4.4z" fill="${shade(PAL.woodDark, 0.15)}"/>
    <path d="M9 16h10l-1.4 7.4H10z" fill="${c.team}"/>
    <path d="M14 16h5l-1.4 7.4H14z" fill="${c.dark}"/>
    <path d="M11.6 22.6h6.6l-1 2.6h-4.8z" fill="${c.light}"/>
    <path d="M12.4 8.4h6.4l1.2 7.4h-8.8z" fill="${PAL.steel}"/>
    <path d="M15.6 8.4h3.2l1.2 7.4h-4.4z" fill="${PAL.steelDark}"/>
    ${head(15.6, 7.6, 3.4)}
    <path d="M12.2 7.6a3.6 3.6 0 0 1 7 0l0.4 2.2h-7.8z" fill="${PAL.steel}"/>
    <path d="M12.4 8.2h7v1.2h-7z" fill="${PAL.steelDark}"/>
    <path d="M15.4 2.6q2.6 0.6 2.2 3.4l-1.8-0.6z" fill="${c.light}"/>
    <path d="M20 12.6 30.6 5.4" stroke="${PAL.wood}" stroke-width="1.6" stroke-linecap="round"/>
    <path d="M30 3.6 32 6.6l-2.6 0.4z" fill="${PAL.steel}"/>`,

  /* ------------------------------------------------------------- monsters */
  ogre: (c) => `
    ${shadow}
    <path d="M9.6 18q0-7 6.8-7t6.8 7l1 10H8.6z" fill="${shade(PAL.hide, 0)}"/>
    <path d="M16.4 11q6.8 0 6.8 7l1 10h-7.8z" fill="${shade(PAL.hide, -0.2)}"/>
    <path d="M10 22h12.6l0.4 3H9.6z" fill="${c.team}"/>
    <path d="M16.4 22h6.2l0.4 3h-6.6z" fill="${c.dark}"/>
    <path d="M8.6 28h5.4l-0.4-3.4H9z" fill="${shade(PAL.hide, -0.28)}"/>
    <path d="M18.6 28h5.4l-0.6-3.4h-4.4z" fill="${shade(PAL.hide, -0.28)}"/>
    <circle cx="16" cy="8.6" r="4.4" fill="${PAL.hideLight}"/>
    <path d="M11.6 8.6a4.4 4.4 0 0 0 8.8 0z" fill="${PAL.hideDark}"/>
    <path d="M13.4 7.4h1.8v1.6h-1.8zM17 7.4h1.8v1.6H17z" fill="${PAL.ink}"/>
    <path d="M13.8 11.4h1.4v1.6h-1.4zM17 11.4h1.4v1.6H17z" fill="${PAL.plaster}"/>
    <path d="M4.2 8.6q3-3.4 6.4-1.6" stroke="${PAL.wood}" stroke-width="2.6" fill="none" stroke-linecap="round"/>
    <path d="M2 5.4q3.6-1.6 4.6 2.2Q4.2 9.4 2 5.4z" fill="${PAL.woodDark}"/>
    <circle cx="3.4" cy="6.2" r="0.9" fill="${PAL.stoneDark}"/>
    <path d="M22.4 14q4.4 1.4 4.4 6" stroke="${PAL.hide}" stroke-width="3.2" fill="none" stroke-linecap="round"/>`,

  dragon: (c) => `
    ${shadow}
    <path d="M14 12q-6-8-12-6 1.6 7 9 9z" fill="${c.light}"/>
    <path d="M14 12q-6-8-12-6 3.4 1 5.6 4.4Q9.6 13 14 12z" fill="${c.team}" opacity="0.7"/>
    <path d="M18 12q6-8 12-6-1.6 7-9 9z" fill="${c.light}"/>
    <path d="M18 12q6-8 12-6-3.4 1-5.6 4.4Q22.4 13 18 12z" fill="${c.team}" opacity="0.7"/>
    <path d="M11 18q0-6 5-6t5 6-2.6 8H13.6q-2.6-2-2.6-8z" fill="${c.team}"/>
    <path d="M16 12q5 0 5 6t-2.6 8H16z" fill="${c.dark}"/>
    <path d="M13.4 20h5.2l0.6 3h-6.4z" fill="${PAL.plaster}" opacity="0.75"/>
    <path d="M12.6 25.6 10 28.4h4.4zM19.4 25.6 22 28.4h-4.4z" fill="${c.dark}"/>
    <path d="M20.6 16.6q5 1 6.4 6.6-3.6-1.4-5-4.4z" fill="${c.dark}"/>
    <path d="M14.6 9.6q0-4.4 4-4.4 3.6 0 3.6 3.4 0 2.6-2.4 3.4z" fill="${c.team}"/>
    <path d="M18.6 5.2q3.6 0 3.6 3.4 0 2.6-2.4 3.4z" fill="${c.dark}"/>
    <path d="M22 7.6 26.4 6l-3.6 3.4z" fill="${PAL.plaster}"/>
    <circle cx="19.8" cy="7.8" r="0.9" fill="${PAL.gold}"/>
    <path d="M16.2 4.6 18 1.8l0.6 3.2zM19.4 4.4 21.6 2l-0.4 3z" fill="${c.dark}"/>`,

  /* ---------------------------------------------------------------- siege */
  ballista: (c) => `
    ${shadow}
    <circle cx="10.6" cy="24.4" r="4.2" fill="${PAL.woodDark}"/>
    <circle cx="10.6" cy="24.4" r="1.6" fill="${PAL.wood}"/>
    <circle cx="21.4" cy="24.4" r="4.2" fill="${PAL.woodDark}"/>
    <circle cx="21.4" cy="24.4" r="1.6" fill="${PAL.wood}"/>
    <path d="M6.6 20.4h19l-1.4 3.6H8z" fill="${PAL.wood}"/>
    <path d="M9 20.4 20 10.6l1.8 2L11 22.4z" fill="${PAL.woodDark}"/>
    <path d="M14.4 8.6q5 2.4 5 8.6" stroke="${PAL.wood}" stroke-width="2" fill="none" stroke-linecap="round"/>
    <path d="M25 8.6q-5 2.4-5 8.6" stroke="${PAL.wood}" stroke-width="2" fill="none" stroke-linecap="round"/>
    <path d="M14.4 8.6 25 8.6" stroke="${PAL.cloth}" stroke-width="0.9" opacity="0.85"/>
    <path d="M14 14.6h11.4" stroke="${PAL.steelDark}" stroke-width="1.6" stroke-linecap="round"/>
    <path d="M25.4 12.8 29.4 14.6l-4 1.8z" fill="${PAL.steel}"/>
    <rect x="6.4" y="6.4" width="1.2" height="14" fill="${PAL.woodDark}"/>
    <path d="M7.6 6.8h5.4l-1.7 2.4 1.7 2.4H7.6z" fill="${c.team}"/>
    <path d="M8.6 18.4h6v2.2h-6z" fill="${c.dark}"/>`,
};

/**
 * One unit, drawn by the best answer available for it.
 *
 * The definition rather than the id, because the last of the three answers needs
 * it. `sprites[type] ?? sprites.soldier` used to be that line, and it drew every
 * type nobody hand-drew as the same swordsman — including the campaign's supply
 * cart, which carries units in it.
 *
 * A picture rather than markup, because a generated unit is a spritesheet and a
 * drawn one is not, and that difference is the renderer's to act on.
 */
export function unitPicture(art: ArtDirection, unit: UnitDef, team: string): BoardPicture {
  const runtime = art.resolve((provider) => provider.unitPicture?.(unit.id, team));
  if (runtime !== null) return runtime;
  const colors = spriteColors(team);
  const sprite = sprites[unit.id];
  // Drawn rather than generated: a hand-drawn sprite has no frames, so nothing here
  // declares a strip and `play` on its drawing is silent.
  return {
    body: sprite
      ? `<g shape-rendering="crispEdges">${sprite(colors)}</g>`
      : unitFromRules(unit, colors),
  };
}

/** Standalone svg string, for palettes, menus and the recruit dialog. */
export function unitIcon(art: ArtDirection, unit: UnitDef, team: string, size = 32): string {
  const runtime = art.resolve((provider) => provider.unitIcon?.(unit.id, team, size));
  if (runtime !== null) return runtime;
  const picture = boardPictureMarkup(unitPicture(art, unit, team));
  return `<svg viewBox="0 0 32 32" width="${size}" height="${size}" shape-rendering="crispEdges">${picture}</svg>`;
}

