import { gaugeRatio, type BattlefieldMarker, type StructureDef, type StructureState } from '@empire/battle-engine';
import { GROUND_TONES, PAL, shade } from './palette';
import { nameHash, pick, r2 } from './variation';

/**
 * A structure and a battlefield marker drawn from what the rules can see.
 *
 * The generic art answered an unfamiliar structure with silence — the board
 * asked the presentation, got `null`, and drew nothing at all. Six structure
 * types and five marker kinds ship in this repository and every one of them was
 * invisible on a board with no painted scene.
 *
 * It is worse than invisible in one shipped chapter: `c01-15` places a 500 HP
 * `c01.mother-root` and makes destroying it the victory condition, and the
 * campaign's own art has no topic for that type — so the player is asked to
 * break something that is not drawn. A terrain of the same name *is* drawn, so
 * the cell shows silverwood ground and nothing standing on it.
 *
 * A picture is not optional for a thing the rules track. This is the floor: the
 * presentation answers first, and whatever it declines is drawn from the rules,
 * which is why these two functions are total.
 */


const RAISED_BY_COVER = { none: 0, half: 1.4, full: 2.8 } as const;

/** The condition bar every structure wears, in the same idiom a painted one uses. */
function conditionBar(ratio: number): string {
  return `<rect x="5" y="27" width="22" height="3" rx="1.5" fill="#201914" opacity="0.72"/>
    <rect x="5.5" y="27.5" width="${r2(21 * ratio)}" height="2" rx="1" fill="${ratio > 0.5 ? PAL.hpGood : PAL.hpLow}"/>`;
}

/** Cracks that open as a structure is broken down. */
function cracks(id: string, wear: number): string {
  const count = Math.round(wear * 3);
  let out = '';
  for (let index = 0; index < count; index++) {
    const x = r2(7 + nameHash(id, 40 + index) * 18);
    const y = r2(9 + nameHash(id, 50 + index) * 12);
    out += `<path d="M${x} ${y}l${r2(1.5 - nameHash(id, 60 + index) * 3)} 4l2 3" stroke="${PAL.ink}"
      stroke-width="1" fill="none" opacity="0.55"/>`;
  }
  return out;
}

/**
 * What the rules can say about a structure, drawn.
 *
 * Height comes from what it obstructs, mass from whether it stops movement, wear
 * from its hit points, and the owner's colour from who holds it. A structure
 * nothing may shoot wears no condition bar, because it has no condition to show.
 */
