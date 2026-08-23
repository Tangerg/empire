import type { UnitDef } from '@empire/battle-engine';
import { PAL, shade, type SpriteColors } from './palette';

/**
 * A unit drawn from what the rules can see about it.
 *
 * The last of the three tables that answered an unfamiliar id with a specific
 * lie. Terrain answered "grass", structures answered with silence, and this one
 * answered `sprites[type] ?? sprites.soldier` — every type nobody hand-drew was
 * the same swordsman. In this repository that is `c01.supply-wagon`: a cart drawn
 * as an infantryman, in a campaign that carries units in it.
 *
 * `movementClass` and `armorClass` are open strings, so this may not look for
 * `mounted` or `flying`. What it can do is treat them as *families*: units that
 * move the same way stand on the same footing and units armoured the same way
 * wear the same plate. The specific shape a family gets is arbitrary; the
 * grouping is not, and the grouping is the part a player reads.
 *
 * Everything else is a fact: how heavily built, how far it goes, how many arms it
 * carries, whether it mends, whether it takes ground, whether it carries other
 * units, and whether standing in the way is its business at all.
 */

/** What the rules can say about a unit, and nothing else. */
interface UnitReading {
  /** 0..1, hit points and damage reduction together. */
  readonly build: number;
  /** 0..1, how far it goes in a turn. */
  readonly pace: number;
  /** How many weapons it carries. */
  readonly arms: number;
  /** It can mend. */
  readonly mends: boolean;
  /** It can take ground. */
  readonly takes: boolean;
  /** It carries other units. */
  readonly bears: boolean;
  /** Standing in the way is not its business. */
  readonly bystander: boolean;
}

/**
 * A typical soldier, for the scales to mean something.
 *
 * Not read from the catalog: the drawing has to be the same in a thumbnail, in
 * the editor palette and on the board, and those have no army in common.
 */
const TYPICAL_HP = 100;
const TYPICAL_PACE = 6;

function readUnit(unit: UnitDef): UnitReading {
  return {
    build: Math.min(1, (unit.maxHp / TYPICAL_HP) * 0.6 + unit.defense * 1.6),
    pace: Math.min(1, unit.movement / TYPICAL_PACE),
    arms: unit.weapons.length,
    mends: unit.abilities.includes('heal'),
    takes: unit.abilities.includes('capture'),
    bears: (unit.transport?.capacity ?? 0) > 0,
    bystander: unit.zoneOfControl === 0,
  };
}

/** A stable 0..1 from a name, so a class always looks like itself. */
function nameHash(name: string, salt: number): number {
  let hash = 0x811c9dc5 ^ salt;
  for (let index = 0; index < name.length; index++) {
    hash = Math.imul(hash ^ name.charCodeAt(index), 0x01000193) >>> 0;
  }
  return (hash >>> 8) / 0x01000000;
}

const pick = <T>(choices: readonly T[], at: number): T => choices[Math.floor(at * choices.length) % choices.length];

const r2 = (value: number) => Math.round(value * 100) / 100;

/**
 * The footing a whole movement class shares.
 *
 * A plate rather than a mount or a pair of wings: inventing a horse for a class
 * whose name this module is forbidden to read would be a guess dressed as a
 * fact. A counter under the figure says "these move alike" and claims nothing
 * else — and it doubles as the contact shadow that grounds the figure.
 */
const FOOTINGS: readonly ((tone: string) => string)[] = [
  (tone) => `<ellipse cx="16" cy="28" rx="8.4" ry="3" fill="${tone}"/>`,
  (tone) => `<rect x="7.6" y="25.4" width="16.8" height="5.2" rx="1" fill="${tone}"/>`,
  (tone) => `<path d="M16 24.8 25 28 16 31.2 7 28z" fill="${tone}"/>`,
  (tone) => `<path d="M11 25.2h10l4 2.8-4 2.8H11l-4-2.8z" fill="${tone}"/>`,
  (tone) => `<path d="M6.6 26.4h18.8l-2 3.2H8.6z" fill="${tone}"/>`,
];

/** The headgear a whole armour class shares. */
const HELMS: readonly ((crown: string, band: string) => string)[] = [
  (crown, band) => `<path d="M11.6 10.2a4.6 4.6 0 0 1 8.8 0z" fill="${crown}"/>
    <path d="M11.6 10.2h8.8v1.5h-8.8z" fill="${band}"/>`,
  (crown, band) => `<path d="M11.4 11.6q0-6 4.6-6t4.4 5.6l-1.6 1q-0.4-4-2.8-4t-3 3.8z" fill="${crown}"/>
    <path d="M12.4 11.4h6.6l-0.4 1.4h-5.8z" fill="${band}"/>`,
  (crown, band) => `<path d="M11.8 10.4q0-4.6 4.2-4.6t4.2 4.6z" fill="${crown}"/>
    <path d="M9.4 10.2h13.2l-0.8 1.6H10.2z" fill="${band}"/>`,
  (crown, band) => `<path d="M12 9.6h8v1.8h-8z" fill="${band}"/>
    <path d="M14.6 5.6h2.8l1 4h-4.8z" fill="${crown}"/>`,
  (crown, band) => `<circle cx="16" cy="8.4" r="4.4" fill="none" stroke="${crown}" stroke-width="1.4"/>
    <path d="M11.8 9.6h8.4v1.4h-8.4z" fill="${band}"/>`,
];

