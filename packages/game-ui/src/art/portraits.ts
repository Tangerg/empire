import type { ArtDirection } from './direction';
import type { UnitDef, UnitTypeId } from '@empire/battle-engine';
import { PAL, shade, spriteColors, type SpriteColors } from './palette';
import { definitionKey } from './svg';
import { nameHash, pick } from './variation';

/**
 * Unit portraits (立绘) — 96x112 busts for the inspector panel. They share one
 * body/face template so the roster reads as a single set; class identity comes
 * from headgear, props and palette accents layered on top.
 */

const FRAME_W = 96;
const FRAME_H = 112;

const face = (cx: number, cy: number, r: number, opts: { brow?: string } = {}) => `
  <ellipse cx="${cx}" cy="${cy}" rx="${r}" ry="${r * 1.12}" fill="${PAL.skin}"/>
  <path d="M${cx - r} ${cy + 1}a${r} ${r * 1.12} 0 0 0 ${r * 2} 0z" fill="${PAL.skinDark}" opacity="0.22"/>
  <ellipse cx="${cx - r * 0.36}" cy="${cy - 1}" rx="${r * 0.13}" ry="${r * 0.18}" fill="${PAL.ink}"/>
  <ellipse cx="${cx + r * 0.36}" cy="${cy - 1}" rx="${r * 0.13}" ry="${r * 0.18}" fill="${PAL.ink}"/>
  <path d="M${cx - r * 0.6} ${cy - r * 0.42}q${r * 0.28} -${r * 0.22} ${r * 0.5} 0" stroke="${opts.brow ?? '#5b3d24'}" stroke-width="1.5" fill="none" stroke-linecap="round"/>
  <path d="M${cx + r * 0.1} ${cy - r * 0.42}q${r * 0.28} -${r * 0.22} ${r * 0.5} 0" stroke="${opts.brow ?? '#5b3d24'}" stroke-width="1.5" fill="none" stroke-linecap="round"/>
  <path d="M${cx - r * 0.26} ${cy + r * 0.5}q${r * 0.26} ${r * 0.22} ${r * 0.52} 0" stroke="${PAL.skinDark}" stroke-width="1.4" fill="none" stroke-linecap="round"/>`;

const shoulders = (c: SpriteColors, cloth = c.team) => `
  <path d="M18 112V96q0-18 30-18t30 18v16z" fill="${cloth}"/>
  <path d="M48 78q30 0 30 18v16H48z" fill="${shade(cloth, -0.22)}"/>
  <path d="M40 80h16l4 32H36z" fill="${shade(cloth, 0.2)}" opacity="0.55"/>`;

const collar = (color: string) => `<path d="M34 82q14 10 28 0l3 6q-16 11-34 0z" fill="${color}"/>`;

type Portrait = (c: SpriteColors) => string;

