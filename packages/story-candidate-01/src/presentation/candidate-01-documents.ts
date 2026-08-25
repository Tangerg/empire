/**
 * How this pack reads its own generated JSON.
 *
 * The asset manifest, the tactical HD manifest and each authored scene are files
 * a tool writes and this package imports. They arrived as
 * `json as unknown as TheShapeIWanted` — three assertions covering the two things
 * TypeScript cannot infer from a JSON import: a closed set of names, and an array
 * that is meant to be a fixed number of numbers long. Everything *else* about
 * those documents the compiler checks against the file's own inferred type, and
 * the assertions were switching that off as well.
 *
 * So: two checks, stated once, at the edge where a document becomes a value. A
 * generator that renames a category or drops a coordinate is a loud failure on
 * import instead of a sprite that quietly never appears.
 */

/** One name a document gave, checked against the set it is allowed to be. */
export function oneOf<T extends string>(
  allowed: readonly T[],
  value: string,
  field: string,
  subject: string,
): T {
  const known = allowed.find((candidate) => candidate === value);
  if (known === undefined) {
    throw new Error(`candidate-01: "${subject}" has unknown ${field} "${value}"`);
  }
  return known;
}

/** A point, a size or a span a document gave, checked for being that long. */
export function fixedNumbers<N extends number>(
  length: N,
  value: readonly number[],
  field: string,
  subject: string,
): FixedNumbers<N> {
  if (value.length !== length || value.some((entry) => !Number.isFinite(entry))) {
    throw new Error(
      `candidate-01: "${subject}" has a ${field} of ${value.length} numbers, expected ${length}`,
    );
  }
  return value as FixedNumbers<N>;
}

/**
 * A readonly tuple of `N` numbers.
 *
 * The recursion is the only way to say it, and it stops at the arities this pack
 * actually asks for. `fixedNumbers` is the sole producer, and it has already
 * compared `value.length` to `length` — this type is that comparison's result
 * written down, not a claim made without one.
 */
export type FixedNumbers<N extends number, Built extends number[] = []> =
  Built['length'] extends N ? readonly [...Built] : FixedNumbers<N, [...Built, number]>;
