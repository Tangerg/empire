import { DomainInvariantError } from './domain/errors';
/**
 * The mechanics every open extension point shares.
 *
 * A dozen registries had grown independently — action handlers, scenario
 * conditions, status behaviours, AI intents, resource adapters — and each had
 * hand-written the same four members. They drifted, and the drift was not
 * cosmetic: some could be *asked* whether they had an entry, some could only be
 * *told* to produce one and would throw, so callers who needed a question wrote
 * `try/catch` around the answer. Some listed their keys, some did not.
 *
 * Storage, duplicate rejection, the ask/demand pair and copying now live here
 * once, so a new extension point cannot be born missing half of them. What a
 * subclass adds is the part that is actually its own: the vocabulary its
 * entries are registered under, and what the registry *does* with them.
 */
export abstract class KeyedRegistry<K extends string, V> {
  private readonly entries = new Map<K, V>();

  /** Names the registry in its own error messages, e.g. `'action handler'`. */
  protected constructor(protected readonly subject: string) {}

  /** Where an entry keeps its key. The only thing storage needs to know. */
  protected abstract keyOf(entry: V): K;

  /**
   * Publish an entry. A second entry under the same key is a composition
   * mistake — two plugins claiming one rule — and silently keeping either one
   * would make behaviour depend on installation order.
   */
  register(entry: V): this {
    const key = this.keyOf(entry);
    if (this.entries.has(key)) {
      throw new DomainInvariantError(`${this.subject} already registered: "${key}"`);
    }
    this.entries.set(key, entry);
    return this;
  }

  /** Publish over any incumbent. This is how a mod swaps a built-in rule. */
  replace(entry: V): this {
    this.entries.set(this.keyOf(entry), entry);
    return this;
  }

  /** Demand an entry: absence is a defect in whoever composed the ruleset. */
  get(key: K): V {
    const entry = this.entries.get(key);
    if (!entry) throw new DomainInvariantError(`unknown ${this.subject} "${key}"`);
    return entry;
  }

  /** Ask for an entry. Every registry can answer this without an exception. */
  tryGet(key: K): V | undefined {
    return this.entries.get(key);
  }

  has(key: K): boolean {
    return this.entries.has(key);
  }

  all(): V[] {
    return [...this.entries.values()];
  }

  keys(): K[] {
    return [...this.entries.keys()];
  }

  get size(): number {
    return this.entries.size;
  }

  /**
   * Fill a fresh registry of the same shape. Subclasses expose this as their
   * own `clone()`, which is where their constructor arguments are decided.
   */
  protected copyInto<R extends KeyedRegistry<K, V>>(target: R): R {
    for (const entry of this.entries.values()) target.register(entry);
    return target;
  }
}

/** An entry that is consulted in a declared order rather than looked up. */
export interface PrioritizedEntry {
  readonly id: string;
  /** Lower goes first. */
  readonly priority: number;
}

/**
 * A registry whose entries are all consulted, in a declared order.
 *
 * Combat modifier providers and AI intents both work this way and both had
 * written the same sort by hand — with the same tie-break, but only one of them
 * remembering to cache it. The tie-break is the part that matters: breaking ties
 * by id keeps installation order out of the outcome, so two plugins registering
 * at the same priority cannot make a battle depend on module load order.
 */
export abstract class PriorityRegistry<V extends PrioritizedEntry> extends KeyedRegistry<string, V> {
  private orderedCache: readonly V[] | null = null;

  protected keyOf(entry: V): string {
    return entry.id;
  }

  override register(entry: V): this {
    this.orderedCache = null;
    return super.register(entry);
  }

  override replace(entry: V): this {
    this.orderedCache = null;
    return super.replace(entry);
  }

  ordered(): readonly V[] {
    return this.orderedCache ??= Object.freeze(
      this.all().sort((left, right) => left.priority - right.priority || left.id.localeCompare(right.id)),
    );
  }
}

/**
 * A catalog of content definitions — terrain, unit types, abilities, stances.
 *
 * Content earns its own subclass for one reason: definitions are *data*, so an
 * existing entry can be patched field by field rather than swapped whole, which
 * is what a balance mod actually wants to do.
 */
export class ContentRegistry<T extends { id: string }> extends KeyedRegistry<string, T> {
  constructor(label: string) {
    super(label);
  }

  protected keyOf(entry: T): string {
    return entry.id;
  }

  define(definition: T): T {
    this.register(definition);
    return definition;
  }

  defineAll(definitions: readonly T[]): void {
    for (const definition of definitions) this.define(definition);
  }

  /** Patch an existing entry. Its id is fixed: a patch cannot rename content. */
  override(id: string, patch: Partial<T>): T {
    const next = { ...this.get(id), ...patch, id } as T;
    this.replace(next);
    return next;
  }

  /** Ids in definition order. */
  ids(): string[] {
    return this.keys();
  }

  clone(): ContentRegistry<T> {
    return this.copyInto(new ContentRegistry<T>(this.subject));
  }
}