const portraits: Record<UnitTypeId, Portrait> = {
  soldier: (c) => `
    ${shoulders(c)}
    ${collar(PAL.steel)}
    <path d="M12 112q0-22 10-30l6 5q-8 8-8 25z" fill="${PAL.steelDark}"/>
    ${face(48, 52, 17)}
    <path d="M29 52a19 19 0 0 1 38 0v4H29z" fill="${PAL.steel}"/>
    <path d="M48 33a19 19 0 0 1 19 19v4H48z" fill="${PAL.steelDark}"/>
    <path d="M29 54h38v5H29z" fill="${shade(PAL.steel, -0.3)}"/>
    <path d="M46 34h4v-9a2 2 0 0 1 4 0v9" fill="none" stroke="${c.light}" stroke-width="3"/>
    <path d="M45 52h6v18h-6z" fill="${PAL.steel}" opacity="0.55"/>
    <path d="M74 112 66 60l6-2 10 54z" fill="${PAL.steel}"/>
    <path d="M74 112 70 86l4-1 8 27z" fill="${PAL.steelDark}"/>
    <rect x="62" y="56" width="16" height="5" rx="2" fill="${PAL.gold}" transform="rotate(-12 70 58)"/>`,

  archer: (c) => `
    ${shoulders(c)}
    ${collar(PAL.leafDark)}
    <path d="M6 108q14-46 42-46 30 0 42 46-12-34-42-34T6 108z" fill="${PAL.wood}" opacity="0.0"/>
    <path d="M84 6q16 30 0 100" stroke="${PAL.wood}" stroke-width="6" fill="none" stroke-linecap="round"/>
    <path d="M84 6q16 30 0 100" stroke="${PAL.woodDark}" stroke-width="2" fill="none" opacity="0.5"/>
    <path d="M84 8 86 104" stroke="${PAL.cloth}" stroke-width="1.6" opacity="0.8"/>
    ${face(46, 54, 16.5)}
    <path d="M27 56q0-24 19-24t19 22l-5 4q0-17-14-17t-14 17z" fill="${PAL.leaf}"/>
    <path d="M46 32q19 0 19 22l-5 4q0-17-14-17z" fill="${PAL.leafDark}"/>
    <path d="M62 30q10 4 6 16l-9-6z" fill="${PAL.leafDark}"/>
    <path d="M28 60q-6 14 2 22l6-4q-6-8-2-16z" fill="${PAL.leaf}" opacity="0.9"/>
    <path d="M20 112 30 74l5 2-8 36z" fill="${PAL.leafDark}" opacity="0.7"/>
    <g stroke="${PAL.woodDark}" stroke-width="2">
      <path d="M14 108 22 66M20 110 28 68"/>
    </g>
    <path d="M20 62l6-2 1 6-6 1z" fill="${PAL.steel}"/>`,

  rogue: (c) => `
    ${shoulders(c, c.dark)}
    <path d="M30 80q18 12 36 0l4 8q-22 14-44 0z" fill="${c.team}"/>
    ${face(48, 56, 16)}
    <path d="M30 60q0-28 18-28t18 26l-5 3q1-19-13-19t-13 21z" fill="${c.dark}"/>
    <path d="M48 32q18 0 18 26l-5 3q1-19-13-19z" fill="${shade(c.dark, -0.3)}"/>
    <path d="M33 52h30v9H33z" fill="${PAL.ink}" opacity="0.6"/>
    <ellipse cx="41" cy="56.5" rx="2.6" ry="2" fill="${PAL.gold}"/>
    <ellipse cx="55" cy="56.5" rx="2.6" ry="2" fill="${PAL.gold}"/>
    <path d="M64 36q9 5 5 15l-8-6z" fill="${shade(c.dark, -0.3)}"/>
    <path d="M78 112 66 72l6-2 12 42z" fill="${PAL.steel}"/>
    <path d="M78 112 72 92l4-1 8 21z" fill="${PAL.steelDark}"/>
    <rect x="62" y="68" width="12" height="4" rx="1.6" fill="${PAL.woodDark}"/>
    <path d="M16 112 24 84l5 2-6 26z" fill="${PAL.steel}" opacity="0.8"/>`,

  cleric: (c) => `
    ${shoulders(c, PAL.cloth)}
    <path d="M42 78h12l3 34H39z" fill="${c.team}"/>
    ${collar(c.team)}
    <ellipse cx="48" cy="26" rx="20" ry="6" fill="none" stroke="${PAL.gold}" stroke-width="3.4"/>
    ${face(48, 54, 16.5)}
    <path d="M30 54q0-22 18-22t18 22l-4 3q0-18-14-18t-14 18z" fill="${PAL.cloth}"/>
    <path d="M48 32q18 0 18 22l-4 3q0-18-14-18z" fill="${shade(PAL.cloth, -0.16)}"/>
    <path d="M31 60q-5 12 1 20h8q-6-8-3-18z" fill="${PAL.cloth}"/>
    <path d="M80 18v94" stroke="${PAL.wood}" stroke-width="5" stroke-linecap="round"/>
    <circle cx="80" cy="16" r="9" fill="${PAL.gold}"/>
    <circle cx="80" cy="16" r="4" fill="${PAL.plaster}"/>
    <path d="M76 8h8M80 4v8" stroke="${PAL.plaster}" stroke-width="0" />
    <path d="M40 92h16v4H40z" fill="${PAL.gold}" opacity="0.8"/>
    <path d="M46 86h4v16h-4z" fill="${PAL.gold}" opacity="0.8"/>`,

  mage: (c) => `
    ${shoulders(c)}
    ${collar(c.light)}
    ${face(48, 58, 15.5)}
    <path d="M32 60q0-6 3-9h26q3 3 3 9l-4 2q0-6-12-6t-12 6z" fill="${PAL.skinDark}" opacity="0.15"/>
    <path d="M28 52h40l-3 6H31z" fill="${c.dark}"/>
    <path d="M48 4 74 54H22z" fill="${c.team}"/>
    <path d="M48 4 74 54H48z" fill="${c.dark}"/>
    <circle cx="48" cy="38" r="4.4" fill="${PAL.gold}"/>
    <path d="M38 72q10 8 20 0l4 8q-14 10-28 0z" fill="${PAL.cloth}"/>
    <path d="M40 68q8 20 16 0-4 26-16 0z" fill="${PAL.cloth}" opacity="0.9"/>
    <path d="M42 70q6 26 12 0 0 30-12 0z" fill="${shade(PAL.cloth, -0.12)}"/>
    <path d="M84 30v82" stroke="${PAL.wood}" stroke-width="5" stroke-linecap="round"/>
    <circle cx="84" cy="26" r="10" fill="${PAL.waterLight}" opacity="0.9"/>
    <circle cx="80" cy="22" r="3.4" fill="#ffffff" opacity="0.85"/>
    <circle cx="84" cy="26" r="10" fill="none" stroke="${c.light}" stroke-width="1.6" opacity="0.8"/>`,

  knight: (c) => `
    ${shoulders(c)}
    <path d="M12 112q2-24 14-32l8 6q-10 8-12 26z" fill="${PAL.steel}"/>
    <path d="M84 112q-2-24-14-32l-8 6q10 8 12 26z" fill="${PAL.steel}"/>
    ${collar(PAL.steelDark)}
    <path d="M28 50a20 20 0 0 1 40 0v30q0 12-20 12t-20-12z" fill="${PAL.steel}"/>
    <path d="M48 30a20 20 0 0 1 20 20v30q0 12-20 12z" fill="${PAL.steelDark}"/>
    <path d="M28 56h40v10H28z" fill="${PAL.ink}" opacity="0.75"/>
    <path d="M44 50h8v42h-8z" fill="${shade(PAL.steel, -0.18)}"/>
    <g fill="${PAL.ink}" opacity="0.6">
      <rect x="34" y="72" width="10" height="3" rx="1.4"/>
      <rect x="52" y="72" width="10" height="3" rx="1.4"/>
      <rect x="34" y="78" width="10" height="3" rx="1.4"/>
      <rect x="52" y="78" width="10" height="3" rx="1.4"/>
    </g>
    <ellipse cx="38" cy="60.5" rx="3" ry="2.2" fill="${PAL.gold}"/>
    <ellipse cx="58" cy="60.5" rx="3" ry="2.2" fill="${PAL.gold}"/>
    <path d="M44 28h8v-8q0-6 8-8l2 5q-5 2-5 6v9" fill="none" stroke="${c.light}" stroke-width="4"/>
    <path d="M62 12q10-2 14 8-10 4-16-2z" fill="${c.light}"/>`,

  ogre: (c) => `
    <path d="M8 112V92q0-22 40-22t40 22v20z" fill="#8fa06a"/>
    <path d="M48 70q40 0 40 22v20H48z" fill="#7f9160"/>
    <path d="M28 88h40l4 24H24z" fill="${c.team}"/>
    <path d="M48 88h20l4 24H48z" fill="${c.dark}"/>
    <ellipse cx="48" cy="46" rx="24" ry="22" fill="#9cae76"/>
    <path d="M24 46a24 22 0 0 0 48 0z" fill="#7f9160"/>
    <path d="M30 34q8-6 14 0" stroke="#5f6d44" stroke-width="3" fill="none" stroke-linecap="round"/>
    <path d="M52 34q8-6 14 0" stroke="#5f6d44" stroke-width="3" fill="none" stroke-linecap="round"/>
    <rect x="33" y="40" width="8" height="7" rx="2" fill="${PAL.ink}"/>
    <rect x="55" y="40" width="8" height="7" rx="2" fill="${PAL.ink}"/>
    <path d="M38 58q10 6 20 0-4 8-10 8t-10-8z" fill="#5f6d44"/>
    <path d="M40 60h5v7h-5zM52 60h5v7h-5z" fill="${PAL.plaster}"/>
    <path d="M24 30q-8-8-4-18l6 2q-3 7 3 12z" fill="#7f9160"/>
    <path d="M72 30q8-8 4-18l-6 2q3 7-3 12z" fill="#7f9160"/>
    <path d="M4 96 30 62l8 6L14 104z" fill="${PAL.wood}"/>
    <path d="M22 60q12-14 22-2-8 12-22 2z" fill="${PAL.woodDark}"/>
    <circle cx="30" cy="58" r="3" fill="${PAL.stoneDark}"/>
    <circle cx="38" cy="64" r="2.4" fill="${PAL.stoneDark}"/>`,

  ballista: (c) => `
    <rect x="0" y="0" width="96" height="112" fill="none"/>
    <path d="M10 96h76l-6 14H16z" fill="${PAL.wood}"/>
    <path d="M48 96h38l-6 14H48z" fill="${PAL.woodDark}"/>
    <circle cx="26" cy="86" r="18" fill="${PAL.woodDark}"/>
    <circle cx="26" cy="86" r="12" fill="${PAL.wood}"/>
    <circle cx="26" cy="86" r="4" fill="${PAL.woodDark}"/>
    <g stroke="${PAL.woodDark}" stroke-width="3">
      <path d="M26 74v24M14 86h24M17 77l18 18M35 77 17 95"/>
    </g>
    <path d="M20 86 74 30l10 10L30 96z" fill="${PAL.wood}"/>
    <path d="M20 86 74 30l4 4L24 90z" fill="${shade(PAL.wood, 0.2)}"/>
    <path d="M40 10q22 10 22 42" stroke="${PAL.woodDark}" stroke-width="6" fill="none" stroke-linecap="round"/>
    <path d="M92 22q-22 10-22 42" stroke="${PAL.woodDark}" stroke-width="6" fill="none" stroke-linecap="round"/>
    <path d="M40 10 92 22" stroke="${PAL.cloth}" stroke-width="2.4"/>
    <path d="M36 40h52" stroke="${PAL.steelDark}" stroke-width="5" stroke-linecap="round"/>
    <path d="M86 32 100 40 86 48z" fill="${PAL.steel}"/>
    <rect x="6" y="8" width="4" height="60" fill="${PAL.woodDark}"/>
    <path d="M10 10h22l-7 9 7 9H10z" fill="${c.team}"/>
    <path d="M10 10h11l-7 9 7 9H10z" fill="${c.dark}"/>`,

  dragon: (c) => `
    <path d="M2 22q30-12 44 22-22 14-40-4z" fill="${c.light}"/>
    <path d="M2 22q30-12 44 22-16-24-44-22z" fill="${c.team}" opacity="0.6"/>
    <path d="M94 30q-28-6-40 26 22 12 38-6z" fill="${c.light}"/>
    <path d="M94 30q-28-6-40 26 14-22 40-26z" fill="${c.team}" opacity="0.6"/>
    <path d="M30 112q0-30 20-30t20 30z" fill="${c.team}"/>
    <path d="M50 82q20 0 20 30H50z" fill="${c.dark}"/>
    <path d="M40 96h20l3 16H37z" fill="${PAL.plaster}" opacity="0.7"/>
    <path d="M26 56q0-26 22-26 20 0 20 20 0 16-14 22l-6 14q-16-8-22-30z" fill="${c.team}"/>
    <path d="M48 30q20 0 20 20 0 16-14 22l-6 14z" fill="${c.dark}"/>
    <path d="M62 44 90 34 66 58z" fill="${PAL.plaster}"/>
    <path d="M62 44 90 34 74 46z" fill="${shade(PAL.plaster, -0.15)}"/>
    <ellipse cx="56" cy="48" rx="5" ry="6" fill="${PAL.gold}"/>
    <ellipse cx="56" cy="48" rx="1.8" ry="5" fill="${PAL.ink}"/>
    <path d="M36 26 40 8l8 16zM50 24 58 8l2 16z" fill="${c.dark}"/>
    <path d="M28 62q-8 10-4 24l8-4q-3-10 2-16z" fill="${c.dark}" opacity="0.85"/>
    <path d="M66 70q12 6 14 22l-10-2q-1-12-8-16z" fill="${c.dark}"/>`,
};

