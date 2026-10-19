import { createHash } from 'node:crypto';

/** Stable fingerprint for JSON-compatible review projections. */
export const reviewFingerprint = (value: unknown): string =>
  createHash('sha256').update(JSON.stringify(value)).digest('hex');
