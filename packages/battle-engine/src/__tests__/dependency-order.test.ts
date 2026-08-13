import { describe, expect, it } from 'vitest';
import { orderByDependencies } from '../dependency-order';

interface Node {
  id: string;
  requires?: readonly string[];
}

const order = (nodes: readonly Node[], external: ReadonlySet<string> = new Set()) =>
  orderByDependencies(nodes, {
    idOf: (node) => node.id,
    dependenciesOf: (node) => node.requires ?? [],
    isSatisfiedExternally: (id) => external.has(id),
    missing: (node, dependency) => new Error(`${node.id} misses ${dependency}`),
    cycle: (path) => new Error(`cycle: ${path.join(' -> ')}`),
  });

describe('dependency order planner', () => {
  it('orders dependencies first and preserves unrelated insertion order', () => {
    const result = order([
      { id: 'feature', requires: ['foundation'] },
      { id: 'unrelated' },
      { id: 'foundation' },
    ]);

    expect(result.map((node) => node.id)).toEqual(['foundation', 'feature', 'unrelated']);
  });

  it('accepts explicitly satisfied external dependencies', () => {
    expect(order([{ id: 'addon', requires: ['installed'] }], new Set(['installed']))).toHaveLength(1);
  });

  it('reports the missing owner and the complete cycle path', () => {
    expect(() => order([{ id: 'addon', requires: ['missing'] }])).toThrow('addon misses missing');
    expect(() => order([
      { id: 'left', requires: ['right'] },
      { id: 'right', requires: ['left'] },
    ])).toThrow('left -> right -> left');
  });

  it('rejects ambiguous duplicate nodes', () => {
    expect(() => order([{ id: 'same' }, { id: 'same' }])).toThrow('duplicate dependency node');
  });
});