/**
 * A bust from what the rules can see: the shared template, with the collar and
 * headgear a whole armour class shares, a haft per weapon, and one mark for what
 * the unit is for.
 *
 * Same families as the board sprite, so a derived unit's portrait and its figure
 * are recognisably the same soldier.
 */
function portraitFromRules(unit: UnitDef, c: SpriteColors): string {
  const plate = pick([PAL.steel, PAL.steelDark, PAL.stoneLight, PAL.rock, PAL.cloth], nameHash(unit.armorClass, 3));
  const bystander = unit.zoneOfControl === 0;
  const cloth = bystander ? PAL.cloth : c.team;
  const crest = nameHash(unit.armorClass, 2);

  const headgear = crest < 0.2
    ? `<path d="M29 56a19 19 0 0 1 38 0v4H29z" fill="${plate}"/>
       <path d="M29 54h38v5H29z" fill="${shade(plate, -0.3)}"/>`
    : crest < 0.4
      ? `<path d="M27 58q0-26 21-26t21 24l-5 4q0-19-16-19t-16 19z" fill="${plate}"/>`
      : crest < 0.6
        ? `<path d="M28 54q0-20 20-20t20 20z" fill="${plate}"/>
           <path d="M20 53h56l-3 6H23z" fill="${shade(plate, -0.3)}"/>`
        : crest < 0.8
          ? `<path d="M31 50h34v6H31z" fill="${shade(plate, -0.25)}"/>
             <path d="M44 34h8l3 14H41z" fill="${plate}"/>`
          : `<ellipse cx="48" cy="46" rx="20" ry="19" fill="none" stroke="${plate}" stroke-width="4"/>`;

  // A haft over each shoulder, up to two, in the same idiom the drawn set uses.
  const hafts = (unit.weapons.length > 0
    ? `<path d="M74 112 66 58l6-2 10 56z" fill="${plate}"/>
       <path d="M74 112 70 86l4-1 8 27z" fill="${shade(plate, -0.3)}"/>`
    : '')
    + (unit.weapons.length > 1 ? `<path d="M18 112 26 76l5 2-7 34z" fill="${shade(plate, -0.15)}"/>` : '');

  const mark = unit.abilities.includes('heal')
    ? `<path d="M43 92h10v4H43zM46 89h4v10h-4z" fill="${PAL.plaster}"/>`
    : unit.abilities.includes('capture')
      ? `<path d="M56 88h14l-4 5 4 5H56z" fill="${c.light}" stroke="${PAL.ink}" stroke-width="1"/>`
      : (unit.transport?.capacity ?? 0) > 0
        ? `<path d="M30 90q18 10 36 0" stroke="${PAL.woodDark}" stroke-width="5" fill="none"/>`
        : unit.vision > 1
          ? `<circle cx="66" cy="52" r="5" fill="none" stroke="${PAL.gold}" stroke-width="2"/>`
          : '';

  // The name decides one accent, because two types can be alike in every rule
  // this reads and still need telling apart in the inspector.
  const trim = pick([PAL.gold, PAL.stoneLight, PAL.leaf, PAL.roof, PAL.waterLight, PAL.plaster], nameHash(unit.id, 5));
  return `${shoulders(c, cloth)}
    ${collar(shade(plate, -0.15))}
    <rect x="41" y="${Math.round(88 + nameHash(unit.id, 6) * 12)}" width="14" height="4" rx="1.5" fill="${trim}"/>
    ${face(48, 54, 17, { brow: shade(PAL.skinDark, -0.3) })}
    ${headgear}
    ${hafts}
    ${mark}`;
}

