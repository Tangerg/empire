import { GENERIC_PRESENTATION, type BattlePresentation } from './battle-presentation';
import type { ArtProvider } from './ports';

/**
 * The art one shell was handed: providers in the order they are consulted, and
 * the battle presentations it may choose from.
 *
 * This used to be two module-level mutable arrays that story packages pushed
 * themselves into, which made the theme a function of import order. It worked
 * only because exactly one pack exists: two packs that both answer for the same
 * unit id would resolve by whoever registered last, and nothing in the code said
 * so. Art is composed by the application root now, the same way its content
 * catalog and its ruleset are.
 */
export class ArtDirection {
  constructor(
    readonly providers: readonly ArtProvider[] = [],
    readonly presentations: readonly BattlePresentation[] = [],
  ) {
    const seen = new Set<string>();
    for (const provider of providers) {
      if (seen.has(provider.id)) throw new Error(`duplicate art provider "${provider.id}"`);
      seen.add(provider.id);
    }
  }

  /** First provider with an answer wins, which is why the order is the caller's. */
  resolve<T>(select: (provider: ArtProvider) => T | null | undefined): T | null {
    for (const provider of this.providers) {
      const found = select(provider);
      if (found != null) return found;
    }
    return null;
  }

  /**
   * The presentation for one level.
   *
   * Painted scenes claim their own levels by `matches`; anything unclaimed gets
   * the generic look, which is the one that works over any art.
   */
  presentationFor(levelId: string): BattlePresentation {
    return this.presentations.find((presentation) => presentation.matches(levelId)) ?? GENERIC_PRESENTATION;
  }
}

/** No theme art: plain shapes and the ruled board. */
export const GENERIC_ART = new ArtDirection();
