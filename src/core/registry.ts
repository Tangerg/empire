/**
 * Tiny content registry. Terrain, unit types and abilities all live in one of
 * these, which is what makes new content additive: call `define()` from a mod
 * file at boot and the rest of the engine picks it up.
 */
export class Registry<T extends { id: string }> {
  private readonly items = new Map<string, T>();

  constructor(private readonly label: string) {}

  define(def: T): T {
    if (this.items.has(def.id)) {
      throw new Error(`${this.label}: duplicate id "${def.id}"`);
    }
    this.items.set(def.id, def);
    return def;
  }

  defineAll(defs: readonly T[]): void {
    for (const d of defs) this.define(d);
  }

  /** Replace or patch an existing entry — handy for balance mods. */
  override(id: string, patch: Partial<T>): T {
    const cur = this.items.get(id);
    if (!cur) throw new Error(`${this.label}: cannot override unknown id "${id}"`);
    const next = { ...cur, ...patch, id } as T;
    this.items.set(id, next);
    return next;
  }

  get(id: string): T {
    const item = this.items.get(id);
    if (!item) throw new Error(`${this.label}: unknown id "${id}"`);
    return item;
  }

  tryGet(id: string): T | undefined {
    return this.items.get(id);
  }

  has(id: string): boolean {
    return this.items.has(id);
  }

  all(): T[] {
    return [...this.items.values()];
  }

  ids(): string[] {
    return [...this.items.keys()];
  }
}
