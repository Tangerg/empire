import type {
  PlayerState,
  ResourceAmount,
  ResourceId,
  ResourceTransaction,
  Unit,
  WeaponId,
} from './types';
import { DomainInvariantError } from './domain/errors';

/** Open subject family; plugins may declaration-merge additional holder kinds. */
export interface ResourceSubjectKindMap {
  player: { kind: 'player'; player: PlayerState };
  unit: { kind: 'unit'; unit: Unit };
  weapon: { kind: 'weapon'; unit: Unit; weapon: WeaponId };
}

export type ResourceSubject = ResourceSubjectKindMap[keyof ResourceSubjectKindMap];
export type ResourceSubjectKind = ResourceSubject['kind'];

export interface ResourceSnapshot {
  /** null means the account is unlimited. */
  current: number | null;
  /** null means no upper bound. */
  capacity: number | null;
}

export interface ResourceAccount {
  read(): ResourceSnapshot;
  write(current: number): void;
}

export interface ResourceAdapter<S extends ResourceSubject = ResourceSubject> {
  readonly id: ResourceId;
  readonly name: string;
  readonly subjectKind: S['kind'];
  readonly integer: boolean;
  /** Relative planning value for AI comparisons; it never changes legality. */
  readonly aiWeight: number;
  account(subject: S): ResourceAccount | null;
}

/** Registry of independent resource ports. Storage layout remains an adapter concern. */
export class ResourceAdapterRegistry {
  private readonly adapters = new Map<ResourceId, ResourceAdapter>();

  register<S extends ResourceSubject>(adapter: ResourceAdapter<S>): this {
    if (!adapter.id.trim()) throw new Error('resource id cannot be empty');
    if (this.adapters.has(adapter.id)) throw new Error(`resource adapter already registered: "${adapter.id}"`);
    this.adapters.set(adapter.id, adapter as ResourceAdapter);
    return this;
  }

  replace<S extends ResourceSubject>(adapter: ResourceAdapter<S>): this {
    this.adapters.set(adapter.id, adapter as ResourceAdapter);
    return this;
  }

  get(id: ResourceId): ResourceAdapter {
    const adapter = this.adapters.get(id);
    if (!adapter) throw new Error(`unknown resource: "${id}"`);
    return adapter;
  }

  /**
   * The adapter, if there is one. A registry that can say "no" should be
   * askable: without this the HUD reached for the throwing form and caught the
   * exception to print a fallback label, which is a query written as a crash.
   */
  tryGet(id: ResourceId): ResourceAdapter | undefined {
    return this.adapters.get(id);
  }

  ids(): ResourceId[] {
    return [...this.adapters.keys()];
  }

  clone(): ResourceAdapterRegistry {
    const copy = new ResourceAdapterRegistry();
    for (const adapter of this.adapters.values()) copy.register(adapter);
    return copy;
  }
}

/**
 * Generic resource application service. It owns clamping and spending rules;
 * individual adapters only expose where one resource account is stored.
 */
export class BattleResourceSystem {
  constructor(readonly adapters: ResourceAdapterRegistry) {}

  hasAccount(id: ResourceId, subject: ResourceSubject): boolean {
    const adapter = this.adapters.get(id);
    return adapter.subjectKind === subject.kind && adapter.account(subject as never) !== null;
  }

  inspect(id: ResourceId, subject: ResourceSubject): ResourceSnapshot {
    return this.requireAccount(id, subject).account.read();
  }

  balance(id: ResourceId, subject: ResourceSubject): number | null {
    return this.inspect(id, subject).current;
  }

  canSpend(id: ResourceId, subject: ResourceSubject, requested: number): boolean {
    const current = this.balance(id, subject);
    const amount = this.normalize(this.adapters.get(id), requested);
    return current === null || current >= amount;
  }

  canAfford(costs: readonly ResourceAmount[], subject: ResourceSubject): boolean {
    return [...sumAmounts(costs)].every(([resource, amount]) =>
      this.canSpend(resource, subject, amount));
  }

  spendAll(costs: readonly ResourceAmount[], subject: ResourceSubject): ResourceAmount[] {
    const totals = [...sumAmounts(costs)];
    const missing = totals.find(([resource, amount]) => !this.canSpend(resource, subject, amount));
    if (missing) {
      throw new DomainInvariantError(`insufficient resource "${missing[0]}": need ${missing[1]}`);
    }
    for (const [resource, amount] of totals) this.spend(resource, subject, amount);
    return totals.map(([resource, amount]) => ({ resource, amount }));
  }

  planningValue(amounts: readonly ResourceAmount[]): number {
    return amounts.reduce(
      (total, amount) => total + amount.amount * this.adapters.get(amount.resource).aiWeight,
      0,
    );
  }

  credit(id: ResourceId, subject: ResourceSubject, requested: number): number {
    const { adapter, account } = this.requireAccount(id, subject);
    const amount = this.normalize(adapter, requested);
    const snapshot = account.read();
    if (snapshot.current === null || amount <= 0) return 0;
    const next = snapshot.capacity === null
      ? snapshot.current + amount
      : Math.min(snapshot.capacity, snapshot.current + amount);
    account.write(next);
    return next - snapshot.current;
  }

  spend(id: ResourceId, subject: ResourceSubject, requested: number): number {
    const { adapter, account } = this.requireAccount(id, subject);
    const amount = this.normalize(adapter, requested);
    const snapshot = account.read();
    if (snapshot.current === null || amount <= 0) return 0;
    if (snapshot.current < amount) {
      throw new DomainInvariantError(`insufficient resource "${id}": need ${amount}`);
    }
    account.write(snapshot.current - amount);
    return amount;
  }

