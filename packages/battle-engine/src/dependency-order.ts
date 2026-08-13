/** A small, reusable topological planner for plugins and content packages. */
export interface DependencyOrderOptions<T> {
  idOf(item: T): string;
  dependenciesOf(item: T): readonly string[];
  /** Dependencies already supplied outside the current batch. */
  isSatisfiedExternally?(id: string): boolean;
  duplicate?(id: string): Error;
  missing(item: T, dependency: string): Error;
  cycle(path: readonly string[]): Error;
}

/**
 * Returns dependencies before their consumers while preserving insertion order
 * between unrelated items. The input collection is never mutated.
 */
export function orderByDependencies<T>(
  items: readonly T[],
  options: DependencyOrderOptions<T>,
): T[] {
  const byId = new Map<string, T>();
  for (const item of items) {
    const id = options.idOf(item);
    if (byId.has(id)) {
      throw options.duplicate?.(id) ?? new Error(`duplicate dependency node: "${id}"`);
    }
    byId.set(id, item);
  }
  const visiting = new Map<string, number>();
  const visited = new Set<string>();
  const path: string[] = [];
  const ordered: T[] = [];

  const visit = (item: T): void => {
    const id = options.idOf(item);
    if (visited.has(id)) return;
    const cycleStart = visiting.get(id);
    if (cycleStart !== undefined) {
      throw options.cycle([...path.slice(cycleStart), id]);
    }

    visiting.set(id, path.length);
    path.push(id);
    for (const dependency of options.dependenciesOf(item)) {
      const pending = byId.get(dependency);
      if (pending) {
        visit(pending);
      } else if (!options.isSatisfiedExternally?.(dependency)) {
        throw options.missing(item, dependency);
      }
    }
    path.pop();
    visiting.delete(id);
    visited.add(id);
    ordered.push(item);
  };

  for (const item of items) visit(item);
  return ordered;
}
