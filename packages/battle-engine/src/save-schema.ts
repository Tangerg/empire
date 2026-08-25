import { StoredDocumentError } from './domain/errors';

/** A persisted document that carries the version of the shape it was written in. */
export interface VersionedDocument {
  schema: number;
}

/**
 * Owns one current-schema document read.
 *
 * During development there is no compatibility ladder: every authored and
 * persisted document must use the current schema. Format-specific validation
 * remains with the owner that knows the document; this shared boundary only
 * rejects a non-object or a different version and returns a clone it owns.
 */
export function readCurrentDocument<T extends VersionedDocument>(
  subject: string,
  currentSchema: number,
  raw: unknown,
): T {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) {
    throw new StoredDocumentError(`${subject} must be an object`);
  }
  const value = structuredClone(raw) as Record<string, unknown>;
  if (!Number.isInteger(value.schema)) throw new StoredDocumentError(`${subject} has invalid schema`);
  if (value.schema !== currentSchema) {
    throw new StoredDocumentError(`unsupported ${subject} schema ${String(value.schema)}`);
  }
  return value as unknown as T;
}