/** Steel families, so two armour classes are not the same grey. */
const PLATES = [PAL.steel, PAL.steelDark, PAL.stoneLight, PAL.rock, PAL.cloth] as const;

/**
 * Trim the unit's own name decides.
 *
 * Two types can be alike in every rule this module may read — a legion shield and
 * a rune shield are, and so are a stone golem and a cemetery colossus — and
 * drawing them identically is honest but useless: the player still has to tell
 * them apart. The name is the one thing left that distinguishes them.
 */
const TRIMS = [PAL.gold, PAL.stoneLight, PAL.leaf, PAL.roof, PAL.waterLight, PAL.plaster] as const;

/** A haft over the shoulder, one per weapon, up to two. */
function arms(count: number, plate: string): string {
  if (count <= 0) return '';
  const first = `<path d="M22.6 5.4 25.6 15l-1.8 0.6-3-9.4z" fill="${plate}"/>
    <path d="M24 14.6 25.6 15 22.8 22.6l-1.6-0.5z" fill="${PAL.woodDark}"/>`;
  if (count === 1) return first;
  return `${first}<path d="M8.4 20.6 4.6 24.2l-1.2-1.3 3.8-3.5z" fill="${plate}"/>`;
}

/**
 * The whole figure. Layers in the order the rules become visible: what it stands
 * on, what it is built of, what it carries, and what it is for.
 */
export function unitFromRules(unit: UnitDef, colors: SpriteColors): string {
  const reading = readUnit(unit);
  const footing = pick(FOOTINGS, nameHash(unit.movementClass, 1));
  const helm = pick(HELMS, nameHash(unit.armorClass, 2));
  const plate = pick(PLATES, nameHash(unit.armorClass, 3));
  const cloth = reading.bystander ? PAL.cloth : colors.team;
  const trim = pick(TRIMS, nameHash(unit.id, 5));
  const crest = r2(13.4 + nameHash(unit.id, 6) * 5);

  // Width from build, lean from pace: a heavy line unit is broad and square, a
  // fast one narrow and pitched forward.
  const half = r2(4 + reading.build * 2.4);
  const lean = r2(reading.pace * 2.2);
  const torso = `<path d="M${r2(16 - half)} 13h${r2(half * 2)}l${lean} 9H${r2(16 - half - lean * 0.4)}z" fill="${cloth}"/>
    <path d="M16 13h${half}l${lean} 9H16z" fill="${shade(cloth, -0.3)}"/>
    <rect x="${r2(16 - half - 0.6)}" y="20.4" width="${r2(half * 2 + 1.2)}" height="2" rx="0.8" fill="${PAL.woodDark}"/>
    <rect x="${r2(16 - half + 0.8)}" y="${crest}" width="2" height="2" fill="${trim}"/>`;

  const load = reading.bears
    ? `<rect x="3.4" y="15.6" width="9.4" height="7.4" rx="1.2" fill="${PAL.wood}" stroke="${PAL.woodDark}" stroke-width="0.8"/>
       <path d="M3.4 18.4h9.4M8.1 15.6v7.4" stroke="${PAL.woodDark}" stroke-width="0.8" opacity="0.7"/>
       <rect x="2.4" y="22.6" width="11.4" height="1.8" rx="0.8" fill="${PAL.woodDark}"/>`
    : '';

  // What it is for, at most one mark, in the order a player needs it.
  const purpose = reading.mends
    ? `<path d="M14.4 15.4h3.2v1.4h-3.2zM15.4 14.4h1.2v3.4h-1.2z" fill="${PAL.plaster}"/>`
    : reading.takes
      ? `<path d="M20.4 12.6h4.6l-1.4 1.9 1.4 1.9h-4.6z" fill="${colors.light}" stroke="${PAL.ink}" stroke-width="0.4"/>`
      : unit.vision > 1
        ? `<circle cx="19.4" cy="10.2" r="1.5" fill="none" stroke="${PAL.gold}" stroke-width="1"/>`
        : '';

  return `<g shape-rendering="crispEdges">
    ${footing(shade(colors.dark, -0.15))}
    ${load}
    ${torso}
    <circle cx="16" cy="10.4" r="3.8" fill="${PAL.skin}"/>
    <path d="M12.2 10.4a3.8 3.8 0 0 0 7.6 0z" fill="${PAL.skinDark}" opacity="0.35"/>
    ${helm(plate, shade(plate, -0.3))}
    <rect x="15.4" y="4.8" width="1.2" height="2.6" fill="${trim}"/>
    ${arms(reading.arms, plate)}
    ${purpose}
  </g>`;
}
