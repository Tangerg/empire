import { byId } from './id-order';
import type { ContentCatalog } from './content-pack';
import { DomainInvariantError } from './domain/errors';

/** Serializable identity of every versioned input that defines battle rules. */
export interface BattleRulesetManifest {
  readonly plugins: Readonly<Record<string, number>>;
  readonly contentPacks: Readonly<Record<string, number>>;
}

export interface BattleRulesetIdentity {
  readonly content: ContentCatalog;
  readonly plugins: ReadonlyMap<string, number>;
}

const versionRecord = (entries: Iterable<readonly [string, number]>): Record<string, number> =>
  Object.freeze(Object.fromEntries([...entries].sort(([left], [right]) => byId(left, right))));

/** Captures the ruleset identity once, at the composition boundary. */
export function createBattleRulesetManifest(identity: BattleRulesetIdentity): BattleRulesetManifest {
  return Object.freeze({
    plugins: versionRecord(identity.plugins),
    contentPacks: versionRecord(
      identity.content.packVersions.all().map((pack) => [pack.id, pack.version] as const),
    ),
  });
}

/**
 * The id an anonymous factory override is composed under.
 *
 * Declared here, beside the check that refuses it, and called by the factory that
 * mints one. The prefix used to be a string literal in both files: whoever changed
 * the minted shape would have left the check quietly matching nothing, which is a
 * guard that passes because it stopped looking.
 */
export const anonymousOverrideId = (capability: string): string => `engine.override.${capability}`;

const ANONYMOUS_PREFIX = anonymousOverrideId('');

/**
 * Factory field overrides are intentionally anonymous conveniences for tests
 * and short-lived sandboxes. A persisted battle needs a stable plugin id and
 * version instead, otherwise a different rule value would carry the same
 * manifest on the next process.
 */
export function requirePersistentRuleset(manifest: BattleRulesetManifest): void {
  if (Object.keys(manifest.contentPacks).length === 0) {
    throw new DomainInvariantError(
      'ruleset has no versioned content pack; install authored content through ContentPackInstaller before persisting',
    );
  }
  const anonymous = Object.keys(manifest.plugins).find((id) => id.startsWith(ANONYMOUS_PREFIX));
  if (anonymous) {
    throw new DomainInvariantError(
      `ruleset contains unversioned override "${anonymous}"; persist it through a named, versioned plugin`,
    );
  }
}

function versionDifferences(
  family: string,
  expected: Readonly<Record<string, number>>,
  actual: Readonly<Record<string, number>>,
): string[] {
  const ids = [...new Set([...Object.keys(expected), ...Object.keys(actual)])].sort();
  return ids.flatMap((id) => {
    if (!(id in expected)) return [`${family} "${id}" is not part of the current ruleset`];
    if (!(id in actual)) return [`${family} "${id}" is missing`];
    if (expected[id] !== actual[id]) {
      return [`${family} "${id}" version is ${actual[id]}, expected ${expected[id]}`];
    }
    return [];
  });
}

/** Empty means the two documents describe the same versioned ruleset. */
export function rulesetDifferences(
  expected: BattleRulesetManifest,
  actual: BattleRulesetManifest,
): string[] {
  return [
    ...versionDifferences('plugin', expected.plugins, actual.plugins),
    ...versionDifferences('content pack', expected.contentPacks, actual.contentPacks),
  ];
}
