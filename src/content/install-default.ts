import { installContentPacks } from '../core/content-pack';
import { ANCIENT_EMPIRES_CONTENT_PACK } from './ancient-empires';
import { COMMON_CONTENT_PACK } from './common';

/** Explicit application composition root; importing core alone installs nothing. */
export function installDefaultContent(): string[] {
  return installContentPacks(COMMON_CONTENT_PACK, ANCIENT_EMPIRES_CONTENT_PACK);
}
