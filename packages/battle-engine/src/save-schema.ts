import { StoredDocumentError } from './domain/errors';

/** A persisted document that carries the version of the shape it was written in. */
export interface VersionedDocument {
  schema: number;
}

/** One step up the version ladder, on the raw document. */
export type SchemaMigration = (raw: Record<string, unknown>) => Record<string, unknown>;

/**
 * Explicit, sequential schema migration; never permissive best-effort loading.
 *
 * The ladder itself — walk from the version on disk to the current one, refuse a
 * gap, refuse a migration that fails to advance, refuse an unknown version — has
 * nothing to do with what the document holds. The campaign save had it written
 * out; a battle save needs the same ladder, and a second copy of a loop this
 * fiddly is how two save formats end up disagreeing about what "no migration
 * from schema 3" means.
 *
 * What a loaded document must additionally *satisfy* stays with whoever knows:
 * this class answers "is it the right shape version", not "is it usable here".
 */
export class SchemaMigrator<T extends VersionedDocument> {
  private readonly migrations = new Map<number, SchemaMigration>();

  constructor(
    /** Names the document in every refusal: `campaign save`, `battle save`. */
    readonly subject: string,
    readonly currentSchema: number,
  ) {}

  register(fromSchema: number, migrate: SchemaMigration): this {
    if (!Number.isInteger(fromSchema) || fromSchema < 0) {
      throw new StoredDocumentError('migration schema must be non-negative');
    }
    if (this.migrations.has(fromSchema)) {
      throw new StoredDocumentError(`${this.subject} migration ${fromSchema} already registered`);
    }
    this.migrations.set(fromSchema, migrate);
    return this;
  }

  /** The ladder itself, so an owner can hand its migrations to a copy. */
  registered(): ReadonlyMap<number, SchemaMigration> {
    return new Map(this.migrations);
  }

  /** Raw document in, current-schema document out. The copy is the caller's. */
  load(raw: unknown): T {
    if (!raw || typeof raw !== 'object' || Array.isArray(raw)) {
      throw new StoredDocumentError(`${this.subject} must be an object`);
    }
    let value = structuredClone(raw) as Record<string, unknown>;
    let schema = Number(value.schema);
    if (!Number.isInteger(schema) || schema < 0) throw new StoredDocumentError(`${this.subject} has invalid schema`);
    while (schema < this.currentSchema) {
      const migrate = this.migrations.get(schema);
      if (!migrate) throw new StoredDocumentError(`no ${this.subject} migration from schema ${schema}`);
      value = migrate(value);
      const next = Number(value.schema);
      if (!Number.isInteger(next) || next <= schema) {
        throw new StoredDocumentError(`${this.subject} migration ${schema} did not advance schema`);
      }
      schema = next;
    }
    if (schema !== this.currentSchema) {
      throw new StoredDocumentError(`unsupported ${this.subject} schema ${schema}`);
    }
    return value as unknown as T;
  }
}
