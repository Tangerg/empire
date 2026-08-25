/**
 * How generic art varies without being random.
 *
 * Four modules draw from what the rules can see — a tile, a unit, a structure, a
 * portrait — and all four need the same three things: a stable number derived
 * from a name, a way to choose one of several looks with it, and coordinates
 * short enough that the markup stays readable.
 *
 * All four had their own copy. The name hash appeared four times under three
 * names (`idHash`, `nameHash`, and a `hash` nested inside a drawing function),
 * `pick` four times, and `r2` four times — one of them spelling its parameter
 * `n` instead of `value`. Twelve declarations of three functions, in one
 * directory, and a change to how art varies would have had to find all of them.
 *
 * `tileHash` is the fourth member of this family and lives in the engine, because
 * it seeds from coordinates and coordinate determinism is the engine's. This
 * seeds from a content id, which is what keeps a terrain looking the same in
 * every level, every thumbnail and every editor swatch.
 */

/** A stable 0..1 from a name, so a thing always looks like itself. */
export function nameHash(name: string, salt: number): number {
  let hash = 0x811c9dc5 ^ salt;
  for (let index = 0; index < name.length; index++) {
    hash = Math.imul(hash ^ name.charCodeAt(index), 0x01000193) >>> 0;
  }
  return (hash >>> 8) / 0x01000000;
}

/** One of several looks, chosen by a 0..1 and never out of range. */
export const pick = <T>(choices: readonly T[], at: number): T =>
  choices[Math.floor(at * choices.length) % choices.length];

/** A coordinate rounded to hundredths: enough precision, half the characters. */
export const r2 = (value: number): number => Math.round(value * 100) / 100;