export function structureFromRules(
  state: StructureState,
  def: StructureDef,
  ownerColor?: string,
): string {
  const ratio = gaugeRatio(state.hp, def.maxHp);
  const wear = 1 - ratio;
  const raised = Math.max(def.obstructionHeight, RAISED_BY_COVER[def.cover]);
  const banner = ownerColor ?? PAL.neutral;
  // Masonry tone by name, as the derived terrain picks its ground tone: without
  // it two structures alike in every rule came out identically, which is the
  // same defect one step down — a gate that looks like a depot.
  const face = shade(pick(GROUND_TONES.stone, nameHash(state.type, 0)), state.disabled ? -0.32 : 0);
  const top = shade(face, 0.24);
  const side = shade(face, -0.3);
  const height = Math.min(23, 9 + raised * 4);
  const crown = r2(27 - height);
  const lean = nameHash(state.type, 1) > 0.5 ? 1 : -1;
  // Two details the name decides, so structures alike in every rule still differ:
  // how many courses a wall is laid in, and how broad a fixture's footing is.
  const courses = 3 + Math.round(nameHash(state.type, 2) * 2);
  const footing = r2(7.5 + nameHash(state.type, 3) * 3);

  // A wall fills its cell; a fixture stands on a footing in the middle of it.
  const body = def.blocksMovement
    ? `<path d="M2 27V${crown}h28v27z" fill="${face}" stroke="${PAL.ink}" stroke-width="0.8"/>
       <path d="M2 ${crown}h28l-2.6 2.6H4.6z" fill="${top}"/>
       <path d="M27.4 ${r2(crown + 2.6)}H30V27h-2.6z" fill="${side}"/>
       ${Array.from({ length: courses }, (_unused, row) => {
         const y = r2(crown + 3.4 + row * (height - 4) / courses);
         if (y > 26) return '';
         const offset = row % 2 === 0 ? 9 : 16;
         return `<path d="M3 ${y}h26" stroke="${side}" stroke-width="0.8" opacity="0.6"/>
           <path d="M${offset} ${y}v${r2((height - 4) / courses)}" stroke="${side}" stroke-width="0.8" opacity="0.5"/>`;
       }).join('')}`
    : `<ellipse cx="16" cy="27" rx="${r2(footing + 2)}" ry="2.6" fill="${PAL.ink}" opacity="0.26"/>
       <path d="M${r2(16 - footing)} 27V${r2(crown + 4)}q0-4 ${footing}-4t${footing} 4V27z" fill="${face}" stroke="${PAL.ink}" stroke-width="0.8"/>
       <ellipse cx="16" cy="${r2(crown + 4)}" rx="${footing}" ry="3" fill="${top}"/>
       <path d="M${r2(16 + lean * 5)} ${r2(crown + 3)}v-${r2(Math.max(3, height - 8))}" stroke="${side}" stroke-width="2.4"/>
       <circle cx="${r2(16 + lean * 5)}" cy="${crown}" r="2.6" fill="${banner}" stroke="${PAL.ink}" stroke-width="0.6"/>`;

  // Who holds it, on the face rather than on a pole: a wall cannot fly a flag
  // from the middle of the cell without covering whoever stands beside it.
  const held = `<rect x="2" y="${r2(Math.min(24, crown + height - 6))}" width="28" height="2.4"
    fill="${banner}" opacity="${def.blocksMovement ? 0.85 : 0}"/>`;

  return `${body}${held}${cracks(state.type, wear)}
    ${state.disabled ? `<path d="M8 ${r2(crown + 3)}l16 16m0-16-16 16" stroke="${PAL.ink}" stroke-width="2" opacity="0.5"/>` : ''}
    ${def.targetable ? conditionBar(ratio) : ''}`;
}

/**
 * A mark left on the ground, drawn from the only two things the rules say about
 * one: whose it was, and whether a unit fell here.
 *
 * `kind` is an open string — a pack invents `corpse`, `routed`, `surrendered` or
 * something nobody has named yet — so it decides the variation rather than the
 * meaning: two kinds always differ, and the same kind always looks the same.
 */
export function markerFromRules(marker: BattlefieldMarker, ownerColor?: string): string {
  const tint = ownerColor ?? PAL.neutral;
  const turn = r2(-30 + nameHash(marker.kind, 2) * 60);
  if (marker.fallenUnit) {
    // A body: slumped, face down, with the side's colour still on it.
    return `<g transform="rotate(${turn} 16 22)" opacity="0.82">
      <ellipse cx="16" cy="24" rx="9" ry="3" fill="${PAL.ink}" opacity="0.3"/>
      <path d="M9 23q3-4 8-3.6t6 3.6z" fill="${shade(tint, -0.28)}" stroke="${PAL.ink}" stroke-width="0.7"/>
      <circle cx="8.6" cy="21.4" r="2.6" fill="${PAL.skinDark}" stroke="${PAL.ink}" stroke-width="0.6"/>
      <path d="M21 20.6l5-3.4" stroke="${PAL.steelDark}" stroke-width="1.6" stroke-linecap="round"/>
    </g>`;
  }
  // Otherwise: a stake driven in where it happened, in the side's colour.
  return `<g transform="rotate(${turn} 16 24)" opacity="0.9">
    <ellipse cx="16" cy="25" rx="5.4" ry="1.8" fill="${PAL.ink}" opacity="0.28"/>
    <rect x="15.2" y="13" width="1.6" height="12" fill="${PAL.woodDark}"/>
    <path d="M16.8 13.4h6l-1.9 2.6 1.9 2.6h-6z" fill="${tint}" stroke="${PAL.ink}" stroke-width="0.5"/>
  </g>`;
}
