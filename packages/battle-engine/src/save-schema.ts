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

/** What one raw field has to be before anything is allowed to walk it. */
export type StoredFieldCheck = (value: unknown) => boolean;

/**
 * The shape of one aggregate a stored document is made of.
 *
 * `Record<keyof T, StoredFieldCheck>` is the whole point: the compiler refuses a
 * table that has not been taught a field the state grew. Depth stops where the
 * per-field checks of the owning format take over — those know what a unit or a
 * roster entry means, and this only knows that there is something there to ask
 * about.
 */
export type StoredShape<T> = Record<keyof T, StoredFieldCheck>;

/**
 * The words a raw field is described in, as one vocabulary.
 *
 * Both save formats had their own copy of these six, which is how the copies came
 * to disagree about the name of the last one: the battle save called it `orNull`
 * and the campaign save called it `nullable`. A shape check is not a rule either
 * format owns — it is what "this document is not a battle" means before anybody
 * is allowed to read a field — so it belongs with the document read.
 */
export const storedField = Object.freeze({
  array: ((value) => Array.isArray(value)) as StoredFieldCheck,
  object: ((value) =>
    typeof value === 'object' && value !== null && !Array.isArray(value)) as StoredFieldCheck,
  number: ((value) => typeof value === 'number' && Number.isFinite(value)) as StoredFieldCheck,
  integer: ((value) => Number.isInteger(value)) as StoredFieldCheck,
  string: ((value) => typeof value === 'string') as StoredFieldCheck,
  orNull: (check: StoredFieldCheck): StoredFieldCheck =>
    (value) => value === null || check(value),
});

/**
 * Every field of one aggregate checked, and the first bad one named.
 *
 * The refusal is the caller's because the two formats word theirs differently and
 * in different languages; what they must not word differently is which fields get
 * asked about. `null` means the aggregate itself was not there to walk.
 */
export function requireStoredShape<T>(
  value: unknown,
  shape: StoredShape<T>,
  refuse: (field: string | null) => never,
): void {
  if (!storedField.object(value)) refuse(null);
  const fields = value as Record<string, unknown>;
  for (const [field, check] of Object.entries(shape) as [string, StoredFieldCheck][]) {
    if (!check(fields[field])) refuse(field);
  }
}