/**
 * The bust for a unit, drawn by the best answer available for it.
 *
 * `portraits[type] ?? portraits.soldier` was the same lie as the board sprite one
 * layer up: every type nobody drew wore the same face in the inspector. The
 * fallback is built from the shared template and what the rules can see.
 */
export function portraitMarkup(art: ArtDirection, unit: UnitDef, team: string): string {
  const provided = art.resolve((provider) => provider.portraitMarkup?.(unit.id, team));
  if (provided !== null) return provided;
  const c = spriteColors(team);
  const drawn = portraits[unit.id];
  const face = drawn ? drawn(c) : portraitFromRules(unit, c);
  const key = definitionKey(unit.id, team);
  return `
    <defs>
      <linearGradient id="pg-${key}" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stop-color="${shade(team, 0.55)}"/>
        <stop offset="1" stop-color="${shade(team, -0.15)}"/>
      </linearGradient>
      <clipPath id="pc-${key}"><rect x="0" y="0" width="${FRAME_W}" height="${FRAME_H}" rx="8"/></clipPath>
    </defs>
    <g clip-path="url(#pc-${key})">
      <rect width="${FRAME_W}" height="${FRAME_H}" fill="url(#pg-${key})"/>
      <circle cx="48" cy="46" r="40" fill="#ffffff" opacity="0.14"/>
      ${face}
    </g>
    <rect x="0.75" y="0.75" width="${FRAME_W - 1.5}" height="${FRAME_H - 1.5}" rx="8" fill="none" stroke="${shade(team, -0.4)}" stroke-width="1.5"/>`;
}

export function portraitSvg(art: ArtDirection, unit: UnitDef, team: string, width = 96): string {
  const height = Math.round((width / FRAME_W) * FRAME_H);
  return `<svg viewBox="0 0 ${FRAME_W} ${FRAME_H}" width="${width}" height="${height}" class="portrait">${portraitMarkup(
    art,
    unit,
    team,
  )}</svg>`;
}
