import { GENERIC_PRESENTATION, type BattlePresentation } from './battle-presentation';
import type { ArtProvider } from './ports';

/**
 * Two providers under one name are refused, because order would decide silently.
 *
 * The presentation list was checked the same way, and is gone: an art direction
 * carries one scene, so there is no second copy to be shadowed.
 */
function requireDistinctIds(kind: string, entries: readonly { id: string }[]): void {
  const seen = new Set<string>();
  for (const entry of entries) {
    if (seen.has(entry.id)) throw new Error(`duplicate ${kind} "${entry.id}"`);
    seen.add(entry.id);
  }
}

/**
 * The art one shell was handed: providers in the order they are consulted, and
 * the one scene its battles are drawn in.
 *
 * This used to be two module-level mutable arrays that story packages pushed
 * themselves into, which made the theme a function of import order. It worked
 * only because exactly one pack exists: two packs that both answer for the same
 * unit id would resolve by whoever registered last, and nothing in the code said
 * so. Art is composed by the application root now, the same way its content
 * catalog and its ruleset are.
 *
 * The scene became singular for the same reason it became explicit. It was a list
 * searched by a `matches(levelId)` predicate, and the predicate was how a pack
 * declined levels the root had already given it.
 */
export class ArtDirection {
  constructor(
    readonly providers: readonly ArtProvider[] = [],
    readonly presentation: BattlePresentation = GENERIC_PRESENTATION,
  ) {
    requireDistinctIds('art provider', providers);
  }

  /**
   * First provider with an answer wins, which is why the order is the caller's.
   *
   * `null` is not an answer; empty is. A pack that has no opinion about a terrain
   * returns `null` and the floor draws it, and a pack whose painted scene has
   * already drawn the ground returns nothing at all — two different requests, and
   * the difference is the whole reason this returns `T | null` rather than a
   * falsy-or-not string. Callers must test for `null`, never for truthiness.
   */
  resolve<T>(select: (provider: ArtProvider) => T | null | undefined): T | null {
    for (const provider of this.providers) {
      const found = select(provider);
      if (found != null) return found;
    }
    return null;
  }
}

/** No theme art: plain shapes and the ruled board. */
export const GENERIC_ART = new ArtDirection();
