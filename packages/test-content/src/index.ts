import {
  ContentPackInstaller,
  createContentCatalog,
  type ContentCatalog,
  type ContentPack,
} from '@empire/battle-engine';
import { COMMON_CONTENT_PACK } from '@empire/content-common';
import { ANCIENT_EMPIRES_CONTENT_PACK } from '@empire/content-ancient-empires';

/**
 * Test-only composition root.
 *
 * Suites build catalogs the same way an application does — create one, install
 * packs into it — rather than relying on a setup file to mutate ambient state.
 * That keeps a test's content explicit and, more importantly, keeps two suites
 * (or two catalogs inside one suite) from sharing a namespace.
 */
export function createTestCatalog(...extraPacks: readonly ContentPack[]): ContentCatalog {
  const catalog = createContentCatalog();
  new ContentPackInstaller(catalog).install(
    COMMON_CONTENT_PACK,
    ANCIENT_EMPIRES_CONTENT_PACK,
    ...extraPacks,
  );
  return catalog;
}

export * from './levels';