  clone(): BattleResourceSystem {
    return new BattleResourceSystem(this.adapters.clone());
  }

  private requireAccount(
    id: ResourceId,
    subject: ResourceSubject,
  ): { adapter: ResourceAdapter; account: ResourceAccount } {
    const adapter = this.adapters.get(id);
    if (adapter.subjectKind !== subject.kind) {
      throw new Error(`resource "${id}" requires ${adapter.subjectKind} subject, received ${subject.kind}`);
    }
    const account = adapter.account(subject as never);
    if (!account) throw new Error(`resource "${id}" is not available for this ${subject.kind}`);
    return { adapter, account };
  }

  private normalize(adapter: ResourceAdapter, requested: number): number {
    if (!Number.isFinite(requested)) throw new Error(`resource "${adapter.id}" amount must be finite`);
    const nonnegative = Math.max(0, requested);
    return adapter.integer ? Math.round(nonnegative) : nonnegative;
  }
}

export const FUNDS_RESOURCE = 'funds';
export const COMMAND_POINTS_RESOURCE = 'command_points';
export const MOMENTUM_RESOURCE = 'momentum';
export const WEAPON_USES_RESOURCE = 'weapon_uses';

export const playerResource = (player: PlayerState): ResourceSubjectKindMap['player'] => ({
  kind: 'player',
  player,
});

export const unitResource = (unit: Unit): ResourceSubjectKindMap['unit'] => ({ kind: 'unit', unit });

export const weaponResource = (
  unit: Unit,
  weapon: WeaponId,
): ResourceSubjectKindMap['weapon'] => ({ kind: 'weapon', unit, weapon });

export interface ResourceTransactionContext {
  player?: PlayerState;
  unit: Unit;
  weapon: WeaponId;
}

export function transactionSubject(
  transaction: ResourceTransaction,
  context: ResourceTransactionContext,
): ResourceSubject {
  switch (transaction.subject) {
    case 'player':
      if (!context.player) throw new Error(`resource "${transaction.resource}" requires a player context`);
      return playerResource(context.player);
    case 'unit':
      return unitResource(context.unit);
    case 'weapon':
      return weaponResource(context.unit, context.weapon);
  }
}

export function canAffordTransactions(
  resources: BattleResourceSystem,
  transactions: readonly ResourceTransaction[],
  context: ResourceTransactionContext,
): boolean {
  const totals = new Map<string, { transaction: ResourceTransaction; amount: number }>();
  for (const transaction of transactions) {
    const key = `${transaction.subject}\u0000${transaction.resource}`;
    const total = totals.get(key);
    if (total) total.amount += transaction.amount;
    else totals.set(key, { transaction, amount: transaction.amount });
  }
  return [...totals.values()].every(({ transaction, amount }) => {
    const subject = transactionSubject(transaction, context);
    return resources.hasAccount(transaction.resource, subject) &&
      resources.canSpend(transaction.resource, subject, amount);
  });
}

function sumAmounts(amounts: readonly ResourceAmount[]): Map<ResourceId, number> {
  const totals = new Map<ResourceId, number>();
  for (const amount of amounts) {
    totals.set(amount.resource, (totals.get(amount.resource) ?? 0) + amount.amount);
  }
  return totals;
}

function accountIn(accounts: Record<ResourceId, { current: number; capacity: number | null }>, id: ResourceId): ResourceAccount | null {
  const state = accounts[id];
  return state
    ? {
        read: () => ({ current: state.current, capacity: state.capacity }),
        write: (current) => { state.current = current; },
      }
    : null;
}

const fundsAdapter: ResourceAdapter<ResourceSubjectKindMap['player']> = {
    id: FUNDS_RESOURCE,
    name: '资金',
    subjectKind: 'player',
    integer: true,
    aiWeight: 1,
    account: ({ player }) => accountIn(player.resources, FUNDS_RESOURCE),
  };

const commandPointsAdapter: ResourceAdapter<ResourceSubjectKindMap['player']> = {
    id: COMMAND_POINTS_RESOURCE,
    name: '指挥点',
    subjectKind: 'player',
    integer: true,
    aiWeight: 8,
    account: ({ player }) => accountIn(player.resources, COMMAND_POINTS_RESOURCE),
  };

const momentumAdapter: ResourceAdapter<ResourceSubjectKindMap['unit']> = {
    id: MOMENTUM_RESOURCE,
    name: '气势',
    subjectKind: 'unit',
    integer: true,
    aiWeight: 2,
    account: ({ unit }) => accountIn(unit.resources, MOMENTUM_RESOURCE),
  };

const weaponUsesAdapter: ResourceAdapter<ResourceSubjectKindMap['weapon']> = {
    id: WEAPON_USES_RESOURCE,
    name: '武器次数',
    subjectKind: 'weapon',
    integer: true,
    aiWeight: 20,
    account: ({ unit, weapon }) => {
      const runtime = unit.weaponState[weapon];
      return runtime ? accountIn(runtime.resources, WEAPON_USES_RESOURCE) : null;
    },
  };

export const DefaultResourceAdapters = new ResourceAdapterRegistry()
  .register(fundsAdapter)
  .register(commandPointsAdapter)
  .register(momentumAdapter)
  .register(weaponUsesAdapter);

export const DefaultBattleResources = new BattleResourceSystem(DefaultResourceAdapters);
